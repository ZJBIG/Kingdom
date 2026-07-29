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
            Is.EqualTo(400d).Within(0.000001d));

        Building knowledge = DataBase<Building>.Find("KnowledgeCircle");
        Assert.That(knowledge.RequiredResearch.Select(x => x.Id),
            Is.EquivalentTo(new[] { "ControlledFire" }));
        Assert.That(knowledge.CostGrowth.ToDouble(),
            Is.InRange(1.20d, 1.22d));
    }

    [Test]
    public void CriticalNeolithicFlows_HaveConfiguredTwentyPercentHeadroom()
    {
        AssertRate("Quarry", "StoneChunk", 2.4d);
        AssertRate("FiberGatheringCamp", "PlantFiber", 1.2d);
        AssertRate("CopperMine", "CopperOre", 3d);
        AssertRate("TinMine", "TinOre", 3d);
        AssertRate("IronMine", "IronOre", 3.6d);
        AssertRate("CopperSmelter", "Copper", 1.2d);
        AssertRate("BronzeFoundry", "Bronze", 1.2d);
    }

    [Test]
    public void IronMine_DoesNotRequireItsOwnOutput()
    {
        Building mine = DataBase<Building>.Find("IronMine");
        Assert.That(
            mine.ResourceRequirements.Any(pair => pair.First.Id == "IronOre"),
            Is.False);
        Assert.That(
            mine.ResourceRequirements.Any(pair => pair.First.Id == "Bronze"),
            Is.True);
    }

    [Test]
    public void BronzeFoundry_DoesNotRequireItsOwnOutput()
    {
        Building foundry = DataBase<Building>.Find("BronzeFoundry");
        Assert.That(
            foundry.ResourceRequirements.Any(pair => pair.First.Id == "Bronze"),
            Is.False);
        Assert.That(
            foundry.ResourceRequirements.Select(pair => pair.First.Id),
            Does.Contain("Copper").And.Contain("Tin"));
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
            Is.EqualTo(270000d));
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
