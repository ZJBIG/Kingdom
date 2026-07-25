using System;
using System.Collections.Generic;
using UnityEngine;

public class ResearchManager : Singleton<ResearchManager>
{
    public ExpantaNum GlobalEfficiencyFactor { get; set; } = ExpantaNum.One;

    private readonly Dictionary<Research, ResearchState> states = new();
    private readonly Dictionary<TechLevel, int> researchCountByTech = new();
    private readonly List<ResearchState> orderedStates = new();

    public IReadOnlyDictionary<Research, ResearchState> States => states;
    public IReadOnlyDictionary<TechLevel, int> ResearchCountByTech => researchCountByTech;
    public ResearchState ActiveResearch { get; private set; }
    public int TotalResearchCount => orderedStates.Count;
    public string SelectedResearchId { get; private set; } = string.Empty;
    public event Action<ResearchState> ResearchStateAdded;

    public int TotalFinishedResearchCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < orderedStates.Count; i++)
                if (orderedStates[i].Status == ResearchStatus.Completed)
                    count++;
            return count;
        }
    }

    protected override void Initialize()
    {
        IReadOnlyList<Research> researches = DataBase<Research>.All;
        if (!ResearchValidator.ValidateNoCycles(researches, out string error))
        {
            Debug.LogError(error);
            enabled = false;
            return;
        }

        InitializeResearchStates(researches);
        InitializeResearchCount();
    }

    public ResearchState GetState(Research research)
    {
        if (research == null)
            throw new ArgumentNullException(nameof(research));
        if (states.TryGetValue(research, out ResearchState state))
            return state;
        throw new KeyNotFoundException($"Research state '{research.Id}' has not been created.");
    }

    public bool StartResearch(Research research)
    {
        if (research == null || !states.ContainsKey(research))
        {
            Debug.LogError("Cannot start a null or uninitialized research definition.");
            return false;
        }
        ResearchState state = states[research];
        if (state.Status == ResearchStatus.Completed ||
            GameManager.Instance.State.TechLevel < research.TechLevel ||
            !ArePrerequisitesCompleted(research) || !state.CostPaid)
            return false;

        if (ActiveResearch == state)
            return true;

        if (ActiveResearch != null)
        {
            ActiveResearch.SetStatus(
                ArePrerequisitesCompleted(ActiveResearch.Definition)
                    ? ResearchStatus.Available
                    : ResearchStatus.Locked);
        }

        ActiveResearch = state;
        state.SetStatus(ResearchStatus.Researching);
        return true;
    }

    public void SetSelectedResearch(Research research)
    {
        SelectedResearchId = research == null ? string.Empty : research.Id;
    }

    public void Tick(double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        ResearchState current = ActiveResearch;
        if (current == null)
            return;

        if (!current.CostPaid)
        {
            current.SetStatus(ResearchStatus.WaitingResources);
            return;
        }

        current.SetStatus(ResearchStatus.Researching);
        ExpantaNum speed = ResearchSpeedEffect(
            GameManager.Instance.State.TechLevel,
            current.Definition.TechLevel) * GlobalEfficiencyFactor;
        current.SetProgress(AdvanceResearchProgress(
            current.Progress,
            speed,
            current.BaseCost,
            deltaSeconds));

        if (current.Progress < current.BaseCost)
            return;

        CompleteCurrentResearch(current);
    }

    public static bool TryPayResearchCost(ResearchState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (state.CostPaid)
            return true;

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
        bool fullyPaid = true;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.Second <= ExpantaNum.Zero)
                continue;

            ResourceState resource = ResourceManager.Instance.EnsureResource(requirement.First);
            ExpantaNum paid = state.GetPaidResourceCost(requirement.First);
            ExpantaNum remaining = ExpantaNum.Max(ExpantaNum.Zero, requirement.Second - paid);
            if (remaining <= ExpantaNum.Zero)
                continue;

            ExpantaNum payment = ExpantaNum.Min(
                ExpantaNum.Max(ExpantaNum.Zero, resource.Amount),
                remaining);
            if (payment > ExpantaNum.Zero)
            {
                ResourceManager.Instance.AddAmount(requirement.First, -payment);
                state.SetPaidResourceCost(requirement.First, paid + payment);
            }

            if (paid + payment < requirement.Second)
                fullyPaid = false;
        }

        if (fullyPaid)
            state.SetCostPaid(true);
        return fullyPaid;
    }

    private void CompleteCurrentResearch(ResearchState current)
    {
        current.SetProgress(current.BaseCost);
        current.SetStatus(ResearchStatus.Completed);
        ActiveResearch = null;

        IReadOnlyList<Building> unlocks = current.Definition.BuildingUnlock;
        if (unlocks != null)
        {
            for (int i = 0; i < unlocks.Count; i++)
                BuildingManager.Instance.AddBuilding(unlocks[i]);
        }

        RefreshAvailabilityStatuses();
    }

    private void RefreshAvailabilityStatuses()
    {
        for (int i = 0; i < orderedStates.Count; i++)
        {
            ResearchState state = orderedStates[i];
            if (state.Status == ResearchStatus.Completed)
                continue;
            state.SetStatus(ArePrerequisitesCompleted(state.Definition)
                ? ResearchStatus.Available
                : ResearchStatus.Locked);
        }
    }

    public bool ArePrerequisitesCompleted(Research research)
    {
        IReadOnlyList<Research> prerequisites = research.Prerequisites;
        if (prerequisites == null)
            return true;
        for (int i = 0; i < prerequisites.Count; i++)
            if (states[prerequisites[i]].Status != ResearchStatus.Completed)
                return false;
        return true;
    }

    public static ExpantaNum AdvanceResearchProgress(
        ExpantaNum current,
        ExpantaNum speedPerSecond,
        ExpantaNum baseCost,
        double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        return ExpantaNum.Min(baseCost, current + speedPerSecond * deltaSeconds);
    }

    public static double ResearchSpeedEffect(TechLevel current, TechLevel target)
    {
        if (current == target)
            return 1d;
        return 1d / Math.Abs((int)target - (int)current + 0.5d);
    }

    private void InitializeResearchStates(IReadOnlyList<Research> researches)
    {
        for (int i = 0; i < researches.Count; i++)
        {
            Research research = researches[i];
            var state = new ResearchState(research);
            states.Add(research, state);
            orderedStates.Add(state);
            ResearchStateAdded?.Invoke(state);
        }
    }

    private void InitializeResearchCount()
    {
        foreach (TechLevel techLevel in Enum.GetValues(typeof(TechLevel)))
            researchCountByTech[techLevel] = 0;
        for (int i = 0; i < orderedStates.Count; i++)
            researchCountByTech[orderedStates[i].Definition.TechLevel]++;
    }

    internal SaveManager.ResearchSaveData CaptureSaveData()
    {
        var data = new SaveManager.ResearchSaveData
        {
            States = new List<SaveManager.ResearchStateSaveData>(orderedStates.Count),
            ActiveResearchId = ActiveResearch?.Definition.Id,
            SelectedResearchId = SelectedResearchId,
            GlobalEfficiencyFactor = GlobalEfficiencyFactor.ToString()
        };

        for (int i = 0; i < orderedStates.Count; i++)
        {
            ResearchState state = orderedStates[i];
            data.States.Add(new SaveManager.ResearchStateSaveData
            {
                ResearchId = state.Definition.Id,
                Progress = state.Progress.ToString(),
                CostPaid = state.CostPaid,
                Completed = state.Status == ResearchStatus.Completed,
                PaidResourceCosts = CapturePaidResourceCosts(state)
            });
        }

        return data;
    }

    internal void ResetForLoad()
    {
        ActiveResearch = null;
        SelectedResearchId = string.Empty;
        GlobalEfficiencyFactor = ExpantaNum.One;
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();
    }

    internal void RestoreSaveData(SaveManager.ResearchSaveData data)
    {
        if (data == null)
            return;

        if (data.States != null)
        {
            for (int i = 0; i < data.States.Count; i++)
            {
                SaveManager.ResearchStateSaveData saved = data.States[i];
                ResearchState state = GetState(DataBase<Research>.Find(saved.ResearchId));
                state.Restore(
                    Parse(saved.Progress, saved.ResearchId, nameof(saved.Progress)),
                    saved.CostPaid,
                    saved.Completed,
                    RestorePaidResourceCosts(saved.PaidResourceCosts));
            }
        }

        RefreshAvailabilityStatuses();
        if (!string.IsNullOrWhiteSpace(data.ActiveResearchId))
        {
            ResearchState state = GetState(DataBase<Research>.Find(data.ActiveResearchId));
            if (state.Status != ResearchStatus.Completed && ArePrerequisitesCompleted(state.Definition))
            {
                ActiveResearch = state;
                state.SetStatus(state.CostPaid
                    ? ResearchStatus.Researching
                    : ResearchStatus.WaitingResources);
            }
        }

        if (string.IsNullOrWhiteSpace(data.SelectedResearchId))
        {
            SelectedResearchId = string.Empty;
        }
        else
        {
            Research selected = DataBase<Research>.Find(data.SelectedResearchId);
            SelectedResearchId = selected.Id;
        }
        GlobalEfficiencyFactor = Parse(
            data.GlobalEfficiencyFactor,
            nameof(ResearchManager),
            nameof(data.GlobalEfficiencyFactor),
            ExpantaNum.One);
    }

    public override void Save() => SaveManager.Instance.SaveNow(true);

    public override void Load() => SaveManager.Instance.LoadOrCreateGame();

    private static ExpantaNum Parse(
        string raw,
        string owner,
        string field,
        ExpantaNum fallback = default)
    {
        if (ExpantaNum.TryParse(raw, out ExpantaNum value))
            return value;
        if (string.IsNullOrEmpty(raw))
            return fallback;
        throw new FormatException($"Invalid ExpantaNum '{raw}' for {owner}.{field}.");
    }

    private static List<SaveManager.ResearchResourceCostSaveData> CapturePaidResourceCosts(ResearchState state)
    {
        var result = new List<SaveManager.ResearchResourceCostSaveData>();
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            ExpantaNum paid = state.GetPaidResourceCost(requirements[i].First);
            if (paid > ExpantaNum.Zero)
            {
                result.Add(new SaveManager.ResearchResourceCostSaveData
                {
                    ResourceId = requirements[i].First.Id,
                    Amount = paid.ToString()
                });
            }
        }
        return result;
    }

    private static IReadOnlyDictionary<Resource, ExpantaNum> RestorePaidResourceCosts(
        List<SaveManager.ResearchResourceCostSaveData> savedCosts)
    {
        var result = new Dictionary<Resource, ExpantaNum>();
        if (savedCosts == null)
            return result;

        for (int i = 0; i < savedCosts.Count; i++)
        {
            SaveManager.ResearchResourceCostSaveData saved = savedCosts[i];
            Resource resource = DataBase<Resource>.Find(saved.ResourceId);
            result[resource] = Parse(saved.Amount, saved.ResourceId, nameof(saved.Amount));
        }
        return result;
    }

}
