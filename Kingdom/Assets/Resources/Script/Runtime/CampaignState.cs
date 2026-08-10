using System;

[Serializable]
public sealed class CampaignState
{
    public bool Active { get; private set; }
    public string TargetSectorId { get; private set; }
    public ExpantaNum Casualties { get; private set; }
    public ExpantaNum CombatRatio { get; private set; }
    public int Version { get; private set; }

    public CampaignState() => InitializeNew();

    internal void InitializeNew()
    {
        Active = false;
        TargetSectorId = string.Empty;
        Casualties = ExpantaNum.Zero;
        CombatRatio = ExpantaNum.Zero;
        Version++;
    }

    internal void Begin(string sectorId)
    {
        if (string.IsNullOrWhiteSpace(sectorId))
            throw new ArgumentException("Campaign target ID is required.", nameof(sectorId));
        if (Active && string.Equals(TargetSectorId, sectorId, StringComparison.OrdinalIgnoreCase))
            return;
        Active = true;
        TargetSectorId = sectorId;
        Casualties = ExpantaNum.Zero;
        CombatRatio = ExpantaNum.Zero;
        Version++;
    }

    internal void RecordCombat(ExpantaNum combatRatio, ExpantaNum casualties)
    {
        ExpantaNum normalizedRatio = ExpantaNum.Max(ExpantaNum.Zero, combatRatio);
        ExpantaNum normalizedCasualties = ExpantaNum.Max(ExpantaNum.Zero, casualties);
        if (CombatRatio == normalizedRatio && normalizedCasualties == ExpantaNum.Zero)
            return;
        CombatRatio = normalizedRatio;
        Casualties += normalizedCasualties;
        Version++;
    }

    internal ExpantaNum Repair(ExpantaNum amount)
    {
        ExpantaNum requested = ExpantaNum.Max(ExpantaNum.Zero, amount);
        ExpantaNum repaired = ExpantaNum.Min(requested, Casualties);
        if (repaired <= ExpantaNum.Zero)
            return ExpantaNum.Zero;
        Casualties -= repaired;
        Version++;
        return repaired;
    }

    internal void Complete()
    {
        if (!Active && string.IsNullOrEmpty(TargetSectorId))
            return;
        Active = false;
        TargetSectorId = string.Empty;
        Casualties = ExpantaNum.Zero;
        CombatRatio = ExpantaNum.Zero;
        Version++;
    }

    internal void ResetForLoad() => InitializeNew();

    internal void Restore(
        bool active,
        string targetSectorId,
        ExpantaNum casualties,
        ExpantaNum combatRatio)
    {
        if (!active || string.IsNullOrWhiteSpace(targetSectorId))
        {
            ResetForLoad();
            return;
        }

        Active = true;
        TargetSectorId = targetSectorId;
        Casualties = ExpantaNum.Max(ExpantaNum.Zero, casualties);
        CombatRatio = ExpantaNum.Max(ExpantaNum.Zero, combatRatio);
        Version++;
    }
}
