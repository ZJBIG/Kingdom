using System.Linq;
using NUnit.Framework;

public sealed class SpacerResearchPlacementTests
{
    [Test]
    public void TechnologicalSingularityIsTheIndependentUltraTransition()
    {
        Research singularity = DataBase<Research>.Find("TechnologicalSingularity");

        Assert.That(singularity, Is.Not.Null);
        Assert.That(singularity.TechLevel, Is.EqualTo(TechLevel.Ultra));
        Assert.That(singularity.AdvancesTechLevel, Is.True);
        Assert.That(new ExpantaNum(singularity.BaseCost), Is.EqualTo(new ExpantaNum("5.5e11")));
        AssertPrerequisites(singularity,
            "QuantumComputing",
            "PhaseFieldNavigation",
            "MatterStateControlTheory",
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
            Assert.That(DataBase<Research>.TryFind(id, out _), Is.False, id);
        foreach (string id in workshopIds)
            Assert.That(DataBase<WorkshopUpgrade>.TryFind(id, out _), Is.False, id);
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
            "DeepSpaceSystemsTheory",
            "PlanetaryGeologyTheory",
            "SpaceWeatherForecastingTheory",
            "PlanetaryAtmosphereEngineering"
        };

        foreach (string id in earlyResearchIds)
        {
            Research research = DataBase<Research>.Find(id);
            Assert.That(research, Is.Not.Null, id);
            Assert.That(HasResource(research, "PhaseMaterial"), Is.False, id);
            Assert.That(HasResource(research, "PhantomAlloy"), Is.False, id);
            Assert.That(HasResource(research, "PhantomWeave"), Is.False, id);
        }
    }

    [Test]
    public void ResearchLabelsDescriptionsAndEffectsMatchTheirRoles()
    {
        Research quantum = DataBase<Research>.Find("QuantumComputing");
        Research material = DataBase<Research>.Find("MatterTransmutationTheory");
        Research systems = DataBase<Research>.Find("DeepSpaceSystemsTheory");
        Research geology = DataBase<Research>.Find("PlanetaryGeologyTheory");
        Research phase = DataBase<Research>.Find("PhaseFieldStabilizationTheory");
        Research causal = DataBase<Research>.Find("InterstellarCausalCoordination");

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
        WorkshopUpgrade trajectory = DataBase<WorkshopUpgrade>.Find("GravityAssistTrajectoryPlanner");
        WorkshopUpgrade thermal = DataBase<WorkshopUpgrade>.Find("DeepSpaceThermalControlArray");
        WorkshopUpgrade causal = DataBase<WorkshopUpgrade>.Find("CausalSynchronizationArray");
        WorkshopUpgrade solar = DataBase<WorkshopUpgrade>.Find("SolarWindMonitoringArray");
        WorkshopUpgrade material = DataBase<WorkshopUpgrade>.Find("ControlledMatterTransmutation");

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

    [Test]
    public void RevisedResearchGraphHasNoCycles()
    {
        Assert.That(
            ResearchValidator.ValidateNoCycles(DataBase<Research>.All, out string error),
            Is.True,
            error);
    }

    private static void AssertPrerequisites(Research research, params string[] ids)
    {
        Assert.That(research.Prerequisites.Select(item => item.Id), Is.EquivalentTo(ids));
    }

    private static void AssertResource(Research research, string id)
    {
        Pair<Resource, ExpantaNum> pair = research.ResourceRequirements
            .Single(item => item.First != null && item.First.Id == id);
        Assert.That(pair.Second, Is.GreaterThan(ExpantaNum.Zero), id);
    }

    private static bool HasResource(Research research, string id) =>
        research.ResourceRequirements.Any(item => item.First != null && item.First.Id == id);

    private static bool HasEffect(Research research, ResearchEffectType type) =>
        research.Effects.Any(effect => effect != null && effect.Type == type);

    private static void AssertWorkshopEffect(
        WorkshopUpgrade workshop,
        WorkshopEffectType type,
        string value)
    {
        Assert.That(workshop, Is.Not.Null);
        Assert.That(workshop.Effects.Any(effect =>
            effect != null &&
            effect.Type == type &&
            effect.NumericValue == new ExpantaNum(value)), Is.True, workshop.Id);
    }

    private static void AssertBuildingWorkshopEffect(
        WorkshopUpgrade workshop,
        WorkshopEffectType type,
        string buildingId,
        string value)
    {
        Assert.That(workshop, Is.Not.Null);
        Assert.That(workshop.Effects.Any(effect =>
            effect != null &&
            effect.Type == type &&
            effect.Building != null &&
            effect.Building.Id == buildingId &&
            effect.NumericValue == new ExpantaNum(value)), Is.True, workshop.Id);
    }
}
