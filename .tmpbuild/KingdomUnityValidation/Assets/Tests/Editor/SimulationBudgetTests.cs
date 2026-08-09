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
    public void Advance_ClampsBacklogToMaximumTicksPerFrame()
    {
        managerObject = new GameObject("Simulation-Budget-Test");
        managerObject.AddComponent<GameManager>();
        managerObject.AddComponent<ResourceManager>();
        managerObject.AddComponent<BuildingManager>();
        managerObject.AddComponent<ResearchManager>();
        SimulationManager simulationManager = managerObject.AddComponent<SimulationManager>();

        simulationManager.Advance(10d);

        FieldInfo accumulatedSeconds = typeof(SimulationManager).GetField(
            "accumulatedSeconds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(accumulatedSeconds, Is.Not.Null);
        Assert.That((double)accumulatedSeconds.GetValue(simulationManager), Is.EqualTo(2d).Within(0.000001d));
    }
}
