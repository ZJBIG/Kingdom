using System;
using System.ComponentModel;
using UnityEngine;

public enum TechLevel
{
    Animal,
    StoneAge,
    Medieval,
    Industrial,
    Spacer,
    Ultra,
    Archotech
}

public class GameManager : Singleton<GameManager>
{
    private const double SecondsPerDay = 10d;

    public GameState State { get; private set; } = new GameState();
    public SectorManager Sectors { get; } = new SectorManager();
    public UltraProjectManager UltraProject { get; } = new UltraProjectManager();
    public RelicManager Relic { get; } = new RelicManager();
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
            BuildingManager buildingManager = cachedBuildingManager != null
                ? cachedBuildingManager
                : cachedBuildingManager = FindObjectOfType<BuildingManager>();
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
    private BuildingManager cachedBuildingManager;

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
        UltraProject.InitializeNew();
        Relic.InitializeNew();
        Sectors.InitializeNew();
        ResetCalendarAccumulator();
        InitializeStartingResources();
        InitializeStartingInventory();
    }

    internal void InitializeStartingResources()
    {
        ResourceManager.Instance.EnsureStartingResource();
    }

    private void InitializeStartingInventory()
    {
        ResourceDef woodLog = DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId);
        ResourceManager.Instance.SetAmount(woodLog, new ExpantaNum(60));
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

    internal void AdvanceWorldEvents(double seconds, bool online)
    {
        ExpantaNum previous = State.Events.ProductionMultiplier;
        State.AdvanceWorldEvents(seconds, online);
        ExpantaNum current = State.Events.ProductionMultiplier;
        if (previous != current)
            BuildingManager.Instance.ApplyEventProductionChange(previous, current);
    }

    public void Tick(
        double deltaSeconds,
        ExpantaNum populationDepartureAllowance)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        AdvanceSimulationCore(deltaSeconds, deltaSeconds, populationDepartureAllowance);
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

        AdvanceSimulationCore(simulationSeconds, calendarSeconds, populationDepartureAllowance);
    }

    private void AdvanceSimulationCore(
        double simSeconds,
        double calSeconds,
        ExpantaNum populationDepartureAllowance)
    {
        double fedSeconds = simSeconds;
        ExpantaNum foodDeficit = -State.FoodNetRate;
        if (foodDeficit > ExpantaNum.Zero &&
            State.FoodAmount < foodDeficit * simSeconds)
        {
            fedSeconds = Math.Min(simSeconds,
                Math.Max(0d, (State.FoodAmount / foodDeficit).ToDouble()));
        }
        State.AdvanceFood(simSeconds);
        ExpantaNum populationGrowthRate = PopulationGrowthRatePerSecond;
        State.AdvancePopulation(
            fedSeconds,
            populationGrowthRate,
            populationDepartureAllowance,
            false);
        double unfedSeconds = simSeconds - fedSeconds;
        if (unfedSeconds > 0d)
        {
            State.AdvancePopulation(
                unfedSeconds,
                populationGrowthRate,
                populationDepartureAllowance,
                true);
        }
        calendarElapsedSeconds += calSeconds;
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
        State.SetFoodAvailability(CalculateSegmentFoodAvailability(
            potentialProductionRate,
            potentialConsumptionRate,
            deltaSeconds));
    }

    // Simulation stops at the stock-depletion boundary before preparing the
    // next segment. Stored food therefore supports full operation in this one.
    internal ExpantaNum CalculateSegmentFoodAvailability(
        ExpantaNum production, ExpantaNum consumption, double seconds) =>
        State.FoodAmount > ExpantaNum.Zero ? ExpantaNum.One :
            HappinessFormula.CalculateFoodAvailability(
                State.FoodAmount, production, consumption, seconds);

    internal double GetFoodSegmentSeconds(double maximumSeconds)
    {
        if (maximumSeconds <= 0d || State.FoodAmount <= ExpantaNum.Zero ||
            State.FoodNetRate >= ExpantaNum.Zero)
            return maximumSeconds;
        double supported = (State.FoodAmount / -State.FoodNetRate).ToDouble();
        // A minimum positive step consumes rounding residue and guarantees
        // progress without rewinding an already committed simulation segment.
        return Math.Min(maximumSeconds, Math.Max(1e-9d, supported));
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

#if UNITY_EDITOR
    public void InitializeNewGameForEditor() => InitializeNewGame();
    public void AdvanceTechLevelForEditor(TechLevel target) => AdvanceTechLevel(target);
    public double CalendarElapsedSecondsForEditor
    {
        get => calendarElapsedSeconds;
        set => calendarElapsedSeconds = value;
    }
    public void RestoreSaveDataForEditor(SaveManager.GameSaveData data) =>
        RestoreSaveData(data);
    public void MarkSaveTimestampForEditor(long unixSeconds) =>
        MarkSaveTimestamp(unixSeconds);
    public void ResetDerivedEconomyForEditor() => ResetDerivedEconomy();
    public void RestoreMilitarySaveDataForEditor(SaveManager.GameSaveData data) =>
        RestoreMilitarySaveData(data);
#endif

    internal void ResetCalendarAccumulator() => calendarElapsedSeconds = 0d;

    internal void MarkSaveTimestamp(long unixSeconds) => State.MarkSaved(unixSeconds);

    internal void ResetDerivedEconomy() =>
        State.ResetDerivedEconomy(TerritoryState.InitialTotal);

    public SaveManager.GameSaveData CaptureSaveData()
    {
        return new SaveManager.GameSaveData
        {
            CalendarDays = State.CalendarDays,
            Events = State.Events.CaptureSaveData(),
            TechLevel = State.TechLevel,
            HasBuiltHousing = State.HasBuiltHousing,
            HasCurrentFeudalTerritoryReward = State.HasCurrentFeudalTerritoryReward,
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
            CampaignDoctrine = State.Campaign.Doctrine,
            CalendarElapsedSeconds = calendarElapsedSeconds,
            RemainingOfflineSeconds = State.RemainingOfflineSeconds,
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
            !DataBase<SectorDef>.TryFind(data.CampaignTargetSectorId, out _))
            throw new System.IO.InvalidDataException(
                $"存档包含未知战役目标星区“{data.CampaignTargetSectorId}”。");

        State.RestoreCore(
            data.CalendarDays,
            data.TechLevel,
            Parse(data.FoodAmount, nameof(data.FoodAmount)),
            data.LastSaveUnixSeconds);
        State.RestoreOfflineTime(data.RemainingOfflineSeconds);
        State.RestorePopulation(Parse(data.Population, nameof(data.Population)));
        State.RestoreHousingMilestone(data.HasBuiltHousing);
        State.RestoreFeudalTerritoryReward(data.HasCurrentFeudalTerritoryReward);
        State.Events.Restore(data.Events);
        State.RestoreTerritoryTotal(Parse(data.TerritoryTotal, nameof(data.TerritoryTotal)));
        State.RestoreCampaign(
            data.CampaignActive,
            data.CampaignTargetSectorId,
            Parse(data.CampaignCasualties, nameof(data.CampaignCasualties)),
            Parse(data.CampaignCombatRatio, nameof(data.CampaignCombatRatio)),
            data.CampaignDoctrine);
        calendarElapsedSeconds = data.CalendarElapsedSeconds;
    }

    private static void ValidateSaveData(SaveManager.GameSaveData data)
    {
        WorldEventState.Validate(data.Events);
        if (double.IsNaN(data.RemainingOfflineSeconds) || double.IsInfinity(data.RemainingOfflineSeconds) ||
            data.RemainingOfflineSeconds < 0d)
            throw new System.IO.InvalidDataException("存档的离线时间无效。");
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
        if (!Enum.IsDefined(typeof(CampaignDoctrine), data.CampaignDoctrine))
            throw new System.IO.InvalidDataException(
                $"存档包含未知远征姿态值“{(int)data.CampaignDoctrine}”。");

        ValidateNonNegative(Parse(data.FoodAmount, nameof(data.FoodAmount)), nameof(data.FoodAmount));
        ValidateNonNegative(Parse(data.Population, nameof(data.Population)), nameof(data.Population));
        ValidateNonNegative(Parse(data.TerritoryTotal, nameof(data.TerritoryTotal)), nameof(data.TerritoryTotal));
        ValidateNonNegative(Parse(data.AttackPower, nameof(data.AttackPower)), nameof(data.AttackPower));
        ValidateNonNegative(Parse(data.DefensePower, nameof(data.DefensePower)), nameof(data.DefensePower));
        ValidateNonNegative(Parse(data.FleetPower, nameof(data.FleetPower)), nameof(data.FleetPower));
        ValidateNonNegative(Parse(data.MilitaryManpower, nameof(data.MilitaryManpower)), nameof(data.MilitaryManpower));
        ValidateUnitInterval(data.PopulationChangeProgress, nameof(data.PopulationChangeProgress));
        ValidateUnitInterval(data.SupplySatisfaction, nameof(data.SupplySatisfaction));
        ValidateUnitInterval(data.PowerSatisfaction, nameof(data.PowerSatisfaction));
        ValidateUnitInterval(data.LogisticsSatisfaction, nameof(data.LogisticsSatisfaction));
        ValidateNonNegative(Parse(data.CampaignCasualties, nameof(data.CampaignCasualties)), nameof(data.CampaignCasualties));
        ValidateNonNegative(Parse(data.CampaignCombatRatio, nameof(data.CampaignCombatRatio)), nameof(data.CampaignCombatRatio));
        if (data.LastSaveUnixSeconds < 0L)
            throw new System.IO.InvalidDataException("存档的 LastSaveUnixSeconds 不能为负数。");
        if (string.IsNullOrWhiteSpace(data.CampaignTargetSectorId) &&
            (Parse(data.CampaignCasualties, nameof(data.CampaignCasualties)) > ExpantaNum.Zero ||
             Parse(data.CampaignCombatRatio, nameof(data.CampaignCombatRatio)) > ExpantaNum.Zero))
            throw new System.IO.InvalidDataException("无目标战役存档不能保留伤亡或战斗比。");
    }

    private static void ValidateUnitInterval(string raw, string field)
    {
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
        State.RestorePopulationChangeProgress(Parse(data.PopulationChangeProgress, nameof(data.PopulationChangeProgress)));
    }

    internal void RestoreMilitarySaveData(SaveManager.GameSaveData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        State.RestoreMilitary(
            Parse(data.AttackPower, nameof(data.AttackPower)),
            Parse(data.DefensePower, nameof(data.DefensePower)),
            Parse(data.FleetPower, nameof(data.FleetPower)),
            Parse(data.MilitaryManpower, nameof(data.MilitaryManpower)),
            Parse(data.SupplySatisfaction, nameof(data.SupplySatisfaction)),
            Parse(data.PowerSatisfaction, nameof(data.PowerSatisfaction)),
            Parse(data.LogisticsSatisfaction, nameof(data.LogisticsSatisfaction)));
    }

    private static ExpantaNum Parse(string raw, string field)
    {
        if (ExpantaNum.TryParse(raw, out ExpantaNum value) && value.IsFinite)
            return value;
        throw new FormatException($"GameState.{field} 中的 ExpantaNum 值“{raw}”无效。");
    }
}
