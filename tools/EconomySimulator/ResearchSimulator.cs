using System;
using System.Collections.Generic;
using System.Linq;
namespace Kingdom.EconomySimulation;
public static class ResearchSimulator
{
    public static void TrySelect(
        SimulationState s,
        IReadOnlyList<Definition> defs,
        IReadOnlyList<Definition> buildings,
        Route route)
    {
        if(s.ActiveResearch!=null)
            return;
        int decisionInterval = route switch
        {
            Route.Fast => 1,
            Route.Normal => 180,
            _ => 300
        };
        if (s.Tick % decisionInterval != 0)
            return;
        var d=defs
            .Where(x=>!s.CompletedResearch.Contains(x.Id))
            .Where(x=>(!x.AdvancesTechLevel&&x.TechLevel<=s.TechLevel)||
                (x.AdvancesTechLevel&&(int)x.TechLevel==(int)s.TechLevel+1))
            .Where(x=>x.Prerequisites.All(s.CompletedResearch.Contains))
            .Where(x=>CostsHaveAvailableSources(s,x,buildings))
            .OrderByDescending(x=>x.AdvancesTechLevel)
            .ThenBy(x=>x.BaseCost)
            .ThenBy(x=>x.Id,StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if(d==null)
            return;
        s.ActiveResearch=new ResearchTask{Definition=d,StartedSeconds=s.Seconds};
        s.MarkAction("ResearchSelected",d.Id);
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
        bool paid=true;
        foreach(var p in t.Definition.ResourceRequirements)
        {
            double rem=p.Value-t.Paid.GetValueOrDefault(p.Key);
            double pay=Math.Min(rem,Math.Max(0,ResourceSimulator.Get(s,p.Key)));
            if(pay>0)
            {
                ResourceSimulator.Add(s.Resources,p.Key,-pay);
                t.Paid[p.Key]=t.Paid.GetValueOrDefault(p.Key)+pay;
            }
            if(t.Paid.GetValueOrDefault(p.Key)+1e-9<p.Value)
                paid=false;
        }
        if(!paid)
        {
            s.ResearchWaitingSeconds+=deltaSeconds;
            return;
        }
        double speed=ResearchPower(s,all.Where(x=>x.Kind=="Building").ToList(),all)*
            EraEffect(s.TechLevel,t.Definition.TechLevel)*deltaSeconds;
        t.Progress+=speed;
        if(t.Progress<t.Definition.BaseCost)
            return;
        s.CompletedResearch.Add(t.Definition.Id);
        s.ActiveEffects.AddRange(t.Definition.Effects);
        s.MarkAction("ResearchCompleted",t.Definition.Id);
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
        double rp=1;
        foreach(var b in buildings)
        {
            double mult=1;
            foreach(var e in CompletedEffects(s,13,b.Id))
                mult*=e.Value;
            rp+=s.Buildings.GetValueOrDefault(b.Id)*b.ResearchPower*mult;
        }
        double global=1;
        foreach(var e in CompletedEffects(s,4,""))
            global*=e.Value;
        return rp*global;
    }
    private static IEnumerable<SimEffect> CompletedEffects(SimulationState s,int type,string target){return s.ActiveEffects.Where(e=>e.Type==type&&(string.IsNullOrEmpty(e.Target)||e.Target.Equals(target,StringComparison.OrdinalIgnoreCase)));}
    private static double EraEffect(SimTechLevel current,SimTechLevel target)=>current==target?1:1/(Math.Abs((int)target-(int)current)+.5);
}
