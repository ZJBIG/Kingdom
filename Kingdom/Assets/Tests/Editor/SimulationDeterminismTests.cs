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
            Is.EqualTo(foodAtSixtyFps.ToDouble()).Within(0.000001d));
        Assert.That(
            resourceAtThirtyFps.ToDouble(),
            Is.EqualTo(resourceAtSixtyFps.ToDouble()).Within(0.000001d));
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
