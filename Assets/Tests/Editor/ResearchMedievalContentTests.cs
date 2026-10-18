using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class ResearchMedievalContentTests
{
    [Test]
    public void C503_MedievalBuildingsOwnTheirResearchPrerequisites()
    {
        var expectedUnlocks = new Dictionary<string, string[]>
        {
            ["ScholasticInstitutions"] = new[] { "Academy" },
            ["Steelmaking"] = new[] { "SteelForge" },
            ["Bookmaking"] = new[] { "Library" },
            ["TradeRoutes"] = new[] { "Caravanserai" },
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
        Assert.That(industrialization.Prerequisites, Has.Count.EqualTo(2));
        Assert.That(
            industrialization.Prerequisites.Select(research => research.Id),
            Is.EquivalentTo(new[] { "MechanicalEngineering", "Steelmaking" }));
        Assert.That(industrialization.Effects, Has.Count.EqualTo(3));
        Assert.That(
            industrialization.Effects,
            Has.Some.Matches<ResearchEffectDefinition>(effect =>
                effect.Type == ResearchEffectType.ProductivityGranted &&
                effect.NumericValue == new ExpantaNum(800)));
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
