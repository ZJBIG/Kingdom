using System;

[Serializable]
public sealed class SectorState
{
    private bool unlocked;
    private bool occupied;
    private ExpantaNum campaignProgress;
    private int visitCount;

    public SectorDefinition Definition { get; }
    public bool Unlocked => unlocked;
    public bool Occupied => occupied;
    public ExpantaNum CampaignProgress => campaignProgress;
    public int VisitCount => visitCount;
    public int Version { get; private set; }

    public SectorState(SectorDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        campaignProgress = ExpantaNum.Zero;
    }

    internal void SetUnlocked(bool value) => Change(ref unlocked, value);
    internal void SetOccupied(bool value) => Change(ref occupied, value);
    internal void SetCampaignProgress(ExpantaNum value) =>
        Change(ref campaignProgress, ExpantaNum.Clamp01(value));
    internal void SetVisitCount(int value) => Change(ref visitCount, Math.Max(0, value));

#if UNITY_EDITOR
    public void SetUnlockedForEditor(bool value) => SetUnlocked(value);
    public void SetOccupiedForEditor(bool value) => SetOccupied(value);
    public void SetCampaignProgressForEditor(ExpantaNum value) => SetCampaignProgress(value);
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
