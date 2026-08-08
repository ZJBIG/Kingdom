using System;
using System.Linq;

namespace Kingdom.EconomySimulation;

public static class SimulatorSelfTests
{
    public static void Run(EconomySnapshot snapshot)
    {
        Require(snapshot.Resources.Count > 0, "No resources were loaded.");
        Require(snapshot.Buildings.Count > 0, "No buildings were loaded.");
        Require(snapshot.Research.Count > 0, "No research was loaded.");
        Require(snapshot.Workshops.Count > 0, "No Workshop upgrades were loaded.");

        Definition unlock = snapshot.Find("IndustrialWorkshop", DefinitionKind.Research);
        Require(unlock.Effects.Any(x =>
                x.Kind == SimEffectKind.UnlockSystem &&
                x.SystemId == WorkshopSimulator.WorkshopSystemId),
            "IndustrialWorkshop does not unlock the Workshop system.");

        Definition boilers = snapshot.Find("ReinforcedBoilers", DefinitionKind.Workshop);
        Require(boilers.Kind == DefinitionKind.Workshop,
            "ReinforcedBoilers was not typed as a Workshop definition.");
        Require(boilers.Effects.Any(x =>
                x.Kind == SimEffectKind.BuildingProductionMultiplier &&
                x.Target == "SteamPlant"),
            "Workshop effect mapping for ReinforcedBoilers is incorrect: " +
            string.Join(";", boilers.Effects.Select(x =>
                $"{x.Kind}:{x.Target}:{x.Value}")));

        Require(Math.Abs(EconomySimulationParity.AdvanceStockpile(
            3d, 2d, 5d, .5d) - 1.5d) < 1e-9d,
            "Stockpile parity formula failed.");
        Require(Math.Abs(EconomySimulationParity.CalculateSatisfaction(
            2d, 3d, 10d, .5d) - .7d) < 1e-9d,
            "Satisfaction parity formula failed.");
        Require(Math.Abs(EconomySimulationParity.ResearchSpeedEffect(0, 2) - .4d)
            < 1e-9d, "Research speed parity formula failed.");
        double lowHappiness = ResourceSimulator.CalculateHappinessMultiplier(-10d, 10d, 0.5d);
        double highHappiness = ResourceSimulator.CalculateHappinessMultiplier(1000000d, 10d, 1d);
        Require(Math.Abs(lowHappiness - 0.5d) < 1e-9d,
            "Happiness must own the food deficit penalty.");
        Require(highHappiness > lowHappiness && highHappiness < 1.5d,
            "Happiness curve must be increasing and bounded.");

        VerifyAtomicResearchPayment();
        VerifyWorkshopPurchaseAndEffect();

        foreach (Route route in Enum.GetValues<Route>())
        {
            ISimulationStrategy strategy = SimulationStrategies.Create(route);
            Require(strategy.Route == route, $"Strategy route mismatch for {route}.");
            Require(strategy.ResearchDecisionInterval > 0 &&
                strategy.BuildingDecisionInterval > 0 &&
                strategy.WorkshopDecisionInterval > 0,
                $"Strategy interval is invalid for {route}.");
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
            "Research payment was partially deducted.");
        state.Resources["WoodLog"] = 10d;
        ResearchSimulator.Tick(state, new[] { definition },
            new[] { definition }, 1d);
        Require(state.ActiveResearch.CostPaid && state.Resources["WoodLog"] == 0d,
            "Research payment was not committed atomically.");
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
            Kind = SimEffectKind.UnlockSystem,
            SystemId = WorkshopSimulator.WorkshopSystemId,
            Value = 1d
        });
        WorkshopSimulator.Decide(state,
            new EconomySnapshot(new[] { workshop }),
            SimulationStrategies.Create(Route.Fast));
        Require(state.PurchasedWorkshop.Contains(workshop.Id),
            "Workshop purchase was not recorded.");
        Require(state.ActiveEffects.Any(x =>
                x.Kind == SimEffectKind.GlobalBuildingProductionMultiplier &&
                Math.Abs(x.Value - 1.2d) < 1e-9d),
            "Workshop effect was not activated.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Simulator self-test failed: " + message);
    }
}
