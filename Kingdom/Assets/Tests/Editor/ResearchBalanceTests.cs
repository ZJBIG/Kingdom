using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class ResearchBalanceTests
{
    [Test]
    public void 高级材料消耗必须先完成对应材料研究()
    {
        Research phantomMaterials = DataBase<Research>.Find("PhantomMaterials");
        Research phaseMaterialEngineering = DataBase<Research>.Find("PhaseMaterialEngineering");
        Research titaniumAlloyEngineering = DataBase<Research>.Find("TitaniumAlloyEngineering");
        Assert.That(phantomMaterials, Is.Not.Null);
        Assert.That(phaseMaterialEngineering, Is.Not.Null);
        Assert.That(titaniumAlloyEngineering, Is.Not.Null);
        Assert.That(ContainsResearch(phaseMaterialEngineering.Prerequisites, phantomMaterials), Is.True,
            "PhaseMaterialEngineering 必须以后置的 PhantomMaterials 为前置，不能跳过暗影材料研究");

        foreach (Research research in DataBase<Research>.All)
        {
            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement = research.ResourceRequirements[i];
                if (requirement == null || requirement.First == null)
                    continue;

                if (requirement.First.Id == "PhantomAlloy" || requirement.First.Id == "PhantomWeave")
                    Assert.That(DependsOnResearch(research.Prerequisites, phantomMaterials), Is.True, research.Id);
                if (requirement.First.Id == "PhaseMaterial")
                    Assert.That(DependsOnResearch(research.Prerequisites, phaseMaterialEngineering), Is.True, research.Id);
                if (requirement.First.Id == "TitaniumAlloy" &&
                    research != titaniumAlloyEngineering && research.TechLevel < TechLevel.Spacer)
                     Assert.That(DependsOnResearch(research.Prerequisites, titaniumAlloyEngineering), Is.True, research.Id);
            }
        }

        foreach (WorkshopUpgrade workshop in DataBase<WorkshopUpgrade>.All)
        {
            for (int i = 0; i < workshop.ResourceRequirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement = workshop.ResourceRequirements[i];
                if (requirement == null || requirement.First == null)
                    continue;

                if (requirement.First.Id == "PhantomAlloy" || requirement.First.Id == "PhantomWeave")
                     Assert.That(DependsOnResearch(workshop.RequiredResearch, phantomMaterials), Is.True, workshop.Id);
                if (requirement.First.Id == "PhaseMaterial")
                     Assert.That(DependsOnResearch(workshop.RequiredResearch, phaseMaterialEngineering), Is.True, workshop.Id);
                if (requirement.First.Id == "TitaniumAlloy")
                     Assert.That(DependsOnResearch(workshop.RequiredResearch, titaniumAlloyEngineering), Is.True, workshop.Id);
            }
        }

        foreach (Building building in DataBase<Building>.All)
        {
            AssertBuildingMaterialPrerequisites(building, building.ResourceRequirements, phantomMaterials, phaseMaterialEngineering, titaniumAlloyEngineering);
            AssertBuildingMaterialPrerequisites(building, building.ResourceGenerationRates, phantomMaterials, phaseMaterialEngineering, titaniumAlloyEngineering);
            AssertBuildingMaterialPrerequisites(building, building.ResourceConsumptionRates, phantomMaterials, phaseMaterialEngineering, titaniumAlloyEngineering);
        }
    }

    [Test]
    public void 高级材料研究保持时代顺序()
    {
        Research titanium = DataBase<Research>.Find("TitaniumAlloyEngineering");
        Research phantom = DataBase<Research>.Find("PhantomMaterials");
        Research phase = DataBase<Research>.Find("PhaseMaterialEngineering");

        Assert.That(titanium, Is.Not.Null);
        Assert.That(phantom, Is.Not.Null);
        Assert.That(phase, Is.Not.Null);
        Assert.That((int)phantom.TechLevel, Is.GreaterThan((int)titanium.TechLevel));
        Assert.That((int)phase.TechLevel, Is.GreaterThanOrEqualTo((int)phantom.TechLevel));
        Assert.That(ContainsResearch(phantom.Prerequisites, titanium), Is.True);
        Assert.That(ContainsResearch(phase.Prerequisites, phantom), Is.True);
    }

    private static void AssertBuildingMaterialPrerequisites(
        Building building,
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements,
        Research phantomMaterials,
        Research phaseMaterialEngineering,
        Research titaniumAlloyEngineering)
    {
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement == null || requirement.First == null)
                continue;

            if (requirement.First.Id == "PhantomAlloy" || requirement.First.Id == "PhantomWeave")
                Assert.That(DependsOnResearch(building.RequiredResearch, phantomMaterials), Is.True, building.Id);
            if (requirement.First.Id == "PhaseMaterial")
                Assert.That(DependsOnResearch(building.RequiredResearch, phaseMaterialEngineering), Is.True, building.Id);
            if (requirement.First.Id == "TitaniumAlloy")
                Assert.That(DependsOnResearch(building.RequiredResearch, titaniumAlloyEngineering), Is.True, building.Id);
        }
    }

    private static bool ContainsResource(IReadOnlyList<Pair<Resource, ExpantaNum>> rates, string resourceId)
    {
        if (rates == null)
            return false;
        for (int i = 0; i < rates.Count; i++)
            if (rates[i] != null && rates[i].First != null && rates[i].First.Id == resourceId)
                return true;
        return false;
    }

    private static bool ContainsResearch(IReadOnlyList<Research> prerequisites, Research target)
    {
        if (prerequisites == null || target == null)
            return false;
        for (int i = 0; i < prerequisites.Count; i++)
            if (prerequisites[i] == target ||
                (prerequisites[i] != null && prerequisites[i].Id == target.Id))
                return true;
        return false;
    }

    private static bool DependsOnResearch(IReadOnlyList<Research> prerequisites, Research target)
    {
        var visited = new HashSet<Research>();
        for (int i = 0; i < prerequisites.Count; i++)
            if (DependsOnResearch(prerequisites[i], target, visited))
                return true;
        return false;
    }

    private static bool DependsOnResearch(
        Research research,
        Research target,
        HashSet<Research> visited)
    {
        if (research == null || !visited.Add(research))
            return false;
        if (research == target)
            return true;
        for (int i = 0; i < research.Prerequisites.Count; i++)
            if (DependsOnResearch(research.Prerequisites[i], target, visited))
                return true;
        return false;
    }

    [Test]
    public void 已发布研究与工坊不得拥有空Effect()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            Assert.That(research, Is.Not.Null);
            Assert.That(research.Effects, Is.Not.Null, research.Id);
            Assert.That(
                research.AdvancesTechLevel || research.Effects.Count > 0,
                Is.True,
                research.Id);
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
        Assert.That(DataBase<Research>.TryFind("MechanizedProduction", out _), Is.False);

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
            ["Agriculture"] = 300d,
            ["AnimalHusbandry"] = 240d,
            ["ClayExtraction"] = 240d,
            ["StoneTools"] = 700d,
            ["Mining"] = 1800d,
            ["Mathematics"] = 1700d,
            ["Calendar"] = 1900d,
            ["KnowledgeSharing"] = 1200d,
            ["CeramicFiring"] = 1800d,
            ["TextileCraft"] = 1600d,
            ["NeolithicSettlement"] = 1600d,
            ["Measurement"] = 3200d,
            ["FoodStorage"] = 4200d,
            ["WrittenRecords"] = 6500d,
            ["Masonry"] = 8500d,
            ["Smithing_Copper"] = 15000d,
            ["Smithing_Iron"] = 19000d,
            ["Smithing_Bronze"] = 21000d,
            ["FeudalAdministration"] = 130000d
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
        Research transition = DataBase<Research>.Find("FeudalAdministration");
        Assert.That(transition.BaseCost, Is.EqualTo("130000"));
        Assert.That(transition.TechLevel, Is.EqualTo(TechLevel.Medieval));
        Assert.That(transition.AdvancesTechLevel, Is.True);
        Assert.That(transition.Prerequisites.Select(research => research.Id),
            Is.EquivalentTo(new[] { "Smithing_Bronze", "Smithing_Iron", "WrittenRecords" }));
    }

    [Test]
    public void VerticalSlice_ResearchEffectsAndUnlocksMatchTheContentPlan()
    {
        // Productivity consumption is read from the current content asset.
        Research mathematics = DataBase<Research>.Find("Mathematics");
        Research calendar = DataBase<Research>.Find("Calendar");
        Research knowledgeSharing = DataBase<Research>.Find("KnowledgeSharing");
        Research controlledFire = DataBase<Research>.Find("ControlledFire");
        Research mining = DataBase<Research>.Find("Mining");
        Research measurement = DataBase<Research>.Find("Measurement");
        Research smithing = DataBase<Research>.Find("Smithing_Bronze");

        Assert.That(HasEffect(controlledFire, ResearchEffectType.BuildingProductionMultiplier, 1.1d, "CeramicKiln"), Is.True);
        Assert.That(HasEffect(mining, ResearchEffectType.BuildingProductionMultiplier, 1.242d), Is.True);
        Assert.That(HasEffect(mathematics, ResearchEffectType.GlobalResearchMultiplier, 1.3375d), Is.True);
        Assert.That(HasEffect(calendar, ResearchEffectType.BuildingFoodProductionMultiplier, 1.1d), Is.True);
        Assert.That(HasEffect(measurement, ResearchEffectType.GlobalConstructionMultiplier, 1.1d), Is.True);
        Assert.That(
            HasEffect(
                smithing,
                ResearchEffectType.BuildingProductionMultiplier,
                1.15d,
                "MetalSmelter"),
            Is.True);
        Building knowledgeCircle = DataBase<Building>.Find("KnowledgeCircle");
        Assert.That(knowledgeCircle.RequiredResearch, Is.EquivalentTo(new[] { controlledFire, knowledgeSharing }));
        Assert.That(knowledgeCircle.ResourceRequirements.Count, Is.EqualTo(2));
        Assert.That(knowledgeCircle.ResourceRequirements[0].First.Id, Is.EqualTo("WoodLog"));
        Assert.That(
            knowledgeCircle.ResourceRequirements[0].Second.ToDouble(),
            Is.EqualTo(100d).Within(0.000001d));
        Assert.That(knowledgeCircle.ResourceRequirements[1].First.Id, Is.EqualTo("StoneChunk"));
        Assert.That(
            knowledgeCircle.ResourceRequirements[1].Second.ToDouble(),
            Is.EqualTo(20d).Within(0.000001d));
        Assert.That(
            knowledgeCircle.ResearchPowerGranted.ToDouble(),
            Is.EqualTo(1d).Within(0.000001d));
        Assert.That(
            knowledgeCircle.ProductivityConsumption.ToDouble(),
            Is.EqualTo(2d).Within(0.000001d));
        Assert.That(
            HasEffect(
                knowledgeSharing,
                ResearchEffectType.GlobalResearchMultiplier,
                1.15d),
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
        bool implementationExists = DataBase<WorkshopUpgrade>.TryFind(
            "RemoteSurgicalSystems", out WorkshopUpgrade implementation);

        Assert.That(theory, Is.Not.Null);
        Assert.That(implementationExists, Is.False);
        Assert.That(implementation, Is.Null);
        Assert.That(theory.Prerequisites, Has.Some.Property("Id").EqualTo("ModernMedicine"));
        Assert.That(theory.Prerequisites, Has.Some.Property("Id").EqualTo("BioregenerativeLifeSupport"));
        Assert.That(theory.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.PopulationProductivityMultiplier &&
            effect.NumericValue == new ExpantaNum("1.5")));
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
        Assert.That(publicHealthCost, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(modernCost, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(lifeSupportCost, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(precisionCost, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(HasResourceRequirementById(modernMedicine, "Ceramic"), Is.True);
        Assert.That(HasResourceRequirementById(modernMedicine, "Electronics"), Is.True);
        Assert.That(HasResourceRequirementById(lifeSupport, "Biomass"), Is.True);
        Assert.That(HasResourceRequirementById(precisionMedicine, "Biomass"), Is.True);
        Assert.That(HasResourceRequirementById(precisionMedicine, "TitaniumAlloy"), Is.True);
    }

    [Test]
    public void IndustrialResearchEffectsMatchTargetBuildingSystems()
    {
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("CombustionEngines"),
            ResearchEffectType.BuildingLogisticsProductionMultiplier,
            "RailHub"), Is.True);
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("CombustionEngines"),
            ResearchEffectType.BuildingProductionMultiplier,
            "MachineFactory"), Is.True);
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("ElectricalCommunication"),
            ResearchEffectType.BuildingLogisticsProductionMultiplier,
            "RailHub"), Is.True);
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("ElectricalCommunication"),
            ResearchEffectType.BuildingResearchPowerMultiplier,
            "University"), Is.True);
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("PowerGridEngineering"),
            ResearchEffectType.BuildingPowerProductionMultiplier,
            "CentralPowerStation"), Is.True);
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("MechanicalEngineering"),
            ResearchEffectType.BuildingFoodProductionMultiplier,
            "IrrigationWorks"), Is.True);
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("IndustrialAgriculture"),
            ResearchEffectType.BuildingProductionMultiplier,
            "PlantingField"), Is.True);
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("TitaniumAlloyEngineering"),
            ResearchEffectType.BuildingProductionMultiplier,
            "TitaniumMetallurgicalComplex"), Is.True);
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
                effect.Building.Id == "IndustrialMetalSmelter" &&
                effect.NumericValue.ToDouble() >= 1.25d)
            {
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True, "一体化冶炉必须提升多金属冶炼炉产出。");
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
                effect.NumericValue.ToDouble() == value)
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
                return effect.NumericValue.ToDouble();
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
                effect.NumericValue.ToDouble() == value &&
                effect.Building != null &&
                effect.Building.Id == buildingId)
                return true;
        }
        return false;
    }

    private static bool HasTargetedEffect(
        Research research,
        ResearchEffectType type,
        string buildingId)
    {
        if (research == null)
            return false;
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Type == type &&
                effect.Building != null && effect.Building.Id == buildingId)
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
                effect.NumericValue.ToDouble() == value &&
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
