using System;
using System.Collections.Generic;
using System.Linq;
namespace Kingdom.EconomySimulation;
public static class ResourceSimulator
{
    public static void Tick(
        SimulationState s,
        IReadOnlyList<Definition> buildings,
        IReadOnlyList<Definition> defs,
        double deltaSeconds)
    {
        var net=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase){["WoodLog"]=1}; Add(s.Resources,"WoodLog",deltaSeconds); double foodIn=5,foodOut=s.Population*0.8d,powerIn=0,powerOut=0,logIn=0,logOut=0;
        foreach(var d in buildings){int n=s.Buildings.GetValueOrDefault(d.Id);if(n<=0)continue;double e=Efficiency(s,d,buildings,deltaSeconds);double foodMultiplier=1;foreach(var fx in CompletedEffects(s,defs,2,d.Id))foodMultiplier*=fx.Value;foreach(var p in d.Generation)Add(net,p.Key,p.Value*n*e);foreach(var p in d.Consumption)Add(net,p.Key,-p.Value*n*e);foodIn+=d.FoodProduction*n*e*foodMultiplier;foodOut+=d.FoodConsumption*n;powerIn+=d.PowerProduction*n*e;powerOut+=d.PowerConsumption*n;logIn+=d.LogisticsProduction*n*e;logOut+=d.LogisticsConsumption*n;}
        s.PowerSatisfaction=powerOut<=0?1:Math.Clamp(powerIn/powerOut,0,1);s.LogisticsSatisfaction=logOut<=0?1:Math.Clamp(logIn/logOut,0,1); double foodSat=foodOut<=0?1:Math.Clamp((s.Food+foodIn*deltaSeconds)/(foodOut*deltaSeconds),0,1);
        s.FoodSatisfaction=foodSat;
        double globalProduction=1;foreach(var e in CompletedEffects(s,defs,12,""))globalProduction*=e.Value;
        foreach(var d in buildings){int n=s.Buildings.GetValueOrDefault(d.Id);if(n<=0)continue;double e=Efficiency(s,d,buildings,deltaSeconds)*s.PowerSatisfaction*s.LogisticsSatisfaction*foodSat;double buildingProduction=globalProduction;foreach(var fx in CompletedEffects(s,defs,1,d.Id))buildingProduction*=fx.Value;foreach(var p in d.Generation){double resourceProduction=1;foreach(var fx in CompletedEffects(s,defs,3,p.Key))resourceProduction*=fx.Value;Add(s.Resources,p.Key,p.Value*n*e*buildingProduction*resourceProduction*deltaSeconds);}foreach(var p in d.Consumption)Add(s.Resources,p.Key,-p.Value*n*e*deltaSeconds);}
        s.Food=Math.Clamp(s.Food+(foodIn-foodOut)*deltaSeconds,0,s.FoodCapacity);
        double populationCapacity=buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.PopulationCapacity));
        double departureAllowance=Math.Floor(Math.Max(
            0,
            BuildingSimulator.TotalProductivity(s,buildings)-
            BuildingSimulator.UsedProductivity(s,buildings)));
        AdvancePopulation(
            s,
            populationCapacity,
            foodSat,
            PopulationGrowthMultiplier(s),
            departureAllowance,
            deltaSeconds);
        foreach(var k in s.Resources.Keys.ToList()){s.Resources[k]=Math.Max(0,s.Resources[k]);s.Minimums[k]=Math.Min(s.Minimums.GetValueOrDefault(k,double.MaxValue),s.Resources[k]);}
        if(s.Resources.Values.Any(x=>x<=1e-9))s.ZeroSeconds+=deltaSeconds;
    }
    public static void AdvancePopulation(
        SimulationState s,
        double populationCapacity,
        double foodSatisfaction,
        double growthMultiplier,
        double departureAllowance,
        double deltaSeconds)
    {
        populationCapacity=Math.Max(0,Math.Floor(populationCapacity+1e-9));
        int previousRelation=Compare(s.Population,s.PopulationCapacity);
        int nextRelation=Compare(s.Population,populationCapacity);
        if(previousRelation!=nextRelation)
            s.PopulationChangeProgress=0;
        s.PopulationCapacity=populationCapacity;
        if(Math.Abs(s.Population-populationCapacity)<=1e-9)
        {
            s.PopulationChangeProgress=0;
            return;
        }
        if(s.Population<populationCapacity)
        {
            if(foodSatisfaction<=0)
                return;
            double occupancy=populationCapacity<=0d
                ?1d
                :Math.Clamp(s.Population/populationCapacity,0d,1d);
            double effectivePopulation=Math.Max(1d,s.Population);
            double logisticRate=Math.Max(0d,growthMultiplier)/60d*
                effectivePopulation*(1d-occupancy);
            double accumulated=s.PopulationChangeProgress+
                Math.Clamp(foodSatisfaction,0,1)*logisticRate*deltaSeconds;
            double births=Math.Min(
                Math.Floor(accumulated+1e-9),
                populationCapacity-s.Population);
            s.Population+=births;
            s.PopulationChangeProgress=s.Population+1e-9>=populationCapacity
                ?0
                :Math.Max(0,accumulated-births);
            return;
        }

        double safeDepartures=Math.Floor(Math.Max(0,departureAllowance)+1e-9);
        if(safeDepartures<1)
        {
            s.PopulationChangeProgress=0;
            return;
        }

        double excess=s.Population-populationCapacity;
        double normalizedExcess=populationCapacity<=0d
            ?excess
            :excess/populationCapacity;
        double departureMultiplier=1d+Math.Min(8d,Math.Max(0d,normalizedExcess));
        double departureProgress=s.PopulationChangeProgress+
            deltaSeconds/60d*departureMultiplier;
        double departures=Math.Min(
            Math.Min(s.Population-populationCapacity,safeDepartures),
            Math.Floor(departureProgress+1e-9));
        s.Population-=departures;
        bool blockedByCapacity=s.Population<=populationCapacity+1e-9;
        bool blockedByProductivity=
            departures>=safeDepartures&&s.Population>populationCapacity+1e-9;
        s.PopulationChangeProgress=blockedByCapacity||blockedByProductivity
            ?0
            :Math.Max(0,departureProgress-departures);
    }
    private static int Compare(double population,double capacity)=>
        population<capacity-1e-9?-1:population>capacity+1e-9?1:0;
    private static double Efficiency(SimulationState s,Definition d,IReadOnlyList<Definition> all,double deltaSeconds){double e=1;foreach(var p in d.Consumption){double demand=p.Value*s.Buildings.GetValueOrDefault(d.Id);if(demand>0)e=Math.Min(e,(s.Resources.GetValueOrDefault(p.Key)+p.Value*deltaSeconds)/(demand*deltaSeconds));}return Math.Clamp(e,0,1);}
    private static IEnumerable<SimEffect> CompletedEffects(SimulationState s,IReadOnlyList<Definition> all,int type,string target)=>s.ActiveEffects.Where(e=>e.Type==type&&(string.IsNullOrEmpty(e.Target)||e.Target.Equals(target,StringComparison.OrdinalIgnoreCase)));
    public static double PopulationGrowthMultiplier(SimulationState s)
    {
        double result=1d;
        foreach(SimEffect effect in s.ActiveEffects.Where(x=>x.Type==17))
            result*=effect.Value>0d?effect.Value:1d;
        return result;
    }
    public static double PopulationGrowthRatePerMinute(SimulationState s)
    {
        if(s.Population>=s.PopulationCapacity || s.FoodSatisfaction<=0d)
            return 0d;
        double occupancy=s.PopulationCapacity<=0d
            ?1d
            :Math.Clamp(s.Population/s.PopulationCapacity,0d,1d);
        return Math.Clamp(s.FoodSatisfaction,0d,1d)*
            Math.Max(1d,s.Population)*
            PopulationGrowthMultiplier(s)*(1d-occupancy);
    }
    public static double PopulationDepartureRatePerMinute(
        SimulationState s,
        double departureAllowance)
    {
        if(s.Population<=s.PopulationCapacity ||
            Math.Floor(Math.Max(0d,departureAllowance)+1e-9)<1d)
            return 0d;
        double excess=s.Population-s.PopulationCapacity;
        double normalizedExcess=s.PopulationCapacity<=0d
            ?excess
            :excess/s.PopulationCapacity;
        return 1d+Math.Min(8d,Math.Max(0d,normalizedExcess));
    }
    public static double Get(SimulationState s,string id)=>s.Resources.GetValueOrDefault(id);
    public static bool CanPay(SimulationState s,IDictionary<string,double> cost)=>cost.All(p=>Get(s,p.Key)+1e-9>=p.Value);
    public static void Pay(SimulationState s,IDictionary<string,double> cost){foreach(var p in cost)Add(s.Resources,p.Key,-p.Value);}
    public static void Add(Dictionary<string,double> d,string k,double v){d[k]=d.GetValueOrDefault(k)+v;}
}
