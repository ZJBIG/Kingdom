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
    public void NeolithicProcessingChains_HaveOutputAndInputFlow()
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
        Research neolithic = DataBase<Research>.Find("NeolithicSettlement");
        Research medieval = DataBase<Research>.Find("FeudalAdministration");

        Assert.That(neolithic.AdvancesTechLevel, Is.True);
        Assert.That(neolithic.TechLevel, Is.EqualTo(TechLevel.Neolithic));
        Assert.That(HasPrerequisite(neolithic, "AnimalHusbandry"), Is.True);
        Assert.That(HasPrerequisite(neolithic, "Agriculture"), Is.True);
        Assert.That(HasPrerequisite(neolithic, "ClayExtraction"), Is.True);
        foreach (Research prerequisite in neolithic.Prerequisites)
            Assert.That(prerequisite.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(medieval.AdvancesTechLevel, Is.True);
        Assert.That(medieval.TechLevel, Is.EqualTo(TechLevel.Medieval));
    }

    [Test]
    public void EraTransitionsExposeResearchAndResourceChecklist()
    {
        AssertPrerequisites(
            "NeolithicSettlement",
            new[] { "Agriculture", "AnimalHusbandry", "ClayExtraction" });
        AssertTransitionRequirements(
            "NeolithicSettlement",
            new[] { "WoodLog", "StoneChunk", "Clay", "Biomass" },
            new[] { "2000", "1200", "800", "800" });
        AssertPrerequisites(
            "FeudalAdministration",
            new[] { "Smithing_Bronze", "Smithing_Iron", "WrittenRecords" });
        AssertTransitionRequirements(
            "FeudalAdministration",
            new[] { "StoneBrick", "Cloth" },
            new[] { "5000", "2000" });
        AssertPrerequisites(
            "Industrialization",
            new[] { "MechanicalEngineering", "Steelmaking" });
        AssertTransitionRequirements(
            "Industrialization",
            new[] { "Steel" },
            new[] { "50000" });
        AssertPrerequisites(
            "InterstellarNavigation",
            new[] { "Industrialization", "TitaniumAlloyEngineering" });
        AssertTransitionRequirements(
            "InterstellarNavigation",
            new[] { "RocketFuel", "TitaniumAlloy", "CopperWire", "Electronics" },
            new[] { "1250", "1250", "6000", "7000" });
        AssertPrerequisites(
            "TechnologicalSingularity",
            new[] { "PhaseFieldNavigation", "QuantumComputing" });

        TechLevel[] currentEras =
        {
            TechLevel.Animal,
            TechLevel.Neolithic,
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
            Assert.That(evaluation.Conditions.Count,
                Is.GreaterThanOrEqualTo(transition.Prerequisites.Count), transition.Id);
        }
    }

    private static void AssertTransitionRequirements(
        string researchId, string[] resourceIds, string[] amounts)
    {
        Research transition = DataBase<Research>.Find(researchId);
        Assert.That(transition, Is.Not.Null, researchId);
        Assert.That(resourceIds.Length, Is.EqualTo(amounts.Length));
        Assert.That(transition.ResourceRequirements.Count,
            Is.EqualTo(resourceIds.Length), researchId);
        for (int i = 0; i < resourceIds.Length; i++)
        {
            Pair<Resource, ExpantaNum> requirement = transition.ResourceRequirements[i];
            Assert.That(requirement.First, Is.SameAs(DataBase<Resource>.Find(resourceIds[i])),
                researchId + " resource " + resourceIds[i]);
            Assert.That(requirement.Second, Is.EqualTo(new ExpantaNum(amounts[i])),
                researchId + " amount " + resourceIds[i]);
        }
    }

    private static void AssertPrerequisites(string researchId, string[] prerequisiteIds)
    {
        Research transition = DataBase<Research>.Find(researchId);
        Assert.That(transition, Is.Not.Null, researchId);
        Assert.That(transition.Prerequisites.Count, Is.EqualTo(prerequisiteIds.Length), researchId);
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

        Assert.That(result.HighestTechLevel, Is.GreaterThanOrEqualTo(TechLevel.Neolithic),
            result.FormatFailureReport());
        Assert.That(result.UnreachableResearch, Does.Not.Contain("NeolithicSettlement"),
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
