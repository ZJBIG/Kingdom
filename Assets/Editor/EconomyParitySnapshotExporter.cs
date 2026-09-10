using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EconomyParitySnapshotExporter
{
    public const int FormatVersion = 3;
    public const string DefaultRelativePath = "data/economy-parity/UnitySnapshot.json";

    [MenuItem("Kingdom/Economy/Export parity snapshot")]
    public static void ExportMenu()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = Path.Combine(root, DefaultRelativePath);
        ExportCurrent(path);
        Debug.Log($"[EconomyParity] Exported snapshot: {path}");
    }

    public static EconomySnapshotDto CaptureCurrent()
    {
        EconomySnapshotDto snapshot = new EconomySnapshotDto
        {
            FormatVersion = FormatVersion,
            Source = "Kingdom Unity 2022.3 runtime",
            TechLevels = new List<string>
            {
                TechLevel.Animal.ToString(),
                TechLevel.StoneAge.ToString(),
                TechLevel.Medieval.ToString(),
                TechLevel.Industrial.ToString(),
                TechLevel.Spacer.ToString(),
                TechLevel.Ultra.ToString(),
                TechLevel.Archotech.ToString()
            },
            Resources = OrderedDefinitions(DataBase<Resource>.All, "Resource").Select(Resource).ToList(),
            Buildings = AllBuildings().Select(Building).ToList(),
            Research = OrderedDefinitions(DataBase<Research>.All, "Research").Select(Research).ToList(),
            Workshops = OrderedDefinitions(DataBase<WorkshopUpgrade>.All, "Workshop").Select(Workshop).ToList(),
            Sectors = OrderedDefinitions(DataBase<SectorDefinition>.All, "Sector").Select(Sector).ToList(),
            InitialState = CaptureInitialState(),
            SaveState = CaptureSaveState(),
            RuntimeState = CaptureRuntimeState()
        };
        ValidateSnapshotContracts(snapshot);
        return snapshot;
    }

    public static void ExportCurrent(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Snapshot path is required.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, JsonUtility.ToJson(CaptureCurrent(), true));
    }

    private static IEnumerable<Building> AllBuildings()
    {
        var byId = new SortedDictionary<string, Building>(StringComparer.Ordinal);
        AddBuildings(byId, DataBase<Building>.All);
        AddBuildings(byId, DataBase<SectorBuilding>.All);
        return byId.Values;
    }

    private static void AddBuildings(SortedDictionary<string, Building> byId, IEnumerable<Building> buildings)
    {
        foreach (Building building in buildings)
        {
            if (building == null)
                throw new InvalidOperationException("A building definition is null.");
            string id = StableId(building);
            if (byId.TryGetValue(id, out Building existing))
            {
                if (AssetDatabase.GetAssetPath(existing) != AssetDatabase.GetAssetPath(building))
                    throw new InvalidOperationException($"Duplicate building stable ID '{id}'.");
                continue;
            }
            byId.Add(id, building);
        }
    }

    private static List<T> OrderedDefinitions<T>(IEnumerable<T> definitions, string kind) where T : GameDefinition
    {
        var byId = new SortedDictionary<string, T>(StringComparer.Ordinal);
        foreach (T definition in definitions)
        {
            if (definition == null)
                throw new InvalidOperationException($"A {kind} definition is null.");
            byId.Add(StableId(definition), definition);
        }
        return byId.Values.ToList();
    }

    private static ResourceDto Resource(Resource value) => new ResourceDto
    {
        Id = StableId(value), Guid = Guid(value), AssetPath = AssetDatabase.GetAssetPath(value),
        TechLevel = value.TechLevel.ToString(), Label = value.Label, Description = value.Description
    };

    private static BuildingDto Building(Building value) => new BuildingDto
    {
        Id = StableId(value), Guid = Guid(value), AssetPath = AssetDatabase.GetAssetPath(value), TechLevel = value.TechLevel.ToString(),
        CostGrowth = Number(value.CostGrowth), SpaceCost = Number(value.SpaceCost),
        ProductivityConsumption = Number(value.ProductivityConsumption), ProductivityGranted = Number(value.ProductivityGranted),
        FoodProductionRate = Number(value.FoodProductionRate), FoodConsumptionRate = Number(value.FoodConsumptionRate),
        FoodCapacityGranted = Number(value.FoodCapacityGranted), PowerProductionRate = Number(value.PowerProductionRate),
        PowerConsumptionRate = Number(value.PowerConsumptionRate), LogisticsProductionRate = Number(value.LogisticsProductionRate),
        LogisticsConsumptionRate = Number(value.LogisticsConsumptionRate), ResearchPowerGranted = Number(value.ResearchPowerGranted),
        PopulationCapacityGranted = Number(value.PopulationCapacityGranted), FleetPowerGranted = Number(value.FleetPowerGranted),
        AttackPowerGranted = Number(value.AttackPowerGranted), DefensePowerGranted = Number(value.DefensePowerGranted),
        MilitaryManpowerGranted = Number(value.MilitaryManpowerGranted), UpgradeToId = Id(value.UpgradeTo),
        ResourceRequirements = Amounts(value.ResourceRequirements), ResourceGenerationRates = Amounts(value.ResourceGenerationRates),
        ResourceConsumptionRates = Amounts(value.ResourceConsumptionRates), RequiredResearchIds = Ids(value.RequiredResearch),
        RequiredWorkshopIds = Ids(value.RequiredWorkshopUpgrades),
        SectorId = value is SectorBuilding sectorBuilding ? Id(sectorBuilding.Sector) : string.Empty
    };

    private static ResearchDto Research(Research value) => new ResearchDto
    {
        Id = StableId(value), Guid = Guid(value), AssetPath = AssetDatabase.GetAssetPath(value), TechLevel = value.TechLevel.ToString(),
        BaseCost = Number(value.BaseCost), AdvancesTechLevel = value.AdvancesTechLevel,
        PrerequisiteIds = Ids(value.Prerequisites), ResourceRequirements = Amounts(value.ResourceRequirements),
        Effects = Effects(value.Effects)
    };

    private static WorkshopDto Workshop(WorkshopUpgrade value) => new WorkshopDto
    {
        Id = StableId(value), Guid = Guid(value), AssetPath = AssetDatabase.GetAssetPath(value), TechLevel = value.TechLevel.ToString(),
        Label = value.Label, Description = value.Description, SortOrder = value.SortOrder,
        RequiredResearchIds = Ids(value.RequiredResearch), RequiredWorkshopIds = Ids(value.RequiredUpgrades),
        ResourceRequirements = Amounts(value.ResourceRequirements), Effects = Effects(value.Effects)
    };

    private static SectorDto Sector(SectorDefinition value) => new SectorDto
    {
        Id = StableId(value), Guid = Guid(value), AssetPath = AssetDatabase.GetAssetPath(value), StarSystemId = value.StarSystemId,
        Domain = value.Domain.ToString(), TerritoryReward = Number(value.TerritoryReward), EnemyPower = Number(value.EnemyPower),
        Repeatable = value.Repeatable, ColonizationFoodPerSecond = Number(value.ColonizationFoodPerSecond),
        ColonizationDurationSeconds = Number(value.ColonizationDurationSeconds), CampaignFoodPerSecond = Number(value.CampaignFoodPerSecond),
        CampaignProgressMultiplier = Number(value.CampaignProgressMultiplier), PrerequisiteSectorIds = Ids(value.PrerequisiteSectors),
        ResourceRewards = Amounts(value.ResourceRewards), OccupiedResourceRatesPerSecond = Amounts(value.OccupiedResourceRatesPerSecond),
        ColonizationResourceRatesPerSecond = Amounts(value.ColonizationResourceRatesPerSecond),
        CampaignResourceRatesPerSecond = Amounts(value.CampaignResourceRatesPerSecond)
    };

    private static StateDto CaptureInitialState()
    {
        var game = new GameState();
        return new StateDto
        {
            TechLevel = game.TechLevel.ToString(), Food = Number(game.FoodAmount), FoodCapacity = Number(game.FoodCapacity),
            Resources = OrderedDefinitions(DataBase<Resource>.All, "Resource")
                .Select(x => new AmountDto { ResourceId = x.Id, Value = Number(ResourceManager.IsStartingResource(x) ? new ExpantaNum(60) : ExpantaNum.Zero) }).ToList(),
            Buildings = AllBuildings()
                .Select(x => new BuildingStateDto { BuildingId = x.Id, Amount = Number(ExpantaNum.Zero), Efficiency = Number(ExpantaNum.One) }).ToList(),
            Research = OrderedDefinitions(DataBase<Research>.All, "Research")
                .Select(x => new ResearchStateDto { ResearchId = x.Id, Progress = Number(ExpantaNum.Zero), CostPaid = !x.HasPositiveResourceRequirement }).ToList(),
            PurchasedWorkshopIds = new List<string>(),
            Sectors = OrderedDefinitions(DataBase<SectorDefinition>.All, "Sector")
                .Select(x => new SectorStateDto { SectorId = x.Id }).ToList()
        };
    }

    private static SaveStateDto CaptureSaveState()
    {
        SaveManager.KingdomSaveData save = SaveManager.Instance.CaptureSaveData();
        return FromSave(save);
    }

    private static RuntimeStateDto CaptureRuntimeState()
    {
        RuntimeStateDto dto = CopyRuntime(FromSave(SaveManager.Instance.CaptureSaveData()));
        dto.Buildings = BuildingManager.Instance.States.Values.OrderBy(x => x.Definition.Id, StringComparer.Ordinal)
            .Select(x => new BuildingStateDto { BuildingId = x.Definition.Id, Amount = Number(x.Amount), Efficiency = Number(x.Efficiency) }).ToList();
        dto.Research = ResearchManager.Instance.States.Values.OrderBy(x => x.Definition.Id, StringComparer.Ordinal)
            .Select(x => new ResearchStateDto { ResearchId = x.Definition.Id, Progress = Number(x.Progress), CostPaid = x.CostPaid,
                Completed = x.Status == ResearchStatus.Completed,
                PaidResourceCosts = Amounts(x.Definition.ResourceRequirements.Where(p => x.GetPaidResourceCost(p.First) > ExpantaNum.Zero)
                    .Select(p => new Pair<Resource, ExpantaNum>(p.First, x.GetPaidResourceCost(p.First))).ToList()) }).ToList();
        dto.ElapsedSeconds = Number(ExpantaNum.Zero);
        dto.CampaignProgress = dto.CampaignActive
            ? dto.Sectors.FirstOrDefault(x => x.SectorId == dto.CampaignTargetSectorId)?.Progress ?? Number(ExpantaNum.Zero)
            : Number(ExpantaNum.Zero);
        dto.Events = new List<EventDto>();
        return dto;
    }

    private static SaveStateDto FromSave(SaveManager.KingdomSaveData save)
    {
        if (save == null || save.General == null || save.Resources == null || save.Buildings == null ||
            save.Researches == null || save.Workshop == null || save.Sectors == null)
            throw new InvalidDataException("The captured save is missing a required state section.");
        if (save.Resources.Resources == null || save.Buildings.Buildings == null || save.Researches.States == null ||
            save.Researches.QueuedResearchIds == null || save.Workshop.PurchasedUpgradeIds == null ||
            save.Sectors.States == null)
            throw new InvalidDataException("The captured save is missing a required state list.");

        SaveManager.GameSaveData game = save.General;
        return new SaveStateDto
        {
            TechLevel = game.TechLevel.ToString(), Food = Number(game.FoodAmount), FoodCapacity = Number(GameManager.Instance.State.FoodCapacity),
            CalendarDays = game.CalendarDays, Population = Number(game.Population), TerritoryTotal = Number(game.TerritoryTotal),
            AttackPower = Number(game.AttackPower), DefensePower = Number(game.DefensePower), FleetPower = Number(game.FleetPower),
            MilitaryManpower = Number(game.MilitaryManpower), SupplySatisfaction = Number(game.SupplySatisfaction),
            PowerSatisfaction = Number(game.PowerSatisfaction), LogisticsSatisfaction = Number(game.LogisticsSatisfaction),
            CampaignActive = game.CampaignActive, CampaignTargetSectorId = game.CampaignTargetSectorId,
            CampaignCasualties = Number(game.CampaignCasualties), CampaignCombatRatio = Number(game.CampaignCombatRatio),
            ActiveResearchId = save.Researches.ActiveResearchId,
            QueuedResearchIds = save.Researches.QueuedResearchIds.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            LastSaveUnixSeconds = game.LastSaveUnixSeconds,
            Resources = save.Resources.Resources.OrderBy(x => x.ResourceId, StringComparer.Ordinal)
                .Select(x => new AmountDto { ResourceId = x.ResourceId, Value = Number(x.Amount) }).ToList(),
            Buildings = save.Buildings.Buildings.OrderBy(x => x.BuildingId, StringComparer.Ordinal)
                .Select(x => new BuildingStateDto { BuildingId = x.BuildingId, Amount = Number(x.Amount), Efficiency = Number(ExpantaNum.One) }).ToList(),
            Research = save.Researches.States.OrderBy(x => x.ResearchId, StringComparer.Ordinal)
                .Select(x => new ResearchStateDto { ResearchId = x.ResearchId, Progress = Number(x.Progress), CostPaid = x.CostPaid, Completed = x.Completed,
                    PaidResourceCosts = (x.PaidResourceCosts == null
                        ? throw new InvalidDataException($"Missing paid resource costs for research '{x.ResearchId}'.")
                        : x.PaidResourceCosts).OrderBy(p => p.ResourceId, StringComparer.Ordinal)
                        .Select(p => new AmountDto { ResourceId = p.ResourceId, Value = Number(p.Amount) }).ToList() }).ToList(),
            PurchasedWorkshopIds = save.Workshop.PurchasedUpgradeIds.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            Sectors = save.Sectors.States.OrderBy(x => x.SectorId, StringComparer.Ordinal)
                .Select(x => new SectorStateDto { SectorId = x.SectorId, Unlocked = x.Unlocked, Occupied = x.Occupied,
                    ColonizationActive = x.ColonizationActive, CampaignActive = x.CampaignActive, Progress = Number(x.CampaignProgress),
                    Casualties = Number(x.CampaignCasualties), CombatRatio = Number(x.CampaignCombatRatio), VisitCount = x.VisitCount }).ToList()
        };
    }

    private static RuntimeStateDto CopyRuntime(SaveStateDto value) => new RuntimeStateDto
    {
        TechLevel = value.TechLevel, Food = value.Food, FoodCapacity = value.FoodCapacity, Resources = value.Resources,
        Buildings = value.Buildings, Research = value.Research, PurchasedWorkshopIds = value.PurchasedWorkshopIds, Sectors = value.Sectors,
        CalendarDays = value.CalendarDays, Population = value.Population, TerritoryTotal = value.TerritoryTotal,
        AttackPower = value.AttackPower, DefensePower = value.DefensePower, FleetPower = value.FleetPower,
        MilitaryManpower = value.MilitaryManpower, SupplySatisfaction = value.SupplySatisfaction,
        PowerSatisfaction = value.PowerSatisfaction, LogisticsSatisfaction = value.LogisticsSatisfaction,
        CampaignActive = value.CampaignActive, CampaignTargetSectorId = value.CampaignTargetSectorId,
        CampaignCasualties = value.CampaignCasualties, CampaignCombatRatio = value.CampaignCombatRatio,
        ActiveResearchId = value.ActiveResearchId, QueuedResearchIds = value.QueuedResearchIds, LastSaveUnixSeconds = value.LastSaveUnixSeconds
    };

    private static void ValidateSnapshotContracts(EconomySnapshotDto snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Source) || snapshot.InitialState == null ||
            snapshot.SaveState == null || snapshot.RuntimeState == null)
            throw new InvalidDataException("The snapshot is missing source or state data.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CheckDefinitions(snapshot.Resources, "Resource", ids, guids);
        CheckDefinitions(snapshot.Buildings, "Building", ids, guids);
        CheckDefinitions(snapshot.Research, "Research", ids, guids);
        CheckDefinitions(snapshot.Workshops, "Workshop", ids, guids);
        CheckDefinitions(snapshot.Sectors, "Sector", ids, guids);
    }

    private static void CheckDefinitions<T>(IEnumerable<T> definitions, string kind, HashSet<string> ids, HashSet<string> guids)
        where T : DefinitionDto
    {
        foreach (DefinitionDto definition in definitions)
        {
            if (definition == null)
                throw new InvalidOperationException($"A {kind} definition is null.");
            string id = definition.Id?.Trim() ?? string.Empty;
            if (id.Length == 0)
                throw new InvalidDataException($"Missing stable ID in {kind} definitions.");
            if (!ids.Add(id))
                throw new InvalidOperationException($"Duplicate stable ID '{id}' in {kind} definitions.");
            if (definition.Guid == null || definition.Guid.Length != 32 || !definition.Guid.All(Uri.IsHexDigit))
                throw new InvalidDataException($"Invalid Unity GUID for {kind} '{id}'.");
            if (!guids.Add(definition.Guid))
                throw new InvalidOperationException($"Duplicate Unity GUID '{definition.Guid}' in {kind} definitions.");
            if (string.IsNullOrWhiteSpace(definition.AssetPath))
                throw new InvalidDataException($"Missing asset path for {kind} '{definition.Id}'.");
        }
    }

    private static string Guid(UnityEngine.Object value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        string assetPath = AssetDatabase.GetAssetPath(value);
        if (string.IsNullOrWhiteSpace(assetPath))
            throw new InvalidDataException($"Missing asset path for '{value.name}'.");
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long _))
            throw new InvalidDataException($"Could not resolve the Unity GUID for '{value.name}'.");

        guid = guid.ToLowerInvariant();
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string metaPath = Path.Combine(projectRoot, assetPath + ".meta");
        if (!File.Exists(metaPath))
            throw new InvalidDataException($"Missing .meta file '{assetPath}.meta'.");

        string metaGuid = File.ReadLines(metaPath)
            .FirstOrDefault(line => line.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
            ?.Substring(5).Trim();
        if (!string.Equals(metaGuid, guid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unity GUID does not match '{assetPath}.meta'.");

        return guid;
    }

    private static string StableId(GameDefinition value)
    {
        string id = value.Id?.Trim() ?? string.Empty;
        if (id.Length == 0)
            throw new InvalidDataException($"Missing stable ID for '{value.name}'.");
        return id;
    }

    private static string Id(GameDefinition value) => value == null ? string.Empty : StableId(value);
    private static string Number(string value) => ExpantaNum.TryParse(value, out ExpantaNum number) && number.IsFinite
        ? number.ToString() : throw new FormatException($"Invalid ExpantaNum '{value}'.");
    private static string Number(ExpantaNum value) => value.IsFinite
        ? value.ToString() : throw new FormatException($"Invalid ExpantaNum '{value}'.");

    private static List<string> Ids<T>(IReadOnlyList<T> values) where T : GameDefinition
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (T value in values)
        {
            if (value == null)
                throw new InvalidOperationException("A definition reference is null.");
            string id = StableId(value);
            if (!ids.Add(id))
                throw new InvalidOperationException($"Duplicate definition reference '{id}'.");
            result.Add(id);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static List<AmountDto> Amounts(IReadOnlyList<Pair<Resource, ExpantaNum>> values)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        var byResourceId = new SortedDictionary<string, AmountDto>(StringComparer.Ordinal);
        foreach (Pair<Resource, ExpantaNum> value in values)
        {
            if (value.First == null)
                throw new InvalidOperationException("A resource amount references a null resource.");
            string resourceId = StableId(value.First);
            if (byResourceId.ContainsKey(resourceId))
                throw new InvalidOperationException($"Duplicate resource pair '{resourceId}'.");
            byResourceId.Add(resourceId, new AmountDto { ResourceId = resourceId, Value = Number(value.Second) });
        }
        return byResourceId.Values.ToList();
    }

    private static List<EffectDto> Effects(IReadOnlyList<ResearchEffectDefinition> values) => Effects(
        values,
        value => value.Type.ToString(),
        value => value.Building,
        value => value.Resource,
        value => value.NumericValue);

    private static List<EffectDto> Effects(IReadOnlyList<WorkshopEffectDefinition> values) => Effects(
        values,
        value => value.Type.ToString(),
        value => value.Building,
        value => value.Resource,
        value => value.NumericValue);

    private static List<EffectDto> Effects<T>(
        IReadOnlyList<T> values,
        Func<T, string> type,
        Func<T, Building> building,
        Func<T, Resource> resource,
        Func<T, ExpantaNum> amount)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        return values.Select(value =>
        {
            if (value == null)
                throw new InvalidOperationException("An effect definition is null.");
            return new EffectDto
            {
                Type = type(value), BuildingId = Id(building(value)), ResourceId = Id(resource(value)), Value = Number(amount(value))
            };
        }).OrderBy(x => x.Type, StringComparer.Ordinal)
            .ThenBy(x => x.BuildingId, StringComparer.Ordinal)
            .ThenBy(x => x.ResourceId, StringComparer.Ordinal).ToList();
    }
}

[Serializable] public class DefinitionDto { public string Id=""; public string Guid=""; public string AssetPath=""; public string TechLevel=""; }
[Serializable] public sealed class ResourceDto : DefinitionDto { public string Label=""; public string Description=""; }
[Serializable] public sealed class BuildingDto : DefinitionDto { public string CostGrowth="1"; public string SpaceCost="0"; public string ProductivityConsumption="0"; public string ProductivityGranted="0"; public string FoodProductionRate="0"; public string FoodConsumptionRate="0"; public string FoodCapacityGranted="0"; public string PowerProductionRate="0"; public string PowerConsumptionRate="0"; public string LogisticsProductionRate="0"; public string LogisticsConsumptionRate="0"; public string ResearchPowerGranted="0"; public string PopulationCapacityGranted="0"; public string FleetPowerGranted="0"; public string AttackPowerGranted="0"; public string DefensePowerGranted="0"; public string MilitaryManpowerGranted="0"; public string UpgradeToId=""; public List<AmountDto> ResourceRequirements=new(); public List<AmountDto> ResourceGenerationRates=new(); public List<AmountDto> ResourceConsumptionRates=new(); public List<string> RequiredResearchIds=new(); public List<string> RequiredWorkshopIds=new(); public string SectorId=""; }
[Serializable] public sealed class ResearchDto : DefinitionDto { public string BaseCost="0"; public bool AdvancesTechLevel; public List<string> PrerequisiteIds=new(); public List<AmountDto> ResourceRequirements=new(); public List<EffectDto> Effects=new(); }
[Serializable] public sealed class WorkshopDto : DefinitionDto { public string Label=""; public string Description=""; public int SortOrder; public List<string> RequiredResearchIds=new(); public List<string> RequiredWorkshopIds=new(); public List<AmountDto> ResourceRequirements=new(); public List<EffectDto> Effects=new(); }
[Serializable] public sealed class SectorDto : DefinitionDto { public string StarSystemId=""; public string Domain=""; public string TerritoryReward="0"; public string EnemyPower="0"; public bool Repeatable; public string ColonizationFoodPerSecond="0"; public string ColonizationDurationSeconds="0"; public string CampaignFoodPerSecond="0"; public string CampaignProgressMultiplier="1"; public List<string> PrerequisiteSectorIds=new(); public List<AmountDto> ResourceRewards=new(); public List<AmountDto> OccupiedResourceRatesPerSecond=new(); public List<AmountDto> ColonizationResourceRatesPerSecond=new(); public List<AmountDto> CampaignResourceRatesPerSecond=new(); }
[Serializable] public sealed class AmountDto { public string ResourceId=""; public string Value="0"; }
[Serializable] public sealed class EffectDto { public string Type=""; public string BuildingId=""; public string ResourceId=""; public string Value="1"; }
[Serializable] public class StateDto { public string TechLevel="Animal"; public string Food="0"; public string FoodCapacity="1"; public List<AmountDto> Resources=new(); public List<BuildingStateDto> Buildings=new(); public List<ResearchStateDto> Research=new(); public List<string> PurchasedWorkshopIds=new(); public List<SectorStateDto> Sectors=new(); }
[Serializable] public class SaveStateDto : StateDto { public int CalendarDays; public string Population="0"; public string TerritoryTotal="0"; public string AttackPower="0"; public string DefensePower="0"; public string FleetPower="0"; public string MilitaryManpower="0"; public string SupplySatisfaction="1"; public string PowerSatisfaction="1"; public string LogisticsSatisfaction="1"; public bool CampaignActive; public string CampaignTargetSectorId=""; public string CampaignCasualties="0"; public string CampaignCombatRatio="0"; public string ActiveResearchId=""; public List<string> QueuedResearchIds=new(); public long LastSaveUnixSeconds; }
[Serializable] public sealed class RuntimeStateDto : SaveStateDto { public long Tick; public string ElapsedSeconds="0"; public string CampaignProgress="0"; public List<EventDto> Events=new(); }
[Serializable] public sealed class BuildingStateDto { public string BuildingId=""; public string Amount="0"; public string Efficiency="1"; }
[Serializable] public sealed class ResearchStateDto { public string ResearchId=""; public string Progress="0"; public bool CostPaid; public bool Completed; public List<AmountDto> PaidResourceCosts=new(); }
[Serializable] public sealed class SectorStateDto { public string SectorId=""; public bool Unlocked; public bool Occupied; public bool ColonizationActive; public bool CampaignActive; public string Progress="0"; public string Casualties="0"; public string CombatRatio="0"; public int VisitCount; }
[Serializable] public sealed class EventDto { public long Tick; public string Kind=""; public string Id=""; public string Amount="0"; public string Detail=""; }
[Serializable] public sealed class EconomySnapshotDto { public int FormatVersion; public string Source=""; public List<string> TechLevels=new(); public List<ResourceDto> Resources=new(); public List<BuildingDto> Buildings=new(); public List<ResearchDto> Research=new(); public List<WorkshopDto> Workshops=new(); public List<SectorDto> Sectors=new(); public StateDto InitialState=new(); public SaveStateDto SaveState=new(); public RuntimeStateDto RuntimeState=new(); }
