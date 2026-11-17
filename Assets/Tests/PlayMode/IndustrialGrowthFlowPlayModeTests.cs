using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class IndustrialGrowthFlowPlayModeTests
{
    private string saveRoot;
    private bool loadedSampleScene;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;
    private UIButtonSoundManager initialSound;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        initialSound = Object.FindObjectOfType<UIButtonSoundManager>(true);
        loadedSampleScene = false;
        saveRoot = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            "Temp", "KingdomIndustrialGrowthFlow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (loadedSampleScene)
        {
            Scene scene = SceneManager.GetSceneByName("SampleScene");
            if (scene.IsValid() && scene.isLoaded)
                foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        }
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        UIButtonSoundManager sound = Object.FindObjectOfType<UIButtonSoundManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        if (sound != null && sound != initialSound) Object.DestroyImmediate(sound.gameObject);
        ProgressionModifierManager.Rebuild(null);
        SaveManager.ClearSaveRootOverrideForTests();
        if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot))
        {
            Directory.Delete(saveRoot, true);
            Assert.That(Directory.Exists(saveRoot), Is.False, "Remove the isolated behavior-test save directory.");
        }
        saveRoot = null;
    }

    [UnityTest]
    public IEnumerator PaidOilExpansion_RecoversChemicalSupplyAndFundsTheFirstRefinery()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        loadedSampleScene = true;
        for (int frame = 0; frame < 180 && Object.FindObjectOfType<GameBootstrap>()?.Completed != true; frame++)
            yield return null;
        Assert.That(Object.FindObjectOfType<GameBootstrap>()?.Completed, Is.True);
        Assert.That(SaveManager.Instance.LastLoadCreatedNewGame, Is.True);

        GameManager game = GameManager.Instance;
        ResourceManager resources = ResourceManager.Instance;
        BuildingManager buildings = BuildingManager.Instance;
        ResearchManager research = ResearchManager.Instance;
        WorkshopManager workshops = WorkshopManager.Instance;
        SimulationManager simulation = SimulationManager.Instance;
        simulation.SetRunning(false);
        Assert.That(buildings.States.Values.All(state => state.Amount <= ExpantaNum.Zero), Is.True,
            "The isolated scene must not contain pre-owned production facilities.");

        BuildingDef irrigation = DataBase<BuildingDef>.Find("IrrigationWorks");
        BuildingDef caravan = DataBase<BuildingDef>.Find("Caravanserai");
        BuildingDef power = DataBase<BuildingDef>.Find("CentralPowerStation");
        BuildingDef well = DataBase<BuildingDef>.Find("OilDerrick");
        BuildingDef plant = DataBase<BuildingDef>.Find("ChemicalPlant");
        BuildingDef refinery = DataBase<BuildingDef>.Find("OilRefinery");
        ResourceDef oil = DataBase<ResourceDef>.Find("CrudeOil");
        ResourceDef chemical = DataBase<ResourceDef>.Find("Chemical");
        WorkshopUpgradeDef distillation = DataBase<WorkshopUpgradeDef>.Find("ContinuousDistillation");
        var construction = new (BuildingDef Definition, int Count)[]
        {
            (irrigation, 2), (caravan, 4), (power, 1), (well, 4), (plant, 2), (refinery, 1)
        };

        // Controlled Industrial behavior fixture: prerequisite history and finite construction funds
        // are prepared once; this is not evidence of natural progression to Industrial.
        ExpantaNum territory = ExpantaNum.One;
        var completedResearch = new HashSet<string>(StringComparer.Ordinal);
        foreach (var investment in construction)
        {
            territory += investment.Definition.SpaceCost * investment.Count;
            foreach (ResearchDef prerequisite in investment.Definition.RequiredResearch)
                RestoreResearchPrerequisites(research, prerequisite, completedResearch);
        }
        ProgressionModifierManager.Rebuild(research.States.Values.ToArray(), workshops.States.Values.ToArray());
        game.State.ResetDerivedEconomyForEditor(territory);
        game.State.RestoreCoreForEditor(0, TechLevel.Industrial, GameState.BaseFoodCapacity, 0L);
        game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(30), ExpantaNum.Zero);
        game.State.RestorePopulationForEditor(new ExpantaNum(30));

        const int maximumTicks = 1200;
        const double tickSeconds = 0.1d;
        var seed = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (var investment in construction)
        {
            foreach (var cost in ConstructionCosts(investment.Definition, ExpantaNum.Zero, investment.Count))
                // Separate paid purchases round differently from the combined geometric budget.
                Add(seed, cost.Key, cost.Value * new ExpantaNum(1.001d));
            foreach (Pair<ResourceDef, ExpantaNum> input in investment.Definition.ResourceConsumptionRates)
                Add(seed, input.First, input.Second * investment.Count * (maximumTicks * tickSeconds));
        }
        // The well must start from no oil; all refinery construction/upkeep chemicals must be produced.
        seed.Remove(oil);
        seed.Remove(chemical);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            resources.SetAmount(resource, seed.TryGetValue(resource, out ExpantaNum amount) ? amount : ExpantaNum.Zero);
        Assert.That(workshops.IsPurchased(distillation), Is.False);
        TestContext.WriteLine("CONTROLLED_INDUSTRIAL_FLOW seed=authored-geometric-construction-costs+0.1%-rounding-buffer+120s-non-oil-upkeep oil=0 chemical=0 grantsAfterStart=0");

        int ticks = 0;
        void Advance(int count)
        {
            for (int tick = 0; tick < count; tick++)
            {
                simulation.ManualTick(tickSeconds);
                ticks++;
                foreach (ResourceState state in resources.States.Values)
                    Assert.That(state.Amount.IsFinite && state.Amount >= ExpantaNum.Zero, Is.True,
                        state.Definition.Id + " inventory must remain finite and nonnegative.");
            }
        }
        void AssertSupportingSupply()
        {
            Assert.That(buildings.LaborOperatingRatio, Is.GreaterThanOrEqualTo(new ExpantaNum(0.999d)));
            Assert.That(game.State.PowerSatisfaction, Is.GreaterThanOrEqualTo(new ExpantaNum(0.999d)));
            Assert.That(game.State.LogisticsSatisfaction, Is.GreaterThanOrEqualTo(new ExpantaNum(0.999d)));
            Assert.That(game.State.FoodNetRate, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
            Assert.That(game.State.FoodAmount, Is.GreaterThan(ExpantaNum.Zero));
            foreach (string id in new[] { "Coal", "Coke" })
                Assert.That(resources.GetAmount(DataBase<ResourceDef>.Find(id)), Is.GreaterThan(ExpantaNum.Zero),
                    id + " must remain funded by the finite initial upkeep buffer.");
        }
        void Record(string stage)
        {
            ResourceState oilState = resources.GetState(oil);
            ResourceState chemicalState = resources.GetState(chemical);
            TestContext.WriteLine($"INDUSTRIAL_FLOW stage={stage} gameSeconds={ticks * tickSeconds:F1} " +
                $"wells={buildings.GetState(well).Amount} plants={buildings.GetState(plant).Amount} " +
                $"oil={oilState.Amount} oilNet={resources.GetNetRate(oilState)} oilSatisfaction={resources.GetTickSatisfactionForEditor(oil)} " +
                $"chemical={chemicalState.Amount} chemicalProduction={chemicalState.ProductionRate} " +
                $"plantEfficiency={buildings.GetState(plant).Efficiency} power={game.State.PowerSatisfaction} logistics={game.State.LogisticsSatisfaction}");
        }

        BuildPaid(buildings, resources, irrigation, 2);
        BuildPaid(buildings, resources, caravan, 4);
        BuildPaid(buildings, resources, power, 1);
        BuildPaid(buildings, resources, well, 1);
        Advance(10);
        AssertSupportingSupply();
        Assert.That(resources.GetAmount(oil), Is.GreaterThan(ExpantaNum.Zero),
            "A paid first well must produce oil before chemical products exist.");
        Assert.That(resources.GetAmount(chemical), Is.LessThanOrEqualTo(ExpantaNum.Zero));
        Record("well-started");

        var beforeRejectedBuild = resources.States.ToDictionary(entry => entry.Key, entry => entry.Value.Amount);
        ExpantaNum territoryBeforeRejectedBuild = game.State.TerritoryUsed;
        Assert.That(buildings.TryBuild(refinery, ExpantaNum.One, out BuildFailure rejected), Is.False);
        Assert.That(rejected, Is.EqualTo(BuildFailure.ResourceInsufficient),
            "The first refinery must wait for produced chemicals; the optional distillation workshop must not block it.");
        foreach (var before in beforeRejectedBuild)
            Assert.That((resources.GetAmount(before.Key) - before.Value).Abs(), Is.LessThanOrEqualTo(new ExpantaNum(0.000001d)),
                "A rejected refinery must not partly spend " + before.Key.Id + ".");
        Assert.That((game.State.TerritoryUsed - territoryBeforeRejectedBuild).Abs(), Is.LessThanOrEqualTo(new ExpantaNum(0.000001d)));
        Assert.That(buildings.GetState(refinery).Amount, Is.EqualTo(ExpantaNum.Zero), "Owned building count is discrete.");

        ExpantaNum openingOil = resources.GetAmount(oil);
        BuildPaid(buildings, resources, plant, 2);
        Advance(100);
        AssertSupportingSupply();
        Assert.That(resources.GetAmount(oil), Is.LessThan(openingOil), "The newly paid consumers must deplete the small oil stock.");
        Assert.That(resources.GetTickSatisfactionForEditor(oil).ToDouble(), Is.InRange(0.01d, 0.99d));
        Assert.That(buildings.GetState(plant).Efficiency.ToDouble(), Is.InRange(0.01d, 0.99d),
            "Insufficient oil must throttle actual chemical production while funded inputs keep it running.");
        Assert.That(resources.GetAmount(chemical), Is.GreaterThan(ExpantaNum.Zero));
        ExpantaNum shortageOil = resources.GetAmount(oil);
        ExpantaNum shortageChemical = resources.GetAmount(chemical);
        ExpantaNum shortageProduction = resources.GetState(chemical).ProductionRate;
        Record("oil-shortage");

        BuildPaid(buildings, resources, well, 3);
        Advance(100);
        AssertSupportingSupply();
        Assert.That(buildings.GetState(plant).Efficiency, Is.GreaterThanOrEqualTo(new ExpantaNum(0.999d)));
        Assert.That(resources.GetTickSatisfactionForEditor(oil), Is.GreaterThanOrEqualTo(new ExpantaNum(0.999d)));
        Assert.That(resources.GetAmount(oil), Is.GreaterThan(shortageOil));
        Assert.That(resources.GetAmount(chemical), Is.GreaterThan(shortageChemical));
        Assert.That(resources.GetState(chemical).ProductionRate, Is.GreaterThan(shortageProduction));
        Assert.That(resources.GetNetRate(resources.GetState(oil)), Is.GreaterThan(ExpantaNum.Zero));
        Record("upstream-expanded");

        while (buildings.GetBuildFailure(refinery, ExpantaNum.One) == BuildFailure.ResourceInsufficient && ticks < maximumTicks - 10)
            Advance(1);
        Record("refinery-budget");
        foreach (var cost in ConstructionCosts(refinery, ExpantaNum.Zero, 1))
            TestContext.WriteLine($"REFINERY_BUDGET resource={cost.Key.Id} required={cost.Value} amount={resources.GetAmount(cost.Key)}");
        Assert.That(buildings.GetBuildFailure(refinery, ExpantaNum.One), Is.EqualTo(BuildFailure.None),
            "The fixed oil/chemical flow must fund the first refinery within the finite upkeep buffer.");
        ExpantaNum chemicalBeforePurchase = resources.GetAmount(chemical);
        BuildPaid(buildings, resources, refinery, 1);
        Assert.That(resources.GetAmount(chemical), Is.LessThan(chemicalBeforePurchase),
            "The next expansion must spend chemicals actually produced after the fixture started.");
        Assert.That(workshops.IsPurchased(distillation), Is.False,
            "Continuous distillation improves output; the first refinery must operate before that workshop purchase.");
        Advance(10);
        AssertSupportingSupply();
        Assert.That(buildings.GetState(refinery).Efficiency, Is.GreaterThanOrEqualTo(new ExpantaNum(0.999d)));
        foreach (string id in new[] { "RefinedFuel", "Lubricant", "Rubber" })
            Assert.That(resources.GetAmount(DataBase<ResourceDef>.Find(id)), Is.GreaterThan(ExpantaNum.Zero),
                "The paid refinery must produce " + id + " from the recovered supply chain.");
        Assert.That(resources.GetNetRate(resources.GetState(oil)), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(resources.GetNetRate(resources.GetState(chemical)), Is.GreaterThan(ExpantaNum.Zero));
        Record("refinery-paid-and-producing");
        yield return null;
    }

    private static void RestoreResearchPrerequisites(ResearchManager research, ResearchDef definition, HashSet<string> completed)
    {
        if (!completed.Add(definition.Id)) return;
        foreach (ResearchDef prerequisite in definition.Prerequisites)
            RestoreResearchPrerequisites(research, prerequisite, completed);
        var paid = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (Pair<ResourceDef, ExpantaNum> cost in definition.ResourceRequirements)
            Add(paid, cost.First, cost.Second);
        ResearchState state = research.GetState(definition);
        state.RestoreForEditor(state.BaseCost, true, true, paid);
    }

    private static Dictionary<ResourceDef, ExpantaNum> ConstructionCosts(BuildingDef definition, ExpantaNum owned, int count)
    {
        var costs = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (Pair<ResourceDef, ExpantaNum> cost in definition.ResourceRequirements)
            Add(costs, cost.First, cost.Second.GeometricSeriesCost(definition.CostGrowth, owned, new ExpantaNum(count)) *
                BuildingManager.GetConstructionCostMultiplierForEditor(definition));
        return costs;
    }

    private static void BuildPaid(BuildingManager buildings, ResourceManager resources, BuildingDef definition, int count)
    {
        ExpantaNum beforeCount = buildings.GetState(definition).Amount;
        Dictionary<ResourceDef, ExpantaNum> costs = ConstructionCosts(definition, beforeCount, count);
        var before = costs.Keys.ToDictionary(resource => resource, resources.GetAmount);
        Assert.That(buildings.TryBuild(definition, new ExpantaNum(count), out BuildFailure failure), Is.True,
            definition.Id + " must be a real paid construction: " + failure);
        Assert.That(buildings.GetState(definition).Amount, Is.EqualTo(beforeCount + count), "Owned building count is discrete.");
        foreach (var cost in costs)
            Assert.That((before[cost.Key] - resources.GetAmount(cost.Key) - cost.Value).Abs(),
                Is.LessThanOrEqualTo(new ExpantaNum(0.0001d)),
                definition.Id + " must charge its current geometric cost in " + cost.Key.Id + ".");
    }

    private static void Add(Dictionary<ResourceDef, ExpantaNum> totals, ResourceDef resource, ExpantaNum amount)
    {
        totals[resource] = totals.TryGetValue(resource, out ExpantaNum previous) ? previous + amount : amount;
    }
}
