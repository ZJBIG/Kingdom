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
        if (!restoredTotal.IsFinite || restoredTotal < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(restoredTotal));
        if (used > restoredTotal)
            throw new InvalidOperationException(
                "Territory used cannot exceed the restored territory total.");
        total = restoredTotal;
        Version++;
    }

    internal void AddTotal(ExpantaNum delta)
    {
        if (!delta.IsFinite || delta < ExpantaNum.Zero)
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

#if UNITY_EDITOR
    public void AdjustUsedForEditor(ExpantaNum delta) => AdjustUsed(delta);
#endif

    internal void AdjustUsed(ExpantaNum delta)
    {
        if (!delta.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(delta));
        ExpantaNum next = ExpantaNum.Clamp(used + delta, ExpantaNum.Zero, total);
        if (used == next)
            return;
        used = next;
        Version++;
    }

    internal void RestoreUsed(ExpantaNum restoredUsed)
    {
        if (!restoredUsed.IsFinite || restoredUsed < ExpantaNum.Zero || restoredUsed > total)
            throw new ArgumentOutOfRangeException(nameof(restoredUsed));
        if (used == restoredUsed)
            return;
        used = restoredUsed;
        Version++;
    }
}
