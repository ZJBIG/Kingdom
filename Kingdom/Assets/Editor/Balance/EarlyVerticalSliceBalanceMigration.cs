using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kingdom.EditorTools
{
    public static class EarlyVerticalSliceBalanceMigration
    {
        private const string BuildingRoot = "Assets/Resources/Datas/Building";
        private const string ResearchRoot = "Assets/Resources/Datas/Research";
        private const string ResourceRoot = "Assets/Resources/Datas/Resource";

        [MenuItem("Tools/Kingdom/Balance/Apply Early Vertical Slice Pacing")]
        public static void Run()
        {
            Building knowledgeCircle = Find<Building>(BuildingRoot, "KnowledgeCircle");
            Research controlledFire = Find<Research>(ResearchRoot, "ControlledFire");
            Research knowledgeSharing = Find<Research>(ResearchRoot, "KnowledgeSharing");
            Research foragingGroups = Find<Research>(ResearchRoot, "ForagingGroups");
            Research quarryResearch = Find<Research>(ResearchRoot, "Quarry");
            Research neolithicSettlement = Find<Research>(
                ResearchRoot,
                "NeolithicSettlement");
            Research smithingBronze = Find<Research>(ResearchRoot, "Smithing_Bronze");
            Research smithingIron = Find<Research>(ResearchRoot, "Smithing_Iron");
            Research industrialization = Find<Research>(ResearchRoot, "Industrialization");
            Resource woodLog = Find<Resource>(ResourceRoot, "WoodLog");
            Resource stoneChunk = Find<Resource>(ResourceRoot, "StoneChunk");
            Resource stoneBrick = Find<Resource>(ResourceRoot, "StoneBrick");
            Resource bronze = Find<Resource>(ResourceRoot, "Bronze");
            Resource copper = Find<Resource>(ResourceRoot, "Copper");
            Resource tin = Find<Resource>(ResourceRoot, "Tin");
            Resource coal = Find<Resource>(ResourceRoot, "Coal");

            Undo.RecordObjects(
                new Object[]
                {
                    knowledgeCircle,
                    knowledgeSharing,
                    foragingGroups,
                    quarryResearch,
                    neolithicSettlement
                },
                "Apply Early Vertical Slice Pacing");

            knowledgeCircle.SetRequiredResearchForEditor(
                new List<Research> { controlledFire });
            knowledgeCircle.ConfigureEconomyForEditor(
                new ExpantaNum(1.20d),
                knowledgeCircle.SpaceCost,
                new ExpantaNum(1d),
                knowledgeCircle.ProductivityGranted,
                knowledgeCircle.PopulationCapacityGranted,
                new ExpantaNum(1d),
                knowledgeCircle.FoodProductionRate,
                knowledgeCircle.FoodConsumptionRate,
                knowledgeCircle.FoodCapacityGranted,
                knowledgeCircle.PowerProductionRate,
                knowledgeCircle.PowerConsumptionRate,
                knowledgeCircle.LogisticsProductionRate,
                knowledgeCircle.LogisticsConsumptionRate,
                knowledgeCircle.AttackPowerGranted,
                knowledgeCircle.DefensePowerGranted,
                knowledgeCircle.MilitaryManpowerGranted,
                new List<Pair<Resource, ExpantaNum>>
                {
                    new(woodLog, new ExpantaNum(50d))
                },
                knowledgeCircle.ResourceGenerationRates.ToList(),
                knowledgeCircle.ResourceConsumptionRates.ToList());

            knowledgeSharing.SetEffectsForEditor(
                new List<ResearchEffectDefinition>
                {
                    new()
                    {
                        Type = ResearchEffectType.GlobalResearchMultiplier,
                        Value = new ExpantaNum(1.15d)
                    }
                });

            Building hunterGatherer = Find<Building>(
                BuildingRoot,
                "HunterGathererCamp");
            hunterGatherer.SetRequiredResearchForEditor(
                new List<Research> { controlledFire });
            foragingGroups.SetEffectsForEditor(
                new List<ResearchEffectDefinition>
                {
                    new()
                    {
                        Type = ResearchEffectType.BuildingFoodProductionMultiplier,
                        Building = hunterGatherer,
                        Value = new ExpantaNum(1.25d)
                    }
                });

            quarryResearch.SetResourceRequirementsForEditor(
                new List<Pair<Resource, ExpantaNum>>
                {
                    new(woodLog, new ExpantaNum(400d))
                });
            neolithicSettlement.BaseCost = "4400";
            industrialization.BaseCost = "270000";
            smithingIron.SetPrerequisitesForEditor(
                smithingIron.Prerequisites
                    .Append(smithingBronze)
                    .Distinct()
                    .ToList());

            SetGenerationRate("Quarry", "StoneChunk", 2.4d);
            SetGenerationRate("FiberGatheringCamp", "PlantFiber", 1.2d);
            SetGenerationRate("CopperMine", "CopperOre", 3d);
            SetGenerationRate("TinMine", "TinOre", 3d);
            SetGenerationRate("IronMine", "IronOre", 3.6d);
            SetGenerationRate("CopperSmelter", "Copper", 1.2d);
            SetGenerationRate("BronzeFoundry", "Bronze", 1.2d);
            SetBuildingCosts(
                "IronMine",
                new List<Pair<Resource, ExpantaNum>>
                {
                    new(woodLog, new ExpantaNum(220d)),
                    new(stoneChunk, new ExpantaNum(200d)),
                    new(bronze, new ExpantaNum(1.2d))
                });
            SetBuildingCosts(
                "BronzeFoundry",
                new List<Pair<Resource, ExpantaNum>>
                {
                    new(woodLog, new ExpantaNum(120d)),
                    new(stoneBrick, new ExpantaNum(150d)),
                    new(copper, new ExpantaNum(1d)),
                    new(tin, new ExpantaNum(0.5d)),
                    new(coal, new ExpantaNum(0.5d))
                });

            EditorUtility.SetDirty(knowledgeCircle);
            EditorUtility.SetDirty(knowledgeSharing);
            EditorUtility.SetDirty(foragingGroups);
            EditorUtility.SetDirty(hunterGatherer);
            EditorUtility.SetDirty(quarryResearch);
            EditorUtility.SetDirty(neolithicSettlement);
            EditorUtility.SetDirty(smithingIron);
            EditorUtility.SetDirty(industrialization);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log(
                "Early vertical-slice pacing applied: KnowledgeCircle is an early WoodLog-only " +
                "ResearchPower source and KnowledgeSharing grants a concrete research effect.");
        }

        public static void RunFromCommandLine()
        {
            Run();
            ContentBalanceReporter.Export();
        }

        private static T Find<T>(string root, string id) where T : GameDefinition
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root }))
            {
                T definition = AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition != null && definition.Id == id)
                    return definition;
            }
            throw new KeyNotFoundException(
                $"Could not find {typeof(T).Name} '{id}' below '{root}'.");
        }

        private static void SetGenerationRate(
            string buildingId,
            string resourceId,
            double rate)
        {
            Building building = Find<Building>(BuildingRoot, buildingId);
            Resource resource = Find<Resource>(ResourceRoot, resourceId);
            var generation = building.ResourceGenerationRates
                .Where(pair => pair.First != resource)
                .ToList();
            generation.Add(new Pair<Resource, ExpantaNum>(
                resource,
                new ExpantaNum(rate)));
            building.ConfigureEconomyForEditor(
                building.CostGrowth,
                building.SpaceCost,
                building.ProductivityConsumption,
                building.ProductivityGranted,
                building.PopulationCapacityGranted,
                building.ResearchPowerGranted,
                building.FoodProductionRate,
                building.FoodConsumptionRate,
                building.FoodCapacityGranted,
                building.PowerProductionRate,
                building.PowerConsumptionRate,
                building.LogisticsProductionRate,
                building.LogisticsConsumptionRate,
                building.AttackPowerGranted,
                building.DefensePowerGranted,
                building.MilitaryManpowerGranted,
                building.ResourceRequirements.ToList(),
                generation,
                building.ResourceConsumptionRates.ToList());
            EditorUtility.SetDirty(building);
        }

        private static void SetBuildingCosts(
            string buildingId,
            List<Pair<Resource, ExpantaNum>> costs)
        {
            Building building = Find<Building>(BuildingRoot, buildingId);
            building.ConfigureEconomyForEditor(
                building.CostGrowth,
                building.SpaceCost,
                building.ProductivityConsumption,
                building.ProductivityGranted,
                building.PopulationCapacityGranted,
                building.ResearchPowerGranted,
                building.FoodProductionRate,
                building.FoodConsumptionRate,
                building.FoodCapacityGranted,
                building.PowerProductionRate,
                building.PowerConsumptionRate,
                building.LogisticsProductionRate,
                building.LogisticsConsumptionRate,
                building.AttackPowerGranted,
                building.DefensePowerGranted,
                building.MilitaryManpowerGranted,
                costs,
                building.ResourceGenerationRates.ToList(),
                building.ResourceConsumptionRates.ToList());
            EditorUtility.SetDirty(building);
        }
    }
}
