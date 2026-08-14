using System;
using NUnit.Framework;

public sealed class FlowEfficiencyTests
{
    [Test]
    public void FlowSatisfaction_UsesProductionAgainstDemandWithoutInventory()
    {
        Assert.That(GameManager.CalculateFlowSatisfaction(0, 0), Is.EqualTo(ExpantaNum.One));
        Assert.That(GameManager.CalculateFlowSatisfaction(50, 100).ToDouble(),
            Is.EqualTo(0.5d).Within(0.000001d));
        Assert.That(GameManager.CalculateFlowSatisfaction(-5, 100), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(GameManager.CalculateFlowSatisfaction(100, -5), Is.EqualTo(ExpantaNum.One));
    }

    [Test]
    public void EffectiveEfficiency_MultipliesPowerAndLogisticsSatisfaction()
    {
        ExpantaNum result = BuildingManager.CalculateEffectiveEfficiency(
            ExpantaNum.One,
            new ExpantaNum(0.8d),
            ExpantaNum.One,
            new ExpantaNum(0.5d),
            new ExpantaNum(0.4d));

        Assert.That(result.ToDouble(), Is.EqualTo(0.16d).Within(0.000001d));
    }

    [Test]
    public void PowerAndLogisticsAreDerivedFieldsNotSaveInventoryFields()
    {
        Assert.That(typeof(SaveManager.GameSaveData).GetField("PowerProduction"), Is.Null);
        Assert.That(typeof(SaveManager.GameSaveData).GetField("LogisticsProduction"), Is.Null);
        Assert.That(typeof(SaveManager.GameSaveData).GetField("PowerSatisfaction"), Is.Not.Null);
        Assert.That(typeof(SaveManager.GameSaveData).GetField("LogisticsSatisfaction"), Is.Not.Null);
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

        Assert.That(low, Is.EqualTo(ExpantaNum.One));
        Assert.That(medium, Is.GreaterThan(low));
        Assert.That(high, Is.GreaterThan(medium));
        Assert.That(high, Is.LessThan(
            ExpantaNum.One + HappinessFormula.MaximumBonus));
    }

    [Test]
    public void HappinessDoesNotPenalizeExistingFoodDeficit()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(-100),
            new ExpantaNum(10));

        Assert.That(result, Is.EqualTo(ExpantaNum.One));
    }

    [Test]
    public void HappinessOwnsFoodShortagePenalty()
    {
        ExpantaNum result = HappinessFormula.CalculateMultiplier(
            new ExpantaNum(-10),
            new ExpantaNum(10),
            new ExpantaNum(0.5d));

        Assert.That(result, Is.EqualTo(new ExpantaNum(0.5d)));
        Assert.That(HappinessFormula.CalculateConstraintMultiplier(result),
            Is.EqualTo(result));
        Assert.That(HappinessFormula.CalculateRewardMultiplier(result),
            Is.EqualTo(ExpantaNum.One));
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
        Assert.That(shortage, Is.EqualTo(new ExpantaNum(0.5d)));
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
    public void FoodAvailabilityAndHappinessFieldsReplaceLegacyFoodState()
    {
        Assert.That(typeof(GameState).GetProperty("FoodSatisfaction"), Is.Null);
        Assert.That(typeof(GameState).GetProperty("FoodAvailability"), Is.Not.Null);
        Assert.That(typeof(GameState).GetProperty("HappinessMultiplier"), Is.Not.Null);
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
