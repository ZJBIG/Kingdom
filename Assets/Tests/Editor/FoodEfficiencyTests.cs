using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class FoodEfficiencyTests
{
    [Test]
    public void NewGameAndDerivedReset_PreserveFoodProductionBaseline()
    {
        GameState state = new GameState();
        ExpantaNum initialProduction = state.FoodProductionRate;
        Assert.That(initialProduction, Is.GreaterThan(ExpantaNum.Zero));

        state.ResetDerivedEconomyForEditor(new ExpantaNum(100));

        Assert.That(state.FoodProductionRate, Is.EqualTo(initialProduction));
    }

    [Test]
    public void FoodAvailability_UsesInventoryAndPotentialFlow()
    {
        Assert.That(HappinessFormula.CalculateFoodAvailability(0, 0, 10, 1), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(HappinessFormula.CalculateFoodAvailability(5, 0, 10, 1).ToDouble(),
            Is.EqualTo(0.5d).Within(0.000001d));
        Assert.That(HappinessFormula.CalculateFoodAvailability(0, 5, 10, 1).ToDouble(),
            Is.EqualTo(0.5d).Within(0.000001d));
        Assert.That(HappinessFormula.CalculateFoodAvailability(0, 10, 10, 1), Is.EqualTo(ExpantaNum.One));
        Assert.That(HappinessFormula.CalculateFoodAvailability(0, 0, 0, 1), Is.EqualTo(ExpantaNum.One));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => HappinessFormula.CalculateFoodAvailability(0, 0, 1, -1));
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

    [Test]
    public void EnergyDrivenBuildings_DoNotUseFoodHappinessConstraint()
    {
        Building energyDriven = ScriptableObject.CreateInstance<Building>();
        energyDriven.ConfigureEconomyForEditor(
            1.15d, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 100, 0, 0, 0, 0, 0,
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>());

        Assert.That(BuildingManager.IsFoodConstraintRequired(energyDriven), Is.False);
    }

    [Test]
    public void EffectiveEfficiency_MultipliesEveryIndependentConstraint()
    {
        ExpantaNum resourceInputs = new ExpantaNum(0.8d) * new ExpantaNum(0.5d);
        ExpantaNum result = BuildingManager.CalculateEffectiveEfficiency(
            1, resourceInputs, 0.5d, 0.5d, 0.5d);

        Assert.That(result.ToDouble(), Is.EqualTo(0.05d).Within(0.000001d));
    }

    [Test]
    public void EveryBuildingUsesTheSameFoodSupplyRatio()
    {
        Assert.That(
            HappinessFormula.CalculateConstraintMultiplier(0.4d).ToDouble(),
            Is.EqualTo(0.4d).Within(0.000001d));
    }

    [Test]
    public void ResourceSatisfactionUsesInventoryAndRealizedSameTickProduction()
    {
        Assert.That(ResourceManager.CalculateSatisfaction(5, 3, 10, 1).ToDouble(),
            Is.EqualTo(0.8d).Within(0.000001d));
        Assert.That(ResourceManager.CalculateSatisfaction(0, 2.5d, 10, 1).ToDouble(),
            Is.EqualTo(0.25d).Within(0.000001d));
        Assert.That(ResourceManager.CalculateSatisfaction(0, 0, 0, 1),
            Is.EqualTo(ExpantaNum.One));
    }

    [Test]
    public void GameState_AdvanceFoodDoesNotVersionWhenAmountIsUnchanged()
    {
        GameState state = new GameState();
        int versionBefore = state.Version;

        ExpantaNum foodBeforeNoOp = state.FoodAmount;
        state.AdvanceFoodForEditor(0d);

        Assert.That(state.FoodAmount, Is.Not.LessThan(foodBeforeNoOp));
        Assert.That(state.FoodAmount, Is.Not.GreaterThan(foodBeforeNoOp));
        Assert.That(state.Version, Is.EqualTo(versionBefore));
    }

    [Test]
    public void GameState_FoodFlowIncludesPopulationConsumptionInRuntimeAndHudRate()
    {
        GameState state = new GameState();
        state.RestorePopulationForEditor(new ExpantaNum(3));

        ExpantaNum expectedPopulationConsumption =
            PopulationState.FoodConsumptionPerPerson * new ExpantaNum(3);
        Assert.That(state.FoodPopulationConsumptionRate,
            Is.EqualTo(expectedPopulationConsumption));
        Assert.That(state.FoodTotalConsumptionRate,
            Is.EqualTo(expectedPopulationConsumption));
        Assert.That(state.FoodNetRate,
            Is.EqualTo(state.FoodProductionRate - expectedPopulationConsumption));

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
