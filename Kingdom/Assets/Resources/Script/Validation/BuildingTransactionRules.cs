using System;

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
