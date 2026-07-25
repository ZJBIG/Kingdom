using System;

[Serializable]
public sealed class GameState
{
    private const string DefaultKingdomName = "鼠托邦";

    public int CalendarDays { get; private set; }
    public string KingdomName { get; private set; }
    public TechLevel TechLevel { get; private set; }
    public ExpantaNum FoodAmount { get; private set; }
    public ExpantaNum FoodCapacity { get; private set; }
    public ExpantaNum FoodProductionRate { get; private set; }
    public ExpantaNum FoodConsumptionRate { get; private set; }
    public ExpantaNum FoodSatisfaction { get; private set; }
    public ExpantaNum PowerProductionRate { get; private set; }
    public ExpantaNum PowerConsumptionRate { get; private set; }
    public ExpantaNum PowerSatisfaction { get; private set; }
    public ExpantaNum LogisticsProductionRate { get; private set; }
    public ExpantaNum LogisticsConsumptionRate { get; private set; }
    public ExpantaNum LogisticsSatisfaction { get; private set; }
    public MilitaryState Military { get; private set; }
    public CampaignState Campaign { get; private set; }
    public ExpantaNum AttackPower => Military.AttackPower;
    public ExpantaNum DefensePower => Military.DefensePower;
    public ExpantaNum FleetPower => Military.FleetPower;
    public ExpantaNum MilitaryManpower => Military.MilitaryManpower;
    public ExpantaNum SupplySatisfaction => Military.SupplySatisfaction;
    public TerritoryState Territory { get; private set; }
    public ExpantaNum TerritoryTotal => Territory.TerritoryTotal;
    public ExpantaNum TerritoryUsed => Territory.TerritoryUsed;
    public ExpantaNum AvailableTerritory => Territory.AvailableTerritory;
    public PopulationState Population { get; private set; }
    public long LastSaveUnixSeconds { get; private set; }
    public int Version { get; private set; }

    public GameState() => InitializeNew(DefaultKingdomName);

    internal void InitializeNew(string kingdomName)
    {
        CalendarDays = 0;
        KingdomName = string.IsNullOrWhiteSpace(kingdomName) ? DefaultKingdomName : kingdomName;
        TechLevel = TechLevel.Animal;
        FoodAmount = new ExpantaNum(300);
        FoodCapacity = new ExpantaNum(500);
        FoodProductionRate = ExpantaNum.Zero;
        FoodConsumptionRate = ExpantaNum.Zero;
        FoodSatisfaction = ExpantaNum.One;
        PowerProductionRate = ExpantaNum.Zero;
        PowerConsumptionRate = ExpantaNum.Zero;
        PowerSatisfaction = ExpantaNum.One;
        LogisticsProductionRate = ExpantaNum.Zero;
        LogisticsConsumptionRate = ExpantaNum.Zero;
        LogisticsSatisfaction = ExpantaNum.One;
        Military = new MilitaryState();
        Campaign = new CampaignState();
        Territory = new TerritoryState();
        Population = new PopulationState();
        LastSaveUnixSeconds = 0;
        Version++;
    }

    internal void RestoreCore(
        int calendarDays,
        string kingdomName,
        TechLevel techLevel,
        ExpantaNum foodAmount,
        long lastSaveUnixSeconds)
    {
        CalendarDays = calendarDays;
        KingdomName = string.IsNullOrWhiteSpace(kingdomName) ? DefaultKingdomName : kingdomName;
        TechLevel = techLevel;
        FoodAmount = ExpantaNum.Max(ExpantaNum.Zero, foodAmount);
        FoodSatisfaction = ExpantaNum.One;
        FoodCapacity = ExpantaNum.Max(FoodCapacity, FoodAmount);
        LastSaveUnixSeconds = lastSaveUnixSeconds;
        Version++;
    }

    internal void RestorePopulation(
        ExpantaNum population,
        ExpantaNum populationCapacity,
        ExpantaNum assignedMilitary,
        ExpantaNum growthProgress,
        ExpantaNum foodPerPerson)
    {
        if (Population == null)
            Population = new PopulationState();
        Population.Restore(
            population,
            populationCapacity,
            assignedMilitary,
            growthProgress,
            foodPerPerson);
        Version++;
    }

    internal void RestoreTerritoryTotal(ExpantaNum territoryTotal)
    {
        Territory.RestoreTotal(territoryTotal);
        Version++;
    }

    internal void RestoreCampaign(
        bool active,
        string targetSectorId,
        ExpantaNum casualties,
        ExpantaNum combatRatio)
    {
        Campaign.Restore(active, targetSectorId, casualties, combatRatio);
        Version++;
    }

    internal void BeginCampaign(string sectorId)
    {
        int previousVersion = Campaign.Version;
        Campaign.Begin(sectorId);
        if (Campaign.Version != previousVersion)
            Version++;
    }

    internal void RecordCampaignCombat(ExpantaNum combatRatio, ExpantaNum casualties)
    {
        int previousVersion = Campaign.Version;
        Campaign.RecordCombat(combatRatio, casualties);
        if (Campaign.Version != previousVersion)
            Version++;
    }

    internal void CompleteCampaign()
    {
        int previousVersion = Campaign.Version;
        Campaign.Complete();
        if (Campaign.Version != previousVersion)
            Version++;
    }

    internal void AdjustTerritoryTotal(ExpantaNum delta)
    {
        Territory.AddTotal(delta);
        Version++;
    }

    internal void ResetDerivedEconomy(ExpantaNum minimumTerritoryTotal, ExpantaNum availableProductivity)
    {
        FoodProductionRate = ExpantaNum.Zero;
        FoodConsumptionRate = ExpantaNum.Zero;
        FoodSatisfaction = ExpantaNum.One;
        PowerProductionRate = ExpantaNum.Zero;
        PowerConsumptionRate = ExpantaNum.Zero;
        PowerSatisfaction = ExpantaNum.One;
        LogisticsProductionRate = ExpantaNum.Zero;
        LogisticsConsumptionRate = ExpantaNum.Zero;
        LogisticsSatisfaction = ExpantaNum.One;
        Military.ResetDerived();
        FoodCapacity = ExpantaNum.Max(new ExpantaNum(500), FoodAmount);
        Population.ResetDerivedCapacity();
        Population.ResetDerivedWorkforce();
        Territory.ResetDerived(minimumTerritoryTotal);
        Version++;
    }

    internal void AdvanceCalendarStep()
    {
        CalendarDays++;
        Version++;
    }

    internal void AdvanceFood(double deltaSeconds)
    {
        FoodAmount = GameManager.AdvanceFood(
            FoodAmount,
            FoodProductionRate,
            FoodConsumptionRate + Population.Population * Population.FoodPerPerson,
            FoodCapacity,
            deltaSeconds);
        Version++;
    }

    internal void AdjustFoodRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta)
    {
        FoodProductionRate = ExpantaNum.Max(ExpantaNum.Zero, FoodProductionRate + productionDelta);
        FoodConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, FoodConsumptionRate + consumptionDelta);
        Version++;
    }

    internal void AdjustFoodCapacity(ExpantaNum capacityDelta)
    {
        FoodCapacity = ExpantaNum.Max(new ExpantaNum(1), FoodCapacity + capacityDelta);
        FoodAmount = ExpantaNum.Min(FoodAmount, FoodCapacity);
        Version++;
    }

    internal bool TryConsumeFood(ExpantaNum amount)
    {
        ExpantaNum cost = ExpantaNum.Max(ExpantaNum.Zero, amount);
        if (FoodAmount < cost)
            return false;
        if (cost <= ExpantaNum.Zero)
            return true;
        FoodAmount -= cost;
        Version++;
        return true;
    }

    internal void AdjustPowerRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta)
    {
        PowerProductionRate = ExpantaNum.Max(ExpantaNum.Zero, PowerProductionRate + productionDelta);
        PowerConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, PowerConsumptionRate + consumptionDelta);
        Version++;
    }

    internal void AdjustLogisticsRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta)
    {
        LogisticsProductionRate = ExpantaNum.Max(ExpantaNum.Zero, LogisticsProductionRate + productionDelta);
        LogisticsConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, LogisticsConsumptionRate + consumptionDelta);
        Version++;
    }

    internal void AdjustAttackPower(ExpantaNum delta)
    {
        if (Military.AdjustAttackPower(delta))
            Version++;
    }

    internal void AdjustDefensePower(ExpantaNum delta)
    {
        if (Military.AdjustDefensePower(delta))
            Version++;
    }

    internal void AdjustFleetPower(ExpantaNum delta)
    {
        if (Military.AdjustFleetPower(delta))
            Version++;
    }

    internal void AdjustMilitaryManpower(ExpantaNum delta)
    {
        if (Military.AdjustMilitaryManpower(delta))
            Version++;
    }

    internal void SetSupplySatisfaction(ExpantaNum value)
    {
        int previousVersion = Military.Version;
        Military.SetSupplySatisfaction(value);
        if (Military.Version != previousVersion)
            Version++;
    }

    internal void SetPowerSatisfaction(ExpantaNum value)
    {
        ExpantaNum normalized = ExpantaNum.Clamp01(value);
        if (PowerSatisfaction == normalized)
            return;
        PowerSatisfaction = normalized;
        Version++;
    }

    internal void SetLogisticsSatisfaction(ExpantaNum value)
    {
        ExpantaNum normalized = ExpantaNum.Clamp01(value);
        if (LogisticsSatisfaction == normalized)
            return;
        LogisticsSatisfaction = normalized;
        Version++;
    }

    internal void AdjustPopulationCapacity(ExpantaNum capacityDelta)
    {
        Population.AdjustPopulationCapacity(capacityDelta);
        Version++;
    }

    internal void AdvancePopulationGrowth(double deltaSeconds)
    {
        int previousVersion = Population.Version;
        Population.AdvanceGrowth(deltaSeconds, FoodSatisfaction);
        if (Population.Version != previousVersion)
            Version++;
    }

    internal void SetFoodSatisfaction(ExpantaNum value)
    {
        ExpantaNum normalized = ExpantaNum.Clamp01(value);
        if (FoodSatisfaction == normalized)
            return;

        FoodSatisfaction = normalized;
        Version++;
    }

    internal void AdvanceTechLevel(TechLevel target)
    {
        if (target <= TechLevel)
            return;
        TechLevel = target;
        Version++;
    }

    internal void CommitConstruction(
        ExpantaNum spaceCost,
        ExpantaNum buildEffort,
        ExpantaNum productivityGranted)
    {
        Territory.AdjustUsed(spaceCost);
        Population.AdjustBuildingWorkforce(buildEffort);
        Version++;
    }

    internal void RefundConstruction(
        ExpantaNum spaceCost,
        ExpantaNum buildEffort,
        ExpantaNum productivityGranted)
    {
        Territory.AdjustUsed(-spaceCost);
        Population.AdjustBuildingWorkforce(-buildEffort);
        Version++;
    }

    internal void MarkSaved(long unixSeconds)
    {
        LastSaveUnixSeconds = unixSeconds;
        Version++;
    }
}
