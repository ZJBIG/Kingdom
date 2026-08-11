using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public sealed class ResearchBalanceTests
{
    [Test]
    public void 已发布研究与工坊不得拥有空Effect()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            Assert.That(research, Is.Not.Null);
            Assert.That(research.Effects, Is.Not.Null.And.Not.Empty, research.Id);
            for (int i = 0; i < research.Effects.Count; i++)
                Assert.That(research.Effects[i], Is.Not.Null, research.Id);
        }

        foreach (WorkshopUpgrade workshop in DataBase<WorkshopUpgrade>.All)
        {
            Assert.That(workshop, Is.Not.Null);
            Assert.That(workshop.Effects, Is.Not.Null.And.Not.Empty, workshop.Id);
            for (int i = 0; i < workshop.Effects.Count; i++)
                Assert.That(workshop.Effects[i], Is.Not.Null, workshop.Id);
        }
    }

    [Test]
    public void 研究定义不再携带布局坐标或系统路由字段()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.That(typeof(Research).GetField("x", flags), Is.Null);
        Assert.That(typeof(Research).GetField("y", flags), Is.Null);
        Assert.That(typeof(Research).GetField("X", flags), Is.Null);
        Assert.That(typeof(Research).GetField("Y", flags), Is.Null);
        Assert.That(typeof(ResearchEffectDefinition).GetField("SystemID", flags), Is.Null);
        Assert.That(typeof(ResearchEffectDefinition).GetField("systemID", flags), Is.Null);
    }

    [Test]
    public void 通用机械化生产研究不得与军工体系重复机器工厂倍率()
    {
        Assert.That(DataBase<Research>.Find("MechanizedProduction"), Is.Null);

        Research militaryIndustry = DataBase<Research>.Find("MilitaryIndustry");
        bool hasMachineFactoryEffect = false;
        for (int i = 0; i < militaryIndustry.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = militaryIndustry.Effects[i];
            if (effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
                effect.Building != null && effect.Building.Id == "MachineFactory")
            {
                hasMachineFactoryEffect = true;
                break;
            }
        }

        Assert.That(hasMachineFactoryEffect, Is.True);
    }

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
        Assert.That(
            HasEffect(
                DataBase<Research>.Find("HerbalKnowledge"),
                ResearchEffectType.PopulationProductivityMultiplier,
                1.1d),
            Is.True);
        Assert.That(
            HasEffect(
                DataBase<Research>.Find("PublicHealth"),
                ResearchEffectType.PopulationProductivityMultiplier,
                1.25d),
            Is.True);
        Assert.That(
            HasEffect(
                DataBase<Research>.Find("ModernMedicine"),
                ResearchEffectType.PopulationProductivityMultiplier,
                1.35d),
            Is.True);
        Research precisionMedicine = DataBase<Research>.Find("PrecisionMedicine");
        Assert.That(precisionMedicine, Is.Not.Null);
        Assert.That(precisionMedicine.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(
            HasEffect(
                precisionMedicine,
                ResearchEffectType.PopulationProductivityMultiplier,
                1.5d),
            Is.True);
        Assert.That(HasResourceRequirementById(precisionMedicine, "Biomass"), Is.True);
        Assert.That(HasResourceRequirementById(precisionMedicine, "TitaniumAlloy"), Is.True);
    }

    [Test]
    public void 太空医学理论保留人口生产力效果而不依赖已删除工坊()
    {
        Research theory = DataBase<Research>.Find("PrecisionMedicine");
        WorkshopUpgrade implementation = DataBase<WorkshopUpgrade>.Find("RemoteSurgicalSystems");

        Assert.That(theory, Is.Not.Null);
        Assert.That(implementation, Is.Null);
        Assert.That(theory.Prerequisites, Has.Some.Property("Id").EqualTo("ModernMedicine"));
        Assert.That(theory.Prerequisites, Has.Some.Property("Id").EqualTo("BioregenerativeLifeSupport"));
        Assert.That(theory.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.PopulationProductivityMultiplier &&
            effect.Value == new ExpantaNum("1.5")));
    }

    [Test]
    public void 医学研究必须按时代递进提升人口劳动力()
    {
        Research herbalKnowledge = DataBase<Research>.Find("HerbalKnowledge");
        Research publicHealth = DataBase<Research>.Find("PublicHealth");
        Research modernMedicine = DataBase<Research>.Find("ModernMedicine");
        Research precisionMedicine = DataBase<Research>.Find("PrecisionMedicine");

        Assert.That(herbalKnowledge, Is.Not.Null);
        Assert.That(publicHealth, Is.Not.Null);
        Assert.That(modernMedicine, Is.Not.Null);
        Assert.That(precisionMedicine, Is.Not.Null);
        Assert.That(herbalKnowledge.TechLevel, Is.LessThan(publicHealth.TechLevel));
        Assert.That(publicHealth.TechLevel, Is.LessThan(modernMedicine.TechLevel));
        Assert.That(modernMedicine.TechLevel, Is.LessThan(precisionMedicine.TechLevel));
        Assert.That(
            GetPopulationProductivity(herbalKnowledge),
            Is.EqualTo(1.1d).Within(0.000001d));
        Assert.That(
            GetPopulationProductivity(publicHealth),
            Is.EqualTo(1.25d).Within(0.000001d));
        Assert.That(
            GetPopulationProductivity(modernMedicine),
            Is.EqualTo(1.35d).Within(0.000001d));
        Assert.That(
            GetPopulationProductivity(precisionMedicine),
            Is.EqualTo(1.5d).Within(0.000001d));
    }

    [Test]
    public void 医学研究成本与资源门槛必须随时代递进()
    {
        Research herbalKnowledge = DataBase<Research>.Find("HerbalKnowledge");
        Research publicHealth = DataBase<Research>.Find("PublicHealth");
        Research modernMedicine = DataBase<Research>.Find("ModernMedicine");
        Research lifeSupport = DataBase<Research>.Find("BioregenerativeLifeSupport");
        Research precisionMedicine = DataBase<Research>.Find("PrecisionMedicine");

        Assert.That(herbalKnowledge, Is.Not.Null);
        Assert.That(publicHealth, Is.Not.Null);
        Assert.That(modernMedicine, Is.Not.Null);
        Assert.That(lifeSupport, Is.Not.Null);
        Assert.That(precisionMedicine, Is.Not.Null);
        Assert.That(ExpantaNum.TryParse(herbalKnowledge.BaseCost, out ExpantaNum herbalCost), Is.True);
        Assert.That(ExpantaNum.TryParse(publicHealth.BaseCost, out ExpantaNum publicHealthCost), Is.True);
        Assert.That(ExpantaNum.TryParse(modernMedicine.BaseCost, out ExpantaNum modernCost), Is.True);
        Assert.That(ExpantaNum.TryParse(lifeSupport.BaseCost, out ExpantaNum lifeSupportCost), Is.True);
        Assert.That(ExpantaNum.TryParse(precisionMedicine.BaseCost, out ExpantaNum precisionCost), Is.True);
        Assert.That(publicHealthCost, Is.GreaterThan(herbalCost));
        Assert.That(modernCost, Is.GreaterThan(publicHealthCost));
        Assert.That(lifeSupportCost, Is.GreaterThan(modernCost));
        Assert.That(precisionCost, Is.GreaterThan(lifeSupportCost));
        Assert.That(HasResourceRequirementById(modernMedicine, "Ceramic"), Is.True);
        Assert.That(HasResourceRequirementById(modernMedicine, "Electronics"), Is.True);
        Assert.That(HasResourceRequirementById(lifeSupport, "Biomass"), Is.True);
        Assert.That(HasResourceRequirementById(lifeSupport, "PhaseMaterial"), Is.True);
        Assert.That(HasResourceRequirementById(precisionMedicine, "Biomass"), Is.True);
        Assert.That(HasResourceRequirementById(precisionMedicine, "TitaniumAlloy"), Is.True);
    }

    [Test]
    public void IndustrialResearchEffectsMatchTargetBuildingSystems()
    {
        Assert.That(HasEffect(
            DataBase<Research>.Find("CombustionEngines"),
            ResearchEffectType.BuildingLogisticsProductionMultiplier,
            1.25d,
            "RailHub"), Is.True);
        Assert.That(HasEffect(
            DataBase<Research>.Find("CombustionEngines"),
            ResearchEffectType.BuildingProductionMultiplier,
            1.12d,
            "MachineFactory"), Is.True);
        Assert.That(HasEffect(
            DataBase<Research>.Find("ElectricalCommunication"),
            ResearchEffectType.BuildingLogisticsProductionMultiplier,
            1.1d,
            "RailHub"), Is.True);
        Assert.That(HasEffect(
            DataBase<Research>.Find("ElectricalCommunication"),
            ResearchEffectType.BuildingResearchPowerMultiplier,
            1.05d,
            "University"), Is.True);
        Assert.That(HasEffect(
            DataBase<Research>.Find("PowerGridEngineering"),
            ResearchEffectType.BuildingPowerProductionMultiplier,
            1.1d,
            "CentralPowerStation"), Is.True);
        Assert.That(HasEffect(
            DataBase<Research>.Find("MechanicalEngineering"),
            ResearchEffectType.BuildingFoodProductionMultiplier,
            1.25d,
            "IrrigationWorks"), Is.True);
        Assert.That(HasEffect(
            DataBase<Research>.Find("IndustrialAgriculture"),
            ResearchEffectType.BuildingProductionMultiplier,
            2.4d,
            "PlantingField"), Is.True);
        Assert.That(HasResourceEffect(
            DataBase<Research>.Find("TitaniumAlloyEngineering"),
            ResearchEffectType.ResourceProductionMultiplier,
            1.15d,
            "TitaniumAlloy"), Is.True);
    }

    [Test]
    public void IntegratedFurnaces_ImprovesMetalSmelter()
    {
        WorkshopUpgrade upgrade =
            DataBase<WorkshopUpgrade>.Find("IntegratedFurnaces");
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
            ["ChemicalPlant"] = 70d,
            ["CokeOven"] = 60d,
            ["Glassworks"] = 60d,
            ["MachineFactory"] = 80d,
            ["OilDerrick"] = 50d,
            ["OilRefinery"] = 80d,
            ["RailHub"] = 70d,
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

    [Test]
    public void IndustrialFactoryResearchesHaveDistinctPlayerFacingRoles()
    {
        Research organization = DataBase<Research>.Find("FactoryOrganization");
        Research massProduction = DataBase<Research>.Find("MassProduction");
        Research precision = DataBase<Research>.Find("PrecisionManufacturing");

        Assert.That(organization.Description, Does.Contain("工业行政体系"));
        Assert.That(organization.Description, Does.Contain("建设效率"));
        Assert.That(massProduction.Description, Does.Contain("批量生产速度"));
        Assert.That(precision.Description, Does.Contain("机器工厂"));
        Assert.That(HasEffect(organization, ResearchEffectType.GlobalBuildingProductionMultiplier, 1.21d), Is.False);
        Assert.That(HasEffect(organization, ResearchEffectType.GlobalConstructionMultiplier, 1.2d), Is.True);
        Assert.That(HasEffect(massProduction, ResearchEffectType.GlobalBuildingProductionMultiplier, 1.4375d), Is.True);
        Assert.That(HasEffect(precision, ResearchEffectType.BuildingProductionMultiplier, 1.12d, "MachineFactory"), Is.True);
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

    private static double GetPopulationProductivity(Research research)
    {
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Type == ResearchEffectType.PopulationProductivityMultiplier)
                return effect.Value.ToDouble();
        }

        return 0d;
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

    private static bool HasResourceEffect(
        Research research,
        ResearchEffectType type,
        double value,
        string resourceId)
    {
        if (research == null)
            return false;
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Type == type &&
                effect.Value.ToDouble() == value &&
                effect.Resource != null && effect.Resource.Id == resourceId)
                return true;
        }
        return false;
    }

    private static bool HasResourceRequirementById(Research research, string resourceId)
    {
        if (research == null)
            return false;
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = research.ResourceRequirements[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return pair.Second > ExpantaNum.Zero;
        }
        return false;
    }
}
