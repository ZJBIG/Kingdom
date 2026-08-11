using System;
using System.Collections.Generic;
using System.Linq;

namespace Kingdom.EconomySimulation;

public enum DefinitionKind
{
    Resource,
    Building,
    Research,
    Workshop
}

public enum SimEffectKind
{
    BuildingProductionMultiplier = 1,
    BuildingFoodProductionMultiplier = 2,
    ResourceProductionMultiplier = 3,
    GlobalResearchMultiplier = 4,
    GlobalConstructionMultiplier = 5,
    FoodCapacityMultiplier = 6,
    ProductivityGranted = 7,
    TerritoryGranted = 8,
    MilitaryMultiplier = 10,
    PowerMultiplier = 11,
    GlobalBuildingProductionMultiplier = 12,
    BuildingResearchPowerMultiplier = 13,
    BuildingPowerProductionMultiplier = 14,
    BuildingLogisticsProductionMultiplier = 15,
    GlobalLogisticsMultiplier = 16,
    PopulationGrowthMultiplier = 17,
    UnlockIndustrialWorkshop = 18,
    UnlockFirstContact = 19,
    UnlockDeepSpaceFleet = 20,
    UnlockInterstellarNavigation = 21,
    DeconstructionReturnRate = 22,
    FleetRepairCostMultiplier = 23,
    OccupiedResourceProductionMultiplier = 24,
    CampaignProgressMultiplier = 25,
    CampaignSupplyCostMultiplier = 26,
    CampaignCasualtyMultiplier = 27,
    PopulationProductivityMultiplier = 28,
    ExplorationPowerMultiplier = 29,
    BuildingConstructionMultiplier = 30
}

public sealed class Definition
{
    public string Id = "", UpgradeTo = "";
    public DefinitionKind Kind;
    public SimTechLevel TechLevel;
    public bool AdvancesTechLevel;
    public double BaseCost, CostGrowth = 1.15, SpaceCost, ResearchPower,
        FoodProduction, FoodConsumption, PowerProduction, PowerConsumption,
        LogisticsProduction, LogisticsConsumption, ProductivityConsumption,
        ProductivityGranted, PopulationCapacity, FoodCapacity;
    public readonly List<string> Prerequisites = new();
    public readonly List<string> RequiredResearch = new();
    public readonly List<string> RequiredWorkshop = new();
    public readonly List<string> RequiredUpgrades = new();
    public readonly Dictionary<string, double> ResourceRequirements =
        new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, double> Generation =
        new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, double> Consumption =
        new(StringComparer.OrdinalIgnoreCase);
    public readonly List<SimEffect> Effects = new();
}

public sealed class SimEffect
{
    public SimEffectKind Kind;
    public string Target = "";
    public double Value;
}

public sealed class EconomySnapshot
{
    public EconomySnapshot(IEnumerable<Definition> definitions)
    {
        All = definitions.OrderBy(x => x.Kind).ThenBy(
            x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        Resources = OfKind(DefinitionKind.Resource);
        Buildings = OfKind(DefinitionKind.Building);
        Research = OfKind(DefinitionKind.Research);
        Workshops = OfKind(DefinitionKind.Workshop);
    }

    public IReadOnlyList<Definition> All { get; }
    public IReadOnlyList<Definition> Resources { get; }
    public IReadOnlyList<Definition> Buildings { get; }
    public IReadOnlyList<Definition> Research { get; }
    public IReadOnlyList<Definition> Workshops { get; }

    public Definition Find(string id, DefinitionKind? kind = null)
    {
        Definition[] matches = All.Where(x =>
            string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) &&
            (!kind.HasValue || x.Kind == kind.Value)).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException(
                $"Definition '{id}' was not loaded."),
            _ => throw new InvalidOperationException(
                $"Definition ID '{id}' is ambiguous; specify its kind.")
        };
    }

    private IReadOnlyList<Definition> OfKind(DefinitionKind kind) =>
        All.Where(x => x.Kind == kind).ToArray();
}
