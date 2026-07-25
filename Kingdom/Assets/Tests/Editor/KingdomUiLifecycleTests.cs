using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class KingdomUiLifecycleTests
{
    private readonly System.Collections.Generic.List<Object> createdObjects =
        new System.Collections.Generic.List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
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

        simulationManager.SetRunning(false);
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, ExpantaNum.Zero);
        resourceManager.SetProductionRate(wood, 10);

        GameObject viewerObject = new GameObject("PlayMode-ResourceViewer");
        createdObjects.Add(viewerObject);
        ResourceViewer viewer = viewerObject.AddComponent<ResourceViewer>();
        viewer.enabled = false;

        simulationManager.ManualTick(1d);

        Assert.That(gameManager.State, Is.Not.Null);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(10)));
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisabledViewer_IsRemovedFromRefreshScheduler()
    {
        GameObject managerObject = new GameObject("PlayMode-UIRefreshManager");
        createdObjects.Add(managerObject);
        GameUIRefreshManager manager = managerObject.AddComponent<GameUIRefreshManager>();

        GameObject probeObject = new GameObject("PlayMode-RefreshProbe");
        createdObjects.Add(probeObject);
        RefreshProbe probe = probeObject.AddComponent<RefreshProbe>();

        yield return new WaitForSecondsRealtime(0.15f);
        int refreshCount = probe.RefreshCount;
        Assert.That(refreshCount, Is.GreaterThan(0));

        probe.enabled = false;
        yield return new WaitForSecondsRealtime(0.15f);

        Assert.That(probe.RefreshCount, Is.EqualTo(refreshCount));
        Assert.That(manager, Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator BuildingViewerDisabled_SimulationContinues()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-Building-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-Building-Managers");
        BuildingManager buildingManager = FindOrCreateManager<BuildingManager>("PlayMode-Building-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-Building-Managers");
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-Building-Managers");

        Building building = DataBase<Building>.Find("Farm");
        BuildingState state = buildingManager.EnsureBuilding(building);
        buildingManager.SetAutoBuild(building, true);
        GameObject viewerObject = new GameObject("PlayMode-BuildingViewer");
        createdObjects.Add(viewerObject);
        viewerObject.AddComponent<BuildingViewer>().enabled = false;

        simulationManager.SetRunning(false);
        ExpantaNum before = state.AutoBuildProgress;
        simulationManager.ManualTick(1d);

        Assert.That(state.AutoBuildProgress, Is.GreaterThan(before));
        Assert.That(gameManager.State, Is.Not.Null);
        Assert.That(resourceManager, Is.Not.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ResearchViewerDisabled_ResearchContinues()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-Research-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-Research-Managers");
        FindOrCreateManager<BuildingManager>("PlayMode-Research-Managers");
        ResearchManager researchManager = FindOrCreateManager<ResearchManager>("PlayMode-Research-Managers");
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-Research-Managers");

        Research research = FindResearchWithoutPrerequisites();
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
        viewerObject.AddComponent<ResearchViewer>().enabled = false;

        simulationManager.SetRunning(false);
        ExpantaNum before = state.Progress;
        simulationManager.ManualTick(1d);

        Assert.That(state.Progress, Is.GreaterThan(before));
        Assert.That(gameManager.State, Is.Not.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator SettingViewerDisabled_MusicManagerContinues()
    {
        GameObject musicObject = new GameObject("PlayMode-MusicManager");
        createdObjects.Add(musicObject);
        musicObject.AddComponent<AudioSource>();
        MusicManager musicManager = musicObject.AddComponent<MusicManager>();

        GameObject settingObject = new GameObject("PlayMode-SettingViewer");
        createdObjects.Add(settingObject);
        settingObject.AddComponent<SettingViewer>();
        settingObject.SetActive(false);

        AudioClip clip = AudioClip.Create("PlayModeClip", 4410, 1, 44100, false);
        createdObjects.Add(clip);
        Assert.That(musicManager.Play(clip), Is.True);
        Assert.That(musicManager.AudioSource.clip, Is.SameAs(clip));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ViewerReenabled_ImmediatelyShowsLatestState()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-Hud-Managers");
        GameObject hudObject = new GameObject("PlayMode-HudViewer");
        createdObjects.Add(hudObject);
        TextMeshProUGUI kingdomName = hudObject.AddComponent<TextMeshProUGUI>();
        GameHudViewer hud = hudObject.AddComponent<GameHudViewer>();
        SetPrivateField(hud, "Text_KingdomName", kingdomName);

        var restoreMethod = typeof(GameManager).GetMethod(
            "RestoreSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(restoreMethod, Is.Not.Null);
        restoreMethod.Invoke(gameManager, new object[] { new SaveManager.GameSaveData
        {
            KingdomName = "Latest State",
            FoodAmount = "10000",
            TechLevel = TechLevel.Primitive
        }});
        hud.enabled = false;
        hud.enabled = true;

        Assert.That(kingdomName.text, Is.EqualTo("Latest State"));
        yield return null;
    }

    [UnityTest]
    public IEnumerator MainTabSwitch_DoesNotMutateGameplayState()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-Navigation-Managers");
        int version = gameManager.State.Version;
        GameObject navigationObject = new GameObject("PlayMode-Navigation");
        createdObjects.Add(navigationObject);
        MainNavigationViewer navigation = navigationObject.AddComponent<MainNavigationViewer>();

        navigation.SetMainTab(MainTab.Building);
        navigation.SetMainTab(MainTab.Research);
        navigation.SetMainTab(MainTab.Resource);

        Assert.That(navigation.CurrentTab, Is.EqualTo(MainTab.Resource));
        Assert.That(gameManager.State.Version, Is.EqualTo(version));
        yield return null;
    }

    [UnityTest]
    public IEnumerator RepeatedEnableDisable_DoesNotDuplicateCardsOrSubscriptions()
    {
        GameObject managerObject = new GameObject("PlayMode-Repeated-RefreshManager");
        createdObjects.Add(managerObject);
        GameUIRefreshManager manager = managerObject.AddComponent<GameUIRefreshManager>();
        RefreshProbe probe = new GameObject("PlayMode-Repeated-Probe").AddComponent<RefreshProbe>();
        createdObjects.Add(probe.gameObject);

        manager.Register(probe);
        manager.Register(probe);
        Assert.That(manager.RegisteredViewerCount, Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(0.15f);
        int refreshCount = probe.RefreshCount;
        Assert.That(refreshCount, Is.GreaterThan(0));

        probe.enabled = false;
        probe.enabled = true;
        manager.Register(probe);
        Assert.That(manager.RegisteredViewerCount, Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(0.15f);

        Assert.That(probe.RefreshCount, Is.GreaterThan(refreshCount));
    }

    private static Research FindResearchWithoutPrerequisites()
    {
        foreach (Research research in DataBase<Research>.All)
            if (research.Prerequisites == null || research.Prerequisites.Count == 0)
                return research;
        throw new AssertionException("No research without prerequisites exists for the lifecycle test.");
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing serialized field '{name}'.");
        field.SetValue(target, value);
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

        return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
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

        public void RefreshUI() => RefreshCount++;
    }
}
