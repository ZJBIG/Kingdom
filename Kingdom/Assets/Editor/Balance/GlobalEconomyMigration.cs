using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Kingdom.EditorTools
{
    public static class GlobalEconomyMigration
    {
        private const string ResourceRoot = "Assets/Resources/Datas/Resource";
        private const string BuildingRoot = "Assets/Resources/Datas/Building";
        private const string ResearchRoot = "Assets/Resources/Datas/Research";
        private const string WorkshopRoot = "Assets/Resources/Datas/Workshop";

        private static readonly string[] DeferredResourceIds =
        {
            "Citrine", "Diamond", "EbonyLog", "ElvenWoodLog", "Emerald", "Gold",
            "GoldOre", "Jade", "Mithril", "MithrilOre", "RoseWoodLog", "Ruby",
            "Sapphire", "ScentedWoodLog", "Silver", "SilverOre", "Titanium",
            "TitaniumOre", "Uranium", "WhiteBirchLog"
        };

        [MenuItem("Tools/Kingdom/Balance/Apply Global Economy Migration")]
        public static void Run()
        {
            EnsureFolders();
            CreateIndustrialResources();
            CreateIndustrialResearchSkeletons();
            CreateIndustrialBuildingSkeletons();
            CreateWorkshopSkeletons();

            NormalizeLegacyStableIds();
            ReplaceRetiredResourceReferences();
            ConfigureExistingResearchEffects();
            ConfigureAllResearch();
            ConfigureAllBuildings();
            ConfigureWorkshopUpgrades();
            ConfigureWorkshopClosureOverrides();
            ConfigureBuildingWorkshopPrerequisites();
            NormalizeDefinitionFolders();

            DeleteRetiredDefinitions();
            MoveDeferredDefinitions();
            ForceReserializeDefinitions();
            EnsureSceneComponents();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log(
                $"Global economy migration complete. Resources={Count<Resource>()}, " +
                $"Buildings={Count<Building>()}, Research={Count<Research>()}, " +
                $"Workshop={Count<WorkshopUpgradeDefinition>()}.");
        }

        public static void RunFromCommandLine()
        {
            Run();
            ContentBalanceReporter.Export();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Resources/Datas", "Workshop");
            EnsureFolder(ResourceRoot, "Industrial");
            EnsureFolder(BuildingRoot, "Industrial");
            EnsureFolder(ResearchRoot, "Industrial");
            EnsureFolder("Assets/ContentBacklog", "Resource");
        }

        private static void CreateIndustrialResources()
        {
            CreateResource("CrudeOil", "原油", "石油化工与燃料工业的基础原料。");
            CreateResource("Silica", "硅砂", "玻璃与工业陶瓷的矿物原料。");
            CreateResource("Coke", "焦炭", "高温冶金和工业化学的高纯碳材料。");
            CreateResource("Glass", "玻璃", "实验室、电气和后续航天结构材料。");
            CreateResource("IndustrialCeramic", "工业陶瓷", "精密机械、实验设备与隔热结构材料。");
            CreateResource("RefinedFuel", "精炼燃料", "铁路、军工和内燃机使用的持续燃料。");
            CreateResource("Lubricant", "润滑剂", "机械、线缆和运输设备的持续消耗品。");
            CreateResource("Rubber", "橡胶", "密封、电气绝缘和工业升级材料。");
            CreateResource("CopperWire", "铜线", "机械、电气和通信设备的基础中间件。");
            CreateResource("PrecisionParts", "精密零件", "军工、科研和后续高端设备的核心部件。");
            CreateResource("Engine", "发动机", "运输、工业和后续载具的动力设备。");
        }

        private static void CreateIndustrialResearchSkeletons()
        {
            string[] ids =
            {
                "SteamPower", "IndustrialWorkshop", "PetroleumExtraction",
                "SilicaProcessing", "Coking", "IndustrialChemistry",
                "ElectricalEngineering", "PrecisionManufacturing",
                "MechanizedProduction", "RailwayEngineering", "ModernUniversity",
                "Standardization", "MassProduction", "Electrification",
                "ScientificMethod", "IndustrialAgriculture", "MilitaryIndustry",
                "LogisticsManagement", "FactoryOrganization", "CombustionEngines",
                "IndustrialAdministration"
            };
            for (int i = 0; i < ids.Length; i++)
                LoadOrCreate<Research>($"{ResearchRoot}/Industrial/{ids[i]}.asset", ids[i]);
        }

        private static void CreateIndustrialBuildingSkeletons()
        {
            string[] ids =
            {
                "OilDerrick", "SilicaQuarry", "CokeOven",
                "Glassworks", "OilRefinery", "WireMill"
            };
            for (int i = 0; i < ids.Length; i++)
                LoadOrCreate<Building>($"{BuildingRoot}/Industrial/{ids[i]}.asset", ids[i]);
        }

        private static void CreateWorkshopSkeletons()
        {
            string[] ids =
            {
                "DraftingTables", "ShiftRegisters", "ReinforcedBoilers",
                "InterchangeableParts", "PrecisionTooling", "PoweredMining",
                "ChemicalCatalysts", "AgriculturalMachinery", "MechanicalLooms",
                "RotaryKilns", "StandardGauge", "TelegraphDispatch",
                "AcademicJournals", "LaboratoryGlassware",
                "ElectricalInstrumentation", "ConveyorSystems",
                "ReinforcedConcrete", "MilitaryStandardization"
            };
            for (int i = 0; i < ids.Length; i++)
                LoadOrCreate<WorkshopUpgradeDefinition>(
                    $"{WorkshopRoot}/{ids[i]}.asset",
                    ids[i]);
        }

        private static void ReplaceRetiredResourceReferences()
        {
            Resource stone = Find<Resource>("StoneChunk");
            Resource bronze = Find<Resource>("Bronze");

            foreach (Building building in LoadAll<Building>())
            {
                if (building.Id == "StoneToolWorkshop" || building.Id == "Blacksmith")
                    continue;
                ReplacePairs(building, "resourceRequirements", stone, bronze);
                ReplacePairs(building, "resourceGenerationRates", stone, bronze, true);
                ReplacePairs(building, "resourceConsumptionRates", stone, bronze, true);
            }

            foreach (Research research in LoadAll<Research>())
            {
                var replaced = new List<Pair<Resource, ExpantaNum>>();
                for (int i = 0; i < research.ResourceRequirements.Count; i++)
                {
                    Pair<Resource, ExpantaNum> pair = research.ResourceRequirements[i];
                    if (pair.First == null)
                        continue;
                    Resource resource = pair.First.Id == "StoneTool"
                        ? stone
                        : pair.First.Id == "MetalTool" ? bronze : pair.First;
                    replaced.Add(new Pair<Resource, ExpantaNum>(resource, pair.Second));
                }
                research.SetResourceRequirementsForEditor(MergePairs(replaced));

                var effects = new List<ResearchEffectDefinition>();
                for (int i = 0; i < research.Effects.Count; i++)
                {
                    ResearchEffectDefinition effect = research.Effects[i];
                    if (effect == null || (int)effect.Type == 0 ||
                        effect.Building != null &&
                        (effect.Building.Id == "StoneToolWorkshop" ||
                         effect.Building.Id == "Blacksmith"))
                        continue;
                    if (effect.Resource != null &&
                        (effect.Resource.Id == "StoneTool" || effect.Resource.Id == "MetalTool"))
                        continue;
                    effects.Add(effect);
                }
                research.SetEffectsForEditor(effects);
                EditorUtility.SetDirty(research);
            }
        }

        private static void NormalizeLegacyStableIds()
        {
            RenameStableId<Resource>("StoneChunk_Marble", "StoneChunk");
            RenameStableId<Resource>("StoneBrick_Marble", "StoneBrick");
            RenameStableId<Building>(
                "StoneCuttingWorkshop_Marble",
                "StoneCuttingWorkshop");
        }

        private static void RenameStableId<T>(string legacyId, string currentId)
            where T : GameDefinition
        {
            if (FindOptional<T>(currentId) != null)
                return;

            T value = FindOptional<T>(legacyId);
            if (value == null)
                throw new InvalidOperationException(
                    $"{typeof(T).Name} '{legacyId}' could not be migrated to '{currentId}'.");

            value.name = currentId;
            value.SetIdForEditor(currentId);
            EditorUtility.SetDirty(value);
        }

        private static void ConfigureExistingResearchEffects()
        {
            Research stoneTools = Find<Research>("StoneTools");
            stoneTools.SetResourceRequirementsForEditor(Pairs("WoodLog", 30));
            EditorUtility.SetDirty(stoneTools);

            SetResearchEffects("StoneTools",
                BuildingMultiplier("Quarry", 1.25),
                BuildingMultiplier("StoneCuttingWorkshop", 1.20),
                GlobalConstruction(1.05));
            SetResearchEffects("Mining",
                BuildingMultiplier("CoalMine", 1.15),
                BuildingMultiplier("CopperMine", 1.15),
                BuildingMultiplier("TinMine", 1.15),
                BuildingMultiplier("IronMine", 1.15));
            SetResearchEffects("Smithing",
                BuildingMultiplier("CopperSmelter", 1.10),
                BuildingMultiplier("TinSmelter", 1.10),
                BuildingMultiplier("IronSmelter", 1.10),
                BuildingMultiplier("BronzeFoundry", 1.10),
                GlobalConstruction(1.05));
            SetResearchEffects("ControlledFire",
                BuildingFoodMultiplier("HunterGathererCamp", 1.10),
                BuildingMultiplier("PotteryKiln", 1.10));
            SetResearchEffects("NeolithicSettlement", Territory(300), Productivity(100));
            SetResearchEffects("SmithingRevolution", Territory(500), Productivity(300));
        }

        private static void ConfigureAllResearch()
        {
            Research neolithicSettlement = Find<Research>("NeolithicSettlement");
            neolithicSettlement.TechLevel = TechLevel.Neolithic;
            neolithicSettlement.AdvancesTechLevel = true;
            Research smithingRevolution = Find<Research>("SmithingRevolution");
            smithingRevolution.TechLevel = TechLevel.Medieval;
            smithingRevolution.AdvancesTechLevel = true;
            Research industrialization = Find<Research>("Industrialization");
            industrialization.SetResourceRequirementsForEditor(Pairs(
                "Steel", 1000, "Bronze", 500));
            industrialization.SetEffectsForEditor(new List<ResearchEffectDefinition>
            {
                SystemUnlock("industrialization"),
                Territory(1000),
                Productivity(800)
            });
            industrialization.TechLevel = TechLevel.Industrial;
            industrialization.AdvancesTechLevel = true;
            EditorUtility.SetDirty(industrialization);

            ConfigureResearch("SteamPower", "蒸汽动力", 75000,
                R("Industrialization"), P("Steel", 300, "Coal", 600),
                Power(1.05));
            ConfigureResearch("IndustrialWorkshop", "工业工坊", 90000,
                R("Industrialization"), P("Steel", 250, "Bronze", 100, "Pottery", 100),
                SystemUnlock(WorkshopManager.WorkshopSystemId));
            ConfigureResearch("PetroleumExtraction", "石油开采", 110000,
                R("Industrialization"), P("Steel", 250, "Bronze", 120),
                BuildingMultiplier("OilDerrick", 1.05));
            ConfigureResearch("SilicaProcessing", "硅质加工", 120000,
                R("Industrialization"), P("Steel", 200, "Pottery", 200),
                BuildingMultiplier("Glassworks", 1.05));
            ConfigureResearch("Coking", "炼焦", 140000,
                R("SteamPower"), P("Coal", 800, "StoneBrick", 400),
                BuildingMultiplier("CokeOven", 1.10));
            ConfigureResearch("IndustrialChemistry", "工业化学", 180000,
                R("PetroleumExtraction", "Coking"),
                P("CrudeOil", 500, "Coke", 300, "Glass", 100),
                BuildingMultiplier("ChemicalPlant", 1.10));
            ConfigureResearch("ElectricalEngineering", "电气工程", 220000,
                R("IndustrialChemistry", "Standardization"),
                P("Copper", 500, "Glass", 250, "Steel", 100),
                BuildingMultiplier("WireMill", 1.10));
            ConfigureResearch("PrecisionManufacturing", "精密制造", 280000,
                R("IndustrialChemistry", "ElectricalEngineering"),
                P("Steel", 500, "CopperWire", 200, "Lubricant", 80),
                GlobalConstruction(1.05));
            ConfigureResearch("MechanizedProduction", "机械化生产", 360000,
                R("PrecisionManufacturing"),
                P("Steel", 500, "Coke", 300),
                BuildingMultiplier("MachineFactory", 1.20));
            ConfigureResearch("RailwayEngineering", "铁路工程", 440000,
                R("MechanizedProduction"),
                P("Machinery", 150, "Engine", 25, "Steel", 600),
                GlobalLogistics(1.05));
            ConfigureResearch("ModernUniversity", "现代大学", 520000,
                R("ElectricalEngineering"),
                P("Machinery", 100, "Electronics", 100, "Glass", 200),
                GlobalResearch(1.10));
            ConfigureResearch("Standardization", "标准化", 620000,
                R("IndustrialWorkshop", "IndustrialChemistry"),
                P("Steel", 600, "Bronze", 300, "Glass", 200, "Chemical", 150),
                GlobalConstruction(1.10));
            ConfigureResearch("MassProduction", "大规模生产", 740000,
                R("Standardization"),
                P("Machinery", 250, "Steel", 800, "Chemical", 100),
                GlobalBuildingProduction(1.25));
            ConfigureResearch("Electrification", "电气化", 860000,
                R("RailwayEngineering", "ElectricalEngineering"),
                P("Electronics", 250, "CopperWire", 500, "Chemical", 150),
                Power(1.25));
            ConfigureResearch("ScientificMethod", "科学方法", 980000,
                R("ModernUniversity"),
                P("Electronics", 200, "Chemical", 150, "Cloth", 300, "Silica", 300),
                GlobalResearch(1.35));
            ConfigureResearch("IndustrialAgriculture", "工业农业", 1050000,
                R("MassProduction"),
                P("Machinery", 300, "Chemical", 200, "Engine", 30),
                BuildingFoodMultiplier("Farm", 1.50),
                BuildingFoodMultiplier("Pasture", 1.50),
                BuildingFoodMultiplier("WaterMill", 1.50));
            ConfigureResearch("MilitaryIndustry", "军事工业", 1200000,
                R("MassProduction", "Electrification"),
                P("Machinery", 250, "Chemical", 200, "PrecisionParts", 150, "Rubber", 200),
                Military(1.15));
            ConfigureResearch("LogisticsManagement", "物流管理", 1350000,
                R("RailwayEngineering", "Standardization"),
                P("Machinery", 300, "Electronics", 250, "Engine", 40),
                GlobalLogistics(1.30));
            ConfigureResearch("FactoryOrganization", "工厂组织", 1500000,
                R("Standardization", "ScientificMethod"),
                P("Machinery", 400, "Electronics", 200, "Cloth", 300),
                GlobalBuildingProduction(1.10));
            ConfigureResearch("CombustionEngines", "内燃机", 1650000,
                R("MechanizedProduction", "IndustrialChemistry"),
                P("Engine", 100, "RefinedFuel", 500, "Lubricant", 150),
                BuildingMultiplier("RailHub", 1.25),
                GlobalBuildingProduction(1.10));
            ConfigureResearch("IndustrialAdministration", "工业行政", 1800000,
                R("LogisticsManagement", "FactoryOrganization", "MilitaryIndustry"),
                P("Engine", 100, "PrecisionParts", 250, "Electronics", 300,
                    "Chemical", 250, "Steel", 1000),
                Territory(250), GlobalConstruction(1.20));
        }

        private static void ConfigureAllBuildings()
        {
            B("WoodHouse", TechLevel.Animal, 1.18, 3, 0, 0, 5, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 80), P(), P(), R());
            B("HunterGathererCamp", TechLevel.Animal, 1.15, 2, 2, 0, 0, 0, 3, 1, 0,
                0, 0, 0, 0, P("WoodLog", 30), P(), P(), R("ForagingGroups"));
            B("Farm", TechLevel.Animal, 1.14, 4, 3, 0, 0, 0, 8, 0, 0,
                0, 0, 0, 0, P("WoodLog", 50), P(), P(), R("Agriculture"));
            B("Pasture", TechLevel.Animal, 1.15, 5, 4, 0, 0, 0, 6, 1, 0,
                0, 0, 0, 0, P("WoodLog", 100), P(), P(),
                R("AnimalHusbandry", "Agriculture"));
            B("Lumberyard", TechLevel.Animal, 1.13, 6, 4, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 120, "StoneChunk", 50),
                P("WoodLog", 5), P(), R("TreeCultivate", "StoneTools"));
            B("Quarry", TechLevel.Animal, 1.14, 6, 6, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 120), P("StoneChunk", 2), P(),
                R("Quarry", "Mining"));
            B("ClayPit", TechLevel.Animal, 1.14, 4, 4, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 80), P("Clay", 1.5), P(),
                R("ControlledFire", "ClayExtraction"));
            B("FiberGatheringCamp", TechLevel.Animal, 1.14, 3, 3, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 60), P("PlantFiber", 1.5), P(),
                R("Agriculture", "ForagingGroups"));
            B("StoneCuttingWorkshop", TechLevel.Animal, 1.15, 5, 5, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 100, "StoneChunk", 100),
                P("StoneBrick", 1), P("StoneChunk", 2),
                R("StoneTools", "StoneCutting"));
            B("KnowledgeCircle", TechLevel.Animal, 1.20, 2, 2, 0, 0, 1, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 100, "StoneChunk", 20), P(), P(),
                R("ControlledFire", "KnowledgeSharing"));

            B("CoalMine", TechLevel.Neolithic, 1.14, 8, 8, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 150, "StoneChunk", 100), P("Coal", 1.5), P(),
                R("NeolithicSettlement", "Mining", "CoalMining"));
            B("CopperMine", TechLevel.Neolithic, 1.15, 8, 10, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 180, "StoneChunk", 140), P("CopperOre", 1), P(),
                R("NeolithicSettlement", "Mining", "Mining_Copper"));
            B("TinMine", TechLevel.Neolithic, 1.15, 8, 10, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 180, "StoneChunk", 140), P("TinOre", .8), P(),
                R("NeolithicSettlement", "Mining", "Mining_Tin"));
            B("IronMine", TechLevel.Neolithic, 1.15, 10, 12, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 220, "StoneChunk", 200), P("IronOre", .8), P(),
                R("NeolithicSettlement", "Mining", "Mining_Iron"));
            B("PotteryKiln", TechLevel.Neolithic, 1.16, 4, 5, 0, 0, 0, 0, 0, 250,
                0, 0, 0, 0, P("Clay", 50, "WoodLog", 50), P("Pottery", 1),
                P("Clay", 1, "WoodLog", .2), R("NeolithicSettlement", "Pottery", "ClayExtraction"));
            B("WeavingWorkshop", TechLevel.Neolithic, 1.16, 4, 5, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("PlantFiber", 50, "WoodLog", 40), P("Cloth", 1),
                P("PlantFiber", 1), R("NeolithicSettlement", "TextileCraft", "Agriculture"));
            B("CopperSmelter", TechLevel.Neolithic, 1.17, 8, 10, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 100, "StoneBrick", 80), P("Copper", .8),
                P("CopperOre", 1, "Coal", .5), R("Smithing", "Smithing_Copper"));
            B("TinSmelter", TechLevel.Neolithic, 1.17, 8, 10, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 100, "StoneBrick", 80), P("Tin", .8),
                P("TinOre", 1, "Coal", .5), R("Smithing", "Smithing_Tin"));
            B("IronSmelter", TechLevel.Neolithic, 1.17, 10, 12, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 120, "StoneBrick", 100), P("Iron", .8),
                P("IronOre", 1, "Coal", .6), R("Smithing", "Smithing_Iron"));
            B("BronzeFoundry", TechLevel.Neolithic, 1.18, 12, 16, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 120, "StoneBrick", 150), P("Bronze", 1),
                P("Copper", 1, "Tin", .5, "Coal", .5),
                R("Smithing_Copper", "Smithing_Tin", "Smithing_Bronze"));
            B("Granary", TechLevel.Neolithic, 1.18, 4, 2, 0, 0, 0, 0, 0, 1000,
                0, 0, 0, 0, P("WoodLog", 150, "StoneBrick", 100, "Pottery", 30),
                P(), P(), R("FoodStorage", "Pottery"));
            B("ScribeHut", TechLevel.Neolithic, 1.20, 5, 5, 0, 0, 5, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 200, "StoneBrick", 100, "Pottery", 20, "Cloth", 20),
                P(), P(), R("WrittenRecords", "Pottery", "TextileCraft"));

            B("WaterMill", TechLevel.Medieval, 1.20, 8, 5, 0, 0, 0, 10, 0, 0,
                0, 0, 0, 0, P("WoodLog", 200, "StoneBrick", 150, "Pottery", 50, "Bronze", 30),
                P(), P(), R("SmithingRevolution", "WaterManagement", "MechanicalEngineering"));
            B("SteelForge", TechLevel.Medieval, 1.20, 10, 8, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 300, "StoneBrick", 250, "Coal", 100, "Iron", 100),
                P("Steel", 1), P("Iron", 1, "Coal", 1),
                R("SmithingRevolution", "Smithing_Iron", "Steelmaking"));
            B("Library", TechLevel.Medieval, 1.22, 6, 6, 0, 0, 25, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 250, "StoneBrick", 200, "Cloth", 50, "Bronze", 30),
                P(), P(), R("SmithingRevolution", "WrittenRecords", "Bookmaking"));
            B("Market", TechLevel.Medieval, 1.20, 5, 5, 0, 0, 5, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 200, "StoneBrick", 100, "Cloth", 30, "Pottery", 30),
                P(), P(), R("SmithingRevolution", "TradeRoutes", "Measurement"));
            B("Barracks", TechLevel.Medieval, 1.20, 8, 8, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 2, P("WoodLog", 250, "StoneBrick", 250, "Steel", 50, "Bronze", 50),
                P(), P(), R("SmithingRevolution", "StandingArmy", "Steelmaking"), 1, 0, 5);
            B("Fortification", TechLevel.Medieval, 1.20, 12, 3, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("StoneBrick", 400, "Steel", 100, "Bronze", 80),
                P(), P(), R("SmithingRevolution", "Fortification", "Masonry"), 5);

            B("SteamPlant", TechLevel.Industrial, 1.20, 18, 12, 0, 0, 0, 0, 0, 0,
                100, 0, 50, 0, P("StoneBrick", 500, "Steel", 250, "Coal", 400),
                P(), P("Coal", 2), R("Industrialization", "SteamPower"));
            B("OilDerrick", TechLevel.Industrial, 1.15, 14, 10, 0, 0, 0, 0, 0, 0,
                0, 8, 0, 0, P("Steel", 500, "Bronze", 250),
                P("CrudeOil", 3), P(), R("Industrialization", "PetroleumExtraction"), 0, 6);
            B("SilicaQuarry", TechLevel.Industrial, 1.15, 12, 10, 0, 0, 0, 0, 0, 0,
                0, 6, 0, 0, P("Steel", 350, "Bronze", 200),
                P("Silica", 2), P(), R("Industrialization", "SilicaProcessing"), 0, 4);
            B("CokeOven", TechLevel.Industrial, 1.18, 14, 12, 0, 0, 0, 0, 0, 0,
                0, 5, 0, 0, P("StoneBrick", 600, "Steel", 300, "Coal", 500),
                P("Coke", 1.5), P("Coal", 2), R("SteamPower", "Coking"), 0, 4);
            B("Glassworks", TechLevel.Industrial, 1.18, 14, 12, 0, 0, 0, 0, 0, 0,
                0, 12, 0, 0, P("StoneBrick", 500, "Steel", 250, "Pottery", 300),
                P("Glass", 1, "IndustrialCeramic", .2),
                P("Silica", 1.5, "Coal", .4, "Pottery", .2),
                R("SilicaProcessing", "Coking"), 0, 6);
            B("ChemicalPlant", TechLevel.Industrial, 1.20, 18, 14, 0, 0, 0, 0, 0, 0,
                0, 20, 0, 0, P("Steel", 500, "StoneBrick", 400, "Coke", 150, "Cloth", 100),
                P("Chemical", 1), P("CrudeOil", 1.2, "Coke", .4, "Cloth", .05),
                R("PetroleumExtraction", "Coking", "IndustrialChemistry"), 0, 10);
            B("OilRefinery", TechLevel.Industrial, 1.21, 20, 16, 0, 0, 0, 0, 0, 0,
                0, 25, 0, 0, P("Steel", 850, "Bronze", 250, "Copper", 200, "Chemical", 100),
                P("RefinedFuel", 1.2, "Lubricant", .4, "Rubber", .3),
                P("CrudeOil", 2, "Chemical", .2),
                R("IndustrialChemistry", "Standardization"), 0, 12);
            B("WireMill", TechLevel.Industrial, 1.20, 16, 14, 0, 0, 0, 0, 0, 0,
                0, 18, 0, 0, P("Steel", 500, "Copper", 300, "Bronze", 150),
                P("CopperWire", 1.5), P("Copper", 1.2, "Tin", .1, "Lubricant", .05),
                R("ElectricalEngineering", "Standardization"), 0, 10);
            B("MachineFactory", TechLevel.Industrial, 1.20, 20, 16, 0, 0, 0, 0, 0, 0,
                0, 30, 0, 0, P("Steel", 600, "CopperWire", 200, "Coke", 200, "Bronze", 200),
                P("Machinery", 1, "Electronics", .3, "PrecisionParts", .35, "Engine", .15),
                P("Steel", .8, "Coke", .4, "Lubricant", .15, "CopperWire", .4,
                    "Bronze", .1, "IndustrialCeramic", .05),
                R("PrecisionManufacturing"), 0, 18);
            B("RailHub", TechLevel.Industrial, 1.20, 25, 14, 0, 0, 0, 0, 0, 0,
                0, 8, 100, 0, P("Machinery", 180, "Electronics", 120, "Steel", 300, "Engine", 30),
                P(), P("RefinedFuel", .3, "Lubricant", .05),
                R("RailwayEngineering", "CombustionEngines"));
            B("University", TechLevel.Industrial, 1.20, 12, 12, 0, 0, 250, 0, 0, 0,
                0, 10, 0, 0, P("Machinery", 100, "Chemical", 80, "Electronics", 100, "Glass", 150),
                P(), P(), R("ModernUniversity", "ScientificMethod"), 0, 5);
            B("ArmsFactory", TechLevel.Industrial, 1.20, 22, 18, 0, 0, 0, 0, 0, 0,
                0, 35, 0, 10, P("Steel", 500, "Machinery", 180, "Chemical", 120, "Electronics", 80),
                P(), P("Steel", .5, "Machinery", .2, "Chemical", .15, "PrecisionParts", .1,
                    "Electronics", .1, "RefinedFuel", .1),
                R("MilitaryIndustry", "MassProduction"), 2, 20);
        }

        private static void ConfigureWorkshopUpgrades()
        {
            W("DraftingTables", "制图台", "基础设施", 10, R("IndustrialWorkshop"),
                P("WoodLog", 500, "Cloth", 150, "Bronze", 100), E(WorkshopEffectType.GlobalConstructionMultiplier, 1.10));
            W("ShiftRegisters", "轮班登记", "基础设施", 20, R("IndustrialWorkshop"),
                P("Pottery", 200, "Cloth", 200, "Steel", 150), E(WorkshopEffectType.GlobalResearchMultiplier, 1.10));
            W("ReinforcedBoilers", "强化锅炉", "动力", 30, R("SteamPower"),
                P("Coal", 350, "Iron", 300),
                EB(WorkshopEffectType.BuildingPowerProductionMultiplier, "SteamPlant", 1.25));
            W("InterchangeableParts", "互换零件", "机械", 40, R("PrecisionManufacturing"),
                P("Steel", 400, "Copper", 250),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "MachineFactory", 1.20));
            W("PrecisionTooling", "精密加工", "机械", 50, R("PrecisionManufacturing"),
                P("Steel", 600, "Bronze", 250, "CopperWire", 200, "Lubricant", 80),
                E(WorkshopEffectType.GlobalConstructionMultiplier, 1.10));
            W("PoweredMining", "动力采矿", "采掘", 60, R("PrecisionManufacturing"),
                P("Machinery", 180, "Steel", 450, "Lubricant", 80),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "SilicaQuarry", 1.25));
            W("ChemicalCatalysts", "化学催化剂", "化工", 70, R("IndustrialChemistry"),
                P("Chemical", 250, "Coke", 300, "Glass", 100),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "ChemicalPlant", 1.25));
            W("AgriculturalMachinery", "农业机械", "农业", 80, R("IndustrialAgriculture"),
                P("Machinery", 120, "Engine", 20, "Chemical", 80),
                EB(WorkshopEffectType.BuildingFoodProductionMultiplier, "Farm", 1.30));
            W("MechanicalLooms", "机械织机", "加工", 90, R("MechanizedProduction"),
                P("Machinery", 100, "Steel", 200, "Cloth", 200),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "WeavingWorkshop", 1.50));
            W("RotaryKilns", "回转窑", "加工", 100, R("IndustrialChemistry"),
                P("Coal", 250, "Clay", 250),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "Glassworks", 1.35));
            W("StandardGauge", "标准轨距", "物流", 110, R("RailwayEngineering"),
                P("Steel", 600, "Coke", 300),
                EB(WorkshopEffectType.BuildingLogisticsProductionMultiplier, "RailHub", 1.30));
            W("TelegraphDispatch", "电报调度", "物流", 120, R("LogisticsManagement"),
                P("Electronics", 180, "CopperWire", 400, "Machinery", 120),
                E(WorkshopEffectType.GlobalLogisticsMultiplier, 1.15));
            W("AcademicJournals", "学术期刊", "科研", 130, R("ModernUniversity"),
                P("Electronics", 100, "Glass", 150, "Cloth", 300),
                EB(WorkshopEffectType.BuildingResearchPowerMultiplier, "University", 1.25));
            W("LaboratoryGlassware", "实验玻璃器具", "科研", 140, R("ScientificMethod"),
                P("Chemical", 180, "Glass", 300, "IndustrialCeramic", 80),
                E(WorkshopEffectType.GlobalResearchMultiplier, 1.15));
            W("ElectricalInstrumentation", "电气仪表", "动力", 150, R("Electrification"),
                P("Electronics", 250, "CopperWire", 500, "Chemical", 150),
                E(WorkshopEffectType.PowerMultiplier, 1.20),
                E(WorkshopEffectType.GlobalBuildingProductionMultiplier, 1.10));
            W("ConveyorSystems", "传送系统", "机械", 160, R("MassProduction"),
                P("Machinery", 300, "Steel", 250),
                E(WorkshopEffectType.GlobalBuildingProductionMultiplier, 1.15));
            W("ReinforcedConcrete", "钢筋混凝土", "基础设施", 170, R("FactoryOrganization"),
                P("Chemical", 200, "StoneBrick", 1500, "Steel", 400,
                    "PrecisionParts", 20, "Silica", 500, "IndustrialCeramic", 150),
                E(WorkshopEffectType.TerritoryGranted, 150),
                E(WorkshopEffectType.GlobalConstructionMultiplier, 1.15));
            W("MilitaryStandardization", "军工标准化", "军事", 180, R("MilitaryIndustry"),
                P("Engine", 50, "PrecisionParts", 200, "Chemical", 200, "Steel", 800),
                E(WorkshopEffectType.MilitaryMultiplier, 1.25));
            SetWorkshopPrerequisites("AcademicJournals", "LaboratoryGlassware");
            SetWorkshopPrerequisites("AgriculturalMachinery", "DraftingTables");
            SetWorkshopPrerequisites("ChemicalCatalysts", "RotaryKilns");
            SetWorkshopPrerequisites("ConveyorSystems", "InterchangeableParts");
            SetWorkshopPrerequisites("ElectricalInstrumentation", "LaboratoryGlassware");
            SetWorkshopPrerequisites("InterchangeableParts", "PrecisionTooling");
            SetWorkshopPrerequisites("MechanicalLooms", "ConveyorSystems");
            SetWorkshopPrerequisites("PoweredMining", "InterchangeableParts");
            SetWorkshopPrerequisites("PrecisionTooling", "DraftingTables");
            SetWorkshopPrerequisites("ReinforcedConcrete", "RotaryKilns");
            SetWorkshopPrerequisites("RotaryKilns", "LaboratoryGlassware");
            SetWorkshopPrerequisites("ShiftRegisters", "ElectricalInstrumentation");
            SetWorkshopPrerequisites("StandardGauge", "ReinforcedBoilers");
            SetWorkshopPrerequisites("TelegraphDispatch", "ElectricalInstrumentation");
        }

        private static void ConfigureWorkshopClosureOverrides()
        {
            OverrideWorkshop("ReinforcedBoilers", R("SteamPower"), P("Coal", 350, "Iron", 300));
            OverrideWorkshop("InterchangeableParts", R("PrecisionManufacturing"), P("Steel", 400, "Copper", 250));
            OverrideWorkshop("RotaryKilns", R("IndustrialChemistry"), P("Coal", 250, "Clay", 250));
            OverrideWorkshop("ElectricalInstrumentation", R("ElectricalEngineering"), P("CopperWire", 350, "Glass", 200));
            OverrideWorkshop("ConveyorSystems", R("FactoryOrganization"), P("Machinery", 300, "Steel", 250));
            OverrideWorkshop("StandardGauge", R("RailwayEngineering"), P("Steel", 600, "Coke", 300));
        }

        private static void OverrideWorkshop(
            string id,
            List<Research> research,
            List<Pair<Resource, ExpantaNum>> requirements)
        {
            WorkshopUpgradeDefinition definition = Find<WorkshopUpgradeDefinition>(id);
            definition.ConfigureForEditor(
                research,
                definition.RequiredUpgrades.ToList(),
                requirements,
                definition.Effects.ToList());
            EditorUtility.SetDirty(definition);
        }

        private static void SetWorkshopPrerequisites(string id, params string[] prerequisiteIds)
        {
            WorkshopUpgradeDefinition definition = Find<WorkshopUpgradeDefinition>(id);
            var prerequisites = new List<WorkshopUpgradeDefinition>();
            for (int i = 0; i < prerequisiteIds.Length; i++)
                prerequisites.Add(Find<WorkshopUpgradeDefinition>(prerequisiteIds[i]));
            definition.ConfigureForEditor(
                definition.RequiredResearch.ToList(),
                prerequisites,
                definition.ResourceRequirements.ToList(),
                definition.Effects.ToList());
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureBuildingWorkshopPrerequisites()
        {
            SetWorkshopRequirements("OilRefinery", "ChemicalCatalysts");
            SetWorkshopRequirements("MachineFactory", "PrecisionTooling");
            SetWorkshopRequirements("RailHub", "StandardGauge");
            SetWorkshopRequirements("University", "LaboratoryGlassware");
            SetWorkshopRequirements("ArmsFactory", "MilitaryStandardization");
        }

        private static void DeleteRetiredDefinitions()
        {
            DeleteById<Resource>("StoneTool");
            DeleteById<Resource>("MetalTool");
            DeleteById<Building>("StoneToolWorkshop");
            DeleteById<Building>("Blacksmith");
        }

        private static void NormalizeDefinitionFolders()
        {
            MoveDefinitionIfNeeded<Building>(
                "Pasture", $"{BuildingRoot}/Animal/Pasture.asset");
        }

        private static void MoveDefinitionIfNeeded<T>(string id, string target)
            where T : GameDefinition
        {
            T value = Find<T>(id);
            string source = AssetDatabase.GetAssetPath(value);
            if (string.Equals(source, target, StringComparison.Ordinal))
                return;
            string error = AssetDatabase.MoveAsset(source, target);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }

        private static void MoveDeferredDefinitions()
        {
            for (int i = 0; i < DeferredResourceIds.Length; i++)
            {
                Resource resource = FindOptional<Resource>(DeferredResourceIds[i]);
                if (resource == null)
                    continue;
                string source = AssetDatabase.GetAssetPath(resource);
                if (!source.StartsWith("Assets/Resources/", StringComparison.Ordinal))
                    continue;
                string target = $"Assets/ContentBacklog/Resource/{resource.Id}.asset";
                string error = AssetDatabase.MoveAsset(source, target);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException(error);
            }
        }

        private static void EnsureSceneComponents()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            GameObject manager = GameObject.Find("Manager");
            if (manager == null)
                throw new MissingReferenceException("SampleScene has no Manager object.");
            if (manager.GetComponent<WorkshopManager>() == null)
                manager.AddComponent<WorkshopManager>();

            MainNavigationViewer navigation = UnityEngine.Object.FindObjectOfType<MainNavigationViewer>(true);
            if (navigation == null)
                throw new MissingReferenceException("SampleScene has no MainNavigationViewer.");

            var navigationObject = new SerializedObject(navigation);
            SerializedProperty workshopProperty = navigationObject.FindProperty("WorkshopViewer");
            RectTransform workshopRoot = workshopProperty.objectReferenceValue as RectTransform;
            if (workshopRoot == null)
            {
                RectTransform researchRoot =
                    navigationObject.FindProperty("ResearchViewer").objectReferenceValue as RectTransform;
                Transform parent = researchRoot != null ? researchRoot.parent : navigation.transform;

                var rootObject = new GameObject(
                    "WorkshopViewer", typeof(RectTransform), typeof(Image),
                    typeof(ScrollRect), typeof(WorkshopViewer));
                workshopRoot = rootObject.GetComponent<RectTransform>();
                workshopRoot.SetParent(parent, false);
                workshopRoot.anchorMin = Vector2.zero;
                workshopRoot.anchorMax = Vector2.one;
                workshopRoot.offsetMin = Vector2.zero;
                workshopRoot.offsetMax = Vector2.zero;
                rootObject.GetComponent<Image>().color = new Color(0.035f, 0.04f, 0.05f, 0.96f);

                var viewportObject = new GameObject(
                    "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                RectTransform viewport = viewportObject.GetComponent<RectTransform>();
                viewport.SetParent(workshopRoot, false);
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = new Vector2(16f, 16f);
                viewport.offsetMax = new Vector2(-16f, -16f);
                viewportObject.GetComponent<Image>().color = Color.clear;

                var contentObject = new GameObject("Content", typeof(RectTransform));
                RectTransform content = contentObject.GetComponent<RectTransform>();
                content.SetParent(viewport, false);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = Vector2.one;
                content.pivot = new Vector2(0.5f, 1f);
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;

                ScrollRect scrollRect = rootObject.GetComponent<ScrollRect>();
                scrollRect.viewport = viewport;
                scrollRect.content = content;
                scrollRect.horizontal = false;
                scrollRect.vertical = true;

                var viewerObject = new SerializedObject(rootObject.GetComponent<WorkshopViewer>());
                viewerObject.FindProperty("Content").objectReferenceValue = content;
                viewerObject.ApplyModifiedPropertiesWithoutUndo();

                workshopProperty.objectReferenceValue = workshopRoot;
                navigationObject.ApplyModifiedPropertiesWithoutUndo();
                rootObject.SetActive(false);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ForceReserializeDefinitions()
        {
            var paths = new List<string>();
            paths.AddRange(LoadAll<Resource>().Select(AssetDatabase.GetAssetPath));
            paths.AddRange(LoadAll<Building>().Select(AssetDatabase.GetAssetPath));
            paths.AddRange(LoadAll<Research>().Select(AssetDatabase.GetAssetPath));
            paths.AddRange(LoadAll<WorkshopUpgradeDefinition>().Select(AssetDatabase.GetAssetPath));
            AssetDatabase.ForceReserializeAssets(paths);
        }

        private static void B(
            string id, TechLevel tech, double growth, double territory, double productivityConsumption,
            double productivity, double population, double research, double foodProduction,
            double foodConsumption, double foodCapacity, double powerProduction,
            double powerConsumption, double logisticsProduction, double attack,
            List<Pair<Resource, ExpantaNum>> requirements,
            List<Pair<Resource, ExpantaNum>> generation,
            List<Pair<Resource, ExpantaNum>> consumption,
            List<Research> requiredResearch,
            double defense = 0, double logisticsConsumption = 0, double manpower = 0)
        {
            Building building = Find<Building>(id);
            building.TechLevel = tech;
            if (string.IsNullOrWhiteSpace(building.Label))
                building.Label = id;
            building.ConfigureEconomyForEditor(
                N(growth), N(territory),
                N(productivityConsumption), N(productivity), N(population), N(research),
                N(foodProduction), N(foodConsumption), N(foodCapacity),
                N(powerProduction), N(powerConsumption), N(logisticsProduction),
                N(logisticsConsumption), N(attack), N(defense), N(manpower),
                requirements, generation, consumption);
            building.SetRequiredResearchForEditor(requiredResearch);
            building.SetRequiredWorkshopUpgradesForEditor(
                new List<WorkshopUpgradeDefinition>());
            EditorUtility.SetDirty(building);
        }

        private static void W(
            string id, string label, string category, int order,
            List<Research> research, List<Pair<Resource, ExpantaNum>> requirements,
            params WorkshopEffectDefinition[] effects)
        {
            WorkshopUpgradeDefinition definition = Find<WorkshopUpgradeDefinition>(id);
            definition.Label = label;
            definition.Description = label + "：一次性购买的永久文明升级。";
            definition.Category = category;
            definition.SortOrder = order;
            definition.TechLevel = TechLevel.Industrial;
            definition.ConfigureForEditor(
                research,
                new List<WorkshopUpgradeDefinition>(),
                requirements,
                effects.ToList());
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureResearch(
            string id, string label, double cost, List<Research> prerequisites,
            List<Pair<Resource, ExpantaNum>> requirements,
            params ResearchEffectDefinition[] effects)
        {
            Research research = Find<Research>(id);
            research.Label = label;
            research.Description = label + "推动工业文明的生产与组织能力。";
            research.BaseCost = cost.ToString(System.Globalization.CultureInfo.InvariantCulture);
            research.TechLevel = TechLevel.Industrial;
            research.AdvancesTechLevel = false;
            research.SetPrerequisitesForEditor(prerequisites);
            research.SetResourceRequirementsForEditor(requirements);
            research.SetEffectsForEditor(effects.ToList());
            EditorUtility.SetDirty(research);
        }

        private static void SetWorkshopRequirements(string buildingId, params string[] upgradeIds)
        {
            Building building = Find<Building>(buildingId);
            building.SetRequiredWorkshopUpgradesForEditor(
                upgradeIds.Select(Find<WorkshopUpgradeDefinition>).ToList());
            EditorUtility.SetDirty(building);
        }

        private static void SetResearchEffects(
            string researchId,
            params ResearchEffectDefinition[] effects)
        {
            Research research = Find<Research>(researchId);
            research.SetEffectsForEditor(effects.ToList());
            EditorUtility.SetDirty(research);
        }

        private static ResearchEffectDefinition BuildingMultiplier(string id, double value) =>
            RE(ResearchEffectType.BuildingProductionMultiplier, value, building: Find<Building>(id));
        private static ResearchEffectDefinition BuildingFoodMultiplier(string id, double value) =>
            RE(ResearchEffectType.BuildingFoodProductionMultiplier, value, building: Find<Building>(id));
        private static ResearchEffectDefinition GlobalResearch(double value) =>
            RE(ResearchEffectType.GlobalResearchMultiplier, value);
        private static ResearchEffectDefinition GlobalConstruction(double value) =>
            RE(ResearchEffectType.GlobalConstructionMultiplier, value);
        private static ResearchEffectDefinition GlobalBuildingProduction(double value) =>
            RE(ResearchEffectType.GlobalBuildingProductionMultiplier, value);
        private static ResearchEffectDefinition GlobalLogistics(double value) =>
            RE(ResearchEffectType.GlobalLogisticsMultiplier, value);
        private static ResearchEffectDefinition Power(double value) =>
            RE(ResearchEffectType.PowerMultiplier, value);
        private static ResearchEffectDefinition Military(double value) =>
            RE(ResearchEffectType.MilitaryMultiplier, value);
        private static ResearchEffectDefinition Territory(double value) =>
            RE(ResearchEffectType.TerritoryGranted, value);
        private static ResearchEffectDefinition Productivity(double value) =>
            RE(ResearchEffectType.ProductivityGranted, value);
        private static ResearchEffectDefinition SystemUnlock(string id) =>
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.UnlockSystem,
                SystemId = id,
                Value = ExpantaNum.One
            };

        private static ResearchEffectDefinition RE(
            ResearchEffectType type, double value, Building building = null) =>
            new ResearchEffectDefinition { Type = type, Value = N(value), Building = building };

        private static WorkshopEffectDefinition E(WorkshopEffectType type, double value) =>
            new WorkshopEffectDefinition { Type = type, Value = N(value) };
        private static WorkshopEffectDefinition EB(
            WorkshopEffectType type, string buildingId, double value) =>
            new WorkshopEffectDefinition
            {
                Type = type,
                Building = Find<Building>(buildingId),
                Value = N(value)
            };

        private static void ReplacePairs(
            Building building, string propertyName, Resource stone, Resource bronze,
            bool removeRetiredOutput = false)
        {
            var serialized = new SerializedObject(building);
            SerializedProperty property = serialized.FindProperty(propertyName);
            var replacements = new List<Pair<Resource, ExpantaNum>>();
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty item = property.GetArrayElementAtIndex(i);
                Resource resource = item.FindPropertyRelative("first").objectReferenceValue as Resource;
                ExpantaNum value = ReadExpantaNum(item.FindPropertyRelative("second"));
                if (resource == null)
                    continue;
                if (resource.Id == "StoneTool" || resource.Id == "MetalTool")
                {
                    if (removeRetiredOutput)
                        continue;
                    resource = resource.Id == "StoneTool" ? stone : bronze;
                }
                replacements.Add(new Pair<Resource, ExpantaNum>(resource, value));
            }
            WritePairs(property, MergePairs(replacements));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ExpantaNum ReadExpantaNum(SerializedProperty property)
        {
            SerializedProperty scalar = property.FindPropertyRelative("scalar");
            return scalar == null ? ExpantaNum.Zero : new ExpantaNum(scalar.doubleValue);
        }

        private static void WritePairs(
            SerializedProperty property,
            IReadOnlyList<Pair<Resource, ExpantaNum>> pairs)
        {
            property.arraySize = pairs.Count;
            for (int i = 0; i < pairs.Count; i++)
            {
                SerializedProperty item = property.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("first").objectReferenceValue = pairs[i].First;
                SerializedProperty second = item.FindPropertyRelative("second");
                second.FindPropertyRelative("sign").boolValue = false;
                second.FindPropertyRelative("representation").intValue = 1;
                second.FindPropertyRelative("operatorCount").intValue = 0;
                second.FindPropertyRelative("scalar").doubleValue = pairs[i].Second.ToDouble();
                second.FindPropertyRelative("layer").doubleValue = 0d;
                second.FindPropertyRelative("operators").arraySize = 0;
            }
        }

        private static List<Pair<Resource, ExpantaNum>> MergePairs(
            IEnumerable<Pair<Resource, ExpantaNum>> source)
        {
            var order = new List<Resource>();
            var totals = new Dictionary<Resource, ExpantaNum>();
            foreach (Pair<Resource, ExpantaNum> pair in source)
            {
                if (pair.First == null || pair.Second <= ExpantaNum.Zero)
                    continue;
                if (!totals.ContainsKey(pair.First))
                {
                    totals.Add(pair.First, ExpantaNum.Zero);
                    order.Add(pair.First);
                }
                totals[pair.First] += pair.Second;
            }
            return order.Select(resource =>
                new Pair<Resource, ExpantaNum>(resource, totals[resource])).ToList();
        }

        private static List<Pair<Resource, ExpantaNum>> P(params object[] values)
        {
            var result = new List<Pair<Resource, ExpantaNum>>();
            for (int i = 0; i < values.Length; i += 2)
                result.Add(new Pair<Resource, ExpantaNum>(
                    Find<Resource>((string)values[i]),
                    N(Convert.ToDouble(values[i + 1]))));
            return result;
        }

        private static List<Pair<Resource, ExpantaNum>> Pairs(params object[] values) =>
            P(values);

        private static List<Research> R(params string[] ids) =>
            ids.Select(Find<Research>).ToList();

        private static ExpantaNum N(double value) => new ExpantaNum(value);

        private static void CreateResource(string id, string label, string description)
        {
            Resource resource = LoadOrCreate<Resource>(
                $"{ResourceRoot}/Industrial/{id}.asset",
                id);
            resource.Label = label;
            resource.Description = description;
            resource.DisplayerSet = Resource.Set.UltraTechSet;
            resource.Color = Color.white;
            EditorUtility.SetDirty(resource);
        }

        private static T LoadOrCreate<T>(string path, string id)
            where T : GameDefinition
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null)
            {
                value = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(value, path);
            }
            value.name = id;
            value.SetIdForEditor(id);
            EditorUtility.SetDirty(value);
            return value;
        }

        private static T Find<T>(string id) where T : GameDefinition
        {
            T value = FindOptional<T>(id);
            if (value == null)
                throw new InvalidOperationException($"{typeof(T).Name} '{id}' was not found.");
            return value;
        }

        private static T FindOptional<T>(string id) where T : GameDefinition =>
            LoadAll<T>().FirstOrDefault(value =>
                string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase));

        private static List<T> LoadAll<T>() where T : GameDefinition =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(value => value != null)
                .ToList();

        private static int Count<T>() where T : GameDefinition => LoadAll<T>().Count;

        private static void DeleteById<T>(string id) where T : GameDefinition
        {
            T value = FindOptional<T>(id);
            if (value != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(value));
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
