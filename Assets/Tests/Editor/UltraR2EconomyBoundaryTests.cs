using System.Collections.Generic;
using System;
using NUnit.Framework;
using UnityEngine;

public sealed class UltraR2EconomyBoundaryTests
{
    private const double Tolerance = 1e-7d;
    private const double SimulationAbsoluteTolerance = 1e-6d;
    private const double SimulationRelativeTolerance = 1e-12d;
    private const double OfflineProjectProgressRelativeTolerance = 0.01d;

    [Test]
    public void OfflineEffectiveTime_IsStableAtSixtySecondTwoHourAndEightHourBoundaries()
    {
        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(0d, 60d),
            Is.EqualTo(60d).Within(Tolerance));
        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(7200d, 60d),
            Is.EqualTo(36d).Within(Tolerance));
        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(28800d, 60d),
            Is.EqualTo(15d).Within(Tolerance));

        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(0d, 7260d),
            Is.EqualTo(7236d).Within(Tolerance));
        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(0d, 28860d),
            Is.EqualTo(20175d).Within(Tolerance));
    }

    [Test]
    public void OfflineEffectiveTime_IsAdditiveWhenAWindowCrossesRateBoundaries()
    {
        double throughTwoHours =
            SimulationManager.CalculateOfflineEffectiveSeconds(0d, 7200d) +
            SimulationManager.CalculateOfflineEffectiveSeconds(7200d, 60d);
        double directTwoHourWindow =
            SimulationManager.CalculateOfflineEffectiveSeconds(0d, 7260d);
        double throughEightHours =
            SimulationManager.CalculateOfflineEffectiveSeconds(0d, 28800d) +
            SimulationManager.CalculateOfflineEffectiveSeconds(28800d, 60d);
        double directEightHourWindow =
            SimulationManager.CalculateOfflineEffectiveSeconds(0d, 28860d);

        Assert.That(throughTwoHours, Is.EqualTo(directTwoHourWindow).Within(Tolerance));
        Assert.That(throughEightHours, Is.EqualTo(directEightHourWindow).Within(Tolerance));
    }

    [Test]
    public void UltraProject_ManualAndOfflineTicksUseTheSameFullRateProgression()
    {
        UltraSimulationSnapshot realtime = RunUltraSimulation(false, 60d);
        UltraSimulationSnapshot offline = RunUltraSimulation(true, 60d);

        AssertScaleAwareParity(
            offline.Progress,
            realtime.Progress,
            "Ultra progress",
            OfflineProjectProgressRelativeTolerance);
        AssertScaleAwareParity(offline.Food, realtime.Food, "Food");
        AssertScaleAwareParity(
            offline.ContinuousResource,
            realtime.ContinuousResource,
            "Continuous resource");
    }

    private static void AssertScaleAwareParity(
        double actual,
        double expected,
        string label,
        double relativeTolerance = SimulationRelativeTolerance)
    {
        double scale = Math.Max(Math.Abs(actual), Math.Abs(expected));
        double tolerance = Math.Max(
            SimulationAbsoluteTolerance,
            scale * relativeTolerance);
        Assert.That(
            actual,
            Is.EqualTo(expected).Within(tolerance),
            $"{label} parity exceeded scale-aware tolerance {tolerance:G17}.");
    }

    private static UltraSimulationSnapshot RunUltraSimulation(bool offline, double seconds)
    {
        ProgressionModifierManager.Rebuild(null);
        GameObject root = new GameObject("UltraR2-Economy-Boundary");
        GameObject resourceObject = new GameObject("UltraR2-Economy-Boundary-Resources");
        GameObject buildingObject = new GameObject("UltraR2-Economy-Boundary-Buildings");
        GameObject researchObject = new GameObject("UltraR2-Economy-Boundary-Research");
        Building producer = ScriptableObject.CreateInstance<Building>();
        try
        {
            GameManager gameManager = root.AddComponent<GameManager>();
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            BuildingManager buildingManager = buildingObject.AddComponent<BuildingManager>();
            researchObject.AddComponent<ResearchManager>();
            SimulationManager simulationManager = root.AddComponent<SimulationManager>();

            gameManager.State.RestoreCoreForEditor(
                0,
                TechLevel.Ultra,
                new ExpantaNum(1e12d),
                0L);
            gameManager.State.AdjustFoodCapacityForEditor(new ExpantaNum(1e15d));
            gameManager.UltraProject.RestoreSaveDataForEditor(
                new UltraProjectStateSaveData
                {
                    ProjectId = UltraProjectState.ProjectId,
                    SaveVersion = UltraProjectState.CurrentSaveVersion,
                    Doctrine = UltraProjectDoctrine.Stable,
                    Status = UltraProjectStatus.Running,
                    CurrentStage = UltraProjectStage.Prototype,
                    StageProgress = "0",
                    CompletedStages = new List<UltraProjectStage>(),
                    LaunchFeePaid = true,
                    PauseReason = UltraProjectPauseReason.None,
                    StateVersion = 1
                });

            ConfigureFlowProducer(producer);
            BuildingState producerState = buildingManager.EnsureBuilding(producer);
            buildingManager.SetAmountAndRatesForEditor(producerState, ExpantaNum.One);

            UltraProjectStageDefinition stage = gameManager.UltraProject.Definition.Stages[0];
            for (int i = 0; i < stage.ContinuousResourceCosts.Count; i++)
            {
                Pair<Resource, ExpantaNum> cost = stage.ContinuousResourceCosts[i];
                resourceManager.SetAmount(cost.First, new ExpantaNum(1e12d));
            }

            if (offline)
                simulationManager.AdvanceOffline(seconds);
            else
            {
                const double tickSeconds = 0.1d;
                int tickCount = (int)(seconds / tickSeconds);
                for (int i = 0; i < tickCount; i++)
                    simulationManager.ManualTick(tickSeconds);
            }

            Resource continuousResource = stage.ContinuousResourceCosts[0].First;
            return new UltraSimulationSnapshot(
                gameManager.UltraProject.State.StageProgress.ToDouble(),
                gameManager.State.FoodAmount.ToDouble(),
                resourceManager.GetAmount(continuousResource).ToDouble());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(producer);
            UnityEngine.Object.DestroyImmediate(researchObject);
            UnityEngine.Object.DestroyImmediate(buildingObject);
            UnityEngine.Object.DestroyImmediate(resourceObject);
            UnityEngine.Object.DestroyImmediate(root);
            ProgressionModifierManager.Rebuild(null);
        }
    }

    private static void ConfigureFlowProducer(Building building)
    {
        building.SetIdForEditor("UltraR2EconomyBoundaryProducer");
        building.ConfigureEconomyForEditor(
            new ExpantaNum(1.15d),
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            new ExpantaNum(1e9d),
            ExpantaNum.Zero,
            new ExpantaNum(1e9d),
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>());
    }

    private readonly struct UltraSimulationSnapshot
    {
        public readonly double Progress;
        public readonly double Food;
        public readonly double ContinuousResource;

        public UltraSimulationSnapshot(double progress, double food, double continuousResource)
        {
            Progress = progress;
            Food = food;
            ContinuousResource = continuousResource;
        }
    }
}
