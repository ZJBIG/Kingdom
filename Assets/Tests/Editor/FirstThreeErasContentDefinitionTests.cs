using NUnit.Framework;
using UnityEngine;

public class FirstThreeErasContentDefinitionTests
{
    [Test]
    public void FirstThreeErasBuildingsHaveNoFoodConsumption()
    {
        BuildingDef[] buildings = Resources.LoadAll<BuildingDef>("Datas/Building");
        int checkedCount = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].TechLevel > TechLevel.Medieval) continue;
            checkedCount++;
            Assert.That(buildings[i].FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero), buildings[i].Id);
        }
        Assert.That(checkedCount, Is.GreaterThan(0));
    }

}
