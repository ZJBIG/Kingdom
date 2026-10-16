using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class FoodEfficiencyTests
{
    [Test]
    public void FoodAvailability_DecreasingSupplyCannotImproveDeficitHappiness()
    {
        ExpantaNum previous = HappinessFormula.CalculateMultiplier(-100, 10, 1);
        foreach (double availability in new[] { 0.999d, 0.5d, 0.1d, 0.01d, 0d })
        {
            ExpantaNum current = HappinessFormula.CalculateMultiplier(-100, 10, availability, 0.08d);
            Assert.That(current, Is.LessThanOrEqualTo(previous));
            Assert.That(current, Is.LessThanOrEqualTo(new ExpantaNum(availability)));
            previous = current;
        }
    }

    [Test]
    public void NewGameAndDerivedReset_PreserveFoodProductionBaseline()
    {
        GameState state = new GameState();
        state.ResetDerivedEconomyForEditor(new ExpantaNum(100));
        Assert.That(state.FoodProductionRate, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void FoodAvailability_UsesInventoryAndPotentialFlow()
    {
        ExpantaNum empty = HappinessFormula.CalculateFoodAvailability(0, 0, 10, 1);
        ExpantaNum stored = HappinessFormula.CalculateFoodAvailability(5, 0, 10, 1);
        ExpantaNum produced = HappinessFormula.CalculateFoodAvailability(0, 5, 10, 1);
        Assert.That(empty, Is.LessThan(stored));
        Assert.That(produced, Is.GreaterThan(empty).And.LessThan(ExpantaNum.One));
        Assert.That(HappinessFormula.CalculateFoodAvailability(5, 5, 10, 1),
            Is.GreaterThan(stored).And.LessThanOrEqualTo(ExpantaNum.One));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => HappinessFormula.CalculateFoodAvailability(0, 0, 1, -1));
    }

    [Test]
    public void EnergyDrivenBuildings_DoNotUseFoodHappinessConstraint()
    {
        Building energyDriven = ScriptableObject.CreateInstance<Building>();
        try
        {
            energyDriven.ConfigureEconomyForEditor(
                1.15d, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 100, 0, 0, 0, 0, 0,
                new List<Pair<Resource, ExpantaNum>>(),
                new List<Pair<Resource, ExpantaNum>>(),
                new List<Pair<Resource, ExpantaNum>>());
            Assert.That(BuildingManager.IsFoodConstraintRequired(energyDriven), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(energyDriven);
        }
    }

    [Test]
    public void EffectiveEfficiency_MultipliesEveryIndependentConstraint()
    {
        ExpantaNum previous = BuildingManager.CalculateEffectiveEfficiency(1, 1, 1, 1, 1);
        foreach (ExpantaNum result in new[]
        {
            BuildingManager.CalculateEffectiveEfficiency(1, 0.8d, 1, 1, 1),
            BuildingManager.CalculateEffectiveEfficiency(1, 0.8d, 0.5d, 1, 1),
            BuildingManager.CalculateEffectiveEfficiency(1, 0.8d, 0.5d, 0.5d, 1),
            BuildingManager.CalculateEffectiveEfficiency(1, 0.8d, 0.5d, 0.5d, 0.5d)
        })
        {
            Assert.That(result, Is.GreaterThan(ExpantaNum.Zero).And.LessThan(previous));
            previous = result;
        }
    }

    [Test]
    public void ResourceSatisfactionUsesInventoryAndRealizedSameTickProduction()
    {
        ExpantaNum produced = ResourceManager.CalculateSatisfaction(0, 3, 10, 1);
        ExpantaNum storedAndProduced = ResourceManager.CalculateSatisfaction(5, 3, 10, 1);
        Assert.That(produced, Is.GreaterThan(ExpantaNum.Zero).And.LessThan(ExpantaNum.One));
        Assert.That(storedAndProduced, Is.GreaterThan(produced).And.LessThan(ExpantaNum.One));
    }

    [Test]
    public void GameState_AdvanceFoodDoesNotVersionWhenAmountIsUnchanged()
    {
        GameState state = new GameState();
        int versionBefore = state.Version;

        state.AdvanceFoodForEditor(0d);
        Assert.That(state.Version, Is.EqualTo(versionBefore));
    }

    [Test]
    public void GameState_FoodFlowIncludesPopulationConsumptionInRuntimeAndHudRate()
    {
        GameState state = new GameState();
        ExpantaNum initialNetRate = state.FoodNetRate;
        state.RestorePopulationForEditor(new ExpantaNum(3));
        Assert.That(state.FoodPopulationConsumptionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(state.FoodTotalConsumptionRate,
            Is.GreaterThanOrEqualTo(state.FoodPopulationConsumptionRate));
        Assert.That(state.FoodNetRate, Is.LessThan(initialNetRate));

        ExpantaNum foodBeforeAdvance = state.FoodAmount;
        state.AdvanceFoodForEditor(1d);

        Assert.That(state.FoodAmount, Is.GreaterThan(foodBeforeAdvance));
    }

    [Test]
    public void GameState_NoOpDerivedMutationsDoNotIncrementVersion()
    {
        GameState state = new GameState();
        int versionBefore = state.Version;
        state.AdjustFoodRatesForEditor(ExpantaNum.Zero, ExpantaNum.Zero);
        state.AdjustFoodCapacityForEditor(ExpantaNum.Zero);
        state.AdjustPowerRatesForEditor(ExpantaNum.Zero, ExpantaNum.Zero);
        state.AdjustLogisticsRatesForEditor(ExpantaNum.Zero, ExpantaNum.Zero);
        state.AdjustPopulationCapacityForEditor(ExpantaNum.Zero);
        state.AdjustTerritoryTotalForEditor(ExpantaNum.Zero);

        Assert.That(state.Version, Is.EqualTo(versionBefore));
    }

    [Test]
    public void GameState_RepeatedSaveTimestampDoesNotIncrementVersion()
    {
        GameState state = new GameState();
        int versionBefore = state.Version;
        state.MarkSavedForEditor(12345L);
        int versionAfterFirstSave = state.Version;
        state.MarkSavedForEditor(12345L);

        Assert.That(versionAfterFirstSave, Is.EqualTo(versionBefore + 1));
        Assert.That(state.Version, Is.EqualTo(versionAfterFirstSave));
    }
}
