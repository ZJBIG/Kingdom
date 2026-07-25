using System;
using System.ComponentModel;
using UnityEngine;

public enum TechLevel
{
    [Description("原始时代")] Primitive,
    [Description("中世纪")] Medieval,
    [Description("工业时代")] Industrial,
    [Description("太空时代")] Spacer,
    [Description("极致时代")] Ultra,
    [Description("远古科技时代")] Archotech,
    [Description("超凡时代")] Ascend
}

public class GameManager : Singleton<GameManager>
{
    private const float CalendarUpdateInterval = 10f;
    private const string DefaultKingdomName = "鼠托邦";

    public GameState State { get; private set; } = new GameState();

    private double calendarElapsedSeconds;

    private void Start()
    {
        Application.runInBackground = true;
    }

    internal void InitializeNewGame()
    {
        State.InitializeNew(DefaultKingdomName);
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
    {
        if (deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        return ExpantaNum.Max(
            ExpantaNum.Zero,
            current + (productionRate - consumptionRate) * deltaSeconds);
    }

    public bool CanAffordConstruction(ExpantaNum spaceCost, ExpantaNum buildEffort) =>
        State.AvailableSpace >= spaceCost && State.AvailableProductivity >= buildEffort;

    public void CommitConstruction(
        ExpantaNum spaceCost,
        ExpantaNum buildEffort,
        ExpantaNum productivityGranted) =>
        State.CommitConstruction(spaceCost, buildEffort, productivityGranted);

    public void RefundConstruction(
        ExpantaNum spaceCost,
        ExpantaNum buildEffort,
        ExpantaNum productivityGranted) =>
        State.RefundConstruction(spaceCost, buildEffort, productivityGranted);

    public void AdjustFoodRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta) =>
        State.AdjustFoodRates(productionDelta, consumptionDelta);

    internal void ResetCalendarAccumulator() => calendarElapsedSeconds = 0d;

    internal void ResetDerivedEconomy() =>
        State.ResetDerivedEconomy(new ExpantaNum(100000), new ExpantaNum(100));

    internal SaveManager.GameSaveData CaptureSaveData()
    {
        State.MarkSaved(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return new SaveManager.GameSaveData
        {
            CalendarDays = State.CalendarDays,
            KingdomName = State.KingdomName,
            TechLevel = State.TechLevel,
            FoodAmount = State.FoodAmount.ToString(),
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
        ResetCalendarAccumulator();
    }

    public override void Save() => SaveManager.Instance.SaveNow(true);

    public override void Load() => SaveManager.Instance.LoadOrCreateGame();

    private static ExpantaNum Parse(string raw, string field)
    {
        if (ExpantaNum.TryParse(raw, out ExpantaNum value))
            return value;
        throw new FormatException($"Invalid ExpantaNum '{raw}' for GameState.{field}.");
    }
}
