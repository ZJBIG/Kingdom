using System;

[Serializable]
public sealed class BuildingState
{
    private ExpantaNum amount;
    private ExpantaNum efficiency = ExpantaNum.One;
    private ExpantaNum autoBuildProgress;
    private bool autoBuild;

    public Building Definition { get; }
    public ExpantaNum Amount => amount;
    public ExpantaNum Efficiency => efficiency;
    public ExpantaNum AutoBuildProgress => autoBuildProgress;
    public bool AutoBuild => autoBuild;
    public int Version { get; private set; }

    public ExpantaNum AutoBuildWorkRequired => Definition.AutoBuildWorkRequired;
    public ExpantaNum SpaceCost => Definition.SpaceCost;
    public ExpantaNum BuildEffort => Definition.BuildEffort;
    public ExpantaNum ProductivityGranted => Definition.ProductivityGranted;

    public BuildingState(Building definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    internal void SetAmount(ExpantaNum value) => Change(ref amount, ExpantaNum.Max(ExpantaNum.Zero, value));
    internal void SetEfficiency(ExpantaNum value) => Change(ref efficiency, ExpantaNum.Clamp01(value));
    internal void SetAutoBuild(bool value) => Change(ref autoBuild, value);
    internal void AddAutoBuildProgress(ExpantaNum value) =>
        Change(ref autoBuildProgress, ExpantaNum.Max(ExpantaNum.Zero, autoBuildProgress + value));
    internal void SpendAutoBuildProgress(ExpantaNum value) =>
        Change(ref autoBuildProgress, ExpantaNum.Max(ExpantaNum.Zero, autoBuildProgress - value));

    internal void ResetForLoad()
    {
        SetAmount(ExpantaNum.Zero);
        SetEfficiency(ExpantaNum.One);
        SetAutoBuild(false);
        Change(ref autoBuildProgress, ExpantaNum.Zero);
    }

    internal void Restore(ExpantaNum restoredAmount, bool restoredAutoBuild, ExpantaNum restoredProgress)
    {
        SetAmount(restoredAmount);
        SetAutoBuild(restoredAutoBuild);
        Change(ref autoBuildProgress, ExpantaNum.Max(ExpantaNum.Zero, restoredProgress));
    }

    private void Change(ref ExpantaNum field, ExpantaNum value)
    {
        if (field == value)
            return;
        field = value;
        Version++;
    }

    private void Change(ref bool field, bool value)
    {
        if (field == value)
            return;
        field = value;
        Version++;
    }
}
