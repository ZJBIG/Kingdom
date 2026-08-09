using System;
using System.Linq;

namespace Kingdom.EconomySimulation;

public static class WorkshopSimulator
{
    public static void Decide(
        SimulationState state,
        EconomySnapshot snapshot,
        ISimulationStrategy strategy)
    {
        if (state.Tick % strategy.WorkshopDecisionInterval != 0)
            return;
        if (!IsSystemUnlocked(state))
        {
            state.TraceDecision(strategy.Route, "Workshop", "Blocked", "",
                "industrial-workshop system is locked");
            return;
        }

        Definition[] unlocked = snapshot.Workshops
            .Where(x => !state.PurchasedWorkshop.Contains(x.Id))
            .Where(x => x.TechLevel <= state.TechLevel)
            .Where(x => x.RequiredResearch.All(state.CompletedResearch.Contains))
            .Where(x => x.RequiredUpgrades.All(state.PurchasedWorkshop.Contains))
            .OrderByDescending(x => strategy.ScoreWorkshop(x, state))
            .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Definition? candidate = unlocked.FirstOrDefault(x =>
            ResourceSimulator.CanPay(state, x.ResourceRequirements));
        if (candidate == null)
        {
            state.TraceDecision(strategy.Route, "Workshop", "Waiting", "",
                unlocked.Length == 0
                    ? "no prerequisite-complete upgrade"
                    : "upgrade resources are insufficient");
            return;
        }

        ResourceSimulator.Pay(state, candidate.ResourceRequirements);
        state.PurchasedWorkshop.Add(candidate.Id);
        state.ActiveEffects.AddRange(candidate.Effects);
        state.MarkAction("WorkshopPurchased", candidate.Id);
        state.Events[^1].Count = 1;
        state.TraceDecision(strategy.Route, "Workshop", "Purchased", candidate.Id,
            $"score={strategy.ScoreWorkshop(candidate, state):0.###}");
    }

    public static bool IsSystemUnlocked(SimulationState state) =>
        state.ActiveEffects.Any(x =>
            x.Kind == SimEffectKind.UnlockIndustrialWorkshop);
}
