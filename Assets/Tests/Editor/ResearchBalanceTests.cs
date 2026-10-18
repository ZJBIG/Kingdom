using System.Collections.Generic;
using System.Linq;
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
            Assert.That(workshop.Effects, Is.Not.Null.And.Not.Empty, workshop.Id);
            for (int i = 0; i < workshop.Effects.Count; i++)
                Assert.That(workshop.Effects[i], Is.Not.Null, workshop.Id);
        }
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
    public void 太空医学理论保留人口生产力效果而不依赖已删除工坊()
    {
        Research theory = DataBase<Research>.Find("PrecisionMedicine");
        bool implementationExists = DataBase<WorkshopUpgrade>.TryFind(
            "RemoteSurgicalSystems", out WorkshopUpgrade implementation);

        Assert.That(theory, Is.Not.Null);
        Assert.That(implementationExists, Is.False);
        Assert.That(implementation, Is.Null);
        Assert.That(theory.Prerequisites.Any(research => research != null && research.Id == "ModernMedicine"), Is.True);
        Assert.That(theory.Prerequisites.Any(research => research != null && research.Id == "BioregenerativeLifeSupport"), Is.True);
        Assert.That(theory.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.PopulationProductivityMultiplier &&
            effect.NumericValue > ExpantaNum.One));
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
            Is.GreaterThan(1d));
        Assert.That(
            GetPopulationProductivity(publicHealth),
            Is.GreaterThan(GetPopulationProductivity(herbalKnowledge)));
        Assert.That(
            GetPopulationProductivity(modernMedicine),
            Is.GreaterThan(GetPopulationProductivity(publicHealth)));
        Assert.That(
            GetPopulationProductivity(precisionMedicine),
            Is.GreaterThan(GetPopulationProductivity(modernMedicine)));
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
        Assert.That(HasEffect(
            DataBase<Research>.Find("MechanicalEngineering"),
            ResearchEffectType.GlobalFoodProductionMultiplier), Is.True);
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("IndustrialAgriculture"),
            ResearchEffectType.BuildingProductionMultiplier,
            "PlantingField"), Is.True);
        Assert.That(HasTargetedEffect(
            DataBase<Research>.Find("TitaniumAlloyEngineering"),
            ResearchEffectType.BuildingProductionMultiplier,
            "TitaniumAlloySmelter"), Is.True);
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
                effect.NumericValue > ExpantaNum.One)
            {
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True, "一体化冶炉必须提升多金属冶炼炉产出。");
    }

    [Test]
    public void IndustrialFactoryResearchesHaveDistinctPlayerFacingRoles()
    {
        Research organization = DataBase<Research>.Find("FactoryOrganization");
        Research massProduction = DataBase<Research>.Find("MassProduction");
        Research precision = DataBase<Research>.Find("PrecisionManufacturing");

        Assert.That(HasEffect(organization, ResearchEffectType.GlobalBuildingProductionMultiplier), Is.False);
        Assert.That(HasEffect(organization, ResearchEffectType.GlobalConstructionMultiplier), Is.True);
        Assert.That(HasEffect(massProduction, ResearchEffectType.GlobalBuildingProductionMultiplier), Is.True);
        Assert.That(HasTargetedEffect(precision, ResearchEffectType.BuildingProductionMultiplier, "MachineFactory"), Is.True);
    }

    private static bool HasEffect(Research research, ResearchEffectType type)
    {
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null && effect.Type == type)
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
