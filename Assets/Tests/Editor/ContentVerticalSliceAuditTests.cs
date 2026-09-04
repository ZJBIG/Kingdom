using NUnit.Framework;

public sealed class ContentVerticalSliceAuditTests
{
    [Test]
    public void AnimalAndMetalRawChains_HaveConcreteSources()
    {
        Assert.That(HasGeneration("ClayPit", "Clay"), Is.True);
        Assert.That(HasGeneration("FiberGatheringCamp", "Biomass"), Is.True);
        Assert.That(DataBase<Building>.Contains("StoneToolWorkshop"), Is.False);
        Assert.That(DataBase<Resource>.Contains("StoneTool"), Is.False);
        Assert.That(HasGeneration("MetalMine", "CopperOre"), Is.True);
        Assert.That(HasGeneration("MetalMine", "TinOre"), Is.True);
        Assert.That(HasGeneration("MetalMine", "IronOre"), Is.True);
        Assert.That(DataBase<Building>.Contains("CopperMine"), Is.False);
        Assert.That(DataBase<Building>.Contains("TinMine"), Is.False);
        Assert.That(DataBase<Building>.Contains("IronMine"), Is.False);
        Assert.That(DataBase<Building>.Contains("CopperSmelter"), Is.False);
        Assert.That(DataBase<Building>.Contains("TinSmelter"), Is.False);
        Assert.That(DataBase<Building>.Contains("IronSmelter"), Is.False);
        Assert.That(DataBase<Building>.Contains("IndustrialCopperSmelter"), Is.False);
        Assert.That(DataBase<Building>.Contains("IndustrialTinSmelter"), Is.False);
        Assert.That(DataBase<Research>.Contains("IndustrialCopperSmelting"), Is.False);
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
    public void MetalSmelter_ConsumesOresAndCoalAndProducesBronze()
    {
        Assert.That(HasConsumption("MetalSmelter", "CopperOre"), Is.True);
        Assert.That(HasConsumption("MetalSmelter", "TinOre"), Is.True);
        Assert.That(HasConsumption("MetalSmelter", "Coal"), Is.True);
        Assert.That(HasGeneration("MetalSmelter", "Bronze"), Is.True);
    }

    [Test]
    public void VerticalSliceTransitionResearches_AdvanceTheirTechLevels()
    {
        Research stoneAge = DataBase<Research>.Find("StoneAgeSettlement");
        Research medieval = DataBase<Research>.Find("FeudalAdministration");

        Assert.That(stoneAge.AdvancesTechLevel, Is.True);
        Assert.That(stoneAge.TechLevel, Is.EqualTo(TechLevel.StoneAge));
        Assert.That(HasPrerequisite(stoneAge, "AnimalHusbandry"), Is.True);
        Assert.That(HasPrerequisite(stoneAge, "Agriculture"), Is.True);
        Assert.That(HasPrerequisite(stoneAge, "ClayExtraction"), Is.True);
        foreach (Research prerequisite in stoneAge.Prerequisites)
            Assert.That(prerequisite.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(medieval.AdvancesTechLevel, Is.True);
        Assert.That(medieval.TechLevel, Is.EqualTo(TechLevel.Medieval));
    }

    [Test]
    public void EraTransitionsExposeResearchAndResourceChecklist()
    {
        AssertPrerequisites(
            "StoneAgeSettlement",
            new[] { "Agriculture", "AnimalHusbandry", "ClayExtraction" });
        AssertTransitionRequirements(
            "StoneAgeSettlement",
            new[] { "WoodLog", "StoneChunk", "Clay", "Biomass" });
        AssertPrerequisites(
            "FeudalAdministration",
            new[] { "Smithing_Bronze", "Smithing_Iron", "WrittenRecords" });
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
            "InterstellarNavigation",
            new[] { "Industrialization", "TitaniumAlloyEngineering" });
        AssertTransitionRequirements(
            "InterstellarNavigation",
            new[] { "RocketFuel", "TitaniumAlloy", "CopperWire", "Electronics" });
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
            Research transition = EraGoalEvaluator.FindTransition(targetEra);
            Assert.That(transition, Is.Not.Null,
                "Missing transition research for " + targetEra + ".");
            Assert.That(transition.AdvancesTechLevel, Is.True, transition.Id);
            Assert.That(transition.Prerequisites, Is.Not.Empty, transition.Id);
            Assert.That(transition.ResourceRequirements, Is.Not.Empty, transition.Id);

            EraGoalEvaluation evaluation = EraGoalEvaluator.Evaluate(
                currentEras[i], null, null);
            Assert.That(evaluation.Transition, Is.SameAs(transition));
            AssertEvaluatorConditionsMatchTransition(transition, evaluation);
        }
    }

    private static void AssertTransitionRequirements(
        string researchId, string[] resourceIds)
    {
        Research transition = DataBase<Research>.Find(researchId);
        Assert.That(transition, Is.Not.Null, researchId);
        for (int i = 0; i < resourceIds.Length; i++)
        {
            Resource expected = DataBase<Resource>.Find(resourceIds[i]);
            Assert.That(expected, Is.Not.Null, resourceIds[i]);
            Assert.That(HasResourceRequirement(transition, expected), Is.True,
                researchId + " resource " + resourceIds[i]);
        }
    }

    private static void AssertEvaluatorConditionsMatchTransition(
        Research transition, EraGoalEvaluation evaluation)
    {
        Assert.That(evaluation.Conditions, Is.Not.Null, transition.Id);
        for (int i = 0; i < transition.Prerequisites.Count; i++)
        {
            Research prerequisite = transition.Prerequisites[i];
            Assert.That(HasResearchCondition(evaluation, prerequisite), Is.True,
                transition.Id + " prerequisite condition " + prerequisite.Id);
        }
        for (int i = 0; i < transition.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = transition.ResourceRequirements[i];
            if (requirement == null || requirement.First == null ||
                requirement.Second <= ExpantaNum.Zero)
                continue;
            Assert.That(HasResourceCondition(evaluation, requirement.First), Is.True,
                transition.Id + " resource condition " + requirement.First.Id);
        }
    }

    private static bool HasResearchCondition(
        EraGoalEvaluation evaluation, Research prerequisite)
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
        EraGoalEvaluation evaluation, Resource resource)
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

    private static bool HasResourceRequirement(Research research, Resource resource)
    {
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = research.ResourceRequirements[i];
            if (requirement != null && requirement.First == resource &&
                requirement.Second > ExpantaNum.Zero)
                return true;
        }
        return false;
    }

    private static void AssertPrerequisites(string researchId, string[] prerequisiteIds)
    {
        Research transition = DataBase<Research>.Find(researchId);
        Assert.That(transition, Is.Not.Null, researchId);
        for (int i = 0; i < prerequisiteIds.Length; i++)
            Assert.That(HasPrerequisite(transition, prerequisiteIds[i]), Is.True,
                researchId + " prerequisite " + prerequisiteIds[i]);
    }

    [Test]
    public void CeramicFiring_RequiresTheClayExtractionResearchPath()
    {
        Research ceramicFiring = DataBase<Research>.Find("CeramicFiring");

        Assert.That(HasPrerequisite(ceramicFiring, "StoneTools"), Is.True);
        Assert.That(HasPrerequisite(ceramicFiring, "ClayExtraction"), Is.True);
    }

    [Test]
    public void ProgressionAudit_AllowsTheNextEraTransitionResearch()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        Assert.That(result.HighestTechLevel, Is.GreaterThanOrEqualTo(TechLevel.StoneAge),
            result.FormatFailureReport());
        Assert.That(result.UnreachableResearch, Does.Not.Contain("StoneAgeSettlement"),
            result.FormatFailureReport());
    }

    [Test]
    public void TinOre_UsesStableIdAndCorrectDisplayLabel()
    {
        Resource tinOre = DataBase<Resource>.Find("TinOre");

        Assert.That(tinOre.Id, Is.EqualTo("TinOre"));
        StringAssert.Contains("锡矿石", tinOre.Label);
    }

    private static bool HasGeneration(string buildingId, string resourceId)
    {
        return HasPair(DataBase<Building>.Find(buildingId).ResourceGenerationRates, resourceId);
    }

    private static bool HasConsumption(string buildingId, string resourceId)
    {
        return HasPair(DataBase<Building>.Find(buildingId).ResourceConsumptionRates, resourceId);
    }

    private static bool HasPair(
        System.Collections.Generic.IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].First != null && pairs[i].First.Id == resourceId)
                return true;
        }

        return false;
    }

    private static bool HasPrerequisite(Research research, string researchId)
    {
        for (int i = 0; i < research.Prerequisites.Count; i++)
        {
            if (research.Prerequisites[i] != null && research.Prerequisites[i].Id == researchId)
                return true;
        }

        return false;
    }
}
