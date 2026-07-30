using System;

[Serializable]
public sealed class PopulationState
{
    public static ExpantaNum FoodConsumptionPerPerson => ExpantaNum.One;
    public static ExpantaNum BaseGrowthRatePerSecond => new ExpantaNum(1d / 60d);
    private const double SecondsPerDeparture = 60d;
    private static readonly ExpantaNum PopulationStepEpsilon =
        new ExpantaNum(1e-9d);
    private static readonly ExpantaNum PopulationRemainderEpsilon =
        new ExpantaNum(1e-3d);

    private ExpantaNum population;
    private ExpantaNum populationCapacity;
    private ExpantaNum populationChangeProgress;

    public ExpantaNum Population => population;
    public ExpantaNum PopulationCapacity => populationCapacity;
    public ExpantaNum PopulationChangeProgress => populationChangeProgress;
    public int Version { get; private set; }

    public PopulationState() => InitializeNew();

    internal void InitializeNew()
    {
        population = ExpantaNum.Zero;
        populationCapacity = ExpantaNum.Zero;
        populationChangeProgress = ExpantaNum.Zero;
        Version++;
    }

    internal void RestorePopulation(ExpantaNum restoredPopulation)
    {
        population = NormalizeWhole(restoredPopulation);
        populationChangeProgress = ExpantaNum.Zero;
        Version++;
    }

    internal void RestorePopulationChangeProgress(ExpantaNum restoredProgress)
    {
        ExpantaNum next = population == populationCapacity
            ? ExpantaNum.Zero
            : NormalizeProgress(restoredProgress);
        if (populationChangeProgress == next)
            return;
        populationChangeProgress = next;
        Version++;
    }

    internal void AdjustPopulationCapacity(ExpantaNum delta)
    {
        int previousRelation = ComparePopulationToCapacity();
        ExpantaNum next = NormalizeWhole(populationCapacity + delta);
        if (populationCapacity == next)
            return;
        populationCapacity = next;
        if (previousRelation != ComparePopulationToCapacity())
            populationChangeProgress = ExpantaNum.Zero;
        Version++;
    }

    internal void ResetDerivedCapacity()
    {
        ExpantaNum resetCapacity = ExpantaNum.Zero;
        if (populationCapacity == resetCapacity)
            return;
        populationCapacity = resetCapacity;
        populationChangeProgress = ExpantaNum.Zero;
        Version++;
    }

    internal void AdvancePopulation(
        double deltaSeconds,
        ExpantaNum foodSatisfaction,
        ExpantaNum growthRatePerSecond,
        ExpantaNum departureAllowance)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (population == populationCapacity)
        {
            SetPopulationChangeProgress(ExpantaNum.Zero);
            return;
        }

        if (population < populationCapacity)
        {
            AdvanceGrowth(deltaSeconds, foodSatisfaction, growthRatePerSecond);
            return;
        }

        AdvanceDeparture(deltaSeconds, departureAllowance);
    }

    private static ExpantaNum NormalizeWhole(ExpantaNum value) =>
        ExpantaNum.Max(ExpantaNum.Zero, value).Floor();

    private static ExpantaNum NormalizeProgress(ExpantaNum value)
    {
        ExpantaNum nonNegative = ExpantaNum.Max(ExpantaNum.Zero, value);
        return nonNegative - nonNegative.Floor();
    }

    private int ComparePopulationToCapacity()
    {
        if (population < populationCapacity)
            return -1;
        return population > populationCapacity ? 1 : 0;
    }

    private void AdvanceGrowth(
        double deltaSeconds,
        ExpantaNum foodSatisfaction,
        ExpantaNum growthRatePerSecond)
    {
        ExpantaNum satisfaction = ExpantaNum.Clamp01(foodSatisfaction);
        ExpantaNum growthRate = ExpantaNum.Max(
            ExpantaNum.Zero,
            growthRatePerSecond);
        if (satisfaction <= ExpantaNum.Zero || growthRate <= ExpantaNum.Zero)
            return;

        double growthMultiplier =
            (growthRate / BaseGrowthRatePerSecond).ToDouble();
        if (!double.IsFinite(growthMultiplier) || growthMultiplier <= 0d)
            return;
        double secondsPerPopulation = 60d / growthMultiplier;
        ExpantaNum accumulated = populationChangeProgress +
            satisfaction * deltaSeconds / secondsPerPopulation;
        ExpantaNum possibleBirths =
            (accumulated + PopulationStepEpsilon).Floor();
        ExpantaNum births = ExpantaNum.Min(
            populationCapacity - population,
            possibleBirths);
        if (births > ExpantaNum.Zero)
            SetPopulation(population + births);

        ExpantaNum remainingProgress = accumulated - births;
        if (births > ExpantaNum.Zero &&
            remainingProgress <= PopulationRemainderEpsilon)
        {
            remainingProgress = ExpantaNum.Zero;
        }
        SetPopulationChangeProgress(
            population >= populationCapacity
                ? ExpantaNum.Zero
                : remainingProgress);
    }

    private void AdvanceDeparture(
        double deltaSeconds,
        ExpantaNum departureAllowance)
    {
        ExpantaNum safeDepartures = NormalizeWhole(departureAllowance);
        if (safeDepartures < ExpantaNum.One)
        {
            SetPopulationChangeProgress(ExpantaNum.Zero);
            return;
        }

        ExpantaNum accumulated = populationChangeProgress +
            deltaSeconds / SecondsPerDeparture;
        ExpantaNum departures = ExpantaNum.Min(
            ExpantaNum.Min(population - populationCapacity, safeDepartures),
            (accumulated + PopulationStepEpsilon).Floor());
        if (departures > ExpantaNum.Zero)
            SetPopulation(population - departures);

        bool blockedByCapacity = population <= populationCapacity;
        bool blockedByProductivity =
            departures >= safeDepartures && population > populationCapacity;
        SetPopulationChangeProgress(
            blockedByCapacity || blockedByProductivity
                ? ExpantaNum.Zero
                : accumulated - departures);
    }

    private void SetPopulation(ExpantaNum value)
    {
        ExpantaNum next = NormalizeWhole(value);
        if (population == next)
            return;
        population = next;
        Version++;
    }

    private void SetPopulationChangeProgress(ExpantaNum value)
    {
        ExpantaNum next = NormalizeProgress(value);
        if (populationChangeProgress == next)
            return;
        populationChangeProgress = next;
        Version++;
    }
}
