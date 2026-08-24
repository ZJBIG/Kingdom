using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SimulationBudgetTests
{
    private GameObject managerObject;

    [TearDown]
    public void TearDown()
    {
        if (managerObject != null)
            Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void Advance_PreservesBacklogWhileRespectingMaximumTicksPerFrame()
    {
        managerObject = new GameObject("Simulation-Budget-Test");
        managerObject.AddComponent<GameManager>();
        managerObject.AddComponent<ResourceManager>();
        managerObject.AddComponent<BuildingManager>();
        managerObject.AddComponent<ResearchManager>();
        SimulationManager simulationManager = managerObject.AddComponent<SimulationManager>();

        FieldInfo accumulatedSeconds = typeof(SimulationManager).GetField(
            "accumulatedSeconds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo tickIntervalSeconds = typeof(SimulationManager).GetField(
            "tickIntervalSeconds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo maximumTicksPerFrame = typeof(SimulationManager).GetField(
            "maximumTicksPerFrame",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(accumulatedSeconds, Is.Not.Null);
        Assert.That(tickIntervalSeconds, Is.Not.Null);
        Assert.That(maximumTicksPerFrame, Is.Not.Null);

        double frameBudget =
            (float)tickIntervalSeconds.GetValue(simulationManager) *
            (int)maximumTicksPerFrame.GetValue(simulationManager);
        simulationManager.Advance(frameBudget * 5d);
        Assert.That(
            (double)accumulatedSeconds.GetValue(simulationManager),
            Is.EqualTo(frameBudget * 4d).Within(0.000001d));

        for (int remainingFrameBudgets = 3; remainingFrameBudgets >= 0; remainingFrameBudgets--)
        {
            simulationManager.Advance(0d);
            Assert.That(
                (double)accumulatedSeconds.GetValue(simulationManager),
                Is.EqualTo(frameBudget * remainingFrameBudgets).Within(0.000001d));
        }
    }
}
