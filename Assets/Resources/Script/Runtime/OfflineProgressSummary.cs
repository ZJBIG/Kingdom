using System;
using System.Collections.Generic;

public readonly struct OfflineResourceChange
{
    public Resource Resource { get; }
    public ExpantaNum Before { get; }
    public ExpantaNum After { get; }
    public ExpantaNum Change => After - Before;
    internal OfflineResourceChange(Resource resource, ExpantaNum before, ExpantaNum after)
    { Resource = resource; Before = before; After = after; }
}

// Session evidence only: values are copied around the actual offline settlement.
public sealed class OfflineProgressSummary
{
    public double WallClockSeconds { get; }
    public double SettledSeconds { get; }
    public double EffectiveEconomySeconds => SimulationManager.CalculateOfflineEffectiveSeconds(0d, SettledSeconds);
    public ExpantaNum FoodBefore { get; }
    public ExpantaNum FoodAfter { get; }
    public ExpantaNum PopulationBefore { get; }
    public ExpantaNum PopulationAfter { get; }
    public bool FoodAtCapacity { get; }
    public IReadOnlyList<OfflineResourceChange> Resources { get; }
    public IReadOnlyList<Research> CompletedResearches { get; }
    public IReadOnlyList<SectorDefinition> OccupiedSectors { get; }
    public UltraProjectStage StageBefore { get; }
    public UltraProjectStage StageAfter { get; }
    public ExpantaNum StageProgressBefore { get; }
    public ExpantaNum StageProgressAfter { get; }
    public UltraProjectStatus ProjectStatusBefore { get; }
    public UltraProjectStatus ProjectStatusAfter { get; }
    public UltraProjectPauseReason ProjectPauseReason { get; }
    public Research WaitingResearch { get; }
    public bool ResearchPowerBlocked { get; }
    public IReadOnlyList<SectorDefinition> SupplyBlockedSectors { get; }

    public RelicStatus RelicStatusBefore { get; }
    public RelicStatus RelicStatusAfter { get; }
    public RelicPauseReason RelicPauseReasonBefore { get; }
    public RelicPauseReason RelicPauseReasonAfter { get; }
    public ExpantaNum RelicProgressBefore { get; }
    public ExpantaNum RelicProgressAfter { get; }
    public bool RelicCommissionActiveBefore { get; }
    public bool RelicCommissionActiveAfter { get; }
    public bool RelicSupportReadyBefore { get; }
    public bool RelicSupportReadyAfter { get; }
    public string RelicSupportedSectorIdBefore { get; }
    public string RelicSupportedSectorIdAfter { get; }
    public int RelicCompletedCommissionsBefore { get; }
    public int RelicCompletedCommissionsAfter { get; }

    internal sealed class Snapshot
    {
        internal readonly Dictionary<Resource, ExpantaNum> Resources = new();
        internal readonly HashSet<string> CompletedResearchIds = new(StringComparer.Ordinal);
        internal readonly HashSet<string> OccupiedSectorIds = new(StringComparer.Ordinal);
        internal RelicStatus RelicStatus;
        internal RelicPauseReason RelicPauseReason;
        internal ExpantaNum RelicProgress;
        internal bool RelicCommissionActive;
        internal bool RelicSupportReady;
        internal string RelicSupportedSectorId;
        internal int RelicCompletedCommissions;
        internal ExpantaNum Food, Population, Progress;
        internal UltraProjectStage Stage;
        internal UltraProjectStatus Status;
    }

    internal static Snapshot Capture()
    {
        GameManager game = GameManager.Instance;
        var snapshot = new Snapshot
        {
            RelicStatus = game.Relic.State.Status,
            RelicPauseReason = game.Relic.State.PauseReason,
            RelicProgress = game.Relic.State.Progress,
            RelicCommissionActive = game.Relic.State.CommissionActive,
            RelicSupportReady = game.Relic.State.SupportReady,
            RelicSupportedSectorId = game.Relic.State.SupportedSectorId,
            RelicCompletedCommissions = game.Relic.State.CompletedCommissions,
            Food = game.State.FoodAmount,
            Population = game.State.Population.Population,
            Stage = game.UltraProject.State.CurrentStage,
            Progress = game.UltraProject.State.StageProgress,
            Status = game.UltraProject.State.Status
        };
        foreach (var pair in ResourceManager.Instance.States)
            snapshot.Resources.Add(pair.Key, pair.Value.Amount);
        foreach (var pair in ResearchManager.Instance.States)
            if (pair.Value.Status == ResearchStatus.Completed)
                snapshot.CompletedResearchIds.Add(pair.Key.Id);
        foreach (SectorState state in game.Sectors.OrderedStates)
            if (state.Occupied) snapshot.OccupiedSectorIds.Add(state.Definition.Id);
        return snapshot;
    }

    internal OfflineProgressSummary(Snapshot before, Snapshot after, double elapsed, double settled)
    {
        RelicStatusBefore = before.RelicStatus; RelicStatusAfter = after.RelicStatus;
        RelicPauseReasonBefore = before.RelicPauseReason; RelicPauseReasonAfter = after.RelicPauseReason;
        RelicProgressBefore = before.RelicProgress; RelicProgressAfter = after.RelicProgress;
        RelicCommissionActiveBefore = before.RelicCommissionActive; RelicCommissionActiveAfter = after.RelicCommissionActive;
        RelicSupportReadyBefore = before.RelicSupportReady; RelicSupportReadyAfter = after.RelicSupportReady;
        RelicSupportedSectorIdBefore = before.RelicSupportedSectorId; RelicSupportedSectorIdAfter = after.RelicSupportedSectorId;
        RelicCompletedCommissionsBefore = before.RelicCompletedCommissions; RelicCompletedCommissionsAfter = after.RelicCompletedCommissions;
        WallClockSeconds = elapsed;
        SettledSeconds = settled;
        FoodBefore = before.Food; FoodAfter = after.Food;
        PopulationBefore = before.Population; PopulationAfter = after.Population;
        StageBefore = before.Stage; StageAfter = after.Stage;
        StageProgressBefore = before.Progress; StageProgressAfter = after.Progress;
        ProjectStatusBefore = before.Status; ProjectStatusAfter = after.Status;
        GameManager game = GameManager.Instance;
        FoodAtCapacity = game.State.FoodCapacity > ExpantaNum.Zero && after.Food >= game.State.FoodCapacity;
        ProjectPauseReason = game.UltraProject.State.PauseReason;
        var resources = new List<OfflineResourceChange>();
        foreach (var pair in after.Resources)
        {
            before.Resources.TryGetValue(pair.Key, out ExpantaNum previous);
            if (previous != pair.Value) resources.Add(new OfflineResourceChange(pair.Key, previous, pair.Value));
        }
        resources.Sort((a, b) => string.CompareOrdinal(a.Resource.Id, b.Resource.Id));
        Resources = resources.AsReadOnly();
        var researches = new List<Research>();
        ResearchManager research = ResearchManager.Instance;
        foreach (var pair in research.States)
            if (pair.Value.Status == ResearchStatus.Completed && !before.CompletedResearchIds.Contains(pair.Key.Id))
                researches.Add(pair.Key);
        researches.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        CompletedResearches = researches.AsReadOnly();
        foreach (ResearchState state in research.ResearchQueue)
            if (state.Status == ResearchStatus.WaitingResources) { WaitingResearch = state.Definition; break; }
        ResearchPowerBlocked = research.ActiveResearch != null && research.CurrentResearchSpeed <= ExpantaNum.Zero;
        var sectors = new List<SectorDefinition>();
        var blocked = new List<SectorDefinition>();
        foreach (SectorState state in game.Sectors.OrderedStates)
        {
            if (state.Occupied && !before.OccupiedSectorIds.Contains(state.Definition.Id)) sectors.Add(state.Definition);
            if ((state.ColonizationActive && !game.Sectors.GetExplorationPreview(state.Definition, game.State, ResourceManager.Instance).HasSupply) ||
                (state.CampaignActive && !game.Sectors.GetCampaignPreview(state.Definition, game.State, ResourceManager.Instance).HasSupply))
                blocked.Add(state.Definition);
        }
        OccupiedSectors = sectors.AsReadOnly();
        SupplyBlockedSectors = blocked.AsReadOnly();
    }
}
