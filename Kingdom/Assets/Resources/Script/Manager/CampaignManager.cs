using System;

public static class CampaignManager
{
    private static readonly ExpantaNum MinimumAdvanceRatio = new ExpantaNum(0.7d);
    private static readonly ExpantaNum FullAdvanceRatio = new ExpantaNum(2d);

    public static ExpantaNum CalculateEffectivePower(
        ExpantaNum attackPower,
        ExpantaNum fleetPower,
        ExpantaNum militaryManpower,
        ExpantaNum supplySatisfaction,
        ExpantaNum militaryMultiplier)
    {
        ExpantaNum basePower = ExpantaNum.Max(ExpantaNum.Zero, attackPower) +
            ExpantaNum.Max(ExpantaNum.Zero, fleetPower);
        if (basePower <= ExpantaNum.Zero)
            return ExpantaNum.Zero;

        ExpantaNum manpowerFactor = ExpantaNum.Clamp01(
            ExpantaNum.Max(ExpantaNum.Zero, militaryManpower) / basePower);
        return basePower * manpowerFactor * ExpantaNum.Clamp01(supplySatisfaction) *
            ExpantaNum.Max(ExpantaNum.Zero, militaryMultiplier);
    }

    public static ExpantaNum CalculateCombatRatio(ExpantaNum effectivePlayerPower, ExpantaNum enemyPower)
    {
        ExpantaNum normalizedEnemyPower = ExpantaNum.Max(ExpantaNum.Zero, enemyPower);
        if (normalizedEnemyPower <= ExpantaNum.Zero)
            return ExpantaNum.One;
        return ExpantaNum.Max(ExpantaNum.Zero, effectivePlayerPower) / normalizedEnemyPower;
    }

    public static ExpantaNum CalculateProgressRate(ExpantaNum combatRatio)
    {
        ExpantaNum ratio = ExpantaNum.Max(ExpantaNum.Zero, combatRatio);
        if (ratio < MinimumAdvanceRatio)
            return ExpantaNum.Zero;
        if (ratio < ExpantaNum.One)
            return (ratio - MinimumAdvanceRatio) / new ExpantaNum(0.3d) * new ExpantaNum(0.25d);
        if (ratio < FullAdvanceRatio)
            return new ExpantaNum(0.25d) + (ratio - ExpantaNum.One) * new ExpantaNum(0.75d);

        return ExpantaNum.One + (ratio - FullAdvanceRatio) / (ratio + FullAdvanceRatio);
    }

    public static ExpantaNum CalculateCasualtyRate(ExpantaNum combatRatio)
    {
        ExpantaNum ratio = ExpantaNum.Max(ExpantaNum.Zero, combatRatio);
        if (ratio < MinimumAdvanceRatio)
            return ExpantaNum.One;
        if (ratio < ExpantaNum.One)
            return (ExpantaNum.One - ratio) / new ExpantaNum(0.3d);
        return ExpantaNum.Zero;
    }

    public static ExpantaNum AdvanceProgress(
        ExpantaNum currentProgress,
        ExpantaNum combatRatio,
        double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        ExpantaNum progress = ExpantaNum.Clamp01(currentProgress);
        return ExpantaNum.Clamp01(
            progress + CalculateProgressRate(combatRatio) * deltaSeconds / 60d);
    }
}
