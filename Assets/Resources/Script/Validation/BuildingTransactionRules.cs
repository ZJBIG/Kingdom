using System;

#if !ECONOMY_SIMULATOR
public static class BuildingTransactionRules
{
    public static bool TryNormalizePositiveWhole(string input, out ExpantaNum amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        try
        {
            amount = ExpantaNum.Parse(input).Floor();
            return amount >= 1;
        }
        catch (FormatException)
        {
            amount = 0;
            return false;
        }
        catch (OverflowException)
        {
            amount = 0;
            return false;
        }
    }

    public static ExpantaNum ClampToAvailable(ExpantaNum requested, ExpantaNum available)
    {
        ExpantaNum wholeRequested = requested.Floor();
        ExpantaNum wholeAvailable = ExpantaNum.Max(ExpantaNum.Zero, available.Floor());
        return ExpantaNum.Min(ExpantaNum.Max(ExpantaNum.Zero, wholeRequested), wholeAvailable);
    }

    public static ExpantaNum Total(ExpantaNum perUnit, ExpantaNum amount)
    {
        return perUnit * ExpantaNum.Max(ExpantaNum.Zero, amount.Floor());
    }
}
#endif

/// <summary>
/// Scalar reference formulas shared with the standalone economy simulator.
/// Runtime authority remains in State and Manager classes; this type only
/// provides deterministic parity calculations for values representable as doubles.
/// </summary>
public static class EconomySimulationParity
{
    public static double AdvanceStockpile(
        double current,
        double productionRate,
        double consumptionRate,
        double deltaSeconds)
    {
        ValidateDelta(deltaSeconds);
        return Math.Max(0d, current +
            (productionRate - consumptionRate) * deltaSeconds);
    }

    public static double CalculateSatisfaction(
        double currentInventory,
        double potentialProductionRate,
        double potentialConsumptionRate,
        double deltaSeconds)
    {
        ValidateDelta(deltaSeconds);
        double demand = Math.Max(0d, potentialConsumptionRate) * deltaSeconds;
        if (demand <= 0d)
            return 1d;
        double available = Math.Max(0d, currentInventory) +
            Math.Max(0d, potentialProductionRate) * deltaSeconds;
        return Math.Clamp(available / demand, 0d, 1d);
    }

    public static double CalculateFlowSatisfaction(
        double potentialProductionRate,
        double potentialConsumptionRate)
    {
        double demand = Math.Max(0d, potentialConsumptionRate);
        return demand <= 0d
            ? 1d
            : Math.Clamp(Math.Max(0d, potentialProductionRate) / demand, 0d, 1d);
    }

    public static double CalculateEffectiveEfficiency(
        double global,
        double resource,
        double food,
        double power,
        double logistics) => Math.Clamp(
        Clamp01(global) * Clamp01(resource) * Clamp01(food) *
        Clamp01(power) * Clamp01(logistics), 0d, 1d);

    public static double ResearchSpeedEffect(int currentTech, int targetTech) =>
        currentTech == targetTech
            ? 1d
            : 1d / (Math.Abs(targetTech - currentTech) + .5d);

    private static double Clamp01(double value) => Math.Clamp(value, 0d, 1d);

    private static void ValidateDelta(double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
    }
}
