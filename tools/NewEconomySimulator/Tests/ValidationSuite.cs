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
        CheckSettledResourceBudgets(report);
        CheckHappinessShortageMonotonicity(report);
        CheckPopulationTiming(report);
        CheckResearchTiming(report);
        CheckPostPopulationHappiness(report);
        CheckSnapshotPopulationProgress(report);
        CheckSnapshotQueueOrder(report);
        CheckSnapshotDefinitionNamespaces(report);
        CheckSectorWithoutEra(report);
        CheckBaseResourceProduction(report);
        CheckSnapshotCommandOptions(report);
        CheckSnapshotComparisonBoundaries(report);
        CheckOfflineOperationCadence(report);
        CheckSnapshotOperationTiming(report);
        CheckSnapshotTimingComparison(report);
        CheckHomeSystemDevelopmentContract(report);
        CheckWorkshopProjectPrerequisites(report);
        CheckInteriorExploration(report);
        return report;
    }

    private static EconomySnapshot InteriorSnapshot()
    {
        var snapshot = BudgetSnapshot();
        snapshot.Sectors.Add(new SectorSnapshot
        {
            Id = "Ocean", Guid = new string('d', 32), AssetPath = "fixture/Ocean.asset",
            Domain = "HomeSystem", StarSystemId = "Sol", InteriorExploration = new()
            {
                Label = "Ocean survey", Description = "Drill ice", CompletionDescription = "Ocean found",
                DurationSeconds = "10", Milestones = ["Scan", "Drill", "Dive"],
                ResourceRatesPerSecond = [new() { ResourceId = "A", Value = "2" }, new() { ResourceId = "B", Value = "3" }]
            }
        });
        var body = BudgetBuilding("Ark", "", "0", "", "0");
        body.Guid = new string('e', 32); body.SectorId = "Ocean";
        body.RequiresInteriorExploration = true; body.MaxAmount = 1;
        body.ResourceRequirements = [new() { ResourceId = "A", Value = "5" }];
        snapshot.Buildings.Add(body);
        snapshot.RuntimeState.Sectors = [new() { SectorId = "Ocean", Occupied = true, Unlocked = true, Progress = "1" }];
        snapshot.InitialState.Sectors = snapshot.RuntimeState.Sectors;
        snapshot.SaveState.Sectors = snapshot.RuntimeState.Sectors;
        snapshot.RuntimeState.Resources = [new() { ResourceId = "A", Value = "100" }, new() { ResourceId = "B", Value = "100" }];
        snapshot.RuntimeState.Buildings = [new() { BuildingId = "Ark", Amount = "0" }];
        snapshot.InitialState.Resources = snapshot.RuntimeState.Resources;
        snapshot.SaveState.Resources = snapshot.RuntimeState.Resources;
        snapshot.InitialState.Buildings = snapshot.RuntimeState.Buildings;
        snapshot.SaveState.Buildings = snapshot.RuntimeState.Buildings;
        return snapshot;
    }

    private static void CheckInteriorExploration(ValidationReport report)
    {
        bool Near(ExpantaNum actual, string target) => (actual - ExpantaNum.Parse(target)).Abs() < new ExpantaNum(1e-7d);
        var snapshot = InteriorSnapshot();
        var core = SimulationCore.FromSnapshot(snapshot);
        var state = core.State; var rules = core.SnapshotRules!;
        var passed = !rules.TryPurchaseBuilding(state, "Ark") && Near(state.GetResource("A"), "100");
        state.SetResource("A", "1");
        passed &= rules.TryStartInteriorExploration(state, "Ocean");
        core.Tick(SimulationMode.Realtime, 1, 1);
        var sector = state.GetSectorState("Ocean");
        passed &= Near(sector.InteriorExplorationProgress, ".05") && Near(state.GetResource("A"), "0") &&
            Near(state.GetResource("B"), "98.5") && sector.InteriorExplorationActive && Near(sector.Progress, "1");
        core.Tick(SimulationMode.Realtime, 1, 1);
        passed &= Near(sector.InteriorExplorationProgress, ".05") && Near(state.GetResource("B"), "98.5");
        AddResult(report, "planetary exploration bills affordable partial seconds atomically", passed,
            "Scarce A pays only half a second of both inputs; missing supply preserves progress and leaves the operation active.");

        passed = rules.TryPauseInteriorExploration(state, "Ocean") && sector.InteriorExplorationPaused;
        state.SetResource("A", "100");
        core.Tick(SimulationMode.Realtime, 1, 1);
        passed &= Near(sector.InteriorExplorationProgress, ".05") && Near(state.GetResource("A"), "100");
        passed &= rules.TryStartInteriorExploration(state, "Ocean") && !sector.InteriorExplorationPaused;
        core.Tick(SimulationMode.Realtime, 20, 20);
        passed &= Near(sector.InteriorExplorationProgress, "1") && !sector.InteriorExplorationActive &&
            Near(state.GetResource("A"), "81") && Near(state.GetResource("B"), "70") &&
            !rules.TryStartInteriorExploration(state, "Ocean");
        var before = state.GetResource("A"); core.Tick(SimulationMode.Realtime, 1, 1);
        passed &= Near(state.GetResource("A") - before, "0") && rules.TryPurchaseBuilding(state, "Ark") &&
            !rules.TryPurchaseBuilding(state, "Ark") && Near(state.GetResource("A"), "76");
        AddResult(report, "planetary exploration pause resume completion and unique building gate", passed,
            "Manual pause spends nothing; resume charges remaining duration only; completion stops payment and opens exactly one Ark.");

        SimulationCore Active()
        {
            var result = SimulationCore.FromSnapshot(InteriorSnapshot());
            result.SnapshotRules!.TryStartInteriorExploration(result.State, "Ocean");
            return result;
        }
        var realtime = Active(); var offline = Active();
        for (int tick = 0; tick < 55; tick++) realtime.Tick(SimulationMode.Realtime, .1, .1);
        offline.RunOffline(2.75); offline.RunOffline(2.75);
        passed = Near(realtime.State.GetSectorState("Ocean").InteriorExplorationProgress - offline.State.GetSectorState("Ocean").InteriorExplorationProgress, "0") &&
            Near(realtime.State.GetResource("A") - offline.State.GetResource("A"), "0") &&
            Near(realtime.State.GetResource("B") - offline.State.GetResource("B"), "0") &&
            Math.Abs(offline.OfflinePendingSimulationSeconds) < 1e-9d;
        AddResult(report, "planetary exploration uses ordinary offline windows without a Food cadence remainder", passed,
            $"Realtime={realtime.State.GetSectorState("Ocean").InteriorExplorationProgress}; offline={offline.State.GetSectorState("Ocean").InteriorExplorationProgress}; A={realtime.State.GetResource("A")}/{offline.State.GetResource("A")}; tick={offline.TickSeconds}; pending={offline.OfflinePendingSimulationSeconds}");

        snapshot = InteriorSnapshot();
        var saved = snapshot.RuntimeState.Sectors[0];
        saved.InteriorExplorationProgress = ".25"; saved.InteriorExplorationActive = true;
        var roundtrip = SnapshotExporter.LoadJsonText(System.Text.Json.JsonSerializer.Serialize(snapshot));
        passed = SnapshotValidation.Validate(roundtrip).IsValid &&
            roundtrip.Sectors[0].InteriorExploration!.Milestones.SequenceEqual(new[] { "Scan", "Drill", "Dive" }) &&
            roundtrip.Buildings[0].RequiresInteriorExploration && roundtrip.Buildings[0].MaxAmount == 1;
        var restored = SimulationCore.FromSnapshot(roundtrip);
        passed &= Near(restored.State.GetSectorState("Ocean").InteriorExplorationProgress, ".25") &&
            restored.State.GetSectorState("Ocean").InteriorExplorationActive;
        roundtrip.RuntimeState.Sectors[0].InteriorExplorationProgress = ".5";
        passed &= SnapshotParityCommand.Compare(restored, roundtrip.RuntimeState).Any(d => d.Field.EndsWith("InteriorExplorationProgress"));
        saved.Occupied = false; passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        saved.Occupied = true; saved.InteriorExplorationProgress = "1";
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        saved.InteriorExplorationActive = false; saved.InteriorExplorationProgress = "1.01";
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        AddResult(report, "planetary exploration snapshot roundtrip comparison and invalid states", passed,
            "Ordered milestones, gates and active progress survive; comparison catches drift; unoccupied, active-complete and out-of-range states reject.");

        snapshot = InteriorSnapshot();
        saved = snapshot.RuntimeState.Sectors[0];
        saved.InteriorExplorationPaused = true;
        roundtrip = SnapshotExporter.LoadJsonText(System.Text.Json.JsonSerializer.Serialize(snapshot));
        restored = SimulationCore.FromSnapshot(roundtrip);
        passed = restored.State.GetSectorState("Ocean").InteriorExplorationPaused &&
            Near(restored.State.GetSectorState("Ocean").InteriorExplorationProgress, "0");
        saved.InteriorExplorationActive = true;
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        saved.InteriorExplorationActive = false; saved.InteriorExplorationPaused = false;
        snapshot.Research = [new() { Id = "Probe", Guid = new string('f', 32), AssetPath = "fixture/Probe.asset", TechLevel = "Animal" }];
        snapshot.Sectors[0].InteriorExploration!.RequiredResearchIds = ["Probe"];
        snapshot.RuntimeState.Research = [new() { ResearchId = "Probe", CostPaid = true }];
        snapshot.InitialState.Research = snapshot.RuntimeState.Research;
        snapshot.SaveState.Research = snapshot.RuntimeState.Research;
        core = SimulationCore.FromSnapshot(snapshot); state = core.State; rules = core.SnapshotRules!;
        passed &= !rules.TryStartInteriorExploration(state, "Ocean") && Near(state.GetResource("A"), "100");
        state.SetResearchState("Probe", ExpantaNum.One, true, true);
        passed &= rules.TryStartInteriorExploration(state, "Ocean");
        saved.InteriorExplorationPaused = true;
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        AddResult(report, "planetary exploration preserves zero-progress pause and research prerequisites", passed,
            "A paused zero-progress save stays paused; active-and-paused is invalid; missing research blocks start and restored operation state.");

        snapshot = InteriorSnapshot();
        snapshot.Sectors.Add(new()
        {
            Id = "OceanZ", Guid = new string('c', 32), AssetPath = "fixture/OceanZ.asset",
            Domain = "HomeSystem", StarSystemId = "Sol", InteriorExploration = snapshot.Sectors[0].InteriorExploration
        });
        snapshot.RuntimeState.Sectors.Add(new() { SectorId = "OceanZ", Occupied = true });
        core = SimulationCore.FromSnapshot(snapshot); state = core.State; rules = core.SnapshotRules!;
        state.SetResource("A", "1");
        rules.TryStartInteriorExploration(state, "OceanZ"); rules.TryStartInteriorExploration(state, "Ocean");
        core.Tick(SimulationMode.Realtime, 1, 1);
        passed = Near(state.GetSectorState("Ocean").InteriorExplorationProgress, ".05") &&
            Near(state.GetSectorState("OceanZ").InteriorExplorationProgress, "0") &&
            Near(state.GetResource("B"), "98.5");
        state.SetResource("A", "4");
        core.Tick(SimulationMode.Realtime, 1, 1);
        passed &= state.GetSectorState("OceanZ").InteriorExplorationProgress > ExpantaNum.Zero;
        AddResult(report, "planetary explorations share supplies in stable sector order", passed,
            "Start order cannot reorder scarce supplies; both active projects resume automatically when stock is replenished.");

        snapshot = InteriorSnapshot();
        var project = snapshot.Sectors[0].InteriorExploration!;
        project.DurationSeconds = "0"; passed = !SnapshotValidation.Validate(snapshot).IsValid;
        project.DurationSeconds = "10"; project.ResourceRatesPerSecond[0].Value = "0";
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        project.ResourceRatesPerSecond[0].Value = "2"; project.RequiredResearchIds = ["Missing"];
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        project.RequiredResearchIds.Clear(); snapshot.RuntimeState.Buildings = [new() { BuildingId = "Ark", Amount = "1" }];
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        snapshot.RuntimeState.Buildings.Clear(); project.Milestones.RemoveAt(0);
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        AddResult(report, "planetary exploration rejects invalid definitions and unfinished construction saves", passed,
            "Zero duration/rates, missing research, wrong milestone count and a locked facility in a save are rejected.");

        snapshot = InteriorSnapshot();
        snapshot.Buildings[0].RequiresInteriorExploration = false;
        snapshot.Sectors[0].InteriorExploration = new();
        roundtrip = SnapshotExporter.LoadJsonText(System.Text.Json.JsonSerializer.Serialize(snapshot));
        passed = roundtrip.Sectors[0].InteriorExploration == null && SnapshotValidation.Validate(roundtrip).IsValid;
        snapshot.Sectors[0].InteriorExploration!.Label = "Partially configured";
        SnapshotValidation.Normalize(snapshot);
        passed &= snapshot.Sectors[0].InteriorExploration != null && !SnapshotValidation.Validate(snapshot).IsValid;
        AddResult(report, "Unity empty inline exploration normalizes without hiding malformed projects", passed,
            "An absent Unity inline object is optional; a partially authored project still fails validation.");
    }

    private static void CheckWorkshopProjectPrerequisites(ValidationReport report)
    {
        var snapshot = BudgetSnapshot();
        snapshot.Buildings.Add(BudgetBuilding("Project", "", "0", "", "0"));
        snapshot.Sectors.Add(new SectorSnapshot
        {
            Id = "Orbit", Guid = new string('d', 32), AssetPath = "fixture/Orbit.asset",
            Domain = "HomeSystem", StarSystemId = "Sol"
        });
        var stage = new WorkshopSnapshot
        {
            Id = "Stage", Guid = new string('e', 32), AssetPath = "fixture/Stage.asset",
            TechLevel = "Animal", RequiredBuildingId = "Project", RequiredSectorId = "Orbit",
            ResourceRequirements = [new AmountSnapshot { ResourceId = "A", Value = "2" }]
        };
        snapshot.Workshops.Add(stage);
        snapshot.RuntimeState.Sectors.Add(new SectorStateSnapshot { SectorId = "Orbit" });
        snapshot.InitialState.Sectors = snapshot.RuntimeState.Sectors;
        snapshot.SaveState.Sectors = snapshot.RuntimeState.Sectors;
        var core = BudgetCore(snapshot, "10");
        var state = core.State;
        state.SetBuilding("Project", ExpantaNum.Zero);
        var passed = !core.SnapshotRules!.TryPurchaseWorkshop(state, "Stage") && state.GetResource("A") > "9.99";
        state.SetBuilding("Project", ExpantaNum.One);
        passed &= !core.SnapshotRules!.TryPurchaseWorkshop(state, "Stage") && state.GetResource("A") > "9.99";
        state.SetSectorState("Orbit", true, true, false, false,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, 1);
        state.SetResource("A", ExpantaNum.One);
        passed &= !core.SnapshotRules!.TryPurchaseWorkshop(state, "Stage") && state.GetResource("A") > ".99";
        state.SetResource("A", "10");
        passed &= core.SnapshotRules!.TryPurchaseWorkshop(state, "Stage") &&
            state.GetResource("A") > "7.99" && state.GetResource("A") < "8.01" &&
            !core.SnapshotRules!.TryPurchaseWorkshop(state, "Stage") && state.GetResource("A") > "7.99";
        AddResult(report, "workshop project gates building, occupation and atomic payment", passed,
            "Missing body, missing occupation and insufficient stock preserve payment; valid purchase charges once.");

        snapshot.RuntimeState.PurchasedWorkshopIds.Add("Stage");
        snapshot.RuntimeState.Buildings[0].Amount = "0";
        snapshot.RuntimeState.Sectors[0].Occupied = true;
        passed = SnapshotValidation.Validate(snapshot).IsValid;
        snapshot.RuntimeState.Sectors[0].Occupied = false;
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        snapshot.RuntimeState.PurchasedWorkshopIds.Clear();
        stage.RequiredBuildingId = "Missing";
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        stage.RequiredBuildingId = "Project";
        stage.RequiredSectorId = "Missing";
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        stage.RequiredSectorId = "Orbit";
        var roundtrip = SnapshotExporter.LoadJsonText(System.Text.Json.JsonSerializer.Serialize(snapshot));
        passed &= roundtrip.Workshops[0].RequiredBuildingId == "Project" &&
            roundtrip.Workshops[0].RequiredSectorId == "Orbit";
        stage.RequiredBuildingId = "";
        stage.RequiredSectorId = "";
        passed &= SnapshotValidation.Validate(snapshot).IsValid;
        AddResult(report, "workshop project snapshot preserves prerequisites and completed stages", passed,
            "Invalid references and purchased stages without occupation fail; demolishing the body preserves a completed stage; empty optional gates remain valid.");
    }

    private static void CheckHomeSystemDevelopmentContract(ValidationReport report)
    {
        var snapshot = BudgetSnapshot();
        var sector = new SectorSnapshot
        {
            Id = "Home", Guid = new string('d', 32), AssetPath = "fixture/Home.asset",
            TechLevel = "", Domain = "HomeSystem", StarSystemId = "Sol"
        };
        snapshot.Sectors.Add(sector);
        snapshot.RuntimeState.Sectors = [new SectorStateSnapshot { SectorId = "Home" }];
        snapshot.SaveState.Sectors = snapshot.RuntimeState.Sectors;
        snapshot.InitialState.Sectors = snapshot.RuntimeState.Sectors;
        BudgetCore(snapshot, "0");
        bool passed = SnapshotValidation.Validate(snapshot).IsValid;
        sector.ResourceRewards.Add(new AmountSnapshot { ResourceId = "A", Value = "1" });
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        sector.ResourceRewards.Clear();
        sector.OccupiedResourceRatesPerSecond.Add(new AmountSnapshot { ResourceId = "A", Value = "1" });
        passed &= !SnapshotValidation.Validate(snapshot).IsValid;
        sector.Domain = "Interstellar";
        sector.StarSystemId = "Neighbor";
        passed &= SnapshotValidation.Validate(snapshot).IsValid;
        AddResult(report, "home-system development forbids resource rewards and passive production", passed,
            "Local development space remains valid; both local resource grant paths are rejected while interstellar production stays valid.");
    }

    private static void CheckSnapshotTimingComparison(ValidationReport report)
    {
        var core = new SimulationCore(new SimulationState());
        var passed = SnapshotParityCommand.Compare(core, new RuntimeStateSnapshot()).Count == 0;
        (string Field, Action<RuntimeStateSnapshot> Change)[] probes =
        [
            ("CalendarElapsedSeconds", state => state.CalendarElapsedSeconds = .01d),
            ("SimulationTickIntervalSeconds", state => state.SimulationTickIntervalSeconds = .11d),
            ("OfflinePendingCalendarSeconds", state => state.OfflinePendingCalendarSeconds = .01d),
            ("OfflinePendingSimulationSeconds", state => state.OfflinePendingSimulationSeconds = .01d)
        ];
        foreach (var probe in probes)
        {
            var expected = new RuntimeStateSnapshot();
            probe.Change(expected);
            var differences = SnapshotParityCommand.Compare(core, expected);
            passed &= differences.Count == 1 && differences[0].Field == probe.Field;
        }
        AddResult(report, "snapshot v5 compares every operation timing input independently", passed,
            "Matching timing has no differences; changing each of the four double timing inputs reports exactly that field.");
    }

    private static void CheckSnapshotOperationTiming(ValidationReport report)
    {
        var snapshot = BudgetSnapshot();
        snapshot.RuntimeState.CalendarElapsedSeconds = 9.95d;
        snapshot.RuntimeState.SimulationTickIntervalSeconds = (double).1f;
        snapshot.RuntimeState.OfflinePendingCalendarSeconds = .075d;
        snapshot.RuntimeState.OfflinePendingSimulationSeconds = .05d;
        var core = BudgetCore(snapshot, "0");
        var restored = core.TickSeconds == (double).1f &&
            Math.Abs(core.State.CalendarElapsedSeconds - 9.95d) < 1e-12d &&
            Math.Abs(core.OfflinePendingCalendarSeconds - .075d) < 1e-12d &&
            Math.Abs(core.OfflinePendingSimulationSeconds - .05d) < 1e-12d;
        core.Tick(SimulationMode.Realtime, .01d, .01d);
        AddResult(report, "snapshot v5 restores operation timing without quantization", restored &&
            core.State.CalendarDays == 1 && Math.Abs(core.State.CalendarElapsedSeconds - .035d) < 1e-12d &&
            Math.Abs(core.State.ElapsedSeconds - .06d) < 1e-12d && core.OfflinePendingSimulationSeconds == 0d,
            "Double calendar remainder, serialized float tick interval and pending clocks resume before an online tick; game save remains v1.");
        snapshot.FormatVersion = 4;
        var oldRejected = !SnapshotValidation.Validate(snapshot).IsValid;
        snapshot.FormatVersion = SnapshotFormat.CurrentVersion;
        snapshot.RuntimeState.CalendarElapsedSeconds = 10d;
        var calendarRejected = !SnapshotValidation.Validate(snapshot).IsValid;
        snapshot.RuntimeState.CalendarElapsedSeconds = 0d;
        snapshot.RuntimeState.OfflinePendingSimulationSeconds = snapshot.RuntimeState.SimulationTickIntervalSeconds;
        var pendingRejected = !SnapshotValidation.Validate(snapshot).IsValid;
        snapshot.RuntimeState.OfflinePendingSimulationSeconds = .05d;
        snapshot.RuntimeState.SimulationTickIntervalSeconds = 0d;
        AddResult(report, "snapshot v5 rejects stale or impossible operation timing",
            oldRejected && calendarRejected && pendingRejected && !SnapshotValidation.Validate(snapshot).IsValid,
            "v4 requires recapture; invalid calendar remainder, tick interval and pending duration are rejected.");
    }

    private static void CheckOfflineOperationCadence(ValidationReport report)
    {
        SimulationCore Colony()
        {
            var snapshot = BudgetSnapshot();
            snapshot.RuntimeState.Food = "500";
            var supply = BudgetBuilding("Support", "", "0", "", "0");
            supply.FoodProductionRate = "10000";
            snapshot.Buildings.Add(supply);
            snapshot.Sectors = [new SectorSnapshot
            {
                Id = "Colony", Guid = new string('d', 32), AssetPath = "fixture/Colony.asset",
                StarSystemId = "Sol", Domain = "HomeSystem", ColonizationFoodPerSecond = "4000",
                ColonizationDurationSeconds = "100000", ColonizationResourceRatesPerSecond =
                    [new AmountSnapshot { ResourceId = "A", Value = "120" }]
            }];
            var states = new List<SectorStateSnapshot> { new() { SectorId = "Colony", Unlocked = true, ColonizationActive = true } };
            snapshot.InitialState.Sectors = states;
            snapshot.SaveState.Sectors = states;
            snapshot.RuntimeState.Sectors = states;
            return BudgetCore(snapshot, "1000000");
        }
        bool EqualColony(SimulationCore left, SimulationCore right) =>
            (left.State.SectorStates["Colony"].Progress - right.State.SectorStates["Colony"].Progress).Abs() < new ExpantaNum(1e-6d) &&
            (left.State.Food - right.State.Food).Abs() < new ExpantaNum(1e-5d) &&
            (left.State.GetResource("A") - right.State.GetResource("A")).Abs() < new ExpantaNum(1e-5d);
        var realtime = Colony();
        realtime.RunRealtime(600);
        foreach (var segments in new[] { 1, 7 })
        {
            var offline = Colony();
            var remaining = 60d;
            for (var i = 0; i < segments; i++)
            {
                var seconds = i == segments - 1 ? remaining : 60d / segments;
                offline.RunOffline(seconds);
                remaining -= seconds;
            }
            offline.Tick(SimulationMode.Realtime, 1e-9d, 1e-9d);
            AddResult(report, $"offline capped Food colony ({segments} windows)", EqualColony(realtime, offline) &&
                offline.State.SectorStates["Colony"].Progress > ExpantaNum.Zero,
                "Capacity 500 funds 0.1s payments of 400 but not a coarse 60s payment; repeated calls retain pending time.");
        }
        var reference = Colony();
        reference.Tick(SimulationMode.Realtime, .1d, .1d);
        reference.Tick(SimulationMode.Realtime, .05d, .05d);
        reference.Tick(SimulationMode.Realtime, .1d, .1d);
        var resumed = Colony();
        resumed.RunOffline(.15d);
        var deferred = Math.Abs(resumed.State.RequestedSeconds - .1d) < 1e-9d;
        resumed.Tick(SimulationMode.Realtime, .1d, .1d);
        AddResult(report, "offline operation remainder flushes before realtime", deferred && EqualColony(reference, resumed) &&
            Math.Abs(resumed.State.RequestedSeconds - .25d) < 1e-9d,
            "The 0.05s operation remainder is retained and settled once before the next realtime tick.");
        var clockState = new SimulationState();
        clockState.SetCampaign(true, "fixture", ExpantaNum.Zero, ExpantaNum.One);
        var clock = new SimulationCore(clockState, tickSeconds: 60d);
        clock.RunOffline(28860d);
        clock.Tick(SimulationMode.Realtime, 1e-9d, 1e-9d);
        AddResult(report, "offline operation rate boundaries conserve both clocks",
            Math.Abs(clock.State.RequestedSeconds - 28860d) < 1e-6d && Math.Abs(clock.State.ElapsedSeconds - 20175d) < 1e-6d,
            "Active-campaign clock fixture crosses 2h/8h; configured 60s effective ticks bound this arithmetic check.");
        var research = ResearchTimingSnapshot();
        research.Research[0].BaseCost = "0.1";
        research.Research[1].BaseCost = "10000";
        var cadence = BudgetCore(research, "1000000");
        cadence.State.SetCampaign(true, "fixture", ExpantaNum.Zero, ExpantaNum.One);
        cadence.RunOffline(.1d);
        AddResult(report, "offline operation research uses realtime completion cadence",
            cadence.State.GetResearchState("First").Completed && cadence.State.GetResearchState("Second").Progress <= ExpantaNum.Zero,
            "An operation fine tick activates the next queued item without advancing it with predecessor remainder.");
    }

    private static void CheckSnapshotComparisonBoundaries(ValidationReport report)
    {
        var state = new SimulationState("0", "10");
        var core = new SimulationCore(state, new DeterministicRules());
        SnapshotParityCommand.AdvanceRealtime(core, 1e-10d);
        AddResult(report, "snapshot comparison advances tiny positive durations",
            state.Tick == 1 && Math.Abs(state.RequestedSeconds - 1e-10d) <= 1e-18d,
            "A valid duration below 1e-9 must execute a tick rather than compare an unchanged state.");
        var exactWindow = new SimulationState("0", "10");
        SnapshotParityCommand.AdvanceRealtime(new SimulationCore(exactWindow, new DeterministicRules()), 10d);
        AddResult(report, "snapshot comparison preserves whole online tick counts", exactWindow.Tick == 100,
            "A ten-second window executes exactly 100 ticks, without a floating-point remainder tick.");
        state.SetCampaign(true, "CampaignFixture", new ExpantaNum(2d), new ExpantaNum(.5d));
        state.SetCampaignProgress(new ExpantaNum(.25d));
        var fields = SnapshotParityCommand.Compare(state, new RuntimeStateSnapshot()).Select(item => item.Field).ToHashSet();
        AddResult(report, "snapshot comparison includes global campaign state",
            new[] { "CampaignActive", "CampaignTargetSectorId", "CampaignProgress", "CampaignCasualties", "CampaignCombatRatio" }.All(fields.Contains),
            "All five exported global campaign fields must report a difference independently of sector state.");
    }

    private static void CheckSnapshotCommandOptions(ValidationReport report)
    {
        var valid = SnapshotParityCommand.Parse(["--snapshot-before", "before.json", "--snapshot-after", "after.json", "--offline-seconds", "7260"]);
        var rejected = 0;
        foreach (var invalid in new[] { "NaN", "Infinity", "-1", "0", "86401", "oops" })
            try { SnapshotParityCommand.Parse(["--snapshot-before", "b", "--snapshot-after", "a", "--online-seconds", invalid]); }
            catch (ArgumentException) { rejected++; }
        foreach (var invalid in new string[][]
        {
            ["--snapshot-before", "b"],
            ["--snapshot-before", "b", "--snapshot-after", "a", "--online-seconds", "1", "--offline-seconds", "1"],
            ["--snapshot-before", "b", "--snapshot-before", "c", "--snapshot-after", "a", "--online-seconds", "1"],
            ["--snapshot-before", "b", "--snapshot-after", "a", "--online-seconds"]
        })
            try { SnapshotParityCommand.Parse(invalid); }
            catch (ArgumentException) { rejected++; }
        AddResult(report, "snapshot comparison CLI validates inputs", valid.Offline && valid.Seconds == 7260d && rejected == 10,
            "Valid fixed duration accepted; malformed, duplicate, conflicting and unbounded options rejected.");
    }

    private static void CheckBaseResourceProduction(ValidationReport report)
    {
        var snapshot = BudgetSnapshot();
        snapshot.Resources[0].BaseProductionRate = "1";
        snapshot.Buildings.Add(BudgetBuilding("Consumer", "A", "2", "B", "1"));
        var core = BudgetCore(snapshot, "0");
        core.Tick(SimulationMode.Realtime, 1d, 1d);
        var efficiency = core.State.BuildingStates["Consumer"].Efficiency;
        AddResult(report, "snapshot base resource production funds consumers", efficiency > new ExpantaNum(0.5d) &&
            efficiency * 2 <= FoodReward(5) + new ExpantaNum(1e-5d) && core.State.GetResource("B") > ExpantaNum.Zero,
            "Passive production is exported separately and participates in satisfaction, settlement and final output.");
    }

    private static void CheckSectorWithoutEra(ValidationReport report)
    {
        var snapshot = BudgetSnapshot();
        snapshot.Sectors = [new SectorSnapshot
        {
            Id = "Home", Guid = new string('d', 32), AssetPath = "fixture/Home.asset",
            TechLevel = "", Domain = "HomeSystem", StarSystemId = "Sol"
        }];
        snapshot.RuntimeState.Sectors = [new SectorStateSnapshot { SectorId = "Home" }];
        snapshot.SaveState.Sectors = snapshot.RuntimeState.Sectors;
        snapshot.InitialState.Sectors = snapshot.RuntimeState.Sectors;
        var accepted = false;
        try { BudgetCore(snapshot, "0"); accepted = true; }
        catch (System.IO.InvalidDataException) { }
        AddResult(report, "snapshot sectors do not invent an era", accepted,
            "Runtime SectorDef has system/domain gates and no TechLevel; its real export must load.");
    }

    private static void CheckSnapshotDefinitionNamespaces(ValidationReport report)
    {
        var snapshot = BudgetSnapshot();
        snapshot.Buildings.Add(BudgetBuilding("A", "", "0", "A", "1"));
        snapshot.Research.Add(new ResearchSnapshot
        {
            Id = "A", Guid = new string('f', 32), AssetPath = "fixture/ResearchA.asset",
            TechLevel = "Animal", ResourceRequirements = [new AmountSnapshot { ResourceId = "A", Value = "1" }]
        });
        snapshot.RuntimeState.Research = [new ResearchStateSnapshot { ResearchId = "A" }];
        snapshot.InitialState.Research = snapshot.RuntimeState.Research;
        snapshot.SaveState.Research = snapshot.RuntimeState.Research;
        var valid = false;
        try { BudgetCore(snapshot, "0"); valid = true; }
        catch (System.IO.InvalidDataException) { }
        snapshot.Research[0].PrerequisiteIds = ["B"];
        var wrongTypeRejected = !SnapshotValidation.Validate(snapshot).IsValid;
        snapshot.Research[0].PrerequisiteIds.Clear();
        snapshot.Research[0].Guid = snapshot.Buildings[0].Guid;
        var sharedGuidRejected = !SnapshotValidation.Validate(snapshot).IsValid;
        snapshot.Research[0].Guid = new string('f', 32);
        var duplicate = BudgetBuilding("A", "", "0", "", "0");
        duplicate.Guid = new string('e', 32);
        snapshot.Buildings.Add(duplicate);
        var duplicateTypeRejected = !SnapshotValidation.Validate(snapshot).IsValid;
        AddResult(report, "snapshot stable IDs are unique within type", valid && wrongTypeRejected &&
            sharedGuidRejected && duplicateTypeRejected,
            "Resource, Building and Research may share an ID; wrong-type references, shared GUIDs and same-type duplicates remain invalid.");
    }

    private static void CheckSnapshotPopulationProgress(ValidationReport report)
    {
        foreach (var progress in new[] { "0.9", "1" })
        {
            var snapshot = BudgetSnapshot();
            snapshot.RuntimeState.Population = "1";
            snapshot.RuntimeState.PopulationChangeProgress = progress;
            var housing = BudgetBuilding("Housing", "", "0", "", "0");
            housing.PopulationCapacityGranted = "20";
            snapshot.Buildings.Add(housing);
            var core = BudgetCore(snapshot, "0");
            var restored = (core.State.PopulationChangeProgress - ParseAmount(progress)).Abs() < new ExpantaNum(1e-6d);
            core.Tick(SimulationMode.Realtime, 0.1d, 0.1d);
            var continued = progress == "1" ? core.State.Population > ExpantaNum.One :
                core.State.PopulationChangeProgress > new ExpantaNum(0.9d);
            AddResult(report, $"snapshot population progress survives derived housing ({progress})",
                restored && continued, "Restore housing before progress; the first tick must retain a fraction or consume one pending step.");
        }
        foreach (var progress in new[] { "-0.1", "1.1", "NaN" })
        {
            var snapshot = BudgetSnapshot();
            snapshot.RuntimeState.PopulationChangeProgress = progress;
            var rejected = false;
            try { BudgetCore(snapshot, "0"); }
            catch (System.IO.InvalidDataException) { rejected = true; }
            AddResult(report, $"snapshot rejects invalid population progress ({progress})", rejected,
                "Population progress must be finite and in [0, 1], matching Runtime restore.");
        }
    }

    private static void CheckSnapshotQueueOrder(ValidationReport report)
    {
        var snapshot = ResearchTimingSnapshot();
        snapshot.Research.Single(item => item.Id == "Second").BaseCost = "10000";
        snapshot.RuntimeState.QueuedResearchIds = ["Second", "First"];
        var core = BudgetCore(snapshot, "0");
        var restored = core.State.ResearchQueue.SequenceEqual(new[] { "Second", "First" });
        core.Tick(SimulationMode.Realtime, 0.1d, 0.1d);
        AddResult(report, "snapshot preserves research queue order", restored &&
            core.State.ActiveResearch == "Second" && core.State.GetResearchState("First").Progress <= ExpantaNum.Zero,
            "Queued commands are ordered state; canonicalization must not alphabetize them.");
    }

    private static void CheckPostPopulationHappiness(ValidationReport report)
    {
        foreach (var mode in new[] { SimulationMode.Realtime, SimulationMode.Offline })
        {
            var snapshot = ResearchTimingSnapshot();
            snapshot.Research[0].BaseCost = "10000";
            var housing = BudgetBuilding("Housing", "", "0", "A", "1");
            housing.PopulationCapacityGranted = "20";
            snapshot.Buildings.Add(housing);
            var core = BudgetCore(snapshot, "0");
            core.Tick(mode, 120d, 120d);
            var population = core.State.Population;
            var expectedHappiness = FoodReward((5 - population * new ExpantaNum(0.8d)) /
                ExpantaNum.Max(ExpantaNum.One, population));
            var expectedProgress = 4 * expectedHappiness * 120;
            AddResult(report, $"research reads post-population happiness ({mode})",
                population > ExpantaNum.Zero &&
                (core.State.GetResearchState("First").Progress - expectedProgress).Abs() < new ExpantaNum(1e-4d) &&
                (core.State.GetResource("A") - FoodReward(5) * 120).Abs() < new ExpantaNum(1e-4d),
                $"Population={population}, progress={core.State.GetResearchState("First").Progress}, expected={expectedProgress}; resource output retains the prepared budget multiplier.");
        }
    }

    private static void CheckPopulationTiming(ValidationReport report)
    {
        foreach (var mode in new[] { SimulationMode.Realtime, SimulationMode.Offline })
        foreach (var food in new[] { "30", "15", "0" })
        {
            var snapshot = BudgetSnapshot();
            snapshot.RuntimeState.Population = "10";
            snapshot.RuntimeState.Food = food;
            var housing = BudgetBuilding("Housing", "", "0", "", "0");
            housing.PopulationCapacityGranted = "20";
            snapshot.Buildings.Add(housing);
            var core = BudgetCore(snapshot, "0");
            core.Tick(mode, 10d, 10d);
            var progress = core.State.PopulationChangeProgress;
            var passed = food == "30" ? progress > new ExpantaNum(0.1d) :
                food == "15" ? progress > ExpantaNum.Zero && progress < new ExpantaNum(0.01d) :
                progress > new ExpantaNum(0.01d) && progress < new ExpantaNum(0.02d);
            AddResult(report, $"food coverage population ({mode}, food={food})", passed,
                $"Population={core.State.Population}, pending={progress}; only unfed seconds contribute to departure.");
        }

        var fullHousing = BudgetSnapshot();
        fullHousing.RuntimeState.Population = "10";
        fullHousing.RuntimeState.Food = "30";
        var fullHouse = BudgetBuilding("Housing", "", "0", "", "0");
        fullHouse.PopulationCapacityGranted = "10";
        fullHousing.Buildings.Add(fullHouse);
        var fullCore = BudgetCore(fullHousing, "0");
        fullCore.Tick(SimulationMode.Offline, 10d, 10d);
        AddResult(report, "exact food coverage at full housing", fullCore.State.PopulationChangeProgress <= ExpantaNum.Zero &&
            (fullCore.State.Population - 10).Abs() < new ExpantaNum(1e-9d),
            "A fully fed interval ending at zero food must not accumulate departure, even when growth is blocked by housing.");

        var state = new SimulationState();
        state.SetPopulation(new ExpantaNum(10d));
        state.SetPopulationCapacity(new ExpantaNum(20d));
        state.AdvancePopulation(10d, new ExpantaNum(0.5d), new ExpantaNum(0.01d), false);
        var growth = state.PopulationChangeProgress;
        state.AdvancePopulation(0d, new ExpantaNum(0.5d), new ExpantaNum(0.01d), true);
        var zeroPreserved = (state.PopulationChangeProgress - growth).Abs() < new ExpantaNum(1e-9d);
        state.AdvancePopulation(1d, new ExpantaNum(0.5d), new ExpantaNum(0.01d), true);
        var departure = state.PopulationChangeProgress;
        state.AdvancePopulation(1d, new ExpantaNum(0.5d), new ExpantaNum(0.01d), false);
        AddResult(report, "population mode changes and zero time", zeroPreserved &&
            departure < new ExpantaNum(0.01d) && state.PopulationChangeProgress < growth / 2,
            "Zero time preserves progress; growth and departure never reuse each other's accumulated fraction.");
    }

    private static void CheckResearchTiming(ValidationReport report)
    {
        foreach (var mode in new[] { SimulationMode.Realtime, SimulationMode.Offline })
        {
            var snapshot = ResearchTimingSnapshot();
            var core = BudgetCore(snapshot, "0");
            core.Tick(mode, 10d, 10d);
            var second = core.State.GetResearchState("Second");
            AddResult(report, $"research remainder ({mode})", core.State.GetResearchState("First").Completed &&
                (mode == SimulationMode.Realtime ? !second.Completed && second.Progress <= ExpantaNum.Zero &&
                    core.State.ActiveResearch == "Second" : second.Completed),
                "Online starts the next item without advancing it; offline consumes the remaining window.");
        }
        foreach (var seconds in new[] { 0.001d, 0.1d })
        {
            var boundary = ResearchTimingSnapshot();
            boundary.Research[0].BaseCost = "200";
            boundary.RuntimeState.Research[0].Progress = "199.99";
            boundary.Research[0].Effects = [new EffectSnapshot { Type = "GlobalResearchMultiplier", Value = "2" }];
            boundary.Research[1].BaseCost = "10000";
            var boundaryCore = BudgetCore(boundary, "0");
            boundaryCore.Tick(SimulationMode.Offline, seconds, seconds);
            var speed = 4 * FoodReward(5);
            var first = boundaryCore.State.GetResearchState("First");
            var second = boundaryCore.State.GetResearchState("Second");
            var expectedSecond = ExpantaNum.Max(ExpantaNum.Zero, speed * seconds - new ExpantaNum(0.01d)) * 2;
            var passed = seconds < 0.01d / speed.ToDouble()
                ? !first.Completed && (first.Progress - (new ExpantaNum(199.99d) + speed * seconds)).Abs() < new ExpantaNum(1e-8d)
                : first.Completed && !second.Completed;
            // ExpantaNum quantizes intermediate scalar arithmetic to 1e-6.
            AddResult(report, $"offline research completion boundary ({seconds}s)", passed &&
                (second.Progress - expectedSecond).Abs() < new ExpantaNum(1e-5d),
                $"Second progress={second.Progress}, expected={expectedSecond}; only true remainder uses the newly completed speed multiplier.");
        }

        var blocked = ResearchTimingSnapshot();
        blocked.Research[0].PrerequisiteIds.Add("Second");
        var blockedCore = BudgetCore(blocked, "0");
        blockedCore.Tick(SimulationMode.Offline, 10d, 10d);
        AddResult(report, "research blocked queue head", blockedCore.State.ActiveResearch is null &&
            blockedCore.State.ResearchStates.Values.All(item => !item.Completed && item.Progress <= ExpantaNum.Zero),
            "An unmet prerequisite at the queue head must block later entries.");
        var unpaid = ResearchTimingSnapshot();
        unpaid.Research[0].ResourceRequirements =
            [new AmountSnapshot { ResourceId = "A", Value = "2" }, new AmountSnapshot { ResourceId = "B", Value = "2" }];
        unpaid.RuntimeState.Research[0].CostPaid = false;
        var paymentCore = BudgetCore(unpaid, "1");
        paymentCore.State.SetResource("A", "2");
        paymentCore.Tick(SimulationMode.Realtime, 0.01d, 0.01d);
        var noPartialPayment = paymentCore.State.ActiveResearch is null &&
            (paymentCore.State.GetResource("A") - 2).Abs() < new ExpantaNum(1e-9d) &&
            (paymentCore.State.GetResource("B") - 1).Abs() < new ExpantaNum(1e-9d);
        paymentCore.State.SetResource("B", "2");
        paymentCore.Tick(SimulationMode.Realtime, 0.01d, 0.01d);
        AddResult(report, "queued research payment is atomic", noPartialPayment &&
            paymentCore.State.ActiveResearch == "First" && paymentCore.State.GetResource("A") <= ExpantaNum.Zero &&
            paymentCore.State.GetResource("B") <= ExpantaNum.Zero && paymentCore.State.GetResearchState("First").Progress > ExpantaNum.Zero,
            "Unaffordable head leaves all inputs intact; later funding pays every cost before progress.");

        var activeUnpaid = ResearchTimingSnapshot();
        activeUnpaid.RuntimeState.ActiveResearchId = "First";
        activeUnpaid.RuntimeState.QueuedResearchIds = ["Second"];
        activeUnpaid.RuntimeState.Research[0].CostPaid = false;
        activeUnpaid.Research[0].ResourceRequirements = [new AmountSnapshot { ResourceId = "A", Value = "2" }];
        var activeCore = BudgetCore(activeUnpaid, "10");
        activeCore.Tick(SimulationMode.Offline, 10d, 10d);
        AddResult(report, "unpaid active research waits", !activeCore.State.GetResearchState("First").CostPaid &&
            activeCore.State.GetResearchState("First").Progress <= ExpantaNum.Zero &&
            (activeCore.State.GetResource("A") - 10).Abs() < new ExpantaNum(1e-9d),
            "Like Runtime Tick, active-but-unpaid input cannot consume resources or advance through the queue-head payment path.");

    }

    private static EconomySnapshot ResearchTimingSnapshot()
    {
        var snapshot = BudgetSnapshot();
        snapshot.Research = new[] { "First", "Second" }.Select((id, index) => new ResearchSnapshot
        {
            Id = id, Guid = (index + 500).ToString("x32"), AssetPath = $"fixture/{id}.asset",
            TechLevel = "Animal", BaseCost = "1"
        }).ToList();
        snapshot.RuntimeState.Research = snapshot.Research.Select(item => new ResearchStateSnapshot
            { ResearchId = item.Id, CostPaid = true }).ToList();
        snapshot.InitialState.Research = snapshot.RuntimeState.Research;
        snapshot.SaveState.Research = snapshot.RuntimeState.Research;
        snapshot.RuntimeState.QueuedResearchIds = ["First", "Second"];
        return snapshot;
    }

    private static void CheckSettledResourceBudgets(ValidationReport report)
    {
        foreach (var demand in new[] { "2", "4" })
        foreach (var inventory in new[] { "0", "1e-6", "0.05", "1e-100" })
        {
            var snapshot = BudgetSnapshot();
            snapshot.Buildings.Add(BudgetBuilding("CycleA", "B", demand, "A", "1"));
            snapshot.Buildings.Add(BudgetBuilding("CycleB", "A", demand, "B", "1"));
            var core = BudgetCore(snapshot, inventory);
            core.Tick(SimulationMode.Realtime, 1d, 1d);
            var a = core.State.BuildingStates["CycleA"].Efficiency;
            var b = core.State.BuildingStates["CycleB"].Efficiency;
            var reward = FoodReward(5d);
            var initial = ParseAmount(inventory);
            // Test the actual input budget, not the inventory clamp hiding an overdraft.
            var budgetFits = a * ParseAmount(demand) <= initial + b * reward + new ExpantaNum(1e-110d) &&
                b * ParseAmount(demand) <= initial + a * reward + new ExpantaNum(1e-110d);
            var retainsFundedWork = initial <= ExpantaNum.Zero ||
                (a > ExpantaNum.Zero && b > ExpantaNum.Zero);
            AddResult(report, $"settled cyclic input budget (demand={demand}, inventory={inventory})", budgetFits && retainsFundedWork,
                $"Final efficiencies A={a}, B={b}; funded cyclic work must fit both inputs without a clamp or losing tiny inventory.");
        }

        var foodSnapshot = BudgetSnapshot();
        foodSnapshot.Buildings.Add(BudgetBuilding("Source", "", "0", "A", "1"));
        var farm = BudgetBuilding("Farm", "A", "100", "", "0");
        farm.FoodProductionRate = "100";
        foodSnapshot.Buildings.Add(farm);
        foodSnapshot.Buildings.Add(BudgetBuilding("Processor", "A", "100", "B", "100"));
        var foodCore = BudgetCore(foodSnapshot, "0");
        foodCore.Tick(SimulationMode.Realtime, 1d, 1d);
        var sourceEfficiency = foodCore.State.BuildingStates["Source"].Efficiency;
        var farmEfficiency = foodCore.State.BuildingStates["Farm"].Efficiency;
        var processorEfficiency = foodCore.State.BuildingStates["Processor"].Efficiency;
        var foodReward = FoodReward(5 + farmEfficiency * 100);
        var funded = (farmEfficiency + processorEfficiency) * 100 <=
            sourceEfficiency * foodReward + new ExpantaNum(1e-9d);
        var outputMatches = (foodCore.State.GetResource("B") -
            processorEfficiency * 100 * foodReward).Abs() < new ExpantaNum(1e-9d);
        AddResult(report, "settled food happiness feedback budget", funded && outputMatches,
            $"Acyclic farm/material feedback settles Farm={farmEfficiency}, Processor={processorEfficiency}, H={foodReward}; all output must be funded.");
    }

    private static void CheckHappinessShortageMonotonicity(ValidationReport report)
    {
        var efficiencies = new List<ExpantaNum>();
        foreach (var food in new[] { "300", "0.292", "0" })
        {
            var snapshot = BudgetSnapshot();
            snapshot.RuntimeState.Population = "10";
            snapshot.RuntimeState.Food = food;
            snapshot.Buildings.Add(BudgetBuilding("Manual", "", "0", "A", "1"));
            var core = BudgetCore(snapshot, "0");
            core.Tick(SimulationMode.Realtime, 0.1d, 0.1d);
            efficiencies.Add(core.State.BuildingStates["Manual"].Efficiency);
        }
        var epsilon = new ExpantaNum(1e-8d);
        var passed = efficiencies[0] < ExpantaNum.One &&
            efficiencies[1] <= efficiencies[0] + epsilon &&
            efficiencies[2] < efficiencies[1];
        AddResult(report, "happiness shortage cannot improve efficiency", passed,
            $"Same net food/population, decreasing stored food: {string.Join(", ", efficiencies)}.");
    }

    private static EconomySnapshot BudgetSnapshot() => new()
    {
        TechLevels = EraStepSchedule.ExpectedTechLevels.ToList(),
        Resources = new[] { "A", "B" }.Select((id, index) => new ResourceSnapshot
        {
            Id = id, Guid = (index + 1).ToString("x32"), AssetPath = $"fixture/{id}.asset", TechLevel = "Animal"
        }).ToList(),
        RuntimeState = new RuntimeStateSnapshot { Food = "300", FoodCapacity = "500" }
    };

    private static BuildingSnapshot BudgetBuilding(string id, string input, string demand,
        string output, string production) => new()
    {
        Id = id, TechLevel = "Animal", AssetPath = $"fixture/{id}.asset",
        ResourceConsumptionRates = string.IsNullOrEmpty(input) ? [] :
            [new AmountSnapshot { ResourceId = input, Value = demand }],
        ResourceGenerationRates = string.IsNullOrEmpty(output) ? [] :
            [new AmountSnapshot { ResourceId = output, Value = production }]
    };

    private static SimulationCore BudgetCore(EconomySnapshot snapshot, string inventory)
    {
        for (var i = 0; i < snapshot.Buildings.Count; i++)
        {
            snapshot.Buildings[i].Guid = (i + 100).ToString("x32");
            snapshot.RuntimeState.Buildings.Add(new BuildingStateSnapshot
                { BuildingId = snapshot.Buildings[i].Id, Amount = "1", Efficiency = "1" });
        }
        foreach (var resource in snapshot.Resources)
            snapshot.RuntimeState.Resources.Add(new AmountSnapshot { ResourceId = resource.Id, Value = inventory });
        snapshot.InitialState.Resources = snapshot.RuntimeState.Resources;
        snapshot.InitialState.Buildings = snapshot.RuntimeState.Buildings;
        snapshot.SaveState.Resources = snapshot.RuntimeState.Resources;
        snapshot.SaveState.Buildings = snapshot.RuntimeState.Buildings;
        return SimulationCore.FromSnapshot(snapshot);
    }

    private static ExpantaNum FoodReward(ExpantaNum foodNet)
    {
        var score = (ExpantaNum.One + foodNet).Log10();
        return ExpantaNum.One + new ExpantaNum(0.5d) * score / (score + ExpantaNum.One);
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
