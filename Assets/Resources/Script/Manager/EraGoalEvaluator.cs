using System;
using System.Collections.Generic;

public enum EraGoalConditionKind
{
    PrerequisiteResearch,
    Resource
}

public sealed class EraGoalConditionEvaluation
{
    public EraGoalConditionKind Kind { get; }
    public Research Research { get; }
    public Resource Resource { get; }
    public ResearchState ResearchState { get; }
    public bool Met { get; }
    public ExpantaNum RequiredAmount { get; }
    public ExpantaNum PaidAmount { get; }
    public ExpantaNum AvailableAmount { get; }
    public ExpantaNum RemainingAmount { get; }
    public ExpantaNum ProductionRate { get; }
    public ExpantaNum ConsumptionRate { get; }

    internal EraGoalConditionEvaluation(
        Research prerequisite,
        ResearchState state,
        bool met)
    {
        Kind = EraGoalConditionKind.PrerequisiteResearch;
        Research = prerequisite;
        ResearchState = state;
        Met = met;
        RequiredAmount = ExpantaNum.Zero;
        PaidAmount = ExpantaNum.Zero;
        AvailableAmount = ExpantaNum.Zero;
        RemainingAmount = ExpantaNum.Zero;
        ProductionRate = ExpantaNum.Zero;
        ConsumptionRate = ExpantaNum.Zero;
    }

    internal EraGoalConditionEvaluation(
        Resource resource,
        ResearchState transitionState,
        ExpantaNum requiredAmount,
        ExpantaNum availableAmount,
        ExpantaNum productionRate,
        ExpantaNum consumptionRate)
    {
        Kind = EraGoalConditionKind.Resource;
        Resource = resource;
        RequiredAmount = ExpantaNum.Max(ExpantaNum.Zero, requiredAmount);
        PaidAmount = transitionState == null
            ? ExpantaNum.Zero
            : transitionState.GetPaidResourceCost(resource);
        AvailableAmount = ExpantaNum.Max(ExpantaNum.Zero, availableAmount);
        RemainingAmount = ExpantaNum.Max(
            ExpantaNum.Zero,
            RequiredAmount - PaidAmount);
        ProductionRate = ExpantaNum.Max(ExpantaNum.Zero, productionRate);
        ConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, consumptionRate);
        Met = RemainingAmount <= AvailableAmount;
    }
}

public sealed class EraGoalEvaluation
{
    public TechLevel CurrentEra { get; }
    public TechLevel TargetEra { get; }
    public Research Transition { get; }
    public IReadOnlyList<EraGoalConditionEvaluation> Conditions { get; }

    internal EraGoalEvaluation(
        TechLevel currentEra,
        TechLevel targetEra,
        Research transition,
        IReadOnlyList<EraGoalConditionEvaluation> conditions)
    {
        CurrentEra = currentEra;
        TargetEra = targetEra;
        Transition = transition;
        Conditions = conditions;
    }
}

public static class EraGoalEvaluator
{
    public static EraGoalEvaluation Evaluate(
        TechLevel currentEra,
        ResearchManager researchManager,
        ResourceManager resourceManager)
    {
        // The last era has no successor: return an empty evaluation instead of
        // producing an out-of-range TechLevel value.
        if (currentEra == TechLevel.Archotech)
            return new EraGoalEvaluation(
                currentEra,
                currentEra,
                null,
                new List<EraGoalConditionEvaluation>());
        TechLevel targetEra = (TechLevel)((int)currentEra + 1);
        Research transition = FindTransition(targetEra);
        var conditions = new List<EraGoalConditionEvaluation>();
        if (transition == null)
            return new EraGoalEvaluation(currentEra, targetEra, null, conditions);

        ResearchState transitionState = null;
        if (researchManager != null)
            researchManager.States.TryGetValue(transition, out transitionState);

        IReadOnlyList<Research> prerequisites = transition.Prerequisites;
        for (int i = 0; i < prerequisites.Count; i++)
        {
            Research prerequisite = prerequisites[i];
            if (prerequisite == null)
                continue;

            ResearchState prerequisiteState = null;
            if (researchManager != null)
                researchManager.States.TryGetValue(prerequisite, out prerequisiteState);
            bool met = prerequisiteState != null &&
                prerequisiteState.Status == ResearchStatus.Completed;
            conditions.Add(new EraGoalConditionEvaluation(
                prerequisite,
                prerequisiteState,
                met));
        }

        var resources = new List<Resource>();
        var requiredAmounts = new List<ExpantaNum>();
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements =
            transition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;

            int resourceIndex = -1;
            for (int j = 0; j < resources.Count; j++)
                if (resources[j] == requirement.First)
                {
                    resourceIndex = j;
                    break;
                }

            if (resourceIndex < 0)
            {
                resources.Add(requirement.First);
                requiredAmounts.Add(requirement.Second);
            }
            else
                requiredAmounts[resourceIndex] += requirement.Second;
        }

        for (int i = 0; i < resources.Count; i++)
        {
            Resource resource = resources[i];
            ExpantaNum available = ExpantaNum.Zero;
            ExpantaNum productionRate = ExpantaNum.Zero;
            ExpantaNum consumptionRate = ExpantaNum.Zero;
            if (resourceManager != null &&
                resourceManager.States.TryGetValue(resource, out ResourceState resourceState))
            {
                available = resourceState.Amount;
                productionRate = ResourceManager.ApplyCurrentProductionReward(
                    resourceState.ProductionRate);
                consumptionRate = resourceState.ConsumptionRate;
            }
            conditions.Add(new EraGoalConditionEvaluation(
                resource,
                transitionState,
                requiredAmounts[i],
                available,
                productionRate,
                consumptionRate));
        }

        return new EraGoalEvaluation(
            currentEra,
            targetEra,
            transition,
            conditions);
    }

    public static Research FindTransition(TechLevel targetEra)
    {
        IReadOnlyList<Research> definitions = DataBase<Research>.All;
        Research result = null;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research candidate = definitions[i];
            if (candidate == null || !candidate.AdvancesTechLevel ||
                candidate.TechLevel != targetEra)
                continue;
            if (result == null || string.CompareOrdinal(candidate.Id, result.Id) < 0)
                result = candidate;
        }
        return result;
    }
}
