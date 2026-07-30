using System;
using NUnit.Framework;

public sealed class C6IndustrialClosureAuditTests
{
    private static readonly string[] IndustrialBuildingIds =
    {
        "SteamPlant",
        "OilDerrick",
        "SilicaQuarry",
        "CokeOven",
        "Glassworks",
        "MachineFactory",
        "ChemicalPlant",
        "OilRefinery",
        "WireMill",
        "University",
        "RailHub",
        "ArmsFactory",
        "IndustrialCopperSmelter",
        "IndustrialTinSmelter",
        "IndustrialBronzeFoundry",
        "BlastFurnace",
        "BauxiteMine",
        "AluminumSmelter",
        "ConcreteWorks",
        "CentralPowerStation"
    };

    private static readonly string[] IndustrialResourceIds =
    {
        "Machinery",
        "Chemical",
        "Electronics",
        "CrudeOil",
        "Silica",
        "Coke",
        "Glass",
        "IndustrialCeramic",
        "RefinedFuel",
        "Lubricant",
        "Rubber",
        "CopperWire",
        "PrecisionParts",
        "Engine",
        "Concrete",
        "BauxiteOre",
        "Aluminum"
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
    public void C604_PowerAndLogisticsRatesRemainDerivedOutsideSaveDtos()
    {
        foreach (Type fieldType in new[]
        {
            typeof(SaveManager.GameSaveData),
            typeof(SaveManager.KingdomSaveData)
        })
        {
            foreach (var field in fieldType.GetFields())
            {
                Assert.That(field.Name, Is.Not.EqualTo("PowerProductionRate"));
                Assert.That(field.Name, Is.Not.EqualTo("PowerConsumptionRate"));
                Assert.That(field.Name, Is.Not.EqualTo("LogisticsProductionRate"));
                Assert.That(field.Name, Is.Not.EqualTo("LogisticsConsumptionRate"));
            }
        }
    }
}
