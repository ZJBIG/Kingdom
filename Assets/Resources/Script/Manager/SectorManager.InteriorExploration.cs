using System;
using System.Collections.Generic;

public enum SectorInteriorExplorationStatus
{
    Locked, Ready, Running, WaitingForSupply, Paused, Completed
}

public sealed class SectorInteriorExplorationPreview
{
    public SectorInteriorExplorationStatus Status { get; internal set; }
    public SectorOperationFailure Failure { get; internal set; }
    public ExpantaNum Progress { get; internal set; }
    public ExpantaNum EstimatedSecondsRemaining { get; internal set; }
    public ResourceDef MissingResource { get; internal set; }
    public ResearchDef MissingResearch { get; internal set; }
}

public sealed partial class SectorManager
{
    private readonly Dictionary<ResourceDef, ExpantaNum> interiorPaymentBuffer = new();
    private Action interiorCommit;
    private Action interiorRollback;
    private SectorState interiorTransactionState;
    private ExpantaNum interiorPreviousProgress;
    private bool interiorPreviousActive;
    private ExpantaNum interiorNextProgress;

    private void CommitInteriorPayment() => interiorTransactionState.SetInteriorExploration(
        interiorNextProgress, interiorNextProgress < ExpantaNum.One);
    private void RollbackInteriorPayment() => interiorTransactionState.SetInteriorExploration(
        interiorPreviousProgress, interiorPreviousActive);

    public SectorInteriorExplorationPreview GetInteriorExplorationPreview(SectorDef definition)
    {
        EnsureInitialized();
        var preview = new SectorInteriorExplorationPreview();
        preview.Failure = GetInteriorExplorationFailure(definition, out SectorState state);
        if (state != null)
            preview.Progress = state.InteriorExplorationProgress;
        SectorInteriorExplorationDefinition project = definition == null ? null : definition.InteriorExploration;
        if (project != null && project.DurationSeconds.IsFinite && project.DurationSeconds > ExpantaNum.Zero)
            preview.EstimatedSecondsRemaining = (ExpantaNum.One - preview.Progress) * project.DurationSeconds;
        if (state != null && state.InteriorExplorationCompleted)
        {
            preview.Status = SectorInteriorExplorationStatus.Completed;
            return preview;
        }
        if (preview.Failure != SectorOperationFailure.None)
        {
            preview.Status = SectorInteriorExplorationStatus.Locked;
            if (preview.Failure == SectorOperationFailure.InteriorExplorationResearchRequired)
                preview.MissingResearch = GetMissingInteriorResearch(project);
            return preview;
        }
        if (!state.InteriorExplorationActive)
        {
            preview.Status = state.InteriorExplorationPaused || preview.Progress > ExpantaNum.Zero
                ? SectorInteriorExplorationStatus.Paused : SectorInteriorExplorationStatus.Ready;
            return preview;
        }
        ResourceManager.TryGetInstance(out ResourceManager resources);
        if (GetInteriorAffordableSeconds(project, resources, ExpantaNum.One, out ResourceDef missing) <= ExpantaNum.Zero)
        {
            preview.Status = SectorInteriorExplorationStatus.WaitingForSupply;
            preview.Failure = SectorOperationFailure.InsufficientInteriorExplorationSupply;
            preview.MissingResource = missing;
        }
        else
            preview.Status = SectorInteriorExplorationStatus.Running;
        return preview;
    }

    public bool TryStartInteriorExploration(SectorDef definition, out SectorOperationFailure failure)
    {
        EnsureInitialized();
        failure = GetInteriorExplorationFailure(definition, out SectorState state);
        if (failure != SectorOperationFailure.None)
            return false;
        state.SetInteriorExploration(state.InteriorExplorationProgress, true);
        return true;
    }

    public void PauseInteriorExploration(SectorDef definition)
    {
        EnsureInitialized();
        if (definition != null && TryGetStateByStableId(definition, out SectorState state) && state.InteriorExplorationActive)
            state.SetInteriorExploration(state.InteriorExplorationProgress, false, true);
    }

    private SectorOperationFailure GetInteriorExplorationFailure(SectorDef definition, out SectorState state)
    {
        state = null;
        if (definition == null || !TryGetStateByStableId(definition, out state))
            return SectorOperationFailure.UnknownSector;
        SectorInteriorExplorationDefinition project = definition.InteriorExploration;
        if (project == null)
            return SectorOperationFailure.InteriorExplorationUnavailable;
        if (!ValidateInteriorDefinition(project))
            return SectorOperationFailure.InvalidInteriorExploration;
        if (!state.Unlocked || !state.Occupied)
            return SectorOperationFailure.InteriorExplorationRequiresOccupation;
        if (state.InteriorExplorationCompleted)
            return SectorOperationFailure.InteriorExplorationCompleted;
        if (GetMissingInteriorResearch(project) != null)
            return SectorOperationFailure.InteriorExplorationResearchRequired;
        return SectorOperationFailure.None;
    }

    private static ResearchDef GetMissingInteriorResearch(SectorInteriorExplorationDefinition project)
    {
        IReadOnlyList<ResearchDef> required = project.RequiredResearch;
        ResearchManager.TryGetInstance(out ResearchManager research);
        for (int i = 0; required != null && i < required.Count; i++)
            if (required[i] != null && (research == null || research.GetState(required[i]).Status != ResearchStatus.Completed))
                return required[i];
        return null;
    }

    internal static bool ValidateInteriorDefinition(SectorInteriorExplorationDefinition project)
    {
        if (project == null || !project.DurationSeconds.IsFinite || project.DurationSeconds <= ExpantaNum.Zero ||
            string.IsNullOrWhiteSpace(project.Label) || string.IsNullOrWhiteSpace(project.Description) ||
            string.IsNullOrWhiteSpace(project.CompletionDescription) || project.Milestones == null || project.Milestones.Count != 3)
            return false;
        for (int i = 0; i < project.Milestones.Count; i++)
            if (string.IsNullOrWhiteSpace(project.Milestones[i]))
                return false;
        IReadOnlyList<ResearchDef> required = project.RequiredResearch;
        for (int i = 0; required != null && i < required.Count; i++)
        {
            if (required[i] == null) return false;
            for (int j = 0; j < i; j++)
                if (required[j] == required[i]) return false;
        }
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates = project.ResourceRatesPerSecond;
        if (rates == null || rates.Count == 0)
            return false;
        for (int i = 0; i < rates.Count; i++)
        {
            if (rates[i].First == null || !rates[i].Second.IsFinite || rates[i].Second <= ExpantaNum.Zero)
                return false;
            for (int j = 0; j < i; j++)
                if (rates[j].First == rates[i].First)
                    return false;
        }
        return true;
    }

    private static ExpantaNum GetInteriorAffordableSeconds(SectorInteriorExplorationDefinition project,
        ResourceManager resources, ExpantaNum requestedSeconds, out ResourceDef missing)
    {
        ExpantaNum seconds = requestedSeconds;
        missing = null;
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates = project.ResourceRatesPerSecond;
        for (int i = 0; i < rates.Count; i++)
        {
            ExpantaNum available = resources == null ? ExpantaNum.Zero : resources.GetAmount(rates[i].First);
            ExpantaNum affordable = available / rates[i].Second;
            if (affordable < seconds)
            {
                seconds = ExpantaNum.Max(ExpantaNum.Zero, affordable);
                missing = rates[i].First;
            }
        }
        return seconds;
    }

    public bool TickActiveInteriorExploration(double deltaSeconds, GameState runtimeState,
        ResourceManager resourceManager, out SectorOperationFailure failure)
    {
        EnsureInitialized();
        failure = SectorOperationFailure.None;
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d || runtimeState == null)
        {
            failure = SectorOperationFailure.InvalidDelta;
            return false;
        }
        bool advanced = false;
        // orderedStates is maintained by stable ID, including competing resource consumers.
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            if (!state.InteriorExplorationActive)
                continue;
            SectorOperationFailure prerequisite = GetInteriorExplorationFailure(state.Definition, out _);
            if (prerequisite != SectorOperationFailure.None)
            {
                failure = prerequisite;
                continue;
            }
            SectorInteriorExplorationDefinition project = state.Definition.InteriorExploration;
            ExpantaNum remaining = (ExpantaNum.One - state.InteriorExplorationProgress) * project.DurationSeconds;
            ExpantaNum requested = ExpantaNum.Min(remaining, new ExpantaNum(deltaSeconds));
            ExpantaNum seconds = GetInteriorAffordableSeconds(project, resourceManager, requested, out _);
            if (seconds <= ExpantaNum.Zero)
            {
                if (deltaSeconds > 0d)
                    failure = SectorOperationFailure.InsufficientInteriorExplorationSupply;
                continue;
            }
            interiorPaymentBuffer.Clear();
            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates = project.ResourceRatesPerSecond;
            for (int j = 0; j < rates.Count; j++)
                interiorPaymentBuffer.Add(rates[j].First, rates[j].Second * seconds);
            ExpantaNum previousProgress = state.InteriorExplorationProgress;
            bool previousActive = state.InteriorExplorationActive;
            ExpantaNum next = seconds >= remaining ? ExpantaNum.One :
                ExpantaNum.Clamp01(previousProgress + seconds / project.DurationSeconds);
            interiorCommit ??= CommitInteriorPayment;
            interiorRollback ??= RollbackInteriorPayment;
            interiorTransactionState = state;
            interiorPreviousProgress = previousProgress;
            interiorPreviousActive = previousActive;
            interiorNextProgress = next;
            if (resourceManager != null && resourceManager.TryApplyAtomicPayment(interiorPaymentBuffer,
                    interiorCommit, interiorRollback))
                advanced = true;
            else
                failure = SectorOperationFailure.InsufficientInteriorExplorationSupply;
        }
        return advanced;
    }

    private static void ValidateInteriorRestoredState(SectorDef definition, bool occupied,
        ExpantaNum progress, bool active, bool paused)
    {
        if (!progress.IsFinite || progress < ExpantaNum.Zero || progress > ExpantaNum.One ||
            ((progress > ExpantaNum.Zero || active || paused) && (!occupied || definition.InteriorExploration == null)) ||
            (active && paused) || ((active || paused) && progress >= ExpantaNum.One))
            throw new InvalidOperationException($"Invalid planetary exploration state: '{definition.Id}'.");
        if ((progress > ExpantaNum.Zero || active || paused) && definition.InteriorExploration != null &&
            GetMissingInteriorResearch(definition.InteriorExploration) != null)
            throw new InvalidOperationException($"Planetary exploration is missing required research: '{definition.Id}'.");
    }
}
