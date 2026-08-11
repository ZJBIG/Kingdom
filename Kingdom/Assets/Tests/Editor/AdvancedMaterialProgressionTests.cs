using NUnit.Framework;

public sealed class AdvancedMaterialProgressionTests
{
    [Test]
    public void AdvancedMaterialsKeepIndustrialInputsAndLowOutput()
    {
        Resource titaniumAlloy = DataBase<Resource>.Find("TitaniumAlloy");
        Resource phantomAlloy = DataBase<Resource>.Find("PhantomAlloy");
        Resource phantomWeave = DataBase<Resource>.Find("PhantomWeave");
        Resource phaseMaterial = DataBase<Resource>.Find("PhaseMaterial");
        Resource ceramic = DataBase<Resource>.Find("Ceramic");
        Assert.That(titaniumAlloy, Is.Not.Null);
        Assert.That(phantomAlloy, Is.Not.Null);
        Assert.That(phantomWeave, Is.Not.Null);
        Assert.That(phaseMaterial, Is.Not.Null);
        Assert.That(ceramic, Is.Not.Null);

        Building titaniumPlant = DataBase<Building>.Find("TitaniumMetallurgicalComplex");
        Building phantomPlant = DataBase<Building>.Find("PhantomMaterialsFabricator");
        Building phasePlant = DataBase<Building>.Find("PhaseMaterialSynthesisArray");
        Building ceramicPlant = DataBase<Building>.Find("AdvancedCeramicsPlant");
        Assert.That(phasePlant, Is.Not.Null);
        Assert.That(HasPositiveRate(titaniumPlant.ResourceGenerationRates, titaniumAlloy), Is.True);
        Assert.That(HasPositiveRate(phantomPlant.ResourceGenerationRates, phantomAlloy), Is.True);
        Assert.That(HasPositiveRate(phantomPlant.ResourceGenerationRates, phantomWeave), Is.True);
        Assert.That(HasPositiveRate(phantomPlant.ResourceGenerationRates, phaseMaterial), Is.False);
        Assert.That(HasPositiveRate(phasePlant.ResourceGenerationRates, phaseMaterial), Is.True);
        Assert.That(HasPositiveRate(ceramicPlant.ResourceGenerationRates, ceramic), Is.True);
        Assert.That(phantomPlant.ResourceGenerationRates[0].Second.ToDouble(), Is.LessThan(0.1d));
        Assert.That(phantomPlant.ResourceGenerationRates[1].Second.ToDouble(), Is.LessThan(0.1d));
        Assert.That(titaniumPlant.ResourceConsumptionRates.Count, Is.GreaterThanOrEqualTo(4));
        Assert.That(phantomPlant.ResourceConsumptionRates.Count, Is.GreaterThanOrEqualTo(6));
        Assert.That(phasePlant.ResourceConsumptionRates.Count, Is.GreaterThanOrEqualTo(5));
        Assert.That(HasPositiveRate(
            titaniumPlant.ResourceConsumptionRates,
            DataBase<Resource>.Find("Nickel")), Is.True);
        Assert.That(HasPositiveRate(
            titaniumPlant.ResourceConsumptionRates,
            ceramic), Is.True);
        Assert.That(HasResourceRequirement(
            titaniumPlant.ResourceRequirements,
            DataBase<Resource>.Find("Nickel"),
            180d), Is.True);
        Assert.That(HasResourceRequirement(ceramicPlant.ResourceRequirements, ceramic, 420d), Is.True);
        Assert.That(HasResourceRequirement(phantomPlant.ResourceRequirements, ceramic, 260d), Is.True);
        Assert.That(HasResourceRequirement(phasePlant.ResourceRequirements, phantomAlloy, 1200d), Is.True);
        Assert.That(HasResourceRequirement(phasePlant.ResourceRequirements, phantomWeave, 1000d), Is.True);
        Assert.That(HasResourceRequirement(phasePlant.ResourceRequirements, titaniumAlloy, 1800d), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceConsumptionRates, phantomAlloy), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceConsumptionRates, phantomWeave), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceConsumptionRates, titaniumAlloy), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceConsumptionRates, phaseMaterial), Is.False);
        Assert.That(CountBuildingSinks(phaseMaterial), Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void AdvancedMaterialWorkshopsImproveTheCorrectProductionBuildings()
    {
        Assert.That(
            HasBuildingEffect("TitaniumReductionRetorts", "TitaniumMetallurgicalComplex", 1.2d),
            Is.True);
        Assert.That(
            HasBuildingEffect("VacuumArcFurnace", "TitaniumMetallurgicalComplex", 1.2d),
            Is.True);
        Assert.That(
            HasResourceRequirement("TitaniumReductionRetorts", "Nickel", 140d),
            Is.True);
        Assert.That(
            HasBuildingEffect("PhaseFieldContainment", "PhantomMaterialsFabricator", 1.25d),
            Is.True);
        Assert.That(
            HasBuildingEffect("AdvancedCeramicFiring", "AdvancedCeramicsPlant", 1.3d),
            Is.True);
        Assert.That(
            HasResearchBuildingEffect("AdvancedCeramicEngineering", "AdvancedCeramicsPlant", 1.2d),
            Is.True);
        Assert.That(HasBuildingEffect("PhaseMaterialCalibration", "PhaseMaterialSynthesisArray", 1.25d), Is.True);
        Assert.That(HasResourceRequirement("PhaseMaterialCalibration", "PhaseMaterial", 260d), Is.True);
        Assert.That(HasResourceRequirement("AutomatedShipyardAssembly", "Composite", 700d), Is.True);
        Assert.That(HasResourceProductionEffect("PhantomWeaveLattice", "PhantomWeave", 1.25d), Is.True);
        Assert.That(HasResourceRequirement("PhantomWeaveLattice", "PhantomAlloy", 650d), Is.True);
        Assert.That(HasResourceProductionEffect("PhantomAlloyRecrystallization", "PhantomAlloy", 1.2d), Is.True);
        Assert.That(HasResourceRequirement("PhantomAlloyRecrystallization", "PhaseMaterial", 240d), Is.True);
    }

    [Test]
    public void 高级材料研究必须强化对应生产建筑()
    {
        Assert.That(
            HasResearchBuildingEffect("TitaniumAlloyEngineering", "TitaniumMetallurgicalComplex", 1.1d),
            Is.True);
        Assert.That(
            HasResearchBuildingEffect("PhantomMaterials", "PhantomMaterialsFabricator", 1.15d),
            Is.True);
        Assert.That(
            HasResearchBuildingEffect("PhaseMaterialEngineering", "PhaseMaterialSynthesisArray", 1.2d),
            Is.True);
    }

    [Test]
    public void 氯化钛冶金研究负责钛精矿纯化理论()
    {
        Research theory = DataBase<Research>.Find("ChlorideTitaniumMetallurgy");
        Resource concentrate = DataBase<Resource>.Find("TitaniumConcentrate");

        Assert.That(theory, Is.Not.Null);
        Assert.That(concentrate, Is.Not.Null);
        bool hasPurificationTheory = false;
        for (int i = 0; i < theory.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = theory.Effects[i];
            if (effect != null &&
                effect.Type == ResearchEffectType.ResourceProductionMultiplier &&
                effect.Resource == concentrate &&
                effect.Value.ToDouble() >= 1.15d)
            {
                hasPurificationTheory = true;
                break;
            }
        }
        Assert.That(hasPurificationTheory, Is.True);
    }

    [Test]
    public void SpacerAdvancedMaterialsKeepSourcesAndDurableSinks()
    {
        foreach (string resourceId in new[] { "TitaniumAlloy", "Composite", "PhantomAlloy", "PhantomWeave", "PhaseMaterial" })
        {
            Resource resource = DataBase<Resource>.Find(resourceId);
            Assert.That(resource, Is.Not.Null, resourceId);
            Assert.That(CountPositiveSources(resource), Is.GreaterThanOrEqualTo(1), resourceId);
            Assert.That(CountBuildingSinks(resource), Is.GreaterThanOrEqualTo(2), resourceId);
            Assert.That(CountResearchSinks(resource), Is.GreaterThanOrEqualTo(2), resourceId);
            Assert.That(CountWorkshopSinks(resource), Is.GreaterThanOrEqualTo(2), resourceId);
        }
    }

    [Test]
    public void 太空高级材料必须进入多个星区行动()
    {
        foreach (string resourceId in new[]
        {
            "TitaniumAlloy", "Composite", "PhantomAlloy", "PhantomWeave", "PhaseMaterial"
        })
        {
            Resource resource = DataBase<Resource>.Find(resourceId);
            int sectorUses = 0;
            foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
            {
                if (sector == null)
                    continue;
                if (HasPositiveRate(sector.CampaignResourceRatesPerSecond, resource) ||
                    HasPositiveRate(sector.ColonizationResourceRatesPerSecond, resource))
                    sectorUses++;
            }

            Assert.That(resource, Is.Not.Null, resourceId);
            Assert.That(
                sectorUses,
                Is.GreaterThanOrEqualTo(2),
                $"高级材料 {resourceId} 必须服务至少两个星区行动，不能只承担一次性建造费用。");
        }
    }

    [Test]
    public void 高级材料必须跨时代生产并持续服务太空建筑()
    {
        Assert.That(HasSourceAtTechLevel("TitaniumAlloy", TechLevel.Industrial), Is.True);
        Assert.That(HasSourceAtTechLevel("Composite", TechLevel.Industrial), Is.True);
        Assert.That(HasSourceAtTechLevel("PhantomAlloy", TechLevel.Spacer), Is.True);
        Assert.That(HasSourceAtTechLevel("PhantomWeave", TechLevel.Spacer), Is.True);
        Assert.That(HasSourceAtTechLevel("PhaseMaterial", TechLevel.Spacer), Is.True);

        foreach (string resourceId in new[] { "TitaniumAlloy", "Composite", "PhantomAlloy", "PhantomWeave", "PhaseMaterial" })
            Assert.That(HasSpacerContinuousSink(resourceId), Is.True, resourceId);
    }

    [Test]
    public void 舰队维修材料必须拥有工业生产入口()
    {
        Assert.That(HasSourceAtTechLevel("Composite", TechLevel.Industrial), Is.True);
        Assert.That(HasSourceAtTechLevel("TitaniumAlloy", TechLevel.Industrial), Is.True);
        Assert.That(HasSourceAtTechLevel("RocketFuel", TechLevel.Industrial), Is.True);
        Assert.That(HasSourceAtTechLevel("PhantomWeave", TechLevel.Spacer), Is.True);
    }

    [Test]
    public void 太空高级材料必须拥有明确且无生产循环的核心工坊()
    {
        Assert.That(HasPositiveProductionBuilding("Composite", "MachineFactory"), Is.True);
        Assert.That(HasPositiveProductionBuilding("PhantomAlloy", "PhantomMaterialsFabricator"), Is.True);
        Assert.That(HasPositiveProductionBuilding("PhantomWeave", "PhantomMaterialsFabricator"), Is.True);
        Assert.That(HasPositiveProductionBuilding("PhaseMaterial", "PhaseMaterialSynthesisArray"), Is.True);

        Building machineFactory = DataBase<Building>.Find("MachineFactory");
        Building phantomFabricator = DataBase<Building>.Find("PhantomMaterialsFabricator");
        Building phaseArray = DataBase<Building>.Find("PhaseMaterialSynthesisArray");
        Assert.That(machineFactory, Is.Not.Null);
        Assert.That(phantomFabricator, Is.Not.Null);
        Assert.That(phaseArray, Is.Not.Null);
        Assert.That(HasPositiveRate(machineFactory.ResourceConsumptionRates, DataBase<Resource>.Find("Composite")), Is.False);
        Assert.That(HasPositiveRate(phantomFabricator.ResourceConsumptionRates, DataBase<Resource>.Find("PhantomAlloy")), Is.False);
        Assert.That(HasPositiveRate(phaseArray.ResourceConsumptionRates, DataBase<Resource>.Find("PhaseMaterial")), Is.False);
    }

    private static bool HasPositiveRate(
        System.Collections.Generic.IReadOnlyList<Pair<Resource, ExpantaNum>> rates,
        Resource resource)
    {
        if (rates == null || resource == null)
            return false;
        for (int i = 0; i < rates.Count; i++)
            if (rates[i].First == resource && rates[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static bool HasPositiveProductionBuilding(string resourceId, string buildingId)
    {
        Resource resource = DataBase<Resource>.Find(resourceId);
        Building building = DataBase<Building>.Find(buildingId);
        return resource != null && building != null && HasPositiveRate(building.ResourceGenerationRates, resource);
    }

    private static int CountPositiveSources(Resource resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<Building>.All.Count; i++)
            if (HasPositiveRate(DataBase<Building>.All[i].ResourceGenerationRates, resource))
                count++;
        return count;
    }

    private static bool HasSourceAtTechLevel(string resourceId, TechLevel techLevel)
    {
        Resource resource = DataBase<Resource>.Find(resourceId);
        if (resource == null)
            return false;
        for (int i = 0; i < DataBase<Building>.All.Count; i++)
        {
            Building building = DataBase<Building>.All[i];
            if (building.TechLevel == techLevel &&
                HasPositiveRate(building.ResourceGenerationRates, resource))
                return true;
        }
        return false;
    }

    private static bool HasSpacerContinuousSink(string resourceId)
    {
        Resource resource = DataBase<Resource>.Find(resourceId);
        if (resource == null)
            return false;
        for (int i = 0; i < DataBase<Building>.All.Count; i++)
        {
            Building building = DataBase<Building>.All[i];
            if (building.TechLevel == TechLevel.Spacer &&
                HasPositiveRate(building.ResourceConsumptionRates, resource))
                return true;
        }
        return false;
    }

    private static int CountBuildingSinks(Resource resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<Building>.All.Count; i++)
        {
            Building building = DataBase<Building>.All[i];
            if (HasPositiveRate(building.ResourceRequirements, resource) ||
                HasPositiveRate(building.ResourceConsumptionRates, resource))
                count++;
        }
        return count;
    }

    private static int CountResearchSinks(Resource resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<Research>.All.Count; i++)
            if (HasPositiveRate(DataBase<Research>.All[i].ResourceRequirements, resource))
                count++;
        return count;
    }

    private static int CountWorkshopSinks(Resource resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<WorkshopUpgrade>.All.Count; i++)
            if (HasPositiveRate(DataBase<WorkshopUpgrade>.All[i].ResourceRequirements, resource))
                count++;
        return count;
    }

    private static bool HasBuildingEffect(string workshopId, string buildingId, double minimumMultiplier)
    {
        WorkshopUpgrade workshop = DataBase<WorkshopUpgrade>.Find(workshopId);
        if (workshop == null)
            return false;
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null &&
                effect.Type == WorkshopEffectType.BuildingProductionMultiplier &&
                effect.Building != null &&
                effect.Building.Id == buildingId &&
                effect.Value.ToDouble() >= minimumMultiplier)
                return true;
        }
        return false;
    }

    private static bool HasResourceRequirement(
        System.Collections.Generic.IReadOnlyList<Pair<Resource, ExpantaNum>> requirements,
        Resource resource,
        double minimumAmount)
    {
        if (requirements == null || resource == null)
            return false;
        for (int i = 0; i < requirements.Count; i++)
            if (requirements[i].First == resource &&
                requirements[i].Second.ToDouble() >= minimumAmount)
                return true;
        return false;
    }

    private static bool HasResourceRequirement(string workshopId, string resourceId, double minimumAmount)
    {
        WorkshopUpgrade workshop = DataBase<WorkshopUpgrade>.Find(workshopId);
        Resource resource = DataBase<Resource>.Find(resourceId);
        return workshop != null && HasResourceRequirement(workshop.ResourceRequirements, resource, minimumAmount);
    }

    private static bool HasResearchBuildingEffect(string researchId, string buildingId, double minimumMultiplier)
    {
        Research research = DataBase<Research>.Find(researchId);
        if (research == null)
            return false;
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null &&
                effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
                effect.Building != null &&
                effect.Building.Id == buildingId &&
                effect.Value.ToDouble() >= minimumMultiplier)
                return true;
        }
        return false;
    }

    private static bool HasResourceProductionEffect(string workshopId, string resourceId, double minimumMultiplier)
    {
        WorkshopUpgrade workshop = DataBase<WorkshopUpgrade>.Find(workshopId);
        Resource resource = DataBase<Resource>.Find(resourceId);
        if (workshop == null || resource == null)
            return false;
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null &&
                effect.Type == WorkshopEffectType.ResourceProductionMultiplier &&
                effect.Resource == resource &&
                effect.Value.ToDouble() >= minimumMultiplier)
                return true;
        }
        return false;
    }
}
