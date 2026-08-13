using System;

[Serializable]
public sealed class MilitaryState
{
    private ExpantaNum attackPower;
    private ExpantaNum defensePower;
    private ExpantaNum fleetPower;
    private ExpantaNum militaryManpower;
    private ExpantaNum supplySatisfaction;

    public ExpantaNum AttackPower => attackPower;
    public ExpantaNum DefensePower => defensePower;
    public ExpantaNum FleetPower => fleetPower;
    public ExpantaNum MilitaryManpower => militaryManpower;
    public ExpantaNum SupplySatisfaction => supplySatisfaction;
    public int Version { get; private set; }

    public MilitaryState() => InitializeNew();

    internal void InitializeNew()
    {
        attackPower = ExpantaNum.Zero;
        defensePower = ExpantaNum.Zero;
        fleetPower = ExpantaNum.Zero;
        militaryManpower = ExpantaNum.Zero;
        supplySatisfaction = ExpantaNum.One;
        Version++;
    }

    internal void ResetDerived()
    {
        attackPower = ExpantaNum.Zero;
        defensePower = ExpantaNum.Zero;
        fleetPower = ExpantaNum.Zero;
        militaryManpower = ExpantaNum.Zero;
        supplySatisfaction = ExpantaNum.One;
        Version++;
    }

    internal bool AdjustAttackPower(ExpantaNum delta) => AdjustNonNegative(ref attackPower, delta);
    internal bool AdjustDefensePower(ExpantaNum delta) => AdjustNonNegative(ref defensePower, delta);
    internal bool AdjustFleetPower(ExpantaNum delta) => AdjustNonNegative(ref fleetPower, delta);
    internal bool AdjustMilitaryManpower(ExpantaNum delta) => AdjustNonNegative(ref militaryManpower, delta);

    internal void SetSupplySatisfaction(ExpantaNum value)
    {
        if (!value.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(value));
        ExpantaNum normalized = ExpantaNum.Clamp01(value);
        if (supplySatisfaction == normalized)
            return;
        supplySatisfaction = normalized;
        Version++;
    }

    internal void Restore(
        ExpantaNum restoredAttackPower,
        ExpantaNum restoredDefensePower,
        ExpantaNum restoredFleetPower,
        ExpantaNum restoredMilitaryManpower,
        ExpantaNum restoredSupplySatisfaction)
    {
        if (!restoredSupplySatisfaction.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(restoredSupplySatisfaction));
        if (restoredSupplySatisfaction < ExpantaNum.Zero || restoredSupplySatisfaction > ExpantaNum.One)
            throw new ArgumentOutOfRangeException(nameof(restoredSupplySatisfaction));
        attackPower = NormalizeFiniteNonNegative(restoredAttackPower, nameof(restoredAttackPower));
        defensePower = NormalizeFiniteNonNegative(restoredDefensePower, nameof(restoredDefensePower));
        fleetPower = NormalizeFiniteNonNegative(restoredFleetPower, nameof(restoredFleetPower));
        militaryManpower = NormalizeFiniteNonNegative(restoredMilitaryManpower, nameof(restoredMilitaryManpower));
        supplySatisfaction = restoredSupplySatisfaction;
        Version++;
    }

    private bool AdjustNonNegative(ref ExpantaNum field, ExpantaNum delta)
    {
        if (!delta.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(delta));
        ExpantaNum next = ExpantaNum.Max(ExpantaNum.Zero, field + delta);
        if (field == next)
            return false;
        field = next;
        Version++;
        return true;
    }

    private static ExpantaNum NormalizeFiniteNonNegative(ExpantaNum value, string parameterName)
    {
        if (!value.IsFinite)
            throw new ArgumentOutOfRangeException(parameterName);
        return ExpantaNum.Max(ExpantaNum.Zero, value);
    }
}
