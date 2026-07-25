using System;

[Serializable]
public sealed class ResourceState
{
    private ExpantaNum amount;
    private ExpantaNum productionRate;
    private ExpantaNum consumptionRate;
    private ExpantaNum efficiency;
    private ExpantaNum tickPotentialProductionRate;
    private ExpantaNum tickPotentialConsumptionRate;
    private ExpantaNum tickSatisfaction = ExpantaNum.One;
    private bool hasTickSatisfaction;

    public Resource Definition { get; }
    public ExpantaNum Amount => amount;
    public ExpantaNum ProductionRate => productionRate;
    public ExpantaNum ConsumptionRate => consumptionRate;
    public ExpantaNum Efficiency => efficiency;
    public int Version { get; private set; }

    public ResourceState(Resource definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        amount = ExpantaNum.Zero;
        productionRate = ExpantaNum.Zero;
        consumptionRate = ExpantaNum.Zero;
        efficiency = ExpantaNum.One;
        tickPotentialProductionRate = ExpantaNum.Zero;
        tickPotentialConsumptionRate = ExpantaNum.Zero;
    }

    internal void SetAmount(ExpantaNum value) => Change(ref amount, ExpantaNum.Max(ExpantaNum.Zero, value));
    internal void SetProductionRate(ExpantaNum value) => Change(ref productionRate, ExpantaNum.Max(ExpantaNum.Zero, value));
    internal void SetConsumptionRate(ExpantaNum value) => Change(ref consumptionRate, ExpantaNum.Max(ExpantaNum.Zero, value));
    internal void SetEfficiency(ExpantaNum value) => Change(ref efficiency, ExpantaNum.Clamp01(value));

    internal void ResetForLoad()
    {
        SetAmount(ExpantaNum.Zero);
        SetProductionRate(ExpantaNum.Zero);
        SetConsumptionRate(ExpantaNum.Zero);
        SetEfficiency(ExpantaNum.One);
        BeginTick();
    }

    internal void BeginTick()
    {
        tickPotentialProductionRate = productionRate;
        tickPotentialConsumptionRate = consumptionRate;
        tickSatisfaction = ExpantaNum.One;
        hasTickSatisfaction = false;
    }

    internal void AdjustTickPotentialProductionRate(ExpantaNum delta) =>
        tickPotentialProductionRate = ExpantaNum.Max(ExpantaNum.Zero, tickPotentialProductionRate + delta);

    internal void AdjustTickPotentialConsumptionRate(ExpantaNum delta) =>
        tickPotentialConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, tickPotentialConsumptionRate + delta);

    internal void CalculateTickSatisfaction(double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        tickSatisfaction = ResourceManager.CalculateSatisfaction(
            amount,
            tickPotentialProductionRate,
            tickPotentialConsumptionRate,
            deltaSeconds);
        hasTickSatisfaction = true;
    }

    internal ExpantaNum GetTickSatisfactionOrFallback()
    {
        if (hasTickSatisfaction)
            return tickSatisfaction;
        if (consumptionRate <= ExpantaNum.Zero)
            return ExpantaNum.One;
        return ExpantaNum.Clamp01(productionRate / consumptionRate);
    }

    private void Change(ref ExpantaNum field, ExpantaNum value)
    {
        if (field == value)
            return;

        field = value;
        Version++;
    }
}
