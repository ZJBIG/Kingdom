using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

    [UnityTest]
    public IEnumerator SettingViewerDisabled_MusicManagerContinues()
    {
        GameObject musicObject = new GameObject("PlayMode-MusicManager");
        createdObjects.Add(musicObject);
        AudioSource audioSource = musicObject.AddComponent<AudioSource>();
        MusicManager musicManager = musicObject.AddComponent<MusicManager>();

        GameObject settingObject = new GameObject("PlayMode-SettingViewer");
        createdObjects.Add(settingObject);
        settingObject.AddComponent<SettingViewer>();
        settingObject.SetActive(false);

        AudioClip clip = AudioClip.Create("PlayModeClip", 4410, 1, 44100, false);
        createdObjects.Add(clip);
        yield return null;

        Assert.That(musicManager.Play(clip), Is.True);
        Assert.That(audioSource.clip, Is.SameAs(clip));
    }

    [UnityTest]
    public IEnumerator BuildingViewerDisabled_SimulationContinues()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-Building-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-Building-Managers");
        BuildingManager buildingManager = FindOrCreateManager<BuildingManager>("PlayMode-Building-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-Building-Managers");
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-Building-Managers");
        yield return null;

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, ExpantaNum.Zero);
        Building lumberyard = DataBase<Building>.Find("Lumberyard");
        BuildingState state = buildingManager.EnsureBuilding(lumberyard);
        MethodInfo setAmountAndRates = typeof(BuildingManager).GetMethod(
            "SetAmountAndRates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setAmountAndRates, Is.Not.Null);
        setAmountAndRates.Invoke(buildingManager, new object[] { state, ExpantaNum.One });

        GameObject viewerObject = new GameObject("PlayMode-BuildingViewer");
        createdObjects.Add(viewerObject);
        BuildingViewer viewer = viewerObject.AddComponent<BuildingViewer>();
        viewer.enabled = false;

        simulationManager.SetRunning(false);
        simulationManager.ManualTick(1d);

        Assert.That(gameManager.State, Is.Not.Null);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(5)));
    }

    [UnityTest]
    public IEnumerator ResearchViewerDisabled_ResearchContinues()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-Research-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-Research-Managers");
        FindOrCreateManager<BuildingManager>("PlayMode-Research-Managers");
        ResearchManager researchManager = FindOrCreateManager<ResearchManager>("PlayMode-Research-Managers");
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-Research-Managers");
        yield return null;

        Research research = FindAvailableResearch(researchManager);
        ResearchState state = researchManager.GetState(research);
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = research.ResourceRequirements[i];
            resourceManager.SetAmount(requirement.First, requirement.Second * 2d);
        }

        Assert.That(ResearchManager.TryPayResearchCost(state), Is.True);
        Assert.That(researchManager.StartResearch(research), Is.True);

        GameObject viewerObject = new GameObject("PlayMode-ResearchViewer");
        createdObjects.Add(viewerObject);
        ResearchViewer viewer = viewerObject.AddComponent<ResearchViewer>();
        viewer.enabled = false;

        simulationManager.SetRunning(false);
        ExpantaNum before = state.Progress;
        simulationManager.ManualTick(1d);

        Assert.That(gameManager.State, Is.Not.Null);
        Assert.That(state.Progress, Is.GreaterThan(before));
    }

    [UnityTest]
    public IEnumerator RepeatedEnableDisable_DoesNotDuplicateRefreshRegistration()
    {
        GameUIRefreshManager manager = Object.FindObjectOfType<GameUIRefreshManager>();
        if (manager == null)
        {
            GameObject managerObject = new GameObject("PlayMode-UIRefreshManager");
            createdObjects.Add(managerObject);
            manager = managerObject.AddComponent<GameUIRefreshManager>();
        }

        GameObject probeObject = new GameObject("PlayMode-RefreshProbe");
        createdObjects.Add(probeObject);
        RefreshProbe probe = probeObject.AddComponent<RefreshProbe>();
        yield return null;

        manager.Register(probe);
        manager.Register(probe);
        Assert.That(manager.RegisteredViewerCount, Is.EqualTo(1));

        probe.enabled = false;
        Assert.That(manager.RegisteredViewerCount, Is.EqualTo(0));
        probe.enabled = true;
        manager.Register(probe);
        Assert.That(manager.RegisteredViewerCount, Is.EqualTo(1));

        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(probe.RefreshCount, Is.GreaterThan(0));
    }

    private static Research FindAvailableResearch(ResearchManager researchManager)
    {
        IReadOnlyList<Research> researches = DataBase<Research>.All;
        for (int i = 0; i < researches.Count; i++)
        {
            Research research = researches[i];
            ResearchState state = researchManager.GetState(research);
            if (state.Status != ResearchStatus.Completed &&
                (research.Prerequisites == null || research.Prerequisites.Count == 0))
                return research;
        }

        Assert.Fail("No unfinished research without prerequisites is available for the PlayMode test.");
        return null;
    }

    private sealed class RefreshProbe : MonoBehaviour, IGameUIRefreshable
    {
        public int RefreshCount { get; private set; }

        private void OnEnable()
        {
            GameUIRefreshManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            GameUIRefreshManager.Instance?.Unregister(this);
        }

        public void RefreshUI()
        {
            RefreshCount++;
        }
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
