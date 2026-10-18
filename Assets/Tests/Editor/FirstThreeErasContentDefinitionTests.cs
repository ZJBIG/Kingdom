using NUnit.Framework;
using UnityEngine;

public class FirstThreeErasContentDefinitionTests
{
    [Test]
    public void HousingUpgradeChainIsDefined()
    {
        Building wood = FindBuilding("WoodHouse");
        Building stone = FindBuilding("StoneHouse");
        Building town = FindBuilding("TownHouse");
        Assert.That(wood, Is.Not.Null);
        Assert.That(stone, Is.Not.Null);
        Assert.That(town, Is.Not.Null);
        Assert.That(wood.UpgradeTo, Is.EqualTo(stone));
        Assert.That(stone.UpgradeTo, Is.EqualTo(town));
        Assert.That(town.UpgradeTo, Is.EqualTo(FindBuilding("IndustrialHabitationComplex")));
        Assert.DoesNotThrow(() =>
            BuildingManager.ValidateBuildingChains(
                Resources.LoadAll<Building>("Datas/Building")));
    }

    [Test]
    public void FirstThreeErasBuildingsHaveNoFoodConsumption()
    {
        Building[] buildings = Resources.LoadAll<Building>("Datas/Building");
        int checkedCount = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].TechLevel > TechLevel.Medieval) continue;
            checkedCount++;
            Assert.That(buildings[i].FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero), buildings[i].Id);
        }
        Assert.That(checkedCount, Is.GreaterThan(0));
    }

    private static Building FindBuilding(string id)
    {
        Building[] all = Resources.LoadAll<Building>("Datas/Building");
        for (int i = 0; i < all.Length; i++) if (all[i].Id == id) return all[i];
        return null;
    }

}
