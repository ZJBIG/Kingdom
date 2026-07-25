using System;

[Serializable]
public sealed class TerritoryState
{
    private ExpantaNum total;
    private ExpantaNum used;

    public ExpantaNum TerritoryTotal => total;
    public ExpantaNum TerritoryUsed => used;
    public ExpantaNum AvailableTerritory => ExpantaNum.Max(ExpantaNum.Zero, total - used);
    public int Version { get; private set; }

    public TerritoryState() => InitializeNew();

    internal void InitializeNew()
    {
        total = new ExpantaNum(100);
        used = ExpantaNum.Zero;
        Version++;
    }

    internal void RestoreTotal(ExpantaNum restoredTotal)
    {
        total = ExpantaNum.Max(ExpantaNum.Zero, restoredTotal);
        used = ExpantaNum.Min(used, total);
        Version++;
    }

    internal void ResetDerived(ExpantaNum minimumTotal)
    {
        total = ExpantaNum.Max(total, ExpantaNum.Max(ExpantaNum.Zero, minimumTotal));
        used = ExpantaNum.Zero;
        Version++;
    }

    internal void AdjustUsed(ExpantaNum delta)
    {
        used = ExpantaNum.Clamp(used + delta, ExpantaNum.Zero, total);
        Version++;
    }
}
