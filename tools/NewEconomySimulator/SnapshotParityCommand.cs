using System.Globalization;
using System.Text.Json;

namespace Kingdom.NewEconomySimulator;

/// <summary>Controlled Unity snapshot comparison, not a progression or balance verdict.</summary>
public static class SnapshotParityCommand
{
    public static int Run(string[] args)
    {
        try
        {
            var options = Parse(args);
            var before = SnapshotExporter.LoadJson(options.Before);
            var expected = SnapshotExporter.LoadJson(options.After).RuntimeState;
            var core = SimulationCore.FromSnapshot(before);
            if (options.Offline) core.RunOffline(options.Seconds);
            else AdvanceRealtime(core, options.Seconds);
            var differences = Compare(core, expected);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                source = $"Controlled Unity v{SnapshotFormat.CurrentVersion} fixture; excludes world events, Ultra and relic systems",
                mode = options.Offline ? "Offline" : "Realtime", seconds = options.Seconds,
                passed = differences.Count == 0, firstDifference = differences.FirstOrDefault(), differences
            }, new JsonSerializerOptions { WriteIndented = true }));
            return differences.Count == 0 ? 0 : 1;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    internal static void AdvanceRealtime(SimulationCore core, double seconds)
    {
        var wholeTicks = (int)Math.Floor(seconds / 0.1d);
        for (var tick = 0; tick < wholeTicks; tick++)
            core.Tick(SimulationMode.Realtime, 0.1d, 0.1d);
        var remainder = seconds - wholeTicks * 0.1d;
        if (remainder > 0d)
            core.Tick(SimulationMode.Realtime, remainder, remainder);
    }

    internal sealed record Options(string Before, string After, bool Offline, double Seconds);

    internal static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length || args[i] is not ("--snapshot-before" or "--snapshot-after" or "--offline-seconds" or "--online-seconds") ||
                !values.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Expected unique --snapshot-before PATH --snapshot-after PATH and one of --online-seconds N / --offline-seconds N.");
        }
        if (!values.TryGetValue("--snapshot-before", out var before) || string.IsNullOrWhiteSpace(before) ||
            !values.TryGetValue("--snapshot-after", out var after) || string.IsNullOrWhiteSpace(after) ||
            values.ContainsKey("--offline-seconds") == values.ContainsKey("--online-seconds"))
            throw new ArgumentException("Snapshot paths and exactly one duration are required.");
        var offline = values.ContainsKey("--offline-seconds");
        if (!double.TryParse(values[offline ? "--offline-seconds" : "--online-seconds"], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var seconds) || !double.IsFinite(seconds) || seconds <= 0d || seconds > 86400d)
            throw new ArgumentException("Duration must be finite and in (0, 86400] seconds.");
        return new(before, after, offline, seconds);
    }

    public sealed record Difference(string Field, string Expected, string Actual);

    internal static List<Difference> Compare(SimulationCore core, RuntimeStateSnapshot expected)
    {
        var differences = Compare(core.State, expected);
        void Seconds(string field, double wanted, double got)
        {
            if (Math.Abs(wanted - got) > 1e-7d)
                differences.Add(new(field, wanted.ToString("R", CultureInfo.InvariantCulture),
                    got.ToString("R", CultureInfo.InvariantCulture)));
        }
        Seconds("CalendarElapsedSeconds", expected.CalendarElapsedSeconds, core.State.CalendarElapsedSeconds);
        Seconds("SimulationTickIntervalSeconds", expected.SimulationTickIntervalSeconds, core.TickSeconds);
        Seconds("OfflinePendingCalendarSeconds", expected.OfflinePendingCalendarSeconds, core.OfflinePendingCalendarSeconds);
        Seconds("OfflinePendingSimulationSeconds", expected.OfflinePendingSimulationSeconds, core.OfflinePendingSimulationSeconds);
        return differences;
    }

    internal static List<Difference> Compare(SimulationState actual, RuntimeStateSnapshot expected)
    {
        var differences = new List<Difference>();
        void Text(string field, string wanted, string got)
        { if (!StringComparer.Ordinal.Equals(wanted, got)) differences.Add(new(field, wanted, got)); }
        void Amount(string field, string wanted, ExpantaNum got)
        {
            var target = ExpantaNum.Parse(wanted);
            var tolerance = ExpantaNum.Max(new ExpantaNum(1e-4d), target.Abs() * new ExpantaNum(1e-8d));
            if ((target - got).Abs() > tolerance) differences.Add(new(field, wanted, got.ToString()));
        }
        void Flag(string field, bool wanted, bool got) => Text(field, wanted.ToString(), got.ToString());
        Text("TechLevel", expected.TechLevel, actual.TechLevel);
        Text("CalendarDays", expected.CalendarDays.ToString(), actual.CalendarDays.ToString());
        Amount("Food", expected.Food, actual.Food);
        Amount("FoodCapacity", expected.FoodCapacity, actual.FoodCapacity);
        Amount("Population", expected.Population, actual.Population);
        Amount("PopulationChangeProgress", expected.PopulationChangeProgress, actual.PopulationChangeProgress);
        Amount("TerritoryTotal", expected.TerritoryTotal, actual.TerritoryTotal);
        Amount("AttackPower", expected.AttackPower, actual.AttackPower);
        Amount("DefensePower", expected.DefensePower, actual.DefensePower);
        Amount("FleetPower", expected.FleetPower, actual.FleetPower);
        Amount("MilitaryManpower", expected.MilitaryManpower, actual.MilitaryManpower);
        Amount("SupplySatisfaction", expected.SupplySatisfaction, actual.SupplySatisfaction);
        Amount("PowerSatisfaction", expected.PowerSatisfaction, actual.PowerSatisfaction);
        Amount("LogisticsSatisfaction", expected.LogisticsSatisfaction, actual.LogisticsSatisfaction);
        Flag("CampaignActive", expected.CampaignActive, actual.CampaignActive);
        Text("CampaignTargetSectorId", expected.CampaignTargetSectorId ?? "", actual.CampaignTargetSectorId);
        Amount("CampaignProgress", expected.CampaignProgress, actual.CampaignProgress);
        Amount("CampaignCasualties", expected.CampaignCasualties, actual.CampaignCasualties);
        Amount("CampaignCombatRatio", expected.CampaignCombatRatio, actual.CampaignCombatRatio);
        foreach (var resource in expected.Resources) Amount("Resources." + resource.ResourceId, resource.Value, actual.GetResource(resource.ResourceId));
        foreach (var building in expected.Buildings)
        {
            var state = actual.BuildingStates[building.BuildingId];
            Amount("Building." + building.BuildingId + ".Amount", building.Amount, state.Amount);
            Amount("Building." + building.BuildingId + ".Efficiency", building.Efficiency, state.Efficiency);
        }
        Text("PurchasedWorkshopIds", string.Join("|", expected.PurchasedWorkshopIds.Order()),
            string.Join("|", actual.PurchasedWorkshops.Order()));
        Text("ActiveResearch", expected.ActiveResearchId ?? "", actual.ActiveResearch ?? "");
        Text("QueuedResearchIds", string.Join("|", expected.QueuedResearchIds), string.Join("|", actual.ResearchQueue));
        foreach (var research in expected.Research)
        {
            var state = actual.GetResearchState(research.ResearchId);
            var prefix = "Research." + research.ResearchId;
            Amount(prefix + ".Progress", research.Progress, state.Progress);
            Flag(prefix + ".Completed", research.Completed, state.Completed);
            Flag(prefix + ".CostPaid", research.CostPaid, state.CostPaid);
            foreach (var paid in research.PaidResourceCosts)
                Amount(prefix + ".Paid." + paid.ResourceId, paid.Value, state.PaidResourceCosts.GetValueOrDefault(paid.ResourceId));
            foreach (var paid in state.PaidResourceCosts.Where(paid => research.PaidResourceCosts.All(item => item.ResourceId != paid.Key)))
                Amount(prefix + ".Paid." + paid.Key, "0", paid.Value);
        }
        foreach (var sector in expected.Sectors)
        {
            var state = actual.SectorStates[sector.SectorId];
            var prefix = "Sector." + sector.SectorId;
            Flag(prefix + ".Unlocked", sector.Unlocked, state.Unlocked);
            Flag(prefix + ".Occupied", sector.Occupied, state.Occupied);
            Flag(prefix + ".ColonizationActive", sector.ColonizationActive, state.ColonizationActive);
            Flag(prefix + ".CampaignActive", sector.CampaignActive, state.CampaignActive);
            Amount(prefix + ".Progress", sector.Progress, state.Progress);
            Flag(prefix + ".InteriorExplorationActive", sector.InteriorExplorationActive, state.InteriorExplorationActive);
            Flag(prefix + ".InteriorExplorationPaused", sector.InteriorExplorationPaused, state.InteriorExplorationPaused);
            Amount(prefix + ".InteriorExplorationProgress", sector.InteriorExplorationProgress, state.InteriorExplorationProgress);
            Amount(prefix + ".Casualties", sector.Casualties, state.Casualties);
            Amount(prefix + ".CombatRatio", sector.CombatRatio, state.CombatRatio);
            Text(prefix + ".VisitCount", sector.VisitCount.ToString(), state.VisitCount.ToString());
        }
        return differences;
    }
}
