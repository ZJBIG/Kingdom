using System.Globalization;
using System.Text;

namespace Kingdom.NewEconomySimulator.Tests;

public sealed record SimulationScenario(string Name, int Seed, int Ticks,
    SimulationMode Mode = SimulationMode.Realtime);

public sealed record ResourceRule(string Id, bool IsFood, string? Capacity);
public sealed record WorkshopRule(string Id, IReadOnlyDictionary<string, string> ResourceRequirements);

public sealed record ResearchPayment(string ResearchId, IReadOnlyDictionary<string, string> Cost);

public sealed record SimulationStateSnapshot(
    long Tick,
    string ElapsedSeconds,
    string RequestedSeconds,
    int CalendarDays,
    IReadOnlyDictionary<string, string> Resources,
    IReadOnlyDictionary<string, string> Workshops,
    string Food,
    string FoodCapacity,
    string? ActiveResearch,
    IReadOnlyList<string> CompletedResearch);

public sealed record SimulationRun(IReadOnlyList<TraceEntry> Trace, SimulationStateSnapshot State);

public interface INewEconomySimulatorValidationApi
{
    IReadOnlyList<ResourceRule> ResourceRules { get; }
    IReadOnlyList<WorkshopRule> WorkshopRules { get; }
    SimulationRun Run(SimulationScenario scenario);
    SimulationRun Continue(SimulationScenario scenario);
    SimulationStateSnapshot Snapshot();
    string Save();
    void Restore(string save);
    bool TryStartResearch(ResearchPayment payment);
    bool TryPurchaseWorkshop(string workshopId);
}

public sealed class CoreValidationAdapter : INewEconomySimulatorValidationApi
{
    private const int SaveVersion = 3;
    private const string FoodCapacity = "10";

    private static readonly IReadOnlyList<ResourceRule> Rules =
    [
        new("WoodLog", false, null),
        new("Stone", false, null),
        new("Plank", false, null),
        new("Alloy", false, null),
        new("Food", true, "10")
    ];

    private static readonly IReadOnlyList<WorkshopRule> WorkshopDefinitions =
    [
        new("Sawmill", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WoodLog"] = "2"
        })
    ];

    private readonly SortedSet<string> completedResearch = new(StringComparer.Ordinal);
    private SimulationState state = null!;
    private SimulationCore core = null!;
    private string? activeResearch;
    private int seed;

    public CoreValidationAdapter()
    {
        Reset(0);
    }

    public IReadOnlyList<ResourceRule> ResourceRules => Rules;
    public IReadOnlyList<WorkshopRule> WorkshopRules => WorkshopDefinitions;

    public SimulationRun Run(SimulationScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.Ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(scenario));

        Reset(scenario.Seed);
        return Continue(scenario);
    }

    public SimulationRun Continue(SimulationScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.Ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(scenario));
        if (scenario.Seed != seed)
            throw new ArgumentException("Continuation must use the current scenario seed.", nameof(scenario));

        for (var tick = 0; tick < scenario.Ticks; tick++)
            core.Tick(scenario.Mode);

        return new SimulationRun(core.Events.Events.Select(ToTraceEntry).ToArray(), Snapshot());
    }

    public SimulationStateSnapshot Snapshot()
    {
        var summary = core.Summarize();
        var resources = summary.Resources.ToDictionary(
            resource => resource.Id,
            resource => resource.Amount,
            StringComparer.Ordinal);
        var workshops = state.Workshops.ToDictionary(
            workshop => workshop.Key,
            workshop => workshop.Value.ToString(),
            StringComparer.Ordinal);

        return new SimulationStateSnapshot(
            summary.Tick,
            summary.ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture),
            state.RequestedSeconds.ToString("R", CultureInfo.InvariantCulture),
            state.CalendarDays,
            resources,
            workshops,
            summary.Food,
            summary.FoodCapacity,
            activeResearch,
            completedResearch.ToArray());
    }

    public bool TryStartResearch(ResearchPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        if (activeResearch is not null)
            return false;

        var balances = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
        foreach (var cost in payment.Cost)
        {
            if (!ExpantaNum.TryParse(cost.Value, out var parsedCost) || parsedCost < ExpantaNum.Zero)
                throw new ArgumentOutOfRangeException(nameof(payment));

            var balance = state.GetResource(cost.Key);
            balances.Add(cost.Key, balance);
            if (balance < cost.Value)
                return false;
        }

        foreach (var cost in payment.Cost)
        {
            var amount = ExpantaNum.Parse(cost.Value);
            state.SetResource(cost.Key, balances[cost.Key] - amount);
        }

        activeResearch = payment.ResearchId;
        return true;
    }

    public bool TryPurchaseWorkshop(string workshopId)
    {
        var workshop = WorkshopDefinitions.FirstOrDefault(rule => StringComparer.Ordinal.Equals(rule.Id, workshopId));
        if (workshop is null || state.Workshops.TryGetValue(workshop.Id, out var count) && count > ExpantaNum.Zero)
            return false;

        var costs = workshop.ResourceRequirements.ToDictionary(
            pair => pair.Key,
            pair => ExpantaNum.Parse(pair.Value),
            StringComparer.Ordinal);
        if (!state.TryPay(costs))
            return false;

        state.SetWorkshop(workshop.Id, ExpantaNum.One);
        return true;
    }

    public string Save()
    {
        var snapshot = Snapshot();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(SaveVersion);
            writer.Write(seed);
            writer.Write(snapshot.Tick);
            writer.Write(snapshot.ElapsedSeconds);
            writer.Write(snapshot.RequestedSeconds);
            writer.Write(snapshot.CalendarDays);
            writer.Write(snapshot.Food);
            writer.Write(snapshot.FoodCapacity);
            writer.Write(snapshot.ActiveResearch ?? string.Empty);
            writer.Write(snapshot.CompletedResearch.Count);
            foreach (var researchId in snapshot.CompletedResearch)
                writer.Write(researchId);

            writer.Write(snapshot.Resources.Count);
            foreach (var resource in snapshot.Resources.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.Write(resource.Key);
                writer.Write(resource.Value);
            }

            writer.Write(snapshot.Workshops.Count);
            foreach (var workshop in snapshot.Workshops.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.Write(workshop.Key);
                writer.Write(workshop.Value);
            }
        }

        return Convert.ToBase64String(stream.ToArray());
    }

    public void Restore(string save)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(save);
        using var stream = new MemoryStream(Convert.FromBase64String(save));
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        if (reader.ReadInt32() != SaveVersion)
            throw new InvalidDataException("Unsupported validation save version.");

        var restoredSeed = reader.ReadInt32();
        var restoredTick = reader.ReadInt64();
        var restoredElapsedSeconds = reader.ReadString();
        var restoredRequestedSeconds = reader.ReadString();
        var restoredCalendarDays = reader.ReadInt32();
        var restoredFood = reader.ReadString();
        var restoredFoodCapacity = reader.ReadString();
        var restoredActiveResearch = reader.ReadString();

        var restoredCompleted = ReadStrings(reader);
        var restoredResources = ReadResources(reader);
        var restoredWorkshops = ReadResources(reader);
        if (stream.Position != stream.Length)
            throw new InvalidDataException("Unexpected trailing validation save data.");

        state = new SimulationState(restoredFood, restoredFoodCapacity);
        foreach (var resource in restoredResources)
            state.SetResource(resource.Key, resource.Value);
        foreach (var workshop in restoredWorkshops)
            state.SetWorkshop(workshop.Key, ExpantaNum.Parse(workshop.Value));

        state.RestoreClock(
            restoredTick,
            ParseSeconds(restoredElapsedSeconds),
            ParseSeconds(restoredRequestedSeconds),
            restoredCalendarDays);
        seed = restoredSeed;
        core = new SimulationCore(state, new ValidationRules(seed), 1d, 1d);
        activeResearch = string.IsNullOrEmpty(restoredActiveResearch) ? null : restoredActiveResearch;
        completedResearch.Clear();
        completedResearch.UnionWith(restoredCompleted);
    }

    private void Reset(int scenarioSeed)
    {
        seed = scenarioSeed;
        state = new SimulationState("0", FoodCapacity);
        foreach (var resource in Rules.Where(rule => !rule.IsFood))
            state.SetResource(resource.Id, "0");

        core = new SimulationCore(state, new ValidationRules(seed), 1d, 1d);
        activeResearch = null;
        completedResearch.Clear();
    }

    private static TraceEntry ToTraceEntry(SimulationEvent simulationEvent)
    {
        IReadOnlyDictionary<string, string> values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = simulationEvent.Amount.ToString(),
            ["detail"] = simulationEvent.Detail,
            ["elapsedSeconds"] = simulationEvent.ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture),
            ["mode"] = simulationEvent.Mode.ToString(),
            ["sequence"] = simulationEvent.Sequence.ToString(CultureInfo.InvariantCulture)
        };

        return new TraceEntry(
            simulationEvent.Tick,
            simulationEvent.Kind.ToString(),
            simulationEvent.Id,
            values);
    }

    private static IReadOnlyList<string> ReadStrings(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count < 0)
            throw new InvalidDataException("Negative string count in validation save.");

        var values = new string[count];
        for (var index = 0; index < count; index++)
            values[index] = reader.ReadString();
        return values;
    }

    private static IReadOnlyDictionary<string, string> ReadResources(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count < 0)
            throw new InvalidDataException("Negative resource count in validation save.");

        var resources = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
            resources.Add(reader.ReadString(), reader.ReadString());
        return resources;
    }

    private static ExpantaNum ParseAmount(string amount) => ExpantaNum.Parse(amount);

    private static double ParseSeconds(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    private sealed class ValidationRules(int scenarioSeed) : ISimulationRules
    {
        public void ApplyTick(SimulationState simulationState, SimulationTickContext context)
        {
            var woodDelta = 1 + PositiveModulo(scenarioSeed + context.Tick, 3);
            var stoneDelta = 1 + PositiveModulo(scenarioSeed - context.Tick, 2);
            var plankDelta = 1 + PositiveModulo(scenarioSeed + context.Tick, 2);
            var alloyDelta = 1 + PositiveModulo(scenarioSeed - context.Tick, 3);
            simulationState.AddResource("WoodLog", woodDelta.ToString(CultureInfo.InvariantCulture));
            simulationState.AddResource("Stone", stoneDelta.ToString(CultureInfo.InvariantCulture));
            simulationState.AddResource("Plank", plankDelta.ToString(CultureInfo.InvariantCulture));
            simulationState.AddResource("Alloy", alloyDelta.ToString(CultureInfo.InvariantCulture));
            simulationState.SetFood((context.Tick * 2L).ToString(CultureInfo.InvariantCulture));
            context.Events.Add(
                context.Tick,
                simulationState.ElapsedSeconds,
                context.Mode,
                SimulationEventKind.Diagnostic,
                "validation-state",
                $"WoodLog={simulationState.GetResource("WoodLog")};Stone={simulationState.GetResource("Stone")};Food={simulationState.Food}",
                woodDelta + stoneDelta);

            if (simulationState.Workshops.TryGetValue("Sawmill", out var sawmills) && sawmills > ExpantaNum.Zero)
            {
                simulationState.ConsumeResource("WoodLog", ExpantaNum.One);
                simulationState.AddResource("Plank", new ExpantaNum(2));
                context.Events.Add(
                    context.Tick,
                    simulationState.ElapsedSeconds,
                    context.Mode,
                    SimulationEventKind.ResourceChanged,
                    "Sawmill",
                    "WoodLog:1;Plank:2",
                    new ExpantaNum(1));

                simulationState.ConsumeResource("Plank", new ExpantaNum(2));
                simulationState.AddResource("Alloy", ExpantaNum.One);
                context.Events.Add(
                    context.Tick,
                    simulationState.ElapsedSeconds,
                    context.Mode,
                    SimulationEventKind.ResourceChanged,
                    "AlloyForge",
                    "Plank:2;Alloy:1",
                    ExpantaNum.One);
            }
        }

        private static long PositiveModulo(long value, int divisor)
        {
            var remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
