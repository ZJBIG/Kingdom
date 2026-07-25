using System;
using System.ComponentModel;
using UnityEngine;

public enum TechLevel
{
    [Description("原始时代")] Animal,
    [Description("新石器时代")] Neolithic,
    [Description("中世纪")] Medieval,
    [Description("工业时代")] Industrial,
    [Description("太空时代")] Spacer,
    [Description("极致时代")] Ultra,
    [Description("远古科技时代")] Archotech
}

public class GameManager : Singleton<GameManager>
{
    private const float CalendarUpdateInterval = 10f;
    private const string DefaultKingdomName = "鼠托邦";

    public GameState State { get; private set; } = new GameState();
    public SectorManager Sectors { get; } = new SectorManager();

    private double calendarElapsedSeconds;

    private void Start()
    {
        if (Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer)
        {
            Application.runInBackground = false;
        }
    }

    internal void InitializeNewGame()
    {
        State.InitializeNew(DefaultKingdomName);
        Sectors.InitializeNew();
        ResetCalendarAccumulator();
        InitializeStartingResources();
    }

    internal void InitializeStartingResources()
    {
        Resource woodLog = DataBase<Resource>.Find("WoodLog");
        ResourceManager.Instance.AddResource(woodLog);
        ResourceManager.Instance.SetProductionRate(woodLog, 1);
    }

    public static (int Year, int Month, int Day) CalendarIntToData(
        int totalDays,
        int baseYear = 5500,
        int daysPerMonth = 30,
        int monthsPerYear = 12)
    {
        if (daysPerMonth <= 0)
            throw new ArgumentOutOfRangeException(nameof(daysPerMonth));
        if (monthsPerYear <= 0)
            throw new ArgumentOutOfRangeException(nameof(monthsPerYear));

        int daysPerYear = monthsPerYear * daysPerMonth;
        int yearsOffset = Math.DivRem(totalDays, daysPerYear, out int remainingDays);
        if (remainingDays < 0)
        {
            yearsOffset--;
            remainingDays += daysPerYear;
        }

        int month = remainingDays / daysPerMonth + 1;
        int day = remainingDays % daysPerMonth + 1;
        return (baseYear + yearsOffset, month, day);
    }

    public static string CalendarDataToString(int totalDays)
    {
        var date = CalendarIntToData(totalDays);
        return $"{date.Year}/{date.Month}/{date.Day}";
    }

    public void Tick(double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        State.AdvanceFood(deltaSeconds);
        State.AdvancePopulationGrowth(deltaSeconds);
        calendarElapsedSeconds += deltaSeconds;
        while (calendarElapsedSeconds >= CalendarUpdateInterval)
        {
            State.AdvanceCalendarStep();
            calendarElapsedSeconds -= CalendarUpdateInterval;
        }
    }

    public static ExpantaNum AdvanceFood(
        ExpantaNum current,
        ExpantaNum productionRate,
        ExpantaNum consumptionRate,
        double deltaSeconds)
        => AdvanceFood(current, productionRate, consumptionRate, ExpantaNum.Max(current, new ExpantaNum("1e1000000")), deltaSeconds);

    public static ExpantaNum AdvanceFood(
        ExpantaNum current,
        ExpantaNum productionRate,
        ExpantaNum consumptionRate,
        ExpantaNum capacity,
        double deltaSeconds)
    {
        if (deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        return ExpantaNum.Min(
            ExpantaNum.Max(
            ExpantaNum.Zero,
            current + (productionRate - consumptionRate) * deltaSeconds),
            ExpantaNum.Max(ExpantaNum.Zero, capacity));
    }

    public static ExpantaNum CalculateFoodSatisfaction(
        ExpantaNum currentInventory,
        ExpantaNum potentialProductionRate,
        ExpantaNum potentialConsumptionRate,
        double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        ExpantaNum available = ExpantaNum.Max(ExpantaNum.Zero, currentInventory) +
            ExpantaNum.Max(ExpantaNum.Zero, potentialProductionRate) * deltaSeconds;
        ExpantaNum demand = ExpantaNum.Max(ExpantaNum.Zero, potentialConsumptionRate) * deltaSeconds;
        if (demand <= ExpantaNum.Zero)
            return ExpantaNum.One;

        return ExpantaNum.Clamp01(available / demand);
    }

    public static ExpantaNum CalculateFlowSatisfaction(
        ExpantaNum potentialProductionRate,
        ExpantaNum potentialConsumptionRate)
    {
        ExpantaNum production = ExpantaNum.Max(ExpantaNum.Zero, potentialProductionRate);
        ExpantaNum demand = ExpantaNum.Max(ExpantaNum.Zero, potentialConsumptionRate);
        if (demand <= ExpantaNum.Zero)
            return ExpantaNum.One;
        return ExpantaNum.Clamp01(production / demand);
    }

    internal void PrepareFoodSatisfaction(
        ExpantaNum potentialProductionRate,
        ExpantaNum potentialConsumptionRate,
        double deltaSeconds)
    {
        potentialConsumptionRate += State.Population.Population * State.Population.FoodPerPerson;
        State.SetFoodSatisfaction(CalculateFoodSatisfaction(
            State.FoodAmount,
            potentialProductionRate,
            potentialConsumptionRate,
            deltaSeconds));
    }

    internal void PrepareFlowSatisfaction(
        ExpantaNum potentialPowerProductionRate,
        ExpantaNum potentialPowerConsumptionRate,
        ExpantaNum potentialLogisticsProductionRate,
        ExpantaNum potentialLogisticsConsumptionRate)
    {
        State.SetPowerSatisfaction(CalculateFlowSatisfaction(
            potentialPowerProductionRate,
            potentialPowerConsumptionRate));
        State.SetLogisticsSatisfaction(CalculateFlowSatisfaction(
            potentialLogisticsProductionRate,
            potentialLogisticsConsumptionRate));
    }

    public bool CanAffordConstruction(ExpantaNum territoryCost, ExpantaNum buildEffort) =>
        State.AvailableTerritory >= territoryCost && State.Population.AvailableWorkforce >= buildEffort;

    public void CommitConstruction(
        ExpantaNum territoryCost,
        ExpantaNum buildEffort,
        ExpantaNum productivityGranted) =>
        State.CommitConstruction(territoryCost, buildEffort, productivityGranted);

    public void RefundConstruction(
        ExpantaNum territoryCost,
        ExpantaNum buildEffort,
        ExpantaNum productivityGranted) =>
        State.RefundConstruction(territoryCost, buildEffort, productivityGranted);

    public void AdjustTerritoryTotal(ExpantaNum delta) => State.AdjustTerritoryTotal(delta);

    public void AdjustFoodRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta) =>
        State.AdjustFoodRates(productionDelta, consumptionDelta);

    public void AdjustFoodCapacity(ExpantaNum capacityDelta) =>
        State.AdjustFoodCapacity(capacityDelta);

    public void AdjustPowerRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta) =>
        State.AdjustPowerRates(productionDelta, consumptionDelta);

    public void AdjustLogisticsRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta) =>
        State.AdjustLogisticsRates(productionDelta, consumptionDelta);

    public void AdjustFleetPower(ExpantaNum delta) => State.AdjustFleetPower(delta);

    public void AdjustAttackPower(ExpantaNum delta) => State.AdjustAttackPower(delta);

    public void AdjustDefensePower(ExpantaNum delta) => State.AdjustDefensePower(delta);

    public void AdjustMilitaryManpower(ExpantaNum delta) => State.AdjustMilitaryManpower(delta);

    public void SetSupplySatisfaction(ExpantaNum value) => State.SetSupplySatisfaction(value);

    public void AdjustPopulationCapacity(ExpantaNum capacityDelta) =>
        State.AdjustPopulationCapacity(capacityDelta);

    internal void AdvanceTechLevel(TechLevel target) => State.AdvanceTechLevel(target);

    internal void ResetCalendarAccumulator() => calendarElapsedSeconds = 0d;

    internal void ResetDerivedEconomy() =>
        State.ResetDerivedEconomy(new ExpantaNum(100), new ExpantaNum(15));

    internal SaveManager.GameSaveData CaptureSaveData()
    {
        State.MarkSaved(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return new SaveManager.GameSaveData
        {
            CalendarDays = State.CalendarDays,
            KingdomName = State.KingdomName,
            TechLevel = State.TechLevel,
            FoodAmount = State.FoodAmount.ToString(),
            Population = State.Population.Population.ToString(),
            PopulationCapacity = State.Population.PopulationCapacity.ToString(),
            AssignedMilitary = State.Population.AssignedMilitary.ToString(),
            GrowthProgress = State.Population.GrowthProgress.ToString(),
            FoodPerPerson = State.Population.FoodPerPerson.ToString(),
            TerritoryTotal = State.TerritoryTotal.ToString(),
            AttackPower = State.AttackPower.ToString(),
            DefensePower = State.DefensePower.ToString(),
            FleetPower = State.FleetPower.ToString(),
            MilitaryManpower = State.MilitaryManpower.ToString(),
            SupplySatisfaction = State.SupplySatisfaction.ToString(),
            PowerSatisfaction = State.PowerSatisfaction.ToString(),
            LogisticsSatisfaction = State.LogisticsSatisfaction.ToString(),
            CampaignActive = State.Campaign.Active,
            CampaignTargetSectorId = State.Campaign.TargetSectorId,
            CampaignCasualties = State.Campaign.Casualties.ToString(),
            CampaignCombatRatio = State.Campaign.CombatRatio.ToString(),
            LastSaveUnixSeconds = State.LastSaveUnixSeconds
        };
    }

    internal void RestoreSaveData(SaveManager.GameSaveData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        State.RestoreCore(
            data.CalendarDays,
            data.KingdomName,
            data.TechLevel,
            Parse(data.FoodAmount, nameof(data.FoodAmount)),
            data.LastSaveUnixSeconds);
        if (!string.IsNullOrWhiteSpace(data.Population))
        {
            State.RestorePopulation(
                Parse(data.Population, nameof(data.Population)),
                Parse(data.PopulationCapacity, nameof(data.PopulationCapacity)),
                Parse(data.AssignedMilitary, nameof(data.AssignedMilitary)),
                Parse(data.GrowthProgress, nameof(data.GrowthProgress)),
                Parse(data.FoodPerPerson, nameof(data.FoodPerPerson)));
        }
        if (!string.IsNullOrWhiteSpace(data.TerritoryTotal))
            State.RestoreTerritoryTotal(Parse(data.TerritoryTotal, nameof(data.TerritoryTotal)));
        State.RestoreCampaign(
            data.CampaignActive,
            data.CampaignTargetSectorId,
            data.CampaignActive
                ? ParseOptional(data.CampaignCasualties, ExpantaNum.Zero, nameof(data.CampaignCasualties))
                : ExpantaNum.Zero,
            data.CampaignActive
                ? ParseOptional(data.CampaignCombatRatio, ExpantaNum.Zero, nameof(data.CampaignCombatRatio))
                : ExpantaNum.Zero);
        ResetCalendarAccumulator();
    }

    internal void RestoreMilitarySaveData(SaveManager.GameSaveData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        State.RestoreMilitary(
            ParseOptional(data.AttackPower, State.AttackPower, nameof(data.AttackPower)),
            ParseOptional(data.DefensePower, State.DefensePower, nameof(data.DefensePower)),
            ParseOptional(data.FleetPower, State.FleetPower, nameof(data.FleetPower)),
            ParseOptional(data.MilitaryManpower, State.MilitaryManpower, nameof(data.MilitaryManpower)),
            ParseOptional(data.SupplySatisfaction, State.SupplySatisfaction, nameof(data.SupplySatisfaction)),
            ParseOptional(data.PowerSatisfaction, State.PowerSatisfaction, nameof(data.PowerSatisfaction)),
            ParseOptional(data.LogisticsSatisfaction, State.LogisticsSatisfaction, nameof(data.LogisticsSatisfaction)));
    }

    public override void Save() => SaveManager.Instance.SaveNow(true);

    public override void Load() => SaveManager.Instance.LoadOrCreateGame();

    private static ExpantaNum Parse(string raw, string field)
    {
        if (ExpantaNum.TryParse(raw, out ExpantaNum value))
            return value;
        throw new FormatException($"Invalid ExpantaNum '{raw}' for GameState.{field}.");
    }

    private static ExpantaNum ParseOptional(string raw, ExpantaNum fallback, string field)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        return Parse(raw, field);
    }
}
