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
    private const double SecondsPerDay = 10d;
    private static readonly ExpantaNum UncappedFoodCeiling = new ExpantaNum("1e1000000");

    public GameState State { get; private set; } = new GameState();
    public SectorManager Sectors { get; } = new SectorManager();
    public ExpantaNum PopulationGrowthMultiplier =>
        ProgressionModifierManager.Current.PopulationGrowthMultiplier;
    public ExpantaNum PopulationGrowthRatePerSecond =>
        PopulationState.BaseGrowthRatePerSecond * PopulationGrowthMultiplier;
    public ExpantaNum CurrentPopulationGrowthRatePerSecond =>
        State.Population.CurrentGrowthRatePerSecond(
            State.HappinessMultiplier,
            PopulationGrowthRatePerSecond);
    public ExpantaNum CurrentPopulationDepartureRatePerSecond
    {
        get
        {
            BuildingManager buildingManager = FindObjectOfType<BuildingManager>();
            ExpantaNum allowance = buildingManager == null
                ? ExpantaNum.Zero
                : buildingManager.SafePopulationDepartureAllowance;
            return State.Population.CurrentDepartureRatePerSecond(
                allowance,
                State.HappinessMultiplier,
                CanPopulationLeaveForFoodShortage());
        }
    }
    public ExpantaNum CurrentPopulationNetRatePerSecond =>
        CanPopulationLeaveForFoodShortage()
            ? -CurrentPopulationDepartureRatePerSecond
            : State.Population.Population < State.Population.PopulationCapacity
                ? CurrentPopulationGrowthRatePerSecond
                : ExpantaNum.Zero;

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
        State.InitializeNew();
        Sectors.InitializeNew();
        ResetCalendarAccumulator();
        InitializeStartingResources();
    }

    internal void InitializeStartingResources()
    {
        ResourceManager.Instance.EnsureStartingResource();
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
        Tick(deltaSeconds, ExpantaNum.Zero);
    }

    public void Tick(
        double deltaSeconds,
        ExpantaNum populationDepartureAllowance)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        State.AdvanceFood(deltaSeconds);
        ExpantaNum populationGrowthRate = PopulationGrowthRatePerSecond;
        bool foodShortage = CanPopulationLeaveForFoodShortage();
        State.AdvancePopulation(
            deltaSeconds,
            populationGrowthRate,
            populationDepartureAllowance,
            foodShortage);
        calendarElapsedSeconds += deltaSeconds;
        while (calendarElapsedSeconds >= SecondsPerDay)
        {
            State.AdvanceCalendarStep();
            calendarElapsedSeconds -= SecondsPerDay;
        }
    }

    internal void TickOffline(
        double calendarSeconds,
        double simulationSeconds,
        ExpantaNum populationDepartureAllowance)
    {
        if (double.IsNaN(calendarSeconds) || double.IsInfinity(calendarSeconds) || calendarSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(calendarSeconds));
        if (double.IsNaN(simulationSeconds) || double.IsInfinity(simulationSeconds) || simulationSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(simulationSeconds));

        State.AdvanceFood(simulationSeconds);
        ExpantaNum populationGrowthRate = PopulationGrowthRatePerSecond;
        bool foodShortage = CanPopulationLeaveForFoodShortage();
        State.AdvancePopulation(
            simulationSeconds,
            populationGrowthRate,
            populationDepartureAllowance,
            foodShortage);
        calendarElapsedSeconds += calendarSeconds;
        while (calendarElapsedSeconds >= SecondsPerDay)
        {
            State.AdvanceCalendarStep();
            calendarElapsedSeconds -= SecondsPerDay;
        }
    }

    private bool CanPopulationLeaveForFoodShortage() =>
        State.FoodAmount <= ExpantaNum.Zero && State.FoodNetRate < ExpantaNum.Zero;

    public static ExpantaNum AdvanceFood(
        ExpantaNum current,
        ExpantaNum productionRate,
        ExpantaNum consumptionRate,
        double deltaSeconds)
        => AdvanceFood(current, productionRate, consumptionRate, ExpantaNum.Max(current, UncappedFoodCeiling), deltaSeconds);

    public static ExpantaNum AdvanceFood(
        ExpantaNum current,
        ExpantaNum productionRate,
        ExpantaNum consumptionRate,
        ExpantaNum capacity,
        double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        return ExpantaNum.Min(
            ExpantaNum.Max(
            ExpantaNum.Zero,
            current + (productionRate - consumptionRate) * deltaSeconds),
            ExpantaNum.Max(ExpantaNum.Zero, capacity));
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

    internal void PrepareHappiness(
        ExpantaNum potentialProductionRate,
        ExpantaNum potentialConsumptionRate,
        double deltaSeconds)
    {
        potentialProductionRate =
            (potentialProductionRate + GameState.BaseFoodProductionRate) *
            ProgressionModifierManager.Current.GlobalFoodProductionMultiplier;
        potentialConsumptionRate +=
            State.Population.Population * PopulationState.FoodConsumptionPerPerson;
        State.SetFoodAvailability(HappinessFormula.CalculateFoodAvailability(
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

    public void CommitConstruction(ExpantaNum territoryCost) =>
        State.CommitConstruction(territoryCost);

    public void RefundConstruction(ExpantaNum territoryCost) =>
        State.RefundConstruction(territoryCost);

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

    internal void MarkSaveTimestamp(long unixSeconds) => State.MarkSaved(unixSeconds);

    internal void ResetDerivedEconomy() =>
        State.ResetDerivedEconomy(TerritoryState.InitialTotal);

    internal SaveManager.GameSaveData CaptureSaveData()
    {
        return new SaveManager.GameSaveData
        {
            CalendarDays = State.CalendarDays,
            TechLevel = State.TechLevel,
            FoodAmount = State.FoodAmount.ToString(),
            Population = State.Population.Population.ToString(),
            PopulationChangeProgress =
                State.Population.PopulationChangeProgress.ToString(),
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
            CalendarElapsedSeconds = calendarElapsedSeconds,
            LastSaveUnixSeconds = State.LastSaveUnixSeconds
        };
    }

    internal void RestoreSaveData(SaveManager.GameSaveData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        ValidateSaveData(data);
        if (data.CampaignActive && string.IsNullOrWhiteSpace(data.CampaignTargetSectorId))
            throw new System.IO.InvalidDataException("活动战役存档缺少目标星区编号。");
        if (!string.IsNullOrWhiteSpace(data.CampaignTargetSectorId) &&
            !DataBase<SectorDefinition>.TryFind(data.CampaignTargetSectorId, out _))
            throw new System.IO.InvalidDataException(
                $"存档包含未知战役目标星区“{data.CampaignTargetSectorId}”。");

        State.RestoreCore(
            data.CalendarDays,
            data.TechLevel,
            Parse(data.FoodAmount, nameof(data.FoodAmount)),
            data.LastSaveUnixSeconds);
        if (!string.IsNullOrWhiteSpace(data.Population))
        {
            State.RestorePopulation(
                Parse(data.Population, nameof(data.Population)));
        }
        if (!string.IsNullOrWhiteSpace(data.TerritoryTotal))
            State.RestoreTerritoryTotal(Parse(data.TerritoryTotal, nameof(data.TerritoryTotal)));
        State.RestoreCampaign(
            data.CampaignActive,
            data.CampaignTargetSectorId,
            ParseOptional(data.CampaignCasualties, ExpantaNum.Zero, nameof(data.CampaignCasualties)),
            ParseOptional(data.CampaignCombatRatio, ExpantaNum.Zero, nameof(data.CampaignCombatRatio)));
        calendarElapsedSeconds = data.CalendarElapsedSeconds;
    }

    private static void ValidateSaveData(SaveManager.GameSaveData data)
    {
        if (data.CalendarDays < 0)
            throw new System.IO.InvalidDataException("存档的 CalendarDays 不能为负数。");
        if (double.IsNaN(data.CalendarElapsedSeconds) ||
            double.IsInfinity(data.CalendarElapsedSeconds) ||
            data.CalendarElapsedSeconds < 0d ||
            data.CalendarElapsedSeconds >= SecondsPerDay)
            throw new System.IO.InvalidDataException(
                "存档的 CalendarElapsedSeconds 必须在 0 到一天以内。");
        if (!Enum.IsDefined(typeof(TechLevel), data.TechLevel))
            throw new System.IO.InvalidDataException(
                $"存档包含未知时代值“{(int)data.TechLevel}”。");

        ValidateNonNegative(Parse(data.FoodAmount, nameof(data.FoodAmount)), nameof(data.FoodAmount));
        ValidateOptionalNonNegative(data.Population, nameof(data.Population));
        ValidateOptionalNonNegative(data.TerritoryTotal, nameof(data.TerritoryTotal));
        ValidateOptionalNonNegative(data.AttackPower, nameof(data.AttackPower));
        ValidateOptionalNonNegative(data.DefensePower, nameof(data.DefensePower));
        ValidateOptionalNonNegative(data.FleetPower, nameof(data.FleetPower));
        ValidateOptionalNonNegative(data.MilitaryManpower, nameof(data.MilitaryManpower));
        ValidateOptionalUnitInterval(data.PopulationChangeProgress, nameof(data.PopulationChangeProgress));
        ValidateOptionalUnitInterval(data.SupplySatisfaction, nameof(data.SupplySatisfaction));
        ValidateOptionalUnitInterval(data.PowerSatisfaction, nameof(data.PowerSatisfaction));
        ValidateOptionalUnitInterval(data.LogisticsSatisfaction, nameof(data.LogisticsSatisfaction));
        ValidateOptionalNonNegative(data.CampaignCasualties, nameof(data.CampaignCasualties));
        ValidateOptionalNonNegative(data.CampaignCombatRatio, nameof(data.CampaignCombatRatio));
        if (data.LastSaveUnixSeconds < 0L)
            throw new System.IO.InvalidDataException("存档的 LastSaveUnixSeconds 不能为负数。");
        if (string.IsNullOrWhiteSpace(data.CampaignTargetSectorId) &&
            (!string.IsNullOrWhiteSpace(data.CampaignCasualties) &&
             Parse(data.CampaignCasualties, nameof(data.CampaignCasualties)) > ExpantaNum.Zero ||
             !string.IsNullOrWhiteSpace(data.CampaignCombatRatio) &&
             Parse(data.CampaignCombatRatio, nameof(data.CampaignCombatRatio)) > ExpantaNum.Zero))
            throw new System.IO.InvalidDataException("无目标战役存档不能保留伤亡或战斗比。");
    }

    private static void ValidateOptionalNonNegative(string raw, string field)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;
        ValidateNonNegative(Parse(raw, field), field);
    }

    private static void ValidateOptionalUnitInterval(string raw, string field)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;
        ExpantaNum value = Parse(raw, field);
        if (!value.IsFinite || value < ExpantaNum.Zero || value > ExpantaNum.One)
            throw new System.IO.InvalidDataException(
                $"存档的 {field} 必须是 0 到 1 之间的有限值。");
    }

    private static void ValidateNonNegative(ExpantaNum value, string field)
    {
        if (!value.IsFinite || value < ExpantaNum.Zero)
            throw new System.IO.InvalidDataException(
                $"存档的 {field} 必须是非负有限值。");
    }

    internal void RestorePopulationChangeProgress(
        SaveManager.GameSaveData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        State.RestorePopulationChangeProgress(ParseOptional(
            data.PopulationChangeProgress,
            ExpantaNum.Zero,
            nameof(data.PopulationChangeProgress)));
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
        throw new FormatException($"GameState.{field} 中的 ExpantaNum 值“{raw}”无效。");
    }

    private static ExpantaNum ParseOptional(string raw, ExpantaNum fallback, string field)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        return Parse(raw, field);
    }
}
