using System;
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

        Definition unlock = snapshot.Find("IndustrialWorkshop", DefinitionKind.Research);
        Require(unlock.Effects.Any(x =>
                x.Kind == SimEffectKind.UnlockIndustrialWorkshop),
            "工业工坊研究没有解锁工坊系统。");

        Definition boilers = snapshot.Find("ReinforcedBoilers", DefinitionKind.Workshop);
        Require(boilers.Kind == DefinitionKind.Workshop,
            "强化锅炉没有被识别为工坊定义。");
        Require(boilers.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "SteamPlant"),
            "强化锅炉的工坊效果映射错误：" +
            string.Join(";", boilers.Effects.Select(x =>
                $"{x.Kind}:{x.Target}:{x.Value}")));

        Definition integratedFurnaces =
            snapshot.Find("IntegratedFurnaces", DefinitionKind.Workshop);
        Require(integratedFurnaces.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "MetalSmelter" &&
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
        Definition metalResearch =
            snapshot.Find("IndustrialMetalSmelting", DefinitionKind.Research);
        Require(metalResearch.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "IndustrialMetalSmelter"),
            "工业有色金属冶炼研究必须作用于统一冶炼建筑。");

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
        Require(!oilDerrick.Consumption.ContainsKey("Explosives"),
            "石油井不得消耗工业炸药，否则会与化工厂形成生产环。");
        Definition oilRefinery =
            snapshot.Find("OilRefinery", DefinitionKind.Building);
        Require(oilRefinery.Consumption.ContainsKey("CrudeOil"),
            "炼油厂必须消耗原油，才能保持石油加工链的输入闭合。");
        Require(!oilRefinery.Consumption.ContainsKey("Explosives"),
            "炼油厂不得消耗工业炸药，否则会与化工厂形成生产循环。");
        foreach (string consumerId in new[] { "BauxiteMine", "RareMetalMine" })
        {
            Definition consumer = snapshot.Find(consumerId, DefinitionKind.Building);
            Require(consumer.Consumption.ContainsKey("Explosives"),
                $"{consumerId}必须消耗工业炸药。");
        }

        Definition explosivesResearch =
            snapshot.Find("IndustrialExplosives", DefinitionKind.Research);
        Require(explosivesResearch.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "ChemicalPlant" &&
                x.Value >= 1.25d),
            "工业炸药工艺研究必须提升化工厂产量。");
        Definition controlledBlasting =
            snapshot.Find("ControlledBlasting", DefinitionKind.Workshop);
        foreach (string target in new[] { "BauxiteMine", "RareMetalMine", "OilDerrick" })
            Require(controlledBlasting.Effects.Any(x =>
                    x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                    x.Target == target &&
                    x.Value >= 1.15d),
                $"精确爆破工艺必须提升{target}产量。");

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
