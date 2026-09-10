namespace Kingdom.NewEconomySimulator;

public sealed record EraStep(string TechLevel, double RealtimeSeconds, double OfflineSeconds);

public sealed class EraStepSchedule
{
    public static IReadOnlyList<string> ExpectedTechLevels { get; } = Array.AsReadOnly(
    [
        "Animal",
        "StoneAge",
        "Medieval",
        "Industrial",
        "Spacer",
        "Ultra",
        "Archotech"
    ]);

    public static EraStepSchedule Default { get; } = new(
    [
        new("Animal", 0.1d, 1d),
        new("StoneAge", 0.5d, 2d),
        new("Medieval", 1d, 5d),
        new("Industrial", 5d, 10d),
        new("Spacer", 15d, 20d),
        new("Ultra", 30d, 30d),
        new("Archotech", 60d, 60d)
    ]);

    private readonly IReadOnlyList<EraStep> orderedSteps;
    private readonly IReadOnlyDictionary<string, EraStep> steps;

    public EraStepSchedule(IEnumerable<EraStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var ordered = steps.ToArray();
        if (ordered.Length == 0)
            throw new ArgumentException("At least one era step is required.", nameof(steps));
        if (ordered.Length != ExpectedTechLevels.Count)
            throw new ArgumentException("Era steps must contain the exact simulator era set.", nameof(steps));

        var result = new Dictionary<string, EraStep>(StringComparer.Ordinal);
        for (var index = 0; index < ordered.Length; index++)
        {
            var step = ordered[index]
                ?? throw new ArgumentException("Era step cannot be null.", nameof(steps));
            if (string.IsNullOrWhiteSpace(step.TechLevel) ||
                !double.IsFinite(step.RealtimeSeconds) || step.RealtimeSeconds <= 0d ||
                !double.IsFinite(step.OfflineSeconds) || step.OfflineSeconds <= 0d)
                throw new ArgumentException("Era steps require an ID and positive finite durations.", nameof(steps));
            if (!StringComparer.Ordinal.Equals(step.TechLevel, ExpectedTechLevels[index]))
                throw new ArgumentException("Era steps must use the exact simulator era order.", nameof(steps));
            if (!result.TryAdd(step.TechLevel, step))
                throw new ArgumentException($"Duplicate era step '{step.TechLevel}'.", nameof(steps));
        }

        orderedSteps = Array.AsReadOnly(ordered);
        this.steps = result;
    }

    public IReadOnlyList<EraStep> Steps => orderedSteps;

    public double GetSeconds(string techLevel, SimulationMode mode)
    {
        ValidateMode(mode);
        if (!steps.TryGetValue(techLevel ?? string.Empty, out var step))
            throw new InvalidOperationException($"No simulation step is configured for era '{techLevel}'.");
        return mode switch
        {
            SimulationMode.Realtime => step.RealtimeSeconds,
            SimulationMode.Offline => step.OfflineSeconds,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported simulation mode.")
        };
    }

    internal static void ValidateMode(SimulationMode mode)
    {
        if (mode is not SimulationMode.Realtime and not SimulationMode.Offline)
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported simulation mode.");
    }
}
