using System;

[Serializable]
public sealed class PopulationState
{
    public const double DefaultWorkforcePerPerson = 1d;
    public const double DefaultFoodPerPerson = 1d;

    private ExpantaNum population;
    private ExpantaNum populationCapacity;
    private ExpantaNum assignedMilitary;
    private ExpantaNum assignedBuildingWorkforce;
    private ExpantaNum growthProgress;

    public ExpantaNum Population => population;
    public ExpantaNum PopulationCapacity => populationCapacity;
    public ExpantaNum AssignedMilitary => assignedMilitary;
    public ExpantaNum TotalWorkforce =>
        population * new ExpantaNum(DefaultWorkforcePerPerson);
    public ExpantaNum AssignedBuildingWorkforce => assignedBuildingWorkforce;
    public ExpantaNum AvailableWorkforce =>
        ExpantaNum.Max(
            ExpantaNum.Zero,
            TotalWorkforce - assignedMilitary - assignedBuildingWorkforce);
    public ExpantaNum GrowthProgress => growthProgress;
    public ExpantaNum FoodPerPerson { get; private set; }
    public int Version { get; private set; }

    public PopulationState() => InitializeNew();

    internal void InitializeNew()
    {
        population = new ExpantaNum(15);
        populationCapacity = new ExpantaNum(20);
        assignedMilitary = ExpantaNum.Zero;
        assignedBuildingWorkforce = ExpantaNum.Zero;
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
        assignedBuildingWorkforce = ExpantaNum.Zero;
        growthProgress = ExpantaNum.Clamp01(restoredGrowthProgress);
        FoodPerPerson = ExpantaNum.Max(ExpantaNum.Zero, restoredFoodPerPerson);
        Version++;
    }

    internal void AdjustPopulationCapacity(ExpantaNum delta)
    {
        ExpantaNum next = ExpantaNum.Max(population, populationCapacity + delta);
        if (populationCapacity == next)
            return;
        populationCapacity = next;
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

    internal void ResetDerivedWorkforce()
    {
        if (assignedBuildingWorkforce == ExpantaNum.Zero)
            return;
        assignedBuildingWorkforce = ExpantaNum.Zero;
        Version++;
    }

    internal void AdjustBuildingWorkforce(ExpantaNum delta)
    {
        ExpantaNum next = ExpantaNum.Max(
            ExpantaNum.Zero,
            assignedBuildingWorkforce + delta);
        if (assignedBuildingWorkforce == next)
            return;
        assignedBuildingWorkforce = next;
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
