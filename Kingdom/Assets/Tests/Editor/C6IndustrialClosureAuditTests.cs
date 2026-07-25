using System;
using NUnit.Framework;

public sealed class C6IndustrialClosureAuditTests
{
    private static readonly string[] IndustrialBuildingIds =
    {
        "SteamPlant",
        "MachineFactory",
        "ChemicalPlant",
        "University",
        "RailHub",
        "ArmsFactory"
    };

    private static readonly string[] IndustrialResourceIds =
    {
        "Machinery",
        "Chemical",
        "Electronics"
    };

    [Test]
    public void C604_NewGameReachesIndustrialAndTheWholeIndustrialLayer()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        Assert.That(result.HighestTechLevel, Is.GreaterThanOrEqualTo(TechLevel.Industrial),
            result.FormatFailureReport());
        Assert.That(result.UnreachableResearch, Is.Empty, result.FormatFailureReport());

        for (int i = 0; i < IndustrialBuildingIds.Length; i++)
            Assert.That(result.UnreachableBuildings, Does.Not.Contain(IndustrialBuildingIds[i]),
                result.FormatFailureReport());

        for (int i = 0; i < IndustrialResourceIds.Length; i++)
        {
            Assert.That(result.ResourcesWithoutSource, Does.Not.Contain(IndustrialResourceIds[i]),
                result.FormatFailureReport());
            Assert.That(result.ResourcesWithoutSink, Does.Not.Contain(IndustrialResourceIds[i]),
                result.FormatFailureReport());
        }
    }

    [Test]
    public void C604_PowerAndLogisticsRemainDerivedOutsideSaveDtos()
    {
        foreach (Type fieldType in new[]
        {
            typeof(SaveManager.GameSaveData),
            typeof(SaveManager.KingdomSaveData)
        })
        {
            foreach (var field in fieldType.GetFields())
            {
                Assert.That(field.Name, Does.Not.Contain("Power"));
                Assert.That(field.Name, Does.Not.Contain("Logistics"));
            }
        }
    }
}
