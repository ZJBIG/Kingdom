using System;
using System.Collections.Generic;
using System.Linq;
namespace Kingdom.EconomySimulation;
public static class BuildingSimulator
{
    public static void Decide(
        SimulationState s,
        EconomySnapshot snapshot,
        ISimulationStrategy strategy)
    {
        IReadOnlyList<Definition> defs=snapshot.Buildings;
        if(!s.ShouldDecide(ref s.LastBuildingDecisionSeconds,
                strategy.BuildingDecisionInterval))
            return;
        double gap = strategy.BuildingDecisionInterval;
        if(TryUpgrade(s,defs))
        {
            s.CurrentProductivityWaitingSeconds=0d;
            s.TraceDecision(strategy.Route,"Building","Upgraded",
                s.Events[^1].Id,"eligible upgrade chain selected");
            return;
        }
        Definition[] unlockedDefinitions = GetCandidates(s, defs);
        var unlocked=unlockedDefinitions
            .Select(d=>new{D=d,N=s.Buildings.GetValueOrDefault(d.Id)})
            .Where(x=>x.N<strategy.BuildingLimit(x.D))
            .Where(x=>ResourceSimulator.CanPay(s,ScaledCost(x.D,x.N)))
            .ToList();
        var available=unlocked
            .Where(x=>x.D.ProductivityConsumption<=AvailableProductivity(s,defs)+1e-9)
            .Where(x=>x.D.SpaceCost<=AvailableTerritory(s,defs)+1e-9)
            .ToList();
        double targetResearchPower=strategy.TargetResearchPower(s.TechLevel);
        double currentResearchPower=ResearchSimulator.ResearchPower(s,defs,defs);
        var cand=(currentResearchPower<targetResearchPower
                ?available.Where(x=>x.D.ResearchPower>0)
                    .OrderByDescending(x=>x.D.ResearchPower)
                    .ThenBy(x=>x.N)
                    .FirstOrDefault()
                :null)
            ??available
                .Where(x=>x.D.ResearchPower<=0||
                    x.D.Generation.Count>0||
                    x.D.FoodProduction>0)
                .OrderBy(x=>x.N>0?1:0)
                .ThenByDescending(x=>strategy.ScoreBuilding(x.D,x.N))
                .ThenBy(x=>x.D.TechLevel)
                .ThenBy(x=>x.D.Id,StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        if(cand==null)
        {
            s.BuildingWaitingSeconds+=gap;
            if(unlocked.Any(x=>
                x.D.ProductivityConsumption>AvailableProductivity(s,defs)+1e-9))
            {
                s.ProductivityWaitingSeconds+=gap;
                s.CurrentProductivityWaitingSeconds+=gap;
                s.MaximumProductivityWaitingSeconds=Math.Max(
                    s.MaximumProductivityWaitingSeconds,
                    s.CurrentProductivityWaitingSeconds);
            }
            else
            {
                s.CurrentProductivityWaitingSeconds=0d;
            }
            string reason=unlockedDefinitions.Length==0
                ?"no unlocked and affordable useful building"
                :available.Count==0
                    ?"productivity or territory is insufficient"
                    :"no strategy candidate";
            s.TraceDecision(strategy.Route,"Building","Waiting","",reason);
            return;
        }
        s.CurrentProductivityWaitingSeconds=0d;
        var cost=ScaledCost(cand.D,cand.N);
        ResourceSimulator.Pay(s,cost);
        s.Buildings[cand.D.Id]=cand.N+1;
        s.MarkAction("BuildingCompleted",cand.D.Id);
        s.Events[^1].Count=cand.N+1;
        s.TraceDecision(strategy.Route,"Building","Constructed",cand.D.Id,
            $"score={strategy.ScoreBuilding(cand.D,cand.N):0.###}; count={cand.N+1}");
    }

    private static Definition[] GetCandidates(
        SimulationState state,
        IReadOnlyList<Definition> definitions)
    {
        if (state.CachedBuildingCandidates != null &&
            state.CachedBuildingRevision == state.DefinitionRevision)
            return state.CachedBuildingCandidates;

        state.CachedBuildingCandidates = definitions
            .Where(x=>x.TechLevel<=state.TechLevel)
            .Where(x=>x.RequiredResearch.All(state.CompletedResearch.Contains))
            .Where(x=>x.RequiredWorkshop.All(state.PurchasedWorkshop.Contains))
            .Where(x=>CanConstructNew(state,x,definitions))
            .Where(x=>x.Generation.Count>0||x.ResearchPower>0||x.FoodProduction>0||
                x.PowerProduction>0||x.LogisticsProduction>0||
                x.ProductivityGranted>0||x.PopulationCapacity>0)
            .ToArray();
        state.CachedBuildingRevision = state.DefinitionRevision;
        return state.CachedBuildingCandidates;
    }
    private static bool TryUpgrade(SimulationState s,IReadOnlyList<Definition> defs)
    {
        if (!s.UpgradePairsInitialized)
        {
            var byId = defs.ToDictionary(x => x.Id,
                StringComparer.OrdinalIgnoreCase);
            foreach (Definition source in defs)
                if (!string.IsNullOrEmpty(source.UpgradeTo) &&
                    byId.TryGetValue(source.UpgradeTo, out Definition? target))
                    s.UpgradePairs.Add((source, target));
            s.UpgradePairsInitialized = true;
        }
        foreach ((Definition source, Definition target) in s.UpgradePairs)
        {
            int sourceCount=s.Buildings.GetValueOrDefault(source.Id);
            if(sourceCount<=0)
                continue;
            if(target.TechLevel>s.TechLevel||
                !target.RequiredResearch.All(s.CompletedResearch.Contains)||
                !target.RequiredWorkshop.All(s.PurchasedWorkshop.Contains))
                continue;
            var cost=UpgradeCost(source,target,sourceCount,s.Buildings.GetValueOrDefault(target.Id));
            if(!ResourceSimulator.CanPay(s,cost)||
                !CanUpgradeProductivity(s,defs,source,target)||
                !CanUpgradeTerritory(s,defs,source,target))
                continue;
            ResourceSimulator.Pay(s,cost);
            s.Buildings[source.Id]=sourceCount-1;
            int targetCount=s.Buildings.GetValueOrDefault(target.Id)+1;
            s.Buildings[target.Id]=targetCount;
            s.MarkAction("BuildingUpgrade",$"{source.Id}->{target.Id}");
            s.Events[^1].Count=targetCount;
            s.Events[^1].Detail="recovery=0.8";
            return true;
        }
        return false;
    }
    private static Dictionary<string,double> UpgradeCost(Definition source,Definition target,int sourceCount,int targetCount)
    {
        var result=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        foreach(string resource in source.ResourceRequirements.Keys.Concat(target.ResourceRequirements.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            double sourceCost=source.ResourceRequirements.GetValueOrDefault(resource)*
                Math.Pow(Math.Max(1d,source.CostGrowth),sourceCount-1);
            double targetCost=target.ResourceRequirements.GetValueOrDefault(resource)*
                Math.Pow(Math.Max(1d,target.CostGrowth),targetCount);
            result[resource]=EconomySimulationParity.UpgradeCostDelta(
                source.ResourceRequirements.GetValueOrDefault(resource),
                source.CostGrowth,sourceCount,
                target.ResourceRequirements.GetValueOrDefault(resource),
                target.CostGrowth,targetCount,.8d);
        }
        return result;
    }
    private static bool CanUpgradeProductivity(SimulationState s,IReadOnlyList<Definition> defs,Definition source,Definition target)
    {
        double preMargin=TotalProductivity(s,defs)-UsedProductivity(s,defs);
        double postTotalWithoutTargetGrant=TotalProductivity(s,defs)-Math.Max(0,source.ProductivityGranted);
        double postUsed=UsedProductivity(s,defs)-Math.Max(0,source.ProductivityConsumption)+Math.Max(0,target.ProductivityConsumption);
        return postTotalWithoutTargetGrant-postUsed>=Math.Min(0,preMargin)-1e-9;
    }
    private static bool CanUpgradeTerritory(
        SimulationState s,
        IReadOnlyList<Definition> defs,
        Definition source,
        Definition target) =>
        UsedTerritory(s,defs)-Math.Max(0,source.SpaceCost)+
            Math.Max(0,target.SpaceCost)<=TotalTerritory(s)+1e-9;
    private static bool CanConstructNew(SimulationState s,Definition building,IReadOnlyList<Definition> defs)
    {
        Definition root=building;
        Definition? predecessor;
        while((predecessor=defs.FirstOrDefault(x=>
            string.Equals(x.UpgradeTo,root.Id,StringComparison.OrdinalIgnoreCase)))!=null)
            root=predecessor;
        if(root==building&&string.IsNullOrEmpty(root.UpgradeTo))
            return true;
        Definition highest=root;
        while(!string.IsNullOrEmpty(highest.UpgradeTo))
        {
            Definition? next=defs.FirstOrDefault(x=>
                string.Equals(x.Id,highest.UpgradeTo,StringComparison.OrdinalIgnoreCase));
            if(next==null||next.TechLevel>s.TechLevel||
                !next.RequiredResearch.All(s.CompletedResearch.Contains)||
                !next.RequiredWorkshop.All(s.PurchasedWorkshop.Contains))
                break;
            highest=next;
        }
        return highest==building;
    }
    private static Dictionary<string,double> ScaledCost(Definition d,int owned)=>
        d.ResourceRequirements.ToDictionary(x=>x.Key,x=>
            EconomySimulationParity.GeometricUnitCost(
                x.Value,d.CostGrowth,owned),StringComparer.OrdinalIgnoreCase);
    public static double TotalProductivity(SimulationState s,IReadOnlyList<Definition> buildings)
    {
        double research=s.ActiveEffects.Where(
            x=>x.Kind==SimEffectKind.ProductivityGranted).Sum(x=>Math.Max(0,x.Value));
        double infrastructure=buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.ProductivityGranted));
        double populationMultiplier = ResourceSimulator.EffectMultiplier(
            s,
            SimEffectKind.PopulationProductivityMultiplier,
            "");
        return Math.Max(0,s.Population*2d*populationMultiplier+research+infrastructure);
    }
    public static double UsedProductivity(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.ProductivityConsumption)));
    public static double AvailableProductivity(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,TotalProductivity(s,buildings)-UsedProductivity(s,buildings));
    public static double TotalTerritory(SimulationState s)=>
        500d+s.ActiveEffects.Where(
            x=>x.Kind==SimEffectKind.TerritoryGranted).Sum(x=>Math.Max(0,x.Value));
    public static double UsedTerritory(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.SpaceCost)));
    public static double AvailableTerritory(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,TotalTerritory(s)-UsedTerritory(s,buildings));
}
