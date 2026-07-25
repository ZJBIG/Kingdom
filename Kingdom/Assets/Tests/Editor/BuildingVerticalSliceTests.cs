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
            ["StoneToolWorkshop"] = 1.16d,
            ["StoneCuttingWorkshop_Marble"] = 1.15d,
            ["WoodHouse"] = 1.18d,
            ["KnowledgeCircle"] = 1.2d,
            ["CoalMine"] = 1.14d,
            ["CopperMine"] = 1.15d,
            ["TinMine"] = 1.15d,
            ["IronMine"] = 1.15d,
            ["PotteryKiln"] = 1.16d,
            ["WeavingWorkshop"] = 1.16d,
            ["CopperSmelter"] = 1.17d,
            ["TinSmelter"] = 1.17d,
            ["IronSmelter"] = 1.17d,
            ["BronzeFoundry"] = 1.18d,
            ["Granary"] = 1.18d,
            ["ScribeHut"] = 1.2d
        };

        foreach (KeyValuePair<string, double> pair in expected)
        {
            Building building = DataBase<Building>.Find(pair.Key);
            Assert.That(
                building.CostGrowth.ToDouble(),
                Is.EqualTo(pair.Value).Within(0.000001d),
                $"Building '{pair.Key}' has drifted from the vertical-slice cost baseline.");
        }
    }

    [Test]
    public void FoodCapacity_IsLimitedToThePlannedFoodBuildings()
    {
        Assert.That(DataBase<Building>.Find("Granary").FoodCapacityGranted.ToDouble(), Is.EqualTo(1000d));
        Assert.That(DataBase<Building>.Find("PotteryKiln").FoodCapacityGranted.ToDouble(), Is.EqualTo(250d));
        Assert.That(DataBase<Building>.Find("WoodHouse").FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("KnowledgeCircle").FoodCapacityGranted, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void KeyBuildings_MatchTheProductionAndEraPlan()
    {
        Building farm = DataBase<Building>.Find("Farm");
        Building pasture = DataBase<Building>.Find("Pasture");
        Building lumberyard = DataBase<Building>.Find("Lumberyard");

        Assert.Multiple(() =>
        {
            Assert.That(farm.TechLevel, Is.EqualTo(TechLevel.Animal));
            Assert.That(farm.FoodProductionRate.ToDouble(), Is.EqualTo(8d));
            Assert.That(pasture.TechLevel, Is.EqualTo(TechLevel.Animal));
            Assert.That(lumberyard.ProductivityConsumption.ToDouble(), Is.EqualTo(4d));
        });
    }
}
