using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public sealed class GlobalEconomyDefinitionTests
{
    private static readonly string[] ReleasedResourceIds =
    {
        "WoodLog", "StoneChunk", "StoneBrick", "Clay", "PlantFiber", "Ceramic",
        "Cloth", "Coal", "CopperOre", "Copper", "TinOre", "Tin", "IronOre",
        "Iron", "Bronze", "Steel", "Chemical", "Machinery", "Electronics",
        "CrudeOil", "Silica", "Coke", "Glass", "Ceramic",
        "RefinedFuel", "Lubricant", "Rubber", "CopperWire", "PrecisionParts", "Engine",
        "Concrete", "BauxiteOre", "Aluminum", "Explosives"
    };

    [Test]
    public void MigrationProducesThePlannedDefinitionCounts()
    {
        Assert.That(AssetDatabase.FindAssets("t:Resource", new[] { "Assets" }).Length, Is.EqualTo(52));
        Assert.That(DataBase<Building>.All.Count, Is.EqualTo(58));
        Assert.That(DataBase<Research>.All.Count, Is.EqualTo(81));
        Assert.That(DataBase<WorkshopUpgradeDefinition>.All.Count, Is.EqualTo(32));

        int releasedBuildings = 0;
        foreach (Building building in DataBase<Building>.All)
            if (building.TechLevel <= TechLevel.Industrial)
                releasedBuildings++;
        Assert.That(releasedBuildings, Is.EqualTo(55));
    }

    [Test]
    public void RetiredToolsAreAbsentAndTheirStableIdsAreNotReused()
    {
        Assert.That(DataBase<Resource>.Contains("StoneTool"), Is.False);
        Assert.That(DataBase<Resource>.Contains("MetalTool"), Is.False);
        Assert.That(DataBase<Building>.Contains("StoneToolWorkshop"), Is.False);
        Assert.That(DataBase<Building>.Contains("Blacksmith"), Is.False);
        Assert.That(AssetDatabase.FindAssets("StoneTool t:Resource"), Is.Empty);
        Assert.That(AssetDatabase.FindAssets("MetalTool t:Resource"), Is.Empty);
    }

    [Test]
    public void LegacyStoneResourceIdsMapToCurrentStableIds()
    {
        Type migrationType =
            typeof(SaveManager).Assembly.GetType("RetiredDefinitionMigration", true);
        MethodInfo normalize = migrationType.GetMethod(
            "NormalizeResourceId",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo normalizeBuilding = migrationType.GetMethod(
            "NormalizeBuildingId",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(
            normalize.Invoke(null, new object[] { "StoneChunk_Marble" }),
            Is.EqualTo("StoneChunk"));
        Assert.That(
            normalize.Invoke(null, new object[] { "StoneBrick_Marble" }),
            Is.EqualTo("StoneBrick"));
        Assert.That(
            normalize.Invoke(null, new object[] { "WoodLog" }),
            Is.EqualTo("WoodLog"));
        Assert.That(
            normalizeBuilding.Invoke(
                null,
                new object[] { "StoneCuttingWorkshop_Marble" }),
            Is.EqualTo("StoneCuttingWorkshop"));
    }

    [Test]
    public void ResearchNoLongerOwnsBuildingUnlockData()
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.That(typeof(Research).GetField("BuildingUnlock", flags), Is.Null);
        Assert.That(Enum.IsDefined(typeof(ResearchEffectType), "UnlockBuilding"), Is.False);
        Assert.That(typeof(ProgressionModifierState).GetField("UnlockedBuildings", flags), Is.Null);
    }

    [Test]
    public void BuildingsOwnAndUseAllPrerequisites()
    {
        Building pasture = DataBase<Building>.Find("Pasture");
        Assert.That(pasture.RequiredResearch, Has.Count.EqualTo(2));

        Building oilRefinery = DataBase<Building>.Find("OilRefinery");
        Assert.That(oilRefinery.RequiredResearch, Has.Count.EqualTo(2));
        Assert.That(oilRefinery.RequiredWorkshopUpgrades, Has.Count.EqualTo(1));

        foreach (Building building in DataBase<Building>.All)
        {
            AssertUnique(building.RequiredResearch, $"{building.Id} research");
            AssertUnique(building.RequiredWorkshopUpgrades, $"{building.Id} workshop");
        }
    }

    [Test]
    public void EveryWorkshopUpgradeHasAnEffectAndReferencedBuildingsAreDiscoverable()
    {
        int buildingGates = 0;
        foreach (WorkshopUpgradeDefinition upgrade in DataBase<WorkshopUpgradeDefinition>.All)
        {
            Assert.That(upgrade.Effects, Is.Not.Empty, upgrade.Id);
            Assert.That(
                upgrade.RequiredResearch.Count + upgrade.RequiredUpgrades.Count,
                Is.GreaterThan(0),
                $"{upgrade.Id} must have an explicit prerequisite");
            AssertUnique(upgrade.RequiredResearch, $"{upgrade.Id} research");
            AssertUnique(upgrade.RequiredUpgrades, $"{upgrade.Id} upgrade");
        }
        foreach (Building building in DataBase<Building>.All)
            buildingGates += building.RequiredWorkshopUpgrades.Count;
        Assert.That(buildingGates, Is.GreaterThanOrEqualTo(5));
    }

    [Test]
    public void EveryResearchAndWorkshopEffectHasAValidTarget()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            foreach (ResearchEffectDefinition effect in research.Effects)
            {
                Assert.That(effect, Is.Not.Null, research.Id);
                Assert.That(effect.Value.IsNaN, Is.False, research.Id);
                Assert.That(effect.Value, Is.GreaterThan(ExpantaNum.Zero), research.Id);

                switch (effect.Type)
                {
                    case ResearchEffectType.BuildingProductionMultiplier:
                    case ResearchEffectType.BuildingFoodProductionMultiplier:
                    case ResearchEffectType.BuildingResearchPowerMultiplier:
                    case ResearchEffectType.BuildingPowerProductionMultiplier:
                    case ResearchEffectType.BuildingLogisticsProductionMultiplier:
                        Assert.That(effect.Building, Is.Not.Null,
                            $"研究 {research.Id} 的建筑效果缺少目标建筑。");
                        break;
                    case ResearchEffectType.ResourceProductionMultiplier:
                        Assert.That(effect.Resource, Is.Not.Null,
                            $"研究 {research.Id} 的资源效果缺少目标资源。");
                        break;
                    default:
                        break;
                }
            }
        }

        foreach (WorkshopUpgradeDefinition upgrade in DataBase<WorkshopUpgradeDefinition>.All)
        {
            foreach (WorkshopEffectDefinition effect in upgrade.Effects)
            {
                Assert.That(effect, Is.Not.Null, upgrade.Id);
                Assert.That(effect.Value.IsNaN, Is.False, upgrade.Id);
                Assert.That(effect.Value, Is.GreaterThan(ExpantaNum.Zero), upgrade.Id);

                switch (effect.Type)
                {
                    case WorkshopEffectType.BuildingProductionMultiplier:
                    case WorkshopEffectType.BuildingFoodProductionMultiplier:
                    case WorkshopEffectType.BuildingResearchPowerMultiplier:
                    case WorkshopEffectType.BuildingPowerProductionMultiplier:
                    case WorkshopEffectType.BuildingLogisticsProductionMultiplier:
                        Assert.That(effect.Building, Is.Not.Null,
                            $"工坊 {upgrade.Id} 的建筑效果缺少目标建筑。");
                        break;
                    case WorkshopEffectType.ResourceProductionMultiplier:
                        Assert.That(effect.Resource, Is.Not.Null,
                            $"工坊 {upgrade.Id} 的资源效果缺少目标资源。");
                        break;
                }
            }
        }
    }

    [Test]
    public void ReleasedResourcesExistAndIntegratedDependencyGraphIsReachable()
    {
        foreach (string id in ReleasedResourceIds)
            Assert.That(DataBase<Resource>.Contains(id), Is.True, id);

        Assert.That(EconomyDependencyValidator.Validate(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            DataBase<WorkshopUpgradeDefinition>.All,
            out string error), Is.True, error);
    }

    private static void AssertUnique<T>(IReadOnlyList<T> values, string context) where T : class
    {
        var unique = new HashSet<T>();
        for (int i = 0; i < values.Count; i++)
        {
            Assert.That(values[i], Is.Not.Null, context);
            Assert.That(unique.Add(values[i]), Is.True, context);
        }
    }
}
