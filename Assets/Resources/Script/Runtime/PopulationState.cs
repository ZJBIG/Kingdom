using System;

[Serializable]
public sealed class PopulationState
{
    private const double SecondsPerDeparture = 60d;
    private const double SecondsPerFoodShortageDeparture = 3600d;
    private static readonly ExpantaNum FoodConsumptionPerPersonValue = new ExpantaNum(0.8d);
    private static readonly ExpantaNum ProductivityGrantedPerPersonValue = new ExpantaNum(2d);
    private static readonly ExpantaNum BaseGrowthRatePerSecondValue = new ExpantaNum(1d / 60d);
    private static readonly ExpantaNum Eight = new ExpantaNum(8d);
    private static readonly ExpantaNum DepartureRatePerSecond = new ExpantaNum(1d / SecondsPerDeparture);
    private static readonly ExpantaNum FoodShortageDepartureSeconds = new ExpantaNum(SecondsPerFoodShortageDeparture);
    private static readonly ExpantaNum PopulationStepEpsilon =
        new ExpantaNum(1e-9d);
    private static readonly ExpantaNum PopulationRemainderEpsilon =
        new ExpantaNum(1e-3d);

    public static ExpantaNum FoodConsumptionPerPerson => FoodConsumptionPerPersonValue;
    public static ExpantaNum ProductivityGrantedPerPerson => ProductivityGrantedPerPersonValue;
    public static ExpantaNum BaseGrowthRatePerSecond => BaseGrowthRatePerSecondValue;

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

#if UNITY_EDITOR
    public void RestorePopulationForEditor(ExpantaNum restoredPopulation) =>
        RestorePopulation(restoredPopulation);
    public void RestorePopulationChangeProgressForEditor(ExpantaNum restoredProgress) =>
        RestorePopulationChangeProgress(restoredProgress);
    public void AdjustPopulationCapacityForEditor(ExpantaNum delta) =>
        AdjustPopulationCapacity(delta);
    public void AdvancePopulationForEditor(
        double deltaSeconds,
        ExpantaNum happinessMultiplier,
        ExpantaNum growthRatePerSecond,
        ExpantaNum departureAllowance,
        bool foodShortageDepartureAllowed) =>
        AdvancePopulation(
            deltaSeconds,
            happinessMultiplier,
            growthRatePerSecond,
            departureAllowance,
            foodShortageDepartureAllowed);
    public ExpantaNum CurrentGrowthRatePerSecondForEditor(
        ExpantaNum happinessMultiplier,
        ExpantaNum growthRatePerSecond) =>
        CurrentGrowthRatePerSecond(happinessMultiplier, growthRatePerSecond);
#endif

    internal void RestorePopulation(ExpantaNum restoredPopulation)
    {
        EnsureFinite(restoredPopulation, nameof(restoredPopulation));
        if (restoredPopulation < ExpantaNum.Zero ||
            restoredPopulation != restoredPopulation.Floor())
            throw new ArgumentOutOfRangeException(nameof(restoredPopulation));
        population = restoredPopulation;
        populationChangeProgress = ExpantaNum.Zero;
        Version++;
    }

    internal void RestorePopulationChangeProgress(ExpantaNum restoredProgress)
    {
        EnsureFinite(restoredProgress, nameof(restoredProgress));
        if (restoredProgress < ExpantaNum.Zero || restoredProgress > ExpantaNum.One)
            throw new ArgumentOutOfRangeException(nameof(restoredProgress));
        ExpantaNum next = population == populationCapacity
            ? ExpantaNum.Zero
            : restoredProgress;
        if (populationChangeProgress == next)
            return;
        populationChangeProgress = next;
        Version++;
    }

    internal void AdjustPopulationCapacity(ExpantaNum delta)
    {
        EnsureFinite(delta, nameof(delta));
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

    internal void RestoreCapacityExact(
        ExpantaNum restoredCapacity,
        ExpantaNum restoredProgress)
    {
        EnsureFinite(restoredCapacity, nameof(restoredCapacity));
        EnsureFinite(restoredProgress, nameof(restoredProgress));
        ExpantaNum nextCapacity = NormalizeWhole(restoredCapacity);
        ExpantaNum nextProgress = population == nextCapacity
            ? ExpantaNum.Zero
            : NormalizeProgress(restoredProgress);
        if (populationCapacity == nextCapacity && populationChangeProgress == nextProgress)
            return;
        populationCapacity = nextCapacity;
        populationChangeProgress = nextProgress;
        Version++;
    }

    internal void AdvancePopulation(
        double deltaSeconds,
        ExpantaNum happinessMultiplier,
        ExpantaNum growthRatePerSecond,
        ExpantaNum departureAllowance,
        bool foodShortageDepartureAllowed)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        EnsureFinite(happinessMultiplier, nameof(happinessMultiplier));
        EnsureFinite(growthRatePerSecond, nameof(growthRatePerSecond));
        EnsureFinite(departureAllowance, nameof(departureAllowance));
        if (foodShortageDepartureAllowed)
        {
            AdvanceFoodShortageDeparture(deltaSeconds, happinessMultiplier);
            return;
        }

        if (population == populationCapacity)
        {
            SetPopulationChangeProgress(ExpantaNum.Zero);
            return;
        }

        if (population < populationCapacity)
        {
            AdvanceGrowth(deltaSeconds, happinessMultiplier, growthRatePerSecond);
            return;
        }

        SetPopulationChangeProgress(ExpantaNum.Zero);
    }

    internal ExpantaNum CurrentGrowthRatePerSecond(
        ExpantaNum happinessMultiplier,
        ExpantaNum growthRatePerSecond)
    {
        EnsureFinite(happinessMultiplier, nameof(happinessMultiplier));
        EnsureFinite(growthRatePerSecond, nameof(growthRatePerSecond));
        if (population >= populationCapacity)
            return ExpantaNum.Zero;
        ExpantaNum satisfaction = ExpantaNum.Max(ExpantaNum.Zero, happinessMultiplier);
        if (satisfaction <= ExpantaNum.Zero)
            return ExpantaNum.Zero;
        return satisfaction * CalculateLogisticGrowthRate(growthRatePerSecond);
    }

    internal ExpantaNum CurrentDepartureRatePerSecond(
        ExpantaNum departureAllowance,
        ExpantaNum happinessMultiplier,
        bool foodShortageDepartureAllowed)
    {
        EnsureFinite(happinessMultiplier, nameof(happinessMultiplier));
        if (foodShortageDepartureAllowed)
            return CalculateFoodShortageDepartureRate(happinessMultiplier);
        return ExpantaNum.Zero;
    }

    private static ExpantaNum NormalizeWhole(ExpantaNum value) =>
        ExpantaNum.Max(ExpantaNum.Zero, value).Floor();

    private static void EnsureFinite(ExpantaNum value, string parameterName)
    {
        if (!value.IsFinite)
            throw new ArgumentOutOfRangeException(parameterName);
    }

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
        ExpantaNum happinessMultiplier,
        ExpantaNum growthRatePerSecond)
    {
        ExpantaNum satisfaction = ExpantaNum.Max(ExpantaNum.Zero, happinessMultiplier);
        ExpantaNum growthRate = ExpantaNum.Max(
            ExpantaNum.Zero,
            growthRatePerSecond);
        if (satisfaction <= ExpantaNum.Zero || growthRate <= ExpantaNum.Zero)
            return;

        ExpantaNum effectiveGrowthRate = CalculateLogisticGrowthRate(growthRate);
        ExpantaNum accumulated = populationChangeProgress +
            satisfaction * deltaSeconds * effectiveGrowthRate;
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

    private void AdvanceFoodShortageDeparture(
        double deltaSeconds,
        ExpantaNum happinessMultiplier)
    {
        if (population <= ExpantaNum.Zero)
        {
            SetPopulation(ExpantaNum.Zero);
            SetPopulationChangeProgress(ExpantaNum.Zero);
            return;
        }

        ExpantaNum accumulated = populationChangeProgress +
            CalculateFoodShortageDepartureRate(happinessMultiplier) * deltaSeconds;
        ExpantaNum departures = ExpantaNum.Min(
            population,
            (accumulated + PopulationStepEpsilon).Floor());
        if (departures > ExpantaNum.Zero)
            SetPopulation(population - departures);

        SetPopulationChangeProgress(
            population <= ExpantaNum.Zero
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

    private ExpantaNum CalculateLogisticGrowthRate(ExpantaNum growthRatePerSecond)
    {
        ExpantaNum logisticFactor = ExpantaNum.One -
            ExpantaNum.Clamp01(population / populationCapacity);
        ExpantaNum effectivePopulation = ExpantaNum.Max(
            ExpantaNum.One,
            population);
        return ExpantaNum.Max(ExpantaNum.Zero, growthRatePerSecond) *
            effectivePopulation *
            ExpantaNum.Max(ExpantaNum.Zero, logisticFactor);
    }

    private ExpantaNum CalculateFoodShortageDepartureRate(
        ExpantaNum happinessMultiplier)
    {
        if (population <= ExpantaNum.Zero)
            return ExpantaNum.Zero;
        ExpantaNum shortage = ExpantaNum.Clamp01(
            ExpantaNum.One - happinessMultiplier);
        return shortage * ExpantaNum.Max(ExpantaNum.One, population) /
            FoodShortageDepartureSeconds;
    }
}
