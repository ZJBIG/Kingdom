using System;

[Serializable]
public sealed class GameState
{
    public static ExpantaNum BaseFoodProductionRate => new ExpantaNum(5);

    private const string DefaultKingdomName = "鼠托邦";

    public int CalendarDays { get; private set; }
    public string KingdomName { get; private set; }
    public TechLevel TechLevel { get; private set; }
    public ExpantaNum FoodAmount { get; private set; }
    public ExpantaNum FoodCapacity { get; private set; }
    public ExpantaNum FoodProductionRate { get; private set; }
    public ExpantaNum FoodConsumptionRate { get; private set; }
    public ExpantaNum FoodPopulationConsumptionRate =>
        Population == null
            ? ExpantaNum.Zero
            : Population.Population * PopulationState.FoodConsumptionPerPerson;
    public ExpantaNum FoodTotalConsumptionRate =>
        FoodConsumptionRate + FoodPopulationConsumptionRate;
    public ExpantaNum FoodNetRate =>
        FoodProductionRate - FoodTotalConsumptionRate;
    public ExpantaNum FoodSurplusPerPerson =>
        HappinessFormula.CalculateSurplusPerPerson(
            FoodNetRate,
            Population?.Population ?? ExpantaNum.Zero);
    public ExpantaNum HappinessScore =>
        HappinessFormula.CalculateScore(FoodSurplusPerPerson);
    public ExpantaNum FoodAvailability { get; private set; }
    public ExpantaNum HappinessMultiplier =>
        HappinessFormula.CalculateMultiplier(
            FoodNetRate,
            Population?.Population ?? ExpantaNum.Zero,
            FoodAvailability);
    public ExpantaNum HappinessConstraintMultiplier =>
        HappinessFormula.CalculateConstraintMultiplier(HappinessMultiplier);
    public ExpantaNum HappinessRewardMultiplier =>
        HappinessFormula.CalculateRewardMultiplier(HappinessMultiplier);
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
        FoodProductionRate = BaseFoodProductionRate;
        FoodConsumptionRate = ExpantaNum.Zero;
        FoodAvailability = ExpantaNum.One;
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
        FoodAvailability = ExpantaNum.One;
        FoodCapacity = ExpantaNum.Max(FoodCapacity, FoodAmount);
        LastSaveUnixSeconds = lastSaveUnixSeconds;
        Version++;
    }

    internal void RestorePopulation(ExpantaNum population)
    {
        if (Population == null)
            Population = new PopulationState();
        Population.RestorePopulation(population);
        Version++;
    }

    internal void RestorePopulationChangeProgress(ExpantaNum progress)
    {
        int previousVersion = Population.Version;
        Population.RestorePopulationChangeProgress(progress);
        if (Population.Version != previousVersion)
            Version++;
    }

    internal void AdvancePopulation(
        double deltaSeconds,
        ExpantaNum growthRatePerSecond,
        ExpantaNum departureAllowance)
    {
        int previousVersion = Population.Version;
        Population.AdvancePopulation(
            deltaSeconds,
            HappinessMultiplier,
            growthRatePerSecond,
            departureAllowance);
        if (Population.Version != previousVersion)
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

    internal ExpantaNum RepairCampaignFleet(ExpantaNum amount)
    {
        int previousVersion = Campaign.Version;
        ExpantaNum repaired = Campaign.Repair(amount);
        if (Campaign.Version != previousVersion)
            Version++;
        return repaired;
    }

    internal void CompleteCampaign()
    {
        int previousVersion = Campaign.Version;
        Campaign.Complete();
        if (Campaign.Version != previousVersion)
            Version++;
    }

    internal void CancelCampaign()
    {
        int previousVersion = Campaign.Version;
        Campaign.Cancel();
        if (Campaign.Version != previousVersion)
            Version++;
    }

    internal void AdjustTerritoryTotal(ExpantaNum delta)
    {
        int previousVersion = Territory.Version;
        Territory.AddTotal(delta);
        if (Territory.Version != previousVersion)
            Version++;
    }

    internal void ResetDerivedEconomy(ExpantaNum minimumTerritoryTotal)
    {
        FoodProductionRate = BaseFoodProductionRate;
        FoodConsumptionRate = ExpantaNum.Zero;
        FoodAvailability = ExpantaNum.One;
        PowerProductionRate = ExpantaNum.Zero;
        PowerConsumptionRate = ExpantaNum.Zero;
        PowerSatisfaction = ExpantaNum.One;
        LogisticsProductionRate = ExpantaNum.Zero;
        LogisticsConsumptionRate = ExpantaNum.Zero;
        LogisticsSatisfaction = ExpantaNum.One;
        Military.ResetDerived();
        FoodCapacity = ExpantaNum.Max(new ExpantaNum(500), FoodAmount);
        Population.ResetDerivedCapacity();
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
        ExpantaNum previousAmount = FoodAmount;
        FoodAmount = GameManager.AdvanceFood(
            FoodAmount,
            FoodProductionRate,
            FoodTotalConsumptionRate,
            FoodCapacity,
            deltaSeconds);
        if (FoodAmount != previousAmount)
            Version++;
    }

    internal void AdjustFoodRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta)
    {
        ExpantaNum newProductionRate = ExpantaNum.Max(
            ExpantaNum.Zero,
            FoodProductionRate + productionDelta);
        ExpantaNum newConsumptionRate = ExpantaNum.Max(
            ExpantaNum.Zero,
            FoodConsumptionRate + consumptionDelta);
        if (FoodProductionRate == newProductionRate &&
            FoodConsumptionRate == newConsumptionRate)
            return;

        FoodProductionRate = newProductionRate;
        FoodConsumptionRate = newConsumptionRate;
        Version++;
    }

    internal void AdjustFoodCapacity(ExpantaNum capacityDelta)
    {
        ExpantaNum newCapacity = ExpantaNum.Max(new ExpantaNum(1), FoodCapacity + capacityDelta);
        ExpantaNum newAmount = ExpantaNum.Min(FoodAmount, newCapacity);
        if (FoodCapacity == newCapacity && FoodAmount == newAmount)
            return;

        FoodCapacity = newCapacity;
        FoodAmount = newAmount;
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
        ExpantaNum newProductionRate = ExpantaNum.Max(
            ExpantaNum.Zero,
            PowerProductionRate + productionDelta);
        ExpantaNum newConsumptionRate = ExpantaNum.Max(
            ExpantaNum.Zero,
            PowerConsumptionRate + consumptionDelta);
        if (PowerProductionRate == newProductionRate &&
            PowerConsumptionRate == newConsumptionRate)
            return;

        PowerProductionRate = newProductionRate;
        PowerConsumptionRate = newConsumptionRate;
        Version++;
    }

    internal void AdjustLogisticsRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta)
    {
        ExpantaNum newProductionRate = ExpantaNum.Max(
            ExpantaNum.Zero,
            LogisticsProductionRate + productionDelta);
        ExpantaNum newConsumptionRate = ExpantaNum.Max(
            ExpantaNum.Zero,
            LogisticsConsumptionRate + consumptionDelta);
        if (LogisticsProductionRate == newProductionRate &&
            LogisticsConsumptionRate == newConsumptionRate)
            return;

        LogisticsProductionRate = newProductionRate;
        LogisticsConsumptionRate = newConsumptionRate;
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

    internal void RestoreMilitary(
        ExpantaNum attackPower,
        ExpantaNum defensePower,
        ExpantaNum fleetPower,
        ExpantaNum militaryManpower,
        ExpantaNum supplySatisfaction,
        ExpantaNum powerSatisfaction,
        ExpantaNum logisticsSatisfaction)
    {
        Military.Restore(
            attackPower,
            defensePower,
            fleetPower,
            militaryManpower,
            supplySatisfaction);
        PowerSatisfaction = ExpantaNum.Clamp01(powerSatisfaction);
        LogisticsSatisfaction = ExpantaNum.Clamp01(logisticsSatisfaction);
        Version++;
    }

    internal void AdjustPopulationCapacity(ExpantaNum capacityDelta)
    {
        int previousVersion = Population.Version;
        Population.AdjustPopulationCapacity(capacityDelta);
        if (Population.Version != previousVersion)
            Version++;
    }

    internal void SetFoodAvailability(ExpantaNum value)
    {
        ExpantaNum normalized = ExpantaNum.Clamp01(value);
        if (FoodAvailability == normalized)
            return;

        FoodAvailability = normalized;
        Version++;
    }

    internal void AdvanceTechLevel(TechLevel target)
    {
        if (target <= TechLevel)
            return;
        TechLevel = target;
        Version++;
    }

    internal void CommitConstruction(ExpantaNum spaceCost)
    {
        Territory.AdjustUsed(spaceCost);
        Version++;
    }

    internal void RefundConstruction(ExpantaNum spaceCost)
    {
        Territory.AdjustUsed(-spaceCost);
        Version++;
    }

    internal void MarkSaved(long unixSeconds)
    {
        if (LastSaveUnixSeconds == unixSeconds)
            return;
        LastSaveUnixSeconds = unixSeconds;
        Version++;
    }
}
