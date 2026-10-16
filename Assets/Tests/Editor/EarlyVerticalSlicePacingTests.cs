using System.Linq;
using NUnit.Framework;

public sealed class EarlyVerticalSlicePacingTests
{
    [Test]
    public void FirstTenMinutes_UsesAffordableResearchAndKnowledgeCosts()
    {
        Research quarry = DataBase<Research>.Find("Quarry");
        Assert.That(quarry.ResourceRequirements.Count, Is.EqualTo(1));
        Assert.That(quarry.ResourceRequirements[0].First.Id, Is.EqualTo("WoodLog"));
        Assert.That(
            quarry.ResourceRequirements[0].Second.ToDouble(),
            Is.GreaterThan(0d));

        Building knowledge = DataBase<Building>.Find("KnowledgeCircle");
        Assert.That(knowledge.CostGrowth.ToDouble(),
            Is.InRange(1.20d, 1.22d));
    }

    [Test]
    public void CriticalStoneAgeFlows_PreserveCombinedMaterialSupply()
    {
        AssertRate("Quarry", "StoneChunk", 2.4d);
        AssertRate("FiberGatheringCamp", "Biomass", 1.5d);
        AssertRate("MetalMine", "BasicMetalOre", 2.6d);
        AssertRate("MetalSmelter", "BasicMetalIngot", 2.8d);
    }

    [Test]
    public void MetalMine_DoesNotRequireItsOwnOutputs()
    {
        Building mine = DataBase<Building>.Find("MetalMine");
        Assert.That(
            mine.ResourceRequirements.Any(pair =>
                pair.First.Id == "BasicMetalOre"),
            Is.False);
        Assert.That(
            mine.ResourceRequirements.Any(pair => pair.First.Id == "BasicMetalIngot"),
            Is.False);
    }

    [Test]
    public void MetalSmelter_DoesNotRequireItsOwnOutput()
    {
        Building foundry = DataBase<Building>.Find("MetalSmelter");
        Assert.That(
            foundry.ResourceRequirements.Select(pair => pair.First.Id),
            Does.Not.Contain("BasicMetalIngot"));
    }

    [Test]
    public void IronworkingAndSteelmaking_FollowsReachableBronzeChain()
    {
        Research iron = DataBase<Research>.Find("IronworkingAndSteelmaking");
        Assert.That(
            iron.Prerequisites.Select(value => value.Id),
            Does.Contain("BronzeAlloyCrafting"));
    }

    [Test]
    public void Industrialization_ClosesTheMedievalSliceWithinTargetBudget()
    {
        Assert.That(
            double.Parse(
                DataBase<Research>.Find("Industrialization").BaseCost,
                System.Globalization.CultureInfo.InvariantCulture),
            Is.EqualTo(336000d));
        Assert.That(
            double.Parse(
                DataBase<Research>.Find("Steelmaking").BaseCost,
                System.Globalization.CultureInfo.InvariantCulture),
            Is.EqualTo(181440d));
        Assert.That(
            double.Parse(
                DataBase<Research>.Find("MechanicalEngineering").BaseCost,
                System.Globalization.CultureInfo.InvariantCulture),
            Is.EqualTo(133920d));
    }

    private static void AssertRate(string buildingId, string resourceId, double expected)
    {
        Building building = DataBase<Building>.Find(buildingId);
        Pair<Resource, ExpantaNum> pair = building.ResourceGenerationRates
            .Single(value => value.First.Id == resourceId);
        Assert.That(
            pair.Second.ToDouble(),
            Is.EqualTo(expected).Within(0.000001d),
            $"{buildingId} must preserve the configured headroom for {resourceId}.");
    }
}
