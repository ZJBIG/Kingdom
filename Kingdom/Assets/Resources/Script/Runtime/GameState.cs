using System;

[Serializable]
public sealed class GameState
{
    private const string DefaultKingdomName = "鼠托邦";

    public int CalendarDays { get; private set; }
    public string KingdomName { get; private set; }
    public TechLevel TechLevel { get; private set; }
    public ExpantaNum FoodAmount { get; private set; }
    public ExpantaNum FoodProductionRate { get; private set; }
    public ExpantaNum FoodConsumptionRate { get; private set; }
    public ExpantaNum AvailableSpace { get; private set; }
    public ExpantaNum AvailableProductivity { get; private set; }
    public long LastSaveUnixSeconds { get; private set; }
    public int Version { get; private set; }

    public GameState() => InitializeNew(DefaultKingdomName);

    internal void InitializeNew(string kingdomName)
    {
        CalendarDays = 0;
        KingdomName = string.IsNullOrWhiteSpace(kingdomName) ? DefaultKingdomName : kingdomName;
        TechLevel = TechLevel.Primitive;
        FoodAmount = new ExpantaNum(10000);
        FoodProductionRate = ExpantaNum.Zero;
        FoodConsumptionRate = ExpantaNum.Zero;
        AvailableSpace = new ExpantaNum(100000);
        AvailableProductivity = new ExpantaNum(100);
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
        LastSaveUnixSeconds = lastSaveUnixSeconds;
        Version++;
    }

    internal void ResetDerivedEconomy(ExpantaNum availableSpace, ExpantaNum availableProductivity)
    {
        FoodProductionRate = ExpantaNum.Zero;
        FoodConsumptionRate = ExpantaNum.Zero;
        AvailableSpace = ExpantaNum.Max(ExpantaNum.Zero, availableSpace);
        AvailableProductivity = ExpantaNum.Max(ExpantaNum.Zero, availableProductivity);
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
            FoodConsumptionRate,
            deltaSeconds);
        Version++;
    }

    internal void AdjustFoodRates(ExpantaNum productionDelta, ExpantaNum consumptionDelta)
    {
        FoodProductionRate = ExpantaNum.Max(ExpantaNum.Zero, FoodProductionRate + productionDelta);
        FoodConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, FoodConsumptionRate + consumptionDelta);
        Version++;
    }

    internal void CommitConstruction(
        ExpantaNum spaceCost,
        ExpantaNum buildEffort,
        ExpantaNum productivityGranted)
    {
        AvailableSpace = ExpantaNum.Max(ExpantaNum.Zero, AvailableSpace - spaceCost);
        AvailableProductivity = ExpantaNum.Max(
            ExpantaNum.Zero,
            AvailableProductivity - buildEffort + productivityGranted);
        Version++;
    }

    internal void RefundConstruction(
        ExpantaNum spaceCost,
        ExpantaNum buildEffort,
        ExpantaNum productivityGranted)
    {
        AvailableSpace += spaceCost;
        AvailableProductivity = ExpantaNum.Max(
            ExpantaNum.Zero,
            AvailableProductivity + buildEffort - productivityGranted);
        Version++;
    }

    internal void MarkSaved(long unixSeconds)
    {
        LastSaveUnixSeconds = unixSeconds;
        Version++;
    }
}
