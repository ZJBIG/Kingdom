using System;

[Serializable]
public sealed class PopulationState
{
    public static ExpantaNum FoodConsumptionPerPerson => ExpantaNum.One;

    private ExpantaNum population;
    private ExpantaNum populationCapacity;
    private ExpantaNum growthProgress;

    public ExpantaNum Population => population;
    public ExpantaNum PopulationCapacity => populationCapacity;
    public ExpantaNum GrowthProgress => growthProgress;
    public int Version { get; private set; }

    public PopulationState() => InitializeNew();

    internal void InitializeNew()
    {
        population = ExpantaNum.Zero;
        populationCapacity = ExpantaNum.Zero;
        growthProgress = ExpantaNum.Zero;
        Version++;
    }

    internal void Restore(
        ExpantaNum restoredPopulation,
        ExpantaNum restoredPopulationCapacity,
        ExpantaNum restoredGrowthProgress)
    {
        population = NormalizeWhole(restoredPopulation);
        populationCapacity = ExpantaNum.Max(population, NormalizeWhole(restoredPopulationCapacity));
        growthProgress = ExpantaNum.Clamp01(restoredGrowthProgress);
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
        ExpantaNum resetCapacity = ExpantaNum.Max(population, ExpantaNum.Zero);
        if (populationCapacity == resetCapacity)
            return;
        populationCapacity = resetCapacity;
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
