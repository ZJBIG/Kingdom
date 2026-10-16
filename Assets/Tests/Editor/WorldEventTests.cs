using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldEventTests
{
    [Test]
    public void Protection_RotationAndRecovery_AreStableAcrossTimePartitions()
    {
        var whole = new WorldEventState();
        var split = new WorldEventState();
        double interval = WorldEventState.EventIntervalSeconds;
        Advance(whole, interval - 1d, true);
        Assert.That(whole.LastKind, Is.EqualTo(WorldEventKind.None));
        Advance(whole, 1d, true);
        Assert.That(whole.ActiveKind, Is.EqualTo(WorldEventKind.ProductiveDay));
        Assert.That(whole.ProductionMultiplier.ToDouble(), Is.GreaterThan(1d));
        Advance(whole, WorldEventState.EffectDurationSeconds, true);
        Assert.That(whole.ActiveKind, Is.EqualTo(WorldEventKind.None));
        Advance(whole, interval - WorldEventState.EffectDurationSeconds, true);
        Assert.That(whole.LastKind, Is.EqualTo(WorldEventKind.Aurora));
        Assert.That(whole.ActiveKind, Is.EqualTo(WorldEventKind.None));
        Advance(whole, interval, true);
        Assert.That(whole.ActiveKind, Is.EqualTo(WorldEventKind.Fire));
        Assert.That(whole.ProductionMultiplier.ToDouble(), Is.LessThan(1d));
        for (int i = 0; i < interval * 3d * 4d; i++) Advance(split, .25d, true);
        // The sequence is a discrete event count; durations use tolerance.
        Assert.That(split.Sequence, Is.EqualTo(whole.Sequence));
        Assert.That(split.SecondsUntilNextEvent, Is.EqualTo(whole.SecondsUntilNextEvent).Within(1e-8d));
    }

    [Test]
    public void Offline_ExpiresSavedEffectWithoutCreatingEventsOrConsumingProtection()
    {
        var original = new WorldEventState();
        Advance(original, WorldEventState.EventIntervalSeconds + 40d, true);
        string json = JsonUtility.ToJson(original.CaptureSaveData());
        var restored = new WorldEventState();
        restored.RestoreForEditor(JsonUtility.FromJson<SaveManager.WorldEventSaveData>(json));
        Assert.That(restored.RemainingSeconds, Is.EqualTo(original.RemainingSeconds).Within(1e-8d));
        Advance(restored, 1000d, false);
        Assert.That(restored.ActiveKind, Is.EqualTo(WorldEventKind.None));
        Assert.That(restored.Sequence, Is.EqualTo(original.Sequence));
        Assert.That(restored.SecondsUntilNextEvent, Is.EqualTo(original.SecondsUntilNextEvent).Within(1e-8d));
        var protectedState = new WorldEventState();
        Advance(protectedState, 1000d, false);
        Assert.That(protectedState.Sequence, Is.Zero);
        Assert.That(protectedState.SecondsUntilNextEvent,
            Is.EqualTo(WorldEventState.EventIntervalSeconds).Within(1e-8d));
    }

    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 1)]
    [TestCase(100, 1)]
    public void Fire_LosesAtMostOnePersonAndNeverTheLastPerson(double population, int lost)
    {
        var state = new GameState();
        state.RestorePopulationForEditor(new ExpantaNum(population));
        state.RestorePopulationChangeProgressForEditor(new ExpantaNum(.5d));
        double growthProgress = state.Population.PopulationChangeProgress.ToDouble();
        state.Events.RestoreForEditor(new SaveManager.WorldEventSaveData
            { Sequence = 2, SecondsUntilNextEvent = 1d });
        state.AdvanceWorldEventsForEditor(1d, true);
        Assert.That(state.Population.PopulationChangeProgress.ToDouble(), Is.EqualTo(growthProgress).Within(1e-8d));
        Assert.That(state.Events.PopulationLost, Is.EqualTo(lost));
        Assert.That(state.Population.Population.ToDouble(), Is.EqualTo(population - lost).Within(1e-8d));
        state.AdvanceWorldEventsForEditor(1d, true);
        Assert.That(state.Population.Population.ToDouble(), Is.EqualTo(population - lost).Within(1e-8d));
    }

    [Test]
    public void SaveValidation_RejectsImpossibleEffectsWithoutChangingState()
    {
        var state = new WorldEventState();
        Assert.Throws<InvalidDataException>(() => state.RestoreForEditor(new SaveManager.WorldEventSaveData
            { Sequence = 2, RemainingSeconds = 10d, SecondsUntilNextEvent = 20d }));
        Assert.Throws<InvalidDataException>(() => state.RestoreForEditor(new SaveManager.WorldEventSaveData
            { Sequence = 1, RemainingSeconds = double.NaN }));
        Assert.Throws<InvalidDataException>(() => state.RestoreForEditor(new SaveManager.WorldEventSaveData
            { Sequence = 1, PopulationLost = 2 }));
        Assert.That(state.Sequence, Is.Zero);
    }

    [Test]
    public void Sequence_WrapsAtACompleteCycleWithoutOverflow()
    {
        var state = new WorldEventState();
        state.RestoreForEditor(new SaveManager.WorldEventSaveData
            { Sequence = int.MaxValue - 1, SecondsUntilNextEvent = 1d });
        Advance(state, 1d, true);
        Assert.That(state.LastKind, Is.EqualTo(WorldEventKind.ProductiveDay));
        Assert.That(state.Sequence, Is.EqualTo(1));
        var restored = new WorldEventState();
        restored.RestoreForEditor(state.CaptureSaveData());
        Assert.That(restored.ActiveKind, Is.EqualTo(WorldEventKind.ProductiveDay));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Simulation_ResourceOutputsCrossEventBoundaryWithoutScalingInputsOrCapacity(bool offline)
    {
        double whole = SimulateOutput(offline, false);
        double split = SimulateOutput(offline, true);
        Assert.That(split, Is.EqualTo(whole).Within(1e-5d));
    }

    private static double SimulateOutput(bool offline, bool split)
    {
        ProgressionModifierManager.Rebuild(null);
        var root = new GameObject("WorldEventTests");
        Building output = ScriptableObject.CreateInstance<Building>();
        try
        {
            GameManager game = root.AddComponent<GameManager>();
            ResourceManager resources = root.AddComponent<ResourceManager>();
            BuildingManager buildings = root.AddComponent<BuildingManager>();
            ResearchManager research = root.AddComponent<ResearchManager>();
            SimulationManager simulation = root.AddComponent<SimulationManager>();
            research.InitializeForEditor();
            Resource stone = DataBase<Resource>.Find("StoneChunk");
            Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
            resources.SetAmount(wood, new ExpantaNum(1e6d));
            output.ConfigureEconomyForEditor(
                growth: ExpantaNum.One, territory: ExpantaNum.Zero,
                productivityConsumptionValue: ExpantaNum.Zero, productivity: ExpantaNum.Zero,
                populationCapacity: ExpantaNum.Zero, researchPower: ExpantaNum.Zero,
                foodProduction: new ExpantaNum(10d), foodConsumption: ExpantaNum.Zero,
                foodCapacity: new ExpantaNum(5000d), powerProduction: ExpantaNum.Zero,
                powerConsumption: ExpantaNum.Zero, logisticsProduction: ExpantaNum.Zero,
                logisticsConsumption: ExpantaNum.Zero, attack: ExpantaNum.Zero,
                defense: ExpantaNum.Zero, manpower: ExpantaNum.Zero,
                requirements: null,
                generation: new System.Collections.Generic.List<Pair<Resource, ExpantaNum>>
                    { new Pair<Resource, ExpantaNum>(stone, new ExpantaNum(10d)) },
                consumption: new System.Collections.Generic.List<Pair<Resource, ExpantaNum>>
                    { new Pair<Resource, ExpantaNum>(wood, ExpantaNum.One) });
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(output), ExpantaNum.One);
            game.State.Events.RestoreForEditor(new SaveManager.WorldEventSaveData
                { Sequence = 2, SecondsUntilNextEvent = 1d });
            simulation.ManualTick(1d);
            Assert.That(game.State.Events.ActiveKind, Is.EqualTo(WorldEventKind.Fire));
            double initialAmount = resources.GetAmount(stone).ToDouble();
            double fireRate = (resources.GetState(stone).ProductionRate * game.State.HappinessRewardMultiplier).ToDouble();
            double fireFoodRate = game.State.FoodProductionRate.ToDouble();
            ExpantaNum capacity = game.State.FoodCapacity;
            ExpantaNum inputRate = resources.GetState(wood).ConsumptionRate;
            if (split)
                for (int i = 0; i < 130; i++)
                {
                    if (offline) simulation.AdvanceOffline(1d); else simulation.ManualTick(1d);
                }
            else if (offline) simulation.AdvanceOffline(130d);
            else simulation.ManualTick(130d);
            double restoredRate = (resources.GetState(stone).ProductionRate * game.State.HappinessRewardMultiplier).ToDouble();
            Assert.That(fireRate, Is.LessThan(restoredRate));
            Assert.That(fireFoodRate, Is.LessThan(game.State.FoodProductionRate.ToDouble()));
            Assert.That(game.State.FoodCapacity.ToDouble(), Is.EqualTo(capacity.ToDouble()).Within(1e-6d));
            Assert.That(resources.GetState(stone).ConsumptionRate.ToDouble(), Is.EqualTo(0d).Within(1e-8d));
            Assert.That(resources.GetState(wood).ConsumptionRate.ToDouble(), Is.EqualTo(inputRate.ToDouble()).Within(1e-8d));
            double produced = resources.GetAmount(stone).ToDouble() - initialAmount;
            Assert.That(produced, Is.EqualTo(fireRate * 120d + restoredRate * 10d).Within(1e-5d));
            // Capture through the actual Game DTO, not only the event's standalone DTO.
            SaveManager.GameSaveData saved = game.CaptureSaveData();
            game.RestoreSaveDataForEditor(JsonUtility.FromJson<SaveManager.GameSaveData>(JsonUtility.ToJson(saved)));
            Assert.That(game.State.Events.Sequence, Is.EqualTo(saved.Events.Sequence));
            return produced;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(output);
            ProgressionModifierManager.Rebuild(null);
        }
    }

    private static void Advance(WorldEventState state, double seconds, bool online)
    {
        while (seconds > 0d)
        {
            double step = Math.Min(seconds, state.NextBoundaryForEditor(online));
            state.AdvanceForEditor(step, online);
            seconds -= step;
        }
    }
}
