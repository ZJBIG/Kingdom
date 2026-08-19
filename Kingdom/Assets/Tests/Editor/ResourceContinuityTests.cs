using System.Linq;
using NUnit.Framework;

public sealed class ResourceContinuityTests
{
    [Test]
    public void 资源变化率统一按秒结算()
    {
        ExpantaNum result = ResourceManager.AdvanceAmount(
            new ExpantaNum(10),
            new ExpantaNum(3),
            new ExpantaNum(1),
            2d);

        Assert.That(
            result.ToDouble(),
            Is.EqualTo(14d).Within(1e-9),
            "资源变化率必须按每秒数值乘以经过秒数结算，不得隐式按分钟换算。");
    }

    [Test]
    public void 铝土矿必须进入铝冶金研究与后续冶炼链()
    {
        Research aluminumMetallurgy = DataBase<Research>.Find("AluminumMetallurgy");

        Assert.That(
            HasPositiveRequirement(aluminumMetallurgy, "BauxiteOre"),
            Is.True,
            "铝冶金研究必须消耗铝土矿样品，保持矿场到冶炼链的长期用途。");
        Assert.That(
            HasBuildingRequirement("AluminumSmelter", "BauxiteOre"),
            Is.True);
    }

    [Test]
    public void 中世纪转型研究必须拥有冶炼前置并消耗石砖与布料()
    {
        Research transition = DataBase<Research>.Find("FeudalAdministration");

        Assert.That(transition.Prerequisites.Select(research => research.Id),
            Is.EquivalentTo(new[] { "Smithing_Bronze", "Smithing_Iron", "WrittenRecords" }));
        Assert.That(HasPositiveRequirement(transition, "StoneBrick"), Is.True);
        Assert.That(HasPositiveRequirement(transition, "Cloth"), Is.True);
    }

    [Test]
    public void 食物与基础材料链必须保留后续时代用途()
    {
        Assert.That(HasBuildingRequirement("Granary", "Ceramic"), Is.True);
        Assert.That(HasBuildingRequirement("ScribeHut", "Cloth"), Is.True);
        Assert.That(HasBuildingRequirement("MetalSmelter", "StoneBrick"), Is.True);
    }

    [Test]
    public void 太空资源必须在太空建造前拥有工业来源()
    {
        Assert.That(HasGenerationRate("ChemicalPlant", "RocketFuel", 0.25d), Is.True);
        Assert.That(HasGenerationRate("MachineFactory", "Composite", 0.45d), Is.True);
        Assert.That(HasBuildingRequirement("LaunchCenter", "RocketFuel"), Is.True);
        Assert.That(HasBuildingRequirement("Shipyard", "Composite"), Is.True);
        Assert.That(HasColonizationCost("Moon", "RocketFuel", 25d / 60d), Is.True);
        Assert.That(HasColonizationCost("Mars", "Composite", 45d / 60d), Is.True);
        Assert.That(HasColonizationCost("Mars", "TitaniumAlloy", 0.3333333333d), Is.True);
        Assert.That(HasCampaignCost("AlphaCentauri", "RocketFuel", 100d / 60d), Is.True);
    }

    [Test]
    public void 工业中间矿物必须连接到太空长期材料链()
    {
        Assert.That(HasBuildingRequirement("AluminumSmelter", "BauxiteOre"), Is.True);
        Assert.That(HasBuildingGeneration("AluminumSmelter", "Aluminum"), Is.True);
        Assert.That(HasLaterEraUse("Aluminum"), Is.True);

        Assert.That(HasBuildingRequirement("NickelRefinery", "NickelConcentrate"), Is.True);
        Assert.That(HasBuildingGeneration("NickelRefinery", "Nickel"), Is.True);
        Assert.That(HasLaterEraUse("Nickel"), Is.True);

        Assert.That(HasBuildingRequirement("TitaniumMetallurgicalComplex", "TitaniumConcentrate"), Is.True);
        Assert.That(HasBuildingGeneration("TitaniumMetallurgicalComplex", "TitaniumAlloy"), Is.True);
        Assert.That(HasLaterEraUse("TitaniumAlloy"), Is.True);
    }

    [Test]
    public void 锡必须进入轨道电力硬件的跨时代链条()
    {
        Assert.That(HasPositiveRequirement(DataBase<Research>.Find("OrbitalEngineering"), "Tin"), Is.True);
        Assert.That(HasBuildingRequirement("OrbitalSolarArray", "Tin"), Is.True);
        Assert.That(HasWorkshopRequirement("OrbitalPowerBeaming", "Tin"), Is.True);
        Assert.That(HasLaterEraUse("Tin"), Is.True);
    }

    [Test]
    public void 玻璃必须进入轨道热管理工坊()
    {
        Assert.That(HasGenerationRate("Glassworks", "Glass", 1.2d), Is.True);
        Assert.That(HasBuildingConsumption("Glassworks", "StoneBrick"), Is.True);
        Assert.That(HasBuildingConsumption("Glassworks", "Coke"), Is.True);
        Assert.That(HasPositiveRequirement(DataBase<Research>.Find("IndustrialChemistry"), "Glass"), Is.True);
        Assert.That(HasWorkshopRequirement("OrbitalThermalManagement", "Glass"), Is.True);
        Assert.That(HasWorkshopRequirement("OrbitalPowerBeaming", "Glass"), Is.True);
        Assert.That(HasBuildingRequirement("OrbitalHabitatMegastructure", "Glass"), Is.True);
        Assert.That(HasLaterEraUse("Glass"), Is.True);
    }

    [Test]
    public void 化学品必须进入低温推进剂实体工艺()
    {
        Assert.That(HasWorkshopRequirement("CryogenicFuelSystems", "Chemical"), Is.True);
        Assert.That(HasLaterEraUse("Chemical"), Is.True);
    }

    [Test]
    public void 工业核心资源必须延续到太空建筑研究与工坊()
    {
        string[] industrialResources =
        {
            "Nickel", "Rubber", "Chemical", "Glass", "Electronics",
            "Engine", "Aluminum", "RefinedFuel", "Lubricant", "Coke"
        };

        for (int i = 0; i < industrialResources.Length; i++)
        {
            Assert.That(
                HasLaterEraUse(industrialResources[i]),
                Is.True,
                $"工业资源 {industrialResources[i]} 必须在太空建筑、研究和工坊中继续发挥作用。");
        }
    }

    [Test]
    public void 原油必须通过炼油链继续服务太空推进系统()
    {
        Assert.That(HasBuildingConsumption("OilRefinery", "CrudeOil"), Is.True);
        Assert.That(HasBuildingGeneration("OilRefinery", "RefinedFuel"), Is.True);
        Assert.That(HasWorkshopRequirement("CryogenicFuelSystems", "RocketFuel"), Is.True);
        Assert.That(HasLaterEraUse("RefinedFuel"), Is.True);
    }

    [Test]
    public void 煤炭必须通过焦化链服务太空幽影材料()
    {
        Assert.That(HasBuildingConsumption("CokeOven", "Coal"), Is.True);
        Assert.That(HasBuildingGeneration("CokeOven", "Coke"), Is.True);
        Assert.That(HasPositiveRequirement(DataBase<Research>.Find("PhantomMaterials"), "Coke"), Is.True);
    }

    [Test]
    public void 基础金属矿必须通过统一冶炼进入跨时代材料链()
    {
        Assert.That(HasBuildingGeneration("IndustrialMetalSmelter", "Copper"), Is.True);
        Assert.That(HasBuildingGeneration("IndustrialMetalSmelter", "Tin"), Is.True);
        Assert.That(HasBuildingGeneration("IndustrialMetalSmelter", "Iron"), Is.True);
        Assert.That(HasLaterEraUse("Steel"), Is.True);
        Assert.That(HasLaterEraUse("CopperWire"), Is.True);
    }

    [Test]
    public void IndustrialIntermediateMineralsEnterTheirMatchingPhysicalWorkshops()
    {
        Assert.That(HasWorkshopRequirement("AluminumElectrolyticCells", "BauxiteOre"), Is.True);
        Assert.That(HasWorkshopRequirement("NickelLeachingElectrowinningSystem", "NickelConcentrate"), Is.True);
        Assert.That(HasWorkshopRequirement("TitaniumReductionRetorts", "TitaniumConcentrate"), Is.True);
    }

    [Test]
    public void BasicMineralsReachLaterErasThroughDurableIntermediateMaterials()
    {
        Assert.That(HasBuildingRequirement("CeramicKiln", "Clay"), Is.True);
        Assert.That(HasBuildingGeneration("CeramicKiln", "Ceramic"), Is.True);
        Assert.That(HasLaterEraUse("Ceramic"), Is.True);

        Assert.That(HasBuildingRequirement("StoneCuttingWorkshop", "StoneChunk"), Is.True);
        Assert.That(HasBuildingGeneration("StoneCuttingWorkshop", "StoneBrick"), Is.True);
        Assert.That(HasLaterEraUse("StoneBrick"), Is.True);
    }

    [Test]
    public void 生物质必须进入太空居住的建筑研究与工坊链()
    {
        Assert.That(HasBuildingRequirement("OrbitalHabitatMegastructure", "Biomass"), Is.True);
        Assert.That(HasBuildingConsumption("OrbitalHabitatMegastructure", "Biomass"), Is.True);
        Assert.That(HasBuildingConsumptionRate("OrbitalHabitatMegastructure", "Electronics", 0.10d), Is.True);
        Assert.That(HasBuildingConsumption("OrbitalHabitatMegastructure", "Chemical"), Is.True);
        Assert.That(HasBuildingConsumption("OrbitalHabitatMegastructure", "Rubber"), Is.True);
        Assert.That(HasPositiveRequirement(DataBase<Research>.Find("OrbitalHabitation"), "Biomass"), Is.True);
        Assert.That(HasWorkshopRequirement("OrbitalLifeSupportNetworks", "Biomass"), Is.True);
        Assert.That(HasLaterEraUse("Biomass"), Is.True);
    }

    [Test]
    public void 纤维采集地必须在工业时代升级为同时产粮的种植田()
    {
        Building camp = DataBase<Building>.Find("FiberGatheringCamp");
        Building farm = DataBase<Building>.Find("Farm");
        Building field = DataBase<Building>.Find("PlantingField");
        Building irrigation = DataBase<Building>.Find("IrrigationWorks");

        Assert.That(camp, Is.Not.Null);
        Assert.That(farm, Is.Not.Null);
        Assert.That(field, Is.Not.Null);
        Assert.That(irrigation, Is.Not.Null);
        Assert.That(camp.UpgradeTo, Is.EqualTo(field));
        Assert.That(farm.UpgradeTo, Is.EqualTo(irrigation));
        Assert.That(irrigation.UpgradeTo, Is.EqualTo(field));
        Assert.That(field.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(field.FoodProductionRate, Is.GreaterThan(irrigation.FoodProductionRate * 2d));
        Assert.That(field.SpaceCost, Is.GreaterThan(irrigation.SpaceCost));
        Assert.That(field.ProductivityConsumption, Is.GreaterThan(irrigation.ProductivityConsumption));
        Assert.That(HasBuildingGeneration("PlantingField", "Biomass"), Is.True);
    }

    [Test]
    public void 太空居住链的基础生物质用量必须显著高于高级材料()
    {
        Building habitat = DataBase<Building>.Find("OrbitalHabitatMegastructure");
        Research habitation = DataBase<Research>.Find("OrbitalHabitation");
        WorkshopUpgrade lifeSupport = DataBase<WorkshopUpgrade>.Find("OrbitalLifeSupportNetworks");

        Assert.That(GetBuildingAmount(habitat, "Biomass"), Is.GreaterThan(
            GetBuildingAmount(habitat, "TitaniumAlloy") * 4d));
        Assert.That(GetBuildingAmount(habitat, "Glass"), Is.GreaterThan(
            GetBuildingAmount(habitat, "TitaniumAlloy")));
        Assert.That(GetBuildingAmount(habitat, "Biomass", true), Is.GreaterThan(
            GetBuildingAmount(habitat, "TitaniumAlloy", true) * 4d));
        Assert.That(GetResearchAmount(habitation, "Biomass"), Is.GreaterThan(
            GetResearchAmount(habitation, "TitaniumAlloy") * 4d));
        Assert.That(GetWorkshopAmount(lifeSupport, "Biomass"), Is.GreaterThan(
            GetWorkshopAmount(lifeSupport, "TitaniumAlloy") * 4d));
    }

    [Test]
    public void FullOrbitalHabitatCanBeSupportedByOrbitalAgriculture()
    {
        Building habitat = DataBase<Building>.Find("OrbitalHabitatMegastructure");
        Building agroecologyArray = DataBase<Building>.Find("OrbitalAgroecologyArray");

        Assert.That(habitat, Is.Not.Null);
        Assert.That(agroecologyArray, Is.Not.Null);
        ExpantaNum habitatFoodDemand =
            habitat.PopulationCapacityGranted * PopulationState.FoodConsumptionPerPerson +
            habitat.FoodConsumptionRate;
        ExpantaNum unmodifiedArrayCount =
            habitatFoodDemand / agroecologyArray.FoodProductionRate;

        Assert.That(habitatFoodDemand, Is.EqualTo(new ExpantaNum(2408d)));
        Assert.That(unmodifiedArrayCount, Is.GreaterThanOrEqualTo(new ExpantaNum(3d)));
        Assert.That(unmodifiedArrayCount, Is.LessThanOrEqualTo(new ExpantaNum(4d)));
        Assert.That(agroecologyArray.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void OrbitalStationUsesMuchMoreBiomassThanTitaniumAlloy()
    {
        Building station = DataBase<Building>.Find("OrbitalStation");

        Assert.That(station, Is.Not.Null);
        Assert.That(GetBuildingAmount(station, "Biomass"), Is.GreaterThan(
            GetBuildingAmount(station, "TitaniumAlloy") * 4d));
        Assert.That(GetBuildingAmount(station, "Biomass", true), Is.GreaterThan(
            GetBuildingAmount(station, "TitaniumAlloy", true) * 4d));
    }

    [Test]
    public void 轨道空间站必须使用镍基耐热材料并持续维护()
    {
        Building station = DataBase<Building>.Find("OrbitalStation");

        Assert.That(station, Is.Not.Null);
        Assert.That(GetBuildingAmount(station, "Nickel"), Is.EqualTo(1000d).Within(0.0001d));
        Assert.That(HasBuildingConsumptionRate("OrbitalStation", "Nickel", 0.04d), Is.True);
    }

    [Test]
    public void 太空建筑必须使用高级材料持续维护并保持多重去向()
    {
        string[] advancedResources =
        {
            "TitaniumAlloy", "Composite", "PhantomAlloy", "PhantomWeave", "PhaseMaterial"
        };

        Building[] spaceBuildings = DataBase<Building>.All
            .Where(building => building != null && building.TechLevel >= TechLevel.Spacer)
            .ToArray();

        Assert.That(spaceBuildings, Is.Not.Empty);
        Assert.That(spaceBuildings.All(building => advancedResources.Any(resourceId =>
            HasResourcePair(building.ResourceRequirements, resourceId))), Is.True,
            "每座太空建筑的建造成本都必须包含符合职责的高级结构材料。");
        Assert.That(spaceBuildings.All(building => advancedResources.Any(resourceId =>
            HasResourcePair(building.ResourceConsumptionRates, resourceId))), Is.True,
            "每座太空建筑都必须有符合职责的高级材料x/s维护消耗。");

        foreach (string resourceId in advancedResources)
        {
            int sinks = spaceBuildings.Count(building =>
                HasResourcePair(building.ResourceConsumptionRates, resourceId));
            Assert.That(sinks, Is.GreaterThanOrEqualTo(2),
                $"{resourceId}必须至少拥有两个太空建筑持续消耗去向。");
        }
    }

    [Test]
    public void 陶瓷仍然是跨时代工业与深空材料()
    {
        Assert.That(HasGenerationRate("CeramicKiln", "Ceramic", 1d), Is.True);
        Assert.That(HasBuildingRequirement("AdvancedCeramicsPlant", "Ceramic"), Is.True);
        Assert.That(HasBuildingRequirement("DeepSpaceObservatory", "Ceramic"), Is.True);
        Assert.That(HasBuildingRequirement("QuantumComputingArray", "Ceramic"), Is.True);
        Assert.That(HasBuildingRequirement("PhaseMaterialSynthesisArray", "Ceramic"), Is.True);
        Assert.That(HasBuildingRequirement("Shipyard", "Ceramic"), Is.True);
    }

    [Test]
    public void 炸药应作为工业与远征中的持续消耗资源()
    {
        Assert.That(HasGenerationRate("ChemicalPlant", "Explosives", 0.35d), Is.True);
        Assert.That(HasBuildingConsumptionRate("ChemicalPlant", "Electronics", 0.04d), Is.True);
        Assert.That(HasBuildingConsumption("OilDerrick", "Explosives"), Is.True);
        Assert.That(HasBuildingConsumption("RareMetalMine", "Explosives"), Is.True);
        Assert.That(HasBuildingConsumption("RailHub", "Explosives"), Is.True);
        Assert.That(HasBuildingConsumptionRate("RailHub", "Explosives", 0.03d), Is.True);
        Assert.That(HasBuildingRequirement("OilRefinery", "Chemical"), Is.True);
        Assert.That(HasBuildingConsumption("LaunchCenter", "Explosives"), Is.False);
        Assert.That(HasBuildingConsumption("DeepSpaceObservatory", "Explosives"), Is.False);
        Assert.That(HasCampaignCost("AlphaCentauri", "Explosives", 0.08d), Is.False);
        Assert.That(HasCampaignCost("ProximaB", "Explosives", 0.16d), Is.False);
        Assert.That(HasCampaignCost("TauCetiFoundry", "Explosives", 0.24d), Is.False);
        Assert.That(HasCampaignCost("SiriusResourceBelt", "Explosives", 0.4d), Is.False);
        Assert.That(HasPositiveRequirement(DataBase<Research>.Find("DeepSpaceSurvey"), "Explosives"), Is.False);
        Assert.That(HasBuildingRequirement("LaunchCenter", "Explosives"), Is.False);
        Building[] spacerBuildings = DataBase<Building>.All
            .Where(building => building != null && building.TechLevel >= TechLevel.Spacer)
            .ToArray();
        Assert.That(spacerBuildings.All(building =>
            !HasBuildingConsumption(building.Id, "Explosives") &&
            !HasBuildingRequirement(building.Id, "Explosives")), Is.True);
        Assert.That(DataBase<Building>.All.Any(building =>
            building != null && HasBuildingRequirement(building.Id, "Explosives")), Is.False,
            "工业炸药应作为持续生产线投入，不应成为建筑的一次性建造材料。");
    }

    [Test]
    public void 工业住宅必须持续维护钢结构()
    {
        Assert.That(HasBuildingConsumption("IndustrialHabitationComplex", "Steel"), Is.True);
        Assert.That(
            HasBuildingConsumptionRate("IndustrialHabitationComplex", "Steel", 0.02d),
            Is.True);
    }

    [Test]
    public void 工业住宅必须持续维护混凝土骨架()
    {
        Assert.That(HasBuildingConsumption("IndustrialHabitationComplex", "Concrete"), Is.True);
        Assert.That(
            HasBuildingConsumptionRate("IndustrialHabitationComplex", "Concrete", 0.03d),
            Is.True);
    }

    [Test]
    public void 所有工业建筑必须拥有持续资源维护回路()
    {
        Building[] industrialBuildings = DataBase<Building>.All
            .Where(building => building != null && building.TechLevel == TechLevel.Industrial)
            .ToArray();

        Assert.That(industrialBuildings, Is.Not.Empty);
        foreach (Building building in industrialBuildings)
        {
            Assert.That(
                building.ResourceConsumptionRates,
                Is.Not.Null.And.Not.Empty,
                $"工业建筑 {building.Id} 必须有持续资源维护消耗，而不是只在建造时消耗资源。");
        }
    }

    [Test]
    public void 生物质必须同时覆盖生态医疗居住与远征消耗()
    {
        Assert.That(HasBuildingGeneration("FiberGatheringCamp", "Biomass"), Is.True);
        Assert.That(HasBuildingGeneration("PlantingField", "Biomass"), Is.True);
        Assert.That(HasBuildingConsumption("OrbitalHabitatMegastructure", "Biomass"), Is.True);
        Assert.That(HasPositiveRequirement(DataBase<Research>.Find("OrbitalAgroecology"), "Biomass"), Is.True);
        Assert.That(HasWorkshopRequirement("OrbitalAgroponicSystems", "Biomass"), Is.True);
        Assert.That(HasCampaignCost("AlphaCentauri", "Biomass", 0.8d), Is.True);
    }

    [Test]
    public void 工业中间资源必须继续支撑太空设施与后期工坊()
    {
        Assert.That(HasLaterEraUse("Nickel"), Is.True);
        Assert.That(HasLaterEraUse("Rubber"), Is.True);
        Assert.That(HasLaterEraUse("Lubricant"), Is.True);

        Assert.That(HasBuildingConsumption("TitaniumMetallurgicalComplex", "NickelConcentrate"), Is.True);
        Assert.That(HasBuildingConsumption("Shipyard", "Rubber"), Is.True);
        Assert.That(HasBuildingConsumption("OrbitalLogisticsHub", "Lubricant"), Is.True);

        Assert.That(HasWorkshopRequirement("AdvancedCompositeLayup", "Nickel"), Is.True);
        Assert.That(HasWorkshopRequirement("AutonomousFleetLogistics", "Rubber"), Is.True);
        Assert.That(HasWorkshopRequirement("AutonomousFleetLogistics", "Lubricant"), Is.True);
    }

    private static bool HasRequirement(Research research, string resourceId, double amount)
    {
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = research.ResourceRequirements[i];
            if (pair.First != null && pair.First.Id == resourceId &&
                pair.Second.ToDouble() == amount)
                return true;
        }
        return false;
    }

    private static bool HasPositiveRequirement(Research research, string resourceId)
    {
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = research.ResourceRequirements[i];
            if (pair.First != null && pair.First.Id == resourceId &&
                pair.Second > ExpantaNum.Zero)
                return true;
        }
        return false;
    }

    [Test]
    public void 焦炭必须进入太空幽影材料制造的持续工艺()
    {
        Assert.That(HasBuildingRequirement("PhantomMaterialsFabricator", "Coke"), Is.True);
        Assert.That(
            HasBuildingConsumptionRate("PhantomMaterialsFabricator", "Coke", 0.06d),
            Is.True);
        Assert.That(HasWorkshopRequirement("PhantomWeaveLattice", "Coke"), Is.True);
        Assert.That(HasPositiveRequirement(DataBase<Research>.Find("PhantomMaterials"), "Coke"), Is.True);
    }

    private static bool HasBuildingRequirement(string buildingId, string resourceId)
    {
        Building building = DataBase<Building>.Find(buildingId);
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = building.ResourceRequirements[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return true;
        }
        return false;
    }

    private static bool HasGenerationRate(string buildingId, string resourceId, double amount)
    {
        Building building = DataBase<Building>.Find(buildingId);
        for (int i = 0; i < building.ResourceGenerationRates.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = building.ResourceGenerationRates[i];
            if (pair.First != null && pair.First.Id == resourceId &&
                pair.Second.ToDouble() == amount)
                return true;
        }

        return false;
    }

    private static bool HasCampaignCost(string sectorId, string resourceId, double amount)
    {
        SectorDefinition sector = DataBase<SectorDefinition>.Find(sectorId);
        for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = sector.CampaignResourceRatesPerSecond[i];
            if (pair.First != null && pair.First.Id == resourceId &&
                pair.Second.ToDouble() == amount)
                return true;
        }

        return false;
    }

    private static double GetBuildingAmount(Building building, string resourceId, bool consumption = false)
    {
        var pairs = consumption
            ? building.ResourceConsumptionRates
            : building.ResourceRequirements;
        Pair<Resource, ExpantaNum> pair = pairs.First(item => item.First != null && item.First.Id == resourceId);
        return pair.Second.ToDouble();
    }

    private static double GetResearchAmount(Research research, string resourceId)
    {
        Pair<Resource, ExpantaNum> pair = research.ResourceRequirements
            .First(item => item.First != null && item.First.Id == resourceId);
        return pair.Second.ToDouble();
    }

    private static double GetWorkshopAmount(WorkshopUpgrade workshop, string resourceId)
    {
        Pair<Resource, ExpantaNum> pair = workshop.ResourceRequirements
            .First(item => item.First != null && item.First.Id == resourceId);
        return pair.Second.ToDouble();
    }

    private static bool HasBuildingConsumption(string buildingId, string resourceId)
    {
        Building building = DataBase<Building>.Find(buildingId);
        for (int i = 0; i < building.ResourceConsumptionRates.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = building.ResourceConsumptionRates[i];
            if (pair.First != null && pair.First.Id == resourceId && pair.Second > ExpantaNum.Zero)
                return true;
        }

        return false;
    }

    private static bool HasBuildingConsumptionRate(string buildingId, string resourceId, double amount)
    {
        Building building = DataBase<Building>.Find(buildingId);
        if (building == null)
            return false;
        for (int i = 0; i < building.ResourceConsumptionRates.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = building.ResourceConsumptionRates[i];
            if (pair.First != null &&
                pair.First.Id == resourceId &&
                pair.Second.ToDouble() == amount)
                return true;
        }

        return false;
    }

    private static bool HasWorkshopRequirement(string workshopId, string resourceId)
    {
        WorkshopUpgrade workshop = DataBase<WorkshopUpgrade>.Find(workshopId);
        for (int i = 0; i < workshop.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = workshop.ResourceRequirements[i];
            if (pair.First != null && pair.First.Id == resourceId && pair.Second > ExpantaNum.Zero)
                return true;
        }

        return false;
    }

    private static bool HasBuildingGeneration(string buildingId, string resourceId)
    {
        Building building = DataBase<Building>.Find(buildingId);
        if (building == null)
            return false;
        for (int i = 0; i < building.ResourceGenerationRates.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = building.ResourceGenerationRates[i];
            if (pair.First != null && pair.First.Id == resourceId && pair.Second > ExpantaNum.Zero)
                return true;
        }
        return false;
    }

    private static bool HasLaterEraUse(string resourceId)
    {
        bool inBuilding = DataBase<Building>.All.Any(building =>
            building != null && building.TechLevel >= TechLevel.Spacer &&
            (HasResourcePair(building.ResourceRequirements, resourceId) ||
             HasResourcePair(building.ResourceConsumptionRates, resourceId)));
        bool inResearch = DataBase<Research>.All.Any(research =>
            research != null && research.TechLevel >= TechLevel.Spacer &&
            HasResourcePair(research.ResourceRequirements, resourceId));
        bool inWorkshop = DataBase<WorkshopUpgrade>.All.Any(workshop =>
            workshop != null && workshop.TechLevel >= TechLevel.Spacer &&
            HasResourcePair(workshop.ResourceRequirements, resourceId));
        return inBuilding && inResearch && inWorkshop;
    }

    private static bool HasResourcePair(
        System.Collections.Generic.IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        string resourceId)
    {
        if (pairs == null)
            return false;
        for (int i = 0; i < pairs.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = pairs[i];
            if (pair.First != null && pair.First.Id == resourceId && pair.Second > ExpantaNum.Zero)
                return true;
        }
        return false;
    }

    private static bool HasColonizationCost(string sectorId, string resourceId, double amount)
    {
        SectorDefinition sector = DataBase<SectorDefinition>.Find(sectorId);
        for (int i = 0; i < sector.ColonizationResourceRatesPerSecond.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = sector.ColonizationResourceRatesPerSecond[i];
            if (pair.First != null && pair.First.Id == resourceId && pair.Second.ToDouble() == amount)
                return true;
        }
        return false;
    }
}
