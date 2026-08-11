using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
            Debug.LogWarning(
                "全局经济迁移器已停用：当前内容资产已经完成工业时代与太空时代迁移，" +
                "旧迁移器会重新生成已淘汰资源和建筑。请直接编辑当前资产或使用专用迁移脚本。", null);
            return;

#pragma warning disable CS0162
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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log(
                $"Global economy migration complete. Resources={Count<Resource>()}, " +
                $"Buildings={Count<Building>()}, Research={Count<Research>()}, " +
                $"Workshop={Count<WorkshopUpgrade>()}.");
#pragma warning restore CS0162
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
            CreateResource("Ceramic", "陶瓷", "从储粮器皿、农业设施到工业隔热与太空结构，跨时代提供耐用非金属材料。");
            CreateResource("RefinedFuel", "精炼燃料", "铁路、军工和内燃机使用的持续燃料。");
            CreateResource("Lubricant", "润滑剂", "机械、线缆和运输设备的持续消耗品。");
            CreateResource("Rubber", "橡胶", "密封、电气绝缘和工业升级材料。");
            CreateResource("CopperWire", "铜线", "机械、电气和通信设备的基础中间件。");
            CreateResource("PrecisionParts", "精密零件", "军工、科研和后续高端设备的核心部件。");
            CreateResource("Engine", "发动机", "运输、工业和后续载具的动力设备。");
            CreateResource("Concrete", "混凝土", "大型工程、中央电站和重型厂房使用的结构材料。");
            CreateResource("BauxiteOre", "铝土矿", "铝冶金使用的轻金属矿物原料。");
            CreateResource("Aluminum", "铝锭", "轻量化结构和高性能导电设备使用的工业金属。");
            CreateResource("Explosives", "工业炸药", "化工厂制造的高能量爆破材料，用于深层采掘、重矿分选和油井开发。");
        }

        private static void CreateIndustrialResearchSkeletons()
        {
            string[] ids =
            {
                "SteamPower", "IndustrialWorkshop", "PetroleumExtraction",
                "SilicaProcessing", "Coking", "IndustrialChemistry",
                "ElectricalEngineering", "PrecisionManufacturing",
                "RailwayEngineering", "ModernUniversity",
                "Standardization", "MassProduction", "Electrification",
                "ScientificMethod", "IndustrialAgriculture", "MilitaryIndustry",
                "LogisticsManagement", "FactoryOrganization", "CombustionEngines",
                "IndustrialAdministration"
            };
            for (int i = 0; i < ids.Length; i++)
                LoadOrCreate<Research>($"{ResearchRoot}/Industrial/{ids[i]}.asset", ids[i]);

            string[] additionalIds =
            {
                "IndustrialMetalSmelting", "IndustrialBronzeCasting",
                "DeepOilDrilling", "IndustrialExplosives",
                "LightMetalOres", "AluminumMetallurgy", "ConcreteEngineering",
                "ElectricalManufacturing", "EngineManufacturing", "SyntheticFertilizers",
                "MechanizedAgricultureSystems", "ElectricalCommunication", "CentralizedGeneration",
                "AcademicJournals", "MilitaryStandardization", "ShiftRegisters", "StandardGauge",
                "TelegraphDispatch", "ReinforcedConcrete", "SyntheticAmmonia", "AdvancedCeramicEngineering"
            };
            for (int i = 0; i < additionalIds.Length; i++)
                LoadOrCreate<Research>($"{ResearchRoot}/Industrial/{additionalIds[i]}.asset", additionalIds[i]);
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

            string[] additionalIds =
            {
                "IndustrialMetalSmelter", "AluminumSmelter", "ConcreteWorks", "AdvancedCeramicsPlant",
                "CentralPowerStation", "IndustrialHabitationComplex"
            };
            for (int i = 0; i < additionalIds.Length; i++)
                LoadOrCreate<Building>($"{BuildingRoot}/Industrial/{additionalIds[i]}.asset", additionalIds[i]);
        }

        private static void CreateWorkshopSkeletons()
        {
            string[] ids =
            {
                "DraftingTables", "ShiftRegisters", "ReinforcedBoilers",
                "InterchangeableParts", "PrecisionTooling", "PoweredMining",
                "IntegratedFurnaces",
                "RotaryDrillingHeads", "ControlledBlasting",
                "ChemicalCatalysts", "AgriculturalMachinery", "MechanicalLooms",
                "RotaryKilns", "LaboratoryGlassware", "ElectricalInstrumentation",
                "ConveyorSystems", "HotBlastStoves", "ContinuousCasting",
                "ConcreteBatching",
                "InsulatedWindings", "BallBearings", "FuelInjection", "BlockSignalling",
                "HighPressureTurbines", "ContinuousDistillation", "PressurizedReactors",
                "StandardizedFreightContainers", "AluminumElectrolyticCells",
                "LightAlloyFrames", "AluminumBusbars", "ReinforcedConcrete",
                "StandardGauge", "TelegraphDispatch", "ReusableLaunchStages",
                "ModularHabitatSystems", "AutomatedShipyardAssembly", "AdvancedCeramicFiring",
                "CryogenicFuelSystems", "AdvancedCompositeLayup", "PhaseMaterialCalibration", "PhantomWeaveLattice",
                "PhantomAlloyRecrystallization"
            };
            for (int i = 0; i < ids.Length; i++)
                LoadOrCreate<WorkshopUpgrade>(
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

            Research mining = Find<Research>("Mining");
            mining.SetResourceRequirementsForEditor(
                Pairs("WoodLog", 180, "StoneChunk", 80));
            EditorUtility.SetDirty(mining);

            SetResearchEffects("StoneTools",
                BuildingMultiplier("Quarry", 1.25),
                BuildingMultiplier("StoneCuttingWorkshop", 1.20),
                GlobalConstruction(1.05));
            SetResearchEffects("Mining",
                BuildingMultiplier("CoalMine", 1.15),
                BuildingMultiplier("MetalMine", 1.15));
            SetResearchEffects("Smithing",
                BuildingMultiplier("MetalSmelter", 1.10),
                GlobalConstruction(1.05));
            SetResearchEffects("ClayExtraction",
                BuildingMultiplier("ClayPit", 1.20));
            SetResearchEffects("Quarry",
                BuildingMultiplier("Quarry", 1.25));
            SetResearchEffects("FoodStorage",
                RE(ResearchEffectType.FoodCapacityMultiplier, 1.10));
            SetResearchEffects("Smithing_Iron",
                GlobalBuildingProduction(1.04));
            SetResearchEffects("ControlledFire",
                BuildingFoodMultiplier("HunterGathererCamp", 1.10),
                BuildingMultiplier("CeramicKiln", 1.10));
            SetResearchEffects("CeramicFiring",
                GlobalConstruction(1.02),
                Deconstruction(0.10));
            SetResearchEffects("Agriculture", PopulationGrowth(1.20));
            SetResearchEffects(
                "NeolithicSettlement",
                Territory(300),
                Productivity(100),
                PopulationGrowth(1.25));
            SetResearchEffects("SmithingRevolution", Territory(500), Productivity(300));
            SetResearchEffects("UrbanHousing",
                GlobalConstruction(1.02),
                Deconstruction(0.25));
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
                Territory(1000),
                Productivity(800),
                Deconstruction(0.50)
            });
            industrialization.TechLevel = TechLevel.Industrial;
            industrialization.AdvancesTechLevel = true;
            EditorUtility.SetDirty(industrialization);

            ConfigureResearch("SteamPower", "蒸汽动力", 75000,
                R("Industrialization"), P("Steel", 300, "Coal", 600),
                Power(1.05));
            ConfigureResearch("IndustrialWorkshop", "工业工坊", 90000,
                R("Industrialization"), P("Steel", 250, "Bronze", 100, "Ceramic", 100),
                Unlock(ResearchEffectType.UnlockIndustrialWorkshop));
            ConfigureResearch("PetroleumExtraction", "石油开采", 110000,
                R("Industrialization"), P("Steel", 250, "Bronze", 120),
                BuildingMultiplier("OilDerrick", 1.05));
            ConfigureResearch("SilicaProcessing", "硅质加工", 120000,
                R("Industrialization"), P("Steel", 200, "Ceramic", 200),
                BuildingMultiplier("Glassworks", 1.05));
            ConfigureResearch("Coking", "炼焦", 140000,
                R("SteamPower"), P("Coal", 800, "StoneBrick", 400),
                BuildingMultiplier("CokeOven", 1.10));
            ConfigureResearch("IndustrialChemistry", "工业化学", 180000,
                R("PetroleumExtraction", "Coking"),
                P("CrudeOil", 500, "Coke", 300, "Glass", 100),
                BuildingMultiplier("ChemicalPlant", 1.10));
            ConfigureResearch("DeepOilDrilling", "深层石油钻探", 420000,
                R("IndustrialChemistry"),
                P("Steel", 600, "Machinery", 120, "Chemical", 100, "CrudeOil", 300),
                BuildingMultiplier("OilDerrick", 1.25));
            ConfigureResearch("IndustrialExplosives", "工业炸药工艺", 460000,
                R("IndustrialChemistry"),
                P("Steel", 500, "Chemical", 250, "Coke", 300, "CrudeOil", 300),
                BuildingMultiplier("ChemicalPlant", 1.25));
            ConfigureResearch("ElectricalEngineering", "电气工程", 220000,
                R("IndustrialChemistry", "Standardization"),
                P("Copper", 500, "Glass", 250, "Steel", 100),
                BuildingMultiplier("WireMill", 1.10));
            ConfigureResearch("PrecisionManufacturing", "精密制造", 280000,
                R("IndustrialChemistry", "ElectricalEngineering"),
                P("Steel", 500, "CopperWire", 200, "Lubricant", 80),
                GlobalConstruction(1.05));
            ConfigureResearch("RailwayEngineering", "铁路工程", 440000,
                R("PrecisionManufacturing"),
                P("Machinery", 150, "Engine", 25, "Steel", 600),
                GlobalLogistics(1.05));
            ConfigureResearch("ModernUniversity", "现代大学", 520000,
                R("Industrialization"),
                P("Steel", 300, "Bronze", 200, "Ceramic", 250),
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
                R("Industrialization"),
                P("Steel", 300, "Bronze", 200, "Ceramic", 250),
                GlobalResearch(1.35));
            ConfigureResearch("IndustrialAgriculture", "工业农业", 1050000,
                R("MassProduction"),
                P("Machinery", 300, "Chemical", 200, "Engine", 30),
                BuildingFoodMultiplier("Farm", 1.50),
                BuildingFoodMultiplier("PlantingField", 1.50),
                BuildingFoodMultiplier("WaterMill", 1.50));
            ConfigureResearch("MilitaryIndustry", "军事工业", 1200000,
                R("MassProduction", "Electrification"),
                P("Machinery", 250, "Chemical", 200, "PrecisionParts", 150, "Rubber", 200),
                GlobalBuildingProduction(1.10));
            ConfigureResearch("LogisticsManagement", "物流管理", 1350000,
                R("RailwayEngineering", "Standardization"),
                P("Machinery", 300, "Electronics", 250, "Engine", 40),
                GlobalLogistics(1.30));
            ConfigureResearch("FactoryOrganization", "工厂组织", 1500000,
                R("Standardization", "ScientificMethod"),
                P("Machinery", 400, "Electronics", 200, "Cloth", 300),
                GlobalBuildingProduction(1.10));
            ConfigureResearch("CombustionEngines", "内燃机", 1650000,
                R("PrecisionManufacturing", "IndustrialChemistry"),
                P("Engine", 100, "RefinedFuel", 500, "Lubricant", 150),
                BuildingMultiplier("RailHub", 1.25),
                GlobalBuildingProduction(1.10));
            ConfigureResearch("IndustrialAdministration", "工业行政", 1800000,
                R("LogisticsManagement", "FactoryOrganization", "MilitaryIndustry"),
                P("Engine", 100, "PrecisionParts", 250, "Electronics", 300,
                    "Chemical", 250, "Steel", 1000),
                Territory(250), GlobalConstruction(1.20));

            ConfigureResearch("IndustrialMetalSmelting", "工业有色金属冶炼", 360000,
                R("Coking", "Industrialization"),
                P("CopperOre", 500, "TinOre", 420, "Coke", 430, "Steel", 270),
                BuildingMultiplier("IndustrialMetalSmelter", 1.10));
            ConfigureResearch("IndustrialBronzeCasting", "工业青铜铸造", 330000,
                R("IndustrialMetalSmelting", "Standardization"),
                P("Copper", 400, "Tin", 220, "Steel", 150),
                BuildingMultiplier("IndustrialMetalSmelter", 1.10));
            ConfigureResearch("LightMetalOres", "轻金属矿床", 480000,
                R("ScientificMethod", "IndustrialLogistics"),
                P("Steel", 300, "Machinery", 120, "Chemical", 80),
                BuildingMultiplier("RareMetalMine", 1.10));
            ConfigureResearch("AluminumMetallurgy", "铝冶金", 1250000,
                R("LightMetalOres", "ElectricalEngineering", "CentralizedGeneration"),
                P("BauxiteOre", 800, "CopperWire", 300, "Ceramic", 180,
                    "Chemical", 200),
                BuildingMultiplier("AluminumSmelter", 1.10));
            ConfigureResearch("ConcreteEngineering", "混凝土工程", 260000,
                R("SilicaProcessing", "IndustrialChemistry"),
                P("StoneBrick", 800, "Silica", 400, "Chemical", 120),
                BuildingMultiplier("ConcreteWorks", 1.10));
            ConfigureResearch("ElectricalManufacturing", "电气制造", 520000,
                R("ElectricalEngineering", "PrecisionManufacturing"),
                P("CopperWire", 300, "Glass", 180, "Ceramic", 100,
                    "Machinery", 120),
                BuildingMultiplier("MachineFactory", 1.12));
            ConfigureResearch("EngineManufacturing", "发动机制造", 680000,
                R("CombustionEngines", "PrecisionManufacturing"),
                P("Machinery", 200, "PrecisionParts", 120, "RefinedFuel", 250,
                    "Lubricant", 100),
                BuildingMultiplier("MachineFactory", 1.12));
            ConfigureResearch("SyntheticFertilizers", "合成肥料", 560000,
                R("IndustrialChemistry", "IndustrialAgriculture"),
                P("Chemical", 350, "Coke", 200, "Glass", 100),
                BuildingFoodMultiplier("Farm", 1.20),
                BuildingFoodMultiplier("PlantingField", 1.20));
            ConfigureResearch("MechanizedAgricultureSystems", "农业机械化体系", 780000,
                R("SyntheticFertilizers", "EngineManufacturing", "IndustrialAgriculture"),
                P("Machinery", 250, "Engine", 40, "Steel", 400, "RefinedFuel", 250),
                BuildingFoodMultiplier("Farm", 1.25),
                BuildingFoodMultiplier("PlantingField", 1.25),
                BuildingFoodMultiplier("WaterMill", 1.20));
            ConfigureResearch("ElectricalCommunication", "电气通信", 450000,
                R("ElectricalEngineering", "RailwayEngineering"),
                P("CopperWire", 350, "Electronics", 150, "Glass", 100),
                BuildingMultiplier("RailHub", 1.10),
                BuildingMultiplier("University", 1.05));
            ConfigureResearch("CentralizedGeneration", "集中式发电", 1100000,
                R("PowerGridEngineering", "PrecisionManufacturing", "ConcreteEngineering"),
                P("Machinery", 450, "CopperWire", 400, "Ceramic", 250,
                    "Concrete", 900),
                BuildingMultiplier("CentralPowerStation", 1.10));
            ConfigureResearch("AcademicJournals", "学术出版体系", 720000,
                R("ModernUniversity", "ScientificMethod"),
                P("Electronics", 100, "Glass", 150, "Cloth", 300),
                RE(ResearchEffectType.BuildingResearchPowerMultiplier, 1.25,
                    Find<Building>("University")));
            ConfigureResearch("MilitaryStandardization", "军工标准化", 980000,
                R("MilitaryIndustry", "Standardization"),
                P("Engine", 50, "PrecisionParts", 200, "Chemical", 200, "Steel", 800),
                GlobalBuildingProduction(1.10));
            ConfigureResearch("ShiftRegisters", "工业轮班制度", 260000,
                R("IndustrialWorkshop", "FactoryOrganization"),
                P("Ceramic", 200, "Cloth", 200, "Steel", 150),
                GlobalBuildingProduction(1.10));
            ConfigureResearch("StandardGauge", "标准轨距", 380000,
                R("RailwayEngineering", "Standardization"),
                P("Coke", 300, "Steel", 600),
                RE(ResearchEffectType.BuildingLogisticsProductionMultiplier, 1.30,
                    Find<Building>("RailHub")));
            ConfigureResearch("TelegraphDispatch", "电报调度制度", 620000,
                R("ElectricalCommunication", "LogisticsManagement"),
                P("Electronics", 180, "CopperWire", 400, "Machinery", 120),
                GlobalLogistics(1.15));
            ConfigureResearch("ReinforcedConcrete", "钢筋混凝土结构", 760000,
                R("ConcreteEngineering", "FactoryOrganization"),
                P("Concrete", 800, "Steel", 400, "PrecisionParts", 40,
                    "Ceramic", 150),
                Territory(150), GlobalConstruction(1.15));
            ConfigureResearch("SyntheticAmmonia", "合成氨工艺", 690000,
                R("SyntheticFertilizers", "ProcessControl"),
                P("Chemical", 300, "Coke", 200, "Glass", 100),
                BuildingFoodMultiplier("Farm", 1.15),
                BuildingFoodMultiplier("PlantingField", 1.15));
        }

        private static void ConfigureAllBuildings()
        {
            B("WoodHouse", TechLevel.Animal, 1.18, 3, 0, 0, 5, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 80), P(), P(), R());
            B("HunterGathererCamp", TechLevel.Animal, 1.15, 2, 2, 0, 0, 0, 3, 1, 0,
                0, 0, 0, 0, P("WoodLog", 30), P(), P(), R("ForagingGroups"));
            B("Farm", TechLevel.Animal, 1.14, 4, 3, 0, 0, 0, 8, 0, 0,
                0, 0, 0, 0, P("WoodLog", 50), P(), P(), R("Agriculture"));
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
                0, 0, 0, 0, P("WoodLog", 60), P("Biomass", 1.5), P(),
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
            B("MetalMine", TechLevel.Neolithic, 1.15, 8, 10, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 180, "StoneChunk", 140),
                P("CopperOre", 1, "TinOre", .8, "IronOre", .8), P(),
                R("NeolithicSettlement", "Mining"));
            B("CeramicKiln", TechLevel.Neolithic, 1.16, 4, 5, 0, 0, 0, 0, 0, 250,
                0, 0, 0, 0, P("Clay", 50, "WoodLog", 50), P("Ceramic", 1),
                P("Clay", 1, "WoodLog", .2), R("NeolithicSettlement", "CeramicFiring", "ClayExtraction"));
            B("WeavingWorkshop", TechLevel.Neolithic, 1.16, 4, 5, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("Biomass", 50, "WoodLog", 40), P("Cloth", 1),
                P("Biomass", 1), R("NeolithicSettlement", "TextileCraft", "Agriculture"));
            B("MetalSmelter", TechLevel.Neolithic, 1.17, 8, 10, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 100, "StoneBrick", 80),
                P("Copper", .8, "Tin", .8, "Iron", .8, "Bronze", .6),
                P("CopperOre", 1, "TinOre", 1, "IronOre", 1, "Coal", 1.5),
                R("Smithing_Copper"));
            B("Granary", TechLevel.Neolithic, 1.18, 4, 2, 0, 0, 0, 0, 0, 1000,
                0, 0, 0, 0, P("WoodLog", 150, "StoneBrick", 100, "Ceramic", 30),
                P(), P(), R("FoodStorage", "CeramicFiring"));
            B("ScribeHut", TechLevel.Neolithic, 1.20, 5, 5, 0, 0, 5, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 200, "StoneBrick", 100, "Ceramic", 20, "Cloth", 20),
                P(), P(), R("WrittenRecords", "CeramicFiring", "TextileCraft"));

            B("WaterMill", TechLevel.Medieval, 1.20, 8, 30, 0, 0, 0, 10, 0, 0,
                0, 0, 0, 0, P("WoodLog", 200, "StoneBrick", 150, "Ceramic", 50, "Bronze", 30),
                P(), P(), R("SmithingRevolution", "WaterManagement", "MechanicalEngineering"));
            B("SteelForge", TechLevel.Medieval, 1.20, 10, 48, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 300, "StoneBrick", 250, "Coal", 100, "Iron", 100),
                P("Steel", 1), P("Iron", 1, "Coal", 1),
                R("SmithingRevolution", "Smithing_Iron", "Steelmaking"));
            B("Library", TechLevel.Medieval, 1.22, 6, 36, 0, 0, 25, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 250, "StoneBrick", 200, "Cloth", 50, "Bronze", 30),
                P(), P(), R("SmithingRevolution", "WrittenRecords", "Bookmaking"));
            B("Market", TechLevel.Medieval, 1.20, 5, 30, 0, 0, 5, 0, 0, 0,
                0, 0, 0, 0, P("WoodLog", 200, "StoneBrick", 100, "Cloth", 30, "Ceramic", 30),
                P(), P(), R("SmithingRevolution", "TradeRoutes", "Measurement"));

            B("SteamPlant", TechLevel.Industrial, 1.20, 18, 60, 0, 0, 0, 0, 0, 0,
                120, 0, 0, 0, P("StoneBrick", 500, "Steel", 250, "Coal", 400),
                P(), P("Coal", 2), R("Industrialization", "SteamPower"), 0, 4);
            B("OilDerrick", TechLevel.Industrial, 1.15, 14, 50, 0, 0, 0, 0, 0, 0,
                0, 8, 0, 0, P("Steel", 500, "Bronze", 250),
                P("CrudeOil", 3), P(), R("Industrialization", "PetroleumExtraction"), 0, 6);
            B("SilicaQuarry", TechLevel.Industrial, 1.15, 12, 50, 0, 0, 0, 0, 0, 0,
                0, 6, 0, 0, P("Steel", 350, "Bronze", 200),
                P("Silica", 2), P(), R("Industrialization", "SilicaProcessing"), 0, 4);
            B("CokeOven", TechLevel.Industrial, 1.18, 14, 60, 0, 0, 0, 0, 0, 0,
                0, 5, 0, 0, P("StoneBrick", 600, "Steel", 300, "Coal", 500),
                P("Coke", 1.5), P("Coal", 2), R("SteamPower", "Coking"), 0, 4);
            B("Glassworks", TechLevel.Industrial, 1.18, 14, 60, 0, 0, 0, 0, 0, 0,
                0, 12, 0, 0, P("StoneBrick", 500, "Steel", 250, "Ceramic", 300),
                P("Glass", 1, "Ceramic", .2),
                P("Silica", 1.5, "Coal", .4, "Ceramic", .2),
                R("SilicaProcessing", "Coking"), 0, 6);
            B("ChemicalPlant", TechLevel.Industrial, 1.20, 18, 70, 0, 0, 0, 0, 0, 0,
                0, 20, 0, 0, P("Steel", 500, "StoneBrick", 400, "Coke", 150, "Cloth", 100),
                P("Chemical", 1, "Explosives", .35, "RocketFuel", .25), P("CrudeOil", 1.2, "Coke", .4, "Cloth", .05),
                R("PetroleumExtraction", "Coking", "IndustrialChemistry"), 0, 10);
            B("OilRefinery", TechLevel.Industrial, 1.21, 20, 80, 0, 0, 0, 0, 0, 0,
                0, 25, 0, 0, P("Steel", 850, "Bronze", 250, "Copper", 200, "Chemical", 100),
                P("RefinedFuel", 1.2, "Lubricant", .4, "Rubber", .3),
                P("CrudeOil", 2, "Chemical", .2, "Explosives", .10),
                R("IndustrialChemistry", "Standardization"), 0, 12);
            B("WireMill", TechLevel.Industrial, 1.20, 16, 70, 0, 0, 0, 0, 0, 0,
                0, 18, 0, 0, P("Steel", 500, "Copper", 300, "Bronze", 150),
                P("CopperWire", 1.5), P("Copper", 1.2, "Tin", .1, "Lubricant", .05),
                R("ElectricalEngineering", "Standardization"), 0, 10);
            B("MachineFactory", TechLevel.Industrial, 1.20, 20, 80, 0, 0, 0, 0, 0, 0,
                0, 30, 0, 0, P("Steel", 600, "CopperWire", 200, "Coke", 200, "Bronze", 200),
                P("Machinery", 1, "Electronics", .3, "PrecisionParts", .35, "Engine", .15, "Composite", .15),
                P("Steel", .8, "Coke", .4, "Lubricant", .15, "CopperWire", .4,
                    "Bronze", .1, "Ceramic", .05, "Rubber", .1, "Aluminum", .08),
                R("PrecisionManufacturing"), 0, 18);
            B("RailHub", TechLevel.Industrial, 1.20, 25, 70, 0, 0, 0, 0, 0, 0,
                0, 8, 100, 0, P("Machinery", 180, "Electronics", 120, "Steel", 300, "Engine", 30),
                P(), P("RefinedFuel", .3, "Lubricant", .05, "Engine", .05, "Machinery", .08),
                R("RailwayEngineering", "CombustionEngines"));
              B("University", TechLevel.Industrial, 1.20, 12, 60, 0, 0, 250, 0, 0, 0,
                  0, 10, 0, 0, P("Machinery", 100, "Chemical", 80, "Electronics", 100, "Glass", 150),
                  P(), P("Electronics", .02, "Glass", .05),
                  R("ModernUniversity", "ScientificMethod"), 0, 5);

            B("IndustrialMetalSmelter", TechLevel.Industrial, 1.20, 18, 65, 0, 0, 0, 0, 0, 0,
                0, 22, 0, 0, P("StoneBrick", 400, "Steel", 400, "Machinery", 100,
                    "Coke", 200, "Bronze", 130),
                P("Copper", 3, "Tin", 2.4, "Iron", 1.8, "Bronze", 2.5, "Steel", 3.2),
                P("CopperOre", 2.2, "TinOre", 1.8, "IronOre", 2.4,
                    "Coke", 1.35, "Chemical", .08, "Copper", 1.6, "Tin", .8),
                R("IndustrialMetalSmelting"), 0, 10);
            B("AluminumSmelter", TechLevel.Industrial, 1.20, 18, 85, 0, 0, 0, 0, 0, 0,
                0, 100, 0, 0, P("Concrete", 500, "Steel", 400, "Machinery", 180,
                    "Ceramic", 120),
                P("Aluminum", 1.2), P("BauxiteOre", 2, "Chemical", .3),
                R("AluminumMetallurgy", "CentralizedGeneration"), 0, 10);
            B("ConcreteWorks", TechLevel.Industrial, 1.20, 16, 60, 0, 0, 0, 0, 0, 0,
                0, 10, 0, 0, P("StoneBrick", 500, "Steel", 200, "Machinery", 60),
                P("Concrete", 2), P("StoneBrick", 2, "Silica", .8, "Chemical", .2),
                R("ConcreteEngineering"), 0, 4);
            B("CentralPowerStation", TechLevel.Industrial, 1.22, 28, 110, 0, 0, 0, 0, 0, 0,
                300, 0, 0, 0, P("Concrete", 1000, "Steel", 800, "Machinery", 250,
                    "CopperWire", 250, "Ceramic", 100),
                P(), P("Coke", 3, "Lubricant", .12, "Chemical", .05),
                R("CentralizedGeneration", "PowerGridEngineering"), 0, 15);
            B("IndustrialHabitationComplex", TechLevel.Industrial, 1.22, 42, 160, 0, 240, 0, 0, 0, 0,
                0, 0, 0, 0, P("Concrete", 1400, "Steel", 900, "Glass", 500,
                    "Ceramic", 250, "Electronics", 200),
                P(), P("Electronics", .03, "Ceramic", .02),
                R("Industrialization"));

            LinkBuildingUpgrade("MetalSmelter", "IndustrialMetalSmelter");
            LinkBuildingUpgrade("SteelForge", "IndustrialMetalSmelter");
            LinkBuildingUpgrade("SteamPlant", "CentralPowerStation");
            LinkBuildingUpgrade("IndustrialHabitationComplex", "OrbitalHabitatMegastructure");
        }

        private static void ConfigureWorkshopUpgrades()
        {
            W("DraftingTables", "制图台", 10, R("IndustrialWorkshop"),
                P("WoodLog", 500, "Cloth", 150, "Bronze", 100), E(WorkshopEffectType.GlobalConstructionMultiplier, 1.10));
            W("ShiftRegisters", "班次登记系统", 15, R("FactoryOrganization"),
                P("WoodLog", 120, "Cloth", 80), E(WorkshopEffectType.GlobalBuildingProductionMultiplier, 1.03));
            W("StandardGauge", "标准轨距", 175, R("RailwayEngineering"),
                P("Steel", 250, "Machinery", 80), E(WorkshopEffectType.GlobalLogisticsMultiplier, 1.05));
            W("TelegraphDispatch", "电报调度", 180, R("ElectricalCommunication"),
                P("CopperWire", 180, "Glass", 60), E(WorkshopEffectType.GlobalLogisticsMultiplier, 1.05));
            W("ReinforcedBoilers", "强化锅炉", 30, R("SteamPower"),
                P("Coal", 350, "Iron", 300),
                EB(WorkshopEffectType.BuildingPowerProductionMultiplier, "SteamPlant", 1.25));
            W("InterchangeableParts", "互换零件", 40, R("PrecisionManufacturing"),
                P("Steel", 400, "Copper", 250),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "MachineFactory", 1.20));
            W("PrecisionTooling", "精密加工", 50, R("PrecisionManufacturing"),
                P("Steel", 600, "Bronze", 250, "CopperWire", 200, "Lubricant", 80),
                E(WorkshopEffectType.GlobalConstructionMultiplier, 1.10));
            W("PoweredMining", "动力采矿", 60, R("PrecisionManufacturing"),
                P("Machinery", 180, "Steel", 450, "Lubricant", 80),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "MetalMine", 1.2),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "RareMetalMine", 1.2));
            W("IntegratedFurnaces", "综合金属精炼炉组", 215,
                R("IndustrialWorkshop", "IndustrialMetalSmelting", "Coking", "ElectricalEngineering"),
                P("Steel", 450, "Bronze", 300, "Machinery", 200, "CopperWire", 200, "Chemical", 100, "Ceramic", 120, "Lubricant", 60),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "IndustrialMetalSmelter", 1.25));
            W("ChemicalCatalysts", "化学催化剂", 70, R("IndustrialChemistry"),
                P("Chemical", 250, "Coke", 300, "Glass", 100),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "ChemicalPlant", 1.25));
            W("RotaryDrillingHeads", "旋转钻头组", 75, R("DeepOilDrilling"),
                P("Steel", 350, "Machinery", 150, "Chemical", 100, "CrudeOil", 250),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "OilDerrick", 1.20));
            W("ControlledBlasting", "精确爆破工艺", 85,
                R("IndustrialExplosives"),
                P("Steel", 300, "Machinery", 120, "Chemical", 120),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "RareMetalMine", 1.15),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "OilDerrick", 1.15));
            W("AgriculturalMachinery", "农业机械", 80, R("IndustrialAgriculture"),
                P("Machinery", 120, "Engine", 20, "Chemical", 80),
                EB(WorkshopEffectType.BuildingFoodProductionMultiplier, "Farm", 1.30));
            W("MechanicalLooms", "机械织机", 90, R("PrecisionManufacturing"),
                P("Machinery", 100, "Steel", 200, "Cloth", 200),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "WeavingWorkshop", 1.50));
            W("RotaryKilns", "回转窑", 100, R("IndustrialChemistry"),
                P("Coal", 250, "Clay", 250, "Ceramic", 150),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "Glassworks", 1.35));
            W("LaboratoryGlassware", "实验玻璃器具", 140, R("ScientificMethod"),
                P("Chemical", 180, "Glass", 300, "Ceramic", 80),
                E(WorkshopEffectType.GlobalResearchMultiplier, 1.15));
            W("ElectricalInstrumentation", "电气仪表", 150, R("Electrification"),
                P("Electronics", 250, "CopperWire", 500, "Chemical", 150),
                E(WorkshopEffectType.PowerMultiplier, 1.20),
                E(WorkshopEffectType.GlobalBuildingProductionMultiplier, 1.10));
            W("ConveyorSystems", "传送系统", 160, R("MassProduction"),
                P("Machinery", 300, "Steel", 250),
                E(WorkshopEffectType.GlobalBuildingProductionMultiplier, 1.15));
            W("HotBlastStoves", "热风炉组", 190, R("IndustrialMetalSmelting"),
                P("Steel", 300, "Coke", 200, "Machinery", 80),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "IndustrialMetalSmelter", 1.15));
            W("ContinuousCasting", "连铸机组", 200, R("IndustrialMetalSmelting"),
                P("Steel", 400, "Machinery", 120, "Ceramic", 80),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "IndustrialMetalSmelter", 1.15));
            W("ConcreteBatching", "混凝土搅拌机组", 230, R("ConcreteEngineering"),
                P("Concrete", 300, "Machinery", 80),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "ConcreteWorks", 1.20));
            W("ReinforcedConcrete", "钢筋混凝土施工", 235, R("ConcreteEngineering"),
                P("Concrete", 400, "Steel", 150),
                E(WorkshopEffectType.GlobalConstructionMultiplier, 1.05));
            W("InsulatedWindings", "绝缘绕组组件", 240, R("ElectricalManufacturing"),
                P("CopperWire", 300, "Ceramic", 100, "Glass", 80),
                E(WorkshopEffectType.PowerMultiplier, 1.10));
            W("BallBearings", "滚珠轴承", 250, R("PrecisionManufacturing"),
                P("Steel", 350, "Lubricant", 100),
                E(WorkshopEffectType.GlobalBuildingProductionMultiplier, 1.08));
            W("FuelInjection", "燃油喷射器", 260, R("EngineManufacturing"),
                P("PrecisionParts", 100, "RefinedFuel", 150, "Steel", 120),
                E(WorkshopEffectType.GlobalBuildingProductionMultiplier, 1.08));
            W("BlockSignalling", "电气闭塞机", 270,
                R("ElectricalCommunication", "StandardGauge", "TelegraphDispatch"),
                P("Electronics", 150, "CopperWire", 250, "Machinery", 100),
                E(WorkshopEffectType.GlobalLogisticsMultiplier, 1.10));
            W("HighPressureTurbines", "高压汽轮机", 280, R("CentralizedGeneration"),
                P("Steel", 500, "Machinery", 180, "Ceramic", 100),
                EB(WorkshopEffectType.BuildingPowerProductionMultiplier, "CentralPowerStation", 1.20));
            W("ContinuousDistillation", "连续蒸馏塔", 290, R("ProcessControl"),
                P("Steel", 300, "Glass", 150, "Chemical", 120),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "OilRefinery", 1.15));
            W("PressurizedReactors", "加压反应釜", 300, R("ProcessControl"),
                P("Steel", 350, "Ceramic", 120, "Glass", 100),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "ChemicalPlant", 1.15));
            W("StandardizedFreightContainers", "标准货运集装箱", 310,
                R("IndustrialLogistics", "StandardGauge", "TelegraphDispatch"),
                P("Steel", 500, "Machinery", 100),
                EB(WorkshopEffectType.BuildingLogisticsProductionMultiplier, "RailHub", 1.15));
            W("AluminumElectrolyticCells", "铝电解槽组", 320, R("AluminumMetallurgy"),
                P("Aluminum", 300, "CopperWire", 150, "Ceramic", 100),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "AluminumSmelter", 1.20));
            W("LightAlloyFrames", "轻合金框架", 330, R("AluminumMetallurgy"),
                P("Aluminum", 300, "Steel", 100, "Machinery", 80),
                E(WorkshopEffectType.GlobalConstructionMultiplier, 1.10));
            W("AluminumBusbars", "铝制母线排", 340,
                R("AluminumMetallurgy", "CentralizedGeneration"),
                P("Aluminum", 250, "CopperWire", 100),
                E(WorkshopEffectType.PowerMultiplier, 1.10));
            W("ReusableLaunchStages", "可复用发射级", 410, R("OrbitalEngineering"),
                P("Aluminum", 500, "Engine", 180, "Ceramic", 220, "RefinedFuel", 250),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "LaunchCenter", 1.25));
            W("ModularHabitatSystems", "模块化空间舱", 420, R("OrbitalHabitation"),
                P("Concrete", 700, "Aluminum", 600, "Electronics", 350, "Ceramic", 180),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "OrbitalStation", 1.25));
            W("AutomatedShipyardAssembly", "自动化船坞装配", 430, R("DeepSpaceShipbuilding"),
                P("Steel", 1200, "Machinery", 900, "Engine", 300, "Electronics", 500, "Lubricant", 300),
                EB(WorkshopEffectType.BuildingProductionMultiplier, "Shipyard", 1.25));
            W("CryogenicFuelSystems", "低温推进剂系统", 440, R("OrbitalEngineering"),
                P("RocketFuel", 300, "Aluminum", 300, "Ceramic", 150, "Lubricant", 180),
                ER(WorkshopEffectType.ResourceProductionMultiplier, "RocketFuel", 1.30));
            W("AdvancedCompositeLayup", "先进复合材料铺层", 450, R("DeepSpaceShipbuilding"),
                P("Composite", 300, "Aluminum", 500, "Ceramic", 200, "Electronics", 250),
                ER(WorkshopEffectType.ResourceProductionMultiplier, "Composite", 1.30));
            SetWorkshopPrerequisites("AgriculturalMachinery", "DraftingTables");
            SetWorkshopPrerequisites("ChemicalCatalysts", "RotaryKilns");
            SetWorkshopPrerequisites("ConveyorSystems", "InterchangeableParts");
            SetWorkshopPrerequisites("ElectricalInstrumentation", "LaboratoryGlassware");
            SetWorkshopPrerequisites("InterchangeableParts", "PrecisionTooling");
            SetWorkshopPrerequisites("MechanicalLooms", "ConveyorSystems");
            SetWorkshopPrerequisites("PoweredMining", "InterchangeableParts");
            SetWorkshopPrerequisites("IntegratedFurnaces", "PoweredMining", "PrecisionTooling", "ElectricalInstrumentation");
            SetWorkshopPrerequisites("RotaryDrillingHeads", "ChemicalCatalysts");
            SetWorkshopPrerequisites("ControlledBlasting", "RotaryDrillingHeads");
            SetWorkshopPrerequisites("PrecisionTooling", "DraftingTables");
            SetWorkshopPrerequisites("ReinforcedConcrete", "RotaryKilns");
            SetWorkshopPrerequisites("RotaryKilns", "LaboratoryGlassware");
            SetWorkshopPrerequisites("HotBlastStoves", "ReinforcedBoilers");
            SetWorkshopPrerequisites("ContinuousCasting", "HotBlastStoves");
            SetWorkshopPrerequisites("ConcreteBatching", "RotaryKilns");
            SetWorkshopPrerequisites("InsulatedWindings", "ElectricalInstrumentation");
            SetWorkshopPrerequisites("BallBearings", "PrecisionTooling");
            SetWorkshopPrerequisites("FuelInjection", "BallBearings");
            SetWorkshopPrerequisites("BlockSignalling", "ElectricalInstrumentation");
            SetWorkshopPrerequisites("HighPressureTurbines", "ReinforcedBoilers");
            SetWorkshopPrerequisites("ContinuousDistillation", "ChemicalCatalysts");
            SetWorkshopPrerequisites("PressurizedReactors", "ChemicalCatalysts");
            SetWorkshopPrerequisites("StandardizedFreightContainers", "StandardGauge");
            SetWorkshopPrerequisites("AluminumElectrolyticCells", "IntegratedFurnaces");
            SetWorkshopPrerequisites("LightAlloyFrames", "AluminumElectrolyticCells");
            SetWorkshopPrerequisites("AluminumBusbars", "AluminumElectrolyticCells");
            SetWorkshopPrerequisites("ModularHabitatSystems", "ReusableLaunchStages");
            SetWorkshopPrerequisites("AutomatedShipyardAssembly", "ModularHabitatSystems");
            SetWorkshopPrerequisites("CryogenicFuelSystems", "ReusableLaunchStages");
            SetWorkshopPrerequisites("AdvancedCompositeLayup", "AutomatedShipyardAssembly");
        }

        private static void ConfigureWorkshopClosureOverrides()
        {
            OverrideWorkshop("ReinforcedBoilers", R("SteamPower"), P("Coal", 350, "Iron", 300));
            OverrideWorkshop("InterchangeableParts", R("PrecisionManufacturing"), P("Steel", 400, "Copper", 250));
            OverrideWorkshop("RotaryKilns", R("IndustrialChemistry"), P("Coal", 250, "Clay", 250, "Ceramic", 150));
            OverrideWorkshop("ElectricalInstrumentation", R("ElectricalEngineering"), P("CopperWire", 350, "Glass", 200));
            OverrideWorkshop("ConveyorSystems", R("FactoryOrganization"), P("Machinery", 300, "Steel", 250));
        }

        private static void OverrideWorkshop(
            string id,
            List<Research> research,
            List<Pair<Resource, ExpantaNum>> requirements)
        {
            WorkshopUpgrade definition = Find<WorkshopUpgrade>(id);
            definition.ConfigureForEditor(
                research,
                definition.RequiredUpgrades.ToList(),
                requirements,
                definition.Effects.ToList());
            EditorUtility.SetDirty(definition);
        }

        private static void SetWorkshopPrerequisites(string id, params string[] prerequisiteIds)
        {
            WorkshopUpgrade definition = Find<WorkshopUpgrade>(id);
            var prerequisites = new List<WorkshopUpgrade>();
            for (int i = 0; i < prerequisiteIds.Length; i++)
                prerequisites.Add(Find<WorkshopUpgrade>(prerequisiteIds[i]));
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
            SetWorkshopRequirements("RailHub", "StandardizedFreightContainers");
            SetWorkshopRequirements("University", "LaboratoryGlassware");
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

        private static void ForceReserializeDefinitions()
        {
            var paths = new List<string>();
            paths.AddRange(LoadAll<Resource>().Select(AssetDatabase.GetAssetPath));
            paths.AddRange(LoadAll<Building>().Select(AssetDatabase.GetAssetPath));
            paths.AddRange(LoadAll<Research>().Select(AssetDatabase.GetAssetPath));
            paths.AddRange(LoadAll<WorkshopUpgrade>().Select(AssetDatabase.GetAssetPath));
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
                new List<WorkshopUpgrade>());
            EditorUtility.SetDirty(building);
        }

        private static void W(
            string id, string label, int order,
            List<Research> research, List<Pair<Resource, ExpantaNum>> requirements,
            params WorkshopEffectDefinition[] effects)
        {
            WorkshopUpgrade definition = Find<WorkshopUpgrade>(id);
            definition.Label = label;
            definition.Description = WorkshopDescription(id, label);
            definition.SortOrder = order;
            definition.TechLevel = TechLevel.Industrial;
            definition.ConfigureForEditor(
                research,
                new List<WorkshopUpgrade>(),
                requirements,
                effects.ToList());
            EditorUtility.SetDirty(definition);
        }

        private static string WorkshopDescription(string id, string label)
        {
            var descriptions = new Dictionary<string, string>
            {
                ["DraftingTables"] = "标准化工程制图与施工放样，降低所有建筑的建造成本。",
                ["ReinforcedBoilers"] = "提高蒸汽锅炉的耐压能力，使蒸汽电站产生更多电力。",
                ["InterchangeableParts"] = "以统一规格生产可互换零件，提高机床厂的产出效率。",
                ["PrecisionTooling"] = "精密刀具和量具提升加工精度，进一步降低建筑建造成本。",
                ["PoweredMining"] = "使用动力采掘设备提高多金属矿场的铜矿、锡矿和铁矿产量，减少采掘损耗。",
                ["RotaryDrillingHeads"] = "耐磨钻头与泥浆循环系统提高深层油井的连续产量。",
                ["ControlledBlasting"] = "定向装药与爆破测量同时提升铝土、稀有金属和石油采掘效率。",
                ["ChemicalCatalysts"] = "催化剂缩短化学反应流程，提高化工厂的连续产出。",
                ["AgriculturalMachinery"] = "机械化耕作扩大单位劳动力的耕种面积，提高农场食物产量。",
                ["MechanicalLooms"] = "机械织机将纺织流程连续化，大幅提高织布作坊产量。",
                ["RotaryKilns"] = "回转窑提供稳定高温，用于提升玻璃厂的熔炼与成型效率。",
                ["LaboratoryGlassware"] = "标准化实验玻璃器皿提高实验重复性，加快后续研究。",
                ["ElectricalInstrumentation"] = "电气仪表改善测量与控制，使工业建筑获得更高产能并提高电网效率。",
                ["ConveyorSystems"] = "传送系统连接工位并减少搬运等待，提升全体工业建筑的产能。",
                ["HotBlastStoves"] = "热风炉回收炉气预热鼓风，降低高炉热损失并提高钢铁产量。",
                ["ContinuousCasting"] = "连续铸造减少钢液转运和冷却损耗，进一步提高高炉产能。",
                ["ConcreteBatching"] = "自动配料和搅拌统一混凝土质量，提高混凝土厂产出。",
                ["ReinforcedConcrete"] = "钢筋混凝土提高结构强度和耐久性，降低大型建筑的建造成本。",
                ["InsulatedWindings"] = "绝缘绕组降低漏电与发热损耗，提高电力系统效率。",
                ["BallBearings"] = "滚动轴承减少旋转设备摩擦，使工业建筑整体运行更高效。",
                ["FuelInjection"] = "燃油喷射精确控制燃料供给，提高工业系统的综合产能。",
                ["BlockSignalling"] = "电气闭塞信号提升铁路区间利用率，提高物流网络效率。",
                ["HighPressureTurbines"] = "高压汽轮机提高蒸汽膨胀效率，使中央火力电站产生更多电力。",
                ["ContinuousDistillation"] = "连续蒸馏塔稳定分离石油馏分，提高炼油厂产出。",
                ["PressurizedReactors"] = "加压反应釜改善化学反应条件，提高化工厂产能。",
                ["StandardizedFreightContainers"] = "标准货运集装箱减少装卸时间，提高铁路枢纽物流吞吐量。",
                ["AluminumElectrolyticCells"] = "电解槽以稳定电流制取铝，提高铝冶炼厂产能。",
                ["LightAlloyFrames"] = "铝合金框架以较低重量提供结构强度，降低建筑建造成本。",
                ["AluminumBusbars"] = "铝制母线降低输电线路重量与损耗，提高电力系统效率。"
            };
            return descriptions.TryGetValue(id, out string description)
                ? description
                : $"{label}应用工业化工艺，提供可验证的永久生产或基础设施效果。";
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
                upgradeIds.Select(Find<WorkshopUpgrade>).ToList());
            EditorUtility.SetDirty(building);
        }

        private static void LinkBuildingUpgrade(string sourceId, string targetId)
        {
            Building source = Find<Building>(sourceId);
            Building target = Find<Building>(targetId);
            source.SetUpgradeToForEditor(target);
            EditorUtility.SetDirty(source);
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
        private static ResearchEffectDefinition Territory(double value) =>
            RE(ResearchEffectType.TerritoryGranted, value);
        private static ResearchEffectDefinition Productivity(double value) =>
            RE(ResearchEffectType.ProductivityGranted, value);
        private static ResearchEffectDefinition PopulationGrowth(double value) =>
            RE(ResearchEffectType.PopulationGrowthMultiplier, value);
        private static ResearchEffectDefinition Deconstruction(double value) =>
            RE(ResearchEffectType.DeconstructionReturnRate, value);
        private static ResearchEffectDefinition Unlock(ResearchEffectType type) =>
            new ResearchEffectDefinition
            {
                Type = type,
                Value = ExpantaNum.One
            };

        private static ResearchEffectDefinition RE(
            ResearchEffectType type, double value, Building building = null) =>
            new ResearchEffectDefinition { Type = type, Value = N(value), Building = building };

        private static WorkshopEffectDefinition E(WorkshopEffectType type, double value) =>
            new WorkshopEffectDefinition { Type = type, Value = N(value) };
        private static WorkshopEffectDefinition ER(
            WorkshopEffectType type, string resourceId, double value) =>
            new WorkshopEffectDefinition
            {
                Type = type,
                Resource = Find<Resource>(resourceId),
                Value = N(value)
            };
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
