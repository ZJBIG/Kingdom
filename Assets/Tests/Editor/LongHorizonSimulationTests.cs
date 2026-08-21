using NUnit.Framework;

public sealed class LongHorizonSimulationTests
{
    [Test]
    public void FoodShortage_StopsAtZeroWithoutGoingNegative()
    {
        ExpantaNum food = new ExpantaNum(10);
        for (int i = 0; i < 10; i++)
            food = GameManager.AdvanceFood(
                food,
                ExpantaNum.Zero,
                new ExpantaNum(3),
                new ExpantaNum(500),
                60d);

        Assert.That(food, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void CompetingResourceFlows_RemainFiniteAndNonNegative()
    {
        ExpantaNum resource = new ExpantaNum(20);
        for (int i = 0; i < 600; i++)
            resource = ResourceManager.AdvanceAmount(
                resource,
                new ExpantaNum(5),
                new ExpantaNum(8),
                60d);

        Assert.That(resource.IsFinite, Is.True);
        Assert.That(resource, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
    }

    [TestCase(600d, TestName = "TenMinutes")]
    [TestCase(3600d, TestName = "OneHour")]
    [TestCase(14400d, TestName = "FourHours")]
    [TestCase(86400d, TestName = "OneDay")]
    public void FoodAndResourceSimulation_RemainsFiniteAcrossLongHorizon(double durationSeconds)
    {
        const double stepSeconds = 60d;
        int stepCount = (int)(durationSeconds / stepSeconds);
        ExpantaNum food = new ExpantaNum(300);
        ExpantaNum resource = ExpantaNum.Zero;

        for (int i = 0; i < stepCount; i++)
        {
            food = GameManager.AdvanceFood(
                food,
                new ExpantaNum(8),
                new ExpantaNum(3),
                new ExpantaNum(500),
                stepSeconds);
            resource = ResourceManager.AdvanceAmount(
                resource,
                new ExpantaNum(5),
                new ExpantaNum(2),
                stepSeconds);
        }

        Assert.That(food.IsFinite, Is.True);
        Assert.That(resource.IsFinite, Is.True);
        Assert.That(food, Is.LessThanOrEqualTo(new ExpantaNum(500)));
        Assert.That(food, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
        Assert.That(resource, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
    }
}
