using System.Linq;
using NUnit.Framework;

public sealed class SpacerResearchPlacementTests
{
    [Test]
    public void TechnologicalSingularityIsTheIndependentUltraTransition()
    {
        ResearchDef singularity = DataBase<ResearchDef>.Find("TechnologicalSingularity");

        Assert.That(singularity.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(singularity.AdvancesTechLevel, Is.True);
        Assert.That(new ExpantaNum(singularity.BaseCost), Is.EqualTo(new ExpantaNum("5.5e11")));
        AssertPrerequisites(singularity,
            "QuantumComputing",
            "PhaseFieldNavigation",
            "MaterialLifecycleControl",
            "InterstellarKnowledgeCoordination");
        AssertResource(singularity, "Electronics");
        AssertResource(singularity, "TitaniumAlloy");
        AssertResource(singularity, "Composite");
        AssertResource(singularity, "PhaseMaterial");
        AssertResource(singularity, "PhantomAlloy");
        AssertResource(singularity, "PhantomWeave");
    }

    [Test]
    public void RemovedDuplicateResearchAndWorkshopsAreAbsent()
    {
        string[] researchIds =
        {
            "DeepSpaceEducationTheory",
            "CrossSectorMetrologyTheory",
            "InterstellarSettlementAccountingTheory",
            "InterstellarCampaignStrategyTheory"
        };
        string[] workshopIds =
        {
            "DeepSpaceEducationSimulationLab",
            "CrossSectorMetrologyArray",
            "InterstellarClearingNode",
            "FleetSituationalSimulationArray"
        };

        foreach (string id in researchIds)
            Assert.That(DataBase<ResearchDef>.TryFind(id, out _), Is.False, id);
        foreach (string id in workshopIds)
            Assert.That(DataBase<WorkshopUpgradeDef>.TryFind(id, out _), Is.False, id);
    }

    [Test]
    public void EarlySpacerFoundationDoesNotRequireLatePhaseMaterials()
    {
        string[] earlyResearchIds =
        {
            "OrbitalLogisticsInfrastructure",
            "DeepSpaceSurvey",
            "DeepSpaceShipbuilding",
            "DeepSpaceFleet",
            "BioregenerativeLifeSupport",
            "OrbitalPropellantEngineering",
            "OrbitalStructuralDynamicsTheory",
            "GravityAssistTrajectoryTheory",
            "DeepSpaceNavigationReliability",
            "DeepSpaceThermalExchangeTheory",
            "DeepSpaceBiosecurityTheory",
            "DeepSpacePowerGridResilience",
            "DeepSpaceLogisticsReliability",
            "PlanetaryGeologyTheory",
            "SpaceWeatherForecastingTheory",
            "PlanetaryAtmosphereEngineering"
        };

        foreach (string id in earlyResearchIds)
        {
            ResearchDef research = DataBase<ResearchDef>.Find(id);
            Assert.That(research, Is.Not.Null, id);
            Assert.That(HasResource(research, "PhaseMaterial"), Is.False, id);
            Assert.That(HasResource(research, "PhantomAlloy"), Is.False, id);
            Assert.That(HasResource(research, "PhantomWeave"), Is.False, id);
        }
    }

    [Test]
    public void ResearchLabelsDescriptionsAndEffectsMatchTheirRoles()
    {
        ResearchDef quantum = DataBase<ResearchDef>.Find("QuantumComputing");
        ResearchDef material = DataBase<ResearchDef>.Find("MatterTransmutationTheory");
        ResearchDef systems = DataBase<ResearchDef>.Find("DeepSpaceLogisticsReliability");
        ResearchDef geology = DataBase<ResearchDef>.Find("PlanetaryGeologyTheory");
        ResearchDef phase = DataBase<ResearchDef>.Find("PhaseMaterialDurabilityTheory");
        ResearchDef causal = DataBase<ResearchDef>.Find("InterstellarCampaignTimingTheory");

        Assert.That(HasEffect(systems, ResearchEffectType.GlobalResearchMultiplier), Is.False);
        Assert.That(HasEffect(systems, ResearchEffectType.GlobalLogisticsMultiplier), Is.True);
        Assert.That(geology.Effects.Any(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
            effect.Building != null &&
            effect.Building.Id == "OrbitalResourceExtractionArray"), Is.True);
        Assert.That(phase.Effects.Any(effect =>
            effect != null &&
            effect.Type == ResearchEffectType.ResourceProductionMultiplier &&
            effect.Resource != null &&
            effect.Resource.Id == "PhaseMaterial"), Is.True);
        Assert.That(HasEffect(causal, ResearchEffectType.GlobalResearchMultiplier), Is.False);
        Assert.That(HasEffect(causal, ResearchEffectType.CampaignProgressMultiplier), Is.True);
    }

    [Test]
    public void WorkshopEffectsMatchTheirDescriptions()
    {
        WorkshopUpgradeDef trajectory = DataBase<WorkshopUpgradeDef>.Find("GravityAssistTrajectoryPlanner");
        WorkshopUpgradeDef thermal = DataBase<WorkshopUpgradeDef>.Find("DeepSpaceThermalControlArray");
        WorkshopUpgradeDef causal = DataBase<WorkshopUpgradeDef>.Find("CampaignTimingCalibrationBench");
        WorkshopUpgradeDef solar = DataBase<WorkshopUpgradeDef>.Find("SolarWindMonitoringArray");
        WorkshopUpgradeDef material = DataBase<WorkshopUpgradeDef>.Find("ControlledMatterTransmutation");

        AssertWorkshopEffect(trajectory, WorkshopEffectType.ExplorationPowerMultiplier, "1.18");
        AssertBuildingWorkshopEffect(
            thermal,
            WorkshopEffectType.BuildingProductionMultiplier,
            "OrbitalCryogenicPropellantArray",
            "1.18");
        AssertWorkshopEffect(causal, WorkshopEffectType.CampaignSupplyCostMultiplier, "0.94");
        AssertBuildingWorkshopEffect(
            solar,
            WorkshopEffectType.BuildingPowerProductionMultiplier,
            "OrbitalSolarArray",
            "1.18");
    }

    private static void AssertPrerequisites(ResearchDef research, params string[] ids)
    {
        Assert.That(research.Prerequisites.Select(item => item.Id), Is.EquivalentTo(ids));
    }

    private static void AssertResource(ResearchDef research, string id)
    {
        Pair<ResourceDef, ExpantaNum> pair = research.ResourceRequirements
            .Single(item => item.First != null && item.First.Id == id);
        Assert.That(pair.Second, Is.GreaterThan(ExpantaNum.Zero), id);
    }

    private static bool HasResource(ResearchDef research, string id) =>
        research.ResourceRequirements.Any(item => item.First != null && item.First.Id == id);

    private static bool HasEffect(ResearchDef research, ResearchEffectType type) =>
        research.Effects.Any(effect => effect != null && effect.Type == type);

    private static void AssertWorkshopEffect(
        WorkshopUpgradeDef workshop,
        WorkshopEffectType type,
        string value)
    {
        Assert.That(workshop.Effects.Any(effect =>
            effect != null &&
            effect.Type == type &&
            effect.NumericValue == new ExpantaNum(value)), Is.True, workshop.Id);
    }

    private static void AssertBuildingWorkshopEffect(
        WorkshopUpgradeDef workshop,
        WorkshopEffectType type,
        string buildingId,
        string value)
    {
        Assert.That(workshop.Effects.Any(effect =>
            effect != null &&
            effect.Type == type &&
            effect.Building != null &&
            effect.Building.Id == buildingId &&
            effect.NumericValue == new ExpantaNum(value)), Is.True, workshop.Id);
    }
}
