using System;

[Serializable]
public sealed class PopulationState
{
    public const double DefaultWorkforcePerPerson = 1d;
    public const double DefaultFoodPerPerson = 1d;

    private ExpantaNum population;
    private ExpantaNum populationCapacity;
    private ExpantaNum assignedMilitary;
    private ExpantaNum growthProgress;

    public ExpantaNum Population => population;
    public ExpantaNum PopulationCapacity => populationCapacity;
    public ExpantaNum AssignedMilitary => assignedMilitary;
    public ExpantaNum AvailableWorkforce =>
        ExpantaNum.Max(ExpantaNum.Zero, population - assignedMilitary) *
        new ExpantaNum(DefaultWorkforcePerPerson);
    public ExpantaNum GrowthProgress => growthProgress;
    public ExpantaNum FoodPerPerson { get; private set; }
    public int Version { get; private set; }

    public PopulationState() => InitializeNew();

    internal void InitializeNew()
    {
        population = new ExpantaNum(15);
        populationCapacity = new ExpantaNum(20);
        assignedMilitary = ExpantaNum.Zero;
        growthProgress = ExpantaNum.Zero;
        FoodPerPerson = new ExpantaNum(DefaultFoodPerPerson);
        Version++;
    }

    internal void Restore(
        ExpantaNum restoredPopulation,
        ExpantaNum restoredPopulationCapacity,
        ExpantaNum restoredAssignedMilitary,
        ExpantaNum restoredGrowthProgress,
        ExpantaNum restoredFoodPerPerson)
    {
        population = NormalizeWhole(restoredPopulation);
        populationCapacity = ExpantaNum.Max(population, NormalizeWhole(restoredPopulationCapacity));
        assignedMilitary = ExpantaNum.Min(population, NormalizeWhole(restoredAssignedMilitary));
        growthProgress = ExpantaNum.Clamp01(restoredGrowthProgress);
        FoodPerPerson = ExpantaNum.Max(ExpantaNum.Zero, restoredFoodPerPerson);
        Version++;
    }

    internal void AdjustPopulationCapacity(ExpantaNum delta)
    {
        populationCapacity = ExpantaNum.Max(population, populationCapacity + delta);
        Version++;
    }

    internal void ResetDerivedCapacity()
    {
        ExpantaNum resetCapacity = ExpantaNum.Max(population, new ExpantaNum(20));
        if (populationCapacity == resetCapacity)
            return;
        populationCapacity = resetCapacity;
        Version++;
    }

    internal void SetAssignedMilitary(ExpantaNum value)
    {
        ExpantaNum normalized = ExpantaNum.Min(population, NormalizeWhole(value));
        if (assignedMilitary == normalized)
            return;
        assignedMilitary = normalized;
        Version++;
    }

    internal void AdvanceGrowth(double deltaSeconds, ExpantaNum foodSatisfaction)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (population >= populationCapacity || foodSatisfaction <= ExpantaNum.Zero)
            return;

        growthProgress = ExpantaNum.Clamp01(
            growthProgress + ExpantaNum.Clamp01(foodSatisfaction) * deltaSeconds / 60d);
        if (growthProgress < ExpantaNum.One)
            return;

        population += ExpantaNum.One;
        growthProgress = ExpantaNum.Zero;
        Version++;
    }

    private static ExpantaNum NormalizeWhole(ExpantaNum value) =>
        ExpantaNum.Max(ExpantaNum.Zero, value).Floor();
}
