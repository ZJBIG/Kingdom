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
        Assert.That(town.UpgradeTo, Is.Null);
        Assert.DoesNotThrow(() =>
            BuildingManager.ValidateBuildingChains(
                Resources.LoadAll<Building>("Datas/Building")));
    }

    [Test]
    public void HousingResearchUnlocksAreContinuous()
    {
        Research masonry = DataBase<Research>.Find("Masonry");
        Research urbanHousing = DataBase<Research>.Find("UrbanHousing");

        Assert.That(masonry.Label, Is.EqualTo("砌筑技术"));
        Assert.That(
            urbanHousing.Prerequisites,
            Does.Contain(masonry));
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

    [Test]
    public void NewResearchHasDescriptionRequirementsAndEffects()
    {
        string[] ids =
        {
            "FoodPreservation", "HerbalKnowledge", "VillageOrganization", "OrganizedDefense",
            "Woodworking", "CeramicFiring", "CharcoalMaking", "AnimalFodder",
            "IrrigationEngineering", "VillageCrafts", "CropRotation", "CouncilGovernance",
            "OrganizedWatch", "Smithing_Bronze", "Smithing_Copper", "Smithing_Iron",
            "FoodStorage", "WrittenRecords", "UrbanHousing", "GuildSystem",
            "ScholasticInstitutions", "PublicHealth", "FeudalAdministration"
        };
        for (int i = 0; i < ids.Length; i++)
        {
            Research research = DataBase<Research>.Find(ids[i]);
            Assert.That(research, Is.Not.Null, ids[i]);
            Assert.That(research.Description, Is.Not.Empty, ids[i]);
            Assert.That(research.ResourceRequirements.Count, Is.GreaterThan(0), ids[i]);
            Assert.That(research.Effects.Count, Is.GreaterThan(0), ids[i]);
        }
    }

    private static Building FindBuilding(string id)
    {
        Building[] all = Resources.LoadAll<Building>("Datas/Building");
        for (int i = 0; i < all.Length; i++) if (all[i].Id == id) return all[i];
        return null;
    }

}
