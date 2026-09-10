using System;
using System.Collections.Generic;

namespace Kingdom.NewEconomySimulator;

public static class SnapshotFormat { public const int CurrentVersion = 3; }

public sealed class EconomySnapshot
{
    public int FormatVersion { get; set; } = SnapshotFormat.CurrentVersion;
    public string Source { get; set; } = "Kingdom Unity snapshot";
    public List<string> TechLevels { get; set; } = new();
    public List<ResourceSnapshot> Resources { get; set; } = new();
    public List<BuildingSnapshot> Buildings { get; set; } = new();
    public List<ResearchSnapshot> Research { get; set; } = new();
    public List<WorkshopSnapshot> Workshops { get; set; } = new();
    public List<SectorSnapshot> Sectors { get; set; } = new();
    public InitialStateSnapshot InitialState { get; set; } = new();
    public SaveStateSnapshot SaveState { get; set; } = new();
    public RuntimeStateSnapshot RuntimeState { get; set; } = new();
}

public abstract class DefinitionSnapshot
{
    public string Id { get; set; } = "";
    public string Guid { get; set; } = "";
    public string AssetPath { get; set; } = "";
    public string TechLevel { get; set; } = "";
}

public sealed class ResourceSnapshot : DefinitionSnapshot { public string Label { get; set; } = ""; public string Description { get; set; } = ""; }

public sealed class BuildingSnapshot : DefinitionSnapshot
{
    public string CostGrowth { get; set; } = "1.15";
    public string SpaceCost { get; set; } = "0";
    public string ProductivityConsumption { get; set; } = "0";
    public string ProductivityGranted { get; set; } = "0";
    public string FoodProductionRate { get; set; } = "0";
    public string FoodConsumptionRate { get; set; } = "0";
    public string FoodCapacityGranted { get; set; } = "0";
    public string PowerProductionRate { get; set; } = "0";
    public string PowerConsumptionRate { get; set; } = "0";
    public string LogisticsProductionRate { get; set; } = "0";
    public string LogisticsConsumptionRate { get; set; } = "0";
    public string ResearchPowerGranted { get; set; } = "0";
    public string PopulationCapacityGranted { get; set; } = "0";
    public string FleetPowerGranted { get; set; } = "0";
    public string AttackPowerGranted { get; set; } = "0";
    public string DefensePowerGranted { get; set; } = "0";
    public string MilitaryManpowerGranted { get; set; } = "0";
    public string UpgradeToId { get; set; } = "";
    public List<AmountSnapshot> ResourceRequirements { get; set; } = new();
    public List<AmountSnapshot> ResourceGenerationRates { get; set; } = new();
    public List<AmountSnapshot> ResourceConsumptionRates { get; set; } = new();
    public List<string> RequiredResearchIds { get; set; } = new();
    public List<string> RequiredWorkshopIds { get; set; } = new();
    public string SectorId { get; set; } = "";
}

public sealed class ResearchSnapshot : DefinitionSnapshot
{
    public string BaseCost { get; set; } = "0"; public bool AdvancesTechLevel { get; set; }
    public List<string> PrerequisiteIds { get; set; } = new(); public List<AmountSnapshot> ResourceRequirements { get; set; } = new(); public List<EffectSnapshot> Effects { get; set; } = new();
}
public sealed class WorkshopSnapshot : DefinitionSnapshot
{
    public string Label { get; set; } = ""; public string Description { get; set; } = ""; public int SortOrder { get; set; }
    public List<string> RequiredResearchIds { get; set; } = new(); public List<string> RequiredWorkshopIds { get; set; } = new(); public List<AmountSnapshot> ResourceRequirements { get; set; } = new(); public List<EffectSnapshot> Effects { get; set; } = new();
}
public sealed class SectorSnapshot : DefinitionSnapshot
{
    public string StarSystemId { get; set; } = ""; public string Domain { get; set; } = ""; public string TerritoryReward { get; set; } = "0"; public string EnemyPower { get; set; } = "0"; public bool Repeatable { get; set; }
    public string ColonizationFoodPerSecond { get; set; } = "0"; public string ColonizationDurationSeconds { get; set; } = "0"; public string CampaignFoodPerSecond { get; set; } = "0"; public string CampaignProgressMultiplier { get; set; } = "1";
    public List<string> PrerequisiteSectorIds { get; set; } = new(); public List<AmountSnapshot> ResourceRewards { get; set; } = new(); public List<AmountSnapshot> OccupiedResourceRatesPerSecond { get; set; } = new(); public List<AmountSnapshot> ColonizationResourceRatesPerSecond { get; set; } = new(); public List<AmountSnapshot> CampaignResourceRatesPerSecond { get; set; } = new();
}

public sealed class AmountSnapshot { public string ResourceId { get; set; } = ""; public string Value { get; set; } = "0"; }
public sealed class EffectSnapshot { public string Type { get; set; } = ""; public string BuildingId { get; set; } = ""; public string ResourceId { get; set; } = ""; public string Value { get; set; } = "1"; }

public class StateSnapshot
{
    public string TechLevel { get; set; } = "Animal"; public string Food { get; set; } = "0"; public string FoodCapacity { get; set; } = "1";
    public List<AmountSnapshot> Resources { get; set; } = new(); public List<BuildingStateSnapshot> Buildings { get; set; } = new(); public List<ResearchStateSnapshot> Research { get; set; } = new(); public List<string> PurchasedWorkshopIds { get; set; } = new(); public List<SectorStateSnapshot> Sectors { get; set; } = new();
}
public sealed class InitialStateSnapshot : StateSnapshot { }
public class SaveStateSnapshot : StateSnapshot
{
    public int CalendarDays { get; set; } public string Population { get; set; } = "0"; public string TerritoryTotal { get; set; } = "0"; public string AttackPower { get; set; } = "0"; public string DefensePower { get; set; } = "0"; public string FleetPower { get; set; } = "0"; public string MilitaryManpower { get; set; } = "0"; public string SupplySatisfaction { get; set; } = "1"; public string PowerSatisfaction { get; set; } = "1"; public string LogisticsSatisfaction { get; set; } = "1"; public bool CampaignActive { get; set; } public string CampaignTargetSectorId { get; set; } = ""; public string CampaignCasualties { get; set; } = "0"; public string CampaignCombatRatio { get; set; } = "0"; public string ActiveResearchId { get; set; } = ""; public List<string> QueuedResearchIds { get; set; } = new(); public long LastSaveUnixSeconds { get; set; }
}
public sealed class RuntimeStateSnapshot : SaveStateSnapshot
{
    public long Tick { get; set; } public string ElapsedSeconds { get; set; } = "0"; public string CampaignProgress { get; set; } = "0"; public List<SimulationEventSnapshot> Events { get; set; } = new();
}
public sealed class BuildingStateSnapshot { public string BuildingId { get; set; } = ""; public string Amount { get; set; } = "0"; public string Efficiency { get; set; } = "1"; }
public sealed class ResearchStateSnapshot { public string ResearchId { get; set; } = ""; public string Progress { get; set; } = "0"; public bool CostPaid { get; set; } public bool Completed { get; set; } public List<AmountSnapshot> PaidResourceCosts { get; set; } = new(); }
public sealed class SectorStateSnapshot { public string SectorId { get; set; } = ""; public bool Unlocked { get; set; } public bool Occupied { get; set; } public bool ColonizationActive { get; set; } public bool CampaignActive { get; set; } public string Progress { get; set; } = "0"; public string Casualties { get; set; } = "0"; public string CombatRatio { get; set; } = "0"; public int VisitCount { get; set; } }
public sealed class SimulationEventSnapshot { public long Tick { get; set; } public string Kind { get; set; } = ""; public string Id { get; set; } = ""; public string Amount { get; set; } = "0"; public string Detail { get; set; } = ""; }
public sealed class SnapshotValidationResult { public List<string> Errors { get; } = new(); public bool IsValid => Errors.Count == 0; }
