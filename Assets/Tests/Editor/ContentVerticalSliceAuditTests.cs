using NUnit.Framework;

public sealed class ContentVerticalSliceAuditTests
{
    [Test]
    public void AnimalAndMetalRawChains_HaveConcreteSources()
    {
        Assert.That(HasGeneration("ClayPit", "Clay"), Is.True);
        Assert.That(HasGeneration("FiberGatheringCamp", "Biomass"), Is.True);
        Assert.That(HasGeneration("MetalMine", "BasicMetalOre"), Is.True);
    }

    [Test]
    public void StoneAgeProcessingChains_HaveOutputAndInputFlow()
    {
        Assert.That(HasGeneration("CeramicKiln", "Ceramic"), Is.True);
        Assert.That(HasConsumption("CeramicKiln", "Clay"), Is.True);
        Assert.That(HasGeneration("WeavingWorkshop", "Cloth"), Is.True);
        Assert.That(HasConsumption("WeavingWorkshop", "Biomass"), Is.True);
    }

    [Test]
    public void MetalSmelter_ConsumesBasicOreAndCoalAndProducesBasicIngots()
    {
        Assert.That(HasConsumption("MetalSmelter", "BasicMetalOre"), Is.True);
        Assert.That(HasConsumption("MetalSmelter", "Coal"), Is.True);
        Assert.That(HasGeneration("MetalSmelter", "BasicMetalIngot"), Is.True);
    }

    [Test]
    public void VerticalSliceTransitionResearches_AdvanceTheirTechLevels()
    {
        ResearchDef stoneAge = DataBase<ResearchDef>.Find("StoneAgeTransition");
        ResearchDef settlement = DataBase<ResearchDef>.Find("StoneAgeSettlement");
        ResearchDef medieval = DataBase<ResearchDef>.Find("FeudalAdministration");

        Assert.That(stoneAge.AdvancesTechLevel, Is.True);
        Assert.That(stoneAge.TechLevel, Is.EqualTo(TechLevel.StoneAge));
        Assert.That(HasPrerequisite(stoneAge, "CivilizationPlanning"), Is.True);
        Assert.That(settlement.AdvancesTechLevel, Is.False);
        Assert.That(settlement.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(HasPrerequisite(settlement, "AnimalHusbandry"), Is.True);
        Assert.That(HasPrerequisite(settlement, "Agriculture"), Is.True);
        Assert.That(HasPrerequisite(settlement, "ClayExtraction"), Is.True);
        foreach (ResearchDef prerequisite in stoneAge.Prerequisites)
            Assert.That(prerequisite.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(medieval.AdvancesTechLevel, Is.True);
        Assert.That(medieval.TechLevel, Is.EqualTo(TechLevel.Medieval));
    }

    [Test]
    public void EraTransitionsExposeResearchAndResourceChecklist()
    {
        AssertPrerequisites(
            "StoneAgeSettlement",
            new[] { "Agriculture", "AnimalHusbandry", "ClayExtraction", "Quarry" });
        AssertTransitionRequirements(
            "StoneAgeSettlement",
            new[] { "WoodLog", "StoneChunk", "Clay", "Biomass" });
        AssertPrerequisites("CivilizationPlanning", new[] { "StoneAgeSettlement" });
        AssertPrerequisites("StoneAgeTransition", new[] { "CivilizationPlanning" });
        Assert.That(DataBase<ResearchDef>.Find("StoneAgeTransition").ResourceRequirements, Is.Empty,
            "The transition does not charge the settlement materials a second time.");
        AssertPrerequisites(
            "FeudalAdministration",
            new[] { "BronzeAlloyCrafting", "IronworkingAndSteelmaking", "WrittenRecords" });
        AssertTransitionRequirements(
            "FeudalAdministration",
            new[] { "StoneBrick", "Cloth" });
        AssertPrerequisites(
            "Industrialization",
            new[] { "MechanicalEngineering", "Steelmaking" });
        AssertTransitionRequirements(
            "Industrialization",
            new[] { "Steel" });
        AssertPrerequisites(
            "OrbitalEngineering",
            new[] { "PowerGridEngineering", "CombustionEngines", "TitaniumAlloyEngineering" });
        AssertTransitionRequirements(
            "OrbitalEngineering",
            new[] { "BasicMetalIngot", "AdvancedMetalIngot", "TitaniumAlloy", "Concrete", "Electronics", "RefinedFuel" });
        AssertPrerequisites(
            "TechnologicalSingularity",
            new[] { "PhaseFieldNavigation", "QuantumComputing" });

        TechLevel[] currentEras =
        {
            TechLevel.Animal,
            TechLevel.StoneAge,
            TechLevel.Medieval,
            TechLevel.Industrial,
            TechLevel.Spacer
        };
        for (int i = 0; i < currentEras.Length; i++)
        {
            TechLevel targetEra = (TechLevel)((int)currentEras[i] + 1);
            ResearchDef transition = EraGoalEvaluator.FindTransition(targetEra);
            Assert.That(transition.AdvancesTechLevel, Is.True, transition.Id);
            Assert.That(transition.Prerequisites, Is.Not.Empty, transition.Id);
            if (transition.Id == "StoneAgeTransition")
                Assert.That(transition.ResourceRequirements, Is.Empty,
                    "Settlement already paid the first era's material cost.");
            else
                Assert.That(transition.ResourceRequirements, Is.Not.Empty, transition.Id);
            Assert.That(ExpantaNum.Parse(transition.BaseCost), Is.GreaterThan(ExpantaNum.Zero),
                "Every transition still requires actual research work: " + transition.Id);

            EraGoalEvaluation evaluation = EraGoalEvaluator.Evaluate(
                currentEras[i], null, null);
            Assert.That(evaluation.Transition, Is.SameAs(transition));
            AssertEvaluatorConditionsMatchTransition(transition, evaluation);
        }
    }

    private static void AssertTransitionRequirements(
        string researchId, string[] resourceIds)
    {
        ResearchDef transition = DataBase<ResearchDef>.Find(researchId);
        Assert.That(transition, Is.Not.Null, researchId);
        for (int i = 0; i < resourceIds.Length; i++)
        {
            ResourceDef expected = DataBase<ResourceDef>.Find(resourceIds[i]);
            Assert.That(expected, Is.Not.Null, resourceIds[i]);
            Assert.That(HasResourceRequirement(transition, expected), Is.True,
                researchId + " resource " + resourceIds[i]);
        }
    }

    private static void AssertEvaluatorConditionsMatchTransition(
        ResearchDef transition, EraGoalEvaluation evaluation)
    {
        Assert.That(evaluation.Conditions, Is.Not.Null, transition.Id);
        for (int i = 0; i < transition.Prerequisites.Count; i++)
        {
            ResearchDef prerequisite = transition.Prerequisites[i];
            Assert.That(HasResearchCondition(evaluation, prerequisite), Is.True,
                transition.Id + " prerequisite condition " + prerequisite.Id);
        }
        for (int i = 0; i < transition.ResourceRequirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = transition.ResourceRequirements[i];
            if (requirement == null || requirement.First == null ||
                requirement.Second <= ExpantaNum.Zero)
                continue;
            Assert.That(HasResourceCondition(evaluation, requirement.First), Is.True,
                transition.Id + " resource condition " + requirement.First.Id);
        }
    }

    private static bool HasResearchCondition(
        EraGoalEvaluation evaluation, ResearchDef prerequisite)
    {
        for (int i = 0; i < evaluation.Conditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = evaluation.Conditions[i];
            if (condition != null &&
                condition.Kind == EraGoalConditionKind.PrerequisiteResearch &&
                condition.Research == prerequisite)
                return true;
        }
        return false;
    }

    private static bool HasResourceCondition(
        EraGoalEvaluation evaluation, ResourceDef resource)
    {
        for (int i = 0; i < evaluation.Conditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = evaluation.Conditions[i];
            if (condition != null && condition.Kind == EraGoalConditionKind.Resource &&
                condition.Resource == resource && condition.RequiredAmount > ExpantaNum.Zero)
                return true;
        }
        return false;
    }

    private static bool HasResourceRequirement(ResearchDef research, ResourceDef resource)
    {
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = research.ResourceRequirements[i];
            if (requirement != null && requirement.First == resource &&
                requirement.Second > ExpantaNum.Zero)
                return true;
        }
        return false;
    }

    private static void AssertPrerequisites(string researchId, string[] prerequisiteIds)
    {
        ResearchDef transition = DataBase<ResearchDef>.Find(researchId);
        Assert.That(transition, Is.Not.Null, researchId);
        for (int i = 0; i < prerequisiteIds.Length; i++)
            Assert.That(HasPrerequisite(transition, prerequisiteIds[i]), Is.True,
                researchId + " prerequisite " + prerequisiteIds[i]);
    }

    [Test]
    public void CeramicFiring_RequiresTheClayExtractionResearchPath()
    {
        ResearchDef ceramicFiring = DataBase<ResearchDef>.Find("CeramicFiring");

        Assert.That(HasPrerequisite(ceramicFiring, "StoneProcessing"), Is.True);
        Assert.That(HasPrerequisite(ceramicFiring, "ClayExtraction"), Is.True);
    }

    private static bool HasGeneration(string buildingId, string resourceId)
    {
        return HasPair(DataBase<BuildingDef>.Find(buildingId).ResourceGenerationRates, resourceId);
    }

    private static bool HasConsumption(string buildingId, string resourceId)
    {
        return HasPair(DataBase<BuildingDef>.Find(buildingId).ResourceConsumptionRates, resourceId);
    }

    private static bool HasPair(
        System.Collections.Generic.IReadOnlyList<Pair<ResourceDef, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].First != null && pairs[i].First.Id == resourceId)
                return true;
        }

        return false;
    }

    private static bool HasPrerequisite(ResearchDef research, string researchId)
    {
        for (int i = 0; i < research.Prerequisites.Count; i++)
        {
            if (research.Prerequisites[i] != null && research.Prerequisites[i].Id == researchId)
                return true;
        }

        return false;
    }
}
