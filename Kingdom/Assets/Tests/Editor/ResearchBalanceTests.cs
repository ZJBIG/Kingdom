using System.Collections.Generic;
using NUnit.Framework;

public sealed class ResearchBalanceTests
{
    [Test]
    public void EarlyResearchCosts_MatchTheContentBalanceBaseline()
    {
        var expectedCosts = new Dictionary<string, double>
        {
            ["ControlledFire"] = 60d,
            ["Quarry"] = 120d,
            ["Agriculture"] = 180d,
            ["AnimalHusbandry"] = 240d,
            ["ClayExtraction"] = 240d,
            ["StoneTools"] = 300d,
            ["ForagingGroups"] = 450d,
            ["Mining"] = 600d,
            ["TreeCultivate"] = 750d,
            ["Mining_Copper"] = 900d,
            ["StoneCutting"] = 900d,
            ["Mathematics"] = 1000d,
            ["Calendar"] = 1400d,
            ["KnowledgeSharing"] = 1500d,
            ["Pottery"] = 1200d,
            ["TextileCraft"] = 1400d,
            ["CoalMining"] = 1800d,
            ["NeolithicSettlement"] = 2500d,
            ["Measurement"] = 3200d,
            ["FoodStorage"] = 3500d,
            ["WrittenRecords"] = 4500d,
            ["WaterManagement"] = 5500d,
            ["Masonry"] = 6500d,
            ["Smithing"] = 8000d,
            ["Mining_Tin"] = 9000d,
            ["Smithing_Copper"] = 10000d,
            ["Mining_Iron"] = 12000d,
            ["Smithing_Tin"] = 12000d,
            ["Smithing_Iron"] = 15000d,
            ["Smithing_Bronze"] = 20000d,
            ["SmithingRevolution"] = 60000d
        };

        foreach (KeyValuePair<string, double> expected in expectedCosts)
        {
            Research research = DataBase<Research>.Find(expected.Key);
            Assert.That(
                ExpantaNum.TryParse(research.BaseCost, out ExpantaNum cost),
                Is.True,
                $"Research '{expected.Key}' has invalid BaseCost '{research.BaseCost}'.");
            Assert.That(
                cost.ToDouble(),
                Is.EqualTo(expected.Value).Within(0.000001d),
                $"Research '{expected.Key}' cost drifted from the pacing baseline.");
        }
    }

    [Test]
    public void MainResearchCosts_DoNotUseUnjustifiedExtremeNotation()
    {
        Research transition = DataBase<Research>.Find("SmithingRevolution");
        Assert.That(transition.BaseCost, Is.EqualTo("60000"));
        Assert.That(transition.TechLevel, Is.EqualTo(TechLevel.Medieval));
        Assert.That(transition.AdvancesTechLevel, Is.True);
    }

    [Test]
    public void VerticalSlice_ResearchEffectsAndUnlocksMatchTheContentPlan()
    {
        Research mathematics = DataBase<Research>.Find("Mathematics");
        Research calendar = DataBase<Research>.Find("Calendar");
        Research knowledgeSharing = DataBase<Research>.Find("KnowledgeSharing");
        Research measurement = DataBase<Research>.Find("Measurement");
        Research waterManagement = DataBase<Research>.Find("WaterManagement");

        Assert.That(mathematics.BuildingUnlock, Is.Empty);
        Assert.That(HasEffect(mathematics, ResearchEffectType.GlobalResearchMultiplier, 1.25d), Is.True);
        Assert.That(HasEffect(calendar, ResearchEffectType.BuildingFoodProductionMultiplier, 1.1d), Is.True);
        Assert.That(HasEffect(measurement, ResearchEffectType.GlobalConstructionMultiplier, 1.1d), Is.True);
        Assert.That(HasEffect(waterManagement, ResearchEffectType.BuildingFoodProductionMultiplier, 1.5d), Is.True);
        Assert.That(knowledgeSharing.BuildingUnlock, Has.Count.EqualTo(1));
        Assert.That(knowledgeSharing.BuildingUnlock[0].Id, Is.EqualTo("KnowledgeCircle"));
    }

    private static bool HasEffect(Research research, ResearchEffectType type, double value)
    {
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Type == type &&
                effect.Value.ToDouble() == value)
                return true;
        }
        return false;
    }
}
