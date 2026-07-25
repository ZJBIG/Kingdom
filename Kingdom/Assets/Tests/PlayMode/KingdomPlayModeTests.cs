using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class KingdomPlayModeTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null)
                Object.Destroy(createdObjects[i]);
        createdObjects.Clear();
    }

    [UnityTest]
    public IEnumerator ResourceViewerDisabled_SimulationContinues()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-Managers");
        FindOrCreateManager<BuildingManager>("PlayMode-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-Managers");
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-Managers");
        yield return null;

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, ExpantaNum.Zero);
        resourceManager.SetProductionRate(wood, 10);

        GameObject viewerObject = new GameObject("PlayMode-ResourceViewer");
        createdObjects.Add(viewerObject);
        ResourceViewer viewer = viewerObject.AddComponent<ResourceViewer>();
        viewer.enabled = false;

        simulationManager.SetRunning(false);
        simulationManager.ManualTick(1d);

        Assert.That(gameManager.State, Is.Not.Null);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(10)));
    }

    [UnityTest]
    public IEnumerator MainTabSwitch_DoesNotMutateGameplayState()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-Navigation-Managers");
        yield return null;

        int versionBefore = gameManager.State.Version;
        GameObject navigationObject = new GameObject("PlayMode-Navigation");
        createdObjects.Add(navigationObject);
        MainNavigationViewer navigation = navigationObject.AddComponent<MainNavigationViewer>();

        navigation.SetMainTab(MainTab.Building);
        navigation.SetMainTab(MainTab.Research);
        navigation.SetMainTab(MainTab.Resource);

        Assert.That(navigation.CurrentTab, Is.EqualTo(MainTab.Resource));
        Assert.That(gameManager.State.Version, Is.EqualTo(versionBefore));
    }

    private T FindOrCreateManager<T>(string name) where T : Component
    {
        T existing = Object.FindObjectOfType<T>();
        if (existing != null)
            return existing;

        GameObject gameObject = GameObject.Find(name);
        if (gameObject == null)
        {
            gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
        }

        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
