using System;
using System.Collections.Generic;
using System.Linq;

namespace Kingdom.NewEconomySimulator;

public static class SnapshotValidation
{
    private enum DefinitionKind
    {
        Resource,
        Building,
        Research,
        Workshop,
        Sector
    }

    private static readonly string[] ExpectedTechLevels = EraStepSchedule.ExpectedTechLevels
        .OrderBy(level => level, StringComparer.Ordinal)
        .ToArray();

    public static void Normalize(EconomySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.TechLevels = SortedStrings(snapshot.TechLevels)!;
        snapshot.Resources = SortedDefinitions(snapshot.Resources)!;
        snapshot.Buildings = SortedDefinitions(snapshot.Buildings)!;
        snapshot.Research = SortedDefinitions(snapshot.Research)!;
        snapshot.Workshops = SortedDefinitions(snapshot.Workshops)!;
        snapshot.Sectors = SortedDefinitions(snapshot.Sectors)!;

        foreach (var building in snapshot.Buildings)
        {
            if (building == null)
                continue;
            building.ResourceRequirements = SortedAmounts(building.ResourceRequirements)!;
            building.ResourceGenerationRates = SortedAmounts(building.ResourceGenerationRates)!;
            building.ResourceConsumptionRates = SortedAmounts(building.ResourceConsumptionRates)!;
            building.RequiredResearchIds = SortedStrings(building.RequiredResearchIds)!;
            building.RequiredWorkshopIds = SortedStrings(building.RequiredWorkshopIds)!;
        }

        foreach (var research in snapshot.Research)
        {
            if (research == null)
                continue;
            research.ResourceRequirements = SortedAmounts(research.ResourceRequirements)!;
            research.PrerequisiteIds = SortedStrings(research.PrerequisiteIds)!;
            research.Effects = SortedEffects(research.Effects)!;
        }

        foreach (var workshop in snapshot.Workshops)
        {
            if (workshop == null)
                continue;
            workshop.ResourceRequirements = SortedAmounts(workshop.ResourceRequirements)!;
            workshop.RequiredResearchIds = SortedStrings(workshop.RequiredResearchIds)!;
            workshop.RequiredWorkshopIds = SortedStrings(workshop.RequiredWorkshopIds)!;
            workshop.Effects = SortedEffects(workshop.Effects)!;
        }

        foreach (var sector in snapshot.Sectors)
        {
            if (sector == null)
                continue;
            sector.ResourceRewards = SortedAmounts(sector.ResourceRewards)!;
            sector.OccupiedResourceRatesPerSecond = SortedAmounts(sector.OccupiedResourceRatesPerSecond)!;
            sector.ColonizationResourceRatesPerSecond = SortedAmounts(sector.ColonizationResourceRatesPerSecond)!;
            sector.CampaignResourceRatesPerSecond = SortedAmounts(sector.CampaignResourceRatesPerSecond)!;
            sector.PrerequisiteSectorIds = SortedStrings(sector.PrerequisiteSectorIds)!;
            var exploration = sector.InteriorExploration;
            // JsonUtility writes absent inline objects as their empty serialized defaults.
            if (exploration != null && string.IsNullOrWhiteSpace(exploration.Label) &&
                string.IsNullOrWhiteSpace(exploration.Description) &&
                string.IsNullOrWhiteSpace(exploration.CompletionDescription) &&
                (string.IsNullOrWhiteSpace(exploration.DurationSeconds) || exploration.DurationSeconds == "0") &&
                exploration.RequiredResearchIds is { Count: 0 } &&
                exploration.ResourceRatesPerSecond is { Count: 0 } && exploration.Milestones is { Count: 0 })
                sector.InteriorExploration = null;
            if (sector.InteriorExploration != null)
            {
                sector.InteriorExploration.RequiredResearchIds = SortedStrings(sector.InteriorExploration.RequiredResearchIds)!;
                sector.InteriorExploration.ResourceRatesPerSecond = SortedAmounts(sector.InteriorExploration.ResourceRatesPerSecond)!;
            }
        }

        NormalizeState(snapshot.InitialState);
        NormalizeState(snapshot.SaveState);
        NormalizeState(snapshot.RuntimeState);
    }

    public static SnapshotValidationResult Validate(EconomySnapshot snapshot)
    {
        var result = new SnapshotValidationResult();
        if (snapshot is null)
        {
            result.Errors.Add("Snapshot is null.");
            return result;
        }

        if (snapshot.FormatVersion != SnapshotFormat.CurrentVersion)
            result.Errors.Add($"Unsupported snapshot version {snapshot.FormatVersion}.");
        if (string.IsNullOrWhiteSpace(snapshot.Source))
            result.Errors.Add("Snapshot source is missing.");
        CheckTechLevelList(result, snapshot.TechLevels);

        var index = new DefinitionIndex();
        CheckDefinitions(result, snapshot.Resources, "Resource", DefinitionKind.Resource, index);
        CheckDefinitions(result, snapshot.Buildings, "Building", DefinitionKind.Building, index);
        CheckDefinitions(result, snapshot.Research, "Research", DefinitionKind.Research, index);
        CheckDefinitions(result, snapshot.Workshops, "Workshop", DefinitionKind.Workshop, index);
        CheckDefinitions(result, snapshot.Sectors, "Sector", DefinitionKind.Sector, index);

        if (snapshot.Resources != null)
            foreach (var resource in snapshot.Resources)
                if (resource != null && (!ExpantaNum.TryParse(resource.BaseProductionRate, out var baseProduction) ||
                    !baseProduction.IsFinite || baseProduction < ExpantaNum.Zero))
                    result.Errors.Add($"Invalid BaseProductionRate for '{resource.Id}'.");
        ValidateBuildings(result, snapshot.Buildings, index);
        ValidateResearch(result, snapshot.Research, index);
        ValidateWorkshops(result, snapshot.Workshops, index);
        ValidateSectors(result, snapshot.Sectors, index);
        CheckState(result, "InitialState", snapshot.InitialState, index);
        CheckState(result, "SaveState", snapshot.SaveState, index);
        CheckState(result, "RuntimeState", snapshot.RuntimeState, index);
        CheckPurchasedWorkshopSectors(result, "InitialState", snapshot.InitialState, snapshot.Workshops);
        CheckPurchasedWorkshopSectors(result, "SaveState", snapshot.SaveState, snapshot.Workshops);
        CheckPurchasedWorkshopSectors(result, "RuntimeState", snapshot.RuntimeState, snapshot.Workshops);
        CheckInteriorStates(result, "InitialState", snapshot.InitialState, snapshot);
        CheckInteriorStates(result, "SaveState", snapshot.SaveState, snapshot);
        CheckInteriorStates(result, "RuntimeState", snapshot.RuntimeState, snapshot);
        foreach (var building in snapshot.Buildings ?? [])
            if (building != null && building.RequiresInteriorExploration &&
                !(snapshot.Sectors?.Any(sector => sector != null && sector.Id == building.SectorId && sector.InteriorExploration != null) ?? false))
                result.Errors.Add($"Building '{building.Id}' requires a configured planetary exploration.");
        return result;
    }

    private static void CheckInteriorStates(SnapshotValidationResult result, string owner, StateSnapshot? state, EconomySnapshot snapshot)
    {
        if (state == null) return;
        foreach (var saved in state.Sectors ?? [])
        {
            if (saved == null || !ExpantaNum.TryParse(saved.InteriorExplorationProgress, out var progress) || !progress.IsFinite)
            { result.Errors.Add($"Invalid planetary exploration progress in '{owner}'."); continue; }
            var project = snapshot.Sectors?.FirstOrDefault(sector => sector != null && sector.Id == saved.SectorId)?.InteriorExploration;
            if (progress < ExpantaNum.Zero || progress > ExpantaNum.One ||
                ((progress > ExpantaNum.Zero || saved.InteriorExplorationActive || saved.InteriorExplorationPaused) && (!saved.Occupied || project == null)) ||
                ((saved.InteriorExplorationActive || saved.InteriorExplorationPaused) && progress >= ExpantaNum.One) ||
                (saved.InteriorExplorationActive && saved.InteriorExplorationPaused))
                result.Errors.Add($"Invalid planetary exploration state '{saved.SectorId}' in '{owner}'.");
            if ((progress > ExpantaNum.Zero || saved.InteriorExplorationActive || saved.InteriorExplorationPaused) && project != null &&
                (project.RequiredResearchIds?.Any(id => !(state.Research?.Any(research => research != null && research.ResearchId == id && research.Completed) ?? false)) ?? true))
                result.Errors.Add($"Planetary exploration '{saved.SectorId}' is missing required research in '{owner}'.");
        }
        foreach (var building in snapshot.Buildings ?? [])
        {
            if (building == null || string.IsNullOrEmpty(building.SectorId)) continue;
            bool owned = state.Buildings?.Any(item => item != null && item.BuildingId == building.Id &&
                ExpantaNum.TryParse(item.Amount, out var count) && count > ExpantaNum.Zero) ?? false;
            bool stagePurchased = snapshot.Workshops?.Any(stage => stage != null && stage.RequiredBuildingId == building.Id &&
                (state.PurchasedWorkshopIds?.Contains(stage.Id) ?? false)) ?? false;
            if (!owned && !stagePurchased) continue;
            var sector = state.Sectors?.FirstOrDefault(item => item != null && item.SectorId == building.SectorId);
            if (sector == null || !sector.Occupied || (building.RequiresInteriorExploration &&
                (!ExpantaNum.TryParse(sector.InteriorExplorationProgress, out var progress) || progress < ExpantaNum.One)))
                result.Errors.Add($"Building or purchased stage '{building.Id}' lacks required sector development in '{owner}'.");
            if (owned && building.MaxAmount > 0 && state.Buildings!.Any(item => item != null && item.BuildingId == building.Id &&
                ExpantaNum.TryParse(item.Amount, out var count) && count > new ExpantaNum(building.MaxAmount)))
                result.Errors.Add($"Building '{building.Id}' exceeds its limit in '{owner}'.");
        }
    }

    private static void CheckPurchasedWorkshopSectors(SnapshotValidationResult result,
        string owner, StateSnapshot? state, List<WorkshopSnapshot>? workshops)
    {
        if (state?.PurchasedWorkshopIds == null || workshops == null)
            return;
        foreach (var workshop in workshops)
        {
            if (workshop == null || string.IsNullOrEmpty(workshop.RequiredSectorId) ||
                !state.PurchasedWorkshopIds.Contains(workshop.Id, StringComparer.Ordinal))
                continue;
            if (state.Sectors == null || !state.Sectors.Any(sector => sector != null &&
                sector.SectorId == workshop.RequiredSectorId && sector.Occupied))
                result.Errors.Add($"Purchased workshop '{workshop.Id}' in '{owner}' requires occupied sector '{workshop.RequiredSectorId}'.");
        }
    }

    public static bool IsExpantaNumString(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ExpantaNum.TryParse(value, out var number) && number.IsFinite;

    private sealed class DefinitionIndex
    {
        private readonly HashSet<string> guids = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<DefinitionKind, HashSet<string>> idsByKind = new()
        {
            [DefinitionKind.Resource] = new(StringComparer.Ordinal),
            [DefinitionKind.Building] = new(StringComparer.Ordinal),
            [DefinitionKind.Research] = new(StringComparer.Ordinal),
            [DefinitionKind.Workshop] = new(StringComparer.Ordinal),
            [DefinitionKind.Sector] = new(StringComparer.Ordinal)
        };

        public void Add(
            SnapshotValidationResult result,
            DefinitionSnapshot definition,
            string kindName,
            DefinitionKind kind)
        {
            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                result.Errors.Add($"Missing stable ID in {kindName} definitions.");
                return;
            }

            string id = definition.Id.Trim();
            if (!idsByKind[kind].Add(id))
                result.Errors.Add($"Duplicate stable ID '{id}' in {kindName} definitions.");

            if (!GuidValid(definition.Guid) || !guids.Add(definition.Guid))
                result.Errors.Add($"Invalid or duplicate Unity GUID for '{id}'.");
            if (string.IsNullOrWhiteSpace(definition.AssetPath))
                result.Errors.Add($"Missing asset path for '{id}'.");
            // Sectors have domain/system gates, not a TechLevel in Runtime.
            if (kind != DefinitionKind.Sector && (string.IsNullOrWhiteSpace(definition.TechLevel) ||
                !ExpectedTechLevels.Contains(definition.TechLevel, StringComparer.Ordinal)))
                result.Errors.Add($"Unknown tech level '{definition.TechLevel}' for '{id}'.");
        }

        public bool Contains(string id, DefinitionKind kind) => idsByKind[kind].Contains(id);

        public IEnumerable<string> GetIds(DefinitionKind kind) => idsByKind[kind];
    }

    private static void CheckDefinitions<T>(
        SnapshotValidationResult result,
        List<T>? definitions,
        string kindName,
        DefinitionKind kind,
        DefinitionIndex index) where T : DefinitionSnapshot
    {
        if (definitions is null)
        {
            result.Errors.Add($"Missing {kindName} definitions.");
            return;
        }

        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                result.Errors.Add($"Null {kindName} definition.");
                continue;
            }
            index.Add(result, definition, kindName, kind);
        }
    }

    private static void ValidateBuildings(
        SnapshotValidationResult result,
        List<BuildingSnapshot>? buildings,
        DefinitionIndex index)
    {
        if (buildings is null)
            return;

        foreach (var building in buildings)
        {
            if (building is null)
                continue;
            if (building.MaxAmount < 0) result.Errors.Add($"Negative building limit for '{building.Id}'.");
            CheckNumbers(result, building.Id,
            [
                ("CostGrowth", building.CostGrowth),
                ("SpaceCost", building.SpaceCost),
                ("ProductivityConsumption", building.ProductivityConsumption),
                ("ProductivityGranted", building.ProductivityGranted),
                ("FoodProductionRate", building.FoodProductionRate),
                ("FoodConsumptionRate", building.FoodConsumptionRate),
                ("FoodCapacityGranted", building.FoodCapacityGranted),
                ("PowerProductionRate", building.PowerProductionRate),
                ("PowerConsumptionRate", building.PowerConsumptionRate),
                ("LogisticsProductionRate", building.LogisticsProductionRate),
                ("LogisticsConsumptionRate", building.LogisticsConsumptionRate),
                ("ResearchPowerGranted", building.ResearchPowerGranted),
                ("PopulationCapacityGranted", building.PopulationCapacityGranted),
                ("FleetPowerGranted", building.FleetPowerGranted),
                ("AttackPowerGranted", building.AttackPowerGranted),
                ("DefensePowerGranted", building.DefensePowerGranted),
                ("MilitaryManpowerGranted", building.MilitaryManpowerGranted)
            ]);
            CheckAmounts(result, building.Id, building.ResourceRequirements, index);
            CheckAmounts(result, building.Id, building.ResourceGenerationRates, index);
            CheckAmounts(result, building.Id, building.ResourceConsumptionRates, index);
            CheckRequiredRefs(result, building.Id, building.RequiredResearchIds, DefinitionKind.Research, index);
            CheckRequiredRefs(result, building.Id, building.RequiredWorkshopIds, DefinitionKind.Workshop, index);
            CheckOptionalRef(result, building.Id, building.UpgradeToId, DefinitionKind.Building, index);
            CheckOptionalRef(result, building.Id, building.SectorId, DefinitionKind.Sector, index);
        }
    }

    private static void ValidateResearch(
        SnapshotValidationResult result,
        List<ResearchSnapshot>? research,
        DefinitionIndex index)
    {
        if (research is null)
            return;

        foreach (var item in research)
        {
            if (item is null)
                continue;
            CheckNumbers(result, item.Id, [("BaseCost", item.BaseCost)]);
            CheckAmounts(result, item.Id, item.ResourceRequirements, index);
            CheckRequiredRefs(result, item.Id, item.PrerequisiteIds, DefinitionKind.Research, index);
            CheckEffects(result, item.Id, item.Effects, index);
        }
    }

    private static void ValidateWorkshops(
        SnapshotValidationResult result,
        List<WorkshopSnapshot>? workshops,
        DefinitionIndex index)
    {
        if (workshops is null)
            return;

        foreach (var workshop in workshops)
        {
            if (workshop is null)
                continue;
            CheckOptionalRef(result, workshop.Id, workshop.RequiredBuildingId, DefinitionKind.Building, index);
            CheckOptionalRef(result, workshop.Id, workshop.RequiredSectorId, DefinitionKind.Sector, index);
            CheckAmounts(result, workshop.Id, workshop.ResourceRequirements, index);
            CheckRequiredRefs(result, workshop.Id, workshop.RequiredResearchIds, DefinitionKind.Research, index);
            CheckRequiredRefs(result, workshop.Id, workshop.RequiredWorkshopIds, DefinitionKind.Workshop, index);
            CheckEffects(result, workshop.Id, workshop.Effects, index);
        }
    }

    private static void ValidateSectors(
        SnapshotValidationResult result,
        List<SectorSnapshot>? sectors,
        DefinitionIndex index)
    {
        if (sectors is null)
            return;

        foreach (var sector in sectors)
        {
            if (sector is null)
                continue;
            if (string.IsNullOrWhiteSpace(sector.StarSystemId))
                result.Errors.Add($"Missing star system ID for '{sector.Id}'.");
            if (sector.Domain != "HomeSystem" && sector.Domain != "Interstellar")
                result.Errors.Add($"Unknown sector domain '{sector.Domain}' for '{sector.Id}'.");
            if (sector.Domain == "HomeSystem" &&
                (sector.ResourceRewards?.Count > 0 || sector.OccupiedResourceRatesPerSecond?.Count > 0))
                result.Errors.Add($"Home-system sector '{sector.Id}' must provide development space without resource rewards or passive production.");
            CheckNumbers(result, sector.Id,
            [
                ("TerritoryReward", sector.TerritoryReward),
                ("EnemyPower", sector.EnemyPower),
                ("ColonizationFoodPerSecond", sector.ColonizationFoodPerSecond),
                ("ColonizationDurationSeconds", sector.ColonizationDurationSeconds),
                ("CampaignFoodPerSecond", sector.CampaignFoodPerSecond),
                ("CampaignProgressMultiplier", sector.CampaignProgressMultiplier)
            ]);
            CheckAmounts(result, sector.Id, sector.ResourceRewards, index);
            CheckAmounts(result, sector.Id, sector.OccupiedResourceRatesPerSecond, index);
            CheckAmounts(result, sector.Id, sector.ColonizationResourceRatesPerSecond, index);
            CheckAmounts(result, sector.Id, sector.CampaignResourceRatesPerSecond, index);
            CheckRequiredRefs(result, sector.Id, sector.PrerequisiteSectorIds, DefinitionKind.Sector, index);
            var project = sector.InteriorExploration;
            if (project != null)
            {
                if (string.IsNullOrWhiteSpace(project.Label) || string.IsNullOrWhiteSpace(project.Description) ||
                    string.IsNullOrWhiteSpace(project.CompletionDescription) ||
                    !ExpantaNum.TryParse(project.DurationSeconds, out var duration) || !duration.IsFinite || duration <= ExpantaNum.Zero ||
                    project.Milestones == null || project.Milestones.Count != 3 || project.Milestones.Any(string.IsNullOrWhiteSpace))
                    result.Errors.Add($"Invalid planetary exploration definition for '{sector.Id}'.");
                CheckRequiredRefs(result, sector.Id, project.RequiredResearchIds, DefinitionKind.Research, index);
                CheckAmounts(result, sector.Id, project.ResourceRatesPerSecond, index);
                if (project.ResourceRatesPerSecond == null || project.ResourceRatesPerSecond.Count == 0 ||
                    project.ResourceRatesPerSecond.Any(rate => rate == null || !ExpantaNum.TryParse(rate.Value, out var amount) || !amount.IsFinite || amount <= ExpantaNum.Zero))
                    result.Errors.Add($"Planetary exploration '{sector.Id}' requires positive supply rates.");
            }
        }
    }

    private static void CheckState(
        SnapshotValidationResult result,
        string owner,
        StateSnapshot? state,
        DefinitionIndex index)
    {
        if (state is null)
        {
            result.Errors.Add($"Missing {owner}.");
            return;
        }

        if (!ExpectedTechLevels.Contains(state.TechLevel, StringComparer.Ordinal))
            result.Errors.Add($"Unknown tech level '{state.TechLevel}' in '{owner}'.");
        CheckNumbers(result, owner, [("Food", state.Food), ("FoodCapacity", state.FoodCapacity)]);
        CheckAmounts(result, owner, state.Resources, index);

        if (state.Buildings is not null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var building in state.Buildings)
            {
                if (building is null)
                {
                    result.Errors.Add($"Null building state in '{owner}'.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(building.BuildingId) || !seen.Add(building.BuildingId))
                    result.Errors.Add($"Missing or duplicate building state ID '{building.BuildingId}' in '{owner}'.");
                CheckOptionalRef(result, owner, building.BuildingId, DefinitionKind.Building, index);
                CheckNumbers(result, owner, [("Amount", building.Amount), ("Efficiency", building.Efficiency)]);
            }
        }

        if (state.Research is not null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var research in state.Research)
            {
                if (research is null)
                {
                    result.Errors.Add($"Null research state in '{owner}'.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(research.ResearchId) || !seen.Add(research.ResearchId))
                    result.Errors.Add($"Missing or duplicate research state ID '{research.ResearchId}' in '{owner}'.");
                CheckOptionalRef(result, owner, research.ResearchId, DefinitionKind.Research, index);
                CheckNumbers(result, owner, [("Progress", research.Progress)]);
                CheckAmounts(result, owner, research.PaidResourceCosts, index);
            }
        }

        CheckRequiredRefs(result, owner, state.PurchasedWorkshopIds, DefinitionKind.Workshop, index);

        if (state.Sectors is not null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sector in state.Sectors)
            {
                if (sector is null)
                {
                    result.Errors.Add($"Null sector state in '{owner}'.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(sector.SectorId) || !seen.Add(sector.SectorId))
                    result.Errors.Add($"Missing or duplicate sector state ID '{sector.SectorId}' in '{owner}'.");
                CheckOptionalRef(result, owner, sector.SectorId, DefinitionKind.Sector, index);
                CheckNumbers(result, owner,
                [
                    ("Progress", sector.Progress),
                    ("Casualties", sector.Casualties),
                    ("CombatRatio", sector.CombatRatio)
                ]);
            }
        }

        if (state is SaveStateSnapshot save)
        {
            CheckNumbers(result, owner,
            [
                ("Population", save.Population),
                ("PopulationChangeProgress", save.PopulationChangeProgress),
                ("TerritoryTotal", save.TerritoryTotal),
                ("AttackPower", save.AttackPower),
                ("DefensePower", save.DefensePower),
                ("FleetPower", save.FleetPower),
                ("MilitaryManpower", save.MilitaryManpower),
                ("SupplySatisfaction", save.SupplySatisfaction),
                ("PowerSatisfaction", save.PowerSatisfaction),
                ("LogisticsSatisfaction", save.LogisticsSatisfaction),
                ("CampaignCasualties", save.CampaignCasualties),
                ("CampaignCombatRatio", save.CampaignCombatRatio)
            ]);
            if (ExpantaNum.TryParse(save.PopulationChangeProgress, out var populationProgress) && populationProgress.IsFinite &&
                (populationProgress < ExpantaNum.Zero || populationProgress > ExpantaNum.One))
                result.Errors.Add($"PopulationChangeProgress must be in [0, 1] in '{owner}'.");
            if (save.CalendarDays < 0 || save.LastSaveUnixSeconds < 0)
                result.Errors.Add($"Negative calendar/save timestamp in '{owner}'.");
            CheckOptionalRef(result, owner, save.ActiveResearchId, DefinitionKind.Research, index);
            CheckRequiredRefs(result, owner, save.QueuedResearchIds, DefinitionKind.Research, index);
            if (save.CampaignActive && string.IsNullOrWhiteSpace(save.CampaignTargetSectorId))
                result.Errors.Add($"Active campaign has no target sector in '{owner}'.");
            CheckOptionalRef(result, owner, save.CampaignTargetSectorId, DefinitionKind.Sector, index);
        }

        if (state is RuntimeStateSnapshot runtime)
        {
            if (!double.IsFinite(runtime.CalendarElapsedSeconds) || runtime.CalendarElapsedSeconds < 0d || runtime.CalendarElapsedSeconds >= 10d)
                result.Errors.Add($"Invalid calendar remainder in '{owner}'.");
            if (!double.IsFinite(runtime.SimulationTickIntervalSeconds) || runtime.SimulationTickIntervalSeconds <= 0d)
                result.Errors.Add($"Invalid simulation tick interval in '{owner}'.");
            if (!double.IsFinite(runtime.OfflinePendingCalendarSeconds) || runtime.OfflinePendingCalendarSeconds < 0d ||
                !double.IsFinite(runtime.OfflinePendingSimulationSeconds) || runtime.OfflinePendingSimulationSeconds < 0d ||
                runtime.OfflinePendingSimulationSeconds >= runtime.SimulationTickIntervalSeconds ||
                (runtime.OfflinePendingCalendarSeconds == 0d) != (runtime.OfflinePendingSimulationSeconds == 0d) ||
                runtime.OfflinePendingCalendarSeconds < runtime.OfflinePendingSimulationSeconds ||
                runtime.OfflinePendingCalendarSeconds > runtime.OfflinePendingSimulationSeconds * 4d + 1e-12d)
                result.Errors.Add($"Invalid offline operation remainder in '{owner}'.");
            if (runtime.Tick < 0)
                result.Errors.Add($"Negative tick in '{owner}'.");
            CheckNumbers(result, owner,
            [
                ("ElapsedSeconds", runtime.ElapsedSeconds),
                ("CampaignProgress", runtime.CampaignProgress)
            ]);
            if (runtime.Events is null)
                result.Errors.Add($"Missing event list in '{owner}'.");
            else
            {
                foreach (var simulationEvent in runtime.Events)
                {
                    if (simulationEvent is null)
                    {
                        result.Errors.Add($"Null event in '{owner}'.");
                        continue;
                    }
                    if (simulationEvent.Tick < 0)
                        result.Errors.Add($"Negative event tick in '{owner}'.");
                    if (string.IsNullOrWhiteSpace(simulationEvent.Kind))
                        result.Errors.Add($"Missing event kind in '{owner}'.");
                    CheckNumbers(result, owner, [("EventAmount", simulationEvent.Amount)]);
                }
            }
        }

        RequireCoverage(result, owner, index.GetIds(DefinitionKind.Resource),
            state.Resources?.Select(value => value?.ResourceId ?? string.Empty) ?? Array.Empty<string>(), "resource");
        RequireCoverage(result, owner, index.GetIds(DefinitionKind.Building),
            state.Buildings?.Select(value => value?.BuildingId ?? string.Empty) ?? Array.Empty<string>(), "building");
        RequireCoverage(result, owner, index.GetIds(DefinitionKind.Research),
            state.Research?.Select(value => value?.ResearchId ?? string.Empty) ?? Array.Empty<string>(), "research");
        RequireCoverage(result, owner, index.GetIds(DefinitionKind.Sector),
            state.Sectors?.Select(value => value?.SectorId ?? string.Empty) ?? Array.Empty<string>(), "sector");
    }

    private static void CheckEffects(
        SnapshotValidationResult result,
        string owner,
        List<EffectSnapshot>? effects,
        DefinitionIndex index)
    {
        if (effects is null)
        {
            result.Errors.Add($"Missing effect list in '{owner}'.");
            return;
        }

        foreach (var effect in effects)
        {
            if (effect is null)
            {
                result.Errors.Add($"Null effect in '{owner}'.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(effect.Type))
                result.Errors.Add($"Missing effect type in '{owner}'.");
            CheckOptionalRef(result, owner, effect.BuildingId, DefinitionKind.Building, index);
            CheckOptionalRef(result, owner, effect.ResourceId, DefinitionKind.Resource, index);
            CheckNumbers(result, owner, [("EffectValue", effect.Value)]);
        }
    }

    private static void CheckAmounts(
        SnapshotValidationResult result,
        string owner,
        List<AmountSnapshot>? amounts,
        DefinitionIndex index)
    {
        if (amounts is null)
        {
            result.Errors.Add($"Missing amount list in '{owner}'.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var amount in amounts)
        {
            if (amount is null)
            {
                result.Errors.Add($"Null amount in '{owner}'.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(amount.ResourceId))
                result.Errors.Add($"Missing resource ID in '{owner}'.");
            else if (!seen.Add(amount.ResourceId))
                result.Errors.Add($"Duplicate resource pair '{amount.ResourceId}' in '{owner}'.");
            else
                CheckOptionalRef(result, owner, amount.ResourceId, DefinitionKind.Resource, index);
            CheckNumbers(result, owner, [("amount", amount.Value)]);
        }
    }

    private static void CheckRequiredRefs(
        SnapshotValidationResult result,
        string owner,
        List<string>? ids,
        DefinitionKind expectedKind,
        DefinitionIndex index)
    {
        if (ids is null)
        {
            result.Errors.Add($"Missing reference list in '{owner}'.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                result.Errors.Add($"Missing or duplicate reference '{id}' in '{owner}'.");
            CheckRef(result, owner, id, expectedKind, index);
        }
    }

    private static void CheckOptionalRef(
        SnapshotValidationResult result,
        string owner,
        string? id,
        DefinitionKind expectedKind,
        DefinitionIndex index)
    {
        if (string.IsNullOrEmpty(id))
            return;
        CheckRef(result, owner, id, expectedKind, index);
    }

    private static void CheckRef(
        SnapshotValidationResult result,
        string owner,
        string id,
        DefinitionKind expectedKind,
        DefinitionIndex index)
    {
        if (!index.Contains(id, expectedKind))
            result.Errors.Add($"Unknown {expectedKind} reference '{id}' in '{owner}'.");
    }

    private static void CheckNumbers(
        SnapshotValidationResult result,
        string owner,
        IEnumerable<(string Field, string? Value)> values)
    {
        foreach (var value in values)
        {
            if (!IsExpantaNumString(value.Value))
                result.Errors.Add($"Invalid ExpantaNum string in '{owner}.{value.Field}': '{value.Value}'.");
        }
    }

    private static void CheckTechLevelList(SnapshotValidationResult result, List<string>? techLevels)
    {
        if (techLevels is null || !techLevels.OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(ExpectedTechLevels))
            result.Errors.Add("Snapshot TechLevels must contain each current TechLevel exactly once.");
    }

    private static void RequireCoverage(
        SnapshotValidationResult result,
        string owner,
        IEnumerable<string> expected,
        IEnumerable<string> actual,
        string kind)
    {
        var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            result.Errors.Add($"Missing {kind} state in '{owner}': {string.Join(", ", missing)}.");
    }

    private static bool GuidValid(string? value) =>
        value is { Length: 32 } && value.All(Uri.IsHexDigit);

    private static List<string>? SortedStrings(List<string>? values)
    {
        if (values is null)
            return null;
        var result = new List<string>(values);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static List<T>? SortedDefinitions<T>(List<T>? values) where T : DefinitionSnapshot
    {
        if (values is null)
            return null;
        var result = new List<T>(values);
        result.Sort(ById);
        return result;
    }

    private static List<AmountSnapshot>? SortedAmounts(List<AmountSnapshot>? values)
    {
        if (values is null)
            return null;
        var result = new List<AmountSnapshot>(values);
        result.Sort((left, right) => StringComparer.Ordinal.Compare(left?.ResourceId, right?.ResourceId));
        return result;
    }

    private static List<EffectSnapshot>? SortedEffects(List<EffectSnapshot>? values)
    {
        if (values is null)
            return null;
        var result = new List<EffectSnapshot>(values);
        result.Sort((left, right) =>
        {
            int comparison = StringComparer.Ordinal.Compare(left?.Type, right?.Type);
            if (comparison != 0)
                return comparison;
            comparison = StringComparer.Ordinal.Compare(left?.BuildingId, right?.BuildingId);
            if (comparison != 0)
                return comparison;
            return StringComparer.Ordinal.Compare(left?.ResourceId, right?.ResourceId);
        });
        return result;
    }

    private static void NormalizeState(StateSnapshot? state)
    {
        if (state is null)
            return;
        state.Resources = SortedAmounts(state.Resources)!;
        state.Buildings = SortedStates(state.Buildings, value => value?.BuildingId)!;
        state.Research = SortedStates(state.Research, value => value?.ResearchId)!;
        state.PurchasedWorkshopIds = SortedStrings(state.PurchasedWorkshopIds)!;
        state.Sectors = SortedStates(state.Sectors, value => value?.SectorId)!;
        if (state is RuntimeStateSnapshot runtime)
        {
            runtime.Events = runtime.Events is null
                ? null!
                : runtime.Events.OrderBy(value => value?.Tick)
                    .ThenBy(value => value?.Kind, StringComparer.Ordinal)
                    .ThenBy(value => value?.Id, StringComparer.Ordinal).ToList()!;
        }
    }

    private static List<T>? SortedStates<T>(List<T>? values, Func<T, string?> id) where T : class
    {
        if (values is null)
            return null;
        var result = new List<T>(values);
        result.Sort((left, right) => StringComparer.Ordinal.Compare(id(left), id(right)));
        return result;
    }

    private static int ById(DefinitionSnapshot? left, DefinitionSnapshot? right) =>
        StringComparer.Ordinal.Compare(left?.Id, right?.Id);
}
