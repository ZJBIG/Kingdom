using System.Collections.Generic;
using NUnit.Framework;

public sealed class C6IndustrialContentTests
{
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

    [Test]
    public void C601_IndustrialResourcesUseTheHighTechDisplaySet()
    {
        for (int i = 0; i < IndustrialResourceIds.Length; i++)
        {
            Resource resource = DataBase<Resource>.Find(IndustrialResourceIds[i]);
            Assert.That(resource, Is.Not.Null);
            Assert.That(resource.DisplayerSet, Is.EqualTo(Resource.Set.UltraTechSet));
        }
    }

    [Test]
    public void C601_IndustrialBuildingsExistAtIndustrialTechLevel()
    {
        for (int i = 0; i < IndustrialBuildingIds.Length; i++)
        {
            Building building = DataBase<Building>.Find(IndustrialBuildingIds[i]);
            Assert.That(building, Is.Not.Null, $"Missing industrial building '{IndustrialBuildingIds[i]}'.");
            Assert.That(building.TechLevel, Is.EqualTo(TechLevel.Industrial));
            Assert.That(building.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));
        }
    }

    [Test]
    public void C602_IndustrialBuildingsDeclarePowerAndLogisticsFlows()
    {
        Assert.That(DataBase<Building>.Find("SteamPlant").PowerProductionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("MachineFactory").PowerConsumptionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("RailHub").LogisticsProductionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("ArmsFactory").LogisticsConsumptionRate,
            Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void C603_IndustrialBuildingsUseProductivityAndFlowsInsteadOfFood()
    {
        for (int i = 0; i < IndustrialBuildingIds.Length; i++)
        {
            Building building = DataBase<Building>.Find(IndustrialBuildingIds[i]);
            Assert.That(building.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero),
                $"Industrial building '{building.Id}' must not directly consume Food.");
            Assert.That(building.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(
                building.PowerProductionRate > ExpantaNum.Zero ||
                building.PowerConsumptionRate > ExpantaNum.Zero ||
                building.LogisticsProductionRate > ExpantaNum.Zero ||
                building.LogisticsConsumptionRate > ExpantaNum.Zero,
                $"Industrial building '{building.Id}' must declare a Power or Logistics flow.");
        }
    }

    [Test]
    public void C603_IndustrialEraRetainsCoalCopperIronAndSteelUses()
    {
        var legacyIndustrialResources = new[] { "Coal", "Copper", "Iron", "Steel" };
        for (int i = 0; i < legacyIndustrialResources.Length; i++)
        {
            Assert.That(CountConsumerBuildings(legacyIndustrialResources[i]), Is.GreaterThan(0),
                $"Industrial layer lost its use for '{legacyIndustrialResources[i]}'.");
        }
    }

    [Test]
    public void C604_IndustrialUpgradeChainsAreExplicitAndAcyclic()
    {
        var expected = new Dictionary<string, string>
        {
            ["CopperSmelter"] = "IndustrialCopperSmelter",
            ["TinSmelter"] = "IndustrialTinSmelter",
            ["BronzeFoundry"] = "IndustrialBronzeFoundry",
            ["SteelForge"] = "BlastFurnace",
            ["SteamPlant"] = "CentralPowerStation"
        };

        foreach (var pair in expected)
        {
            Building source = DataBase<Building>.Find(pair.Key);
            Building target = DataBase<Building>.Find(pair.Value);
            Assert.That(source.UpgradeTo, Is.SameAs(target),
                $"Building chain '{pair.Key}' must point to '{pair.Value}'.");
        }

        BuildingManager.ValidateBuildingChains(DataBase<Building>.All);
    }

    [Test]
    public void C601_IndustrialBuildingsOwnTheirResearchPrerequisites()
    {
        Research industrialization = DataBase<Research>.Find("Industrialization");
        var expected = new HashSet<string>(IndustrialBuildingIds);
        var actual = new HashSet<string>();

        foreach (Building building in DataBase<Building>.All)
        {
            if (building.TechLevel != TechLevel.Industrial)
                continue;
            Assert.That(building.RequiredResearch, Is.Not.Empty, building.Id);
            for (int i = 0; i < building.RequiredResearch.Count; i++)
                if (building.RequiredResearch[i] == industrialization)
                    actual.Add(building.Id);
        }

        Assert.That(actual, Is.SubsetOf(expected));
        Assert.That(actual, Does.Contain("SteamPlant"));
    }

    [Test]
    public void C601_IndustrialResourcesHaveSourcesAndMultipleUses()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        for (int i = 0; i < IndustrialResourceIds.Length; i++)
        {
            Assert.That(result.ResourcesWithoutSource, Does.Not.Contain(IndustrialResourceIds[i]),
                string.Join(", ", result.ResourcesWithoutSource));
            Assert.That(result.ResourcesWithoutSink, Does.Not.Contain(IndustrialResourceIds[i]),
                string.Join(", ", result.ResourcesWithoutSink));
            Assert.That(CountConsumerUses(IndustrialResourceIds[i]), Is.GreaterThanOrEqualTo(2),
                $"Industrial resource '{IndustrialResourceIds[i]}' must have at least two independent uses.");
        }
    }

    private static int CountConsumerBuildings(string resourceId)
    {
        int count = 0;
        for (int i = 0; i < DataBase<Building>.All.Count; i++)
        {
            Building building = DataBase<Building>.All[i];
            if (ContainsResource(building.ResourceRequirements, resourceId) ||
                ContainsResource(building.ResourceConsumptionRates, resourceId))
                count++;
        }
        return count;
    }

    private static int CountConsumerUses(string resourceId)
    {
        int count = CountConsumerBuildings(resourceId);
        for (int i = 0; i < DataBase<Research>.All.Count; i++)
            if (ContainsResource(DataBase<Research>.All[i].ResourceRequirements, resourceId))
                count++;
        for (int i = 0; i < DataBase<WorkshopUpgradeDefinition>.All.Count; i++)
            if (ContainsResource(DataBase<WorkshopUpgradeDefinition>.All[i].ResourceRequirements, resourceId))
                count++;
        return count;
    }

    private static bool ContainsResource(
        IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = pairs[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return true;
        }
        return false;
    }
}
