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
    public double BaseCost, CostGrowth=1.15, ResearchPower, FoodProduction, FoodConsumption, PowerProduction, PowerConsumption, LogisticsProduction, LogisticsConsumption, ProductivityConsumption, ProductivityGranted, PopulationCapacity;
    public readonly List<string> Prerequisites=new(), RequiredResearch=new();
    public readonly Dictionary<string,double> ResourceRequirements=new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string,double> Generation=new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string,double> Consumption=new(StringComparer.OrdinalIgnoreCase);
    public readonly List<SimEffect> Effects=new();
}
public sealed class SimEffect { public int Type; public string Target=""; public double Value; }
public sealed class SimulationEvent
{
    public double Seconds;
    public string Kind = "", Id = "", Detail = "";
    public SimTechLevel TechLevel;
    public int Count;
}
public sealed class TimelineSnapshot
{
    public int Minute;
    public SimTechLevel TechLevel;
    public double ResearchPower;
    public double Population;
    public double TotalProductivity;
    public double UsedProductivity;
    public string Resources = "", Buildings = "", ResearchCompleted = "", ActiveResearch = "";
}
public sealed class ResearchTask { public Definition Definition=null!; public double Progress; public Dictionary<string,double> Paid=new(StringComparer.OrdinalIgnoreCase); public double StartedSeconds; }
public sealed class SimulationState
{
    public long Tick;
    public double Seconds;
    public SimTechLevel TechLevel=SimTechLevel.Animal;
    public double Food=300, FoodCapacity=500, PowerSatisfaction=1, LogisticsSatisfaction=1;
    public double Population, PopulationGrowthProgress;
    public readonly Dictionary<string,double> Resources=new(StringComparer.OrdinalIgnoreCase){["WoodLog"]=0};
    public readonly Dictionary<string,int> Buildings=new(StringComparer.OrdinalIgnoreCase); public readonly HashSet<string> CompletedResearch=new(StringComparer.OrdinalIgnoreCase); public readonly List<SimEffect> ActiveEffects=new();
    public readonly List<SimulationEvent> Events=new();
    public readonly List<TimelineSnapshot> Timeline=new();
    public readonly Dictionary<string,double> EraReachedSeconds=new(StringComparer.OrdinalIgnoreCase){["Animal"]=0};
    public readonly Dictionary<string,double> Minimums=new(StringComparer.OrdinalIgnoreCase);
    public double ResearchWaitingSeconds, BuildingWaitingSeconds, ZeroSeconds;
    public double LastActionSeconds, MaximumNoActionSeconds;
    public ResearchTask? ActiveResearch;

    public void MarkAction(string kind, string id)
    {
        MaximumNoActionSeconds = Math.Max(MaximumNoActionSeconds, Seconds - LastActionSeconds);
        LastActionSeconds = Seconds;
        Events.Add(new SimulationEvent
        {
            Seconds = Seconds,
            Kind = kind,
            Id = id,
            TechLevel = TechLevel
        });
    }
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
        var list=new List<Definition>(); foreach(var r in raw.OrderBy(x=>x.Kind).ThenBy(x=>x.Id,StringComparer.OrdinalIgnoreCase)) { var d=new Definition{Id=r.Id,Kind=r.Kind,TechLevel=(SimTechLevel)Math.Clamp((int)Num(r.Text,"TechLevel",0),0,6),AdvancesTechLevel=Num(r.Text,"AdvancesTechLevel",0)>.5,BaseCost=Num(r.Text,"BaseCost",0),CostGrowth=Num(r.Text,"costGrowth",1.15),ResearchPower=Num(r.Text,"researchPowerGranted",0),FoodProduction=Num(r.Text,"foodProductionRate",0),FoodConsumption=Num(r.Text,"foodConsumptionRate",0),PowerProduction=Num(r.Text,"powerProductionRate",0),PowerConsumption=Num(r.Text,"powerConsumptionRate",0),LogisticsProduction=Num(r.Text,"logisticsProductionRate",0),LogisticsConsumption=Num(r.Text,"logisticsConsumptionRate",0),ProductivityConsumption=Num(r.Text,"productivityConsumption",0),ProductivityGranted=Num(r.Text,"productivityGranted",0),PopulationCapacity=Num(r.Text,"populationCapacityGranted",0)}; Refs(Sec(r.Text,"prerequisites"),guid,d.Prerequisites); Refs(Sec(r.Text,"requiredResearch"),guid,d.RequiredResearch); Pairs(Sec(r.Text,"resourceRequirements"),guid,d.ResourceRequirements); Pairs(Sec(r.Text,"resourceGenerationRates"),guid,d.Generation); Pairs(Sec(r.Text,"resourceConsumptionRates"),guid,d.Consumption); Effects(Sec(r.Text,"effects"),guid,d.Effects); list.Add(d); } return list;
    }
    private static string Sec(string t,string f){var m=Regex.Match(t,@"(?ms)^\s{2}"+Regex.Escape(f)+@":\s*\r?\n(.*?)(?=^\s{2}[A-Za-z][A-Za-z0-9_]*:\s*|\z)");return m.Success?m.Groups[1].Value:"";}
    private static double Num(string t,string f,double x){var direct=Regex.Match(t,@"(?m)^  "+Regex.Escape(f)+@":\s*([-+0-9.eE]+)");if(direct.Success&&double.TryParse(direct.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var v))return v;var block=Regex.Match(t,@"(?ms)^  "+Regex.Escape(f)+@":\s*\r?\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:\s*|\z)");var scalar=block.Success?Regex.Match(block.Groups[1].Value,@"(?m)^\s*scalar:\s*([-+0-9.eE]+)"):Match.Empty;return scalar.Success&&double.TryParse(scalar.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:x;}
    private static void Refs(string s,Dictionary<string,string> g,List<string> dst){foreach(Match m in Regex.Matches(s,@"guid:\s*([0-9a-f]+)",RegexOptions.IgnoreCase))if(g.TryGetValue(m.Groups[1].Value,out var id))dst.Add(id);}
    private static void Pairs(string s,Dictionary<string,string> g,Dictionary<string,double> dst){foreach(Match m in Regex.Matches(s,@"- first:.*?guid:\s*([0-9a-f]+).*?scalar:\s*([-+0-9.eE]+)",RegexOptions.Singleline|RegexOptions.IgnoreCase))if(g.TryGetValue(m.Groups[1].Value,out var id)&&double.TryParse(m.Groups[2].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var v))dst[id]=v;}
    private static void Effects(string s,Dictionary<string,string> g,List<SimEffect> dst){foreach(Match m in Regex.Matches(s,@"- Type:\s*(\d+)(.*?)(?=\r?\n\s*- Type:|\z)",RegexOptions.Singleline|RegexOptions.IgnoreCase)){var vm=Regex.Match(m.Groups[2].Value,@"(?m)^\s*scalar:\s*([-+0-9.eE]+)");var e=new SimEffect{Type=int.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture),Value=vm.Success?double.Parse(vm.Groups[1].Value,CultureInfo.InvariantCulture):1};var b=Regex.Match(m.Groups[2].Value,@"Building:\s*\{[^}]*guid:\s*([0-9a-f]+)",RegexOptions.IgnoreCase);var res=Regex.Match(m.Groups[2].Value,@"Resource:\s*\{[^}]*guid:\s*([0-9a-f]+)",RegexOptions.IgnoreCase);if(b.Success&&g.TryGetValue(b.Groups[1].Value,out var bid))e.Target=bid;else if(res.Success&&g.TryGetValue(res.Groups[1].Value,out var rid))e.Target=rid;dst.Add(e);}}
}

public static class EconomySimulator
{
    public const double TickSeconds=1d;
    public const double DefaultHorizonSeconds=24d*60d*60d;
    public static int Main(string[] args)
    {
        string root=FindRoot(args.Length>0?args[0]:Directory.GetCurrentDirectory()); string output=args.Length>1?Path.GetFullPath(args[1]):Path.Combine(root,"data","economy-simulation"); var defs=DefinitionReader.Read(root);
        var results=new Dictionary<Route,SimulationResult>();
        foreach(Route route in Enum.GetValues<Route>())
        {
            var r=Run(defs,route,DefaultHorizonSeconds);
            results[route]=r;
            SimulationReportWriter.Write(Path.Combine(output,route.ToString()),defs,r);
            if(route==Route.Normal)
                SimulationReportWriter.Write(output,defs,r);
        }
        IReadOnlyList<string> failures=PacingAcceptance.Validate(results);
        File.WriteAllLines(
            Path.Combine(output,"PacingAcceptance.txt"),
            failures.Count==0
                ?new[]{"PASS: all vertical-slice pacing gates passed."}
                :new[]{"FAIL:"}.Concat(failures));
        Console.WriteLine(
            $"Simulation complete: {output}; definitions={defs.Count}; " +
            $"acceptance={(failures.Count==0?"PASS":"FAIL")}");
        foreach(string failure in failures)
            Console.Error.WriteLine(failure);
        return failures.Count==0?0:2;
    }
    public static SimulationResult Run(IReadOnlyList<Definition> defs,Route route,double horizon)
    {
        var s=new SimulationState(); var r=new SimulationResult(s,route); var b=defs.Where(x=>x.Kind=="Building").ToList(); var q=defs.Where(x=>x.Kind=="Research").ToList();
        long totalTicks=(long)Math.Ceiling(horizon/TickSeconds);
        for(long tick=0;tick<=totalTicks;tick++)
        {
            s.Tick=tick;
            s.Seconds=tick*TickSeconds;
            ResourceSimulator.Tick(s,b,defs,TickSeconds);
            ResearchSimulator.Tick(s,q,defs,TickSeconds);
            BuildingSimulator.Decide(s,b,route);
            ResearchSimulator.TrySelect(s,q,b,route);
            if(tick%(long)(60d/TickSeconds)==0)
                Snapshot(s,b,defs);
            if(s.CompletedResearch.Count==q.Count&&s.TechLevel>=SimTechLevel.Industrial)
                break;
        }
        s.MaximumNoActionSeconds=Math.Max(s.MaximumNoActionSeconds,s.Seconds-s.LastActionSeconds);
        BalanceAnalysis.Analyze(r,defs,b,q);
        r.Notes.Add("Internal clock: fixed one-second ticks; reports aggregate to minutes.");
        r.Notes.Add("Research selection may wait while its resource cost is paid progressively.");
        r.Notes.Add("Research speed is ResearchPower x global multiplier x runtime era effect.");
        r.Notes.Add("Building costs use geometric growth; construction commits immediately.");
        return r;
    }
    private static void Snapshot(
        SimulationState s,
        IReadOnlyList<Definition> buildings,
        IReadOnlyList<Definition> definitions)
    {
        s.Timeline.Add(new TimelineSnapshot
        {
            Minute=(int)Math.Round(s.Seconds/60),
            TechLevel=s.TechLevel,
            ResearchPower=ResearchSimulator.ResearchPower(s,buildings,definitions),
            Population=s.Population,
            TotalProductivity=BuildingSimulator.TotalProductivity(s,buildings),
            UsedProductivity=BuildingSimulator.UsedProductivity(s,buildings),
            Resources=string.Join(";",s.Resources.OrderBy(x=>x.Key).Select(x=>$"{x.Key}={x.Value:0.##}")),
            Buildings=string.Join(";",s.Buildings.OrderBy(x=>x.Key).Select(x=>$"{x.Key}={x.Value}")),
            ResearchCompleted=string.Join(";",s.CompletedResearch.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)),
            ActiveResearch=s.ActiveResearch?.Definition.Id ?? string.Empty
        });
    }
    private static string FindRoot(string c){string p=Path.GetFullPath(c);while(Directory.Exists(p)){if(Directory.Exists(Path.Combine(p,"Kingdom","Assets","Resources","Datas")))return p;var d=Directory.GetParent(p);if(d==null)break;p=d.FullName;}throw new DirectoryNotFoundException("Kingdom root not found from "+c);}
}

public static class PacingAcceptance
{
    private sealed record Window(
        double NeolithicMin,
        double NeolithicMax,
        double MedievalMin,
        double MedievalMax,
        double CompletionMin,
        double CompletionMax);

    private static readonly IReadOnlyDictionary<Route,Window> Windows =
        new Dictionary<Route,Window>
        {
            [Route.Fast]=new(45*60,75*60,4*3600,6*3600,8*3600,10*3600),
            [Route.Normal]=new(60*60,90*60,5*3600,8*3600,10*3600,16*3600),
            [Route.Conservative]=new(90*60,120*60,8*3600,12*3600,14*3600,20*3600)
        };

    public static IReadOnlyList<string> Validate(
        IReadOnlyDictionary<Route,SimulationResult> results)
    {
        var failures=new List<string>();
        foreach((Route route,SimulationResult result) in results)
        {
            SimulationState state=result.State;
            Window window=Windows[route];
            CheckEra(state,route,SimTechLevel.Neolithic,
                window.NeolithicMin,window.NeolithicMax,failures);
            CheckEra(state,route,SimTechLevel.Medieval,
                window.MedievalMin,window.MedievalMax,failures);
            CheckEra(state,route,SimTechLevel.Industrial,
                window.CompletionMin,window.CompletionMax,failures);

            SimulationEvent? first=state.Events
                .Where(x=>x.Kind=="ResearchCompleted")
                .OrderBy(x=>x.Seconds)
                .FirstOrDefault();
            if(first==null||first.Seconds<60d||first.Seconds>120d)
                failures.Add($"{route}: first research must complete in 60-120 seconds.");

            TimelineSnapshot? ten=state.Timeline.FirstOrDefault(x=>x.Minute==10);
            if(ten==null||ten.ResearchPower<2d)
                failures.Add($"{route}: ResearchPower must be at least 2/s at minute 10.");
            int completedAtTen=ten==null
                ?0
                :ten.ResearchCompleted.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries).Length;
            if(completedAtTen<2)
                failures.Add($"{route}: at least two research items must complete by minute 10.");

            if(BalanceAnalysis.MaximumNoResearchSeconds(
                    state,
                    SimTechLevel.Animal)>300d)
                failures.Add($"{route}: Animal no-target drought exceeds five minutes.");
            if(BalanceAnalysis.MaximumNoResearchSeconds(
                    state,
                    SimTechLevel.Neolithic)>600d)
                failures.Add($"{route}: Neolithic no-target drought exceeds ten minutes.");
            foreach(BalanceWarning warning in result.Warnings.Where(x=>x.Severity=="High"))
                failures.Add($"{route}: {warning.Type} {warning.Object}: {warning.Reason}");
        }
        return failures;
    }

    private static void CheckEra(
        SimulationState state,
        Route route,
        SimTechLevel era,
        double minimum,
        double maximum,
        List<string> failures)
    {
        double seconds=state.EraReachedSeconds.GetValueOrDefault(era.ToString(),-1d);
        if(seconds<minimum||seconds>maximum)
            failures.Add(
                $"{route}: {era} reached at {seconds/60d:0.##} minutes; " +
                $"expected {minimum/60d:0.##}-{maximum/60d:0.##}.");
    }
}
