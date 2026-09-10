using System.Globalization;

namespace Kingdom.NewEconomySimulator;

public sealed record TraceEntry(
    long Tick,
    string Event,
    string Subject,
    IReadOnlyDictionary<string, string> Values);

public sealed record TraceDifference(
    int Index,
    string Field,
    string Expected,
    string Actual);

public sealed record FirstDifference(int Index, long Tick, string Field, string Expected, string Actual);

public static class TraceComparer
{
    public static FirstDifference? First(IReadOnlyList<TraceDifference> differences, IReadOnlyList<TraceEntry> expected, IReadOnlyList<TraceEntry> actual)
    {
        if (differences.Count == 0) return null;
        var d = differences[0];
        var tick = d.Index < expected.Count ? expected[d.Index].Tick : d.Index < actual.Count ? actual[d.Index].Tick : -1;
        return new(d.Index, tick, d.Field, d.Expected, d.Actual);
    }
    public static IReadOnlyList<TraceDifference> Compare(
        IReadOnlyList<TraceEntry> expected,
        IReadOnlyList<TraceEntry> actual,
        IReadOnlyCollection<string>? ignoredFields = null)
    {
        var differences = new List<TraceDifference>();
        var count = Math.Max(expected.Count, actual.Count);
        for (var index = 0; index < count; index++)
        {
            if (index >= expected.Count)
            {
                differences.Add(new(index, "entry", "<missing>", Describe(actual[index])));
                continue;
            }

            if (index >= actual.Count)
            {
                differences.Add(new(index, "entry", Describe(expected[index]), "<missing>"));
                continue;
            }

            var left = expected[index];
            var right = actual[index];
            CompareValue(differences, index, "tick", left.Tick.ToString(CultureInfo.InvariantCulture), right.Tick.ToString(CultureInfo.InvariantCulture), ignoredFields);
            CompareValue(differences, index, "event", left.Event, right.Event, ignoredFields);
            CompareValue(differences, index, "subject", left.Subject, right.Subject, ignoredFields);

            var keys = left.Values.Keys.Concat(right.Values.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal);
            foreach (var key in keys)
            {
                left.Values.TryGetValue(key, out var expectedValue);
                right.Values.TryGetValue(key, out var actualValue);
                CompareValue(differences, index, $"values.{key}", expectedValue ?? "<missing>", actualValue ?? "<missing>", ignoredFields);
            }
        }

        return differences;
    }

    private static void CompareValue(List<TraceDifference> differences, int index, string field, string? expected, string? actual,
        IReadOnlyCollection<string>? ignoredFields)
    {
        if (ignoredFields is not null && ignoredFields.Contains(field))
            return;
        if (!StringComparer.Ordinal.Equals(expected, actual))
            differences.Add(new(index, field, expected ?? "<null>", actual ?? "<null>"));
    }

    private static string Describe(TraceEntry entry) =>
        $"tick={entry.Tick};event={entry.Event};subject={entry.Subject}";
}
