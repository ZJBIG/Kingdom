using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class RelicSimulationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Simulation_InvestigationAdvancesAndPaysFoodAndEveryMaterial(bool offline)
    {
        Snapshot sealedResult = Run(offline, true, false);
        Snapshot working = Run(offline, false, false);
        RelicWorkDefinition work = DataBase<RelicDefinition>.Find(RelicState.RelicId).Investigation;
        Near(working.Progress, new ExpantaNum(60d) / work.DurationSeconds);
        Near(sealedResult.FoodChange - working.FoodChange, work.FoodConsumptionRate * 60d);
        foreach (var cost in work.ContinuousCosts)
            Near(sealedResult.MaterialChanges[cost.First.Id] - working.MaterialChanges[cost.First.Id],
                cost.Second * 60d);
        Assert.That(working.Suspended, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Simulation_ZeroMaterialSupplyPausesWithoutRelicPayment(bool offline)
    {
        Snapshot sealedResult = Run(offline, true, true);
        Snapshot working = Run(offline, false, true);
        Assert.That(working.Suspended, Is.True);
        Assert.That(working.PauseReason, Is.EqualTo(RelicPauseReason.InsufficientSupply));
        Near(working.Progress, ExpantaNum.Zero);
        Near(working.FoodChange, sealedResult.FoodChange);
        foreach (var entry in working.MaterialChanges)
            Near(entry.Value, sealedResult.MaterialChanges[entry.Key]);
    }

    [Test]
    public void FullRateOfflineAndRealtimeCadence_ProduceMatchingRelicProgressAndBalances()
    {
        Snapshot realtime = Run(false, false, false, true);
        Snapshot offline = Run(true, false, false);
        Near(offline.Progress, realtime.Progress, .01d);
        Near(offline.FoodChange, realtime.FoodChange);
        foreach (var entry in realtime.MaterialChanges)
            Near(offline.MaterialChanges[entry.Key], entry.Value);
    }

    [Test]
    public void AuthoredRelic_PrerequisitesAndEveryCostHaveExistingDefinitionsAndProductionSources()
    {
        RelicDefinition definition = DataBase<RelicDefinition>.Find(RelicState.RelicId);
        Assert.That(definition.Sector, Is.Not.Null);
        Assert.That(definition.Sector.IsHomeSystem, Is.False);
        Assert.That(DataBase<SectorDefinition>.Find(definition.Sector.Id), Is.SameAs(definition.Sector));
        Assert.That(definition.RequiredResearch.Count, Is.GreaterThan(0));
        Assert.That(definition.RequiredBuildings.Count, Is.GreaterThan(0));
        foreach (var research in definition.RequiredResearch)
            Assert.That(DataBase<Research>.Find(research.Id), Is.SameAs(research));
        foreach (var building in definition.RequiredBuildings)
            Assert.That(DataBase<Building>.Find(building.Id), Is.SameAs(building));
        Assert.That(definition.CampaignSupplyMultiplier, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(definition.CampaignSupplyMultiplier, Is.LessThan(ExpantaNum.One));
        var works = new[] { definition.Investigation, definition.Repair,
            definition.ReverseEngineering, definition.Commission };
        foreach (var work in works)
        {
            Assert.That(work.DurationSeconds.IsFinite, Is.True);
            Assert.That(work.DurationSeconds, Is.GreaterThan(ExpantaNum.Zero));
            AssertCosts(work.StartupCosts);
            AssertCosts(work.ContinuousCosts);
        }
        AssertCosts(definition.SupportCraftCosts);
    }

    private static void AssertCosts(IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        Assert.That(costs.Count, Is.GreaterThan(0));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cost in costs)
        {
            Assert.That(cost.First, Is.Not.Null);
            Assert.That(DataBase<Resource>.Find(cost.First.Id), Is.SameAs(cost.First));
            Assert.That(ids.Add(cost.First.Id), Is.True, "A transaction must not duplicate a stable resource ID.");
            Assert.That(cost.Second.IsFinite, Is.True);
            Assert.That(cost.Second, Is.GreaterThan(ExpantaNum.Zero));
            bool produced = false;
            foreach (var building in DataBase<Building>.All)
                foreach (var output in building.ResourceGenerationRates)
                    if (output.First == cost.First && output.Second > ExpantaNum.Zero) produced = true;
            foreach (var building in DataBase<SectorBuilding>.All)
                foreach (var output in building.ResourceGenerationRates)
                    if (output.First == cost.First && output.Second > ExpantaNum.Zero) produced = true;
            foreach (var sector in DataBase<SectorDefinition>.All)
                foreach (var output in sector.OccupiedResourceRatesPerSecond)
                    if (output.First == cost.First && output.Second > ExpantaNum.Zero) produced = true;
            Assert.That(produced, Is.True, $"Relic sink {cost.First.Id} requires an authored production source.");
        }
    }

    private static Snapshot Run(bool offline, bool seal, bool missingMaterial, bool smallTicks = false)
    {
        ProgressionModifierManager.Rebuild(null);
        var root = new GameObject("RelicSimulationTests");
        var temporaryBuildings = new List<Building>();
        try
        {
            GameManager game = root.AddComponent<GameManager>();
            ResourceManager resources = root.AddComponent<ResourceManager>();
            BuildingManager buildings = root.AddComponent<BuildingManager>();
            ResearchManager research = root.AddComponent<ResearchManager>();
            SimulationManager simulation = root.AddComponent<SimulationManager>();
            game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum(1e6d), 0L);
            game.State.AdjustFoodCapacityForEditor(new ExpantaNum(1e9d));
            game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
            game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
            research.InitializeForEditor();
            RelicDefinition definition = game.Relic.Definition;
            foreach (var prerequisite in definition.RequiredResearch)
            {
                var state = research.GetState(prerequisite);
                var paid = new Dictionary<Resource, ExpantaNum>();
                foreach (var cost in prerequisite.ResourceRequirements)
                    if (cost.First != null && cost.Second > ExpantaNum.Zero) paid[cost.First] = cost.Second;
                state.RestoreForEditor(state.BaseCost, true, true, paid);
            }
            foreach (var resource in DataBase<Resource>.All)
                resources.SetAmount(resource, new ExpantaNum(1e6d));
            foreach (var required in definition.RequiredBuildings)
            {
                // Keep stable prerequisite identity while isolating unrelated authored industry flows.
                Building clone = UnityEngine.Object.Instantiate(required);
                temporaryBuildings.Add(clone);
                clone.ConfigureEconomyForEditor(new ExpantaNum(1.15d),
                    ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                    ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                    new ExpantaNum(1e6d), ExpantaNum.Zero, new ExpantaNum(1e6d),
                    ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                    new List<Pair<Resource, ExpantaNum>>(), new List<Pair<Resource, ExpantaNum>>(),
                    new List<Pair<Resource, ExpantaNum>>());
                buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(clone), ExpantaNum.One);
            }
            game.Sectors.InitializeDefinitions();
            game.Sectors.GetState(definition.Sector).SetUnlockedForEditor(true);
            game.Sectors.GetState(definition.Sector).SetOccupiedForEditor(true);
            game.UltraProject.State.RestoreForEditor(new UltraProjectStateSaveData
            {
                ProjectId = UltraProjectState.ProjectId,
                SaveVersion = UltraProjectState.CurrentSaveVersion,
                Doctrine = UltraProjectDoctrine.Stable,
                Status = UltraProjectStatus.Ready,
                CurrentStage = UltraProjectStage.Stabilization,
                StageProgress = "0",
                CompletedStages = new List<UltraProjectStage> { UltraProjectStage.Prototype },
                StateVersion = 1
            });
            Assert.That(game.Relic.TryInvestigate(out RelicOperationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(RelicOperationFailure.None));
            if (seal) Assert.That(game.Relic.TrySuspend(out _), Is.True);
            RelicWorkDefinition work = definition.Investigation;
            if (missingMaterial)
            {
                Resource missing = null;
                foreach (var cost in work.ContinuousCosts)
                {
                    bool producedByOccupiedSector = false;
                    foreach (var rate in definition.Sector.OccupiedResourceRatesPerSecond)
                        if (rate.First == cost.First && rate.Second > ExpantaNum.Zero) producedByOccupiedSector = true;
                    if (!producedByOccupiedSector) { missing = cost.First; break; }
                }
                Assert.That(missing, Is.Not.Null, "Zero-supply case must not be replenished by its occupied sector.");
                resources.SetAmount(missing, ExpantaNum.Zero);
            }
            ExpantaNum foodBefore = game.State.FoodAmount;
            var before = new Dictionary<Resource, ExpantaNum>();
            foreach (var cost in work.ContinuousCosts) before.Add(cost.First, resources.GetAmount(cost.First));
            if (offline)
                Assert.That(simulation.AdvanceOffline(60d), Is.EqualTo(60d).Within(1e-8d));
            else if (smallTicks)
                for (int i = 0; i < 600; i++) simulation.ManualTick(.1d);
            else
                simulation.ManualTick(60d);
            var result = new Snapshot
            {
                Progress = game.Relic.State.Progress,
                FoodChange = game.State.FoodAmount - foodBefore,
                Suspended = game.Relic.State.Suspended,
                PauseReason = game.Relic.State.PauseReason
            };
            foreach (var entry in before)
                result.MaterialChanges.Add(entry.Key.Id, resources.GetAmount(entry.Key) - entry.Value);
            return result;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var building in temporaryBuildings) UnityEngine.Object.DestroyImmediate(building);
            ProgressionModifierManager.Rebuild(null);
        }
    }

    private sealed class Snapshot
    {
        public ExpantaNum Progress, FoodChange;
        public bool Suspended;
        public RelicPauseReason PauseReason;
        public readonly Dictionary<string, ExpantaNum> MaterialChanges = new();
    }

    private static void Near(ExpantaNum actual, ExpantaNum expected, double relativeTolerance = 1e-8d)
    {
        ExpantaNum tolerance = ExpantaNum.Max(new ExpantaNum(1e-5d), expected.Abs() * relativeTolerance);
        Assert.That((actual - expected).Abs(), Is.LessThanOrEqualTo(tolerance));
    }
}
