using System;
using System.Collections.Generic;
using System.Linq;
namespace Kingdom.EconomySimulation;
public static class BuildingSimulator
{
    public static void Decide(SimulationState s,IReadOnlyList<Definition> defs,Route route)
    {
        int gap=route switch{Route.Fast=>10,Route.Normal=>45,_=>270};
        if(s.Tick%gap!=0)
            return;
        if(TryUpgrade(s,defs))
        {
            s.CurrentProductivityWaitingSeconds=0d;
            return;
        }
        var unlocked=defs
            .Where(x=>x.TechLevel<=s.TechLevel)
            .Where(x=>x.RequiredResearch.All(s.CompletedResearch.Contains))
            .Where(x=>CanConstructNew(s,x,defs))
            .Where(x=>x.Generation.Count>0||x.ResearchPower>0||x.FoodProduction>0||
                x.ProductivityGranted>0||x.PopulationCapacity>0)
            .Select(d=>new{D=d,N=s.Buildings.GetValueOrDefault(d.Id)})
            .Where(x=>x.N<BuildingLimit(x.D,route))
            .Where(x=>ResourceSimulator.CanPay(s,ScaledCost(x.D,x.N)))
            .ToList();
        var available=unlocked
            .Where(x=>x.D.ProductivityConsumption<=AvailableProductivity(s,defs)+1e-9)
            .Where(x=>x.D.SpaceCost<=AvailableTerritory(s,defs)+1e-9)
            .ToList();
        double targetResearchPower=s.TechLevel switch
        {
            SimTechLevel.Animal=>route switch
            {
                Route.Fast=>4,
                Route.Normal=>3,
                _=>2
            },
            SimTechLevel.Neolithic=>route switch
            {
                Route.Fast=>25,
                Route.Normal=>20,
                _=>12
            },
            SimTechLevel.Medieval=>route switch
            {
                Route.Fast=>65,
                Route.Normal=>40,
                _=>35
            },
            _=>100
        };
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
                .ThenByDescending(x=>Score(x.D,route))
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
            return;
        }
        s.CurrentProductivityWaitingSeconds=0d;
        var cost=ScaledCost(cand.D,cand.N);
        ResourceSimulator.Pay(s,cost);
        s.Buildings[cand.D.Id]=cand.N+1;
        s.MarkAction("BuildingCompleted",cand.D.Id);
        s.Events[^1].Count=cand.N+1;
    }
    private static bool TryUpgrade(SimulationState s,IReadOnlyList<Definition> defs)
    {
        foreach(var source in defs.Where(x=>!string.IsNullOrEmpty(x.UpgradeTo)))
        {
            int sourceCount=s.Buildings.GetValueOrDefault(source.Id);
            if(sourceCount<=0)
                continue;
            var target=defs.FirstOrDefault(x=>
                string.Equals(x.Id,source.UpgradeTo,StringComparison.OrdinalIgnoreCase));
            if(target==null||target.TechLevel>s.TechLevel||
                !target.RequiredResearch.All(s.CompletedResearch.Contains))
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
            result[resource]=targetCost-sourceCost*0.8d;
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
                !next.RequiredResearch.All(s.CompletedResearch.Contains))
                break;
            highest=next;
        }
        return highest==building;
    }
    private static Dictionary<string,double> ScaledCost(Definition d,int owned){double factor=Math.Pow(Math.Max(1.0,d.CostGrowth),owned);return d.ResourceRequirements.ToDictionary(x=>x.Key,x=>x.Value*factor,StringComparer.OrdinalIgnoreCase);}
    public static double TotalProductivity(SimulationState s,IReadOnlyList<Definition> buildings)
    {
        double research=s.ActiveEffects.Where(x=>x.Type==7).Sum(x=>Math.Max(0,x.Value));
        double infrastructure=buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.ProductivityGranted));
        return Math.Max(0,s.Population*2d+research+infrastructure);
    }
    public static double UsedProductivity(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.ProductivityConsumption)));
    public static double AvailableProductivity(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,TotalProductivity(s,buildings)-UsedProductivity(s,buildings));
    public static double TotalTerritory(SimulationState s)=>
        500d+s.ActiveEffects.Where(x=>x.Type==8).Sum(x=>Math.Max(0,x.Value));
    public static double UsedTerritory(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.SpaceCost)));
    public static double AvailableTerritory(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,TotalTerritory(s)-UsedTerritory(s,buildings));
    private static int BuildingLimit(Definition d,Route route)
    {
        if(d.PopulationCapacity>0)
            return route==Route.Conservative?12:20;
        if(d.ProductivityGranted>0)
            return route==Route.Conservative?6:10;
        return route==Route.Conservative?2:3;
    }
    private static double Score(Definition d,Route r){double p=d.ResearchPower*100+d.Generation.Values.Sum()*100-d.Consumption.Values.Sum()*10+d.ProductivityGranted*40+d.PopulationCapacity*5;return r==Route.Fast?p+50:d.FoodProduction>0?p+20:p;}
}
