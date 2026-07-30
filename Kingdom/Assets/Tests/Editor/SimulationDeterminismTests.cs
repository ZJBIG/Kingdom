using NUnit.Framework;

public sealed class SimulationDeterminismTests
{
    [Test]
    public void FoodAndResourceIntegration_IsStableAcrossThirtyAndSixtyFpsSteps()
    {
        ExpantaNum foodAtThirtyFps = IntegrateFood(30, 1d / 30d);
        ExpantaNum foodAtSixtyFps = IntegrateFood(60, 1d / 60d);
        ExpantaNum resourceAtThirtyFps = IntegrateResource(30, 1d / 30d);
        ExpantaNum resourceAtSixtyFps = IntegrateResource(60, 1d / 60d);

        Assert.That(
            foodAtThirtyFps.ToDouble(),
            Is.EqualTo(foodAtSixtyFps.ToDouble()).Within(0.001d));
        Assert.That(
            resourceAtThirtyFps.ToDouble(),
            Is.EqualTo(resourceAtSixtyFps.ToDouble()).Within(0.001d));
    }

    [Test]
    public void OfflineElapsedTime_ClampsAndRejectsClockRollback()
    {
        Assert.That(
            SaveManager.CalculateOfflineElapsedSeconds(100, 370, 1000),
            Is.EqualTo(270d));
        Assert.That(
            SaveManager.CalculateOfflineElapsedSeconds(100, 370, 120),
            Is.EqualTo(120d));
        Assert.That(
            SaveManager.CalculateOfflineElapsedSeconds(100, 99, 1000),
            Is.EqualTo(0d));
        Assert.That(
            SaveManager.CalculateOfflineElapsedSeconds(0, 370, 1000),
            Is.EqualTo(0d));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => SaveManager.CalculateOfflineElapsedSeconds(100, 370, -1d));
    }

    private static ExpantaNum IntegrateFood(int stepCount, double deltaSeconds)
    {
        ExpantaNum amount = new ExpantaNum(300);
        for (int i = 0; i < stepCount; i++)
            amount = GameManager.AdvanceFood(
                amount,
                new ExpantaNum(8),
                new ExpantaNum(3),
                new ExpantaNum(500),
                deltaSeconds);
        return amount;
    }

    private static ExpantaNum IntegrateResource(int stepCount, double deltaSeconds)
    {
        ExpantaNum amount = ExpantaNum.Zero;
        for (int i = 0; i < stepCount; i++)
            amount = ResourceManager.AdvanceAmount(
                amount,
                new ExpantaNum(5),
                new ExpantaNum(2),
                deltaSeconds);
        return amount;
    }
}
