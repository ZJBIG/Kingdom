using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kingdom.EconomySimulation;

public enum SimTechLevel { Animal=0, Neolithic=1, Medieval=2, Industrial=3, Spacer=4, Ultra=5, Archotech=6 }
public enum Route { Normal, Fast, Conservative }

public sealed class SimulationEvent
{
    public double Seconds;
    public string Kind = "", Id = "", Detail = "";
    public SimTechLevel TechLevel;
    public int Count;
}
public sealed class SimulationDecision
{
    public double FirstSeconds, LastSeconds;
    public Route Route;
    public string Subsystem = "", Outcome = "", Candidate = "", Reason = "";
    public int RepeatCount = 1;
}
public sealed class TimelineSnapshot
{
    public int Minute;
    public SimTechLevel TechLevel;
    public double ResearchPower;
    public double Population;
    public double TotalProductivity;
    public double UsedProductivity;
    public double PopulationGrowthMultiplier;
    public double PopulationGrowthPerSecond;
    public double TerritoryTotal;
    public double TerritoryUsed;
    public string Resources = "", Buildings = "", ResearchCompleted = "", ActiveResearch = "";
    public string WorkshopPurchased = "";
}
public sealed class ResearchTask
{
    public Definition Definition = null!;
    public double Progress;
    public bool CostPaid;
    public double StartedSeconds;
    public Route Route;
}
public sealed class SimulationState
{
    public long Tick;
    public double Seconds;
    public SimTechLevel TechLevel=SimTechLevel.Animal;
    public double Food=300, FoodCapacity=500, FoodAvailability=1, HappinessMultiplier=1, PowerSatisfaction=1, LogisticsSatisfaction=1;
    public double Population, PopulationCapacity, PopulationChangeProgress;
    public readonly Dictionary<string,double> Resources=new(StringComparer.OrdinalIgnoreCase){["WoodLog"]=0};
    public readonly Dictionary<string,int> Buildings=new(StringComparer.OrdinalIgnoreCase); public readonly HashSet<string> CompletedResearch=new(StringComparer.OrdinalIgnoreCase); public readonly List<SimEffect> ActiveEffects=new();
    public readonly HashSet<string> PurchasedWorkshop = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<SimulationEvent> Events=new();
    public readonly List<SimulationDecision> Decisions = new();
    private readonly Dictionary<string,int> LastDecisionIndex =
        new(StringComparer.OrdinalIgnoreCase);
    public readonly List<TimelineSnapshot> Timeline=new();
    public readonly Dictionary<string,double> EraReachedSeconds=new(StringComparer.OrdinalIgnoreCase){["Animal"]=0};
    public readonly Dictionary<string,double> Minimums=new(StringComparer.OrdinalIgnoreCase);
    public double ResearchWaitingSeconds, BuildingWaitingSeconds;
    public double ProductivityWaitingSeconds, CurrentProductivityWaitingSeconds;
    public double MaximumProductivityWaitingSeconds, ZeroSeconds;
    public double LastActionSeconds, MaximumNoActionSeconds;
    public double LastResearchDecisionSeconds = double.NegativeInfinity;
    public double LastBuildingDecisionSeconds = double.NegativeInfinity;
    public double LastWorkshopDecisionSeconds = double.NegativeInfinity;
    public int DefinitionRevision;
    public int CachedBuildingRevision = -1;
    public Definition[]? CachedBuildingCandidates;
    public int CachedWorkshopRevision = -1;
    public Definition[]? CachedWorkshopCandidates;
    public bool UpgradePairsInitialized;
    public readonly List<(Definition Source, Definition Target)> UpgradePairs = new();
    public ResearchTask? ActiveResearch;

    public void MarkAction(string kind, string id)
    {
        if (kind is "ResearchCompleted" or "WorkshopPurchased" or
            "BuildingCompleted" or "BuildingUpgrade")
            DefinitionRevision++;
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

    public bool ShouldDecide(ref double lastDecisionSeconds, int intervalSeconds)
    {
        if (Seconds + 1e-9d < lastDecisionSeconds + intervalSeconds)
            return false;
        lastDecisionSeconds = Seconds;
        return true;
    }

    public void TraceDecision(
        Route route,
        string subsystem,
        string outcome,
        string candidate,
        string reason)
    {
        SimulationDecision? previous = null;
        if (LastDecisionIndex.TryGetValue(subsystem, out int previousIndex) &&
            previousIndex >= 0 && previousIndex < Decisions.Count)
        {
            SimulationDecision previousDecision = Decisions[previousIndex];
            if (previousDecision.Route == route)
                previous = previousDecision;
        }
        if (previous != null && previous.Outcome == outcome &&
            previous.Candidate == candidate && previous.Reason == reason)
        {
            previous.LastSeconds = Seconds;
            previous.RepeatCount++;
            return;
        }
        Decisions.Add(new SimulationDecision
        {
            FirstSeconds = Seconds,
            LastSeconds = Seconds,
            Route = route,
            Subsystem = subsystem,
            Outcome = outcome,
            Candidate = candidate,
            Reason = reason
        });
        LastDecisionIndex[subsystem] = Decisions.Count - 1;
    }
}
public sealed class BalanceWarning { public string Type="", Object="", Reason="", Severity="", Suggestion=""; }
public sealed class SimulationResult
{
    public readonly SimulationState State; public readonly List<BalanceWarning> Warnings=new(); public readonly List<string> Bottlenecks=new(); public readonly List<string> Notes=new();
    public Route Route; public SimulationResult(SimulationState state, Route route){State=state;Route=route;}
}

public static class EconomySimulator
{
    public const double TickSeconds=1d;
    public const double MedievalStepSeconds=10d;
    public const double IndustrialStepSeconds=60d;
    public const double SpacerStepSeconds=120d;
    public const double UltraStepSeconds=180d;
    public const double ArchotechStepSeconds=300d;
    public const int DefaultHorizonDays=30;
    public const double DefaultHorizonSeconds=DefaultHorizonDays*24d*60d*60d;
    public const int ReportSnapshotIntervalSeconds=600;

    public static double StepSeconds(SimTechLevel techLevel) => techLevel switch
    {
        SimTechLevel.Industrial => IndustrialStepSeconds,
        SimTechLevel.Spacer => SpacerStepSeconds,
        SimTechLevel.Ultra => UltraStepSeconds,
        SimTechLevel.Archotech => ArchotechStepSeconds,
        SimTechLevel.Medieval => MedievalStepSeconds,
        _ => TickSeconds
    };
    public static int Main(string[] args)
    {
        string root=FindRoot(args.Length>0 && args[0] != "--self-test"
            ?args[0]:Directory.GetCurrentDirectory());
        string output=args.Length>1?Path.GetFullPath(args[1]):Path.Combine(root,"data","economy-simulation");
        EconomySnapshot snapshot=UnityAssetSnapshotReader.Read(root);
        SimulatorSelfTests.Run(snapshot);
        if(args.Contains("--self-test",StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Simulator self-tests passed; definitions={snapshot.All.Count}; workshops={snapshot.Workshops.Count}.");
            return 0;
        }
        var results=new Dictionary<Route,SimulationResult>();
        foreach(Route route in Enum.GetValues<Route>())
        {
            var r=Run(snapshot,SimulationStrategies.Create(route),DefaultHorizonSeconds);
            results[route]=r;
            SimulationReportWriter.Write(Path.Combine(output,route.ToString()),snapshot,r);
            if(route==Route.Normal)
                SimulationReportWriter.Write(output,snapshot,r);
        }
        IReadOnlyList<string> failures=PacingAcceptance.Validate(results);
        File.WriteAllLines(
            Path.Combine(output,"PacingAcceptance.txt"),
            failures.Count==0
                ?new[]{"PASS: all vertical-slice pacing gates passed."}
                :new[]{"FAIL:"}.Concat(failures));
        Console.WriteLine(
            $"Simulation complete: {output}; definitions={snapshot.All.Count}; " +
            $"workshops={snapshot.Workshops.Count}; acceptance={(failures.Count==0?"PASS":"FAIL")}");
        foreach(string failure in failures)
            Console.Error.WriteLine(failure);
        return failures.Count==0?0:2;
    }
    public static SimulationResult Run(
        EconomySnapshot snapshot,
        ISimulationStrategy strategy,
        double horizon)
    {
        var s=new SimulationState();
        var r=new SimulationResult(s,strategy.Route);
        IReadOnlyList<Definition> b=snapshot.Buildings;
        IReadOnlyList<Definition> q=snapshot.Research;
        var activeBuildings=new List<Definition>();
        int activeBuildingsRevision = -1;
        while(s.Seconds<=horizon+1e-9)
        {
            s.Tick=(long)Math.Round(s.Seconds);
            double stepSeconds=Math.Min(StepSeconds(s.TechLevel),
                Math.Max(0d,horizon-s.Seconds));
            ResourceSimulator.Tick(s,activeBuildings,snapshot.All,stepSeconds);
            ResearchSimulator.Tick(s,q,snapshot.All,stepSeconds,activeBuildings);
            if (s.Resources.Any(x => !double.IsFinite(x.Value)))
                throw new InvalidOperationException(
                    $"模拟在 {s.Seconds:0.###} 秒产生非有限资源值：" +
                    string.Join(",", s.Resources.Where(x => !double.IsFinite(x.Value))
                        .Select(x => x.Key)));
            WorkshopSimulator.Decide(s,snapshot,strategy);
            BuildingSimulator.Decide(s,snapshot,strategy);
            if (activeBuildingsRevision != s.DefinitionRevision)
            {
                RefreshActiveBuildings(s,b,activeBuildings);
                activeBuildingsRevision = s.DefinitionRevision;
            }
            ResearchSimulator.TrySelect(s,snapshot,strategy);
            if(s.Tick % ReportSnapshotIntervalSeconds == 0)
                Snapshot(s,activeBuildings,snapshot.All);
            if(stepSeconds<=0d)
                break;
            s.Seconds+=stepSeconds;
        }
        s.MaximumNoActionSeconds=Math.Max(s.MaximumNoActionSeconds,s.Seconds-s.LastActionSeconds);
        BalanceAnalysis.Analyze(r,snapshot.All,b,q);
        r.Notes.Add($"Internal clock: one-second Animal/Neolithic ticks, ten-second Medieval ticks, sixty-second Industrial ticks, one-hundred-twenty-second Spacer ticks, one-hundred-eighty-second Ultra ticks and three-hundred-second Archotech ticks; rates remain per-second. Observation horizon is {DefaultHorizonDays} days; reports sample every {ReportSnapshotIntervalSeconds / 60} minutes.");
        r.Notes.Add("The standalone double-based simulator saturates only at double.MaxValue to prevent overflow from becoming NaN; this is not a gameplay stockpile cap.");
        r.Notes.Add("Research waits until its complete resource cost can be paid atomically, matching ResearchManager.");
        r.Notes.Add("Workshop unlocks, prerequisites, costs and effects are simulated.");
        r.Notes.Add("Research speed is ResearchPower x global multiplier x runtime era effect.");
        r.Notes.Add("Building costs use geometric growth; construction commits immediately.");
        r.Notes.Add("Population growth uses logistic occupancy; departure accelerates with relative overcapacity and remains productivity-gated.");
        r.Notes.Add(
            $"Productivity-blocked building decision time: {s.ProductivityWaitingSeconds:0.##} seconds.");
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
            PopulationGrowthMultiplier=ResourceSimulator.PopulationGrowthMultiplier(s),
            PopulationGrowthPerSecond=ResourceSimulator.PopulationGrowthRatePerSecond(s),
            TerritoryTotal=BuildingSimulator.TotalTerritory(s),
            TerritoryUsed=BuildingSimulator.UsedTerritory(s,buildings),
            Resources=string.Join(";",s.Resources.OrderBy(x=>x.Key).Select(x=>$"{x.Key}={x.Value:0.##}")),
            Buildings=string.Join(";",s.Buildings.OrderBy(x=>x.Key).Select(x=>$"{x.Key}={x.Value}")),
            ResearchCompleted=string.Join(";",s.CompletedResearch.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)),
            ActiveResearch=s.ActiveResearch?.Definition.Id ?? string.Empty
            ,WorkshopPurchased=string.Join(";",s.PurchasedWorkshop.OrderBy(
                x=>x,StringComparer.OrdinalIgnoreCase))
        });
    }

    private static void RefreshActiveBuildings(
        SimulationState state,
        IReadOnlyList<Definition> definitions,
        List<Definition> active)
    {
        active.Clear();
        foreach (Definition definition in definitions)
            if (state.Buildings.GetValueOrDefault(definition.Id) > 0)
                active.Add(definition);
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
