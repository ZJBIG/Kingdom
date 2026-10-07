using System;

[Serializable]
public sealed class BuildingState
{
    private ExpantaNum amount;
    private ExpantaNum efficiency = ExpantaNum.One;

    public Building Definition { get; }
    public ExpantaNum Amount => amount;
    public ExpantaNum Efficiency => efficiency;
    public int Version { get; private set; }

    public ExpantaNum SpaceCost => Definition.SpaceCost;
    public ExpantaNum ProductivityConsumption => Definition.ProductivityConsumption;
    public ExpantaNum ProductivityGranted => Definition.ProductivityGranted;
    public ExpantaNum PopulationCapacityGranted => Definition.PopulationCapacityGranted;
    public ExpantaNum FoodCapacityGranted => Definition.FoodCapacityGranted;

    public BuildingState(Building definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    internal void SetAmount(ExpantaNum value) => Change(ref amount, NormalizeFiniteNonNegative(value, nameof(value)));
    internal void SetEfficiency(ExpantaNum value)
    {
        if (!value.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(value));
        Change(ref efficiency, ExpantaNum.Clamp01(value));
    }

#if UNITY_EDITOR
    public void SetAmountForEditor(ExpantaNum value) => SetAmount(value);
    public void SetEfficiencyForEditor(ExpantaNum value) => SetEfficiency(value);
#endif

    internal void ResetForLoad()
    {
        SetAmount(ExpantaNum.Zero);
        SetEfficiency(ExpantaNum.One);
    }

    internal void Restore(ExpantaNum restoredAmount)
    {
        SetAmount(restoredAmount);
    }

    private void Change(ref ExpantaNum field, ExpantaNum value)
    {
        if (field == value)
            return;
        field = value;
        Version++;
    }

    private static ExpantaNum NormalizeFiniteNonNegative(ExpantaNum value, string parameterName)
    {
        if (!value.IsFinite)
            throw new ArgumentOutOfRangeException(parameterName);
        return ExpantaNum.Max(ExpantaNum.Zero, value);
    }
}
