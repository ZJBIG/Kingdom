using System;

[Serializable]
public sealed class SectorState
{
    private bool unlocked;
    private bool occupied;
    private bool colonizationActive;
    private bool campaignActive;
    private ExpantaNum campaignProgress;
    private ExpantaNum campaignCasualties;
    private ExpantaNum campaignCombatRatio;
    private int visitCount;

    public SectorDefinition Definition { get; }
    public bool Unlocked => unlocked;
    public bool Occupied => occupied;
    public bool ColonizationActive => colonizationActive;
    public bool CampaignActive => campaignActive;
    public ExpantaNum CampaignProgress => campaignProgress;
    public ExpantaNum CampaignCasualties => campaignCasualties;
    public ExpantaNum CampaignCombatRatio => campaignCombatRatio;
    public int VisitCount => visitCount;
    public int Version { get; private set; }

    public SectorState(SectorDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        campaignProgress = ExpantaNum.Zero;
    }

    internal void SetUnlocked(bool value) => Change(ref unlocked, value);
    internal void SetOccupied(bool value) => Change(ref occupied, value);
    internal void SetColonizationActive(bool value) => Change(ref colonizationActive, value);
    internal void SetCampaignActive(bool value) => Change(ref campaignActive, value);
    internal void SetCampaignProgress(ExpantaNum value) =>
        Change(ref campaignProgress, ExpantaNum.Clamp01(value));
    internal void SetCampaignCasualties(ExpantaNum value) =>
        Change(ref campaignCasualties, ExpantaNum.Max(ExpantaNum.Zero, value));
    internal void SetCampaignCombatRatio(ExpantaNum value) =>
        Change(ref campaignCombatRatio, ExpantaNum.Max(ExpantaNum.Zero, value));
    internal void SetVisitCount(int value) => Change(ref visitCount, Math.Max(0, value));

    internal void ResetForLoad()
    {
        SetUnlocked(false);
        SetOccupied(false);
        SetColonizationActive(false);
        SetCampaignActive(false);
        SetCampaignProgress(ExpantaNum.Zero);
        SetCampaignCasualties(ExpantaNum.Zero);
        SetCampaignCombatRatio(ExpantaNum.Zero);
        SetVisitCount(0);
    }

    internal void Restore(
        bool restoredUnlocked,
        bool restoredOccupied,
        bool restoredColonizationActive,
        bool restoredCampaignActive,
        ExpantaNum progress,
        ExpantaNum casualties,
        ExpantaNum combatRatio,
        int visits)
    {
        ValidateRestoredValues(
            restoredUnlocked,
            restoredOccupied,
            restoredColonizationActive,
            restoredCampaignActive,
            progress,
            casualties,
            combatRatio,
            visits);
        SetUnlocked(restoredUnlocked);
        SetOccupied(restoredOccupied);
        SetColonizationActive(restoredColonizationActive);
        SetCampaignActive(restoredCampaignActive);
        SetCampaignProgress(progress);
        SetCampaignCasualties(casualties);
        SetCampaignCombatRatio(combatRatio);
        SetVisitCount(visits);
    }

    internal void RestoreExact(
        bool restoredUnlocked,
        bool restoredOccupied,
        bool restoredColonizationActive,
        bool restoredCampaignActive,
        ExpantaNum progress,
        ExpantaNum casualties,
        ExpantaNum combatRatio,
        int visits)
    {
        SetUnlocked(restoredUnlocked);
        SetOccupied(restoredOccupied);
        SetColonizationActive(restoredColonizationActive);
        SetCampaignActive(restoredCampaignActive);
        SetCampaignProgress(progress);
        SetCampaignCasualties(casualties);
        SetCampaignCombatRatio(combatRatio);
        SetVisitCount(visits);
    }

    private static void ValidateRestoredValues(
        bool restoredUnlocked,
        bool restoredOccupied,
        bool restoredColonizationActive,
        bool restoredCampaignActive,
        ExpantaNum progress,
        ExpantaNum casualties,
        ExpantaNum combatRatio,
        int visits)
    {
        if (!progress.IsFinite || progress < ExpantaNum.Zero || progress > ExpantaNum.One)
            throw new ArgumentOutOfRangeException(nameof(progress));
        if (!casualties.IsFinite || casualties < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(casualties));
        if (!combatRatio.IsFinite || combatRatio < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(combatRatio));
        if (visits < 0)
            throw new ArgumentOutOfRangeException(nameof(visits));
        if (restoredOccupied && !restoredUnlocked)
            throw new InvalidOperationException("A sector cannot be occupied before it is unlocked.");
        if (restoredColonizationActive && (!restoredUnlocked || restoredOccupied))
            throw new InvalidOperationException("A sector cannot colonize while locked or occupied.");
        if (restoredCampaignActive && (!restoredUnlocked || restoredOccupied))
            throw new InvalidOperationException("A sector cannot campaign while locked or occupied.");
        if (restoredColonizationActive && restoredCampaignActive)
            throw new InvalidOperationException("A sector cannot colonize and campaign simultaneously.");
    }

#if UNITY_EDITOR
    public void SetUnlockedForEditor(bool value) => SetUnlocked(value);
    public void SetOccupiedForEditor(bool value) => SetOccupied(value);
    public void SetColonizationActiveForEditor(bool value) => SetColonizationActive(value);
    public void SetCampaignProgressForEditor(ExpantaNum value) => SetCampaignProgress(value);
    public void SetCampaignActiveForEditor(bool value) => SetCampaignActive(value);
    public void SetCampaignCasualtiesForEditor(ExpantaNum value) => SetCampaignCasualties(value);
    public void SetVisitCountForEditor(int value) => SetVisitCount(value);
#endif

    private void Change(ref bool field, bool value)
    {
        if (field == value)
            return;
        field = value;
        Version++;
    }

    private void Change(ref int field, int value)
    {
        if (field == value)
            return;
        field = value;
        Version++;
    }

    private void Change(ref ExpantaNum field, ExpantaNum value)
    {
        if (field == value)
            return;
        field = value;
        Version++;
    }
}
