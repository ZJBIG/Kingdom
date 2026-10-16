using System;
using System.IO;

public enum WorldEventKind { None, Fire, ProductiveDay, Aurora }

/// <summary>Online-only town dispatches; durations use simulation seconds.</summary>
public sealed class WorldEventState
{
    private static readonly ExpantaNum FireProductionMultiplier = new ExpantaNum(.8d);
    private static readonly ExpantaNum ProductiveDayMultiplier = new ExpantaNum(1.15d);
    public const double EventIntervalSeconds = 900d;
    public const double EffectDurationSeconds = 120d;
    public int Sequence { get; private set; }
    public WorldEventKind LastKind => KindForSequence(Sequence);
    private static WorldEventKind KindForSequence(int sequence) => sequence == 0
        ? WorldEventKind.None : ((sequence - 1) % 3) switch
        {
            0 => WorldEventKind.ProductiveDay,
            1 => WorldEventKind.Aurora,
            _ => WorldEventKind.Fire
        };
    public WorldEventKind ActiveKind => RemainingSeconds > 0d ? LastKind : WorldEventKind.None;
    public double RemainingSeconds { get; private set; }
    public double SecondsUntilNextEvent { get; private set; } = EventIntervalSeconds;
    public int PopulationLost { get; private set; }
    public ExpantaNum ProductionMultiplier => ActiveKind == WorldEventKind.Fire
        ? FireProductionMultiplier : ActiveKind == WorldEventKind.ProductiveDay
            ? ProductiveDayMultiplier : ExpantaNum.One;

    internal double NextBoundary(bool online) => Math.Min(
        RemainingSeconds > 0d ? RemainingSeconds : double.PositiveInfinity,
        online ? SecondsUntilNextEvent : double.PositiveInfinity);

    internal void Advance(double seconds, bool online)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (seconds > NextBoundary(online))
            throw new ArgumentOutOfRangeException(nameof(seconds), "Advance to the next event boundary first.");
        RemainingSeconds = Math.Max(0d, RemainingSeconds - seconds);
        if (!online || seconds == 0d)
            return;
        SecondsUntilNextEvent = Math.Max(0d, SecondsUntilNextEvent - seconds);
        if (SecondsUntilNextEvent > 0d)
            return;
        // Wrap after a complete three-event cycle; the token still changes at every dispatch.
        Sequence = Sequence == int.MaxValue - 1 ? 1 : Sequence + 1;
        PopulationLost = 0;
        RemainingSeconds = LastKind == WorldEventKind.Aurora ? 0d : EffectDurationSeconds;
        SecondsUntilNextEvent = EventIntervalSeconds;
    }

    internal void RecordPopulationLoss(int value) => PopulationLost = value;

    public SaveManager.WorldEventSaveData CaptureSaveData() => new SaveManager.WorldEventSaveData
    {
        Sequence = Sequence,
        RemainingSeconds = RemainingSeconds,
        SecondsUntilNextEvent = SecondsUntilNextEvent,
        PopulationLost = PopulationLost
    };

    internal void Restore(SaveManager.WorldEventSaveData data)
    {
        Validate(data);
        Sequence = data.Sequence;
        RemainingSeconds = data.RemainingSeconds;
        SecondsUntilNextEvent = data.SecondsUntilNextEvent;
        PopulationLost = data.PopulationLost;
    }

    internal static void Validate(SaveManager.WorldEventSaveData data)
    {
        if (data == null || data.Sequence < 0 || data.Sequence == int.MaxValue ||
            !FiniteRange(data.RemainingSeconds, 0d, EffectDurationSeconds) ||
            !FiniteRange(data.SecondsUntilNextEvent, double.Epsilon, EventIntervalSeconds) ||
            data.PopulationLost < 0 || data.PopulationLost > 1)
            throw new InvalidDataException("Invalid town event save state.");
        WorldEventKind kind = KindForSequence(data.Sequence);
        if (((kind == WorldEventKind.None || kind == WorldEventKind.Aurora) && data.RemainingSeconds > 0d) ||
            (kind != WorldEventKind.Fire && data.PopulationLost != 0) ||
            data.RemainingSeconds > data.SecondsUntilNextEvent)
            throw new InvalidDataException("Inconsistent town event save state.");
    }

    private static bool FiniteRange(double value, double minimum, double maximum) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum && value <= maximum;

#if UNITY_EDITOR
    public void AdvanceForEditor(double seconds, bool online) => Advance(seconds, online);
    public double NextBoundaryForEditor(bool online) => NextBoundary(online);
    public void RestoreForEditor(SaveManager.WorldEventSaveData data) => Restore(data);
#endif
}
