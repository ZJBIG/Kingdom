using NUnit.Framework;

public sealed class AdvancedMaterialProgressionTests
{
    [Test]
    public void AdvancedMaterialsKeepIndustrialInputsAndDistinctOutputs()
    {
        ResourceDef titaniumAlloy = DataBase<ResourceDef>.Find("TitaniumAlloy");
        ResourceDef phantomAlloy = DataBase<ResourceDef>.Find("PhantomAlloy");
        ResourceDef phantomWeave = DataBase<ResourceDef>.Find("PhantomWeave");
        ResourceDef phaseMaterial = DataBase<ResourceDef>.Find("PhaseMaterial");
        ResourceDef ceramic = DataBase<ResourceDef>.Find("Ceramic");

        BuildingDef titaniumPlant = DataBase<BuildingDef>.Find("TitaniumAlloySmelter");
        BuildingDef phantomPlant = DataBase<BuildingDef>.Find("PhantomMaterialsFabricator");
        BuildingDef phasePlant = DataBase<BuildingDef>.Find("PhaseMaterialSynthesisArray");
        BuildingDef ceramicPlant = DataBase<BuildingDef>.Find("AdvancedCeramicsPlant");
        Assert.That(HasPositiveRate(titaniumPlant.ResourceGenerationRates, titaniumAlloy), Is.True);
        Assert.That(HasPositiveRate(phantomPlant.ResourceGenerationRates, phantomAlloy), Is.True);
        Assert.That(HasPositiveRate(phantomPlant.ResourceGenerationRates, phantomWeave), Is.True);
        Assert.That(HasPositiveRate(phantomPlant.ResourceGenerationRates, phaseMaterial), Is.False);
        Assert.That(HasPositiveRate(phasePlant.ResourceGenerationRates, phaseMaterial), Is.True);
        Assert.That(HasPositiveRate(ceramicPlant.ResourceGenerationRates, ceramic), Is.True);
        Assert.That(HasPositiveRate(
            titaniumPlant.ResourceConsumptionRates,
            DataBase<ResourceDef>.Find("AdvancedMetalOre")), Is.True);
        Assert.That(HasPositiveRate(
            titaniumPlant.ResourceConsumptionRates,
            ceramic), Is.True);
        Assert.That(HasPositiveRate(
            titaniumPlant.ResourceRequirements,
            DataBase<ResourceDef>.Find("AdvancedMetalOre")), Is.True);
        Assert.That(HasPositiveRate(ceramicPlant.ResourceRequirements, ceramic), Is.True);
        Assert.That(HasPositiveRate(phantomPlant.ResourceRequirements, ceramic), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceRequirements, phantomAlloy), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceRequirements, phantomWeave), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceRequirements, titaniumAlloy), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceConsumptionRates, phantomAlloy), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceConsumptionRates, phantomWeave), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceConsumptionRates, titaniumAlloy), Is.True);
        Assert.That(HasPositiveRate(phasePlant.ResourceConsumptionRates, phaseMaterial), Is.False);
        Assert.That(CountBuildingSinks(phaseMaterial), Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void 炸药必须保持工业流程持续消耗而非星际战役成本()
    {
        ResourceDef explosives = DataBase<ResourceDef>.Find("Explosives");
        BuildingDef chemicalPlant = DataBase<BuildingDef>.Find("ChemicalPlant");
        Assert.That(HasPositiveRate(chemicalPlant.ResourceGenerationRates, explosives), Is.True);

        int continuousConsumers = 0;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            if (!HasPositiveRate(building.ResourceConsumptionRates, explosives))
                continue;

            continuousConsumers++;
            Assert.That(
                building.TechLevel,
                Is.GreaterThanOrEqualTo(TechLevel.Industrial),
                building.Id);
        }
        Assert.That(continuousConsumers, Is.GreaterThanOrEqualTo(7));

        foreach (SectorDef sector in DataBase<SectorDef>.All)
        {
            Assert.That(
                HasPositiveRate(sector.CampaignResourceRatesPerSecond, explosives),
                Is.False,
                sector.Id);
            Assert.That(
                HasPositiveRate(sector.ColonizationResourceRatesPerSecond, explosives),
                Is.False,
                sector.Id);
        }
    }

    [Test]
    public void AdvancedMaterialWorkshopsImproveTheCorrectProductionBuildings()
    {
        Assert.That(
            HasBuildingEffect("ChlorideReductionRetorts", "TitaniumAlloySmelter"),
            Is.True);
        Assert.That(
            HasBuildingEffect("VacuumArcFurnace", "TitaniumAlloySmelter"),
            Is.True);
        Assert.That(
            HasResourceRequirement("ChlorideReductionRetorts", "AdvancedMetalIngot"),
            Is.True);
        Assert.That(
            HasBuildingEffect("PhaseFieldContainment", "PhantomMaterialsFabricator"),
            Is.True);
        Assert.That(
            HasBuildingEffect("AdvancedCeramicFiring", "AdvancedCeramicsPlant"),
            Is.True);
        Assert.That(
            HasResearchBuildingEffect("AdvancedCeramicEngineering", "AdvancedCeramicsPlant"),
            Is.True);
        Assert.That(HasResourceProductionEffect("PhaseMaterialCalibration", "PhaseMaterial"), Is.True);
        Assert.That(HasResourceRequirement("PhaseMaterialCalibration", "PhaseMaterial"), Is.True);
        Assert.That(HasResourceRequirement("AutomatedShipyardAssembly", "Composite"), Is.True);
        Assert.That(HasResourceProductionEffect("PhantomWeaveLattice", "PhantomWeave"), Is.True);
        Assert.That(HasResourceRequirement("PhantomWeaveLattice", "PhantomAlloy"), Is.True);
        Assert.That(HasResourceProductionEffect("PhantomAlloyRecrystallization", "PhantomAlloy"), Is.True);
        Assert.That(HasResourceRequirement("PhantomAlloyRecrystallization", "PhaseMaterial"), Is.True);
    }

    [Test]
    public void 高级材料研究必须强化对应生产建筑()
    {
        Assert.That(
            HasResearchBuildingEffect("TitaniumAlloyEngineering", "TitaniumAlloySmelter"),
            Is.True);
        Assert.That(
            HasResearchBuildingEffect("PhantomMaterials", "PhantomMaterialsFabricator"),
            Is.True);
        Assert.That(
            HasResearchBuildingEffect("PhaseMaterialEngineering", "PhaseMaterialSynthesisArray"),
            Is.True);
    }

    [Test]
    public void 氯化钛冶金研究负责钛精矿纯化理论()
    {
        ResearchDef theory = DataBase<ResearchDef>.Find("ChlorideTitaniumMetallurgy");
        ResourceDef concentrate = DataBase<ResourceDef>.Find("SpecialMetalOre");

        bool hasPurificationTheory = false;
        for (int i = 0; i < theory.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = theory.Effects[i];
            if (effect != null &&
                effect.Type == ResearchEffectType.ResourceProductionMultiplier &&
                effect.Resource == concentrate &&
                effect.NumericValue > ExpantaNum.One)
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
            ResourceDef resource = DataBase<ResourceDef>.Find(resourceId);
            TechLevel sourceEra = resourceId == "TitaniumAlloy" || resourceId == "Composite"
                ? TechLevel.Industrial : TechLevel.Spacer;
            Assert.That(HasSourceAtTechLevel(resourceId, sourceEra), Is.True, resourceId);
            Assert.That(CountSpacerContinuousBuildingSinks(resource), Is.GreaterThanOrEqualTo(2), resourceId);
            Assert.That(CountResearchSinks(resource), Is.GreaterThanOrEqualTo(2), resourceId);
            Assert.That(CountWorkshopSinks(resource), Is.GreaterThanOrEqualTo(2), resourceId);
            Assert.That(CountSpacerResearchSinks(resource), Is.GreaterThanOrEqualTo(1), resourceId);
            Assert.That(CountSpacerWorkshopSinks(resource), Is.GreaterThanOrEqualTo(1), resourceId);
        }
        Assert.That(HasSourceAtTechLevel("RocketFuel", TechLevel.Industrial), Is.True);
    }

    [Test]
    public void 轨道冶金产物必须进入深空结构与持续工艺()
    {
        BuildingDef vacuumMetallurgy = DataBase<BuildingDef>.Find("OrbitalResourceExtractionArray");
        BuildingDef habitat = DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure");
        BuildingDef logistics = DataBase<BuildingDef>.Find("OrbitalStation");
        BuildingDef deepSpaceRelay = DataBase<BuildingDef>.Find("DeepSpaceRelay");
        BuildingDef phaseSynthesis = DataBase<BuildingDef>.Find("PhaseMaterialSynthesisArray");

        Assert.That(
            HasPositiveRate(vacuumMetallurgy.ResourceGenerationRates, DataBase<ResourceDef>.Find("TitaniumAlloy")),
            Is.True);
        Assert.That(
            HasPositiveRate(vacuumMetallurgy.ResourceConsumptionRates, DataBase<ResourceDef>.Find("PhantomAlloy")),
            Is.True);
        Assert.That(
            HasPositiveRate(habitat.ResourceConsumptionRates, DataBase<ResourceDef>.Find("TitaniumAlloy")),
            Is.True);
        Assert.That(
            HasPositiveRate(logistics.ResourceConsumptionRates, DataBase<ResourceDef>.Find("TitaniumAlloy")),
            Is.True);
        Assert.That(
            HasPositiveRate(deepSpaceRelay.ResourceConsumptionRates, DataBase<ResourceDef>.Find("PhantomAlloy")),
            Is.True);
        Assert.That(
            HasPositiveRate(phaseSynthesis.ResourceConsumptionRates, DataBase<ResourceDef>.Find("PhantomAlloy")),
            Is.True);
    }

    [Test]
    public void 幽影织物与相位材料必须保持不同的太空职责()
    {
        ResourceDef phantomWeave = DataBase<ResourceDef>.Find("PhantomWeave");
        ResourceDef phaseMaterial = DataBase<ResourceDef>.Find("PhaseMaterial");
        BuildingDef phantomFabricator = DataBase<BuildingDef>.Find("PhantomMaterialsFabricator");
        BuildingDef textileArray = DataBase<BuildingDef>.Find("OrbitalResourceExtractionArray");
        BuildingDef habitat = DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure");
        BuildingDef phaseSynthesis = DataBase<BuildingDef>.Find("PhaseMaterialSynthesisArray");
        BuildingDef deepSpaceRelay = DataBase<BuildingDef>.Find("DeepSpaceRelay");
        BuildingDef quantumArray = DataBase<BuildingDef>.Find("QuantumComputingArray");

        Assert.That(HasPositiveRate(phantomFabricator.ResourceGenerationRates, phantomWeave), Is.True);
        Assert.That(HasPositiveRate(textileArray.ResourceConsumptionRates, phantomWeave), Is.True);
        Assert.That(HasPositiveRate(habitat.ResourceConsumptionRates, phantomWeave), Is.True);
        Assert.That(HasPositiveRate(phaseSynthesis.ResourceGenerationRates, phaseMaterial), Is.True);
        Assert.That(HasPositiveRate(deepSpaceRelay.ResourceConsumptionRates, phaseMaterial), Is.True);
        Assert.That(HasPositiveRate(quantumArray.ResourceConsumptionRates, phaseMaterial), Is.True);
        Assert.That(HasPositiveRate(phantomFabricator.ResourceGenerationRates, phaseMaterial), Is.False);
    }

    [Test]
    public void 太空高级材料必须进入多个星区行动()
    {
        foreach (string resourceId in new[]
        {
            "TitaniumAlloy", "Composite", "PhantomAlloy", "PhantomWeave", "PhaseMaterial"
        })
        {
            ResourceDef resource = DataBase<ResourceDef>.Find(resourceId);
            int sectorUses = 0;
            foreach (SectorDef sector in DataBase<SectorDef>.All)
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
    public void 相位材料只能服务星际时代星区行动()
    {
        ResourceDef phaseMaterial = DataBase<ResourceDef>.Find("PhaseMaterial");

        int interstellarUses = 0;
        foreach (SectorDef sector in DataBase<SectorDef>.All)
        {
            if (sector == null ||
                (!HasPositiveRate(sector.CampaignResourceRatesPerSecond, phaseMaterial) &&
                 !HasPositiveRate(sector.ColonizationResourceRatesPerSecond, phaseMaterial)))
                continue;

            Assert.That(
                sector.Domain,
                Is.EqualTo(SectorDef.SectorDomain.Interstellar),
                $"PhaseMaterial 不应提前进入近地轨道、月球或火星行动：{sector.Id}");
            interstellarUses++;
        }

        Assert.That(interstellarUses, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void 太空高级材料必须拥有明确且无生产循环的核心工坊()
    {
        Assert.That(HasPositiveProductionBuilding("Composite", "MachineFactory"), Is.True);
        Assert.That(HasPositiveProductionBuilding("PhantomAlloy", "PhantomMaterialsFabricator"), Is.True);
        Assert.That(HasPositiveProductionBuilding("PhantomWeave", "PhantomMaterialsFabricator"), Is.True);
        Assert.That(HasPositiveProductionBuilding("PhaseMaterial", "PhaseMaterialSynthesisArray"), Is.True);

        BuildingDef machineFactory = DataBase<BuildingDef>.Find("MachineFactory");
        BuildingDef phantomFabricator = DataBase<BuildingDef>.Find("PhantomMaterialsFabricator");
        BuildingDef phaseArray = DataBase<BuildingDef>.Find("PhaseMaterialSynthesisArray");
        Assert.That(HasPositiveRate(machineFactory.ResourceConsumptionRates, DataBase<ResourceDef>.Find("Composite")), Is.False);
        Assert.That(HasPositiveRate(phantomFabricator.ResourceConsumptionRates, DataBase<ResourceDef>.Find("PhantomAlloy")), Is.False);
        Assert.That(HasPositiveRate(phaseArray.ResourceConsumptionRates, DataBase<ResourceDef>.Find("PhaseMaterial")), Is.False);
    }

    [Test]
    public void 太空设施必须持续维护生态与后勤()
    {
        ResourceDef biomass = DataBase<ResourceDef>.Find("Biomass");
        ResourceDef titaniumAlloy = DataBase<ResourceDef>.Find("TitaniumAlloy");
        ResourceDef composite = DataBase<ResourceDef>.Find("Composite");
        BuildingDef habitat = DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure");
        BuildingDef station = DataBase<BuildingDef>.Find("OrbitalStation");

        Assert.That(habitat.PopulationCapacityGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(habitat.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(habitat.PowerContribution, Is.LessThan(ExpantaNum.Zero));
        Assert.That(habitat.LogisticsContribution, Is.LessThan(ExpantaNum.Zero));
        Assert.That(HasPositiveRate(habitat.ResourceConsumptionRates, biomass), Is.True);
        Assert.That(HasPositiveRate(habitat.ResourceConsumptionRates, titaniumAlloy), Is.True);
        Assert.That(HasPositiveRate(habitat.ResourceConsumptionRates, composite), Is.True);
        Assert.That(HasPositiveRate(station.ResourceConsumptionRates, biomass), Is.True);
    }

    private static bool HasPositiveRate(
        System.Collections.Generic.IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates,
        ResourceDef resource)
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
        ResourceDef resource = DataBase<ResourceDef>.Find(resourceId);
        BuildingDef building = DataBase<BuildingDef>.Find(buildingId);
        return resource != null && building != null && HasPositiveRate(building.ResourceGenerationRates, resource);
    }

    private static bool HasSourceAtTechLevel(string resourceId, TechLevel techLevel)
    {
        ResourceDef resource = DataBase<ResourceDef>.Find(resourceId);
        if (resource == null)
            return false;
        for (int i = 0; i < DataBase<BuildingDef>.All.Count; i++)
        {
            BuildingDef building = DataBase<BuildingDef>.All[i];
            if (building.TechLevel == techLevel &&
                HasPositiveRate(building.ResourceGenerationRates, resource))
                return true;
        }
        return false;
    }

    private static int CountBuildingSinks(ResourceDef resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<BuildingDef>.All.Count; i++)
        {
            BuildingDef building = DataBase<BuildingDef>.All[i];
            if (HasPositiveRate(building.ResourceRequirements, resource) ||
                HasPositiveRate(building.ResourceConsumptionRates, resource))
                count++;
        }
        return count;
    }

    private static int CountResearchSinks(ResourceDef resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<ResearchDef>.All.Count; i++)
            if (HasPositiveRate(DataBase<ResearchDef>.All[i].ResourceRequirements, resource))
                count++;
        return count;
    }

    private static int CountWorkshopSinks(ResourceDef resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<WorkshopUpgradeDef>.All.Count; i++)
            if (HasPositiveRate(DataBase<WorkshopUpgradeDef>.All[i].ResourceRequirements, resource))
                count++;
        return count;
    }

    private static int CountSpacerContinuousBuildingSinks(ResourceDef resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<BuildingDef>.All.Count; i++)
        {
            BuildingDef building = DataBase<BuildingDef>.All[i];
            if (building.TechLevel == TechLevel.Spacer &&
                HasPositiveRate(building.ResourceConsumptionRates, resource))
                count++;
        }

        return count;
    }

    private static int CountSpacerResearchSinks(ResourceDef resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<ResearchDef>.All.Count; i++)
        {
            ResearchDef research = DataBase<ResearchDef>.All[i];
            if (research.TechLevel == TechLevel.Spacer &&
                HasPositiveRate(research.ResourceRequirements, resource))
                count++;
        }

        return count;
    }

    private static int CountSpacerWorkshopSinks(ResourceDef resource)
    {
        int count = 0;
        for (int i = 0; i < DataBase<WorkshopUpgradeDef>.All.Count; i++)
        {
            WorkshopUpgradeDef workshop = DataBase<WorkshopUpgradeDef>.All[i];
            if (workshop.TechLevel == TechLevel.Spacer &&
                HasPositiveRate(workshop.ResourceRequirements, resource))
                count++;
        }

        return count;
    }

    private static bool HasBuildingEffect(string workshopId, string buildingId)
    {
        WorkshopUpgradeDef workshop = DataBase<WorkshopUpgradeDef>.Find(workshopId);
        if (workshop == null)
            return false;
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null &&
                effect.Type == WorkshopEffectType.BuildingProductionMultiplier &&
                effect.Building != null &&
                effect.Building.Id == buildingId &&
                effect.NumericValue > ExpantaNum.One)
                return true;
        }
        return false;
    }

    private static bool HasResourceRequirement(string workshopId, string resourceId)
    {
        WorkshopUpgradeDef workshop = DataBase<WorkshopUpgradeDef>.Find(workshopId);
        ResourceDef resource = DataBase<ResourceDef>.Find(resourceId);
        return workshop != null && HasPositiveRate(workshop.ResourceRequirements, resource);
    }

    private static bool HasResearchBuildingEffect(string researchId, string buildingId)
    {
        ResearchDef research = DataBase<ResearchDef>.Find(researchId);
        if (research == null)
            return false;
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect != null &&
                effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
                effect.Building != null &&
                effect.Building.Id == buildingId &&
                effect.NumericValue > ExpantaNum.One)
                return true;
        }
        return false;
    }

    private static bool HasResourceProductionEffect(string workshopId, string resourceId)
    {
        WorkshopUpgradeDef workshop = DataBase<WorkshopUpgradeDef>.Find(workshopId);
        ResourceDef resource = DataBase<ResourceDef>.Find(resourceId);
        if (workshop == null || resource == null)
            return false;
        for (int i = 0; i < workshop.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = workshop.Effects[i];
            if (effect != null &&
                effect.Type == WorkshopEffectType.ResourceProductionMultiplier &&
                effect.Resource == resource &&
                effect.NumericValue > ExpantaNum.One)
                return true;
        }
        return false;
    }
}
