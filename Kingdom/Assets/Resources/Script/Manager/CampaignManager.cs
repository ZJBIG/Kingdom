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
        => CalculateEffectivePower(
            attackPower,
            fleetPower,
            militaryManpower,
            supplySatisfaction,
            ExpantaNum.One,
            ExpantaNum.One,
            militaryMultiplier,
            ExpantaNum.Zero);

    public static ExpantaNum CalculateEffectivePower(
        ExpantaNum attackPower,
        ExpantaNum fleetPower,
        ExpantaNum militaryManpower,
        ExpantaNum supplySatisfaction,
        ExpantaNum powerSatisfaction,
        ExpantaNum logisticsSatisfaction,
        ExpantaNum militaryMultiplier)
        => CalculateEffectivePower(
            attackPower,
            fleetPower,
            militaryManpower,
            supplySatisfaction,
            powerSatisfaction,
            logisticsSatisfaction,
            militaryMultiplier,
            ExpantaNum.Zero);

    public static ExpantaNum CalculateEffectivePower(
        ExpantaNum attackPower,
        ExpantaNum fleetPower,
        ExpantaNum militaryManpower,
        ExpantaNum supplySatisfaction,
        ExpantaNum powerSatisfaction,
        ExpantaNum logisticsSatisfaction,
        ExpantaNum militaryMultiplier,
        ExpantaNum casualties)
    {
        ExpantaNum basePower = ExpantaNum.Max(ExpantaNum.Zero, attackPower) +
            ExpantaNum.Max(ExpantaNum.Zero, fleetPower);
        if (basePower <= ExpantaNum.Zero)
            return ExpantaNum.Zero;

        ExpantaNum manpowerFactor = ExpantaNum.Clamp01(
            ExpantaNum.Max(ExpantaNum.Zero, militaryManpower) / basePower);
        ExpantaNum readiness = CalculateFleetReadiness(fleetPower, casualties);
        return basePower * manpowerFactor * readiness * ExpantaNum.Clamp01(supplySatisfaction) *
            ExpantaNum.Clamp01(powerSatisfaction) * ExpantaNum.Clamp01(logisticsSatisfaction) *
            ExpantaNum.Max(ExpantaNum.Zero, militaryMultiplier);
    }

    public static ExpantaNum CalculateFleetReadiness(
        ExpantaNum fleetPower,
        ExpantaNum casualties)
    {
        ExpantaNum safeFleetPower = ExpantaNum.Max(ExpantaNum.One, fleetPower);
        ExpantaNum safeCasualties = ExpantaNum.Max(ExpantaNum.Zero, casualties);
        return ExpantaNum.One / (ExpantaNum.One + safeCasualties / safeFleetPower);
    }

    public static ExpantaNum CalculateCombatRatio(ExpantaNum effectivePlayerPower, ExpantaNum enemyPower)
    {
        ExpantaNum normalizedEnemyPower = ExpantaNum.Max(ExpantaNum.Zero, enemyPower);
        if (normalizedEnemyPower <= ExpantaNum.Zero)
            return ExpantaNum.One;
        return ExpantaNum.Max(ExpantaNum.Zero, effectivePlayerPower) / normalizedEnemyPower;
    }

    public static ExpantaNum CalculateFleetSurvivalFactor(
        ExpantaNum defensePower,
        ExpantaNum enemyPower)
    {
        ExpantaNum normalizedEnemyPower = ExpantaNum.Max(ExpantaNum.One, enemyPower);
        ExpantaNum defenseRatio = ExpantaNum.Clamp01(
            ExpantaNum.Max(ExpantaNum.Zero, defensePower) / normalizedEnemyPower);
        return new ExpantaNum(0.35d) + defenseRatio * new ExpantaNum(0.65d);
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
        => AdvanceProgress(currentProgress, combatRatio, deltaSeconds, ExpantaNum.One);

    public static ExpantaNum AdvanceProgress(
        ExpantaNum currentProgress,
        ExpantaNum combatRatio,
        double deltaSeconds,
        ExpantaNum progressMultiplier)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        ExpantaNum progress = ExpantaNum.Clamp01(currentProgress);
        return ExpantaNum.Clamp01(
            progress + CalculateProgressRate(combatRatio) *
            ExpantaNum.Clamp01(progressMultiplier) * deltaSeconds / 60d);
    }

    public static ExpantaNum CalculateCasualtyAmount(ExpantaNum combatRatio, double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        return CalculateCasualtyRate(combatRatio) * deltaSeconds / 60d;
    }

    public static ExpantaNum CalculateCasualtyAmount(
        ExpantaNum combatRatio,
        ExpantaNum defensePower,
        ExpantaNum enemyPower,
        double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        ExpantaNum survivalFactor = CalculateFleetSurvivalFactor(defensePower, enemyPower);
        return CalculateCasualtyRate(combatRatio) / survivalFactor * deltaSeconds / 60d;
    }
}
