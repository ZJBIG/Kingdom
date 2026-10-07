using System;

public enum CampaignDoctrine
{
    Stable = 0,
    Surge = 1
}

[Serializable]
public sealed class CampaignState
{
    public bool Active { get; private set; }
    public string TargetSectorId { get; private set; }
    public ExpantaNum Casualties { get; private set; }
    public ExpantaNum CombatRatio { get; private set; }
    public CampaignDoctrine Doctrine { get; private set; }
    public int Version { get; private set; }

    public CampaignState() => InitializeNew();

    internal void InitializeNew()
    {
        Active = false;
        TargetSectorId = string.Empty;
        Casualties = ExpantaNum.Zero;
        CombatRatio = ExpantaNum.Zero;
        Doctrine = CampaignDoctrine.Stable;
        Version++;
    }

    internal void Begin(string sectorId)
    {
        Begin(sectorId, CampaignDoctrine.Stable);
    }

    internal void Begin(string sectorId, CampaignDoctrine doctrine)
    {
        if (string.IsNullOrWhiteSpace(sectorId))
            throw new ArgumentException("远征目标星区编号不能为空。", nameof(sectorId));
        if (Active && string.Equals(TargetSectorId, sectorId, StringComparison.OrdinalIgnoreCase))
        {
            SetDoctrine(doctrine);
            return;
        }
        Active = true;
        TargetSectorId = sectorId;
        Casualties = ExpantaNum.Zero;
        CombatRatio = ExpantaNum.Zero;
        SetDoctrine(doctrine);
        Version++;
    }

    internal void SetDoctrine(CampaignDoctrine doctrine)
    {
        if (!Enum.IsDefined(typeof(CampaignDoctrine), doctrine))
            throw new ArgumentOutOfRangeException(nameof(doctrine));
        if (Doctrine == doctrine)
            return;
        Doctrine = doctrine;
        Version++;
    }

    internal void RecordCombat(ExpantaNum combatRatio, ExpantaNum casualties)
    {
        if (!combatRatio.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(combatRatio));
        if (!casualties.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(casualties));
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
        if (!amount.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(amount));
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

    internal void Cancel()
    {
        if (!Active && string.IsNullOrEmpty(TargetSectorId))
            return;
        Active = false;
        if (Casualties <= ExpantaNum.Zero)
        {
            TargetSectorId = string.Empty;
            CombatRatio = ExpantaNum.Zero;
        }
        Version++;
    }

    internal void ResetForLoad() => InitializeNew();

    internal void Restore(
        bool active,
        string targetSectorId,
        ExpantaNum casualties,
        ExpantaNum combatRatio)
    {
        Restore(active, targetSectorId, casualties, combatRatio, CampaignDoctrine.Stable);
    }

    internal void Restore(
        bool active,
        string targetSectorId,
        ExpantaNum casualties,
        ExpantaNum combatRatio,
        CampaignDoctrine doctrine)
    {
        // A cancelled campaign can retain casualties and its target so the
        // player can repair the fleet before resuming. Do not reactivate it
        // merely because the save contains those repairable casualties.
        if (!casualties.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(casualties));
        if (!combatRatio.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(combatRatio));
        if (casualties < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(casualties));
        if (combatRatio < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(combatRatio));
        if (!Enum.IsDefined(typeof(CampaignDoctrine), doctrine))
            throw new ArgumentOutOfRangeException(nameof(doctrine));
        if (active && string.IsNullOrWhiteSpace(targetSectorId))
            throw new InvalidOperationException("An active campaign must have a target sector.");
        if (string.IsNullOrWhiteSpace(targetSectorId) &&
            (casualties > ExpantaNum.Zero || combatRatio > ExpantaNum.Zero))
            throw new InvalidOperationException("A campaign with combat history must retain its target sector.");
        if (string.IsNullOrWhiteSpace(targetSectorId))
        {
            ResetForLoad();
            return;
        }
        Active = active;
        TargetSectorId = targetSectorId;
        Casualties = casualties;
        CombatRatio = combatRatio;
        Doctrine = doctrine;
        Version++;
    }

    internal void RestoreExact(
        bool active,
        string targetSectorId,
        ExpantaNum casualties,
        ExpantaNum combatRatio)
    {
        RestoreExact(active, targetSectorId, casualties, combatRatio, CampaignDoctrine.Stable);
    }

    internal void RestoreExact(
        bool active,
        string targetSectorId,
        ExpantaNum casualties,
        ExpantaNum combatRatio,
        CampaignDoctrine doctrine)
    {
        if (!casualties.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(casualties));
        if (!combatRatio.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(combatRatio));
        if (!Enum.IsDefined(typeof(CampaignDoctrine), doctrine))
            throw new ArgumentOutOfRangeException(nameof(doctrine));
        Active = active;
        TargetSectorId = targetSectorId ?? string.Empty;
        Casualties = ExpantaNum.Max(ExpantaNum.Zero, casualties);
        CombatRatio = ExpantaNum.Max(ExpantaNum.Zero, combatRatio);
        Doctrine = doctrine;
        Version++;
    }

#if UNITY_EDITOR
    public void BeginForEditor(string sectorId) => Begin(sectorId);
    public void RecordCombatForEditor(
        ExpantaNum combatRatio,
        ExpantaNum casualties) =>
        RecordCombat(combatRatio, casualties);
    public void SetDoctrineForEditor(CampaignDoctrine doctrine) => SetDoctrine(doctrine);
#endif
}
