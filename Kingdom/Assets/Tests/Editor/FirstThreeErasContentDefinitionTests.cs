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
        Research permanentArchitecture =
            DataBase<Research>.Find("PermanentArchitecture");
        Research urbanHousing = DataBase<Research>.Find("UrbanHousing");

        Assert.That(permanentArchitecture.Label, Is.EqualTo("石制建筑"));
        Assert.That(
            urbanHousing.Prerequisites,
            Does.Contain(permanentArchitecture));
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
    public void NewResearchHasDescriptionRequirementsEffectsAndDefaultCoordinates()
    {
        string[] ids = { "FishingTechniques", "Woodworking", "HerbalKnowledge", "FoodPreservation", "VillageOrganization", "OrganizedDefense", "CharcoalMaking", "PermanentArchitecture", "AnimalFodder", "KilnEfficiency", "IrrigationEngineering", "VillageCrafts", "CropRotation", "CouncilGovernance", "OrganizedWatch", "BronzeImplements", "UrbanHousing", "GuildSystem", "ScholasticInstitutions", "MerchantAccounting", "ImprovedMilling", "PublicHealth", "Astronomy", "RoadEngineering", "MechanicalPrinting", "MetallurgicalStandards", "CastleArchitecture", "ArsenalOrganization" };
        for (int i = 0; i < ids.Length; i++)
        {
            Research research = Resources.Load<Research>("Datas/Research/" + FindEra(researchIds: ids[i]) + "/" + ids[i]);
            Assert.That(research, Is.Not.Null, ids[i]);
            Assert.That(research.Description, Is.Not.Empty, ids[i]);
            Assert.That(research.ResourceRequirements.Count, Is.GreaterThan(0), ids[i]);
            Assert.That(research.Effects.Count, Is.GreaterThan(0), ids[i]);
            Assert.That(research.x, Is.EqualTo(0f), ids[i]);
            Assert.That(research.y, Is.EqualTo(0f), ids[i]);
        }
    }

    private static Building FindBuilding(string id)
    {
        Building[] all = Resources.LoadAll<Building>("Datas/Building");
        for (int i = 0; i < all.Length; i++) if (all[i].Id == id) return all[i];
        return null;
    }

    private static string FindEra(string researchIds)
    {
        if (researchIds == "UrbanHousing" || researchIds == "GuildSystem" || researchIds == "ScholasticInstitutions" || researchIds == "MerchantAccounting" || researchIds == "ImprovedMilling" || researchIds == "PublicHealth" || researchIds == "Astronomy" || researchIds == "RoadEngineering" || researchIds == "MechanicalPrinting" || researchIds == "MetallurgicalStandards" || researchIds == "CastleArchitecture" || researchIds == "ArsenalOrganization") return "Medieval";
        if (researchIds == "CharcoalMaking" || researchIds == "PermanentArchitecture" || researchIds == "AnimalFodder" || researchIds == "KilnEfficiency" || researchIds == "IrrigationEngineering" || researchIds == "VillageCrafts" || researchIds == "CropRotation" || researchIds == "CouncilGovernance" || researchIds == "OrganizedWatch" || researchIds == "BronzeImplements") return "Neolithic";
        return "Animal";
    }
}
