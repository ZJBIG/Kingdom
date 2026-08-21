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
            Is.EqualTo(200d).Within(0.000001d));

        Building knowledge = DataBase<Building>.Find("KnowledgeCircle");
        Assert.That(knowledge.CostGrowth.ToDouble(),
            Is.InRange(1.20d, 1.22d));
    }

    [Test]
    public void CriticalNeolithicFlows_HaveConfiguredTwentyPercentHeadroom()
    {
        AssertRate("Quarry", "StoneChunk", 2.4d);
        AssertRate("FiberGatheringCamp", "Biomass", 1.5d);
        AssertRate("MetalMine", "CopperOre", 1d);
        AssertRate("MetalMine", "TinOre", .8d);
        AssertRate("MetalMine", "IronOre", .8d);
        AssertRate("MetalSmelter", "Copper", .8d);
        AssertRate("MetalSmelter", "Tin", .8d);
        AssertRate("MetalSmelter", "Iron", .8d);
        AssertRate("MetalSmelter", "Bronze", .6d);
    }

    [Test]
    public void MetalMine_DoesNotRequireItsOwnOutputs()
    {
        Building mine = DataBase<Building>.Find("MetalMine");
        Assert.That(
            mine.ResourceRequirements.Any(pair =>
                pair.First.Id == "CopperOre" ||
                pair.First.Id == "TinOre" ||
                pair.First.Id == "IronOre"),
            Is.False);
        Assert.That(
            mine.ResourceRequirements.Any(pair => pair.First.Id == "Bronze"),
            Is.False);
    }

    [Test]
    public void MetalSmelter_DoesNotRequireItsOwnOutput()
    {
        Building foundry = DataBase<Building>.Find("MetalSmelter");
        Assert.That(
            foundry.ResourceRequirements.Any(pair => pair.First.Id == "Bronze"),
            Is.False);
        Assert.That(
            foundry.ResourceRequirements.Select(pair => pair.First.Id),
            Does.Not.Contain("Copper").And.Not.Contain("Tin"));
    }

    [Test]
    public void IronSmithing_FollowsReachableBronzeChain()
    {
        Research iron = DataBase<Research>.Find("Smithing_Iron");
        Assert.That(
            iron.Prerequisites.Select(value => value.Id),
            Does.Contain("Smithing_Bronze"));
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
            Is.EqualTo(18144d));
        Assert.That(
            double.Parse(
                DataBase<Research>.Find("MechanicalEngineering").BaseCost,
                System.Globalization.CultureInfo.InvariantCulture),
            Is.EqualTo(13392d));
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
