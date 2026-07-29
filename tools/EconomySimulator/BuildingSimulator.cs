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
        var available=defs
            .Where(x=>x.TechLevel<=s.TechLevel)
            .Where(x=>x.RequiredResearch.All(s.CompletedResearch.Contains))
            .Where(x=>x.Generation.Count>0||x.ResearchPower>0||x.FoodProduction>0||
                x.ProductivityGranted>0||x.PopulationCapacity>0)
            .Select(d=>new{D=d,N=s.Buildings.GetValueOrDefault(d.Id)})
            .Where(x=>x.N<BuildingLimit(x.D,route))
            .Where(x=>ResourceSimulator.CanPay(s,ScaledCost(x.D,x.N)))
            .Where(x=>x.D.ProductivityConsumption<=AvailableProductivity(s,defs)+1e-9)
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
            return;
        }
        var cost=ScaledCost(cand.D,cand.N);
        ResourceSimulator.Pay(s,cost);
        s.Buildings[cand.D.Id]=cand.N+1;
        s.MarkAction("BuildingCompleted",cand.D.Id);
        s.Events[^1].Count=cand.N+1;
    }
    private static Dictionary<string,double> ScaledCost(Definition d,int owned){double factor=Math.Pow(Math.Max(1.0,d.CostGrowth),owned);return d.ResourceRequirements.ToDictionary(x=>x.Key,x=>x.Value*factor,StringComparer.OrdinalIgnoreCase);}
    public static double TotalProductivity(SimulationState s,IReadOnlyList<Definition> buildings)
    {
        double research=s.ActiveEffects.Where(x=>x.Type==7).Sum(x=>Math.Max(0,x.Value));
        double infrastructure=buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.ProductivityGranted));
        return Math.Max(0,s.Population+research+infrastructure);
    }
    public static double UsedProductivity(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.ProductivityConsumption)));
    public static double AvailableProductivity(SimulationState s,IReadOnlyList<Definition> buildings)=>
        Math.Max(0,TotalProductivity(s,buildings)-UsedProductivity(s,buildings));
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
