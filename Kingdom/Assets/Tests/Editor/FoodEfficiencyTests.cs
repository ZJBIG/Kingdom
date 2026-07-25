using NUnit.Framework;

public sealed class FoodEfficiencyTests
{
    [Test]
    public void FoodSatisfaction_UsesInventoryAndPotentialFlow()
    {
        Assert.That(GameManager.CalculateFoodSatisfaction(0, 0, 10, 1), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(GameManager.CalculateFoodSatisfaction(5, 0, 10, 1), Is.EqualTo(new ExpantaNum(0.5d)));
        Assert.That(GameManager.CalculateFoodSatisfaction(0, 10, 10, 1), Is.EqualTo(ExpantaNum.One));
        Assert.That(GameManager.CalculateFoodSatisfaction(0, 0, 0, 1), Is.EqualTo(ExpantaNum.One));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => GameManager.CalculateFoodSatisfaction(0, 0, 1, -1));
    }

    [Test]
    public void EffectiveEfficiency_MultipliesFoodAndResourceSatisfaction()
    {
        Assert.That(
            BuildingManager.CalculateEffectiveEfficiency(1, 1, 0),
            Is.EqualTo(ExpantaNum.Zero));
        Assert.That(
            BuildingManager.CalculateEffectiveEfficiency(1, 0.8d, 0.5d).ToDouble(),
            Is.EqualTo(0.4d).Within(0.000001d));
        Assert.That(
            BuildingManager.CalculateEffectiveEfficiency(2, 1, 1),
            Is.EqualTo(ExpantaNum.One));
    }
}
