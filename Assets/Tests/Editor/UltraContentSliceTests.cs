using System.Linq;
using NUnit.Framework;

public sealed class UltraContentSliceTests
{
    [Test]
    public void UltraResearchFormsAReachablePostSingularityChain()
    {
        ResearchDef phase = DataBase<ResearchDef>.Find("PhaseFieldEngineering");
        ResearchDef cognition = DataBase<ResearchDef>.Find("DistributedCognitionProtocol");
        ResearchDef assembly = DataBase<ResearchDef>.Find("AutonomousMatterAssembly");
        ResearchDef grid = DataBase<ResearchDef>.Find("PhaseGridSynchronization");
        ResearchDef coordination = DataBase<ResearchDef>.Find("InterstellarResourceCoordination");

        Assert.That(phase, Is.Not.Null);
        Assert.That(cognition, Is.Not.Null);
        Assert.That(assembly, Is.Not.Null);
        Assert.That(grid, Is.Not.Null);
        Assert.That(coordination, Is.Not.Null);
        Assert.That(phase.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(cognition.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(assembly.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(grid.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(coordination.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(HasPrerequisite(phase, "TechnologicalSingularity"), Is.True);
        Assert.That(HasPrerequisite(cognition, "PhaseFieldEngineering"), Is.True);
        Assert.That(HasPrerequisite(assembly, "DistributedCognitionProtocol"), Is.True);
        Assert.That(HasPrerequisite(grid, "PhaseFieldEngineering"), Is.True);
        Assert.That(HasPrerequisite(grid, "AutonomousMatterAssembly"), Is.True);
        Assert.That(HasPrerequisite(coordination, "PhaseGridSynchronization"), Is.True);
        Assert.That(HasPrerequisite(coordination, "DistributedCognitionProtocol"), Is.True);
        Assert.That(HasEffect(phase, ResearchEffectType.GlobalConstructionMultiplier), Is.True);
        Assert.That(HasEffect(cognition, ResearchEffectType.GlobalResearchMultiplier), Is.True);
        Assert.That(HasEffect(assembly, ResearchEffectType.GlobalBuildingProductionMultiplier), Is.True);
        Assert.That(HasEffect(grid, ResearchEffectType.PowerMultiplier), Is.True);
        Assert.That(HasEffect(coordination, ResearchEffectType.GlobalLogisticsMultiplier), Is.True);
    }

    [Test]
    public void UltraCivilizationEngineeringHasExactlyThreeOrderedStagesWithValidCosts()
    {
        UltraProjectDef definition = DataBase<UltraProjectDef>.Find(
            "UltraCivilizationEngineering");
        ResearchDef[] expectedResearch =
        {
            DataBase<ResearchDef>.Find("PhaseFieldEngineering"),
            DataBase<ResearchDef>.Find("AutonomousMatterAssembly"),
            DataBase<ResearchDef>.Find("UltraCampaignContinuity")
        };
        BuildingDef[] expectedBuildings =
        {
            DataBase<BuildingDef>.Find("PhaseEnergyArray"),
            DataBase<BuildingDef>.Find("AutonomousMatterFabricator"),
            DataBase<BuildingDef>.Find("AutonomousMatterFabricator")
        };
        string[] expectedStageIds =
        {
            "PhaseStabilityCertification",
            "MatterAutonomyCertification",
            "CivilizationContinuityCertification"
        };
        string[] expectedPrerequisites =
        {
            string.Empty,
            "PhaseStabilityCertification",
            "MatterAutonomyCertification"
        };

        Assert.That(definition.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(definition.Stages, Has.Count.EqualTo(3));

        for (int i = 0; i < definition.Stages.Count; i++)
        {
            UltraProjectStageDefinition stage = definition.Stages[i];
            Assert.That(stage.StageId, Is.EqualTo(expectedStageIds[i]));
            Assert.That(stage.PrerequisiteStageId, Is.EqualTo(expectedPrerequisites[i]));
            Assert.That(stage.RequiredResearch, Is.SameAs(expectedResearch[i]));
            Assert.That(stage.RequiredBuilding, Is.SameAs(expectedBuildings[i]));
            Assert.That(stage.BaseDurationSeconds, Is.GreaterThan(ExpantaNum.Zero));
            AssertValidResourceCosts(stage.OneTimeResourceCosts, stage.StageId + " one-time");
            AssertValidResourceCosts(stage.ContinuousResourceCosts, stage.StageId + " continuous");
        }

        UltraProjectStageDefinition continuity = definition.Stages[2];
        Assert.That(continuity.AdditionalRequiredResearch, Has.Count.EqualTo(1));
        Assert.That(continuity.AdditionalRequiredResearch[0], Is.SameAs(
            DataBase<ResearchDef>.Find("AdaptiveFleetLogistics")));
    }

    [Test]
    public void UltraBuildingsHaveDistinctResearchAndPowerRoles()
    {
        BuildingDef computing = DataBase<BuildingDef>.Find("UltraComputingNexus");
        BuildingDef energy = DataBase<BuildingDef>.Find("PhaseEnergyArray");
        BuildingDef fabricator = DataBase<BuildingDef>.Find("AutonomousMatterFabricator");

        Assert.That(computing, Is.Not.Null);
        Assert.That(energy, Is.Not.Null);
        Assert.That(fabricator, Is.Not.Null);
        Assert.That(computing.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(energy.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(fabricator.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(computing.ResearchPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(computing.PowerContribution, Is.LessThan(ExpantaNum.Zero));
        Assert.That(energy.PowerContribution, Is.GreaterThan(ExpantaNum.Zero));
                Assert.That(HasRequiredResearch(computing, "AutonomousMatterAssembly"), Is.True);
        Assert.That(HasRequiredResearch(energy, "PhaseFieldEngineering"), Is.True);
        Assert.That(HasRequiredResearch(fabricator, "PhaseGridSynchronization"), Is.True);
        Assert.That(HasPositiveRate(fabricator.ResourceGenerationRates, DataBase<ResourceDef>.Find("PhantomAlloy")), Is.True);
        Assert.That(HasPositiveRate(fabricator.ResourceGenerationRates, DataBase<ResourceDef>.Find("PhantomWeave")), Is.True);
        Assert.That(HasPositiveRate(fabricator.ResourceGenerationRates, DataBase<ResourceDef>.Find("PhaseMaterial")), Is.True);
        Assert.That(HasPositiveRate(fabricator.ResourceGenerationRates, DataBase<ResourceDef>.Find("Composite")), Is.True);
        Assert.That(HasRequiredResearch(fabricator, "InterstellarResourceCoordination"), Is.True);
        Assert.That(fabricator.LogisticsContribution, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(fabricator.FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void UltraHasOneIntegratedFactoryWithUniqueStrategicMaterialOutputs()
    {
        BuildingDef[] ultraBuildings = DataBase<BuildingDef>.All
            .Where(item => item != null && item.TechLevel == TechLevel.Ultra)
            .ToArray();
        BuildingDef integratedFactory = DataBase<BuildingDef>.Find("AutonomousMatterFabricator");
        string[] strategicMaterialIds =
        {
            "PhantomAlloy", "PhantomWeave", "PhaseMaterial", "Composite"
        };

        Assert.That(ultraBuildings, Has.Length.EqualTo(3));
        BuildingDef[] ultraFactories = ultraBuildings
            .Where(item => item.ResourceGenerationRates.Any(rate => rate.Second > ExpantaNum.Zero))
            .ToArray();
        Assert.That(ultraFactories, Has.Length.EqualTo(1));
        Assert.That(ultraFactories[0], Is.SameAs(integratedFactory));

        foreach (string resourceId in strategicMaterialIds)
        {
            ResourceDef resource = DataBase<ResourceDef>.Find(resourceId);
            BuildingDef[] producers = ultraBuildings
                .Where(item => HasPositiveRate(item.ResourceGenerationRates, resource))
                .ToArray();

            Assert.That(producers, Has.Length.EqualTo(1), resourceId);
            Assert.That(producers[0], Is.SameAs(integratedFactory), resourceId);
        }
    }

    [Test]
    public void UltraBuildingsConsumeRoleSpecificSupportFlows()
    {
        BuildingDef computing = DataBase<BuildingDef>.Find("UltraComputingNexus");
        BuildingDef energy = DataBase<BuildingDef>.Find("PhaseEnergyArray");
        BuildingDef fabricator = DataBase<BuildingDef>.Find("AutonomousMatterFabricator");

        Assert.That(computing.ResearchPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(computing.FoodConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(computing.PowerContribution, Is.LessThan(ExpantaNum.Zero));
        Assert.That(computing.LogisticsContribution, Is.LessThan(ExpantaNum.Zero));
        Assert.That(computing.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));

        Assert.That(energy.PowerContribution, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(energy.LogisticsContribution, Is.LessThan(ExpantaNum.Zero));
        Assert.That(energy.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));

        Assert.That(fabricator.LogisticsContribution, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(fabricator.FoodConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(fabricator.PowerContribution, Is.LessThan(ExpantaNum.Zero));
        Assert.That(fabricator.FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(fabricator.DefensePowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(fabricator.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void UltraContentReusesExistingStrategicResources()
    {
        string[] expectedResources =
        {
            "Electronics", "Machinery", "TitaniumAlloy", "PhantomAlloy",
            "PhantomWeave", "PhaseMaterial", "Composite"
        };

        foreach (string id in expectedResources)
            Assert.That(DataBase<ResourceDef>.Find(id), Is.Not.Null, id);

        foreach (ResearchDef research in DataBase<ResearchDef>.All
                     .Where(item => item != null && item.TechLevel == TechLevel.Ultra))
        {
            Assert.That(research.ResourceRequirements, Is.Not.Empty, research.Id);
            Assert.That(research.Effects, Is.Not.Empty, research.Id);
        }
    }

    [Test]
    public void UltraResearchExtendsExpeditionFleetAndOccupationMechanisms()
    {
        ResearchDef expedition = DataBase<ResearchDef>.Find("UltraExpeditionDoctrine");
        ResearchDef repair = DataBase<ResearchDef>.Find("SelfRepairingFleetArchitecture");
        ResearchDef occupation = DataBase<ResearchDef>.Find("CausalOccupationNetwork");

        Assert.That(expedition, Is.Not.Null);
        Assert.That(repair, Is.Not.Null);
        Assert.That(occupation, Is.Not.Null);
        Assert.That(expedition.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(repair.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(occupation.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(expedition.ResourceRequirements, Is.Not.Empty);
        Assert.That(repair.ResourceRequirements, Is.Not.Empty);
        Assert.That(occupation.ResourceRequirements, Is.Not.Empty);
        Assert.That(HasPrerequisite(expedition, "InterstellarResourceCoordination"), Is.True);
        Assert.That(HasPrerequisite(expedition, "DeepSpaceSurvey"), Is.True);
        Assert.That(HasPrerequisite(repair, "FleetDamageControlTheory"), Is.True);
        Assert.That(HasPrerequisite(occupation, "InterstellarOccupationAdministration"), Is.True);
        Assert.That(HasEffect(expedition, ResearchEffectType.ExplorationPowerMultiplier), Is.True);
        Assert.That(HasEffect(repair, ResearchEffectType.FleetRepairCostMultiplier), Is.True);
        Assert.That(HasEffect(occupation, ResearchEffectType.OccupiedResourceProductionMultiplier), Is.True);
    }

    [Test]
    public void UltraResearchDeepensCampaignContinuitySupplyAndCasualtyMechanisms()
    {
        ResearchDef continuity = DataBase<ResearchDef>.Find("UltraCampaignContinuity");
        ResearchDef logistics = DataBase<ResearchDef>.Find("AdaptiveFleetLogistics");
        ResearchDef casualty = DataBase<ResearchDef>.Find("PredictiveCasualtyControl");

        Assert.That(continuity, Is.Not.Null);
        Assert.That(logistics, Is.Not.Null);
        Assert.That(casualty, Is.Not.Null);
        Assert.That(continuity.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(logistics.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(casualty.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(continuity.ResourceRequirements, Is.Not.Empty);
        Assert.That(logistics.ResourceRequirements, Is.Not.Empty);
        Assert.That(casualty.ResourceRequirements, Is.Not.Empty);
        Assert.That(HasPrerequisite(continuity, "UltraExpeditionDoctrine"), Is.True);
        Assert.That(HasPrerequisite(logistics, "SelfRepairingFleetArchitecture"), Is.True);
        Assert.That(HasPrerequisite(casualty, "CausalOccupationNetwork"), Is.True);
        Assert.That(HasEffect(continuity, ResearchEffectType.CampaignProgressMultiplier), Is.True);
        Assert.That(HasEffect(logistics, ResearchEffectType.CampaignSupplyCostMultiplier), Is.True);
        Assert.That(HasEffect(casualty, ResearchEffectType.CampaignCasualtyMultiplier), Is.True);
        Assert.That(FindEffect(continuity, ResearchEffectType.CampaignProgressMultiplier).NumericValue,
            Is.GreaterThan(new ExpantaNum(1.2d)));
        Assert.That(FindEffect(logistics, ResearchEffectType.CampaignSupplyCostMultiplier).NumericValue,
            Is.LessThan(new ExpantaNum(0.8d)));
        Assert.That(FindEffect(casualty, ResearchEffectType.CampaignCasualtyMultiplier).NumericValue,
            Is.LessThan(new ExpantaNum(0.8d)));
    }

    [Test]
    public void UltraResearchSupportsMegastructureEnergyEcologyAndProductivity()
    {
        ResearchDef energy = DataBase<ResearchDef>.Find("PhaseEnergyHarmonics");
        ResearchDef ecology = DataBase<ResearchDef>.Find("ClosedLoopAgroecology");
        ResearchDef productivity = DataBase<ResearchDef>.Find("AutonomousCivicProductivity");

        Assert.That(energy, Is.Not.Null);
        Assert.That(ecology, Is.Not.Null);
        Assert.That(productivity, Is.Not.Null);
        Assert.That(energy.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(ecology.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(productivity.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(HasPrerequisite(energy, "PhaseGridSynchronization"), Is.True);
        Assert.That(HasPrerequisite(ecology, "CausalOccupationNetwork"), Is.True);
        Assert.That(HasPrerequisite(productivity, "SelfRepairingFleetArchitecture"), Is.True);
        ResearchEffectDefinition energyEffect = FindEffect(
            energy, ResearchEffectType.BuildingPowerProductionMultiplier);
        Assert.That(energyEffect.Building, Is.Not.Null);
        Assert.That(energyEffect.Building.Id, Is.EqualTo("PhaseEnergyArray"));
        Assert.That(energyEffect.NumericValue, Is.GreaterThan(new ExpantaNum(1.2d)));
        Assert.That(FindEffect(ecology, ResearchEffectType.GlobalFoodProductionMultiplier).NumericValue,
            Is.GreaterThan(new ExpantaNum(1.1d)));
        Assert.That(FindEffect(productivity, ResearchEffectType.PopulationProductivityMultiplier).NumericValue,
            Is.GreaterThan(new ExpantaNum(1.1d)));
    }

    [Test]
    public void UltraWorkshopsExtendCapabilityMechanismsWithoutAddingFactories()
    {
        WorkshopUpgradeDef orchestration = DataBase<WorkshopUpgradeDef>.Find("AutonomousMatterOrchestration");
        WorkshopUpgradeDef compute = DataBase<WorkshopUpgradeDef>.Find("PhaseCognitiveCompute");
        WorkshopUpgradeDef supply = DataBase<WorkshopUpgradeDef>.Find("InterstellarSupplyMesh");

        Assert.That(orchestration, Is.Not.Null);
        Assert.That(compute, Is.Not.Null);
        Assert.That(supply, Is.Not.Null);
        Assert.That(orchestration.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(compute.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(supply.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(orchestration.ResourceRequirements, Is.Not.Empty);
        Assert.That(compute.ResourceRequirements, Is.Not.Empty);
        Assert.That(supply.ResourceRequirements, Is.Not.Empty);
        Assert.That(HasWorkshopResearch(orchestration, "AutonomousMatterAssembly"), Is.True);
        Assert.That(HasWorkshopResearch(compute, "DistributedCognitionProtocol"), Is.True);
        Assert.That(HasWorkshopResearch(supply, "InterstellarResourceCoordination"), Is.True);
        Assert.That(HasWorkshopEffect(orchestration, WorkshopEffectType.GlobalBuildingProductionMultiplier), Is.True);
        Assert.That(HasWorkshopEffect(compute, WorkshopEffectType.GlobalResearchMultiplier), Is.True);
        Assert.That(HasWorkshopEffect(supply, WorkshopEffectType.GlobalLogisticsMultiplier), Is.True);
    }

    private static bool HasPrerequisite(ResearchDef research, string id) =>
        research.Prerequisites.Any(item => item != null && item.Id == id);

    private static bool HasEffect(ResearchDef research, ResearchEffectType type) =>
        research.Effects.Any(effect => effect != null && effect.Type == type);

    private static ResearchEffectDefinition FindEffect(ResearchDef research, ResearchEffectType type) =>
        research.Effects.First(effect => effect != null && effect.Type == type);

    private static bool HasRequiredResearch(BuildingDef building, string id) =>
        building.RequiredResearch.Any(item => item != null && item.Id == id);

    private static bool HasWorkshopResearch(WorkshopUpgradeDef workshop, string id) =>
        workshop.RequiredResearch.Any(item => item != null && item.Id == id);

    private static bool HasWorkshopEffect(WorkshopUpgradeDef workshop, WorkshopEffectType type) =>
        workshop.Effects.Any(effect => effect != null && effect.Type == type);

    private static bool HasPositiveRate(
        System.Collections.Generic.IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates,
        ResourceDef resource)
    {
        if (rates == null || resource == null)
            return false;
        return rates.Any(item => item.First == resource && item.Second > ExpantaNum.Zero);
    }

    private static void AssertValidResourceCosts(
        System.Collections.Generic.IReadOnlyList<Pair<ResourceDef, ExpantaNum>> costs,
        string label)
    {
        Assert.That(costs, Is.Not.Null, label);
        Assert.That(costs, Is.Not.Empty, label);
        Assert.That(costs.All(item => item.First != null &&
                                     item.Second.IsFinite &&
                                     item.Second > ExpantaNum.Zero), Is.True, label);
        Assert.That(costs.Select(item => item.First).Distinct().Count(),
            Is.EqualTo(costs.Count), label);
    }
}
