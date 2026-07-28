using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kingdom.EconomySimulation;

public enum SimTechLevel { Animal=0, Neolithic=1, Medieval=2, Industrial=3, Spacer=4, Ultra=5, Archotech=6 }
public enum Route { Normal, Fast, Conservative }

public sealed class Definition
{
    public string Id="", Kind=""; public SimTechLevel TechLevel; public bool AdvancesTechLevel;
    public double BaseCost, CostGrowth=1.15, ResearchPower, FoodProduction, FoodConsumption, PowerProduction, PowerConsumption, LogisticsProduction, LogisticsConsumption, Workforce;
    public readonly List<string> Prerequisites=new(), RequiredResearch=new();
    public readonly Dictionary<string,double> ResourceRequirements=new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string,double> Generation=new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string,double> Consumption=new(StringComparer.OrdinalIgnoreCase);
    public readonly List<SimEffect> Effects=new();
}
public sealed class SimEffect { public int Type; public string Target=""; public double Value; }
public sealed class SimulationEvent { public double Seconds; public string Kind="", Id=""; public SimTechLevel TechLevel; public int Count; }
public sealed class TimelineSnapshot { public int Minute; public SimTechLevel TechLevel; public string Resources="", Buildings="", ResearchCompleted=""; }
public sealed class ResearchTask { public Definition Definition=null!; public double Progress; public Dictionary<string,double> Paid=new(StringComparer.OrdinalIgnoreCase); public double StartedSeconds; }
public sealed class SimulationState
{
    public double Seconds; public SimTechLevel TechLevel=SimTechLevel.Animal; public double Food=300, FoodCapacity=500, PowerSatisfaction=1, LogisticsSatisfaction=1;
    public readonly Dictionary<string,double> Resources=new(StringComparer.OrdinalIgnoreCase){["WoodLog"]=0};
    public readonly Dictionary<string,int> Buildings=new(StringComparer.OrdinalIgnoreCase); public readonly HashSet<string> CompletedResearch=new(StringComparer.OrdinalIgnoreCase); public readonly List<SimEffect> ActiveEffects=new();
    public readonly List<SimulationEvent> Events=new(); public readonly List<TimelineSnapshot> Timeline=new(); public readonly Dictionary<string,int> EraReachedMinute=new(StringComparer.OrdinalIgnoreCase){["Animal"]=0};
    public readonly Dictionary<string,double> Minimums=new(StringComparer.OrdinalIgnoreCase); public double ResearchWaitingSeconds, BuildingWaitingSeconds, ZeroSeconds; public ResearchTask? ActiveResearch;
}
public sealed class BalanceWarning { public string Type="", Object="", Reason="", Severity="", Suggestion=""; }
public sealed class SimulationResult
{
    public readonly SimulationState State; public readonly List<BalanceWarning> Warnings=new(); public readonly List<string> Bottlenecks=new(); public readonly List<string> Notes=new();
    public Route Route; public SimulationResult(SimulationState state, Route route){State=state;Route=route;}
}

public static class DefinitionReader
{
    private sealed class Raw { public string Id="", Kind="", Text=""; }
    public static IReadOnlyList<Definition> Read(string root)
    {
        string data=Path.Combine(root,"Kingdom","Assets","Resources","Datas"); var guid=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); var raw=new List<Raw>();
        foreach(string kind in new[]{"Resource","Building","Research"}) foreach(string p in Directory.EnumerateFiles(Path.Combine(data,kind),"*.asset",SearchOption.AllDirectories))
        { string t=File.ReadAllText(p); var im=Regex.Match(t,@"(?m)^\s*id:\s*(\S+)\s*$"); var mp=p+".meta"; if(!im.Success||!File.Exists(mp))continue; var gm=Regex.Match(File.ReadAllText(mp),@"(?m)^guid:\s*(\S+)"); if(!gm.Success)continue; guid[gm.Groups[1].Value]=im.Groups[1].Value; raw.Add(new Raw{Id=im.Groups[1].Value,Kind=kind,Text=t}); }
        var list=new List<Definition>(); foreach(var r in raw.OrderBy(x=>x.Kind).ThenBy(x=>x.Id,StringComparer.OrdinalIgnoreCase)) { var d=new Definition{Id=r.Id,Kind=r.Kind,TechLevel=(SimTechLevel)Math.Clamp((int)Num(r.Text,"TechLevel",0),0,6),AdvancesTechLevel=Num(r.Text,"AdvancesTechLevel",0)>.5,BaseCost=Num(r.Text,"BaseCost",0),CostGrowth=Num(r.Text,"costGrowth",1.15),ResearchPower=Num(r.Text,"researchPowerGranted",0),FoodProduction=Num(r.Text,"foodProductionRate",0),FoodConsumption=Num(r.Text,"foodConsumptionRate",0),PowerProduction=Num(r.Text,"powerProductionRate",0),PowerConsumption=Num(r.Text,"powerConsumptionRate",0),LogisticsProduction=Num(r.Text,"logisticsProductionRate",0),LogisticsConsumption=Num(r.Text,"logisticsConsumptionRate",0),Workforce=Num(r.Text,"productivityConsumption",0)}; Refs(Sec(r.Text,"prerequisites"),guid,d.Prerequisites); Refs(Sec(r.Text,"requiredResearch"),guid,d.RequiredResearch); Pairs(Sec(r.Text,"resourceRequirements"),guid,d.ResourceRequirements); Pairs(Sec(r.Text,"resourceGenerationRates"),guid,d.Generation); Pairs(Sec(r.Text,"resourceConsumptionRates"),guid,d.Consumption); Effects(Sec(r.Text,"effects"),guid,d.Effects); list.Add(d); } return list;
    }
    private static string Sec(string t,string f){var m=Regex.Match(t,@"(?ms)^\s{2}"+Regex.Escape(f)+@":\s*\r?\n(.*?)(?=^\s{2}[A-Za-z][A-Za-z0-9_]*:\s*|\z)");return m.Success?m.Groups[1].Value:"";}
    private static double Num(string t,string f,double x){var direct=Regex.Match(t,@"(?m)^  "+Regex.Escape(f)+@":\s*([-+0-9.eE]+)");if(direct.Success&&double.TryParse(direct.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var v))return v;var block=Regex.Match(t,@"(?ms)^  "+Regex.Escape(f)+@":\s*\r?\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:\s*|\z)");var scalar=block.Success?Regex.Match(block.Groups[1].Value,@"(?m)^\s*scalar:\s*([-+0-9.eE]+)"):Match.Empty;return scalar.Success&&double.TryParse(scalar.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:x;}
    private static void Refs(string s,Dictionary<string,string> g,List<string> dst){foreach(Match m in Regex.Matches(s,@"guid:\s*([0-9a-f]+)",RegexOptions.IgnoreCase))if(g.TryGetValue(m.Groups[1].Value,out var id))dst.Add(id);}
    private static void Pairs(string s,Dictionary<string,string> g,Dictionary<string,double> dst){foreach(Match m in Regex.Matches(s,@"- first:.*?guid:\s*([0-9a-f]+).*?scalar:\s*([-+0-9.eE]+)",RegexOptions.Singleline|RegexOptions.IgnoreCase))if(g.TryGetValue(m.Groups[1].Value,out var id)&&double.TryParse(m.Groups[2].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var v))dst[id]=v;}
    private static void Effects(string s,Dictionary<string,string> g,List<SimEffect> dst){foreach(Match m in Regex.Matches(s,@"- Type:\s*(\d+)(.*?)(?=\r?\n\s*- Type:|\z)",RegexOptions.Singleline|RegexOptions.IgnoreCase)){var vm=Regex.Match(m.Groups[2].Value,@"(?m)^\s*scalar:\s*([-+0-9.eE]+)");var e=new SimEffect{Type=int.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture),Value=vm.Success?double.Parse(vm.Groups[1].Value,CultureInfo.InvariantCulture):1};var b=Regex.Match(m.Groups[2].Value,@"Building:\s*\{[^}]*guid:\s*([0-9a-f]+)",RegexOptions.IgnoreCase);var res=Regex.Match(m.Groups[2].Value,@"Resource:\s*\{[^}]*guid:\s*([0-9a-f]+)",RegexOptions.IgnoreCase);if(b.Success&&g.TryGetValue(b.Groups[1].Value,out var bid))e.Target=bid;else if(res.Success&&g.TryGetValue(res.Groups[1].Value,out var rid))e.Target=rid;dst.Add(e);}}
}

public static class EconomySimulator
{
    public const double TickSeconds=.1;
    public static int Main(string[] args)
    {
        string root=FindRoot(args.Length>0?args[0]:Directory.GetCurrentDirectory()); string output=args.Length>1?Path.GetFullPath(args[1]):Path.Combine(root,"data","economy-simulation"); var defs=DefinitionReader.Read(root);
        foreach(Route route in Enum.GetValues<Route>()) { var r=Run(defs,route,15*60*60); SimulationReportWriter.Write(Path.Combine(output,route.ToString()),defs,r); if(route==Route.Normal) SimulationReportWriter.Write(output,defs,r); }
        foreach(double hours in new[]{24d,48d}) SimulationReportWriter.Write(Path.Combine(output,"Horizons",$"{hours:0}h","Normal"),defs,Run(defs,Route.Normal,hours*60*60));
        Console.WriteLine($"Simulation complete: {output}; definitions={defs.Count}"); return 0;
    }
    public static SimulationResult Run(IReadOnlyList<Definition> defs,Route route,double horizon)
    {
        var s=new SimulationState(); var r=new SimulationResult(s,route); var b=defs.Where(x=>x.Kind=="Building").ToList(); var q=defs.Where(x=>x.Kind=="Research").ToList();
        for(double t=0;t<horizon;t+=TickSeconds){s.Seconds=t; ResourceSimulator.Tick(s,b,defs); ResearchSimulator.Tick(s,q,defs); BuildingSimulator.Decide(s,b,route); ResearchSimulator.TryStart(s,q); if(Math.Abs(t%60)<.00001) Snapshot(s); if(s.CompletedResearch.Count==q.Count&&s.TechLevel>=SimTechLevel.Industrial)break;}
        BalanceAnalysis.Analyze(r,defs,b,q); r.Notes.Add("Internal clock: fixed 0.1 second ticks; reports aggregate to minutes."); r.Notes.Add("Research cost is paid incrementally; speed is ResearchPower x global multiplier x runtime era effect."); r.Notes.Add("Building costs use geometric series with repeated construction; construction commits immediately because no duration field exists."); return r;
    }
    private static void Snapshot(SimulationState s){s.Timeline.Add(new TimelineSnapshot{Minute=(int)Math.Round(s.Seconds/60),TechLevel=s.TechLevel,Resources=string.Join(";",s.Resources.OrderBy(x=>x.Key).Select(x=>$"{x.Key}={x.Value:0.##}")),Buildings=string.Join(";",s.Buildings.OrderBy(x=>x.Key).Select(x=>$"{x.Key}={x.Value}")),ResearchCompleted=string.Join(";",s.CompletedResearch.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))});}
    private static string FindRoot(string c){string p=Path.GetFullPath(c);while(Directory.Exists(p)){if(Directory.Exists(Path.Combine(p,"Kingdom","Assets","Resources","Datas")))return p;var d=Directory.GetParent(p);if(d==null)break;p=d.FullName;}throw new DirectoryNotFoundException("Kingdom root not found from "+c);}
}
