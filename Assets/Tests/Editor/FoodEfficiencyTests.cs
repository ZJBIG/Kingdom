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

        var reset = typeof(GameState).GetMethod(
            "ResetDerivedEconomy",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.That(reset, Is.Not.Null);
        reset.Invoke(state, new object[] { new ExpantaNum(100) });

        Assert.That(state.FoodProductionRate, Is.EqualTo(initialProduction));
    }

    [Test]
    public void FoodAvailability_UsesInventoryAndPotentialFlow()
    {
        Assert.That(HappinessFormula.CalculateFoodAvailability(0, 0, 10, 1), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(HappinessFormula.CalculateFoodAvailability(5, 0, 10, 1), Is.EqualTo(new ExpantaNum(0.5d)));
        Assert.That(HappinessFormula.CalculateFoodAvailability(0, 5, 10, 1), Is.EqualTo(new ExpantaNum(0.5d)));
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
            HappinessFormula.CalculateConstraintMultiplier(0.4d),
            Is.EqualTo(new ExpantaNum(0.4d)));
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
        var method = typeof(GameState).GetMethod(
            "AdvanceFood",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        ExpantaNum foodBeforeNoOp = state.FoodAmount;
        method.Invoke(state, new object[] { 0d });

        Assert.That(state.FoodAmount, Is.Not.LessThan(foodBeforeNoOp));
        Assert.That(state.FoodAmount, Is.Not.GreaterThan(foodBeforeNoOp));
        Assert.That(state.Version, Is.EqualTo(versionBefore));
    }

    [Test]
    public void GameState_FoodFlowIncludesPopulationConsumptionInRuntimeAndHudRate()
    {
        GameState state = new GameState();
        Invoke(state, "RestorePopulation", new ExpantaNum(3));

        ExpantaNum expectedPopulationConsumption =
            PopulationState.FoodConsumptionPerPerson * new ExpantaNum(3);
        Assert.That(state.FoodPopulationConsumptionRate,
            Is.EqualTo(expectedPopulationConsumption));
        Assert.That(state.FoodTotalConsumptionRate,
            Is.EqualTo(expectedPopulationConsumption));
        Assert.That(state.FoodNetRate,
            Is.EqualTo(state.FoodProductionRate - expectedPopulationConsumption));

        ExpantaNum foodBeforeAdvance = state.FoodAmount;
        Invoke(state, "AdvanceFood", 1d);

        Assert.That(state.FoodAmount, Is.GreaterThan(foodBeforeAdvance));
    }

    [Test]
    public void GameState_NoOpDerivedMutationsDoNotIncrementVersion()
    {
        GameState state = new GameState();
        int versionBefore = state.Version;
        Invoke(state, "AdjustFoodRates", ExpantaNum.Zero, ExpantaNum.Zero);
        Invoke(state, "AdjustFoodCapacity", ExpantaNum.Zero);
        Invoke(state, "AdjustPowerRates", ExpantaNum.Zero, ExpantaNum.Zero);
        Invoke(state, "AdjustLogisticsRates", ExpantaNum.Zero, ExpantaNum.Zero);
        Invoke(state, "AdjustPopulationCapacity", ExpantaNum.Zero);
        Invoke(state, "AdjustTerritoryTotal", ExpantaNum.Zero);

        Assert.That(state.Version, Is.EqualTo(versionBefore));
    }

    [Test]
    public void GameState_RepeatedSaveTimestampDoesNotIncrementVersion()
    {
        GameState state = new GameState();
        int versionBefore = state.Version;
        Invoke(state, "MarkSaved", 12345L);
        int versionAfterFirstSave = state.Version;
        Invoke(state, "MarkSaved", 12345L);

        Assert.That(versionAfterFirstSave, Is.EqualTo(versionBefore + 1));
        Assert.That(state.Version, Is.EqualTo(versionAfterFirstSave));
    }

    private static void Invoke(GameState state, string methodName, params object[] arguments)
    {
        var method = typeof(GameState).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(state, arguments);
    }
}
