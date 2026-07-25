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
}
