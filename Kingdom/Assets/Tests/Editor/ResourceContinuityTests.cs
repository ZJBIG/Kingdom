using NUnit.Framework;

public sealed class ResourceContinuityTests
{
    [Test]
    public void MedievalTransition_ConsumesIronAndBronzeAsStrategicMaterials()
    {
        Research transition = DataBase<Research>.Find("SmithingRevolution");

        Assert.Multiple(() =>
        {
            Assert.That(HasRequirement(transition, "Iron", 200d), Is.True);
            Assert.That(HasRequirement(transition, "Bronze", 100d), Is.True);
        });
    }

    [Test]
    public void FoodAndMaterialChainsRetainTheirExistingLaterUses()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HasBuildingRequirement("Granary", "Pottery"), Is.True);
            Assert.That(HasBuildingRequirement("ScribeHut", "Cloth"), Is.True);
            Assert.That(HasBuildingRequirement("CopperSmelter", "StoneBrick_Marble"), Is.True);
            Assert.That(HasBuildingRequirement("IronSmelter", "StoneBrick_Marble"), Is.True);
        });
    }

    [Test]
    public void CampaignStrategicResourcesHaveIndustrialSourcesAndSinks()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HasGenerationRate("ChemicalPlant", "RocketFuel", 0.25d), Is.True);
            Assert.That(HasGenerationRate("MachineFactory", "Composite", 0.15d), Is.True);
            Assert.That(HasBuildingRequirement("LaunchCenter", "RocketFuel"), Is.True);
            Assert.That(HasBuildingRequirement("Shipyard", "Composite"), Is.True);
            Assert.That(HasCampaignCost("Moon", "RocketFuel", 1d), Is.True);
            Assert.That(HasCampaignCost("Mars", "Composite", 1d), Is.True);
        });
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
        for (int i = 0; i < sector.CampaignResourceCosts.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = sector.CampaignResourceCosts[i];
            if (pair.First != null && pair.First.Id == resourceId &&
                pair.Second.ToDouble() == amount)
                return true;
        }

        return false;
    }
}
