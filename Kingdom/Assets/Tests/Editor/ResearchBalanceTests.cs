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
            ["StoneCutting"] = 900d,
            ["Mathematics"] = 1000d,
            ["Calendar"] = 1400d,
            ["KnowledgeSharing"] = 1500d,
            ["Ceramic"] = 1200d,
            ["TextileCraft"] = 1400d,
            ["CoalMining"] = 1800d,
            ["NeolithicSettlement"] = 4400d,
            ["Measurement"] = 3200d,
            ["FoodStorage"] = 3500d,
            ["WrittenRecords"] = 4500d,
            ["WaterManagement"] = 5500d,
            ["Masonry"] = 6500d,
            ["Smithing"] = 8000d,
            ["Smithing_Copper"] = 10000d,
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
        Research controlledFire = DataBase<Research>.Find("ControlledFire");
        Research foragingGroups = DataBase<Research>.Find("ForagingGroups");
        Research mining = DataBase<Research>.Find("Mining");
        Research measurement = DataBase<Research>.Find("Measurement");
        Research smithing = DataBase<Research>.Find("Smithing");
        Research waterManagement = DataBase<Research>.Find("WaterManagement");

        Assert.That(HasEffect(controlledFire, ResearchEffectType.BuildingFoodProductionMultiplier, 1.1d), Is.True);
        Assert.That(HasEffect(mining, ResearchEffectType.BuildingProductionMultiplier, 1.15d), Is.True);
        Assert.That(HasEffect(mathematics, ResearchEffectType.GlobalResearchMultiplier, 1.25d), Is.True);
        Assert.That(HasEffect(calendar, ResearchEffectType.BuildingFoodProductionMultiplier, 1.1d), Is.True);
        Assert.That(HasEffect(measurement, ResearchEffectType.GlobalConstructionMultiplier, 1.1d), Is.True);
        Assert.That(
            HasEffect(
                smithing,
                ResearchEffectType.BuildingProductionMultiplier,
                1.1d,
                "MetalSmelter"),
            Is.True);
        Assert.That(HasEffect(waterManagement, ResearchEffectType.BuildingFoodProductionMultiplier, 1.5d), Is.True);
        Building knowledgeCircle = DataBase<Building>.Find("KnowledgeCircle");
        Assert.That(knowledgeCircle.RequiredResearch, Is.EquivalentTo(new[] { controlledFire }));
        Assert.That(knowledgeCircle.ResourceRequirements.Count, Is.EqualTo(1));
        Assert.That(knowledgeCircle.ResourceRequirements[0].First.Id, Is.EqualTo("WoodLog"));
        Assert.That(
            knowledgeCircle.ResourceRequirements[0].Second.ToDouble(),
            Is.EqualTo(50d).Within(0.000001d));
        Assert.That(
            knowledgeCircle.ResearchPowerGranted.ToDouble(),
            Is.EqualTo(1d).Within(0.000001d));
        Assert.That(
            knowledgeCircle.ProductivityConsumption.ToDouble(),
            Is.EqualTo(1d).Within(0.000001d),
            "The first knowledge building must leave two workers for food recovery.");
        Assert.That(
            HasEffect(
                knowledgeSharing,
                ResearchEffectType.GlobalResearchMultiplier,
                1.15d),
            Is.True);
        Building hunterGatherer = DataBase<Building>.Find("HunterGathererCamp");
        Assert.That(
            hunterGatherer.RequiredResearch,
            Is.EquivalentTo(new[] { controlledFire }));
        Assert.That(
            HasEffect(
                foragingGroups,
                ResearchEffectType.BuildingFoodProductionMultiplier,
                1.25d,
                "HunterGathererCamp"),
            Is.True);
    }

    [Test]
    public void PopulationGrowthResearch_UsesTheStagedMultipliers()
    {
        Assert.That(
            HasEffect(
                DataBase<Research>.Find("Agriculture"),
                ResearchEffectType.PopulationGrowthMultiplier,
                1.2d),
            Is.True);
        Assert.That(
            HasEffect(
                DataBase<Research>.Find("NeolithicSettlement"),
                ResearchEffectType.PopulationGrowthMultiplier,
                1.25d),
            Is.True);
        Assert.That(
            HasEffect(
                DataBase<Research>.Find("CropRotation"),
                ResearchEffectType.PopulationGrowthMultiplier,
                1.15d),
            Is.True);
        Assert.That(
            HasEffect(
                DataBase<Research>.Find("PublicHealth"),
                ResearchEffectType.PopulationGrowthMultiplier,
                1.4d),
            Is.True);
    }

    [Test]
    public void IntegratedFurnaces_ImprovesMetalSmelter()
    {
        WorkshopUpgradeDefinition upgrade =
            DataBase<WorkshopUpgradeDefinition>.Find("IntegratedFurnaces");
        bool found = false;
        for (int i = 0; i < upgrade.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = upgrade.Effects[i];
            if (effect != null &&
                effect.Type == WorkshopEffectType.BuildingProductionMultiplier &&
                effect.Building != null &&
                effect.Building.Id == "MetalSmelter" &&
                effect.Value.ToDouble() >= 1.25d)
            {
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True, "一体化冶炉必须提升多金属冶炼炉产出。");
    }

    [Test]
    public void LaterBuildings_UseTheProductivityRebalance()
    {
        var expected = new Dictionary<string, double>
        {
            ["Academy"] = 60d,
            ["Library"] = 36d,
            ["Market"] = 30d,
            ["Observatory"] = 72d,
            ["PrintingHouse"] = 72d,
            ["SteelForge"] = 48d,
            ["WaterMill"] = 30d,
            ["Caravanserai"] = 10d,
            ["GuildHall"] = 30d,
            ["Hospital"] = 25d,
            ["RoyalWorkshop"] = 45d,
            ["ChemicalPlant"] = 70d,
            ["CokeOven"] = 60d,
            ["Glassworks"] = 60d,
            ["MachineFactory"] = 80d,
            ["OilDerrick"] = 50d,
            ["OilRefinery"] = 80d,
            ["RailHub"] = 70d,
            ["SilicaQuarry"] = 50d,
            ["SteamPlant"] = 60d,
            ["University"] = 60d,
            ["WireMill"] = 70d
        };

        foreach (KeyValuePair<string, double> item in expected)
        {
            Assert.That(
                DataBase<Building>.Find(item.Key).ProductivityConsumption.ToDouble(),
                Is.EqualTo(item.Value).Within(0.000001d),
                $"Building '{item.Key}' productivity demand drifted.");
        }

        Assert.That(
            DataBase<Building>.Find("TownHouse").ProductivityConsumption,
            Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void PublishedResearch_HasAConcreteUnlockEffectOrEraTransition()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            if (research.TechLevel > TechLevel.Medieval)
                continue;

            bool hasConcreteOutcome =
                research.AdvancesTechLevel ||
                IsRequiredByBuilding(research) ||
                research.Effects.Count > 0;
            Assert.That(
                hasConcreteOutcome,
                Is.True,
                $"Published research '{research.Id}' is a prerequisite-only node with no gameplay effect.");
        }
    }

    private static bool IsRequiredByBuilding(Research research)
    {
        foreach (Building building in DataBase<Building>.All)
            for (int i = 0; i < building.RequiredResearch.Count; i++)
                if (building.RequiredResearch[i] == research)
                    return true;
        return false;
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

    private static bool HasEffect(
        Research research,
        ResearchEffectType type,
        double value,
        string buildingId)
    {
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Type == type &&
                effect.Value.ToDouble() == value &&
                effect.Building != null &&
                effect.Building.Id == buildingId)
                return true;
        }
        return false;
    }
}
