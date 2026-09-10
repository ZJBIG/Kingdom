using System.Text.Json;
using Kingdom.NewEconomySimulator.Tests;

namespace Kingdom.NewEconomySimulator;

public sealed record ValidationResult(
    string Name,
    bool Passed,
    string Detail,
    IReadOnlyList<TraceDifference> Differences,
    string? ComparisonName = null);

public sealed record ApiGap(string Member, string WhyRequired);

public sealed record SourceRun(
    string Source,
    SimulationStateSnapshot Snapshot,
    IReadOnlyList<TraceEntry> OrderedEvents);

public sealed record ComparisonEvidence(
    string Name,
    bool Matches,
    IReadOnlyList<SourceRun> Sources,
    IReadOnlyList<TraceDifference> Differences,
    FirstDifference? FirstDifference,
    bool IsDiagnostic = false);

public sealed class ValidationReport
{
    public string Simulator { get; } = "NewEconomySimulator";
    public bool Executed { get; set; }
    public List<ValidationResult> Results { get; } = new();
    public List<ApiGap> ApiGaps { get; } = new();
    public List<ComparisonEvidence> Comparisons { get; } = new();
    public bool Passed => Executed
        && ApiGaps.Count == 0
        && Results.All(result => result.Passed)
        && Comparisons.All(comparison => comparison.IsDiagnostic || comparison.Matches);
}

public static class ReportWriter
{
    public static string ToCsv(ValidationReport report)
    {
        var lines = new List<string>
        {
            "section,check,source,sequence,tick,field,expected,actual,detail,passed"
        };

        foreach (var result in report.Results)
            lines.Add($"summary,{Csv(result.Name)},,,,,,,{Csv(result.Detail)},{result.Passed}");

        foreach (var comparison in report.Comparisons)
        {
            lines.Add($"comparison,{Csv(comparison.Name)},,,,,,,diagnostic={comparison.IsDiagnostic},{comparison.Matches}");
            foreach (var source in comparison.Sources)
            {
                AddSnapshotCsvRows(lines, comparison.Name, source);
                foreach (var entry in source.OrderedEvents)
                    lines.Add($"event,{Csv(comparison.Name)},{Csv(source.Source)},{Csv(Sequence(entry))},{entry.Tick},{Csv(entry.Event)},{Csv(entry.Subject)},{Csv(Value(entry, "mode"))},{Csv(Value(entry, "amount"))},{Csv(Value(entry, "detail"))},");
            }

            if (comparison.FirstDifference is { } difference)
                lines.Add($"first-difference,{Csv(comparison.Name)},,,{difference.Tick},{Csv(difference.Field)},{Csv(difference.Expected)},{Csv(difference.Actual)},,");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void AddSnapshotCsvRows(List<string> lines, string comparisonName, SourceRun source)
    {
        var snapshot = source.Snapshot;
        lines.Add($"snapshot,{Csv(comparisonName)},{Csv(source.Source)},,,{snapshot.Tick},tick,,,,");
        lines.Add($"snapshot,{Csv(comparisonName)},{Csv(source.Source)},,,{snapshot.Tick},elapsedSeconds,{Csv(snapshot.ElapsedSeconds)},,,,");
        lines.Add($"snapshot,{Csv(comparisonName)},{Csv(source.Source)},,,{snapshot.Tick},requestedSeconds,{Csv(snapshot.RequestedSeconds)},,,,");
        lines.Add($"snapshot,{Csv(comparisonName)},{Csv(source.Source)},,,{snapshot.Tick},calendarDays,{snapshot.CalendarDays},,,,");
        lines.Add($"snapshot,{Csv(comparisonName)},{Csv(source.Source)},,,{snapshot.Tick},food,{Csv(snapshot.Food)},,,,");
        lines.Add($"snapshot,{Csv(comparisonName)},{Csv(source.Source)},,,{snapshot.Tick},foodCapacity,{Csv(snapshot.FoodCapacity)},,,,");
        foreach (var resource in snapshot.Resources)
            lines.Add($"snapshot,{Csv(comparisonName)},{Csv(source.Source)},,,{snapshot.Tick},resource.{resource.Key},{Csv(resource.Value)},,,,");
        foreach (var workshop in snapshot.Workshops)
            lines.Add($"snapshot,{Csv(comparisonName)},{Csv(source.Source)},,,{snapshot.Tick},workshop.{workshop.Key},{Csv(workshop.Value)},,,,");
    }

    public static string ToJson(ValidationReport report) => JsonSerializer.Serialize(
        report,
        new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    public static string ToMarkdown(ValidationReport report)
    {
        var lines = new List<string>
        {
            "# New economy simulator validation",
            "",
            $"- Executed: `{report.Executed}`",
            $"- Passed: `{report.Passed}`",
            $"- Checks: `{report.Results.Count}`",
            "",
            "## Checks",
            "",
            "| Check | Result | Detail |",
            "|---|---|---|"
        };

        foreach (var result in report.Results)
            lines.Add($"| {Escape(result.Name)} | {(result.Passed ? "PASS" : "FAIL")} | {Escape(result.Detail)} |");

        if (report.Comparisons.Count > 0)
        {
            lines.AddRange(["", "## Comparisons", ""]);
            foreach (var comparison in report.Comparisons)
            {
                lines.Add($"### {Escape(comparison.Name)}");
                lines.Add("");
                lines.Add($"- Matched: `{comparison.Matches}`");
                lines.Add($"- Diagnostic: `{comparison.IsDiagnostic}`");
                lines.AddRange(["", "#### Snapshots", "", "| Source | Tick | Elapsed seconds | Requested seconds | Calendar days | Food | Food capacity | Resources | Workshops |", "|---|---:|---:|---:|---:|---:|---:|---|---|"]);
                foreach (var source in comparison.Sources)
                    lines.Add($"| {Escape(source.Source)} | {source.Snapshot.Tick} | {Escape(source.Snapshot.ElapsedSeconds)} | {Escape(source.Snapshot.RequestedSeconds)} | {source.Snapshot.CalendarDays} | {Escape(source.Snapshot.Food)} | {Escape(source.Snapshot.FoodCapacity)} | {Escape(Resources(source.Snapshot))} | {Escape(Workshops(source.Snapshot))} |");

                lines.AddRange(["", "#### Ordered events", "", "| Source | Sequence | Tick | Event | Subject | Mode | Amount | Detail |", "|---|---:|---:|---|---|---|---:|---|"]);
                foreach (var source in comparison.Sources)
                    foreach (var entry in source.OrderedEvents)
                        lines.Add($"| {Escape(source.Source)} | {Escape(Sequence(entry))} | {entry.Tick} | {Escape(entry.Event)} | {Escape(entry.Subject)} | {Escape(Value(entry, "mode"))} | {Escape(Value(entry, "amount"))} | {Escape(Value(entry, "detail"))} |");

                lines.AddRange(["", "#### First difference", ""]);
                if (comparison.FirstDifference is { } difference)
                    lines.Add($"- Tick: `{difference.Tick}`; field: `{Escape(difference.Field)}`; expected: `{Escape(difference.Expected)}`; actual: `{Escape(difference.Actual)}`");
                else
                    lines.Add("- No difference.");
                lines.Add("");
            }
        }

        if (report.ApiGaps.Count > 0)
        {
            lines.AddRange(["", "## API gaps", "", "| Member | Why required |", "|---|---|"]);
            foreach (var gap in report.ApiGaps)
                lines.Add($"| `{Escape(gap.Member)}` | {Escape(gap.WhyRequired)} |");
        }

        var differingResults = report.Results.Where(result => result.Differences.Count > 0).ToArray();
        if (differingResults.Length > 0)
        {
            lines.AddRange(["", "## First differences", ""]);
            foreach (var result in differingResults)
            {
                lines.Add($"### {Escape(result.Name)}");
                lines.Add("");
                foreach (var difference in result.Differences.Take(20))
                {
                    lines.Add(
                        $"- entry `{difference.Index}`, `{Escape(difference.Field)}`: "
                        + $"expected `{Escape(difference.Expected)}`, actual `{Escape(difference.Actual)}`");
                }
                lines.Add("");
            }
        }

        return string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine;
    }

    private static string Resources(SimulationStateSnapshot snapshot) =>
        string.Join("; ", snapshot.Resources.Select(pair => $"{pair.Key}={pair.Value}"));

    private static string Workshops(SimulationStateSnapshot snapshot) =>
        string.Join("; ", snapshot.Workshops.Select(pair => $"{pair.Key}={pair.Value}"));

    private static string Sequence(TraceEntry entry) => Value(entry, "sequence");

    private static string Value(TraceEntry entry, string key) =>
        entry.Values.TryGetValue(key, out var value) ? value : "<missing>";

    private static string Escape(string value) => value
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
