using System.Collections.Generic;
using NUnit.Framework;

public sealed class C6IndustrialContentTests
{
    private static readonly string[] IndustrialResourceIds =
    {
        "Machinery",
        "Chemical",
        "Electronics",
        "CrudeOil",
        "Silica",
        "Coke",
        "Glass",
        "Ceramic",
        "RefinedFuel",
        "Lubricant",
        "Rubber",
        "CopperWire",
        "PrecisionParts",
        "Engine",
        "Concrete",
        "BauxiteOre",
        "Aluminum",
        "Explosives"
    };

    private static readonly string[] IndustrialBuildingIds =
    {
        "SteamPlant",
        "OilDerrick",
        "SilicaQuarry",
        "CokeOven",
        "Glassworks",
        "MachineFactory",
        "ChemicalPlant",
        "OilRefinery",
        "WireMill",
        "University",
        "RailHub",
        "IndustrialMetalSmelter",
        "IndustrialBronzeFoundry",
        "BlastFurnace",
        "AluminumSmelter",
        "ConcreteWorks",
        "CentralPowerStation",
        "NickelRefinery",
        "RareMetalMine",
        "TitaniumMetallurgicalComplex"
    };

    [Test]
    public void C601_IndustrialResourcesHavePlayerFacingMetadata()
    {
        for (int i = 0; i < IndustrialResourceIds.Length; i++)
        {
            Resource resource = DataBase<Resource>.Find(IndustrialResourceIds[i]);
            Assert.That(resource, Is.Not.Null);
            Assert.That(string.IsNullOrWhiteSpace(resource.Label), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(resource.Description), Is.False);
        }
    }

    [Test]
    public void C601_IndustrialBuildingsExistAtIndustrialTechLevel()
    {
        for (int i = 0; i < IndustrialBuildingIds.Length; i++)
        {
            Building building = DataBase<Building>.Find(IndustrialBuildingIds[i]);
            Assert.That(building, Is.Not.Null, $"Missing industrial building '{IndustrialBuildingIds[i]}'.");
            Assert.That(building.TechLevel, Is.EqualTo(TechLevel.Industrial));
            Assert.That(building.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));
        }
    }

    [Test]
    public void C602_IndustrialBuildingsDeclarePowerAndLogisticsFlows()
    {
        Assert.That(DataBase<Building>.Find("SteamPlant").PowerProductionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("SteamPlant").LogisticsProductionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("MachineFactory").PowerConsumptionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("RailHub").LogisticsProductionRate,
            Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void C602_IndustrialChemistryFollowsThePowerGridFoundation()
    {
        Research chemistry = DataBase<Research>.Find("IndustrialChemistry");
        Research powerGrid = DataBase<Research>.Find("PowerGridEngineering");

        Assert.That(chemistry, Is.Not.Null);
        Assert.That(powerGrid, Is.Not.Null);
        Assert.That(chemistry.Prerequisites, Does.Contain(powerGrid));

        Research electrical = DataBase<Research>.Find("ElectricalEngineering");
        Research standardization = DataBase<Research>.Find("Standardization");
        Assert.That(electrical, Is.Not.Null);
        Assert.That(standardization, Is.Not.Null);
        Assert.That(ContainsResearch(electrical.Prerequisites, chemistry), Is.False);
        Assert.That(ContainsResearch(standardization.Prerequisites, chemistry), Is.False);

        Assert.That(DataBase<Building>.Find("OilDerrick").RequiredResearch,
            Does.Contain(powerGrid));
        Assert.That(DataBase<Building>.Find("CokeOven").RequiredResearch,
            Does.Contain(powerGrid));
        Assert.That(DataBase<Building>.Find("IndustrialMetalSmelter").RequiredResearch,
            Does.Contain(powerGrid));

        Building powerStation = DataBase<Building>.Find("CentralPowerStation");
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "Steel"), Is.True);
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "Ceramic"), Is.True);
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "Coal"), Is.True);
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "Machinery"), Is.False);
        Assert.That(ContainsResource(powerStation.ResourceRequirements, "CopperWire"), Is.False);
        Assert.That(ContainsResource(powerStation.ResourceConsumptionRates, "Lubricant"), Is.False);
        Assert.That(powerStation.LogisticsConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C605_IndustrialMetalSmelterProducesCopperTinAndIron()
    {
        Building smelter = DataBase<Building>.Find("IndustrialMetalSmelter");
        Assert.That(FindRate(smelter.ResourceGenerationRates, "Copper"), Is.EqualTo(3d).Within(0.0001d));
        Assert.That(FindRate(smelter.ResourceGenerationRates, "Tin"), Is.EqualTo(2.4d).Within(0.0001d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "CopperOre"), Is.EqualTo(2.2d).Within(0.0001d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "TinOre"), Is.EqualTo(1.8d).Within(0.0001d));
        Assert.That(FindRate(smelter.ResourceGenerationRates, "Iron"), Is.EqualTo(1.8d).Within(0.0001d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "IronOre"), Is.EqualTo(2.4d).Within(0.0001d));
        Assert.That(FindRate(smelter.ResourceConsumptionRates, "Steel"), Is.EqualTo(0d).Within(0.0001d));
    }

    [Test]
    public void C612_IndustrialMultiMetalMineRecoversCommonOres()
    {
        Building mine = DataBase<Building>.Find("RareMetalMine");
        Assert.That(mine.Label, Does.Contain("多金属"));
        Assert.That(FindRate(mine.ResourceGenerationRates, "BauxiteOre"), Is.EqualTo(2d).Within(0.0001d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "CopperOre"), Is.EqualTo(0.45d).Within(0.0001d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "TinOre"), Is.EqualTo(0.35d).Within(0.0001d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "IronOre"), Is.EqualTo(0.35d).Within(0.0001d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "TitaniumConcentrate"), Is.EqualTo(1.6d).Within(0.0001d));
        Assert.That(FindRate(mine.ResourceGenerationRates, "NickelConcentrate"), Is.EqualTo(1.2d).Within(0.0001d));
    }

    [Test]
    public void C613_PoweredMiningImprovesTheUnifiedIndustrialMine()
    {
        WorkshopUpgradeDefinition workshop = DataBase<WorkshopUpgradeDefinition>.Find("PoweredMining");
        Assert.That(workshop, Is.Not.Null);
        Assert.That(HasBuildingEffect(workshop, "RareMetalMine", 1.2d), Is.True);
    }

    [Test]
    public void C614_SeparateBauxiteMineWasRemovedAfterUnifiedMineMigration()
    {
        Assert.That(DataBase<Building>.Find("BauxiteMine"), Is.Null);
        Assert.That(DataBase<Building>.Find("RareMetalMine"), Is.Not.Null);
    }

    [Test]
    public void C609_MachineFactoryConsumesRubberForEngineProduction()
    {
        Building factory = DataBase<Building>.Find("MachineFactory");
        Assert.That(factory, Is.Not.Null, "机器制造厂定义不能为空。");
        Assert.That(FindRate(factory.ResourceGenerationRates, "Engine"), Is.GreaterThan(0d));
        Assert.That(FindRate(factory.ResourceConsumptionRates, "Rubber"),
            Is.EqualTo(0.1d).Within(0.0001d), "机器制造厂应消耗橡胶来生产发动机。");
    }

    [Test]
    public void C610_RailHubConsumesEnginesForLogistics()
    {
        Building railHub = DataBase<Building>.Find("RailHub");
        Assert.That(railHub, Is.Not.Null, "铁路枢纽定义不能为空。");
        Assert.That(FindRate(railHub.ResourceConsumptionRates, "Engine"),
            Is.EqualTo(0.05d).Within(0.0001d), "铁路枢纽应持续消耗发动机来维持运输能力。");
        Assert.That(FindRate(railHub.ResourceConsumptionRates, "Machinery"),
            Is.EqualTo(0.08d).Within(0.0001d), "铁路枢纽应持续消耗机械设备来维护运输能力。");
    }

    [Test]
    public void C606_IndustrialPowerAndLogisticsRolesRemainDistinct()
    {
        Building powerStation = DataBase<Building>.Find("CentralPowerStation");
        Building railHub = DataBase<Building>.Find("RailHub");

        Assert.That(powerStation.PowerProductionRate, Is.GreaterThan(ExpantaNum.Zero),
            "中央电站必须提供电力。");
        Assert.That(railHub.LogisticsProductionRate, Is.GreaterThan(ExpantaNum.Zero),
            "铁路枢纽必须提供物流。");
        Assert.That(powerStation.LogisticsProductionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(railHub.PowerProductionRate, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C603_IndustrialBuildingsUseProductivityAndFlowsInsteadOfFood()
    {
        for (int i = 0; i < IndustrialBuildingIds.Length; i++)
        {
            Building building = DataBase<Building>.Find(IndustrialBuildingIds[i]);
            Assert.That(building.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero),
                $"Industrial building '{building.Id}' must not directly consume Food.");
            Assert.That(building.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(
                building.PowerProductionRate > ExpantaNum.Zero ||
                building.PowerConsumptionRate > ExpantaNum.Zero ||
                building.LogisticsProductionRate > ExpantaNum.Zero ||
                building.LogisticsConsumptionRate > ExpantaNum.Zero,
                $"Industrial building '{building.Id}' must declare a Power or Logistics flow.");
        }
    }

    [Test]
    public void C603_IndustrialEraRetainsCoalCopperIronAndSteelUses()
    {
        var legacyIndustrialResources = new[] { "Coal", "Copper", "Iron", "Steel" };
        for (int i = 0; i < legacyIndustrialResources.Length; i++)
        {
            Assert.That(CountConsumerBuildings(legacyIndustrialResources[i]), Is.GreaterThan(0),
                $"Industrial layer lost its use for '{legacyIndustrialResources[i]}'.");
        }
    }

    [Test]
    public void C604_IndustrialUpgradeChainsAreExplicitAndAcyclic()
    {
        var expected = new Dictionary<string, string>
        {
            ["MetalSmelter"] = "IndustrialMetalSmelter",
            ["BronzeFoundry"] = "IndustrialBronzeFoundry",
            ["SteelForge"] = "BlastFurnace",
            ["SteamPlant"] = "CentralPowerStation"
        };

        foreach (var pair in expected)
        {
            Building source = DataBase<Building>.Find(pair.Key);
            Building target = DataBase<Building>.Find(pair.Value);
            Assert.That(source.UpgradeTo, Is.SameAs(target),
                $"Building chain '{pair.Key}' must point to '{pair.Value}'.");
        }

        BuildingManager.ValidateBuildingChains(DataBase<Building>.All);
    }

    [Test]
    public void C607_IndustrialProductionGraphHasNoRecipeCycle()
    {
        string error;
        bool valid = EconomyDependencyValidator.Validate(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            DataBase<WorkshopUpgradeDefinition>.All,
            out error);

        Assert.That(valid, Is.True, error);
    }

    [Test]
    public void C601_IndustrialBuildingsOwnTheirResearchPrerequisites()
    {
        Research industrialization = DataBase<Research>.Find("Industrialization");
        var expected = new HashSet<string>(IndustrialBuildingIds);
        var actual = new HashSet<string>();

        foreach (Building building in DataBase<Building>.All)
        {
            if (building.TechLevel != TechLevel.Industrial)
                continue;
            Assert.That(building.RequiredResearch, Is.Not.Empty, building.Id);
            for (int i = 0; i < building.RequiredResearch.Count; i++)
                if (building.RequiredResearch[i] == industrialization)
                    actual.Add(building.Id);
        }

        Assert.That(actual, Is.SubsetOf(expected));
        Assert.That(actual, Does.Contain("SteamPlant"));
    }

    [Test]
    public void C601_IndustrialResourcesHaveSourcesAndMultipleUses()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        for (int i = 0; i < IndustrialResourceIds.Length; i++)
        {
            Assert.That(result.ResourcesWithoutSource, Does.Not.Contain(IndustrialResourceIds[i]),
                string.Join(", ", result.ResourcesWithoutSource));
            Assert.That(result.ResourcesWithoutSink, Does.Not.Contain(IndustrialResourceIds[i]),
                string.Join(", ", result.ResourcesWithoutSink));
            Assert.That(CountConsumerUses(IndustrialResourceIds[i]), Is.GreaterThanOrEqualTo(2),
                $"Industrial resource '{IndustrialResourceIds[i]}' must have at least two independent uses.");
        }
    }

    [Test]
    public void C608_NewIndustrialResourcesHavePlayerFacingMetadata()
    {
        AssertResourceMetadata("Concrete");
        AssertResourceMetadata("Explosives");
        AssertResourceMetadata("BauxiteOre");
        AssertResourceMetadata("Aluminum");
    }

    [Test]
    public void C611_IndustrialConstructionMaterialsRemainUsefulInSpaceEra()
    {
        Building launchCenter = DataBase<Building>.Find("LaunchCenter");
        Building orbitalStation = DataBase<Building>.Find("OrbitalStation");
        Building shipyard = DataBase<Building>.Find("Shipyard");

        Assert.That(ContainsResource(launchCenter.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(ContainsResource(launchCenter.ResourceRequirements, "Explosives"), Is.True);
        Assert.That(ContainsResource(orbitalStation.ResourceRequirements, "Concrete"), Is.True);
        Assert.That(ContainsResource(shipyard.ResourceRequirements, "Concrete"), Is.True);
    }

    private static int CountConsumerBuildings(string resourceId)
    {
        int count = 0;
        for (int i = 0; i < DataBase<Building>.All.Count; i++)
        {
            Building building = DataBase<Building>.All[i];
            if (ContainsResource(building.ResourceRequirements, resourceId) ||
                ContainsResource(building.ResourceConsumptionRates, resourceId))
                count++;
        }
        return count;
    }

    private static int CountConsumerUses(string resourceId)
    {
        int count = CountConsumerBuildings(resourceId);
        for (int i = 0; i < DataBase<Research>.All.Count; i++)
            if (ContainsResource(DataBase<Research>.All[i].ResourceRequirements, resourceId))
                count++;
        for (int i = 0; i < DataBase<WorkshopUpgradeDefinition>.All.Count; i++)
            if (ContainsResource(DataBase<WorkshopUpgradeDefinition>.All[i].ResourceRequirements, resourceId))
                count++;
        return count;
    }

    private static bool ContainsResource(
        IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = pairs[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return true;
        }
        return false;
    }

    private static double FindRate(
        IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = pairs[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return pair.Second.ToDouble();
        }
        return 0d;
    }

    private static bool HasBuildingEffect(
        WorkshopUpgradeDefinition workshop,
        string buildingId,
        double minimumMultiplier)
    {
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null &&
                effect.Type == WorkshopEffectType.BuildingProductionMultiplier &&
                effect.Building != null && effect.Building.Id == buildingId &&
                effect.Value.ToDouble() >= minimumMultiplier)
                return true;
        }
        return false;
    }

    private static bool ContainsResearch(
        System.Collections.Generic.IReadOnlyList<Research> prerequisites,
        Research target)
    {
        if (prerequisites == null || target == null)
            return false;
        for (int i = 0; i < prerequisites.Count; i++)
            if (prerequisites[i] == target)
                return true;
        return false;
    }

    private static void AssertResourceMetadata(string resourceId)
    {
        Resource resource = DataBase<Resource>.Find(resourceId);
        Assert.That(resource, Is.Not.Null, $"资源“{resourceId}”必须存在。");
        Assert.That(string.IsNullOrWhiteSpace(resource.Label), Is.False,
            $"资源“{resourceId}”必须有中文名称。");
        Assert.That(string.IsNullOrWhiteSpace(resource.Description), Is.False,
            $"资源“{resourceId}”必须有中文描述。");
        Assert.That(resource.Sprite, Is.Not.Null,
            $"资源“{resourceId}”必须有 UI 图标。");
    }
}
