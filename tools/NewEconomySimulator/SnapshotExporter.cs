using System;
using System.IO;
using System.Text.Json;

namespace Kingdom.NewEconomySimulator;

public static class SnapshotExporter
{
    public static EconomySnapshot LoadJson(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Snapshot path is required.", nameof(path));
        var snapshot = JsonSerializer.Deserialize<EconomySnapshot>(File.ReadAllText(path));
        if (snapshot is null) throw new InvalidDataException("Snapshot JSON is empty.");
        SnapshotValidation.Normalize(snapshot);
        var result = SnapshotValidation.Validate(snapshot);
        if (!result.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, result.Errors));
        return snapshot;
    }

    public static EconomySnapshot LoadJsonText(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Snapshot JSON is required.", nameof(json));
        var snapshot = JsonSerializer.Deserialize<EconomySnapshot>(json) ?? throw new InvalidDataException("Snapshot JSON is empty.");
        SnapshotValidation.Normalize(snapshot);
        var result = SnapshotValidation.Validate(snapshot);
        if (!result.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, result.Errors));
        return snapshot;
    }
}
