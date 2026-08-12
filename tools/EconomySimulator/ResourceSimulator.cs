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
        foreach(var d in buildings){int n=s.Buildings.GetValueOrDefault(d.Id);if(n<=0)continue;double e=Efficiency(s,d,buildings,deltaSeconds);double foodMultiplier=EffectMultiplier(s,SimEffectKind.BuildingFoodProductionMultiplier,d.Id);foreach(var p in d.Generation)Add(net,p.Key,p.Value*n*e);foreach(var p in d.Consumption)Add(net,p.Key,-p.Value*n*e);foodIn+=d.FoodProduction*n*e*foodMultiplier;foodOut+=d.FoodConsumption*n;powerIn+=d.PowerProduction*n*e*EffectMultiplier(s,SimEffectKind.BuildingPowerProductionMultiplier,d.Id);powerOut+=d.PowerConsumption*n;logIn+=d.LogisticsProduction*n*e*EffectMultiplier(s,SimEffectKind.BuildingLogisticsProductionMultiplier,d.Id);logOut+=d.LogisticsConsumption*n;}
        double foodAvailability=EconomySimulationParity.CalculateSatisfaction(s.Food,foodIn,foodOut,deltaSeconds);
        double happinessMultiplier=CalculateHappinessMultiplier(foodIn-foodOut,s.Population,foodAvailability);
        double happinessReward=Math.Max(1d,happinessMultiplier);
        double happinessConstraint=Math.Min(1d,happinessMultiplier);
        powerIn*=happinessReward*EffectMultiplier(s,SimEffectKind.PowerMultiplier,"");
        logIn*=happinessReward*EffectMultiplier(s,SimEffectKind.GlobalLogisticsMultiplier,"");
        s.FoodAvailability=foodAvailability;s.HappinessMultiplier=happinessMultiplier;
        s.PowerSatisfaction=EconomySimulationParity.CalculateFlowSatisfaction(powerIn,powerOut);s.LogisticsSatisfaction=EconomySimulationParity.CalculateFlowSatisfaction(logIn,logOut);
        double globalProduction=EffectMultiplier(s,SimEffectKind.GlobalBuildingProductionMultiplier,"");
        foreach(var d in buildings){int n=s.Buildings.GetValueOrDefault(d.Id);if(n<=0)continue;double e=EconomySimulationParity.CalculateEffectiveEfficiency(1d,Efficiency(s,d,buildings,deltaSeconds),happinessConstraint,d.PowerConsumption>0?s.PowerSatisfaction:1d,d.LogisticsConsumption>0?s.LogisticsSatisfaction:1d);double buildingProduction=globalProduction*EffectMultiplier(s,SimEffectKind.BuildingProductionMultiplier,d.Id);foreach(var p in d.Generation){double resourceProduction=EffectMultiplier(s,SimEffectKind.ResourceProductionMultiplier,p.Key);Add(s.Resources,p.Key,p.Value*n*e*buildingProduction*resourceProduction*happinessReward*deltaSeconds);}foreach(var p in d.Consumption)Add(s.Resources,p.Key,-p.Value*n*e*deltaSeconds);}
        double foodCapacityMultiplier=EffectMultiplier(s,SimEffectKind.FoodCapacityMultiplier,"");
        s.FoodCapacity=Math.Max(500d,500d+buildings.Sum(x=>s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.FoodCapacity))*foodCapacityMultiplier);
        s.Food=Math.Min(s.FoodCapacity,EconomySimulationParity.AdvanceStockpile(s.Food,foodIn,foodOut,deltaSeconds));
        double populationCapacity=buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.PopulationCapacity));
        double departureAllowance=Math.Floor(Math.Max(
            0,
            BuildingSimulator.TotalProductivity(s,buildings)-
            BuildingSimulator.UsedProductivity(s,buildings)));
        AdvancePopulation(
            s,
            populationCapacity,
            happinessMultiplier,
            PopulationGrowthMultiplier(s),
            departureAllowance,
            deltaSeconds);
        foreach(var k in s.Resources.Keys.ToList()){s.Resources[k]=Math.Max(0,s.Resources[k]);s.Minimums[k]=Math.Min(s.Minimums.GetValueOrDefault(k,double.MaxValue),s.Resources[k]);}
        if(s.Resources.Values.Any(x=>x<=1e-9))s.ZeroSeconds+=deltaSeconds;
    }
    public static void AdvancePopulation(
        SimulationState s,
        double populationCapacity,
        double happinessMultiplier,
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
            if(happinessMultiplier<=0)
                return;
            double occupancy=populationCapacity<=0d
                ?1d
                :Math.Clamp(s.Population/populationCapacity,0d,1d);
            double effectivePopulation=Math.Max(1d,s.Population);
            double logisticRate=Math.Max(0d,growthMultiplier)/60d*
                effectivePopulation*(1d-occupancy);
            double accumulated=s.PopulationChangeProgress+
                Math.Max(0d,happinessMultiplier)*logisticRate*deltaSeconds;
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
    private static double Efficiency(
        SimulationState s,
        Definition d,
        IReadOnlyList<Definition> all,
        double deltaSeconds)
    {
        double efficiency = 1d;
        int count = s.Buildings.GetValueOrDefault(d.Id);
        foreach (var pair in d.Consumption)
        {
            double perSecond = pair.Value;
            double demand = perSecond * count;
            if (perSecond <= 0d || demand <= 0d)
                continue;

            // 这里仍是独立模拟器的 double 快照，不是游戏运行时的 ExpantaNum。
            // demand 或 demand * deltaSeconds 溢出后，不能再计算 Infinity / Infinity。
            // 生产补偿恒等于 perSecond / (perSecond * count)，直接化简为 1 / count。
            double inventory = s.Resources.GetValueOrDefault(pair.Key);
            double inventoryRatio = double.IsFinite(demand) &&
                double.IsFinite(deltaSeconds) &&
                demand <= double.MaxValue / Math.Max(1d, deltaSeconds)
                ? inventory / Math.Max(1d, demand * deltaSeconds)
                : 0d;
            double productionRatio = 1d / Math.Max(1, count);
            double available = inventoryRatio + productionRatio;
            efficiency = Math.Min(efficiency, Math.Clamp(available, 0d, 1d));
        }
        return efficiency;
    }
    private static IEnumerable<SimEffect> CompletedEffects(SimulationState s,SimEffectKind kind,string target)=>s.ActiveEffects.Where(e=>e.Kind==kind&&(string.IsNullOrEmpty(e.Target)||e.Target.Equals(target,StringComparison.OrdinalIgnoreCase)));
    internal static double EffectMultiplier(SimulationState s,SimEffectKind kind,string target)
    {
        double result=1d;
        foreach(SimEffect effect in CompletedEffects(s,kind,target))
            result=Math.Max(1d,result+(effect.Value>0d?effect.Value:1d)-1d);
        return result;
    }
    public static double PopulationGrowthMultiplier(SimulationState s)
    {
        double result=1d;
        foreach(SimEffect effect in s.ActiveEffects.Where(
                    x=>x.Kind==SimEffectKind.PopulationGrowthMultiplier))
            result=Math.Max(1d,result+(effect.Value>0d?effect.Value:1d)-1d);
        return result;
    }

    public static double CalculateHappinessMultiplier(
        double foodNetRate,
        double population,
        double foodAvailability=1d)
    {
        double availability=Math.Clamp(foodAvailability,0d,1d);
        if(availability<1d)
            return availability;
        double surplusPerPerson = Math.Max(0d, foodNetRate) /
            Math.Max(1d, population);
        double score = Math.Log10(1d + surplusPerPerson);
        if (double.IsNaN(score) || score < 0d)
            return 1d;
        return 1d + 0.5d * score / (score + 1d);
    }

    public static double CalculateFoodNetRate(
        SimulationState s,
        IReadOnlyList<Definition> buildings,
        double deltaSeconds)
    {
        double foodIn = 5d;
        double foodOut = s.Population * 0.8d;
        foreach (Definition d in buildings)
        {
            int n = s.Buildings.GetValueOrDefault(d.Id);
            if (n <= 0)
                continue;
            double efficiency = Efficiency(s, d, buildings, deltaSeconds);
            double foodMultiplier = EffectMultiplier(
                s,
                SimEffectKind.BuildingFoodProductionMultiplier,
                d.Id);
            foodIn += d.FoodProduction * n * efficiency * foodMultiplier;
            foodOut += d.FoodConsumption * n;
        }
        return foodIn - foodOut;
    }
    public static double PopulationGrowthRatePerSecond(SimulationState s)
    {
        if(s.Population>=s.PopulationCapacity || s.HappinessMultiplier<=0d)
            return 0d;
        double occupancy=s.PopulationCapacity<=0d
            ?1d
            :Math.Clamp(s.Population/s.PopulationCapacity,0d,1d);
        return Math.Max(0d,s.HappinessMultiplier)*
            Math.Max(1d,s.Population)*
            PopulationGrowthMultiplier(s)*(1d-occupancy);
    }
    public static double PopulationDepartureRatePerSecond(
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
    public static void Add(Dictionary<string,double> d,string k,double v)
    {
        if(double.IsNaN(v))
            throw new InvalidOperationException($"资源变化量不是有效数字：{k}。 ");
        double current=d.GetValueOrDefault(k);
        if(v>0d && (double.IsPositiveInfinity(current)||
            current>double.MaxValue-v))
        {
            d[k]=double.MaxValue;
            return;
        }
        if(v<0d && double.IsPositiveInfinity(current))
        {
            d[k]=double.MaxValue;
            return;
        }
        double next=current+v;
        if(double.IsPositiveInfinity(next))
            next=double.MaxValue;
        if(double.IsNaN(next))
            throw new InvalidOperationException($"资源变化后不是有效数字：{k}。 ");
        d[k]=next;
    }
}
