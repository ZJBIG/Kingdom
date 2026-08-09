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
            ["Pasture"] = 1.15d,
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
            ["BronzeFoundry"] = 1.18d,
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
        Assert.That(steel.DisplayerSet, Is.EqualTo(Resource.Set.IngotSet));
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
        Building pasture = DataBase<Building>.Find("Pasture");
        Building lumberyard = DataBase<Building>.Find("Lumberyard");

        Assert.That(farm.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(farm.FoodProductionRate.ToDouble(), Is.EqualTo(8d));
        Assert.That(pasture.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(lumberyard.ProductivityConsumption.ToDouble(), Is.EqualTo(4d));
    }
}
