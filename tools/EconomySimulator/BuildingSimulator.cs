using System;
using System.Collections.Generic;
using System.Linq;
namespace Kingdom.EconomySimulation;
public static class BuildingSimulator
{
    public static void Decide(SimulationState s,IReadOnlyList<Definition> defs,Route route){int gap=route switch{Route.Fast=>15,Route.Normal=>45,_=>270};if(Math.Abs((s.Seconds%gap))>.00001)return;var cand=defs.Where(x=>x.TechLevel<=s.TechLevel).Where(x=>x.RequiredResearch.All(s.CompletedResearch.Contains)).Where(x=>x.Generation.Count>0||x.ResearchPower>0).Select(d=>new{D=d,N=s.Buildings.GetValueOrDefault(d.Id)}).Where(x=>x.N==0||x.N<(route==Route.Conservative?2:3)).Where(x=>ResourceSimulator.CanPay(s,ScaledCost(x.D,x.N))).OrderBy(x=>x.N>0?1:0).ThenByDescending(x=>x.D.ResearchPower).ThenByDescending(x=>Score(x.D,route)).ThenBy(x=>x.D.TechLevel).ThenBy(x=>x.D.Id,StringComparer.OrdinalIgnoreCase).FirstOrDefault();if(cand==null){s.BuildingWaitingSeconds+=gap;return;}var cost=ScaledCost(cand.D,cand.N);ResourceSimulator.Pay(s,cost);s.Buildings[cand.D.Id]=cand.N+1;s.Events.Add(new SimulationEvent{Seconds=s.Seconds,Kind="BuildingCompleted",Id=cand.D.Id,TechLevel=s.TechLevel,Count=cand.N+1});}
    private static Dictionary<string,double> ScaledCost(Definition d,int owned){double factor=Math.Pow(Math.Max(1.0,d.CostGrowth),owned);return d.ResourceRequirements.ToDictionary(x=>x.Key,x=>x.Value*factor,StringComparer.OrdinalIgnoreCase);}
    private static double Score(Definition d,Route r){double p=d.ResearchPower*100+d.Generation.Values.Sum()*100-d.Consumption.Values.Sum()*10;return r==Route.Fast?p+50:d.FoodProduction>0?p+20:p;}
}
