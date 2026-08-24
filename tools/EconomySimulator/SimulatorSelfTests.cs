using System;
using System.Collections.Generic;
using System.Linq;

namespace Kingdom.EconomySimulation;

public static class SimulatorSelfTests
{
    public static void Run(EconomySnapshot snapshot)
    {
        Require(snapshot.Resources.Count > 0, "没有加载资源定义。");
        Require(snapshot.Buildings.Count > 0, "没有加载建筑定义。");
        Require(snapshot.Research.Count > 0, "没有加载研究定义。");
        Require(snapshot.Workshops.Count > 0, "没有加载工坊升级定义。");

        VerifyDefinitionReferenceKinds(snapshot);
        VerifyPopulationProductivityParity();
        VerifyFoodShortagePopulationAndProductivityDeficit();
        VerifyGlobalFoodMultiplierUsesMultiplication();
        VerifyResourceRatesUseSeconds();
        VerifyAdaptiveStepsPreserveRates();
        VerifyAdaptiveStepSchedule();
        VerifyDecisionIntervalsUseElapsedSeconds();
        VerifyLongObservationHorizon();
        VerifyDoubleOverflowDoesNotBecomeNaN();
        VerifyActiveBuildingCacheParity(snapshot);
        VerifyUtilityBuildingCandidates();
        VerifyUpgradePairs(snapshot);
        VerifyCokeOvenCandidate(snapshot);
        VerifyIndustrialSourceEntryPoints(snapshot);
        VerifyIndustrialConstructionGateCosts(snapshot);
        VerifyIndustrializationPacingGate(snapshot);
        VerifyIndustrialMetalSinks(snapshot);
        VerifyLegacyMetalSurplusDiagnostic();
        VerifyConstructionSinkSuppressesExplosionDiagnostic();
        VerifySpacerAdvancedMaterialGateCosts(snapshot);
        VerifyExtremeConsumptionEfficiency();
        VerifyConstructionWaitUsesResourceUnits();
        VerifyConstructionMultiplierParity();

        Definition unlock = snapshot.Find("IndustrialWorkshop", DefinitionKind.Research);
        Require(unlock.Effects.Any(x =>
                x.Kind == SimEffectKind.UnlockIndustrialWorkshop),
            "工业工坊研究没有解锁工坊系统。");

        Definition boilers = snapshot.Find("ReinforcedBoilers", DefinitionKind.Workshop);
        Require(boilers.Kind == DefinitionKind.Workshop,
            "强化锅炉没有被识别为工坊定义。");
        Require(boilers.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingPowerProductionMultiplier &&
                x.Target == "SteamPlant"),
            "强化锅炉的工坊效果映射错误：" +
            string.Join(";", boilers.Effects.Select(x =>
                $"{x.Kind}:{x.Target}:{x.Value}")));

        Require(snapshot.Find("AluminumBusbars", DefinitionKind.Workshop).Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingPowerProductionMultiplier &&
                x.Target == "CentralPowerStation"),
            "铝母线必须提升中央电站发电产出。");
        Require(snapshot.Find("ElectricalInstrumentation", DefinitionKind.Workshop).Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingPowerProductionMultiplier &&
                x.Target == "CentralPowerStation") &&
                snapshot.Find("ElectricalInstrumentation", DefinitionKind.Workshop).Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingResearchPowerMultiplier &&
                x.Target == "University"),
            "电气仪表必须分别提升中央电站发电和大学研究力。");
        Require(snapshot.Find("HighPressureTurbines", DefinitionKind.Workshop).Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingPowerProductionMultiplier &&
                x.Target == "CentralPowerStation"),
            "高压涡轮必须提升中央电站发电产出。");
        Require(snapshot.Find("AluminumElectrolyticCells", DefinitionKind.Workshop)
                    .ResourceRequirements.ContainsKey("BauxiteOre") &&
                snapshot.Find("NickelLeachingElectrowinningSystem", DefinitionKind.Workshop)
                    .ResourceRequirements.ContainsKey("NickelConcentrate") &&
                snapshot.Find("TitaniumReductionRetorts", DefinitionKind.Workshop)
                    .ResourceRequirements.ContainsKey("TitaniumConcentrate"),
            "铝土矿、镍精矿和钛精矿必须进入对应的冶金实物工坊。");
        Require(snapshot.Find("BlockSignalling", DefinitionKind.Workshop).Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingLogisticsProductionMultiplier &&
                x.Target == "RailHub"),
            "区间信号必须提升铁路枢纽物流产出。");
        Definition agriculturalMachinery =
            snapshot.Find("AgriculturalMachinery", DefinitionKind.Workshop);
        Require(agriculturalMachinery.Effects.Count(x =>
                    x.Kind == SimEffectKind.GlobalFoodProductionMultiplier &&
                    x.Value >= 1.69d) == 1 &&
                agriculturalMachinery.Effects.Count(x =>
                    x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                    x.Target == "PlantingField") == 1,
            "农业机械必须提升农场、牧场、灌溉设施的食物产出，并支持纤维采集工业化。");

        VerifyWorkshopTargetCapabilities(snapshot);
        VerifyResearchTargetCapabilities(snapshot);

        Definition integratedFurnaces =
            snapshot.Find("IntegratedFurnaces", DefinitionKind.Workshop);
        Require(integratedFurnaces.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "IndustrialMetalSmelter" &&
                x.Value >= 1.25d),
            "一体化冶炼炉必须提升多金属冶炼炉产出。");

        Definition metalSmelter =
            snapshot.Find("IndustrialMetalSmelter", DefinitionKind.Building);
        Require(metalSmelter.Generation.TryGetValue("Copper", out double copperRate) &&
                copperRate > 0d &&
                metalSmelter.Generation.TryGetValue("Tin", out double tinRate) &&
                tinRate > 0d,
            "工业综合冶炼炉必须同时产出铜和锡。");
        Require(metalSmelter.Consumption.ContainsKey("CopperOre") &&
                metalSmelter.Consumption.ContainsKey("TinOre"),
            "工业综合冶炼炉必须同时消耗铜矿和锡矿。");
        Require(metalSmelter.Generation.TryGetValue("Bronze", out double industrialBronzeRate) &&
                Math.Abs(industrialBronzeRate - 0.4d) < 1e-9d,
            "IndustrialMetalSmelter Bronze output must remain 0.4/s, matching the Unity definition test.");
        Definition earlyMetalSmelter =
            snapshot.Find("MetalSmelter", DefinitionKind.Building);
        Require(!snapshot.All.Any(x => x.Id == "BronzeFoundry") &&
                earlyMetalSmelter.Generation.TryGetValue("Bronze", out double bronzeRate) &&
                bronzeRate > 0d,
            "早期多金属冶炼炉必须直接生产青铜，旧青铜铸造炉不得残留。");
        Definition metalResearch =
            snapshot.Find("IndustrialMetalSmelting", DefinitionKind.Research);
        Require(metalResearch.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "IndustrialMetalSmelter"),
            "工业有色金属冶炼研究必须作用于统一冶炼建筑。");
        foreach (string id in new[]
        {
            "MetalSmelter", "SteelForge", "IndustrialMetalSmelter",
            "AluminumSmelter", "NickelRefinery", "TitaniumMetallurgicalComplex"
        })
        {
            Definition smeltingBuilding = snapshot.Find(id, DefinitionKind.Building);
            double inputMass = smeltingBuilding.Consumption.Values.Sum();
            double outputMass = smeltingBuilding.Generation.Values.Sum();
            Require(outputMass <= inputMass + 1e-9d,
                $"{id} output mass must not exceed input mass.");
        }
        Require(!snapshot.All.Any(x => x.Id == "ModernSteelmaking"),
            "重复的现代炼钢研究不得残留。");

        Definition phaseArray =
            snapshot.Find("PhaseMaterialSynthesisArray", DefinitionKind.Building);
        Require(phaseArray.Generation.TryGetValue("PhaseMaterial", out double phaseRate) &&
                phaseRate > 0d &&
                phaseArray.Consumption.ContainsKey("PhantomAlloy") &&
                phaseArray.Consumption.ContainsKey("PhantomWeave") &&
                phaseArray.Consumption.ContainsKey("TitaniumAlloy"),
            "相位材料合成阵列必须用高阶材料生产相位材料。");
        Require(snapshot.Find("DeepSpaceRelay", DefinitionKind.Building)
                    .Consumption.ContainsKey("PhaseMaterial") &&
                snapshot.Find("QuantumComputingArray", DefinitionKind.Building)
                    .Consumption.ContainsKey("PhaseMaterial") &&
                snapshot.Find("PhaseFieldNavigation", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("PhaseMaterial"),
            "相位材料必须同时支撑深空设施、量子计算和相位航行研究。");
        Require(snapshot.Find("InterstellarOccupationAdministration", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("TitaniumAlloy") &&
                snapshot.Find("InterstellarOccupationAdministration", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("PhaseMaterial"),
            "星际占领行政研究必须消耗钛合金和相位材料。");

        Definition interstellarNavigation =
            snapshot.Find("InterstellarNavigation", DefinitionKind.Research);
        Require(interstellarNavigation.AdvancesTechLevel &&
                interstellarNavigation.TechLevel == SimTechLevel.Spacer &&
                interstellarNavigation.ResourceRequirements.TryGetValue("RocketFuel", out double navigationFuel) &&
                Math.Abs(navigationFuel - 1250d) < 1e-9d &&
                snapshot.Find("ChemicalPlant", DefinitionKind.Building)
                    .Generation.TryGetValue("RocketFuel", out double rocketFuelRate) &&
                rocketFuelRate > 0d,
            "InterstellarNavigation must use RocketFuel with an Industrial-era ChemicalPlant source.");
        Require(snapshot.Find("IndustrialStoneworks", DefinitionKind.Building)
                    .ResourceRequirements.TryGetValue("StoneBrick", out double stoneworksBrickCost) &&
                Math.Abs(stoneworksBrickCost - 3000d) < 1e-9d &&
                !snapshot.Find("IndustrialStoneworks", DefinitionKind.Building)
                    .ResourceRequirements.ContainsKey("StoneChunk"),
            "IndustrialStoneworks must use a single reachable StoneBrick cost and not self-require StoneChunk.");

        Definition[] spaceBuildings = snapshot.Buildings
            .Where(x => x.TechLevel == SimTechLevel.Spacer)
            .ToArray();
        string[] advancedSpaceResources =
            { "TitaniumAlloy", "Composite", "PhantomAlloy", "PhantomWeave", "PhaseMaterial" };
        Require(spaceBuildings.Length > 0 &&
                spaceBuildings.All(x => advancedSpaceResources.Any(resource =>
                    x.ResourceRequirements.ContainsKey(resource))) &&
                spaceBuildings.All(x => advancedSpaceResources.Any(resource =>
                    x.Consumption.ContainsKey(resource))) &&
                advancedSpaceResources.All(resource =>
                    spaceBuildings.Count(x => x.Consumption.ContainsKey(resource)) >= 2),
            "太空建筑建造和维护都必须使用高级材料，并为每种高级材料保留多个建筑维护去向。" );

        Definition deepOilDrilling =
            snapshot.Find("DeepOilDrilling", DefinitionKind.Research);
        Require(deepOilDrilling.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "OilDerrick" &&
                x.Value >= 1.25d),
            "深层石油钻探研究必须提升石油井产量。");
        Definition rotaryDrillingHeads =
            snapshot.Find("RotaryDrillingHeads", DefinitionKind.Workshop);
        Require(rotaryDrillingHeads.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "OilDerrick" &&
                x.Value >= 1.20d),
            "旋转钻头组必须提升石油井产量。");

        Definition chemicalPlant =
            snapshot.Find("ChemicalPlant", DefinitionKind.Building);
        Require(chemicalPlant.Generation.TryGetValue("Explosives", out double explosiveRate) &&
                explosiveRate > 0d,
            "化工厂必须生产工业炸药。");
        Definition oilDerrick =
            snapshot.Find("OilDerrick", DefinitionKind.Building);
        Require(oilDerrick.Consumption.ContainsKey("Explosives"),
            "石油井必须持续消耗工业炸药进行深层钻井爆破。");
        Definition oilRefinery =
            snapshot.Find("OilRefinery", DefinitionKind.Building);
        Require(oilRefinery.Consumption.ContainsKey("CrudeOil"),
            "炼油厂必须消耗原油，才能保持石油加工链的输入闭合。");
        Require(!oilRefinery.Consumption.ContainsKey("Explosives"),
            "炼油厂不得消耗工业炸药，否则会与化工厂形成生产循环。");
        foreach (string consumerId in new[] { "RareMetalMine", "OilDerrick" })
        {
            Definition consumer = snapshot.Find(consumerId, DefinitionKind.Building);
            Require(consumer.Consumption.ContainsKey("Explosives"),
                $"{consumerId}必须消耗工业炸药。");
        }

        foreach (Definition building in snapshot.Buildings)
            Require(!building.ResourceRequirements.ContainsKey("Explosives"),
                $"{building.Id}不得把工业炸药作为建筑建造材料。");

        Definition explosivesResearch =
            snapshot.Find("IndustrialExplosives", DefinitionKind.Research);
        Require(explosivesResearch.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "ChemicalPlant" &&
                x.Value >= 1.25d),
            "工业炸药工艺研究必须提升化工厂产量。");
        Definition controlledBlasting =
            snapshot.Find("ControlledBlasting", DefinitionKind.Workshop);
        foreach (string target in new[] { "RareMetalMine", "OilDerrick" })
            Require(controlledBlasting.Effects.Any(x =>
                    x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                    x.Target == target &&
                    x.Value >= 1.15d),
                $"精确爆破工艺必须提升{target}产量。");

        Definition poweredMining =
            snapshot.Find("PoweredMining", DefinitionKind.Workshop);
        Require(poweredMining.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "RareMetalMine" &&
                x.Value >= 1.2d),
            "动力采矿必须提升统一工业矿场产量。");

        Definition supplyDoctrine =
            snapshot.Find("InterstellarSupplyDoctrine", DefinitionKind.Workshop);
        Require(supplyDoctrine.Effects.Any(x =>
                x.Kind == SimEffectKind.CampaignSupplyCostMultiplier &&
                x.Value <= 0.9d),
            "星际补给标准化模块必须降低星际战役持续补给成本，而不是重复提供后勤枢纽倍率：" +
            string.Join(";", supplyDoctrine.Effects.Select(x =>
                $"{x.Kind}:{x.Target}:{x.Value}")));

        Definition networkAutomation =
            snapshot.Find("DeepSpaceNetworkAutomation", DefinitionKind.Workshop);
        Require(networkAutomation.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingLogisticsProductionMultiplier &&
                x.Target == "DeepSpaceRelay" &&
                x.Value >= 1.2d) &&
                !networkAutomation.Effects.Any(x =>
                    x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                    x.Target == "DeepSpaceRelay"),
            "深空网络自动化必须只提升深空中继站的物流产出。");

        Definition launchStages =
            snapshot.Find("ReusableLaunchStages", DefinitionKind.Workshop);
        Require(launchStages.Effects.Any(x =>
                x.Kind == SimEffectKind.FleetRepairCostMultiplier &&
                x.Target == "" &&
                x.Value <= .85d),
            "可复用发射级必须降低远征舰队的维修消耗。");

        Definition combatLogistics =
            snapshot.Find("InterstellarCombatLogistics", DefinitionKind.Research);
        Require(combatLogistics.Effects.Any(x =>
                x.Kind == SimEffectKind.FleetRepairCostMultiplier &&
                x.Target == "" &&
                x.Value <= .85d),
            "星际战斗后勤研究必须提供维修资源调度效率。" );

        Definition damageControlTheory =
            snapshot.Find("FleetDamageControlTheory", DefinitionKind.Research);
        Require(damageControlTheory.Effects.Any(x =>
                x.Kind == SimEffectKind.CampaignCasualtyMultiplier &&
                x.Value <= .9d) &&
                damageControlTheory.ResourceRequirements.ContainsKey("TitaniumAlloy") &&
                damageControlTheory.ResourceRequirements.ContainsKey("PhaseMaterial"),
            "舰队损伤控制理论必须降低战损并使用高级材料。" );

        Definition adaptiveArmor =
            snapshot.Find("AdaptiveArmorRepairSystems", DefinitionKind.Workshop);
        Require(adaptiveArmor.Effects.Any(x =>
                x.Kind == SimEffectKind.CampaignCasualtyMultiplier &&
                x.Value <= .9d) &&
                adaptiveArmor.ResourceRequirements.ContainsKey("PhantomWeave") &&
                adaptiveArmor.ResourceRequirements.ContainsKey("PhaseMaterial"),
            "自适应装甲维修系统必须把高级材料转化为战损控制能力。" );

        Definition habitatSystems =
            snapshot.Find("ModularHabitatSystems", DefinitionKind.Workshop);
        Require(habitatSystems.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingLogisticsProductionMultiplier &&
                x.Target == "OrbitalStation" &&
                x.Value >= 1.25d),
            "模块化空间站必须提升轨道空间站的物流产出。");

        Definition powerBeaming =
            snapshot.Find("OrbitalPowerBeaming", DefinitionKind.Workshop);
        Require(powerBeaming.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingPowerProductionMultiplier &&
                x.Target == "OrbitalSolarArray" &&
                x.Value >= 1.25d),
            "轨道能量束必须提供真实的全局电力效率增益。");

        Require(powerBeaming.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingPowerProductionMultiplier &&
                x.Target == "OrbitalSolarArray" &&
                x.Value >= 1.25d),
            "轨道能量束必须提升轨道太阳能阵列的电力产出。");

        Definition shipyardAssembly =
            snapshot.Find("AutomatedShipyardAssembly", DefinitionKind.Workshop);
        Require(shipyardAssembly.Effects.Any(x =>
                x.Kind == SimEffectKind.MilitaryMultiplier &&
                x.Target == "" &&
                x.Value > 1d),
            "自动化船坞装配必须提供真实的星际军事增益。");

        Require(shipyardAssembly.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingConstructionMultiplier &&
                x.Target == "Shipyard" &&
                x.Value >= 1.15d),
            "自动化船坞装配必须降低船坞的施工材料成本。");

        Definition phaseContainment =
            snapshot.Find("PhaseFieldContainment", DefinitionKind.Workshop);
        Require(phaseContainment.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "PhantomMaterialsFabricator" &&
                x.Value >= 1.15d),
            "相位场约束必须提升幽影材料制造厂的连续产出。" );

        Definition interstellarSupply =
            snapshot.Find("InterstellarCombatSupplySystems", DefinitionKind.Workshop);
        Require(interstellarSupply.Effects.Any(x =>
                x.Kind == SimEffectKind.MilitaryMultiplier && x.Value >= 1.15d) &&
                interstellarSupply.Effects.Any(x =>
                    x.Kind == SimEffectKind.BuildingLogisticsProductionMultiplier &&
                    x.Target == "OrbitalStation" && x.Value >= 1.25d) &&
                interstellarSupply.Effects.Any(x =>
                    x.Kind == SimEffectKind.FleetRepairCostMultiplier && x.Value <= .8d) &&
                interstellarSupply.ResourceRequirements.ContainsKey("TitaniumAlloy") &&
                interstellarSupply.ResourceRequirements.ContainsKey("PhantomWeave") &&
                interstellarSupply.ResourceRequirements.ContainsKey("PhaseMaterial"),
            "星际战斗补给系统必须同时使用高级材料并专精军事、轨道后勤枢纽和维修支援。");

        Definition occupationAdministration =
            snapshot.Find("InterstellarOccupationAdministration", DefinitionKind.Research);
        Require(occupationAdministration.Effects.Any(x =>
                x.Kind == SimEffectKind.OccupiedResourceProductionMultiplier &&
                x.Value >= 1.15d),
            "星际占领行政研究必须提高已占领星区的持续产出。" );

        Definition logisticsDoctrine =
            snapshot.Find("InterstellarLogisticsDoctrine", DefinitionKind.Research);
        Require(logisticsDoctrine.Effects.Any(x =>
                x.Kind == SimEffectKind.CampaignProgressMultiplier &&
                x.Value >= 1.15d),
            "星际后勤学说必须提供战役推进理论。" );

        Definition supplyChainTheory =
            snapshot.Find("InterstellarSupplyChainTheory", DefinitionKind.Research);
        Require(supplyChainTheory.Effects.Any(x =>
                x.Kind == SimEffectKind.CampaignSupplyCostMultiplier &&
                x.Value <= .9d) &&
                supplyChainTheory.ResourceRequirements.ContainsKey("TitaniumAlloy") &&
                supplyChainTheory.ResourceRequirements.ContainsKey("PhaseMaterial"),
            "星际补给链理论必须降低远征持续补给消耗并使用高级材料。" );

        Definition resupplyModules =
            snapshot.Find("AutomatedFleetResupplyModules", DefinitionKind.Workshop);
        Require(resupplyModules.Effects.Any(x =>
                x.Kind == SimEffectKind.CampaignSupplyCostMultiplier &&
                x.Value <= .9d) &&
                resupplyModules.ResourceRequirements.ContainsKey("PhantomWeave") &&
                resupplyModules.ResourceRequirements.ContainsKey("PhaseMaterial"),
            "自动化舰队补给模块必须把高级材料转化为持续补给节约。" );

        Definition occupationGovernance =
            snapshot.Find("InterstellarOccupationGovernance", DefinitionKind.Workshop);
        Require(occupationGovernance.Effects.Any(x =>
                x.Kind == SimEffectKind.TerritoryGranted && x.Value >= 30000d) &&
                occupationGovernance.Effects.Any(x =>
                    x.Kind == SimEffectKind.OccupiedResourceProductionMultiplier &&
                    x.Value >= 1.2d) &&
                occupationGovernance.ResourceRequirements.ContainsKey("TitaniumAlloy") &&
                occupationGovernance.ResourceRequirements.ContainsKey("PhaseMaterial"),
            "星际占领治理工程必须把高级材料转化为长期领土收益。");

        Definition orbitalStation =
            snapshot.Find("OrbitalStation", DefinitionKind.Building);
        Require(orbitalStation.SpaceCost >= 420d &&
                orbitalStation.ProductivityConsumption >= 600d &&
                orbitalStation.ProductivityGranted == 0d &&
                orbitalStation.LogisticsProduction >= 80d &&
                orbitalStation.PowerConsumption >= 80d &&
                orbitalStation.LogisticsConsumption >= 18d &&
                orbitalStation.Consumption.TryGetValue("TitaniumAlloy", out double stationTitaniumMaintenance) &&
                stationTitaniumMaintenance >= .04d,
            "轨道空间站必须承担与其后勤和舰队能力匹配的空间及生产力成本。");

        Definition orbitalSolarArray =
            snapshot.Find("OrbitalSolarArray", DefinitionKind.Building);
        Require(orbitalSolarArray.SpaceCost >= 420d &&
                orbitalSolarArray.ProductivityConsumption >= 500d &&
                orbitalSolarArray.PowerProduction >= 360d &&
                orbitalSolarArray.LogisticsConsumption >= 8d &&
                orbitalSolarArray.Consumption.TryGetValue(
                    "TitaniumAlloy", out double solarTitaniumMaintenance) &&
                solarTitaniumMaintenance >= .04d,
            "轨道太阳能阵列必须持续消耗钛合金维护大型轨道结构。");

        Require(Math.Abs(EconomySimulationParity.AdvanceStockpile(
            3d, 2d, 5d, .5d) - 1.5d) < 1e-9d,
            "库存同步公式校验失败。");
        Require(Math.Abs(EconomySimulationParity.CalculateSatisfaction(
            2d, 3d, 10d, .5d) - .7d) < 1e-9d,
            "满意度同步公式校验失败。");
        Require(Math.Abs(EconomySimulationParity.ResearchSpeedEffect(0, 2) - .4d)
            < 1e-9d, "研究速度同步公式校验失败。");
        double lowHappiness = ResourceSimulator.CalculateHappinessMultiplier(-10d, 10d, 0.5d);
        double highHappiness = ResourceSimulator.CalculateHappinessMultiplier(1000000d, 10d, 1d);
        double deficitHappiness = ResourceSimulator.CalculateHappinessMultiplier(-100d, 10d, 1d);
        Require(Math.Abs(deficitHappiness - (1d / 11d)) < 1e-9d,
            "负 Food 净产出在库存尚可时仍必须降低幸福度。");
        Require(Math.Abs(lowHappiness - 0.5d) < 1e-9d,
            "幸福度必须负责食物短缺惩罚。");
        Require(highHappiness > lowHappiness && highHappiness < 1.5d,
            "幸福度曲线必须单调递增且有界。");

        VerifyAtomicResearchPayment();
        VerifyWorkshopPurchaseAndEffect();

        foreach (Route route in Enum.GetValues<Route>())
        {
            ISimulationStrategy strategy = SimulationStrategies.Create(route);
            Require(strategy.Route == route, $"模拟路线不匹配：{route}。");
            Require(strategy.ResearchDecisionInterval > 0 &&
                strategy.BuildingDecisionInterval > 0 &&
                strategy.WorkshopDecisionInterval > 0,
                $"模拟路线的决策间隔无效：{route}。");
        }
    }

    private static void VerifyResourceRatesUseSeconds()
    {
        Definition producer = new()
        {
            Id = "RateContractProducer",
            Kind = DefinitionKind.Building,
            FoodConsumption = 5d
        };
        producer.Generation["RateContractResource"] = 2d;

        SimulationState state = new();
        state.Buildings[producer.Id] = 1;
        ResourceSimulator.Tick(state, new[] { producer }, new[] { producer }, 1d);
        Require(Math.Abs(state.Resources.GetValueOrDefault("RateContractResource") - 2d) < 1e-9,
            "资源生产率必须按每秒结算，不能按每分钟结算。");
    }

    private static void VerifyAdaptiveStepSchedule()
    {
        Require(Math.Abs(EconomySimulator.StepSeconds(SimTechLevel.Animal)-1d)<1e-9d,
            "低阶模拟步长必须保持一秒。 ");
        Require(Math.Abs(EconomySimulator.StepSeconds(SimTechLevel.Medieval)-10d)<1e-9d,
            "中世纪长等待段应使用十秒模拟步长。 ");
        Require(Math.Abs(EconomySimulator.StepSeconds(SimTechLevel.Industrial)-600d)<1e-9d,
            "工业时代应使用六百秒模拟步长。 ");
        Require(Math.Abs(EconomySimulator.StepSeconds(SimTechLevel.Spacer)-1800d)<1e-9d,
            "太空时代应使用一千八百秒模拟步长。 ");
        Require(Math.Abs(EconomySimulator.StepSeconds(SimTechLevel.Ultra)-1800d)<1e-9d,
            "Ultra时代应使用一千八百秒模拟步长。 ");
        Require(Math.Abs(EconomySimulator.StepSeconds(SimTechLevel.Archotech)-1800d)<1e-9d,
            "Archotech时代应使用一千八百秒模拟步长。 ");
    }

    private static void VerifyAdaptiveStepsPreserveRates()
    {
        foreach (double seconds in new[]
                 {
                     EconomySimulator.IndustrialStepSeconds,
                     EconomySimulator.SpacerStepSeconds,
                     EconomySimulator.UltraStepSeconds,
                     EconomySimulator.ArchotechStepSeconds
                 })
        {
            Definition producer = new()
            {
                Id = $"AdaptiveRateProducer{seconds:0}",
                Kind = DefinitionKind.Building,
                FoodConsumption = 5d
            };
            producer.Generation["WoodLog"] = 2d;
            SimulationState state = new();
            state.Buildings[producer.Id] = 1;
            ResourceSimulator.Tick(state, new[] { producer },
                new[] { producer }, seconds);
            double expected = 3d * seconds;
            double actual = state.Resources.GetValueOrDefault("WoodLog");
            Require(Math.Abs(actual - expected) < 1e-9,
                $"高阶步长 {seconds:0} 秒破坏了资源每秒结算契约：实际 {actual:0.###}，预期 {expected:0.###}。");
        }
    }

    private static void VerifyDecisionIntervalsUseElapsedSeconds()
    {
        SimulationState state = new();
        double lastDecision = double.NegativeInfinity;
        Require(state.ShouldDecide(ref lastDecision, 45),
            "第一次模拟决策必须立即执行。");
        state.Seconds = 60d;
        Require(state.ShouldDecide(ref lastDecision, 45),
            "跨过决策间隔的长步长必须执行一次决策。");
        state.Seconds = 100d;
        Require(!state.ShouldDecide(ref lastDecision, 45),
            "未达到下一次决策间隔时不应重复决策。");
        state.Seconds = 105d;
        Require(state.ShouldDecide(ref lastDecision, 45),
            "经过完整决策间隔后必须再次决策。");
    }

    private static void VerifyLongObservationHorizon()
    {
        Require(EconomySimulator.DefaultHorizonDays >= 30,
            "高阶经济模拟的上线观察窗口必须至少覆盖三十天。 ");
        Require(Math.Abs(EconomySimulator.DefaultHorizonSeconds -
                EconomySimulator.DefaultHorizonDays * 24d * 60d * 60d) < 1e-9d,
            "上线观察窗口的天数与秒数必须保持一致。 ");
    }

    private static void VerifyDoubleOverflowDoesNotBecomeNaN()
    {
        var resources = new Dictionary<string, double>();
        ResourceSimulator.Add(resources, "OverflowProbe", double.MaxValue);
        ResourceSimulator.Add(resources, "OverflowProbe", double.MaxValue);
        Require(resources["OverflowProbe"] == double.MaxValue,
            "模拟器资源溢出必须饱和，不能产生 Infinity。 ");
        ResourceSimulator.Add(resources, "OverflowProbe", -1d);
        Require(double.IsFinite(resources["OverflowProbe"]) &&
                resources["OverflowProbe"] == double.MaxValue,
            "饱和资源继续扣除时不能传播 NaN。 ");
    }

    private static void VerifyActiveBuildingCacheParity(EconomySnapshot snapshot)
    {
        var state = new SimulationState();
        Definition? first = snapshot.Buildings.FirstOrDefault(x =>
            x.Generation.Count > 0 || x.FoodProduction > 0d);
        Require(first != null, "必须存在可用于活跃建筑缓存测试的生产建筑。 ");
        state.Buildings[first!.Id] = 1;
        var active = snapshot.Buildings.Where(x =>
            state.Buildings.GetValueOrDefault(x.Id) > 0).ToArray();
        ResourceSimulator.Tick(state, active, snapshot.All, 60d);
        Require(state.Resources.Values.All(double.IsFinite),
            "活跃建筑缓存结算不得产生非有限资源值。 ");
    }

    private static void VerifyUpgradePairs(EconomySnapshot snapshot)
    {
        SimulationState state = new();
        var byId = snapshot.Buildings.ToDictionary(x => x.Id,
            StringComparer.OrdinalIgnoreCase);
        foreach (Definition source in snapshot.Buildings.Where(x =>
                     !string.IsNullOrEmpty(x.UpgradeTo)))
        {
            Require(byId.ContainsKey(source.UpgradeTo),
                $"建筑升级目标不存在：{source.Id}->{source.UpgradeTo}。 ");
            state.UpgradePairs.Add((source, byId[source.UpgradeTo]));
        }
        Require(state.UpgradePairs.Count > 0 &&
                state.UpgradePairs.All(x =>
                    string.Equals(x.Source.UpgradeTo, x.Target.Id,
                        StringComparison.OrdinalIgnoreCase)),
            "建筑升级边缓存必须保留所有有效升级目标。 ");
    }

    private static void VerifyUtilityBuildingCandidates()
    {
        Definition powerPlant = new()
        {
            Id = "UtilityPowerPlantProbe",
            Kind = DefinitionKind.Building,
            TechLevel = SimTechLevel.Industrial,
            PowerProduction = 10d
        };
        EconomySnapshot snapshot = new(new[] { powerPlant });
        SimulationState state = new() { TechLevel = SimTechLevel.Industrial };

        BuildingSimulator.Decide(
            state,
            snapshot,
            SimulationStrategies.Create(Route.Normal));

        Require(state.Buildings.GetValueOrDefault(powerPlant.Id) == 1,
            "仅提供电力的建筑必须进入建筑候选集，不能因没有普通资源产出而被过滤。");
    }

    private static void VerifyCokeOvenCandidate(EconomySnapshot snapshot)
    {
        Definition cokeOven = snapshot.Find("CokeOven", DefinitionKind.Building);
        Require(cokeOven.RequiredResearch.Count == 2 &&
                cokeOven.RequiredResearch.All(x =>
                    x is "SteamPower" or "Coking"),
            "焦炉只能依赖蒸汽动力和炼焦研究，不得等待电网研究后才可建造。");
        Require(cokeOven.ProductivityConsumption <= 20d,
            "CokeOven entry productivity must remain affordable for Industrial source establishment.");
        Require(snapshot.Find("OilDerrick", DefinitionKind.Building)
                    .RequiredResearch.Contains("IndustrialChemistry"),
            "石油钻井必须在工业化学之后开放，避免在焦炭生产前吞噬工业生产力。");
        Require(!snapshot.Find("AluminumMetallurgy", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("BauxiteOre"),
            "铝工业研究不得先消耗由同一研究链解锁的铝土矿，否则会形成原料自锁。");
        Require(snapshot.Find("TitaniumAlloyEngineering", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("NickelConcentrate") &&
                !snapshot.Find("TitaniumAlloyEngineering", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("Nickel"),
            "钛合金工程必须使用镍精矿，不能跳过镍精炼链消耗已经枯竭的原始镍。");
        Require(snapshot.Find("PhantomMaterials", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("NickelConcentrate") &&
                !snapshot.Find("PhantomMaterials", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("Nickel"),
            "幽影材料工程必须使用镍精矿，不能在 Spacer 阶段重新跳回原始镍。");
        Definition phantomFabricator = snapshot.Find("PhantomMaterialsFabricator", DefinitionKind.Building);
        Require(phantomFabricator.Generation.ContainsKey("PhantomAlloy") &&
                phantomFabricator.Generation.ContainsKey("PhantomWeave") &&
                phantomFabricator.ResourceRequirements.ContainsKey("Nickel") &&
                !phantomFabricator.ResourceRequirements.ContainsKey("NickelConcentrate") &&
                phantomFabricator.Consumption.ContainsKey("Nickel") &&
                !phantomFabricator.Consumption.ContainsKey("NickelConcentrate"),
            "幽影材料制造厂必须作为可进入的幽影材料来源，并使用原始镍而非镍精矿。");
        Require(snapshot.Find("PhaseMaterialEngineering", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("Nickel") &&
                !snapshot.Find("PhaseMaterialEngineering", DefinitionKind.Research)
                    .ResourceRequirements.ContainsKey("NickelConcentrate"),
            "相位材料工程必须使用原始镍而非镍精矿。");
        Definition phaseArray = snapshot.Find("PhaseMaterialSynthesisArray", DefinitionKind.Building);
        Require(phaseArray.Generation.ContainsKey("PhaseMaterial") &&
                phaseArray.ResourceRequirements.ContainsKey("Nickel") &&
                !phaseArray.ResourceRequirements.ContainsKey("NickelConcentrate") &&
                phaseArray.Consumption.ContainsKey("Nickel") &&
                !phaseArray.Consumption.ContainsKey("NickelConcentrate"),
            "相位材料合成阵列必须作为可进入的相位材料来源，并使用原始镍而非镍精矿。");
        Require(phaseArray.ResourceRequirements.GetValueOrDefault("PhantomWeave") <= 240d,
            "PhaseMaterialSynthesisArray first-copy PhantomWeave gate must remain below the demonstrated multi-hour stall.");
        Require(phaseArray.ResourceRequirements.GetValueOrDefault("PhantomAlloy") <= 240d,
            "PhaseMaterialSynthesisArray first-copy PhantomAlloy gate must remain below the demonstrated multi-hour stall.");
        SimulationState state = new()
        {
            TechLevel = SimTechLevel.Industrial,
            Population = 100d
        };
        foreach (string research in new[] { "SteamPower", "Coking", "Industrialization" })
            state.CompletedResearch.Add(research);
        state.PurchasedWorkshop.Add("CokeOvenOptimization");
        foreach ((string resource, double amount) in cokeOven.ResourceRequirements)
            state.Resources[resource] = amount;

        BuildingSimulator.Decide(state, snapshot, SimulationStrategies.Create(Route.Normal));

        Require(state.Buildings.GetValueOrDefault(cokeOven.Id) == 1,
            "焦炉在工业时代前置研究完成且建造资源充足时必须进入可建候选，否则焦炭链会被模拟器错误阻断。");
    }

    private static void VerifyIndustrialSourceEntryPoints(EconomySnapshot snapshot)
    {
        Definition rareMine = snapshot.Find("RareMetalMine", DefinitionKind.Building);
        Require(rareMine.Generation.ContainsKey("BauxiteOre") &&
                rareMine.Generation.ContainsKey("NickelConcentrate") &&
                rareMine.Generation.ContainsKey("TitaniumConcentrate") &&
                rareMine.ProductivityConsumption <= 20d,
            "工业多金属矿场必须作为可进入的铝土、镍精矿和钛精矿来源，首座矿场不能被过高生产力门槛锁死。");
        Definition aluminumSmelter = snapshot.Find("AluminumSmelter", DefinitionKind.Building);
        Require(aluminumSmelter.Generation.ContainsKey("Aluminum") &&
                aluminumSmelter.ProductivityConsumption <= 20d,
            "铝冶炼厂必须作为可进入的铝来源，首座冶炼厂不能被过高生产力门槛锁死。");
        Definition titaniumComplex = snapshot.Find("TitaniumMetallurgicalComplex", DefinitionKind.Building);
        Require(titaniumComplex.Generation.ContainsKey("TitaniumAlloy") &&
                titaniumComplex.ProductivityConsumption <= 20d &&
                !titaniumComplex.RequiredResearch.Contains("InterstellarNavigation") &&
                titaniumComplex.ResourceRequirements.ContainsKey("NickelConcentrate") &&
                !titaniumComplex.ResourceRequirements.ContainsKey("Nickel") &&
                titaniumComplex.Consumption.ContainsKey("NickelConcentrate") &&
                !titaniumComplex.Consumption.ContainsKey("Nickel"),
            "钛冶金联合厂必须作为工业时代可进入的钛合金来源，不能依赖由钛合金解锁的星际导航。");
    }

    private static void VerifySpacerAdvancedMaterialGateCosts(EconomySnapshot snapshot)
    {
        Definition phantomFabricator =
            snapshot.Find("PhantomMaterialsFabricator", DefinitionKind.Building);
        Require(phantomFabricator.Generation.TryGetValue("PhantomAlloy", out double phantomAlloyRate) &&
                Math.Abs(phantomAlloyRate - 0.06d) < 1e-9d &&
                phantomFabricator.Generation.TryGetValue("PhantomWeave", out double phantomWeaveRate) &&
                Math.Abs(phantomWeaveRate - 0.04d) < 1e-9d,
            "Phantom materials source rates must remain PhantomAlloy 0.06/s and PhantomWeave 0.04/s.");

        foreach ((string id, string resource) in new[]
                 {
                     ("OrbitalCarbonizationComplex", "PhantomAlloy"),
                     ("OrbitalResourceExtractionArray", "PhantomWeave"),
                     ("OrbitalSolarArray", "PhantomWeave"),
                     ("OrbitalStation", "PhantomWeave"),
                     ("PhaseMaterialSynthesisArray", "PhantomAlloy"),
                     ("PhaseMaterialSynthesisArray", "PhantomWeave")
                 })
        {
            Definition building = snapshot.Find(id, DefinitionKind.Building);
            Require(building.ResourceRequirements.GetValueOrDefault(resource) <= 240d,
                $"{id} {resource} first-copy gate must remain within the demonstrated source-entry envelope.");
        }
    }

    private static void VerifyIndustrializationPacingGate(EconomySnapshot snapshot)
    {
        Definition industrialization =
            snapshot.Find("Industrialization", DefinitionKind.Research);
        Require(industrialization.BaseCost >= 200000d,
            "Industrialization must retain a substantial research cost so the Industrial era is not entered by an anomalously cheap transition node.");
    }

    private static void VerifyIndustrialConstructionGateCosts(EconomySnapshot snapshot)
    {
        Require(snapshot.Find("IndustrialOilExtractionComplex", DefinitionKind.Building)
                    .ResourceRequirements.GetValueOrDefault("Machinery") <= 480d,
            "IndustrialOilExtractionComplex must not impose a first-copy Machinery wait above the Industrial gate target.");
        Require(snapshot.Find("IntegratedPetrochemicalComplex", DefinitionKind.Building)
                    .ResourceRequirements.GetValueOrDefault("Machinery") <= 500d,
            "IntegratedPetrochemicalComplex must not impose a first-copy Machinery wait above the Industrial gate target.");
    }

    private static void VerifyIndustrialMetalSinks(EconomySnapshot snapshot)
    {
        Definition machineFactory = snapshot.Find("MachineFactory", DefinitionKind.Building);
        Definition wireMill = snapshot.Find("WireMill", DefinitionKind.Building);
        Require(machineFactory.Consumption.GetValueOrDefault("Bronze") >= 0.5d,
            "MachineFactory must retain a meaningful Bronze sink for Industrial metal output.");
        Require(wireMill.Consumption.GetValueOrDefault("Tin") >= 0.5d,
            "WireMill must retain a meaningful Tin sink for Industrial metal output.");
    }

    private static void VerifyLegacyMetalSurplusDiagnostic()
    {
        Definition producer = new()
        {
            Id = "LegacyMetalProducerProbe",
            Kind = DefinitionKind.Building
        };
        producer.Generation["Bronze"] = 2.5d;
        Definition sink = new()
        {
            Id = "LegacyMetalSinkProbe",
            Kind = DefinitionKind.Building
        };
        sink.Consumption["Bronze"] = 0.5d;

        SimulationState state = new();
        state.Buildings[producer.Id] = 14;
        state.Buildings[sink.Id] = 2;
        var result = new SimulationResult(state, Route.Normal);
        BalanceAnalysis.Analyze(
            result,
            new[] { producer, sink },
            new[] { producer, sink },
            Array.Empty<Definition>());

        Require(result.Warnings.Any(x =>
                x.Type == "Legacy metal surplus" &&
                x.Object == "Bronze" &&
                x.Severity == "Info"),
            "Legacy metal surplus must remain visible when a sink exists but consumes less than ten percent of active production.");
    }

    private static void VerifyConstructionSinkSuppressesExplosionDiagnostic()
    {
        Definition producer = new()
        {
            Id = "ConstructionSinkProducerProbe",
            Kind = DefinitionKind.Building
        };
        producer.Generation["Glass"] = 12d;
        producer.Consumption["Silica"] = 1d;

        Definition activeSink = new()
        {
            Id = "ConstructionSinkActiveProbe",
            Kind = DefinitionKind.Building
        };
        activeSink.Consumption["Glass"] = 0.1d;

        Definition constructionSink = new()
        {
            Id = "ConstructionSinkTargetProbe",
            Kind = DefinitionKind.Building
        };
        constructionSink.ResourceRequirements["Glass"] = 1000d;

        SimulationState state = new();
        state.Buildings[producer.Id] = 1;
        state.Buildings[activeSink.Id] = 1;
        var result = new SimulationResult(state, Route.Normal);
        BalanceAnalysis.Analyze(
            result,
            new[] { producer, activeSink, constructionSink },
            new[] { producer, activeSink, constructionSink },
            Array.Empty<Definition>());

        Require(!result.Warnings.Any(x =>
                x.Type == "Economy explosion" &&
                x.Object == "Glass"),
            "A resource with a legitimate construction sink must not be reported as an unsunk economy explosion.");
    }

    private static void VerifyExtremeConsumptionEfficiency()
    {
        Definition definition = new()
        {
            Id = "ExtremeConsumptionProbe",
            Kind = DefinitionKind.Building,
            FoodConsumption = 1d
        };
        definition.Consumption["CrudeOil"] = double.MaxValue;
        definition.Generation["Electronics"] = 1d;
        SimulationState state = new();
        state.Buildings[definition.Id] = 2;
        state.Resources["CrudeOil"] = double.MaxValue;
        ResourceSimulator.Tick(state, new[] { definition },
            new[] { definition }, 300d);
        Require(state.Resources.Values.All(double.IsFinite),
            "极大资源需求在长步长下不得产生 NaN 或 Infinity。 ");
    }

    private static void VerifyConstructionWaitUsesResourceUnits()
    {
        Definition glassworks = new() { Id = "GlassworksProbe", Kind = DefinitionKind.Building };
        glassworks.Generation["Glass"] = 100d;
        Definition alloyPlant = new() { Id = "AlloyPlantProbe", Kind = DefinitionKind.Building };
        alloyPlant.Generation["TitaniumAlloy"] = 1d;
        Definition target = new() { Id = "MultiMaterialProbe", Kind = DefinitionKind.Building };
        target.ResourceRequirements["Glass"] = 1000d;
        target.ResourceRequirements["TitaniumAlloy"] = 1000d;

        SimulationState state = new();
        state.Buildings[glassworks.Id] = 1;
        state.Buildings[alloyPlant.Id] = 1;
        (string resource, double seconds) = BalanceAnalysis.SlowestConstructionWait(
            state, new[] { glassworks, alloyPlant }, target);

        Require(resource == "TitaniumAlloy" && Math.Abs(seconds - 1000d) < 1e-9d,
            "Construction wait must be calculated per resource, not by summing incompatible resource units.");
    }

    private static void VerifyConstructionMultiplierParity()
    {
        var building = new Definition
        {
            Id = "ConstructionMultiplierProbe",
            Kind = DefinitionKind.Building,
            TechLevel = SimTechLevel.Animal,
            CostGrowth = 1d
        };
        building.ResourceRequirements["WoodLog"] = 100d;
        building.Generation["WoodLog"] = 1d;

        SimulationState state = new()
        {
            Seconds = 60d,
            TechLevel = SimTechLevel.Animal
        };
        state.Resources["WoodLog"] = 50d;
        state.ActiveEffects.Add(new SimEffect
        {
            Kind = SimEffectKind.GlobalConstructionMultiplier,
            Value = 2d
        });

        EconomySnapshot snapshot = new(new[] { building });
        BuildingSimulator.Decide(
            state,
            snapshot,
            SimulationStrategies.Create(Route.Normal));

        Require(state.Buildings.GetValueOrDefault(building.Id) == 1,
            "Building construction multiplier must affect simulator affordability.");
        Require(Math.Abs(state.Resources["WoodLog"] - 0d) < 1e-9d,
            "Simulator must pay the multiplier-adjusted construction cost exactly once.");
    }

    private static void VerifyAtomicResearchPayment()
    {
        var definition = new Definition
        {
            Id = "AtomicPaymentTest",
            Kind = DefinitionKind.Research,
            BaseCost = 100d,
            TechLevel = SimTechLevel.Animal
        };
        definition.ResourceRequirements["WoodLog"] = 10d;
        var state = new SimulationState
        {
            ActiveResearch = new ResearchTask
            {
                Definition = definition,
                Route = Route.Normal
            }
        };
        state.Resources["WoodLog"] = 5d;
        ResearchSimulator.Tick(state, new[] { definition },
            new[] { definition }, 1d);
        Require(!state.ActiveResearch.CostPaid && state.Resources["WoodLog"] == 5d,
            "研究支付不应被部分扣除。");
        state.Resources["WoodLog"] = 10d;
        ResearchSimulator.Tick(state, new[] { definition },
            new[] { definition }, 1d);
        Require(state.ActiveResearch.CostPaid && state.Resources["WoodLog"] == 0d,
            "研究费用没有原子提交。");
    }

    private static void VerifyPopulationProductivityParity()
    {
        SimulationState state = new SimulationState { Population = 5d };
        IReadOnlyList<Definition> noBuildings = Array.Empty<Definition>();

        Require(
            Math.Abs(BuildingSimulator.TotalProductivity(state, noBuildings) - 10d) < 1e-9d,
            "模拟器未应用医学研究时，5人口基础生产力必须为10。" );

        state.ActiveEffects.Add(new SimEffect
        {
            Kind = SimEffectKind.PopulationProductivityMultiplier,
            Value = 1.35d
        });
        Require(
            Math.Abs(BuildingSimulator.TotalProductivity(state, noBuildings) - 13.5d) < 1e-9d,
            "模拟器应用现代医学人口倍率后，5人口生产力必须为13.5。" );
    }

    private static void VerifyGlobalFoodMultiplierUsesMultiplication()
    {
        SimulationState state = new();
        state.ActiveEffects.Add(new SimEffect
        {
            Kind = SimEffectKind.GlobalFoodProductionMultiplier,
            Value = 1.2d
        });
        state.ActiveEffects.Add(new SimEffect
        {
            Kind = SimEffectKind.GlobalFoodProductionMultiplier,
            Value = 1.3d
        });
        Require(Math.Abs(ResourceSimulator.EffectMultiplier(
            state,
            SimEffectKind.GlobalFoodProductionMultiplier,
            "") - 1.56d) < 1e-9d,
            "Global food production multipliers must multiply rather than add.");
    }

    private static void VerifyFoodShortagePopulationAndProductivityDeficit()
    {
        double unhappy = ResourceSimulator.CalculateHappinessMultiplier(-10d, 10d);
        Require(unhappy < 1d && unhappy > 0d,
            "Negative food net rate must lower happiness without producing a negative multiplier.");

        var state = new SimulationState
        {
            Population = 10d,
            PopulationCapacity = 10d,
            HappinessMultiplier = unhappy
        };
        ResourceSimulator.AdvancePopulation(
            state, 10d, unhappy, 1d, 0d, 3600d, false);
        Require(Math.Abs(state.Population - 10d) < 1e-9,
            "Negative food net rate with food still available must not remove population.");
        ResourceSimulator.AdvancePopulation(
            state, 10d, unhappy, 1d, 0d, 3600d, true);
        Require(state.Population < 10d && state.Population >= 0d,
            "Food shortage must slowly reduce population without going below zero.");

        var buildings = new List<Definition>
        {
            new Definition { Id = "Housing", PopulationCapacity = 10d },
            new Definition { Id = "Industry", ProductivityConsumption = 30d }
        };
        state.Buildings["Housing"] = 1;
        state.Buildings["Industry"] = 1;
        Require(BuildingSimulator.AvailableProductivity(state, buildings) < 0d,
            "Available productivity must preserve a negative deficit.");
        Require(BuildingSimulator.AvailableProductivity(state, buildings) <= 0d,
            "Negative available productivity must remain non-positive.");
    }

    private static void VerifyWorkshopTargetCapabilities(EconomySnapshot snapshot)
    {
        foreach (Definition workshop in snapshot.Workshops)
        {
            foreach (SimEffect effect in workshop.Effects)
            {
                if (string.IsNullOrWhiteSpace(effect.Target))
                    continue;
                bool targetsBuilding = effect.Kind == SimEffectKind.BuildingProductionMultiplier ||
                    effect.Kind == SimEffectKind.BuildingResearchPowerMultiplier ||
                    effect.Kind == SimEffectKind.BuildingPowerProductionMultiplier ||
                    effect.Kind == SimEffectKind.BuildingLogisticsProductionMultiplier;
                if (!targetsBuilding)
                    continue;
                Definition building = snapshot.Find(effect.Target, DefinitionKind.Building);
                bool valid = effect.Kind switch
                {
                    SimEffectKind.BuildingProductionMultiplier => building.Generation.Count > 0,
                    SimEffectKind.BuildingResearchPowerMultiplier => building.ResearchPower > 0d,
                    SimEffectKind.BuildingPowerProductionMultiplier => building.PowerProduction > 0d,
                    SimEffectKind.BuildingLogisticsProductionMultiplier => building.LogisticsProduction > 0d,
                    _ => true
                };
                Require(valid,
                    $"工坊 {workshop.Id} 的 {effect.Kind} 指向不具备对应能力的建筑 {building.Id}。");
                }
        }

        Definition orbitalCarbonization = snapshot.Find(
            "OrbitalCarbonizationComplex", DefinitionKind.Building);
        Require(orbitalCarbonization.ResourceRequirements.GetValueOrDefault("Concrete") <= 1200d,
            "OrbitalCarbonizationComplex Concrete first-copy gate must stay within the neighboring Orbital building scale.");

    }

    private static void VerifyResearchTargetCapabilities(EconomySnapshot snapshot)
    {
        foreach (Definition research in snapshot.Research)
        {
            foreach (SimEffect effect in research.Effects)
            {
                if (!IsBuildingTargetEffect(effect.Kind) ||
                    string.IsNullOrWhiteSpace(effect.Target))
                    continue;
                Definition building = snapshot.Find(effect.Target, DefinitionKind.Building);
                bool valid = effect.Kind switch
                {
                    SimEffectKind.BuildingProductionMultiplier => building.Generation.Count > 0,
                    SimEffectKind.BuildingResearchPowerMultiplier => building.ResearchPower > 0d,
                    SimEffectKind.BuildingPowerProductionMultiplier => building.PowerProduction > 0d,
                    SimEffectKind.BuildingLogisticsProductionMultiplier => building.LogisticsProduction > 0d,
                    _ => true
                };
                Require(valid,
                    $"研究 {research.Id} 的 {effect.Kind} 指向不具备对应能力的建筑 {building.Id}。");
            }
        }
    }

    private static void VerifyDefinitionReferenceKinds(EconomySnapshot snapshot)
    {
        foreach (Definition definition in snapshot.All)
        {
            if (definition.Kind == DefinitionKind.Building)
            {
                VerifyReferenceList(snapshot, definition, "RequiredResearch",
                    definition.RequiredResearch, DefinitionKind.Research);
                VerifyReferenceList(snapshot, definition, "RequiredWorkshop",
                    definition.RequiredWorkshop, DefinitionKind.Workshop);
                VerifyResourceList(snapshot, definition, definition.ResourceRequirements);
                if (!string.IsNullOrWhiteSpace(definition.UpgradeTo))
                    VerifyReference(snapshot, definition, "UpgradeTo",
                        definition.UpgradeTo, DefinitionKind.Building);
            }
            else if (definition.Kind == DefinitionKind.Research)
            {
                VerifyReferenceList(snapshot, definition, "Prerequisites",
                    definition.Prerequisites, DefinitionKind.Research);
                VerifyResourceList(snapshot, definition, definition.ResourceRequirements);
            }
            else if (definition.Kind == DefinitionKind.Workshop)
            {
                VerifyReferenceList(snapshot, definition, "RequiredResearch",
                    definition.RequiredResearch, DefinitionKind.Research);
                VerifyReferenceList(snapshot, definition, "RequiredUpgrades",
                    definition.RequiredUpgrades, DefinitionKind.Workshop);
                VerifyResourceList(snapshot, definition, definition.ResourceRequirements);
            }

            foreach (SimEffect effect in definition.Effects)
            {
                DefinitionKind? expected = effect.Kind == SimEffectKind.ResourceProductionMultiplier
                    ? DefinitionKind.Resource
                    : IsBuildingTargetEffect(effect.Kind)
                        ? DefinitionKind.Building
                        : null;
                if (expected.HasValue && !string.IsNullOrWhiteSpace(effect.Target))
                    VerifyReference(snapshot, definition, effect.Kind.ToString(),
                        effect.Target, expected.Value);
            }
        }
    }

    private static bool IsBuildingTargetEffect(SimEffectKind kind) =>
        kind == SimEffectKind.BuildingProductionMultiplier ||
        kind == SimEffectKind.BuildingResearchPowerMultiplier ||
        kind == SimEffectKind.BuildingPowerProductionMultiplier ||
        kind == SimEffectKind.BuildingLogisticsProductionMultiplier;

    private static void VerifyReferenceList(
        EconomySnapshot snapshot,
        Definition owner,
        string field,
        IEnumerable<string> ids,
        DefinitionKind expected)
    {
        foreach (string id in ids)
            VerifyReference(snapshot, owner, field, id, expected);
    }

    private static void VerifyResourceList(
        EconomySnapshot snapshot,
        Definition owner,
        IReadOnlyDictionary<string, double> resources)
    {
        foreach (string id in resources.Keys)
            VerifyReference(snapshot, owner, "ResourceRequirements", id,
                DefinitionKind.Resource);
    }

    private static void VerifyReference(
        EconomySnapshot snapshot,
        Definition owner,
        string field,
        string id,
        DefinitionKind expected)
    {
        try
        {
            snapshot.Find(id, expected);
        }
        catch (Exception)
        {
            Require(false,
                $"{owner.Kind}/{owner.Id}.{field} 引用了错误类型的定义 {id}，期望 {expected}。");
        }
    }

    private static void VerifyWorkshopPurchaseAndEffect()
    {
        var workshop = new Definition
        {
            Id = "WorkshopPurchaseTest",
            Kind = DefinitionKind.Workshop,
            TechLevel = SimTechLevel.Industrial
        };
        workshop.Effects.Add(new SimEffect
        {
            Kind = SimEffectKind.GlobalBuildingProductionMultiplier,
            Value = 1.2d
        });
        var state = new SimulationState
        {
            TechLevel = SimTechLevel.Industrial,
            Tick = 0
        };
        state.ActiveEffects.Add(new SimEffect
        {
            Kind = SimEffectKind.UnlockIndustrialWorkshop,
            Value = 1d
        });
        WorkshopSimulator.Decide(state,
            new EconomySnapshot(new[] { workshop }),
            SimulationStrategies.Create(Route.Fast));
        Require(state.PurchasedWorkshop.Contains(workshop.Id),
            "工坊购买没有被记录。");
        Require(state.ActiveEffects.Any(x =>
                x.Kind == SimEffectKind.GlobalBuildingProductionMultiplier &&
                Math.Abs(x.Value - 1.2d) < 1e-9d),
            "工坊效果没有被激活。");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("模拟器自测失败：" + message);
    }
}
