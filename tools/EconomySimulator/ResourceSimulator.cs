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
        var net=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase){["WoodLog"]=1}; Add(s.Resources,"WoodLog",deltaSeconds); double foodIn=5,foodOut=s.Population,powerIn=0,powerOut=0,logIn=0,logOut=0;
        foreach(var d in buildings){int n=s.Buildings.GetValueOrDefault(d.Id);if(n<=0)continue;double e=Efficiency(s,d,buildings,deltaSeconds);double foodMultiplier=1;foreach(var fx in CompletedEffects(s,defs,2,d.Id))foodMultiplier*=fx.Value;foreach(var p in d.Generation)Add(net,p.Key,p.Value*n*e);foreach(var p in d.Consumption)Add(net,p.Key,-p.Value*n*e);foodIn+=d.FoodProduction*n*e*foodMultiplier;foodOut+=d.FoodConsumption*n;powerIn+=d.PowerProduction*n*e;powerOut+=d.PowerConsumption*n;logIn+=d.LogisticsProduction*n*e;logOut+=d.LogisticsConsumption*n;}
        s.PowerSatisfaction=powerOut<=0?1:Math.Clamp(powerIn/powerOut,0,1);s.LogisticsSatisfaction=logOut<=0?1:Math.Clamp(logIn/logOut,0,1); double foodSat=foodOut<=0?1:Math.Clamp((s.Food+foodIn*deltaSeconds)/(foodOut*deltaSeconds),0,1);
        double globalProduction=1;foreach(var e in CompletedEffects(s,defs,12,""))globalProduction*=e.Value;
        foreach(var d in buildings){int n=s.Buildings.GetValueOrDefault(d.Id);if(n<=0)continue;double e=Efficiency(s,d,buildings,deltaSeconds)*s.PowerSatisfaction*s.LogisticsSatisfaction*foodSat;double buildingProduction=globalProduction;foreach(var fx in CompletedEffects(s,defs,1,d.Id))buildingProduction*=fx.Value;foreach(var p in d.Generation){double resourceProduction=1;foreach(var fx in CompletedEffects(s,defs,3,p.Key))resourceProduction*=fx.Value;Add(s.Resources,p.Key,p.Value*n*e*buildingProduction*resourceProduction*deltaSeconds);}foreach(var p in d.Consumption)Add(s.Resources,p.Key,-p.Value*n*e*deltaSeconds);}
        s.Food=Math.Clamp(s.Food+(foodIn-foodOut)*deltaSeconds,0,s.FoodCapacity);
        double populationCapacity=buildings.Sum(x=>
            s.Buildings.GetValueOrDefault(x.Id)*Math.Max(0,x.PopulationCapacity));
        if(s.Population+1e-9<populationCapacity&&foodSat>0)
        {
            s.PopulationGrowthProgress+=foodSat*deltaSeconds/60d;
            if(s.PopulationGrowthProgress>=1)
            {
                s.Population+=1;
                s.PopulationGrowthProgress=0;
            }
        }
        foreach(var k in s.Resources.Keys.ToList()){s.Resources[k]=Math.Max(0,s.Resources[k]);s.Minimums[k]=Math.Min(s.Minimums.GetValueOrDefault(k,double.MaxValue),s.Resources[k]);}
        if(s.Resources.Values.Any(x=>x<=1e-9))s.ZeroSeconds+=deltaSeconds;
    }
    private static double Efficiency(SimulationState s,Definition d,IReadOnlyList<Definition> all,double deltaSeconds){double e=1;foreach(var p in d.Consumption){double demand=p.Value*s.Buildings.GetValueOrDefault(d.Id);if(demand>0)e=Math.Min(e,(s.Resources.GetValueOrDefault(p.Key)+p.Value*deltaSeconds)/(demand*deltaSeconds));}return Math.Clamp(e,0,1);}
    private static IEnumerable<SimEffect> CompletedEffects(SimulationState s,IReadOnlyList<Definition> all,int type,string target)=>s.ActiveEffects.Where(e=>e.Type==type&&(string.IsNullOrEmpty(e.Target)||e.Target.Equals(target,StringComparison.OrdinalIgnoreCase)));
    public static double Get(SimulationState s,string id)=>s.Resources.GetValueOrDefault(id);
    public static bool CanPay(SimulationState s,IDictionary<string,double> cost)=>cost.All(p=>Get(s,p.Key)+1e-9>=p.Value);
    public static void Pay(SimulationState s,IDictionary<string,double> cost){foreach(var p in cost)Add(s.Resources,p.Key,-p.Value);}
    public static void Add(Dictionary<string,double> d,string k,double v){d[k]=d.GetValueOrDefault(k)+v;}
}
