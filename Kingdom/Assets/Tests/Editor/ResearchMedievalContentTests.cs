using System.Collections.Generic;
using NUnit.Framework;

public sealed class ResearchMedievalContentTests
{
    private static readonly string[] MedievalResearchIds =
    {
        "FeudalAdministration",
        "MechanicalEngineering",
        "Steelmaking",
        "Bookmaking",
        "TradeRoutes",
        "Fortification",
        "StandingArmy",
        "Gunpowder",
        "Industrialization"
    };

    [Test]
    public void C503_MedievalResearchLayerContainsTheNineCoreResearches()
    {
        for (int i = 0; i < MedievalResearchIds.Length; i++)
        {
            Research research = DataBase<Research>.Find(MedievalResearchIds[i]);
            Assert.That(research, Is.Not.Null, $"Missing medieval research '{MedievalResearchIds[i]}'.");
            Assert.That(research.TechLevel, Is.EqualTo(
                research.Id == "Industrialization" ? TechLevel.Industrial : TechLevel.Medieval));
            Assert.That(research.BaseCost, Is.Not.Null.And.Not.Empty);
            Assert.That(research.Prerequisites, Is.Not.Null);
            Assert.That(research.ResourceRequirements, Is.Not.Null);
        }
    }

    [Test]
    public void C503_MedievalBuildingsOwnTheirResearchPrerequisites()
    {
        var expectedUnlocks = new Dictionary<string, string[]>
        {
            ["MechanicalEngineering"] = new[] { "WaterMill" },
            ["Steelmaking"] = new[] { "SteelForge" },
            ["Bookmaking"] = new[] { "Library" },
            ["TradeRoutes"] = new[] { "Market" },
            ["Fortification"] = new[] { "Fortification" },
            ["StandingArmy"] = new[] { "Barracks" }
        };

        foreach (KeyValuePair<string, string[]> pair in expectedUnlocks)
        {
            for (int i = 0; i < pair.Value.Length; i++)
                Assert.That(
                    DataBase<Building>.Find(pair.Value[i]).RequiredResearch,
                    Does.Contain(DataBase<Research>.Find(pair.Key)));
        }
    }

    [Test]
    public void C503_IndustrializationIsTheOnlyIndustrialEraTransition()
    {
        Research industrialization = DataBase<Research>.Find("Industrialization");

        Assert.That(industrialization.TechLevel, Is.EqualTo(TechLevel.Industrial));
        Assert.That(industrialization.AdvancesTechLevel, Is.True);
        Assert.That(industrialization.Prerequisites, Has.Count.EqualTo(4));
        Assert.That(industrialization.Effects, Has.Count.EqualTo(3));
        Assert.That(
            industrialization.Effects,
            Has.Some.Matches<ResearchEffectDefinition>(effect =>
                effect.Type == ResearchEffectType.ProductivityGranted &&
                effect.Value == new ExpantaNum(800)));
    }

    [Test]
    public void C503_MedievalResearchDependenciesHaveNoCycles()
    {
        Assert.That(
            ResearchValidator.ValidateNoCycles(DataBase<Research>.All, out string error),
            Is.True,
            error);
    }
}
