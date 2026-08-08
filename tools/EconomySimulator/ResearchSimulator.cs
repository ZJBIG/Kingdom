using System;
using System.Collections.Generic;
using System.Linq;
namespace Kingdom.EconomySimulation;
public static class ResearchSimulator
{
    public static void TrySelect(
        SimulationState s,
        EconomySnapshot snapshot,
        ISimulationStrategy strategy)
    {
        if(s.ActiveResearch!=null)
            return;
        if (s.Tick % strategy.ResearchDecisionInterval != 0)
            return;
        Definition[] candidates=snapshot.Research
            .Where(x=>!s.CompletedResearch.Contains(x.Id))
            .Where(x=>(!x.AdvancesTechLevel&&x.TechLevel<=s.TechLevel)||
                (x.AdvancesTechLevel&&(int)x.TechLevel==(int)s.TechLevel+1))
            .Where(x=>x.Prerequisites.All(s.CompletedResearch.Contains))
            .Where(x=>CostsHaveAvailableSources(s,x,snapshot.Buildings))
            .OrderByDescending(x=>strategy.ScoreResearch(x,s,snapshot))
            .ThenBy(x=>x.Id,StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Definition? d=candidates.FirstOrDefault();
        if(d==null)
        {
            s.TraceDecision(strategy.Route,"Research","Blocked","",
                "no prerequisite-complete research with an available production chain");
            return;
        }
        s.ActiveResearch=new ResearchTask{
            Definition=d,StartedSeconds=s.Seconds,Route=strategy.Route};
        s.MarkAction("ResearchSelected",d.Id);
        s.TraceDecision(strategy.Route,"Research","Selected",d.Id,
            $"score={strategy.ScoreResearch(d,s,snapshot):0.###}");
    }

    private static bool CostsHaveAvailableSources(
        SimulationState state,
        Definition research,
        IReadOnlyList<Definition> buildings)
    {
        foreach (string resource in research.ResourceRequirements.Keys)
        {
            if (resource.Equals("WoodLog", StringComparison.OrdinalIgnoreCase))
                continue;
            bool sourceAvailable = buildings.Any(building =>
                building.TechLevel <= state.TechLevel &&
                building.RequiredResearch.All(state.CompletedResearch.Contains) &&
                building.RequiredWorkshop.All(state.PurchasedWorkshop.Contains) &&
                building.Generation.ContainsKey(resource));
            if (!sourceAvailable)
                return false;
        }
        return true;
    }

    public static void Tick(
        SimulationState s,
        IReadOnlyList<Definition> defs,
        IReadOnlyList<Definition> all,
        double deltaSeconds)
    {
        var t=s.ActiveResearch;
        if(t==null)
        {
            s.ResearchWaitingSeconds+=deltaSeconds;
            return;
        }
        if(!t.CostPaid)
        {
            if(!ResourceSimulator.CanPay(s,t.Definition.ResourceRequirements))
            {
                s.ResearchWaitingSeconds+=deltaSeconds;
                s.TraceDecision(t.Route,"Research","Waiting",t.Definition.Id,
                    "complete resource cost is not yet affordable");
                return;
            }
            ResourceSimulator.Pay(s,t.Definition.ResourceRequirements);
            t.CostPaid=true;
            s.TraceDecision(t.Route,"Research","Paid",t.Definition.Id,
                "complete resource cost paid atomically");
        }
        double speed=ResearchPower(s,all.Where(
                x=>x.Kind==DefinitionKind.Building).ToArray(),all)*
            EconomySimulationParity.ResearchSpeedEffect(
                (int)s.TechLevel,(int)t.Definition.TechLevel)*
            ResourceSimulator.CalculateHappinessMultiplier(
                ResourceSimulator.CalculateFoodNetRate(
                    s,
                    all.Where(x=>x.Kind==DefinitionKind.Building).ToArray(),
                    deltaSeconds),
                s.Population,
                s.FoodAvailability)*deltaSeconds;
        t.Progress+=speed;
        if(t.Progress<t.Definition.BaseCost)
            return;
        s.CompletedResearch.Add(t.Definition.Id);
        s.ActiveEffects.AddRange(t.Definition.Effects);
        s.MarkAction("ResearchCompleted",t.Definition.Id);
        s.TraceDecision(t.Route,"Research","Completed",t.Definition.Id,
            $"elapsed={s.Seconds-t.StartedSeconds:0.###}s");
        if(t.Definition.AdvancesTechLevel)
        {
            s.TechLevel=t.Definition.TechLevel;
            s.EraReachedSeconds[s.TechLevel.ToString()]=s.Seconds;
        }
        s.ActiveResearch=null;
    }

    public static double ResearchPower(
        SimulationState s,
        IReadOnlyList<Definition> buildings,
        IReadOnlyList<Definition> all)
    {
        double rp=4;
        foreach(var b in buildings)
        {
            double mult=1;
            foreach(var e in CompletedEffects(
                         s,SimEffectKind.BuildingResearchPowerMultiplier,b.Id))
                mult=AdditiveMultiplier(mult,e.Value);
            rp+=s.Buildings.GetValueOrDefault(b.Id)*b.ResearchPower*mult;
        }
        double global=1;
        foreach(var e in CompletedEffects(s,SimEffectKind.GlobalResearchMultiplier,""))
            global=AdditiveMultiplier(global,e.Value);
        return rp*global;
    }
    private static double AdditiveMultiplier(double current,double value) =>
        Math.Max(1d,current+(value>0d?value:1d)-1d);
    private static IEnumerable<SimEffect> CompletedEffects(
        SimulationState s,SimEffectKind kind,string target) =>
        s.ActiveEffects.Where(e=>e.Kind==kind&&
            (string.IsNullOrEmpty(e.Target)||
             e.Target.Equals(target,StringComparison.OrdinalIgnoreCase)));
}
