using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerQaParityTests
{
    private GameObject host;

    [TearDown]
    public void TearDown() => ClearFixture();

    [TestCase(60d)]
    [TestCase(180d)]
    [TestCase(600d)]
    public void FirstHouse_OnlineAndOfflineProduceEquivalentEconomy(double seconds)
    {
        Snapshot online = Run(seconds, false, false);
        Snapshot offline = Run(seconds, true, false);
        Compare(online, offline);
    }

    [Test]
    public void ControlledStarvation_OnlineAndOfflineIncludePopulationDepartures()
    {
        Snapshot online = Run(600d, false, true);
        Snapshot offline = Run(600d, true, true);
            Assert.That(online.Population, Is.LessThan(20d), "Online fixture must actually lose residents.");
            Assert.That(offline.Population, Is.LessThan(20d), "Offline fixture must actually lose residents.");
        Compare(online, offline);
    }

    [Test]
    public void ResearchProductionEffectAppliesAtTheSameTimeOnlineAndOffline()
    {
        Snapshot online = Run(60d, false, false, researchBoundary: true);
        Snapshot offline = Run(60d, true, false, researchBoundary: true);
        Compare(online, offline);
    }

    [Test]
    public void RecoveringFarmRechecksPopulationAfterPreparingTheWindow()
    {
        Snapshot online = Run(60d, false, false, recoveringFarm: true);
        Snapshot offline = Run(60d, true, false, recoveringFarm: true);
        Assert.That(online.Population, Is.GreaterThan(10d), "The fixture must recover and grow inside the window.");
        Compare(online, offline);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OneDayOfflineUsesBoundedEquilibriumBatches(bool controlledIndustry)
    {
        Snapshot result = Run(86400d, true, false, controlledIndustry: controlledIndustry);
        Assert.That(result.Wood, Is.GreaterThan(0d));
        Assert.That(result.Segments, Is.LessThan(20000L),
            "A settled economy must not replay all 345600 effective ticks after a day away.");
        Assert.That(result.CalendarDaysAdvanced, Is.EqualTo(8640), "Discrete ten-second calendar days use elapsed time, without the production discount.");
    }

    private Snapshot Run(double seconds, bool offline, bool starvation,
        bool researchBoundary = false, bool controlledIndustry = false, bool recoveringFarm = false)
    {
        ClearFixture();
        Assert.That(UnityEngine.Object.FindObjectOfType<GameManager>(), Is.Null,
            "Run only in an isolated EditMode test session.");
        Assert.That(UnityEngine.Object.FindObjectOfType<SaveManager>(), Is.Null,
            "This fixture must not coexist with a save writer.");
        ProgressionModifierManager.Rebuild(null);
        host = new GameObject("PlayerQaParity-Managers");
        ResourceManager resources = host.AddComponent<ResourceManager>();
        resources.InitializeForEditor();
        GameManager game = host.AddComponent<GameManager>();
        BuildingManager buildings = host.AddComponent<BuildingManager>();
        ResearchManager research = host.AddComponent<ResearchManager>();
        research.InitializeForEditor();
        host.AddComponent<WorkshopManager>().ResetForLoadForEditor();
        SimulationManager simulation = host.AddComponent<SimulationManager>();
        game.InitializeNewGameForEditor();
        simulation.ManualTick(0d);
        BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
        Assert.That(house, Is.Not.Null);
        // Let the normal starting wood flow pay for the first house; no resource grant.
        for (int tick = 0; buildings.GetBuildFailure(house, ExpantaNum.One) != BuildFailure.None && tick < 3000; tick++)
            simulation.ManualTick(.1d);
        Assert.That(buildings.TryBuild(house, ExpantaNum.One, out BuildFailure failure), Is.True,
            "The first house must be bought through the player transaction: " + failure);
        if (starvation)
        {
            // Explicit controlled state, separate from the naturally purchased first house.
            game.State.RestorePopulationForEditor(new ExpantaNum(20));
            game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(20), ExpantaNum.Zero);
            game.State.RestoreCoreForEditor(0, TechLevel.Animal, ExpantaNum.Zero, 0L);
        }
        ResearchDef changingResearch = null;
        if (researchBoundary)
        {
            changingResearch = DataBase<ResearchDef>.Find("Woodworking");
            foreach (ResearchDef prerequisite in changingResearch.Prerequisites)
                Complete(prerequisite);
            foreach (Pair<ResourceDef, ExpantaNum> cost in changingResearch.ResourceRequirements)
                resources.SetAmount(cost.First, cost.Second * 2d);
            BuildingState lumber = buildings.EnsureBuilding(DataBase<BuildingDef>.Find("Lumberyard"));
            buildings.SetAmountAndRatesForEditor(lumber, ExpantaNum.One);
            Assert.That(research.PayResearchCost(changingResearch), Is.EqualTo(ResearchPaymentResult.Paid));
            Assert.That(research.StartResearch(changingResearch), Is.True);
            ResearchState changingState = research.GetState(changingResearch);
            changingState.SetProgressForEditor(changingState.BaseCost - research.CurrentResearchSpeed * 10d);
        }
        if (controlledIndustry)
        {
            // Controlled scale sample, not evidence of naturally reaching Industrial.
            foreach (ResearchDef definition in DataBase<ResearchDef>.All)
                if (definition.TechLevel <= TechLevel.Industrial) Complete(definition);
            foreach (BuildingDef definition in DataBase<BuildingDef>.All)
                if (definition.TechLevel <= TechLevel.Industrial && !(definition is SectorBuildingDef))
                    buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(definition), ExpantaNum.One);
            foreach (ResourceDef definition in DataBase<ResourceDef>.All)
                resources.SetAmount(definition, new ExpantaNum(1e9d));
            game.State.RestorePopulationForEditor(game.State.Population.PopulationCapacity);
            game.State.RestoreCoreForEditor(0, TechLevel.Industrial, game.State.FoodCapacity, 0L);
        }
        simulation.ManualTick(0d);
        if (recoveringFarm)
        {
            game.State.RestorePopulationForEditor(new ExpantaNum(10));
            game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(20), ExpantaNum.Zero);
            game.State.RestoreCoreForEditor(0, TechLevel.Animal, ExpantaNum.Zero, 0L);
            BuildingState farm = buildings.EnsureBuilding(DataBase<BuildingDef>.Find("Farm"));
            farm.SetEfficiencyForEditor(ExpantaNum.Zero);
            buildings.SetAmountAndRatesForEditor(farm, ExpantaNum.One);
            Assert.That(game.State.FoodNetRate, Is.LessThan(ExpantaNum.Zero),
                "Start from stale zero farm efficiency; preparation must restore its real food flow.");
        }
        Snapshot before = Capture();
        long startSegments = simulation.PreparedSegmentCountForEditor;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        if (offline)
            simulation.AdvanceOffline(seconds);
        else
            for (int tick = 0; tick < (int)Math.Round(seconds * 10d); tick++)
                simulation.ManualTick(.1d);
        Snapshot after = Capture();
        after.Segments = simulation.PreparedSegmentCountForEditor - startSegments;
        after.CalendarDaysAdvanced = after.CalendarDays - before.CalendarDays;
        if (changingResearch != null)
            Assert.That(research.IsResearchCompleted(changingResearch.Id), Is.True,
                "The controlled research must complete inside the measured window.");
        TestContext.WriteLine($"[PlayerQaParity] controlledStarvation={starvation} offline={offline} seconds={seconds:R} before={before} after={after}");
        int activeBuildings = 0;
        foreach (BuildingState state in buildings.States.Values)
            if (state.Amount > ExpantaNum.Zero) activeBuildings++;
        TestContext.WriteLine($"[PlayerQaPerformance] controlledIndustry={controlledIndustry} elapsedMs={watch.Elapsed.TotalMilliseconds:F2} preparedSegments={after.Segments} activeBuildings={activeBuildings}");
        return after;

        void Complete(ResearchDef definition)
        {
            ResearchState state = research.GetState(definition);
            if (state.Status == ResearchStatus.Completed) return;
            foreach (ResearchDef prerequisite in definition.Prerequisites) Complete(prerequisite);
            var paid = new Dictionary<ResourceDef, ExpantaNum>();
            foreach (Pair<ResourceDef, ExpantaNum> cost in definition.ResourceRequirements)
                paid[cost.First] = cost.Second;
            state.RestoreForEditor(state.BaseCost, true, true, paid);
            ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values));
        }

        Snapshot Capture() => new Snapshot
        {
            Population = game.State.Population.Population.ToDouble(),
            PopulationProgress = game.State.Population.PopulationChangeProgress.ToDouble(),
            Food = game.State.FoodAmount.ToDouble(),
            Wood = resources.GetAmount(DataBase<ResourceDef>.Find("WoodLog")).ToDouble(),
            ResearchPower = research.ResearchPower.ToDouble(),
            ResearchSpeed = research.CurrentResearchSpeed.ToDouble(),
            QueueCount = research.ResearchQueue.Count,
            CalendarDays = game.State.CalendarDays,
            HouseCount = buildings.GetState(house).Amount.ToDouble()
        };
    }

    private static void Compare(Snapshot online, Snapshot offline)
    {
            // Residents, owned buildings and queue entries are discrete counts.
            Assert.That(offline.Population, Is.EqualTo(online.Population), "Population parity");
            Assert.That(offline.HouseCount, Is.EqualTo(online.HouseCount), "House count parity");
            Assert.That(offline.QueueCount, Is.EqualTo(online.QueueCount), "Queue count parity");
            Assert.That(offline.PopulationProgress, Is.EqualTo(online.PopulationProgress).Within(.002d), "Population progress parity");
            AssertNear(offline.Food, online.Food, "Food parity");
            AssertNear(offline.Wood, online.Wood, "Wood parity");
            AssertNear(offline.ResearchPower, online.ResearchPower, "Research power parity");
            AssertNear(offline.ResearchSpeed, online.ResearchSpeed, "Research speed parity");
    }

    private static void AssertNear(double actual, double expected, string message) =>
        Assert.That(actual, Is.EqualTo(expected).Within(Math.Max(.02d, Math.Abs(expected) * .001d)),
            message + " (bounded allowance for repeated ExpantaNum quantization)");

    private void ClearFixture()
    {
        if (host != null) UnityEngine.Object.DestroyImmediate(host);
        host = null;
        ProgressionModifierManager.Rebuild(null);
    }

    private sealed class Snapshot
    {
        public double Population, PopulationProgress, Food, Wood, ResearchPower, ResearchSpeed, HouseCount;
        public int QueueCount;
        public long Segments, CalendarDays, CalendarDaysAdvanced;
        public override string ToString() =>
            $"population={Population:R}, populationProgress={PopulationProgress:R}, food={Food:R}, wood={Wood:R}, researchPower={ResearchPower:R}, researchSpeed={ResearchSpeed:R}, queueCount={QueueCount}, houseCount={HouseCount:R}";
    }
}


