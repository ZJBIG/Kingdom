using System.Collections.Generic;
using NUnit.Framework;

public sealed class BuildingVerticalSliceTests
{
    [Test]
    public void BuildingCostGrowth_MatchesTheVerticalSliceBaseline()
    {
        var expected = new Dictionary<string, double>
        {
            ["HunterGathererCamp"] = 1.15d,
            ["Farm"] = 1.14d,
            ["Lumberyard"] = 1.13d,
            ["Quarry"] = 1.14d,
            ["ClayPit"] = 1.14d,
            ["FiberGatheringCamp"] = 1.14d,
            ["StoneCuttingWorkshop"] = 1.15d,
            ["WoodHouse"] = 1.18d,
            ["KnowledgeCircle"] = 1.20d,
            ["CoalMine"] = 1.14d,
            ["MetalMine"] = 1.15d,
            ["CeramicKiln"] = 1.16d,
            ["WeavingWorkshop"] = 1.16d,
            ["MetalSmelter"] = 1.17d,
            ["Granary"] = 1.18d,
            ["ScribeHut"] = 1.20d
        };

        foreach (KeyValuePair<string, double> pair in expected)
        {
            Building building = DataBase<Building>.Find(pair.Key);
            Assert.That(building.CostGrowth.ToDouble(), Is.EqualTo(pair.Value).Within(0.000001d),
                $"Building '{pair.Key}' has drifted from the vertical-slice cost baseline.");
        }
    }

    [Test]
    public void FoodCapacity_IsLimitedToThePlannedFoodBuildings()
    {
        Assert.That(DataBase<Building>.Find("Granary").FoodCapacityGranted.ToDouble(), Is.EqualTo(1000d));
        Assert.That(DataBase<Building>.Find("CeramicKiln").FoodCapacityGranted.ToDouble(), Is.EqualTo(250d));
        Assert.That(DataBase<Building>.Find("WoodHouse").FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("KnowledgeCircle").FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void WoodHouse_ProvidesPopulationCapacityInsteadOfProductivity()
    {
        Building woodHouse = DataBase<Building>.Find("WoodHouse");
        Assert.That(woodHouse.ProductivityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(woodHouse.PopulationCapacityGranted, Is.EqualTo(new ExpantaNum(5)));
    }

    [Test]
    public void WoodHouse_IsTheZeroProductivityCostStartingPopulationBootstrap()
    {
        Building woodHouse = DataBase<Building>.Find("WoodHouse");
        Assert.That(woodHouse.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(woodHouse.ProductivityConsumption, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(woodHouse.RequiredResearch, Is.Empty);
        Assert.That(woodHouse.RequiredWorkshopUpgrades, Is.Empty);
    }

    [Test]
    public void MedievalResourceLayerContainsSteelWithoutAbstractToolStocks()
    {
        Resource steel = DataBase<Resource>.Find("Steel");
        Assert.That(steel, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(steel.Label), Is.False);
        Assert.That(DataBase<Resource>.Contains("MetalTool"), Is.False);
        Assert.That(DataBase<Resource>.Contains("StoneTool"), Is.False);
    }

    [Test]
    public void MedievalBuildingLayerContainsTheSixCoreBuildings()
    {
        string[] ids =
        {
            "WaterMill", "SteelForge", "Library", "Market"
        };

        foreach (string id in ids)
        {
            Building building = DataBase<Building>.Find(id);
            Assert.That(building, Is.Not.Null, $"Missing medieval building '{id}'.");
            Assert.That(building.TechLevel, Is.EqualTo(TechLevel.Medieval));
            Assert.That(building.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));
        }

        Assert.That(DataBase<Building>.Contains("Blacksmith"), Is.False);
        Assert.That(DataBase<Building>.Find("WaterMill").FoodProductionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("SteelForge").ResourceGenerationRates, Has.Count.EqualTo(1));
        Assert.That(DataBase<Building>.Find("Library").ResearchPowerGranted, Is.EqualTo(new ExpantaNum(25)));
    }

    [Test]
    public void KeyBuildings_MatchTheProductionAndEraPlan()
    {
        Building farm = DataBase<Building>.Find("Farm");
        Building lumberyard = DataBase<Building>.Find("Lumberyard");

        Assert.That(farm.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(farm.FoodProductionRate.ToDouble(), Is.EqualTo(8d));
        Assert.That(lumberyard.ProductivityConsumption.ToDouble(), Is.EqualTo(4d));
    }

    [Test]
    public void HousingChainScalesCapacityWithoutCreatingFoodCapacity()
    {
        Building woodHouse = DataBase<Building>.Find("WoodHouse");
        Building stoneHouse = DataBase<Building>.Find("StoneHouse");
        Building townHouse = DataBase<Building>.Find("TownHouse");
        Building industrialHousing = DataBase<Building>.Find("IndustrialHabitationComplex");
        Building orbitalHousing = DataBase<Building>.Find("OrbitalHabitatMegastructure");

        Assert.That(woodHouse, Is.Not.Null);
        Assert.That(stoneHouse, Is.Not.Null);
        Assert.That(townHouse, Is.Not.Null);
        Assert.That(industrialHousing, Is.Not.Null);
        Assert.That(orbitalHousing, Is.Not.Null);
        Assert.That(woodHouse.UpgradeTo, Is.SameAs(stoneHouse));
        Assert.That(stoneHouse.UpgradeTo, Is.SameAs(townHouse));
        Assert.That(townHouse.UpgradeTo, Is.SameAs(industrialHousing));
        Assert.That(industrialHousing.UpgradeTo, Is.SameAs(orbitalHousing));
        Assert.That(woodHouse.PopulationCapacityGranted,
            Is.LessThan(stoneHouse.PopulationCapacityGranted));
        Assert.That(stoneHouse.PopulationCapacityGranted,
            Is.LessThan(townHouse.PopulationCapacityGranted));
        Assert.That(townHouse.PopulationCapacityGranted,
            Is.LessThan(industrialHousing.PopulationCapacityGranted));
        Assert.That(industrialHousing.PopulationCapacityGranted,
            Is.LessThan(orbitalHousing.PopulationCapacityGranted));
        Assert.That(orbitalHousing.PopulationCapacityGranted,
            Is.GreaterThan(industrialHousing.PopulationCapacityGranted * 10d));
        Assert.That(woodHouse.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(stoneHouse.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(townHouse.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(industrialHousing.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(orbitalHousing.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(industrialHousing.ProductivityConsumption,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(orbitalHousing.ProductivityConsumption,
            Is.GreaterThan(industrialHousing.ProductivityConsumption));
    }

    [Test]
    public void IndustrialAndSpacerHaveDedicatedHousingBuildings()
    {
        Building industrial = DataBase<Building>.Find("IndustrialHabitationComplex");
        Building orbital = DataBase<Building>.Find("OrbitalHabitatMegastructure");

        Assert.That(industrial, Is.Not.Null);
        Assert.That(industrial.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(industrial.PopulationCapacityGranted, Is.EqualTo(new ExpantaNum(240)));
        Assert.That(industrial.ResourceConsumptionRates, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(orbital, Is.Not.Null);
        Assert.That(orbital.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(orbital.PopulationCapacityGranted, Is.EqualTo(new ExpantaNum(3000)));
        Assert.That(orbital.SpaceCost, Is.EqualTo(new ExpantaNum(650)));
        Assert.That(orbital.ResourceRequirements, Has.Count.GreaterThanOrEqualTo(7));
        Assert.That(industrial.UpgradeTo, Is.SameAs(orbital));
    }

    [Test]
    public void 工业住宅必须区分居住理论实体工坊与建筑落地()
    {
        Research theory = DataBase<Research>.Find("IndustrialHabitationEngineering");
        WorkshopUpgrade standards = DataBase<WorkshopUpgrade>.Find("IndustrialHousingStandards");
        Building housing = DataBase<Building>.Find("IndustrialHabitationComplex");

        Assert.That(theory, Is.Not.Null);
        Assert.That(standards, Is.Not.Null);
        Assert.That(housing, Is.Not.Null);
        Assert.That(HasResearchPrerequisite(theory, "UrbanHousing"), Is.True);
        Assert.That(HasResearchPrerequisite(theory, "ConcreteEngineering"), Is.True);
        Assert.That(HasResearchEffect(theory, ResearchEffectType.PopulationGrowthMultiplier), Is.True);
        Assert.That(HasWorkshopResearch(standards, "IndustrialHabitationEngineering"), Is.True);
        Assert.That(HasBuildingResearch(housing, "IndustrialHabitationEngineering"), Is.True);
    }

    [Test]
    public void 太空住宅必须同时承担人口容量与持续运营成本()
    {
        Building housing = DataBase<Building>.Find("OrbitalHabitatMegastructure");

        Assert.That(housing, Is.Not.Null);
        Assert.That(housing.PopulationCapacityGranted, Is.GreaterThan(new ExpantaNum(1000)));
        Assert.That(housing.FoodConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(housing.PowerConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(housing.LogisticsConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(housing.ResourceConsumptionRates, Has.Count.GreaterThanOrEqualTo(5));
        bool consumesTitaniumAlloy = false;
        for (int i = 0; i < housing.ResourceConsumptionRates.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = housing.ResourceConsumptionRates[i];
            if (pair.First != null && pair.First.Id == "TitaniumAlloy")
            {
                consumesTitaniumAlloy = true;
                break;
            }
        }

        Assert.That(consumesTitaniumAlloy, Is.True);
    }

    [Test]
    public void 太空住宅维护剖面必须由生态补给主导且不增加普通资源容量()
    {
        Building industrial = DataBase<Building>.Find("IndustrialHabitationComplex");
        Building orbital = DataBase<Building>.Find("OrbitalHabitatMegastructure");

        Assert.That(industrial, Is.Not.Null);
        Assert.That(orbital, Is.Not.Null);
        Assert.That(
            orbital.PopulationCapacityGranted,
            Is.GreaterThan(industrial.PopulationCapacityGranted * 10d));
        Assert.That(orbital.SpaceCost, Is.GreaterThan(industrial.SpaceCost * 10d));
        Assert.That(
            orbital.ProductivityConsumption,
            Is.GreaterThan(industrial.ProductivityConsumption * 4d));
        Assert.That(orbital.FoodConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(orbital.FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(orbital.PowerConsumptionRate, Is.GreaterThan(industrial.PowerConsumptionRate));
        Assert.That(orbital.LogisticsConsumptionRate, Is.GreaterThan(industrial.LogisticsConsumptionRate));
        Assert.That(
            GetResourceRate(orbital, "Biomass"),
            Is.GreaterThan(GetResourceRate(orbital, "TitaniumAlloy") * 20d));
    }

    [Test]
    public void 工业与太空住宅必须使用符合时代的建造材料()
    {
        Building industrial = DataBase<Building>.Find("IndustrialHabitationComplex");
        Building orbital = DataBase<Building>.Find("OrbitalHabitatMegastructure");

        Assert.That(industrial, Is.Not.Null);
        Assert.That(orbital, Is.Not.Null);
        Assert.That(HasBuildingRequirement(industrial, "Steel"), Is.True);
        Assert.That(HasBuildingRequirement(industrial, "Concrete"), Is.True);
        Assert.That(HasBuildingRequirement(industrial, "Glass"), Is.True);
        Assert.That(HasBuildingRequirement(orbital, "TitaniumAlloy"), Is.True);
        Assert.That(HasBuildingRequirement(orbital, "Composite"), Is.True);
        Assert.That(HasBuildingRequirement(orbital, "Biomass"), Is.True);
        Assert.That(HasBuildingRequirement(orbital, "PhaseMaterial"), Is.True);
        Assert.That(orbital.ResourceRequirements.Count,
            Is.GreaterThan(industrial.ResourceRequirements.Count));
    }

    private static bool HasResearchPrerequisite(Research research, string id)
    {
        for (int i = 0; i < research.Prerequisites.Count; i++)
            if (research.Prerequisites[i] != null && research.Prerequisites[i].Id == id)
                return true;
        return false;
    }

    private static bool HasResearchEffect(Research research, ResearchEffectType type)
    {
        for (int i = 0; i < research.Effects.Count; i++)
            if (research.Effects[i] != null && research.Effects[i].Type == type)
                return true;
        return false;
    }

    private static bool HasWorkshopResearch(WorkshopUpgrade workshop, string id)
    {
        for (int i = 0; i < workshop.RequiredResearch.Count; i++)
            if (workshop.RequiredResearch[i] != null && workshop.RequiredResearch[i].Id == id)
                return true;
        return false;
    }

    private static bool HasBuildingResearch(Building building, string id)
    {
        for (int i = 0; i < building.RequiredResearch.Count; i++)
            if (building.RequiredResearch[i] != null && building.RequiredResearch[i].Id == id)
                return true;
        return false;
    }

    private static bool HasBuildingRequirement(Building building, string id)
    {
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = building.ResourceRequirements[i];
            if (pair.First != null && pair.First.Id == id && pair.Second > ExpantaNum.Zero)
                return true;
        }

        return false;
    }

    private static ExpantaNum GetResourceRate(Building building, string resourceId)
    {
        for (int i = 0; i < building.ResourceConsumptionRates.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = building.ResourceConsumptionRates[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return pair.Second;
        }

        return ExpantaNum.Zero;
    }
}
