using System;
using System.Collections.Generic;
using System.Linq;

namespace Kingdom.EconomySimulation;

public interface ISimulationStrategy
{
    Route Route { get; }
    int ResearchDecisionInterval { get; }
    int BuildingDecisionInterval { get; }
    int WorkshopDecisionInterval { get; }
    int BuildingLimit(Definition building);
    double TargetResearchPower(SimTechLevel techLevel);
    double ScoreBuilding(Definition building, int owned);
    double ScoreResearch(
        Definition research,
        SimulationState state,
        EconomySnapshot snapshot);
    double ScoreWorkshop(Definition workshop, SimulationState state);
}

public static class SimulationStrategies
{
    public static ISimulationStrategy Create(Route route) => route switch
    {
        Route.Fast => new FastStrategy(),
        Route.Normal => new NormalStrategy(),
        Route.Conservative => new ConservativeStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private abstract class StrategyBase : ISimulationStrategy
    {
        public abstract Route Route { get; }
        public abstract int ResearchDecisionInterval { get; }
        public abstract int BuildingDecisionInterval { get; }
        public abstract int WorkshopDecisionInterval { get; }
        protected abstract int NormalBuildingLimit { get; }
        protected abstract int CapacityBuildingLimit { get; }
        protected abstract int ProductivityBuildingLimit { get; }

        public int BuildingLimit(Definition building)
        {
            if (building.PopulationCapacity > 0d)
                return CapacityBuildingLimit;
            if (building.ProductivityGranted > 0d)
                return ProductivityBuildingLimit;
            return NormalBuildingLimit;
        }

        public virtual double TargetResearchPower(SimTechLevel techLevel) =>
            techLevel switch
            {
                SimTechLevel.Animal => 3d,
                SimTechLevel.Neolithic => 20d,
                SimTechLevel.Medieval => 40d,
                _ => 100d
            };

        public virtual double ScoreBuilding(Definition building, int owned)
        {
            double firstCopy = owned == 0 ? 120d : 0d;
            return firstCopy + building.ResearchPower * 100d +
                building.Generation.Values.Sum() * 100d -
                building.Consumption.Values.Sum() * 10d +
                building.ProductivityGranted * 40d +
                building.PopulationCapacity * 5d +
                building.FoodProduction * 30d;
        }

        public virtual double ScoreResearch(
            Definition research,
            SimulationState state,
            EconomySnapshot snapshot)
        {
            double unlocks = snapshot.Buildings.Count(x =>
                x.RequiredResearch.Contains(research.Id,
                    StringComparer.OrdinalIgnoreCase));
            double effectValue = research.Effects.Count * 15d;
            double systemUnlock = research.Effects.Any(x =>
                x.Kind == SimEffectKind.UnlockIndustrialWorkshop) ? 1000d : 0d;
            double era = research.AdvancesTechLevel ? -25d : 0d;
            return unlocks * 80d + effectValue + systemUnlock + era -
                Math.Log10(Math.Max(1d, research.BaseCost)) * 12d;
        }

        public virtual double ScoreWorkshop(
            Definition workshop,
            SimulationState state) =>
            workshop.Effects.Count * 50d -
            workshop.ResourceRequirements.Values.Sum() / 1000d;
    }

    private sealed class FastStrategy : StrategyBase
    {
        public override Route Route => Route.Fast;
        public override int ResearchDecisionInterval => 1;
        public override int BuildingDecisionInterval => 10;
        public override int WorkshopDecisionInterval => 10;
        protected override int NormalBuildingLimit => 4;
        protected override int CapacityBuildingLimit => 22;
        protected override int ProductivityBuildingLimit => 12;
        public override double TargetResearchPower(SimTechLevel techLevel) =>
            base.TargetResearchPower(techLevel) * 1.3d;
        public override double ScoreResearch(
            Definition research,
            SimulationState state,
            EconomySnapshot snapshot) =>
            base.ScoreResearch(research, state, snapshot) +
            (research.AdvancesTechLevel ? 40d : 0d);
    }

    private sealed class NormalStrategy : StrategyBase
    {
        public override Route Route => Route.Normal;
        public override int ResearchDecisionInterval => 180;
        public override int BuildingDecisionInterval => 45;
        public override int WorkshopDecisionInterval => 45;
        protected override int NormalBuildingLimit => 3;
        protected override int CapacityBuildingLimit => 20;
        protected override int ProductivityBuildingLimit => 10;
    }

    private sealed class ConservativeStrategy : StrategyBase
    {
        public override Route Route => Route.Conservative;
        public override int ResearchDecisionInterval => 300;
        public override int BuildingDecisionInterval => 270;
        public override int WorkshopDecisionInterval => 270;
        protected override int NormalBuildingLimit => 2;
        protected override int CapacityBuildingLimit => 12;
        protected override int ProductivityBuildingLimit => 6;
        public override double TargetResearchPower(SimTechLevel techLevel) =>
            base.TargetResearchPower(techLevel) * .65d;
        public override double ScoreBuilding(Definition building, int owned) =>
            base.ScoreBuilding(building, owned) +
            (building.FoodProduction > 0d ? 50d : 0d) -
            building.Consumption.Values.Sum() * 20d;
    }
}
