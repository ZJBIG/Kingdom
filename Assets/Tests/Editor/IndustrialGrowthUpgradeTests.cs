using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class IndustrialGrowthUpgradeTests
{
    private GameObject host;
    private GameManager game;
    private ResourceManager resources;
    private BuildingManager buildings;
    private readonly HashSet<ResearchDef> researchDefinitions = new();
    private readonly HashSet<WorkshopUpgradeDef> workshopDefinitions = new();
    private readonly List<ResearchState> researchStates = new();
    private readonly List<WorkshopUpgradeState> workshopStates = new();

    [SetUp]
    public void SetUp()
    {
        ProgressionModifierManager.Rebuild(null);
        host = new GameObject("Industrial first-unlock upgrade tests");
        game = host.AddComponent<GameManager>();
        resources = host.AddComponent<ResourceManager>();
        if (resources.States.Count == 0)
            resources.InitializeForEditor();
        buildings = host.AddComponent<BuildingManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        ProgressionModifierManager.Rebuild(null);
    }

    [TestCase("University", "DeepSpaceObservatory", true)]
    [TestCase("DeepSpaceObservatory", "QuantumComputingArray", true)]
    [TestCase("QuantumComputingArray", "InterstellarTheoryNexus", true)]
    [TestCase("InterstellarTheoryNexus", "UltraComputingNexus", false)]
    public void FirstUnlockedResearchFacilityImprovesEffectivePower(
        string sourceId, string targetId, bool isUpgrade)
    {
        BuildingDef source = DataBase<BuildingDef>.Find(sourceId);
        BuildingDef target = DataBase<BuildingDef>.Find(targetId);
        if (isUpgrade)
            Assert.That(source.UpgradeTo, Is.SameAs(target));
        PrepareFirstUnlock(target, source);

        ExpantaNum before = CalculateSingleBuildingResearch(source);
        ExpantaNum after = CalculateSingleBuildingResearch(target);
        TestContext.WriteLine($"FIRST_UNLOCK_RESEARCH {sourceId}->{targetId}: " +
            $"power={before}->{after}, productivity={source.ProductivityConsumption}->{target.ProductivityConsumption}");
        Assert.That(after.ToDouble(), Is.GreaterThan(before.ToDouble() * 1.5d),
            "The first unlocked facility must visibly improve research with only its required modifiers.");
        Assert.That((after / target.ProductivityConsumption).ToDouble(),
            Is.GreaterThanOrEqualTo((before / source.ProductivityConsumption).ToDouble() * (1d - 1e-8d)),
            "Effective research per committed productivity must not decrease.");
    }

    [TestCase("OrbitalMachiningComplex")]
    [TestCase("OrbitalWireWorks")]
    [TestCase("OrbitalBuildingMaterialsWorks")]
    public void FirstUnlockedComprehensiveFactoryImprovesEachSpecialistOutput(string sourceId)
    {
        BuildingDef source = DataBase<BuildingDef>.Find(sourceId);
        BuildingDef target = DataBase<BuildingDef>.Find("OrbitalResourceExtractionArray");
        Assert.That(source.UpgradeTo, Is.SameAs(target));
        PrepareFirstUnlock(target, source);

        SetAmount(source, ExpantaNum.One);
        var before = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (Pair<ResourceDef, ExpantaNum> output in source.ResourceGenerationRates)
            before.Add(output.First, resources.GetState(output.First).ProductionRate);
        SetAmount(source, ExpantaNum.Zero);
        SetAmount(target, ExpantaNum.One);

        foreach (KeyValuePair<ResourceDef, ExpantaNum> output in before)
        {
            ExpantaNum after = resources.GetState(output.Key).ProductionRate;
            TestContext.WriteLine($"FIRST_UNLOCK_FACTORY {sourceId}->{target.Id} {output.Key.Id}: " +
                $"rate={output.Value}->{after}");
            Assert.That(after.ToDouble(), Is.GreaterThan(output.Value.ToDouble() * 1.5d),
                $"{sourceId}: {output.Key.Id} must gain visible throughput before optional late upgrades.");
        }
    }

    [TestCase("MachineFactory", "OrbitalMachiningComplex")]
    [TestCase("WireMill", "OrbitalWireWorks")]
    [TestCase("BuildingMaterialsComplex", "OrbitalBuildingMaterialsWorks")]
    [TestCase("IndustrialCarbonizationRetort", "OrbitalCarbonizationComplex")]
    public void FirstUnlockedOrbitalFactoryPreservesEachProductPerProductivity(
        string sourceId, string targetId)
    {
        BuildingDef source = DataBase<BuildingDef>.Find(sourceId);
        BuildingDef target = DataBase<BuildingDef>.Find(targetId);
        Assert.That(source.UpgradeTo, Is.SameAs(target));
        PrepareFirstUnlock(target, source);

        SetAmount(source, ExpantaNum.One);
        var before = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (Pair<ResourceDef, ExpantaNum> output in source.ResourceGenerationRates)
            before.Add(output.First, resources.GetState(output.First).ProductionRate);
        SetAmount(source, ExpantaNum.Zero);
        SetAmount(target, ExpantaNum.One);

        foreach (KeyValuePair<ResourceDef, ExpantaNum> output in before)
        {
            ExpantaNum after = resources.GetState(output.Key).ProductionRate;
            TestContext.WriteLine($"FIRST_UNLOCK_ORBITAL {sourceId}->{targetId} {output.Key.Id}: " +
                $"rate={output.Value}->{after}, productivity={source.ProductivityConsumption}->{target.ProductivityConsumption}");
            Assert.That(output.Value, Is.GreaterThan(ExpantaNum.Zero), output.Key.Id);
            Assert.That((after / target.ProductivityConsumption).ToDouble(),
                Is.GreaterThanOrEqualTo((output.Value / source.ProductivityConsumption).ToDouble() * (1d - 1e-8d)),
                $"{sourceId}->{targetId}: {output.Key.Id} per productivity must not decrease after its required upgrades.");
        }
    }

    [Test]
    public void ComprehensiveFactoryImprovesEachProductAgainstAFixedSpecialistGroup()
    {
        BuildingDef target = DataBase<BuildingDef>.Find("OrbitalResourceExtractionArray");
        string[] specialistIds =
        {
            "IntegratedMetalMine", "MechanizedTextileMill", "TitaniumAlloySmelter",
            "OrbitalCryogenicPropellantArray", "OrbitalBuildingMaterialsWorks",
            "OrbitalMachiningComplex", "OrbitalWireWorks", "PhaseMaterialSynthesisArray"
        };
        string[] outputIds =
        {
            "SpecialMetalOre", "AdvancedMetalOre", "BasicMetalOre", "Cloth", "TitaniumAlloy",
            "RocketFuel", "Glass", "Ceramic", "Concrete", "Machinery", "Engine", "Composite",
            "CopperWire", "Electronics", "PhaseMaterial"
        };
        var specialists = new BuildingDef[specialistIds.Length];
        for (int i = 0; i < specialistIds.Length; i++)
            specialists[i] = DataBase<BuildingDef>.Find(specialistIds[i]);
        PrepareFirstUnlock(target, specialists);

        ExpantaNum previousProductivity = ExpantaNum.Zero;
        ExpantaNum previousSpace = ExpantaNum.Zero;
        foreach (BuildingDef specialist in specialists)
        {
            SetAmount(specialist, ExpantaNum.One);
            previousProductivity += specialist.ProductivityConsumption;
            previousSpace += specialist.SpaceCost;
        }
        var before = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (string id in outputIds)
        {
            ResourceDef output = DataBase<ResourceDef>.Find(id);
            before.Add(output, resources.GetState(output).ProductionRate);
        }
        foreach (BuildingDef specialist in specialists)
            SetAmount(specialist, ExpantaNum.Zero);
        SetAmount(target, ExpantaNum.One);

        TestContext.WriteLine($"FIRST_UNLOCK_GROUP productivity={previousProductivity}->{target.ProductivityConsumption}, " +
            $"space={previousSpace}->{target.SpaceCost}; one of each of {specialistIds.Length} fixed specialists.");
        foreach (KeyValuePair<ResourceDef, ExpantaNum> output in before)
        {
            ExpantaNum after = resources.GetState(output.Key).ProductionRate;
            TestContext.WriteLine($"FIRST_UNLOCK_GROUP_PRODUCT {output.Key.Id}: rate={output.Value}->{after}");
            Assert.That(output.Value, Is.GreaterThan(ExpantaNum.Zero), output.Key.Id);
            Assert.That(after.ToDouble(), Is.GreaterThan(output.Value.ToDouble() * 1.5d), output.Key.Id);
            Assert.That((after / target.ProductivityConsumption).ToDouble(),
                Is.GreaterThanOrEqualTo((output.Value / previousProductivity).ToDouble() * (1d - 1e-8d)),
                $"{output.Key.Id}: throughput per productivity must improve against the whole specialist group.");
            Assert.That((after / target.SpaceCost).ToDouble(),
                Is.GreaterThanOrEqualTo((output.Value / previousSpace).ToDouble() * (1d - 1e-8d)),
                $"{output.Key.Id}: throughput per territory must improve against the whole specialist group.");
        }
    }

    [Test]
    public void AutonomousFactoryImprovesEachProductAgainstAFixedSpecialistGroup()
    {
        BuildingDef target = DataBase<BuildingDef>.Find("AutonomousMatterFabricator");
        BuildingDef[] specialists =
        {
            DataBase<BuildingDef>.Find("PhantomMaterialsFabricator"),
            DataBase<BuildingDef>.Find("PhaseMaterialSynthesisArray"),
            DataBase<BuildingDef>.Find("OrbitalMachiningComplex")
        };
        int[] amounts = { 3, 3, 1 };
        string[] outputIds = { "PhantomAlloy", "PhantomWeave", "PhaseMaterial", "Composite" };
        PrepareFirstUnlock(target, specialists);

        ExpantaNum previousProductivity = ExpantaNum.Zero;
        ExpantaNum previousSpace = ExpantaNum.Zero;
        for (int i = 0; i < specialists.Length; i++)
        {
            ExpantaNum amount = new ExpantaNum(amounts[i]);
            SetAmount(specialists[i], amount);
            previousProductivity += specialists[i].ProductivityConsumption * amount;
            previousSpace += specialists[i].SpaceCost * amount;
        }
        var before = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (string id in outputIds)
        {
            ResourceDef output = DataBase<ResourceDef>.Find(id);
            before.Add(output, resources.GetState(output).ProductionRate);
        }
        foreach (BuildingDef specialist in specialists)
            SetAmount(specialist, ExpantaNum.Zero);
        SetAmount(target, ExpantaNum.One);

        TestContext.WriteLine($"FIRST_UNLOCK_ULTRA_GROUP productivity={previousProductivity}->{target.ProductivityConsumption}, " +
            $"space={previousSpace}->{target.SpaceCost}; " +
            "PhantomMaterialsFabricator*3, PhaseMaterialSynthesisArray*3, OrbitalMachiningComplex*1.");
        foreach (KeyValuePair<ResourceDef, ExpantaNum> output in before)
        {
            ExpantaNum after = resources.GetState(output.Key).ProductionRate;
            TestContext.WriteLine($"FIRST_UNLOCK_ULTRA_GROUP_PRODUCT {output.Key.Id}: rate={output.Value}->{after}");
            Assert.That(output.Value, Is.GreaterThan(ExpantaNum.Zero), output.Key.Id);
            Assert.That(after.ToDouble(), Is.GreaterThan(output.Value.ToDouble() * 1.5d), output.Key.Id);
            Assert.That((after / target.ProductivityConsumption).ToDouble(),
                Is.GreaterThanOrEqualTo((output.Value / previousProductivity).ToDouble() * (1d - 1e-8d)),
                $"{output.Key.Id}: throughput per productivity must improve against the whole specialist group.");
            Assert.That((after / target.SpaceCost).ToDouble(),
                Is.GreaterThanOrEqualTo((output.Value / previousSpace).ToDouble() * (1d - 1e-8d)),
                $"{output.Key.Id}: throughput per territory must improve against the whole specialist group.");
        }
    }

    private void PrepareFirstUnlock(BuildingDef target, params BuildingDef[] ownedSources)
    {
        researchDefinitions.Clear();
        workshopDefinitions.Clear();
        researchStates.Clear();
        workshopStates.Clear();
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            if (research.AdvancesTechLevel && research.TechLevel <= target.TechLevel)
                CompleteResearchPrerequisites(research);
        foreach (BuildingDef source in ownedSources)
            CompleteBuildingPrerequisites(source);
        CompleteBuildingPrerequisites(target);

        foreach (ResearchState state in researchStates)
            Assert.That(state.Definition.TechLevel, Is.LessThanOrEqualTo(target.TechLevel),
                $"{target.Id} must not rely on a later era.");
        ProgressionModifierManager.Rebuild(researchStates, workshopStates);
        game.State.RestoreCoreForEditor(0, target.TechLevel, GameState.BaseFoodCapacity, 0L);
        TestContext.WriteLine($"FIRST_UNLOCK_INPUT {target.Id}: " +
            $"ownedSources={ownedSources.Length}, requiredResearch={researchStates.Count}, " +
            $"requiredWorkshops={workshopStates.Count}, era={target.TechLevel}");
    }

    private void CompleteBuildingPrerequisites(BuildingDef definition)
    {
        foreach (ResearchDef required in definition.RequiredResearch)
            CompleteResearchPrerequisites(required);
        foreach (WorkshopUpgradeDef required in definition.RequiredWorkshopUpgrades)
            PurchaseWorkshopPrerequisites(required);
    }

    private void CompleteResearchPrerequisites(ResearchDef definition)
    {
        if (!researchDefinitions.Add(definition))
            return;
        foreach (ResearchDef prerequisite in definition.Prerequisites)
            CompleteResearchPrerequisites(prerequisite);
        var paidCosts = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (Pair<ResourceDef, ExpantaNum> cost in definition.ResourceRequirements)
            paidCosts.Add(cost.First, cost.Second);
        var state = new ResearchState(definition);
        state.RestoreForEditor(state.BaseCost, true, true, paidCosts);
        researchStates.Add(state);
    }

    private void PurchaseWorkshopPrerequisites(WorkshopUpgradeDef definition)
    {
        if (!workshopDefinitions.Add(definition))
            return;
        foreach (ResearchDef required in definition.RequiredResearch)
            CompleteResearchPrerequisites(required);
        foreach (WorkshopUpgradeDef required in definition.RequiredUpgrades)
            PurchaseWorkshopPrerequisites(required);
        var state = new WorkshopUpgradeState(definition);
        state.SetPurchasedForEditor(true);
        workshopStates.Add(state);
    }

    private static ExpantaNum CalculateSingleBuildingResearch(BuildingDef definition)
    {
        var state = new BuildingState(definition);
        state.SetAmountForEditor(ExpantaNum.One);
        return ResearchManager.CalculateResearchPower(new[] { state }, ExpantaNum.Zero) *
            ProgressionModifierManager.Current.GlobalResearchMultiplier;
    }

    private void SetAmount(BuildingDef definition, ExpantaNum amount)
    {
        // Full efficiency isolates the first-unlock upgrade; natural supply is checked by the paid progression route.
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(definition), amount);
    }
}
