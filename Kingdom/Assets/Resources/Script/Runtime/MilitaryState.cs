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
        ExpantaNum normalized = ExpantaNum.Clamp01(value);
        if (supplySatisfaction == normalized)
            return;
        supplySatisfaction = normalized;
        Version++;
    }

    private bool AdjustNonNegative(ref ExpantaNum field, ExpantaNum delta)
    {
        ExpantaNum next = ExpantaNum.Max(ExpantaNum.Zero, field + delta);
        if (field == next)
            return false;
        field = next;
        Version++;
        return true;
    }
}
