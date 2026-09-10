using System.Globalization;

namespace Kingdom.NewEconomySimulator.Tests;

public static class ValidationSuite
{
    public static ValidationReport RunCore()
    {
        var report = Run(new CoreValidationAdapter());
        CheckEraStepSchedule(report);
        AddTraceComparerChecks(report);
        AddReportContractCheck(report);
        return report;
    }

    private static void CheckEraStepSchedule(ValidationReport report)
    {
        EraStep[] expected =
        [
            new("Animal", 0.1d, 1d),
            new("StoneAge", 0.5d, 2d),
            new("Medieval", 1d, 5d),
            new("Industrial", 5d, 10d),
            new("Spacer", 15d, 20d),
            new("Ultra", 30d, 30d),
            new("Archotech", 60d, 60d)
        ];

        var schedule = EraStepSchedule.Default;
        // Exact equality is required here because these are protocol-like schedule constants,
        // not calculated economy values.
        bool tableMatches = schedule.Steps.SequenceEqual(expected);

        var state = new SimulationState("0", "10");
        var core = new SimulationCore(state, new DeterministicRules(), eraStepSchedule: schedule);
        core.Tick(SimulationMode.Realtime);
        state.SetTechLevel("StoneAge");
        core.Tick(SimulationMode.Realtime);
        state.SetTechLevel("Medieval");
        core.Tick(SimulationMode.Offline);

        bool boundariesMatch = Math.Abs(state.ElapsedSeconds - 5.6d) <= 1e-9d &&
            Math.Abs(state.RequestedSeconds - 5.6d) <= 1e-9d && state.Tick == 3;

        var snapshot = new EconomySnapshot
        {
            TechLevels = EraStepSchedule.ExpectedTechLevels.ToList()
        };
        var parityCore = SimulationCore.FromSnapshot(snapshot);
        parityCore.State.SetTechLevel("Industrial");
        bool parityDefaultsMatchUnity = parityCore.CurrentStepSeconds(SimulationMode.Realtime) == 0.1d &&
            parityCore.CurrentStepSeconds(SimulationMode.Offline) == 60d;
        var adaptiveCore = SimulationCore.FromSnapshotWithEraSteps(snapshot);
        adaptiveCore.State.SetTechLevel("Industrial");
        bool adaptiveIsExplicit = adaptiveCore.CurrentStepSeconds(SimulationMode.Realtime) == 5d &&
            adaptiveCore.CurrentStepSeconds(SimulationMode.Offline) == 10d;

        var offlineState = new SimulationState("0", "10");
        var offlineCore = new SimulationCore(
            offlineState,
            new DeterministicRules(),
            tickSeconds: 0.1d,
            offlineTickSeconds: 3600d);
        new ScenarioRunner(offlineCore).RunDetailed(
        [
            new("first-offline-session", SimulationMode.Offline, 9),
            new("second-offline-session", SimulationMode.Offline, 1)
        ]);
        bool offlineSessionsReset = Math.Abs(offlineState.ElapsedSeconds - 24660d) <= 1e-9d &&
            Math.Abs(offlineState.RequestedSeconds - 36000d) <= 1e-9d &&
            offlineState.Tick == 10;

        bool invalidRealtimeDurationsRejected = new[] { 0d, -1d, double.NaN, double.PositiveInfinity }
            .All(value =>
            {
                EraStep[] candidate = expected.ToArray();
                candidate[0] = candidate[0] with { RealtimeSeconds = value };
                return Throws<ArgumentException>(() => new EraStepSchedule(candidate));
            });
        EraStep[] invalidOfflineDuration = expected.ToArray();
        invalidOfflineDuration[0] = invalidOfflineDuration[0] with { OfflineSeconds = double.PositiveInfinity };
        EraStep[] invalidId = expected.ToArray();
        invalidId[0] = invalidId[0] with { TechLevel = "animal" };
        EraStep[] nullStep = expected.ToArray();
        nullStep[0] = null!;
        bool invalidInputsRejected =
            Throws<ArgumentException>(() => new EraStepSchedule(Array.Empty<EraStep>())) &&
            Throws<ArgumentException>(() => new EraStepSchedule(expected.Reverse())) &&
            invalidRealtimeDurationsRejected &&
            Throws<ArgumentException>(() => new EraStepSchedule(invalidOfflineDuration)) &&
            Throws<ArgumentException>(() => new EraStepSchedule(invalidId)) &&
            Throws<ArgumentException>(() => new EraStepSchedule(nullStep)) &&
            Throws<InvalidOperationException>(() => schedule.GetSeconds("Unknown", SimulationMode.Realtime)) &&
            Throws<ArgumentOutOfRangeException>(() => schedule.GetSeconds("Animal", (SimulationMode)99)) &&
            Throws<ArgumentOutOfRangeException>(() => parityCore.CurrentStepSeconds((SimulationMode)99)) &&
            Throws<ArgumentOutOfRangeException>(() => new ScenarioRunner(parityCore).RunDetailed(
                [new("invalid-mode", (SimulationMode)99, 1)]));

        bool passed = tableMatches && boundariesMatch && parityDefaultsMatchUnity &&
            adaptiveIsExplicit && offlineSessionsReset && invalidInputsRejected;
        AddResult(
            report,
            "era step schedule",
            passed,
            passed
                ? "Parity defaults stay at Unity's fixed steps; explicit era-adaptive steps, transition boundaries, offline-session resets, and invalid inputs follow the contract."
                : "Era schedule, parity default, adaptive mode, offline-session reset, or input validation differs from the contract.");
    }

    private static bool Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    public static ValidationReport Run(INewEconomySimulatorValidationApi? api)
    {
        var report = new ValidationReport { Executed = api is not null };
        if (api is null)
        {
            report.ApiGaps.Add(new("INewEconomySimulatorValidationApi", "A strong-typed core adapter is required."));
            AddSkipped(report, "determinism", "Run(scenario) unavailable.");
            AddSkipped(report, "ordinary resource capacity", "ResourceRules and Run(scenario) unavailable.");
            AddSkipped(report, "Food capacity", "ResourceRules and Run(scenario) unavailable.");
            AddSkipped(report, "atomic research payment", "TryStartResearch(payment) unavailable.");
            AddSkipped(report, "workshop production chain", "TryPurchaseWorkshop(workshopId) unavailable.");
            AddSkipped(report, "save restore", "Save(), Restore(save), Snapshot() unavailable.");
            AddSkipped(report, "realtime/offline parity", "Continue(scenario) unavailable.");
            AddSkipped(report, "large ExpantaNum", "SimulationState unavailable.");
            return report;
        }

        CheckDeterminism(api, report);
        CheckResourceCapacity(api, report);
        CheckFoodCapacity(api, report);
        CheckAtomicResearchPayment(api, report);
        CheckSaveRestore(api, report);
        CheckGeometricCost(report);
        CheckWorkshopProductionChain(api, report);
        CheckRealtimeOfflineParity(api, report);
        CheckLargeExpantaNum(report);
        return report;
    }

    private static void AddResult(ValidationReport report, string name, bool passed, string detail,
        IReadOnlyList<TraceDifference>? differences = null, ComparisonEvidence? comparison = null)
    {
        report.Results.Add(new(name, passed, detail, differences ?? Array.Empty<TraceDifference>(), comparison?.Name));
        if (comparison is not null)
            report.Comparisons.Add(comparison);
    }

    private static ComparisonEvidence CompareRuns(string name, string expectedSource, string actualSource,
        SimulationRun expected, SimulationRun actual, IReadOnlyCollection<string>? ignoredFields = null)
    {
        var differences = TraceComparer.Compare(expected.Trace, actual.Trace, ignoredFields);
        var firstDifference = TraceComparer.First(differences, expected.Trace, actual.Trace);
        var matches = differences.Count == 0 && StateEquals(expected.State, actual.State);
        return new(name, matches,
        [
            new(expectedSource, expected.State, expected.Trace),
            new(actualSource, actual.State, actual.Trace)
        ], differences, firstDifference);
    }

    private static void CheckGeometricCost(ValidationReport report)
    {
        var baseCost = new ExpantaNum(10);
        var growth = new ExpantaNum("1.15");
        var first = SimulationState.GeometricCost(baseCost, growth, 0, 1);
        var second = SimulationState.GeometricCost(baseCost, growth, 1, 1);
        var bulk = SimulationState.GeometricCost(baseCost, growth, 0, 2);
        var linear = SimulationState.GeometricCost(baseCost, ExpantaNum.One, 0, 3);
        // Zero is the required result for an empty bulk purchase.
        var passed = second > first
            && bulk >= first + second
            && SimulationState.GeometricCost(baseCost, growth, 0, 0) == ExpantaNum.Zero
            && linear > first;
        AddResult(report, "building geometric bulk cost", passed,
            passed
                ? "Successive single costs increase and closed-form bulk cost covers the first two costs."
                : "Building cost growth or closed-form bulk aggregation is inconsistent.");
    }

    private static void CheckRealtimeOfflineParity(INewEconomySimulatorValidationApi api, ValidationReport report)
    {
        var realtime = api.Run(new SimulationScenario("mode-realtime", 19, 12, SimulationMode.Realtime));
        var offline = api.Run(new SimulationScenario("mode-offline", 19, 12, SimulationMode.Offline));
        var comparison = CompareRuns(
            "realtime/offline parity",
            "Realtime",
            "Offline",
            realtime,
            offline,
            ["values.mode"]);
        AddResult(report, "realtime/offline parity", comparison.Matches,
            comparison.Matches
                ? "Realtime and offline produce identical states and event order; only the mode marker differs."
                : "Realtime and offline differ in state or ordered events beyond the mode marker.",
            comparison.Differences,
            comparison);
    }

    private static void CheckDeterminism(INewEconomySimulatorValidationApi api, ValidationReport report)
    {
        var scenario = new SimulationScenario("validation", 17, 120);
        var first = api.Run(scenario);
        var second = api.Run(scenario);
        var comparison = CompareRuns("self-determinism", "Simulator run 1", "Simulator run 2", first, second);
        AddResult(report,
            "determinism",
            comparison.Matches,
            comparison.Matches
                ? "Same seed produced the same trace and terminal state."
                : "Same seed produced different output.",
            comparison.Differences,
            comparison);
    }

    private static void CheckResourceCapacity(INewEconomySimulatorValidationApi api, ValidationReport report)
    {
        var ordinaryRules = api.ResourceRules.Where(rule => !rule.IsFood).ToArray();
        var metadataValid = ordinaryRules.Length > 0 && ordinaryRules.All(rule => rule.Capacity is null);
        var run = api.Run(new SimulationScenario("ordinary-resource-capacity", 31, 64));
        var foodCapacity = ParseAmount(run.State.FoodCapacity);
        var grewPastFoodCapacity = ordinaryRules.All(rule =>
            run.State.Resources.TryGetValue(rule.Id, out var amount) && ParseAmount(amount) > foodCapacity);
        var passed = metadataValid && grewPastFoodCapacity;

        AddResult(report,
            "ordinary resource capacity",
            passed,
            passed
                ? "All ordinary resources remained uncapped and grew beyond the Food cap probe."
                : "An ordinary resource declared a cap or failed the uncapped growth probe.",
            comparison: new("ordinary resource capacity", passed,
            [
                new("Simulator", run.State, run.Trace)
            ], Array.Empty<TraceDifference>(), null));
    }

    private static void CheckFoodCapacity(INewEconomySimulatorValidationApi api, ValidationReport report)
    {
        var foods = api.ResourceRules.Where(rule => rule.IsFood).ToArray();
        var run = api.Run(new SimulationScenario("food-capacity", 47, 64));
        var food = ParseAmount(run.State.Food);
        var foodCapacity = ParseAmount(run.State.FoodCapacity);
        var valid = foods.Length == 1
            && foods[0].Capacity is not null && ParseAmount(foods[0].Capacity) > ExpantaNum.Zero
            && food > ExpantaNum.Zero
            && food <= foodCapacity
            && food < new ExpantaNum(64);

        var detail = valid
            ? $"Food stayed within its positive {foodCapacity} cap under an overfill probe."
            : "Exactly one positive Food cap must clamp an attempted overfill.";
        AddResult(report, "Food capacity", valid, detail,
            comparison: new("Food capacity", valid,
            [
                new("Simulator", run.State, run.Trace)
            ], Array.Empty<TraceDifference>(), null));
    }

    private static void CheckAtomicResearchPayment(INewEconomySimulatorValidationApi api, ValidationReport report)
    {
        api.Run(new SimulationScenario("research-payment", 5, 1));
        var before = api.Snapshot();
        var stone = ParseAmount(before.Resources["Stone"]);
        var rejected = api.TryStartResearch(new ResearchPayment(
            "AtomicPaymentProbe",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WoodLog"] = "1",
                ["Stone"] = (stone + ExpantaNum.One).ToString()
            }));
        var afterRejected = api.Snapshot();

        var accepted = api.TryStartResearch(new ResearchPayment(
            "AtomicPaymentProbe",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WoodLog"] = "1",
                ["Stone"] = "1"
            }));
        var afterAccepted = api.Snapshot();
        var repeated = api.TryStartResearch(new ResearchPayment(
            "AtomicPaymentProbe",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WoodLog"] = "1",
                ["Stone"] = "1"
            }));
        var afterRepeated = api.Snapshot();
        var paidOnAccept = ParseAmount(afterAccepted.Resources["WoodLog"]) < ParseAmount(afterRejected.Resources["WoodLog"])
            && ParseAmount(afterAccepted.Resources["Stone"]) < ParseAmount(afterRejected.Resources["Stone"]);
        // Exact subtraction is required by the one-time atomic payment contract.
        var paidExactlyOnce = ParseAmount(afterAccepted.Resources["WoodLog"]) == ParseAmount(before.Resources["WoodLog"]) - ExpantaNum.One
            && ParseAmount(afterAccepted.Resources["Stone"]) == ParseAmount(before.Resources["Stone"]) - ExpantaNum.One;
        var passed = !rejected
            && StateEquals(before, afterRejected)
            && accepted
            && StringComparer.Ordinal.Equals(afterAccepted.ActiveResearch, "AtomicPaymentProbe")
            && paidOnAccept
            && paidExactlyOnce
            && !repeated
            && StateEquals(afterAccepted, afterRepeated);

        AddResult(report,
            "atomic research payment",
            passed,
            passed
                ? "Unaffordable payment changed nothing; affordable payment deducted each cost once; activation blocks a second payment."
                : "Research payment was partial, accepted while unaffordable, double-paid, or failed to start after full payment.",
            comparison: new("atomic research payment", passed,
            [
                new("Before", before, Array.Empty<TraceEntry>()),
                new("After rejection", afterRejected, Array.Empty<TraceEntry>()),
                new("After acceptance", afterAccepted, Array.Empty<TraceEntry>()),
                new("After repeated attempt", afterRepeated, Array.Empty<TraceEntry>())
            ], Array.Empty<TraceDifference>(), null));
    }

    private static void CheckSaveRestore(INewEconomySimulatorValidationApi api, ValidationReport report)
    {
        var firstPhase = new SimulationScenario("save-resume", 23, 4);
        var secondPhase = new SimulationScenario("save-resume", 23, 3);
        var baseline = api.Run(firstPhase);
        var baselinePurchased = api.TryPurchaseWorkshop("Sawmill");
        baseline = api.Continue(secondPhase);

        api.Run(firstPhase);
        var resumedPurchased = api.TryPurchaseWorkshop("Sawmill");
        var save = api.Save();
        var saved = api.Snapshot();
        api.Restore(save);
        var restored = api.Snapshot();
        var resumed = api.Continue(secondPhase);
        var expectedTail = baseline.Trace.Where(entry => entry.Tick > firstPhase.Ticks).ToArray();
        var differences = TraceComparer.Compare(expectedTail, resumed.Trace, ["values.sequence"]);
        var passed = baselinePurchased
            && resumedPurchased
            && StateEquals(saved, restored)
            && StateEquals(baseline.State, resumed.State)
            && differences.Count == 0;

        AddResult(report,
            "save state resume",
            passed,
            passed
                ? "Restored state matches its save and resumed state/events match the uninterrupted run."
                : "Restore changed state or resumed state/events diverged from the uninterrupted run.",
            differences,
            new("save state resume", passed,
            [
                new("Uninterrupted", baseline.State, baseline.Trace),
                new("Resumed", resumed.State, resumed.Trace)
            ], differences, TraceComparer.First(differences, expectedTail, resumed.Trace)));
    }

    private static void CheckWorkshopProductionChain(INewEconomySimulatorValidationApi api, ValidationReport report)
    {
        var before = api.Run(new SimulationScenario("workshop-chain", 29, 2));
        var purchased = api.TryPurchaseWorkshop("Sawmill");
        var afterPurchase = api.Snapshot();
        var chained = api.Continue(new SimulationScenario("workshop-chain", 29, 2));
        var repeated = api.TryPurchaseWorkshop("Sawmill");
        var afterRepeated = api.Snapshot();
        var withoutWorkshop = api.Run(new SimulationScenario("workshop-chain-control", 29, 4));

        var sawmillEvents = chained.Trace
            .Select((entry, index) => (Index: index, Entry: entry))
            .Where(item => StringComparer.Ordinal.Equals(item.Entry.Subject, "Sawmill"))
            .ToArray();
        var forgeEvents = chained.Trace
            .Select((entry, index) => (Index: index, Entry: entry))
            .Where(item => StringComparer.Ordinal.Equals(item.Entry.Subject, "AlloyForge"))
            .ToArray();
        // Exact one is the required discrete purchased-workshop count.
        var passed = purchased
            && !repeated
            && StateEquals(chained.State, afterRepeated)
            && ParseAmount(afterPurchase.Workshops["Sawmill"]) == ExpantaNum.One
            && ParseAmount(afterPurchase.Resources["WoodLog"]) < ParseAmount(before.State.Resources["WoodLog"])
            && ParseAmount(chained.State.Resources["WoodLog"]) < ParseAmount(withoutWorkshop.State.Resources["WoodLog"])
            && ParseAmount(chained.State.Resources["Plank"]) >= ParseAmount(withoutWorkshop.State.Resources["Plank"])
            && ParseAmount(chained.State.Resources["Alloy"]) > ParseAmount(withoutWorkshop.State.Resources["Alloy"])
            && sawmillEvents.Length == 2
            && forgeEvents.Length == 2
            && sawmillEvents.Zip(forgeEvents).All(pair => pair.First.Index < pair.Second.Index);

        AddResult(report,
            "workshop/production chain",
            passed,
            passed
                ? "Workshop purchase is single-shot and its ordered WoodLog-Plank-Alloy chain changes every stage output."
                : "Workshop purchase, event order, or a production-chain stage failed.",
            comparison: new("workshop/production chain", passed,
            [
                new("Before purchase", before.State, before.Trace),
                new("After purchase", afterPurchase, Array.Empty<TraceEntry>()),
                new("After chain", chained.State, chained.Trace)
            ], Array.Empty<TraceDifference>(), null));
    }

    private static void CheckLargeExpantaNum(ValidationReport report)
    {
        var initial = ExpantaNum.Parse("1e308");
        var state = new SimulationState("0", "1");
        state.SetResource("WoodLog", initial);
        state.AddResource("WoodLog", initial);
        var doubled = state.GetResource("WoodLog");
        var bulkCost = SimulationState.GeometricCost(initial, new ExpantaNum(2), 10, 10);
        var passed = initial.IsFinite
            && doubled.IsFinite
            && bulkCost.IsFinite
            && doubled > initial
            && bulkCost > initial;
        AddResult(report,
            "large ExpantaNum",
            passed,
            passed
                ? "Large values remain finite through state addition and closed-form geometric cost."
                : "A large ExpantaNum state or cost operation became non-finite or regressed.");
    }

    private static void AddTraceComparerChecks(ValidationReport report)
    {
        var expected = new[]
        {
            TraceEntryFor(1, "1", "Realtime"),
            TraceEntryFor(2, "2", "Realtime")
        };
        var actual = new[]
        {
            TraceEntryFor(1, "1", "Realtime"),
            TraceEntryFor(2, "3", "Realtime")
        };
        var differences = TraceComparer.Compare(expected, actual);
        var first = TraceComparer.First(differences, expected, actual);
        var firstValid = differences.Count == 1
            && first is { Index: 1, Tick: 2, Field: "values.amount", Expected: "2", Actual: "3" };
        AddResult(report,
            "trace first difference",
            firstValid,
            firstValid ? "The first differing event reports index, tick, and field." : "Trace comparison lost the first difference facts.",
            differences,
            new("trace first difference", firstValid,
            [
                new("Expected", SnapshotFor(2), expected),
                new("Actual", SnapshotFor(2), actual)
            ], differences, first));

        var realtime = new[] { TraceEntryFor(1, "1", "Realtime") };
        var offline = new[] { TraceEntryFor(1, "1", "Offline") };
        var ignored = TraceComparer.Compare(realtime, offline, ["values.mode"]);
        AddResult(report,
            "trace field exclusion",
            ignored.Count == 0,
            ignored.Count == 0 ? "Named fields can be excluded from parity comparison." : "Named field exclusion did not work.",
            ignored);
    }

    private static void AddReportContractCheck(ValidationReport report)
    {
        var unity = new[] { TraceEntryFor(1, "1", "Realtime") };
        var simulator = new[] { TraceEntryFor(2, "3", "Realtime") };
        var differences = new[] { new TraceDifference(1, "values.amount", "1", "3") };
        var comparison = new ComparisonEvidence(
            "Unity/simulator report contract",
            false,
        [
            new("Unity", SnapshotFor(1), unity),
            new("Simulator", SnapshotFor(2), simulator)
        ], differences, TraceComparer.First(differences, unity, simulator), IsDiagnostic: true);
        var fixture = new ValidationReport { Executed = true };
        fixture.Results.Add(new("report contract", true, "Controlled first-difference fixture.", differences, comparison.Name));
        fixture.Comparisons.Add(comparison);

        var acceptanceFixture = new ValidationReport { Executed = true };
        acceptanceFixture.Results.Add(new(
            "acceptance comparison",
            true,
            "Aggregate comparison probe.",
            Array.Empty<TraceDifference>()));
        acceptanceFixture.Comparisons.Add(comparison with { IsDiagnostic = false });
        bool acceptanceMismatchFails = !acceptanceFixture.Passed;

        var json = ReportWriter.ToJson(fixture);
        var csv = ReportWriter.ToCsv(fixture);
        var markdown = ReportWriter.ToMarkdown(fixture);
        var requiredJson = new[] { "comparisons", "sources", "orderedEvents", "firstDifference", "tick", "field", "Unity", "Simulator" };
        var requiredCsv = new[] { "snapshot", "event", "first-difference", "Unity", "Simulator", "values.amount" };
        var requiredMarkdown = new[] { "## Comparisons", "#### Snapshots", "#### Ordered events", "#### First difference", "Unity", "Simulator", "values.amount" };
        var forbidden = new[] { "pacing", "balance", "acceptance" };
        var passed = acceptanceMismatchFails
            && requiredJson.All(value => json.Contains(value))
            && requiredCsv.All(value => csv.Contains(value))
            && requiredMarkdown.All(value => markdown.Contains(value))
            && !forbidden.Any(value => json.Contains(value, StringComparison.OrdinalIgnoreCase)
                || csv.Contains(value, StringComparison.OrdinalIgnoreCase)
                || markdown.Contains(value, StringComparison.OrdinalIgnoreCase));

        AddResult(report,
            "JSON/CSV/Markdown report contract",
            passed,
            passed
                ? "All report formats contain snapshots, ordered events, first-difference tick/field, and the Unity/simulator fixture comparison."
                : "A report format omitted required parity facts or contained an out-of-scope conclusion.",
            comparison: comparison);
    }

    private static TraceEntry TraceEntryFor(long tick, string amount, string mode)
    {
        IReadOnlyDictionary<string, string> values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = amount,
            ["detail"] = "fixture",
            ["elapsedSeconds"] = tick.ToString(CultureInfo.InvariantCulture),
            ["mode"] = mode,
            ["sequence"] = tick.ToString(CultureInfo.InvariantCulture)
        };
        return new(tick, "Diagnostic", "Fixture", values);
    }

    private static SimulationStateSnapshot SnapshotFor(long tick) => new(
        tick,
        tick.ToString(CultureInfo.InvariantCulture),
        tick.ToString(CultureInfo.InvariantCulture),
        0,
        new Dictionary<string, string>(StringComparer.Ordinal) { ["WoodLog"] = tick.ToString(CultureInfo.InvariantCulture) },
        new Dictionary<string, string>(StringComparer.Ordinal),
        "1",
        "10",
        null,
        Array.Empty<string>());

    private static bool StateEquals(SimulationStateSnapshot left, SimulationStateSnapshot right)
    {
        // Exactness is required here because determinism and save restoration are serialization contracts.
        return left.Tick == right.Tick
            && StringComparer.Ordinal.Equals(left.ElapsedSeconds, right.ElapsedSeconds)
            && StringComparer.Ordinal.Equals(left.RequestedSeconds, right.RequestedSeconds)
            && left.CalendarDays == right.CalendarDays
            && StringComparer.Ordinal.Equals(left.Food, right.Food)
            && StringComparer.Ordinal.Equals(left.FoodCapacity, right.FoodCapacity)
            && StringComparer.Ordinal.Equals(left.ActiveResearch, right.ActiveResearch)
            && left.CompletedResearch.SequenceEqual(right.CompletedResearch, StringComparer.Ordinal)
            && left.Resources.Count == right.Resources.Count
            && left.Workshops.Count == right.Workshops.Count
            && left.Resources.All(pair =>
                right.Resources.TryGetValue(pair.Key, out var value)
                && StringComparer.Ordinal.Equals(pair.Value, value))
            && left.Workshops.All(pair =>
                right.Workshops.TryGetValue(pair.Key, out var value)
                && StringComparer.Ordinal.Equals(pair.Value, value));
    }

    private static ExpantaNum ParseAmount(string amount) => ExpantaNum.Parse(amount);

    private static void AddSkipped(ValidationReport report, string name, string why) =>
        report.Results.Add(new(name, false, $"SKIPPED: {why}", Array.Empty<TraceDifference>()));
}
