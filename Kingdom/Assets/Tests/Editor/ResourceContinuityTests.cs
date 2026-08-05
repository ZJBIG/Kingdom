using NUnit.Framework;

public sealed class ResourceContinuityTests
{
    [Test]
    public void MedievalTransition_ConsumesIronAndBronzeAsStrategicMaterials()
    {
        Research transition = DataBase<Research>.Find("SmithingRevolution");

        Assert.That(HasPositiveRequirement(transition, "Iron"), Is.True);
        Assert.That(HasPositiveRequirement(transition, "Bronze"), Is.True);
    }

    [Test]
    public void FoodAndMaterialChainsRetainTheirExistingLaterUses()
    {
        Assert.That(HasBuildingRequirement("Granary", "Pottery"), Is.True);
        Assert.That(HasBuildingRequirement("ScribeHut", "Cloth"), Is.True);
        Assert.That(HasBuildingRequirement("CopperSmelter", "StoneBrick"), Is.True);
        Assert.That(HasBuildingRequirement("IronSmelter", "StoneBrick"), Is.True);
    }

    [Test]
    public void UnpublishedSpaceResourcesDoNotLeakIntoIndustrialProduction()
    {
        Assert.That(HasGenerationRate("ChemicalPlant", "RocketFuel", 0.25d), Is.False);
        Assert.That(HasGenerationRate("MachineFactory", "Composite", 0.15d), Is.False);
        Assert.That(HasBuildingRequirement("LaunchCenter", "RocketFuel"), Is.True);
        Assert.That(HasBuildingRequirement("Shipyard", "Composite"), Is.True);
        Assert.That(HasColonizationCost("Moon", "RocketFuel", 1d), Is.True);
        Assert.That(HasColonizationCost("Mars", "Composite", 1d), Is.True);
        Assert.That(HasCampaignCost("AlphaCentauri", "RocketFuel", 5d), Is.True);
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

    private static bool HasColonizationCost(string sectorId, string resourceId, double amount)
    {
        SectorDefinition sector = DataBase<SectorDefinition>.Find(sectorId);
        for (int i = 0; i < sector.ColonizationResourceCosts.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = sector.ColonizationResourceCosts[i];
            if (pair.First != null && pair.First.Id == resourceId && pair.Second.ToDouble() == amount)
                return true;
        }
        return false;
    }
}
