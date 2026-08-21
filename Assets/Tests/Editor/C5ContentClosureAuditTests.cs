using System.Collections.Generic;
using NUnit.Framework;

public sealed class C5ContentClosureAuditTests
{
    private static readonly string[] C5ResearchIds =
    {
        "FeudalAdministration",
        "MechanicalEngineering",
        "Steelmaking",
        "Bookmaking",
        "TradeRoutes",
        "Fortification",
        "StandingArmy",
        "Gunpowder",
        "Industrialization"
    };

    private static readonly string[] C5BuildingIds =
    {
        "Academy",
        "Caravanserai",
        "SteelForge",
        "Library",
        "TownHouse",
    };

    [Test]
    public void C504_NewGameCanReachIndustrializationThroughTheContentGraph()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        Assert.That(result.HighestTechLevel, Is.GreaterThanOrEqualTo(TechLevel.Industrial),
            result.FormatFailureReport());

        for (int i = 0; i < C5ResearchIds.Length; i++)
            Assert.That(result.UnreachableResearch, Does.Not.Contain(C5ResearchIds[i]),
                result.FormatFailureReport());

        for (int i = 0; i < C5BuildingIds.Length; i++)
            Assert.That(result.UnreachableBuildings, Does.Not.Contain(C5BuildingIds[i]),
                result.FormatFailureReport());
    }

    [Test]
    public void C504_MedievalStrategicResourcesHaveSourcesAndSinks()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        var strategicResources = new[] { "Steel", "Bronze" };
        for (int i = 0; i < strategicResources.Length; i++)
        {
            Assert.That(result.ResourcesWithoutSource, Does.Not.Contain(strategicResources[i]),
                string.Join(", ", result.ResourcesWithoutSource));
            Assert.That(result.ResourcesWithoutSink, Does.Not.Contain(strategicResources[i]),
                string.Join(", ", result.ResourcesWithoutSink));
        }
    }

    [Test]
    public void C504_AllResearchAndBuildingStableIdsRemainUnique()
    {
        var researchIds = new HashSet<string>();
        foreach (Research research in DataBase<Research>.All)
        {
            if (research == null)
                continue;
            Assert.That(researchIds.Add(research.Id), Is.True,
                $"Duplicate research ID '{research.Id}'.");
        }

        var buildingIds = new HashSet<string>();
        foreach (Building building in DataBase<Building>.All)
        {
            if (building == null)
                continue;
            Assert.That(buildingIds.Add(building.Id), Is.True,
                $"Duplicate building ID '{building.Id}'.");
        }
    }
}
