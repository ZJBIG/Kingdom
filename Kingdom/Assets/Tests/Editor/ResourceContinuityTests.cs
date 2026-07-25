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
}
