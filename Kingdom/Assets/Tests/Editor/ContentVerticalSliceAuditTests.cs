using NUnit.Framework;

public sealed class ContentVerticalSliceAuditTests
{
    [Test]
    public void AnimalAndMetalRawChains_HaveConcreteSources()
    {
        Assert.That(HasGeneration("ClayPit", "Clay"), Is.True);
        Assert.That(HasGeneration("FiberGatheringCamp", "PlantFiber"), Is.True);
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
        Assert.That(HasConsumption("WeavingWorkshop", "PlantFiber"), Is.True);
    }

    [Test]
    public void BronzeFoundry_ConsumesCopperTinAndCoal()
    {
        Assert.That(HasConsumption("BronzeFoundry", "Copper"), Is.True);
        Assert.That(HasConsumption("BronzeFoundry", "Tin"), Is.True);
        Assert.That(HasConsumption("BronzeFoundry", "Coal"), Is.True);
        Assert.That(HasGeneration("BronzeFoundry", "Bronze"), Is.True);
    }

    [Test]
    public void VerticalSliceTransitionResearches_AdvanceTheirTechLevels()
    {
        Research neolithic = DataBase<Research>.Find("NeolithicSettlement");
        Research medieval = DataBase<Research>.Find("SmithingRevolution");

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
