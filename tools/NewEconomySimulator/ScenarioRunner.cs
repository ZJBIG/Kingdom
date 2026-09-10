using System;
using System.Collections.Generic;
using System.Linq;

namespace Kingdom.NewEconomySimulator;

public sealed record SimulationScenario(string Id, SimulationMode Mode, long Ticks);

public sealed record ScenarioResult(
    string Id,
    SimulationMode Mode,
    long Ticks,
    IReadOnlyList<SimulationEvent> Events,
    DetailedSimulationStateSummary Summary);

public sealed record ScenarioRunResult(
    IReadOnlyList<ScenarioResult> Scenarios,
    DetailedSimulationStateSummary FinalSummary);

public sealed class ScenarioRunner
{
    private readonly SimulationCore core;

    public ScenarioRunner(SimulationCore core) =>
        this.core = core ?? throw new ArgumentNullException(nameof(core));

    public SimulationStateSummary Run(IEnumerable<SimulationScenario> scenarios)
    {
        RunDetailed(scenarios);
        return core.Summarize();
    }

    public ScenarioRunResult RunDetailed(IEnumerable<SimulationScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        var scenarioList = scenarios.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scenario in scenarioList)
        {
            if (scenario is null || string.IsNullOrWhiteSpace(scenario.Id))
                throw new ArgumentException("Scenario ID is required.", nameof(scenarios));
            if (!ids.Add(scenario.Id))
                throw new ArgumentException($"Duplicate scenario ID '{scenario.Id}'.", nameof(scenarios));
            if (scenario.Ticks < 0)
                throw new ArgumentOutOfRangeException(nameof(scenarios), "Scenario ticks cannot be negative.");
            if (scenario.Mode is not SimulationMode.Realtime and not SimulationMode.Offline)
                throw new ArgumentOutOfRangeException(nameof(scenarios), scenario.Mode, "Unknown simulation mode.");
        }

        var results = new List<ScenarioResult>(scenarioList.Length);
        foreach (var scenario in scenarioList)
        {
            int firstEvent = core.Events.Events.Count;
            if (scenario.Mode == SimulationMode.Offline)
            {
                core.BeginOfflineSession();
                for (long tick = 0; tick < scenario.Ticks; tick++)
                    core.Tick(SimulationMode.Offline);
            }
            else
            {
                core.RunRealtime(scenario.Ticks);
            }

            results.Add(new ScenarioResult(
                scenario.Id,
                scenario.Mode,
                scenario.Ticks,
                core.Events.Events.Skip(firstEvent).ToArray(),
                core.SummarizeDetailed()));
        }

        return new ScenarioRunResult(results, core.SummarizeDetailed());
    }
}
