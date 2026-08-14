/// <summary>
/// Central rules for the happiness-derived global development bonus.
/// </summary>
public static class HappinessFormula
{
    public static readonly ExpantaNum MaximumBonus = new ExpantaNum(0.5);
    public static readonly ExpantaNum HalfSaturationScore = ExpantaNum.One;

    public static ExpantaNum CalculateSurplusPerPerson(
        ExpantaNum foodNetRate,
        ExpantaNum population)
    {
        ExpantaNum surplus = ExpantaNum.Max(ExpantaNum.Zero, foodNetRate);
        ExpantaNum safePopulation = ExpantaNum.Max(ExpantaNum.One, population);
        return surplus / safePopulation;
    }

    public static ExpantaNum CalculateScore(ExpantaNum surplusPerPerson)
    {
        ExpantaNum safeSurplus = ExpantaNum.Max(ExpantaNum.Zero, surplusPerPerson);
        return (ExpantaNum.One + safeSurplus).Log10();
    }

    public static ExpantaNum CalculateFoodAvailability(
        ExpantaNum currentInventory,
        ExpantaNum potentialProductionRate,
        ExpantaNum potentialConsumptionRate,
        double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new System.ArgumentOutOfRangeException(nameof(deltaSeconds));

        ExpantaNum available = ExpantaNum.Max(ExpantaNum.Zero, currentInventory) +
            ExpantaNum.Max(ExpantaNum.Zero, potentialProductionRate) * deltaSeconds;
        ExpantaNum demand = ExpantaNum.Max(ExpantaNum.Zero, potentialConsumptionRate) * deltaSeconds;
        if (demand <= ExpantaNum.Zero)
            return ExpantaNum.One;

        return ExpantaNum.Clamp01(available / demand);
    }

    /// <summary>
    /// Applies the food-shortage constraint first, then a logarithmic score
    /// followed by a bounded saturation curve for food surplus. The result is
    /// monotonic and remains in [0, 1 + MaximumBonus].
    /// </summary>
    public static ExpantaNum CalculateMultiplier(
        ExpantaNum foodNetRate,
        ExpantaNum population) =>
        CalculateMultiplier(foodNetRate, population, ExpantaNum.One);

    public static ExpantaNum CalculateMultiplier(
        ExpantaNum foodNetRate,
        ExpantaNum population,
        ExpantaNum foodAvailability)
        => CalculateMultiplier(
            foodNetRate,
            population,
            foodAvailability,
            ExpantaNum.Zero);

    public static ExpantaNum CalculateMultiplier(
        ExpantaNum foodNetRate,
        ExpantaNum population,
        ExpantaNum foodAvailability,
        ExpantaNum happinessBonus)
    {
        ExpantaNum availability = ExpantaNum.Clamp01(foodAvailability);
        if (availability < ExpantaNum.One)
            return availability;

        ExpantaNum score = CalculateScore(
            CalculateSurplusPerPerson(foodNetRate, population));
        if (score.IsNaN || score < ExpantaNum.Zero)
            return ExpantaNum.One;

        ExpantaNum saturation = score / (score + HalfSaturationScore);
        ExpantaNum baseMultiplier =
            ExpantaNum.One + MaximumBonus * ExpantaNum.Clamp01(saturation);
        ExpantaNum safeBonus = happinessBonus.IsNaN || happinessBonus.IsInfinity
            ? ExpantaNum.Zero
            : ExpantaNum.Max(ExpantaNum.Zero, happinessBonus);
        return ExpantaNum.Min(
            ExpantaNum.One + MaximumBonus,
            baseMultiplier + safeBonus);
    }

    public static ExpantaNum CalculateConstraintMultiplier(ExpantaNum multiplier) =>
        ExpantaNum.Min(ExpantaNum.One, ExpantaNum.Max(ExpantaNum.Zero, multiplier));

    public static ExpantaNum CalculateRewardMultiplier(ExpantaNum multiplier) =>
        ExpantaNum.Max(ExpantaNum.One, multiplier);
}
