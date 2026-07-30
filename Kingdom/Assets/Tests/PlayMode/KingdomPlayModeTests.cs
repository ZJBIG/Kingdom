using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
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
    public IEnumerator NewGameStartup_InitializesCoreRuntimeState()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-NewGame-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-NewGame-Managers");
        FindOrCreateManager<BuildingManager>("PlayMode-NewGame-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-NewGame-Managers");
        yield return null;

        MethodInfo initializeNewGame = typeof(GameManager).GetMethod(
            "InitializeNewGame",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initializeNewGame, Is.Not.Null);
        initializeNewGame.Invoke(gameManager, null);

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Assert.That(gameManager.State.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(new ExpantaNum(300)));
        Assert.That(gameManager.State.Population.Population, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(resourceManager.GetState(wood).ProductionRate, Is.EqualTo(new ExpantaNum(1)));
    }

    [UnityTest]
    public IEnumerator NewGameFirstTenMinutes_SimulationSmokeRemainsStable()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-TenMinute-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-TenMinute-Managers");
        FindOrCreateManager<BuildingManager>("PlayMode-TenMinute-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-TenMinute-Managers");
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-TenMinute-Managers");
        yield return null;

        MethodInfo initializeNewGame = typeof(GameManager).GetMethod(
            "InitializeNewGame",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initializeNewGame, Is.Not.Null);
        initializeNewGame.Invoke(gameManager, null);
        simulationManager.SetRunning(true);

        for (int i = 0; i < 6000; i++)
        {
            simulationManager.Advance(0.1d);
            if (i % 250 == 0)
                yield return null;
        }

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Assert.That(gameManager.State.CalendarDays, Is.InRange(59, 60));
        Assert.That(gameManager.State.FoodAmount.IsFinite, Is.True);
        Assert.That(resourceManager.GetAmount(wood).IsFinite, Is.True);
    }

    [UnityTest]
    public IEnumerator FoodProducer_RecoversAfterInventoryAndPriorEfficiencyReachZero()
    {
        GameManager gameManager =
            FindOrCreateManager<GameManager>("PlayMode-FoodRecovery-Managers");
        FindOrCreateManager<ResourceManager>("PlayMode-FoodRecovery-Managers");
        BuildingManager buildingManager =
            FindOrCreateManager<BuildingManager>("PlayMode-FoodRecovery-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-FoodRecovery-Managers");
        SimulationManager simulationManager =
            FindOrCreateManager<SimulationManager>("PlayMode-FoodRecovery-Managers");
        yield return null;

        typeof(GameState).GetMethod(
                "RestoreCore", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[]
            {
                0, "Test", TechLevel.Animal, ExpantaNum.Zero, 0L
            });
        typeof(GameState).GetMethod(
                "RestorePopulation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[] { new ExpantaNum(3) });
        typeof(GameState).GetMethod(
                "AdjustPopulationCapacity", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[] { new ExpantaNum(10) });
        BuildingState farm =
            buildingManager.EnsureBuilding(DataBase<Building>.Find("Farm"));
        farm.SetAmountForEditor(1);
        farm.SetEfficiencyForEditor(ExpantaNum.Zero);

        simulationManager.SetRunning(false);
        simulationManager.ManualTick(1d);

        Assert.That(farm.Efficiency, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(gameManager.State.FoodAmount, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(gameManager.State.FoodSatisfaction, Is.EqualTo(ExpantaNum.One));
    }

    [UnityTest]
    public IEnumerator BuildingManager_RequiresEveryResearchAndWorkshopPrerequisite()
    {
        GameManager gameManager =
            FindOrCreateManager<GameManager>("PlayMode-Prerequisite-Managers");
        FindOrCreateManager<ResourceManager>("PlayMode-Prerequisite-Managers");
        BuildingManager buildingManager =
            FindOrCreateManager<BuildingManager>("PlayMode-Prerequisite-Managers");
        ResearchManager researchManager =
            FindOrCreateManager<ResearchManager>("PlayMode-Prerequisite-Managers");
        WorkshopManager workshopManager =
            FindOrCreateManager<WorkshopManager>("PlayMode-Prerequisite-Managers");
        yield return null;

        typeof(GameState).GetMethod(
            "AdvanceTechLevel", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[] { TechLevel.Industrial });

        Building refinery = DataBase<Building>.Find("OilRefinery");
        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out BuildFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.ResearchPrerequisiteIncomplete));

        MethodInfo restoreResearch = typeof(ResearchState).GetMethod(
            "Restore", BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(ExpantaNum), typeof(bool), typeof(bool) }, null);
        for (int i = 0; i < refinery.RequiredResearch.Count; i++)
            restoreResearch.Invoke(
                researchManager.GetState(refinery.RequiredResearch[i]),
                new object[] { ExpantaNum.Zero, false, true });

        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.WorkshopPrerequisiteIncomplete));

        WorkshopUpgradeDefinition upgrade = refinery.RequiredWorkshopUpgrades[0];
        typeof(WorkshopUpgradeState).GetMethod(
                "SetPurchased", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(workshopManager.States[upgrade], new object[] { true });

        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out failure), Is.True);
        Assert.That(failure, Is.EqualTo(BuildFailure.None));
    }

    [UnityTest]
    public IEnumerator ApplicationPause_StopsAndRestoresSimulationRunningState()
    {
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-PauseLifecycle");
        yield return null;

        MethodInfo applicationPause = typeof(SimulationManager).GetMethod(
            "OnApplicationPause",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(applicationPause, Is.Not.Null);

        simulationManager.SetRunning(true);
        applicationPause.Invoke(simulationManager, new object[] { true });
        Assert.That(simulationManager.IsRunning, Is.False);

        applicationPause.Invoke(simulationManager, new object[] { false });
        Assert.That(simulationManager.IsRunning, Is.True);

        simulationManager.SetRunning(false);
        applicationPause.Invoke(simulationManager, new object[] { true });
        applicationPause.Invoke(simulationManager, new object[] { false });
        Assert.That(simulationManager.IsRunning, Is.False);
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
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(6)));
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

    [UnityTest]
    public IEnumerator ViewerReenabled_ImmediatelyShowsLatestState()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-Hud-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-Hud-Managers");
        yield return null;

        MethodInfo initializeNew = typeof(GameState).GetMethod(
            "InitializeNew",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initializeNew, Is.Not.Null);
        initializeNew.Invoke(gameManager.State, new object[] { "Latest State" });

        GameObject hudObject = new GameObject("PlayMode-HudViewer");
        createdObjects.Add(hudObject);
        TextMeshProUGUI kingdomName = hudObject.AddComponent<TextMeshProUGUI>();
        GameHudViewer hud = hudObject.AddComponent<GameHudViewer>();
        SetPrivateField(hud, "Text_KingdomName", kingdomName);

        hud.enabled = false;
        hud.enabled = true;

        Assert.That(kingdomName.text, Is.EqualTo("Latest State"));
    }

    [UnityTest]
    public IEnumerator SimulationPaused_DoesNotAdvanceDuringUpdate()
    {
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-Pause-Managers");
        FindOrCreateManager<GameManager>("PlayMode-Pause-Managers");
        FindOrCreateManager<BuildingManager>("PlayMode-Pause-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-Pause-Managers");
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-Pause-Managers");
        yield return null;

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, ExpantaNum.Zero);
        resourceManager.SetProductionRate(wood, 10);
        simulationManager.SetRunning(false);

        yield return new WaitForSecondsRealtime(0.15f);

        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(ExpantaNum.Zero));
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

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing serialized field '{name}'.");
        field.SetValue(target, value);
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
