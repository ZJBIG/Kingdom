using NUnit.Framework;

public sealed class FlowEfficiencyTests
{
    [Test]
    public void FlowSatisfaction_UsesProductionAgainstDemandWithoutInventory()
    {
        ExpantaNum shortage = GameManager.CalculateFlowSatisfaction(50, 100);
        Assert.That(shortage, Is.GreaterThan(ExpantaNum.Zero).And.LessThan(ExpantaNum.One));
        Assert.That(GameManager.CalculateFlowSatisfaction(100, 100), Is.GreaterThan(shortage));
        Assert.That(GameManager.CalculateFlowSatisfaction(-5, 100), Is.LessThanOrEqualTo(ExpantaNum.Zero));
        Assert.That(GameManager.CalculateFlowSatisfaction(0, 0), Is.GreaterThanOrEqualTo(ExpantaNum.One));
    }

    [Test]
    public void HappinessCurveIsMonotonicAndBounded()
    {
        ExpantaNum low = HappinessFormula.CalculateMultiplier(
            ExpantaNum.Zero,
            new ExpantaNum(10));
        ExpantaNum medium = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(90),
            new ExpantaNum(10));
        ExpantaNum high = HappinessFormula.CalculateMultiplier(
            new ExpantaNum("1e100000"),
            new ExpantaNum(10));

        Assert.That(low, Is.GreaterThanOrEqualTo(ExpantaNum.One));
        Assert.That(medium, Is.GreaterThan(low));
        Assert.That(high, Is.GreaterThan(medium));
        Assert.That(high, Is.LessThan(
            ExpantaNum.One + HappinessFormula.MaximumBonus));
    }

    [Test]
    public void HappinessPenalizesNegativeFoodNetRateEvenWhenInventoryIsAvailable()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(-100),
            new ExpantaNum(10));

        Assert.That(result, Is.GreaterThan(ExpantaNum.Zero).And.LessThan(ExpantaNum.One));
        Assert.That(HappinessFormula.CalculateMultiplier(-200, 10), Is.LessThan(result));
    }

    [Test]
    public void HappinessOwnsFoodShortagePenalty()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(-10),
            new ExpantaNum(10),
            new ExpantaNum(0.5d));

        Assert.That(result, Is.GreaterThan(ExpantaNum.Zero).And.LessThan(ExpantaNum.One));
        Assert.That(HappinessFormula.CalculateConstraintMultiplier(result),
            Is.LessThan(ExpantaNum.One));
        Assert.That(HappinessFormula.CalculateRewardMultiplier(result),
            Is.LessThanOrEqualTo(ExpantaNum.One));
    }

    [Test]
    public void MedicalHappinessBonusOnlyAppliesWhenFoodIsAvailable()
    {
        ExpantaNum healthy = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(90),
            new ExpantaNum(10),
            ExpantaNum.One,
            new ExpantaNum(0.08d));
        ExpantaNum shortage = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(90),
            new ExpantaNum(10),
            new ExpantaNum(0.5d),
            new ExpantaNum(0.08d));

        Assert.That(healthy, Is.GreaterThan(
            HappinessFormula.CalculateMultiplier(new ExpantaNum(90), new ExpantaNum(10))));
        Assert.That(shortage, Is.LessThan(ExpantaNum.One).And.LessThanOrEqualTo(
            HappinessFormula.CalculateMultiplier(90, 10, 0.5d)));
    }

    [Test]
    public void MedicalHappinessBonusSharesTheExistingUpperBound()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum("1e100000"),
            new ExpantaNum(10),
            ExpantaNum.One,
            new ExpantaNum(0.5d));

        Assert.That(result, Is.LessThanOrEqualTo(
            ExpantaNum.One + HappinessFormula.MaximumBonus));
    }

    [Test]
    public void HappinessWithZeroPopulationIsSafe()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(100),
            ExpantaNum.Zero);

        Assert.That(result.IsNaN, Is.False);
        Assert.That(result, Is.GreaterThanOrEqualTo(ExpantaNum.One));
    }
}
