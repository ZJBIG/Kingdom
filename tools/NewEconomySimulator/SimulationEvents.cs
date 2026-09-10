using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Kingdom.NewEconomySimulator;

public enum SimulationMode
{
    Realtime,
    Offline
}

public enum SimulationEventKind
{
    TickStarted,
    TickCompleted,
    FoodChanged,
    ResourceChanged,
    BuildingChanged,
    ResearchChanged,
    WorkshopChanged,
    SectorChanged,
    CampaignChanged,
    Diagnostic
}

public sealed record SimulationEvent(
    long Sequence,
    long Tick,
    double ElapsedSeconds,
    SimulationMode Mode,
    SimulationEventKind Kind,
    string Id,
    string Detail,
    ExpantaNum Amount);

public static class SimulationSystems
{
    public const string Building = "Building";
    public const string Game = "Game";
    public const string OccupiedSector = "Sector.Occupied";
    public const string Resource = "Resource";
    public const string Colonization = "Sector.Colonization";
    public const string Campaign = "Sector.Campaign";
    public const string Research = "Research";
    public const string Story = "Story";

    public static readonly string[] OrderedSystems =
    [
        Building,
        Game,
        OccupiedSector,
        Resource,
        Colonization,
        Campaign,
        Research,
        Story
    ];
}

public sealed class SimulationEventLog
{
    private readonly List<SimulationEvent> events = new();
    private readonly ReadOnlyCollection<SimulationEvent> readOnlyEvents;

    public SimulationEventLog() => readOnlyEvents = events.AsReadOnly();

    public IReadOnlyList<SimulationEvent> Events => readOnlyEvents;

    public void Add(long tick, double elapsedSeconds, SimulationMode mode, SimulationEventKind kind,
        string id = "", string detail = "", ExpantaNum amount = default)
    {
        if (!double.IsFinite(elapsedSeconds))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!amount.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(amount));
        events.Add(new SimulationEvent(events.Count, tick, elapsedSeconds, mode, kind,
            id ?? string.Empty, detail ?? string.Empty, amount));
    }

    public IReadOnlyList<SimulationEvent> EventsFrom(long sequence) =>
        events.SkipWhile(item => item.Sequence < sequence).ToArray();

    public void Clear() => events.Clear();
}
