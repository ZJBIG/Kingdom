using System;

[Serializable]
public sealed class TerritoryState
{
    public static ExpantaNum InitialTotal => new ExpantaNum(500);

    private ExpantaNum total;
    private ExpantaNum used;

    public ExpantaNum TerritoryTotal => total;
    public ExpantaNum TerritoryUsed => used;
    public ExpantaNum AvailableTerritory => ExpantaNum.Max(ExpantaNum.Zero, total - used);
    public int Version { get; private set; }

    public TerritoryState() => InitializeNew();

    internal void InitializeNew()
    {
        total = InitialTotal;
        used = ExpantaNum.Zero;
        Version++;
    }

    internal void RestoreTotal(ExpantaNum restoredTotal)
    {
        total = ExpantaNum.Max(ExpantaNum.Zero, restoredTotal);
        used = ExpantaNum.Min(used, total);
        Version++;
    }

    internal void AddTotal(ExpantaNum delta)
    {
        if (delta.IsNaN || delta < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta), "领土奖励必须是非负数。");
        if (delta == ExpantaNum.Zero)
            return;
        total += delta;
        Version++;
    }

    internal void ResetDerived(ExpantaNum minimumTotal)
    {
        ExpantaNum nextTotal = ExpantaNum.Max(total, ExpantaNum.Max(ExpantaNum.Zero, minimumTotal));
        if (total == nextTotal && used == ExpantaNum.Zero)
            return;
        total = nextTotal;
        used = ExpantaNum.Zero;
        Version++;
    }

    internal void AdjustUsed(ExpantaNum delta)
    {
        ExpantaNum next = ExpantaNum.Clamp(used + delta, ExpantaNum.Zero, total);
        if (used == next)
            return;
        used = next;
        Version++;
    }
}
