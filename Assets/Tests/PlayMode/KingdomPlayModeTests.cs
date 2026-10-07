using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class KingdomPlayModeTests
{
    private readonly List<Object> createdObjects = new List<Object>();
    private string saveRoot;
    private readonly List<Scene> loadedScenes = new List<Scene>();
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        loadedScenes.Clear();
        SceneManager.sceneLoaded += TrackLoadedScene;
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        saveRoot = Path.Combine(projectRoot, "Temp", "KingdomPlayModeTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [TearDown]
    public void TearDown()
    {
        SceneManager.sceneLoaded -= TrackLoadedScene;
        // Destroy test-loaded scene roots while the isolated save root is still
        // active. Include roots created after sceneLoaded (for example runtime UI).
        foreach (Scene scene in loadedScenes)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                continue;
            foreach (GameObject root in scene.GetRootGameObjects())
                Object.DestroyImmediate(root);
        }
        loadedScenes.Clear();
        // These two managers may move out of SampleScene into DontDestroyOnLoad.
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        if (tutorial != null && tutorial != initialTutorial)
            Object.DestroyImmediate(tutorial.gameObject);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (music != null && music != initialMusic)
            Object.DestroyImmediate(music.gameObject);
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
        SaveManager.ClearSaveRootOverrideForTests();
        if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot))
            Directory.Delete(saveRoot, true);
        saveRoot = null;
    }



    private void TrackLoadedScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "SampleScene")
            loadedScenes.Add(scene);
    }

    [UnityTest]
    public IEnumerator NewGameStartup_InitializesCoreRuntimeState()
    {
        yield return LoadIsolatedNewGame();
        GameManager gameManager = GameManager.Instance;
        ResourceManager resourceManager = ResourceManager.Instance;

        Resource wood = DataBase<Resource>.Find("WoodLog");
        // Era and counts are discrete contracts; startup inventory/rates may be tuned.
        Assert.That(gameManager.State.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(gameManager.State.FoodAmount, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(gameManager.State.Population.Population, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(resourceManager.GetAmount(wood), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(resourceManager.GetState(wood).ProductionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        foreach (BuildingState state in BuildingManager.Instance.States.Values)
            Assert.That(state.Amount, Is.EqualTo(ExpantaNum.Zero),
                "A new game must not grant a player-built building: " + state.Definition.Id);
        Assert.That(ResearchManager.Instance.TotalFinishedResearchCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator NewGameFirstTenMinutes_SimulationSmokeRemainsStable()
    {
        yield return LoadIsolatedNewGame();
        GameManager gameManager = GameManager.Instance;
        ResourceManager resourceManager = ResourceManager.Instance;
        SimulationManager simulationManager = SimulationManager.Instance;
        int daysBefore = gameManager.State.CalendarDays;

        // Keep automatic Update disabled: yielded frames must not add wall time
        // on top of the explicitly simulated ten minutes.
        for (int i = 0; i < 6000; i++)
        {
            simulationManager.ManualTick(0.1d);
            if (i % 250 == 0)
                yield return null;
        }

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Assert.That(gameManager.State.CalendarDays - daysBefore, Is.InRange(59, 60));
        Assert.That(gameManager.State.FoodAmount.IsFinite, Is.True);
        Assert.That(resourceManager.GetAmount(wood).IsFinite, Is.True);
    }

    [UnityTest]
    public IEnumerator NewGameCommands_BuildResearchAndReloadWithoutGrants()
    {
        yield return LoadIsolatedNewGame();
        GameManager game = GameManager.Instance;
        ResourceManager resources = ResourceManager.Instance;
        BuildingManager buildings = BuildingManager.Instance;
        ResearchManager researches = ResearchManager.Instance;
        SimulationManager simulation = SimulationManager.Instance;
        SaveManager saves = SaveManager.Instance;
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Building house = DataBase<Building>.Find("WoodHouse");
        Building farm = DataBase<Building>.Find("Farm");
        Research agriculture = DataBase<Research>.Find("Agriculture");
        int simulatedSeconds = 0;
        const int simulationBudget = 600; // Bounded regression, not a human pacing claim.

        while (buildings.GetMaxBuildable(house, ExpantaNum.One) < ExpantaNum.One &&
               simulatedSeconds < simulationBudget)
        {
            simulation.ManualTick(1d);
            simulatedSeconds++;
        }
        ExpantaNum woodBeforeHouse = resources.GetAmount(wood);
        ExpantaNum capacityBeforeHouse = game.State.Population.PopulationCapacity;
        Assert.That(buildings.TryBuild(house, ExpantaNum.One, out BuildFailure failure),
            Is.True, "Natural production must fund the first house. Failure=" + failure);
        Assert.That(resources.GetAmount(wood), Is.LessThan(woodBeforeHouse));
        Assert.That(game.State.Population.PopulationCapacity, Is.GreaterThan(capacityBeforeHouse));
        Assert.That(buildings.GetState(house).Amount, Is.EqualTo(ExpantaNum.One),
            "Exactly one building was commanded; this is a discrete count.");

        // A locked production building must fail without charging or granting it.
        ExpantaNum woodBeforeLockedBuild = resources.GetAmount(wood);
        ExpantaNum territoryBeforeLockedBuild = game.State.TerritoryUsed;
        ExpantaNum productivityBeforeLockedBuild = buildings.AvailableProductivity;
        Assert.That(buildings.TryBuild(farm, ExpantaNum.One, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.ResearchPrerequisiteIncomplete));
        Assert.That((resources.GetAmount(wood) - woodBeforeLockedBuild).Abs(),
            Is.LessThan(new ExpantaNum(0.000001)));
        Assert.That((game.State.TerritoryUsed - territoryBeforeLockedBuild).Abs(),
            Is.LessThan(new ExpantaNum(0.000001)));
        Assert.That((buildings.AvailableProductivity - productivityBeforeLockedBuild).Abs(),
            Is.LessThan(new ExpantaNum(0.000001)));
        if (buildings.States.TryGetValue(farm, out BuildingState lockedFarm))
            Assert.That(lockedFarm.Amount, Is.EqualTo(ExpantaNum.Zero));

        while (!researches.CanPayResearchCost(agriculture, out _) &&
               simulatedSeconds < simulationBudget)
        {
            simulation.ManualTick(1d);
            simulatedSeconds++;
        }
        Assert.That(researches.CanPayResearchCost(agriculture, out string blocker), Is.True, blocker);
        var inventoryBeforeResearch = new Dictionary<Resource, ExpantaNum>();
        foreach (Pair<Resource, ExpantaNum> requirement in agriculture.ResourceRequirements)
            inventoryBeforeResearch.Add(requirement.First, resources.GetAmount(requirement.First));
        // No tick between these observations: a payment ledger alone must not
        // pass if the underlying inventory was never actually charged.
        Assert.That(researches.HandleResearchAction(agriculture), Is.EqualTo(ResearchActionResult.Started));
        foreach (Pair<Resource, ExpantaNum> requirement in agriculture.ResourceRequirements)
            Assert.That((inventoryBeforeResearch[requirement.First] -
                resources.GetAmount(requirement.First) - requirement.Second).Abs(),
                Is.LessThan(new ExpantaNum(0.000001)),
                "Starting research must consume its real cost: " + requirement.First.Id);
        ResearchState research = researches.GetState(agriculture);
        while (research.Status != ResearchStatus.Completed && simulatedSeconds < simulationBudget)
        {
            simulation.ManualTick(1d);
            simulatedSeconds++;
        }
        Assert.That(research.CostPaid, Is.True, "Queueing alone is not payment.");
        Assert.That(research.Progress, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(research.Status, Is.EqualTo(ResearchStatus.Completed),
            "Agriculture must finish through paid simulation, not a forced status.");
        foreach (Pair<Resource, ExpantaNum> requirement in agriculture.ResourceRequirements)
            Assert.That((research.GetPaidResourceCost(requirement.First) - requirement.Second).Abs(),
                Is.LessThan(new ExpantaNum(0.000001)),
                "The payment ledger must match the actual definition: " + requirement.First.Id);

        while (buildings.GetMaxBuildable(farm, ExpantaNum.One) < ExpantaNum.One &&
               simulatedSeconds < simulationBudget)
        {
            simulation.ManualTick(1d);
            simulatedSeconds++;
        }
        ExpantaNum woodBeforeFarm = resources.GetAmount(wood);
        ExpantaNum foodProductionBeforeFarm = game.State.FoodProductionRate;
        Assert.That(buildings.TryBuild(farm, ExpantaNum.One, out failure), Is.True,
            "Paid research must unlock a naturally affordable producer. Failure=" + failure);
        Assert.That(resources.GetAmount(wood), Is.LessThan(woodBeforeFarm));
        simulation.ManualTick(1d);
        Assert.That(buildings.GetState(farm).Efficiency, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(game.State.FoodProductionRate, Is.GreaterThan(foodProductionBeforeFarm),
            "The new producer must add food output, not merely leave baseline gathering positive.");
        Assert.That(game.State.Population.Population, Is.GreaterThan(ExpantaNum.Zero));

        Assert.That(saves.SaveNow(true), Is.True);
        Assert.That(saves.HasSave, Is.True);
        ExpantaNum savedWood = resources.GetAmount(wood);
        ExpantaNum savedPopulation = game.State.Population.Population;
        ExpantaNum savedFood = game.State.FoodAmount;
        // Change live state through a real tick; a no-op load must not pass.
        simulation.ManualTick(1d);
        Assert.That(resources.GetAmount(wood), Is.GreaterThan(savedWood));
        Assert.That(saves.LoadOrCreateGame(), Is.True,
            "Reload must use the isolated on-disk v9 save, not initialize a new game.");
        Assert.That(saves.LastLoadCreatedNewGame, Is.False);
        Assert.That((resources.GetAmount(wood) - savedWood).Abs(), Is.LessThan(new ExpantaNum(0.000001)));
        Assert.That((game.State.Population.Population - savedPopulation).Abs(), Is.LessThan(new ExpantaNum(0.000001)));
        Assert.That((game.State.FoodAmount - savedFood).Abs(), Is.LessThan(new ExpantaNum(0.000001)));
        Assert.That(buildings.GetState(house).Amount, Is.EqualTo(ExpantaNum.One));
        Assert.That(buildings.GetState(farm).Amount, Is.EqualTo(ExpantaNum.One));
        Assert.That(researches.GetState(agriculture).Status, Is.EqualTo(ResearchStatus.Completed));
        Assert.That(researches.GetState(agriculture).CostPaid, Is.True);
        foreach (ResourceState state in resources.States.Values)
        {
            Assert.That(state.Amount.IsFinite, Is.True, state.Definition.Id);
            Assert.That(state.Amount, Is.GreaterThanOrEqualTo(ExpantaNum.Zero), state.Definition.Id);
        }
        Debug.Log("[KingdomOnboarding] Real bootstrap -> house -> paid Agriculture -> farm -> " +
            "isolated save/reload; simulated seconds=" + simulatedSeconds + "; no resource grants.");
    }

    [UnityTest]
    public IEnumerator UltraProjectExpeditionDoctrine_IsGatedAndPersistsThroughReload()
    {
        yield return LoadIsolatedNewGame();
        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        GameManager gameManager = GameManager.Instance;
        SaveManager saveManager = SaveManager.Instance;
        Building phaseEnergyArray = DataBase<Building>.Find("PhaseEnergyArray");
        Assert.That(root, Is.Not.Null);
        Assert.That(phaseEnergyArray, Is.Not.Null);

        gameManager.State.AdvanceTechLevelForEditor(TechLevel.Ultra);
        gameManager.UltraProject.RestoreSaveDataForEditor(
            CreateUltraProjectSave(
                UltraProjectDoctrine.Stable,
                UltraProjectStatus.Ready,
                UltraProjectStage.Prototype,
                "0",
                new List<UltraProjectStage>(),
                false,
                100));
        // The authored list only displays buildings that are already available
        // or owned. Seed the target row without bypassing the UI's real
        // display predicate so this test exercises the existing detail action.
        BuildingState phaseEnergyState = BuildingManager.Instance.EnsureBuilding(phaseEnergyArray);
        BuildingManager.Instance.SetAmountAndRatesForEditor(
            phaseEnergyState,
            ExpantaNum.One);
        root.SetPage("Buildings");
        yield return null;

        Button buildingCard = root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button =>
            {
                TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
                return label != null && label.text == phaseEnergyArray.Label;
            });
        Assert.That(buildingCard, Is.Not.Null,
            "The authored Ultra building row must expose the existing detail action.");
        buildingCard.onClick.Invoke();
        yield return null;

        Button doctrineButton = root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.name == "Doctrine");
        Assert.That(doctrineButton, Is.Not.Null);
        Assert.That(doctrineButton.gameObject.activeSelf, Is.True);
        Assert.That(doctrineButton.interactable, Is.False,
            "Expedition must remain gated before the civilization engineering is committed.");

        gameManager.UltraProject.RestoreSaveDataForEditor(
            CreateUltraProjectSave(
                UltraProjectDoctrine.Stable,
                UltraProjectStatus.Committed,
                UltraProjectStage.Completed,
                "1",
                new List<UltraProjectStage>
                {
                    UltraProjectStage.Prototype,
                    UltraProjectStage.Stabilization,
                    UltraProjectStage.Expansion
                },
                true,
                101));
        Assert.That(gameManager.UltraProject.IsCampaignDoctrineUnlocked, Is.True,
            "A committed Ultra save must expose the campaign posture gate.");
        root.RefreshUI();
        // The detail presenter is throttled with the normal live-refresh
        // cadence; allow that authored button binding to observe the commit.
        yield return new WaitForSeconds(0.35f);

        doctrineButton = root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.name == "Doctrine");
        TMP_Text doctrineLabel = doctrineButton.GetComponentInChildren<TMP_Text>(true);
        Assert.That(doctrineButton.interactable, Is.True,
            "A committed civilization engineering project must enable posture switching.");
        Assert.That(doctrineLabel.text, Does.Contain("切换远征供给"));

        doctrineButton.onClick.Invoke();
        yield return null;
        Assert.That(gameManager.UltraProject.State.Doctrine,
            Is.EqualTo(UltraProjectDoctrine.Expedition));
        Assert.That(saveManager.SaveNow(true), Is.True);

        Assert.That(gameManager.UltraProject.TrySetOperationalDoctrine(
            UltraProjectDoctrine.Stable, out _), Is.True);
        Assert.That(saveManager.LoadOrCreateGame(), Is.True,
            "Reload must restore the committed Ultra posture from the v9 save.");
        Assert.That(saveManager.LastLoadCreatedNewGame, Is.False);
        Assert.That(gameManager.UltraProject.State.Doctrine,
            Is.EqualTo(UltraProjectDoctrine.Expedition));

        SectorDefinition occupiedSector = DataBase<SectorDefinition>.Find("AlphaCentauri");
        Assert.That(occupiedSector, Is.Not.Null);
        foreach (SectorDefinition prerequisite in occupiedSector.PrerequisiteSectors)
        {
            SectorState prerequisiteState = gameManager.Sectors.GetState(prerequisite);
            prerequisiteState.SetUnlockedForEditor(true);
            prerequisiteState.SetOccupiedForEditor(true);
        }
        SectorState occupiedSectorState = gameManager.Sectors.GetState(occupiedSector);
        occupiedSectorState.SetUnlockedForEditor(true);
        occupiedSectorState.SetOccupiedForEditor(true);
        root.SetPage("Sectors");
        yield return null;
        Transform occupiedSectorRowTransform = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Sectors/DataRows/SectorRow_AlphaCentauri");
        Assert.That(occupiedSectorRowTransform, Is.Not.Null,
            "The occupied remote sector must be present in the authored Sectors page.");
        Button occupiedSectorRow = occupiedSectorRowTransform.GetComponent<Button>();
        Assert.That(occupiedSectorRow, Is.Not.Null);
        occupiedSectorRow.onClick.Invoke();
        yield return null;
        Button campaignDoctrineButton = root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.name == "Doctrine");
        Assert.That(campaignDoctrineButton, Is.Not.Null);
        Assert.That(campaignDoctrineButton.gameObject.activeSelf, Is.True,
            "An occupied remote sector must still expose the unlocked expedition posture control.");
        RectTransform campaignDoctrineRect = campaignDoctrineButton.transform as RectTransform;
        RectTransform detailFooterRect = campaignDoctrineRect.parent as RectTransform;
        Assert.That(campaignDoctrineRect.rect.width,
            Is.GreaterThan(detailFooterRect.rect.width * 0.7f),
            "When it is the only available action, the doctrine button must use the single-action slot.");
        campaignDoctrineButton.onClick.Invoke();
        Assert.That(gameManager.State.Campaign.Doctrine, Is.EqualTo(CampaignDoctrine.Surge),
            "The sector detail posture control must issue the campaign doctrine command.");
    }

    [UnityTest]
    public IEnumerator UltraLockedOverview_NavigatesToPhaseEnergyArrayDetailAndRefreshesConditions()
    {
        yield return LoadIsolatedNewGame();
        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        GameManager gameManager = GameManager.Instance;
        Building phaseEnergyArray = DataBase<Building>.Find("PhaseEnergyArray");
        Assert.That(root, Is.Not.Null);
        Assert.That(phaseEnergyArray, Is.Not.Null);

        gameManager.State.AdvanceTechLevelForEditor(TechLevel.Ultra);
        gameManager.UltraProject.RestoreSaveDataForEditor(
            CreateUltraProjectSave(
                UltraProjectDoctrine.None,
                UltraProjectStatus.Locked,
                UltraProjectStage.None,
                "0",
                new List<UltraProjectStage>(),
                false,
                200));
        root.SetPage("Overview");
        root.RefreshUI();
        yield return new WaitForSeconds(0.35f);
        yield return null;
        Canvas.ForceUpdateCanvases();

        Transform primaryCard = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Overview/PrimaryCard");
        TMP_Text guidance = primaryCard?.Find("Text")?.GetComponent<TMP_Text>();
        Button navigation = primaryCard?.Find("NavigationButton")?.GetComponent<Button>();
        Assert.That(guidance, Is.Not.Null);
        Assert.That(guidance.text, Does.Contain("文明工程"),
            "Ultra Overview guidance must describe the locked civilization project.");
        Assert.That(guidance.text, Does.Contain("阻碍："));
        Assert.That(guidance.text, Does.Contain("下一步："));
        Assert.That(navigation, Is.Not.Null);
        Assert.That(navigation.interactable, Is.True,
            "A locked Ultra project must expose its authored Overview navigation action.");

        navigation.onClick.Invoke();
        yield return null;
        yield return null;
        Transform detailBody = root.transform.Find(
            "SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body");
        TMP_Text detailText = detailBody?.GetComponent<TMP_Text>();
        Assert.That(detailText, Is.Not.Null);
        Assert.That(root.transform.Find("SafeAreaRoot/Content/PageHost/Buildings"), Is.Not.Null);
        Assert.That(detailText.text, Does.Contain(phaseEnergyArray.Label),
            "Overview navigation must open the PhaseEnergyArray detail target.");
        Assert.That(detailText.text, Does.Contain("状态: 锁定"));
        Assert.That(detailText.text, Does.Contain("阻碍:"),
            "Locked Ultra detail must expose the current blocking condition.");
        Assert.That(detailText.text, Does.Contain("下一步:"),
            "Locked Ultra detail must expose the next action.");
        string lockedDetail = detailText.text;

        gameManager.UltraProject.RestoreSaveDataForEditor(
            CreateUltraProjectSave(
                UltraProjectDoctrine.Stable,
                UltraProjectStatus.Ready,
                UltraProjectStage.Prototype,
                "0",
                new List<UltraProjectStage>(),
                false,
                201));
        root.RefreshUI();
        yield return new WaitForSeconds(0.35f);
        Assert.That(detailText.text, Does.Contain("状态: 待启动"),
            "Changing the civilization project conditions must refresh the open detail.");
        Assert.That(detailText.text, Is.Not.EqualTo(lockedDetail),
            "Ultra detail text must change when the project state changes.");
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

        gameManager.State.RestoreCoreForEditor(
            0, TechLevel.Animal, ExpantaNum.Zero, 0L);
        gameManager.State.RestorePopulationForEditor(new ExpantaNum(3));
        gameManager.State.AdjustPopulationCapacityForEditor(new ExpantaNum(10));
        BuildingState farm =
            buildingManager.EnsureBuilding(DataBase<Building>.Find("Farm"));
        farm.SetAmountForEditor(1);
        farm.SetEfficiencyForEditor(ExpantaNum.Zero);

        simulationManager.SetRunning(false);
        simulationManager.ManualTick(1d);

        Assert.That(farm.Efficiency, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(gameManager.State.FoodAmount, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(gameManager.State.HappinessMultiplier, Is.GreaterThanOrEqualTo(ExpantaNum.One));
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

        gameManager.InitializeNewGameForEditor();
        researchManager.ResetForLoadForEditor();
        workshopManager.ResetForLoadForEditor();

        gameManager.State.AdvanceTechLevelForEditor(TechLevel.Industrial);

        Building refinery = DataBase<Building>.Find("OilRefinery");
        Assert.That(refinery.RequiredResearch, Is.Not.Empty);
        Assert.That(refinery.RequiredWorkshopUpgrades, Is.Not.Empty);
        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out BuildFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.ResearchPrerequisiteIncomplete)
            .Or.EqualTo(BuildFailure.WorkshopPrerequisiteIncomplete));

        for (int i = 0; i < refinery.RequiredResearch.Count; i++)
        {
            Research required = refinery.RequiredResearch[i];
            var paidCosts = new Dictionary<Resource, ExpantaNum>();
            foreach (Pair<Resource, ExpantaNum> requirement in required.ResourceRequirements)
                paidCosts[requirement.First] = requirement.Second;
            researchManager.GetState(required).RestoreForEditor(
                ExpantaNum.Zero, true, true, paidCosts);
        }

        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.WorkshopPrerequisiteIncomplete));

        WorkshopUpgrade upgrade = refinery.RequiredWorkshopUpgrades[0];
        workshopManager.States[upgrade].SetPurchasedForEditor(true);

        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out failure), Is.True);
        Assert.That(failure, Is.EqualTo(BuildFailure.None));
    }

    [UnityTest]
    public IEnumerator ApplicationPause_StopsAndRestoresSimulationRunningState()
    {
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-PauseLifecycle");
        yield return null;

        simulationManager.SetRunning(true);
        simulationManager.OnApplicationPauseForEditor(true);
        Assert.That(simulationManager.IsRunning, Is.False);

        simulationManager.OnApplicationPauseForEditor(false);
        Assert.That(simulationManager.IsRunning, Is.True);

        simulationManager.SetRunning(false);
        simulationManager.OnApplicationPauseForEditor(true);
        simulationManager.OnApplicationPauseForEditor(false);
        Assert.That(simulationManager.IsRunning, Is.False);
    }


    [UnityTest]
    public IEnumerator OverviewDevelopmentGuidance_IsReadOnlyAndUnique()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        Transform primaryCard = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Overview/PrimaryCard");
        Assert.That(primaryCard, Is.Not.Null, "Overview must contain its authored PrimaryCard.");
        TMP_Text body = primaryCard.Find("Text")?.GetComponent<TMP_Text>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.text, Is.Not.Empty);
        Assert.That(body.preferredHeight, Is.GreaterThan(0f),
            "Development guidance Body must have measurable text content.");

        SimulationManager simulation = Object.FindObjectOfType<SimulationManager>();
        GameManager game = Object.FindObjectOfType<GameManager>();
        Assert.That(simulation, Is.Not.Null);
        Assert.That(game, Is.Not.Null);
        simulation.SetRunning(false);
        Assert.That(GameBootstrap.Instance.Completed, Is.True,
            "The scene must complete the real bootstrap before measuring refresh.");
        Assert.That(SaveManager.Instance.LastLoadCreatedNewGame, Is.True,
            "This fixture must start from its isolated empty save directory.");
        Transform overview = primaryCard.parent;
        string stateBeforeRefresh = CaptureOverviewState();
        int versionBeforeRefresh = game.State.Version;
        for (int refresh = 0; refresh < 2; refresh++)
        {
            // Clear presentation only: a missing/no-op refresh must now fail.
            body.text = string.Empty;
            root.RefreshUI();
            Assert.That(body.text, Is.Not.Empty,
                "The real refresh must repopulate the authored guidance text.");
            Assert.That(game.State.Version, Is.EqualTo(versionBeforeRefresh),
                "Refreshing guidance must not mutate GameState.");
            Assert.That(CaptureOverviewState(), Is.EqualTo(stateBeforeRefresh),
                "Refreshing guidance must not mutate gameplay or saved progression.");

            int primaryCardCount = 0;
            int navigationCount = 0;
            foreach (Transform item in overview.GetComponentsInChildren<Transform>(true))
            {
                if (!item.gameObject.activeInHierarchy)
                    continue;
                if (item.name.StartsWith("PrimaryCard", StringComparison.Ordinal))
                    primaryCardCount++;
                if (item.parent != null &&
                    item.parent.name.StartsWith("PrimaryCard", StringComparison.Ordinal) &&
                    item.GetComponent<Button>() != null)
                    navigationCount++;
            }
            // Exact equality is intentional for a single authored primary goal/action.
            Assert.That(primaryCardCount, Is.EqualTo(1));
            Assert.That(navigationCount, Is.EqualTo(1));
            Assert.That(primaryCard.GetComponentsInChildren<TMP_Text>(true)
                .Count(text => text.text == body.text), Is.EqualTo(1),
                "The primary goal text must not be duplicated inside the card.");
        }
        Debug.Log("[KingdomUI] Overview: two real refreshes rendered guidance; " +
            "gameplay snapshots unchanged; one primary goal and navigation action.");
    }









    [UnityTest]
    public IEnumerator ResearchQueue_PaysAutomaticallyWhenReachingHead()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-ResearchQueue");
        ResourceManager resourceManager =
            FindOrCreateManager<ResourceManager>("PlayMode-ResearchQueue");
        FindOrCreateManager<BuildingManager>("PlayMode-ResearchQueue");
        ResearchManager researchManager =
            FindOrCreateManager<ResearchManager>("PlayMode-ResearchQueue");
        yield return null;

        gameManager.InitializeNewGameForEditor();
        researchManager.ResetForLoadForEditor();

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, 1000);
        Research active = DataBase<Research>.Find("Agriculture");
        Research queued = DataBase<Research>.Find("ControlledFire");

        Assert.That(researchManager.HandleResearchAction(active), Is.EqualTo(ResearchActionResult.Started));
        Assert.That(researchManager.HandleResearchAction(queued), Is.EqualTo(ResearchActionResult.Queued));
        Assert.That(researchManager.ResearchQueue.Any(state => state.Definition == queued), Is.True);
        Assert.That(researchManager.GetState(queued).CostPaid, Is.False);
        researchManager.Tick(1000000d);
        researchManager.TryStartNextQueuedResearch();
        Assert.That(researchManager.GetState(queued).CostPaid, Is.True);
        Assert.That(researchManager.ActiveResearch, Is.Not.Null);
        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(queued));
    }

    [UnityTest]
    public IEnumerator EraTransitions_CompleteInOrderAndExposeNextEraContent()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-EraChain-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-EraChain-Managers");
        BuildingManager buildingManager = FindOrCreateManager<BuildingManager>("PlayMode-EraChain-Managers");
        ResearchManager researchManager = FindOrCreateManager<ResearchManager>("PlayMode-EraChain-Managers");
        WorkshopManager workshopManager = FindOrCreateManager<WorkshopManager>("PlayMode-EraChain-Managers");
        yield return null;

        gameManager.InitializeNewGameForEditor();

        researchManager.ResetForLoadForEditor();
        workshopManager.ResetForLoadForEditor();
        GrantResearchTestResources(resourceManager);

        Assert.That(gameManager.State.TechLevel, Is.EqualTo(TechLevel.Animal));
        var transitions = new[]
        {
            new { Id = "StoneAgeSettlement", Target = TechLevel.StoneAge },
            new { Id = "FeudalAdministration", Target = TechLevel.Medieval },
            new { Id = "Industrialization", Target = TechLevel.Industrial },
            new { Id = "InterstellarNavigation", Target = TechLevel.Spacer }
        };

        for (int i = 0; i < transitions.Length; i++)
        {
            Research transition = DataBase<Research>.Find(transitions[i].Id);
            Assert.That(transition, Is.Not.Null);
            Assert.That(transition.AdvancesTechLevel, Is.True);
            Assert.That(transition.TechLevel, Is.EqualTo(transitions[i].Target));
            CompleteResearchPrerequisites(
                gameManager,
                researchManager,
                resourceManager,
                transition,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.That(researchManager.CanAccessResearch(transition), Is.True,
                "Transition should be accessible from the preceding era: " + transition.Id);

            CompleteResearchThroughRuntime(gameManager, researchManager, resourceManager, transition);
            Assert.That(researchManager.IsResearchCompleted(transition.Id), Is.True);
            Assert.That(gameManager.State.TechLevel, Is.EqualTo(transitions[i].Target),
                "Completed transition must update the authoritative TechLevel.");

            Research nextResearch = DataBase<Research>.All.FirstOrDefault(value =>
                value != null && value.TechLevel == transitions[i].Target &&
                !value.AdvancesTechLevel);
            Assert.That(nextResearch, Is.Not.Null,
                "No next-era Research definition exists after " + transition.Id);

            Building nextBuilding = DataBase<Building>.All.FirstOrDefault(value =>
                value != null && value.TechLevel == transitions[i].Target);
            Assert.That(nextBuilding, Is.Not.Null,
                "No next-era Building definition exists after " + transition.Id);

            if (transitions[i].Target != TechLevel.Industrial &&
                transitions[i].Target != TechLevel.Spacer)
                continue;

            if (transitions[i].Target == TechLevel.Industrial)
            {
                Research workshopTheory = DataBase<Research>.Find("IndustrialWorkshop");
                Assert.That(workshopTheory, Is.Not.Null);
                CompleteResearchThroughRuntime(gameManager, researchManager, resourceManager, workshopTheory);
                Assert.That(workshopManager.IsSystemUnlocked, Is.True,
                    "IndustrialWorkshop must unlock the Workshop system at runtime.");
            }

            WorkshopUpgrade nextWorkshop = DataBase<WorkshopUpgrade>.All.FirstOrDefault(value =>
                value != null && value.TechLevel == transitions[i].Target &&
                value.RequiredUpgrades.Count == 0 &&
                (transitions[i].Target != TechLevel.Industrial ||
                    workshopManager.ArePrerequisitesMet(value)));
            Assert.That(nextWorkshop, Is.Not.Null,
                "No next-era Workshop definition exists after " + transition.Id);
            if (transitions[i].Target == TechLevel.Industrial)
            {
                Assert.That(workshopManager.TryPurchase(nextWorkshop, out WorkshopPurchaseFailure failure), Is.True,
                    "Next-era Workshop purchase failed: " + failure);
            }
        }
    }

    [UnityTest]
    public IEnumerator HomeSystemGuidance_OverviewOpensFirstReachableSector()
    {
        yield return LoadIsolatedNewGame();
        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        GameManager game = GameManager.Instance;
        ResearchManager research = ResearchManager.Instance;
        game.State.AdvanceTechLevelForEditor(TechLevel.Spacer);
        foreach (ResearchState state in research.States.Values)
            state.SetStatusForEditor(state.Definition.Id == "DeepSpaceFleet" ||
                state.Definition.Id == "InterstellarNavigation"
                    ? ResearchStatus.Queued : ResearchStatus.Completed);
        ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values));
        BuildingManager.Instance.EnsureBuilding(DataBase<Building>.Find("LaunchCenter"))
            .SetAmountForEditor(ExpantaNum.One);
        TutorialManager.Current.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            ActiveStepId = "long-term", CompletedStepIds = new List<string>()
        }, TechLevel.Spacer);
        root.SetPage("Overview");
        root.RefreshUI();
        yield return new WaitForSeconds(0.35f);
        Transform card = root.transform.Find("SafeAreaRoot/Content/PageHost/Overview/PrimaryCard");
        TMP_Text guidance = card.Find("Text").GetComponent<TMP_Text>();
        SectorDefinition target = DataBase<SectorDefinition>.Find("DawnRing");
        Assert.That(guidance.text, Does.Contain(target.Label));
        Button navigation = card.Find("NavigationButton").GetComponent<Button>();
        Assert.That(navigation.interactable, Is.True);
        navigation.onClick.Invoke();
        yield return null;
        TMP_Text detail = root.transform.Find(
            "SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body")
            .GetComponent<TMP_Text>();
        Assert.That(detail.text, Does.Contain(target.Label));
        Assert.That(game.Sectors.GetState(target).Unlocked, Is.False,
            "Opening the recommendation must not unlock or occupy its destination.");
    }

    [UnityTest]
    public IEnumerator WorkshopBenefit_AuthoredDetailsRefreshAndPreviewMatchesCommittedRates()
    {
        yield return LoadIsolatedNewGame();
        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        ResourceManager resources = ResourceManager.Instance;
        BuildingManager buildings = BuildingManager.Instance;
        ResearchManager research = ResearchManager.Instance;
        WorkshopManager workshop = WorkshopManager.Instance;
        WorkshopUpgrade candidate = DataBase<WorkshopUpgrade>.Find("RotaryDrillingHeads");
        Building target = DataBase<Building>.Find("OilDerrick");
        Assert.That(root, Is.Not.Null);
        Assert.That(candidate, Is.Not.Null);
        Assert.That(target, Is.Not.Null);

        // Controlled Industrial fixture inside an isolated new-game scene; this is UI/rate
        // regression coverage, not evidence of reaching Industrial from natural production.
        GameManager.Instance.State.AdvanceTechLevelForEditor(TechLevel.Industrial);
        var completed = new List<Research>(candidate.RequiredResearch)
        {
            DataBase<Research>.Find("IndustrialWorkshop")
        };
        foreach (Research prerequisite in completed)
        {
            var paid = new Dictionary<Resource, ExpantaNum>();
            foreach (Pair<Resource, ExpantaNum> cost in prerequisite.ResourceRequirements)
                paid[cost.First] = cost.Second;
            research.GetState(prerequisite).RestoreForEditor(ExpantaNum.Zero, false, true, paid);
        }
        foreach (WorkshopUpgrade prerequisite in candidate.RequiredUpgrades)
            workshop.States[prerequisite].SetPurchasedForEditor(true);
        ProgressionModifierState previous = ProgressionModifierManager.Current;
        ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values),
            new List<WorkshopUpgradeState>(workshop.States.Values));
        buildings.ApplyProgressionModifierChangeForEditor(previous, ProgressionModifierManager.Current);
        foreach (Pair<Resource, ExpantaNum> cost in candidate.ResourceRequirements)
            resources.SetAmount(cost.First, cost.Second * new ExpantaNum(3));
        BuildingState building = buildings.EnsureBuilding(target);
        buildings.SetAmountAndRatesForEditor(building, ExpantaNum.One);

        var inventoryVersions = new Dictionary<Resource, int>();
        foreach (KeyValuePair<Resource, ResourceState> entry in resources.States)
            inventoryVersions.Add(entry.Key, entry.Value.Version);
        int buildingVersion = building.Version;
        int workshopVersion = workshop.States[candidate].Version;
        ProgressionModifierState current = ProgressionModifierManager.Current;
        IReadOnlyList<WorkshopBenefitRatePreview> preview = workshop.GetPurchaseBenefitPreview(candidate);
        Assert.That(preview, Is.Not.Empty);
        foreach (KeyValuePair<Resource, int> version in inventoryVersions)
            Assert.That(resources.GetState(version.Key).Version, Is.EqualTo(version.Value),
                "A read-only preview must not mutate resource state versions.");
        Assert.That(building.Version, Is.EqualTo(buildingVersion));
        Assert.That(workshop.States[candidate].Version, Is.EqualTo(workshopVersion));
        Assert.That(ProgressionModifierManager.Current, Is.SameAs(current));

        root.SetPage("Workshop");
        yield return null;
        Button card = root.GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
            button.transform.Find("Label")?.GetComponent<TMP_Text>()?.text == candidate.Label);
        Assert.That(card, Is.Not.Null, "The real authored workshop card must be present.");
        card.onClick.Invoke();
        yield return null;
        TMP_Text body = root.transform.Find(
            "SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body")?.GetComponent<TMP_Text>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.text, Does.Contain(candidate.Label));
        foreach (WorkshopBenefitRatePreview rate in preview)
            Assert.That(body.text, Does.Contain(rate.Before.ToGameString() + " → " + rate.After.ToGameString() + "/s"));
        string oneBuildingText = body.text;
        buildings.SetAmountAndRatesForEditor(building, new ExpantaNum(2));
        root.RefreshUI();
        yield return new WaitForSeconds(0.35f);
        Assert.That(body.text, Is.Not.EqualTo(oneBuildingText),
            "Open details must refresh when owned quantities change without a workshop purchase.");
        preview = workshop.GetPurchaseBenefitPreview(candidate);
        foreach (WorkshopBenefitRatePreview rate in preview)
            Assert.That(body.text, Does.Contain(rate.Before.ToGameString() + " → " + rate.After.ToGameString() + "/s"));

        var beforeRates = new Dictionary<Resource, Pair<ExpantaNum, ExpantaNum>>();
        foreach (KeyValuePair<Resource, ResourceState> entry in resources.States)
            beforeRates[entry.Key] = new Pair<ExpantaNum, ExpantaNum>(entry.Value.ProductionRate, entry.Value.ConsumptionRate);
        var beforeCosts = new Dictionary<Resource, ExpantaNum>();
        foreach (Pair<Resource, ExpantaNum> cost in candidate.ResourceRequirements)
            beforeCosts[cost.First] = resources.GetAmount(cost.First);
        Button purchase = root.GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
            button.name == "Action" && button.gameObject.activeInHierarchy);
        Assert.That(purchase, Is.Not.Null);
        Assert.That(purchase.interactable, Is.True);
        ExpantaNum reward = GameManager.Instance.State.HappinessRewardMultiplier;
        purchase.onClick.Invoke();
        Assert.That(workshop.IsPurchased(candidate), Is.True);
        foreach (Pair<Resource, ExpantaNum> cost in candidate.ResourceRequirements)
            Assert.That((beforeCosts[cost.First] - resources.GetAmount(cost.First) - cost.Second).Abs(),
                Is.LessThan(new ExpantaNum("1e-6")), "The real purchase must charge its authored cost.");
        foreach (WorkshopBenefitRatePreview rate in preview)
        {
            ResourceState state = resources.GetState(rate.Resource);
            ExpantaNum committed = rate.Kind == WorkshopBenefitRateKind.ResourceProduction
                ? (state.ProductionRate - beforeRates[rate.Resource].First) * reward
                : state.ConsumptionRate - beforeRates[rate.Resource].Second;
            // The ledger and preview use different operation orders; ExpantaNum quantizes
            // scalar arithmetic to 1e-6, so allow one quantization unit inclusively.
            Assert.That((committed - rate.Change).Abs(), Is.LessThanOrEqualTo(new ExpantaNum("1e-6")),
                "Settled production includes happiness; ongoing input consumption does not. " +
                "Committed=" + committed.ToString() + "; preview=" + rate.Change.ToString());
        }
        Assert.That(workshop.GetPurchaseBenefitPreview(candidate), Is.Empty);
    }

    [UnityTest]
    public IEnumerator AudioFeedback_RealUiAndResearchPathsEmitOnlyCommittedResults()
    {
        yield return LoadIsolatedNewGame();
        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        GameManager gameManager = GameManager.Instance;
        ResourceManager resourceManager = ResourceManager.Instance;
        BuildingManager buildingManager = BuildingManager.Instance;
        ResearchManager researchManager = ResearchManager.Instance;
        SimulationManager simulationManager = SimulationManager.Instance;
        Building house = DataBase<Building>.Find("WoodHouse");
        Assert.That(root, Is.Not.Null);
        Assert.That(house, Is.Not.Null);

        var requests = new List<UIButtonSoundManager.Sound>();
        Action<UIButtonSoundManager.Sound> observer = requests.Add;
        UIButtonSoundManager.PlayRequested += observer;
        try
        {
            int simulatedSeconds = 0;
            simulationManager.SetRunning(false);
            while (buildingManager.GetMaxBuildable(house, ExpantaNum.One) < ExpantaNum.One &&
                   simulatedSeconds++ < 600)
                simulationManager.ManualTick(1d);
            Assert.That(buildingManager.GetMaxBuildable(house, ExpantaNum.One),
                Is.GreaterThanOrEqualTo(ExpantaNum.One));

            root.SetPage("Buildings");
            yield return null;
            Button houseRow = root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button =>
                {
                    TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
                    return label != null && label.text == house.Label;
                });
            Assert.That(houseRow, Is.Not.Null);
            Transform row = houseRow.transform;
            Button buildButton = row.Find("BuildButton")?.GetComponent<Button>();
            Button deconstructButton = row.Find("DeconstructButton")?.GetComponent<Button>();
            Assert.That(buildButton, Is.Not.Null);
            Assert.That(deconstructButton, Is.Not.Null);

            requests.Clear();
            buildButton.onClick.Invoke();
            yield return null;
            Assert.That(requests, Is.EqualTo(new[] { UIButtonSoundManager.Sound.Build }));
            Assert.That(buildingManager.GetState(house).Amount,
                Is.GreaterThanOrEqualTo(ExpantaNum.One));

            foreach (Pair<Resource, ExpantaNum> requirement in house.ResourceRequirements)
                if (requirement.First != null)
                    resourceManager.SetAmount(requirement.First, ExpantaNum.Zero);
            root.RefreshUI();
            yield return null;
            row = root.GetComponentsInChildren<Button>(true)
                .First(button =>
                {
                    TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
                    return label != null && label.text == house.Label;
                }).transform;
            buildButton = row.Find("BuildButton")?.GetComponent<Button>();
            requests.Clear();
            buildButton.onClick.Invoke();
            yield return null;
            Assert.That(requests, Is.Empty,
                "A failed UI transaction must not emit a successful result sound.");

            deconstructButton = row.Find("DeconstructButton")?.GetComponent<Button>();
            requests.Clear();
            deconstructButton.onClick.Invoke();
            yield return null;
            Assert.That(requests, Is.EqualTo(new[] { UIButtonSoundManager.Sound.Deconstruct }));

            GrantResearchTestResources(resourceManager);
            Research first = FindAvailableResearch(researchManager);
            requests.Clear();
            CompleteResearchThroughRuntime(gameManager, researchManager, resourceManager, first);
            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0], Is.EqualTo(
                first.AdvancesTechLevel
                    ? UIButtonSoundManager.Sound.EraBreakthrough
                    : UIButtonSoundManager.Sound.ResearchComplete));

            GrantResearchTestResources(resourceManager);
            Research offline = FindAvailableResearch(researchManager);
            requests.Clear();
            Assert.That(researchManager.HandleResearchAction(offline),
                Is.Not.EqualTo(ResearchActionResult.Invalid));
            researchManager.TickOfflineForEditor(1000000d);
            Assert.That(requests, Is.Empty,
                "Offline completion must not replay research result sounds.");
        }
        finally
        {
            UIButtonSoundManager.PlayRequested -= observer;
        }
    }

    [UnityTest]
    public IEnumerator EraPage_RendersCurrentNextEraProgressAndGoal()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null, "SampleScene must contain the runtime KingdomUIRoot.");

        root.SetPage("Era");
        yield return null;
        yield return null;

        Transform rows = root.transform.Find("SafeAreaRoot/Content/PageHost/Era/DataRows");
        Assert.That(rows, Is.Not.Null, "Era/DataRows must exist under the isolated runtime UI.");
        Assert.That(rows.childCount, Is.GreaterThan(0), "Era page must not remain an empty DataRows container.");

        TMP_Text[] texts = rows.GetComponentsInChildren<TMP_Text>(true);
        Assert.That(texts.Any(text => text.text.Contains("时代档案")), Is.True,
            "Era page must display an authored-style era archive header.");
        Assert.That(texts.Any(text => text.text.Contains("下一时代跃迁")), Is.True,
            "Era page must display the dedicated next-era transition section.");
        Assert.That(rows.Find("EraCurrentArchive"), Is.Not.Null,
            "Era page must group current-era identity, definition and capabilities.");
        Assert.That(rows.Find("EraTransitionSection"), Is.Not.Null,
            "Era page must group next-era transition information.");
        Assert.That(rows.Find("EraRequirementSection"), Is.Not.Null,
            "Era page must group research and resource requirements together.");
        Assert.That(texts.Any(text => text.text == "本时代能力"), Is.True,
            "Era page must summarize capabilities already earned in the current era.");
        Assert.That(texts.Any(text => text.text == "进入后变化"), Is.True,
            "Era page must summarize representative changes in the next era.");
        Assert.That(texts.Any(text => text.text.Contains("研究前置")), Is.True,
            "Era page must separate research prerequisites.");
        Assert.That(texts.Any(text => text.text.Contains("资源储备")), Is.True,
            "Era page must separate resource reserves.");
        Assert.That(texts.Any(text => text.text == "当前首要阻碍"), Is.True,
            "Era page must display the first unmet era condition as the actionable blocker.");
        Assert.That(texts.Any(text => text.text.Contains("阻碍类别：")), Is.True,
            "Era page must classify the current blocker.");
        Assert.That(texts.Any(text => text.text.Contains("下一时代") || text.text.Contains("时代状态")), Is.True,
            "Era page must identify the next era or the terminal state.");
        Assert.That(texts.Any(text => text.text.Contains("发展准备度") || text.text.Contains("基础产业准备")), Is.False,
            "Era page must not duplicate Overview soft readiness guidance.");

        Button[] eraActions = rows.GetComponentsInChildren<Button>(true);
        Assert.That(eraActions.Any(button => button.name == "DetailAction" && button.interactable), Is.True,
            "Era cards must expose at least one usable detail action.");
        Assert.That(eraActions.Any(button => button.name == "QueueAction" && button.interactable), Is.True,
            "An incomplete transition research must expose a queue action.");
        foreach (Button action in eraActions.Where(button => (button.name == "DetailAction" || button.name == "QueueAction") && button.gameObject.activeInHierarchy))
        {
            RectTransform actionRect = action.transform as RectTransform;
            Assert.That(actionRect, Is.Not.Null);
            Assert.That(actionRect.rect.width, Is.GreaterThan(0f));
            Assert.That(actionRect.rect.height, Is.GreaterThan(0f));
        }

        TMP_Text eraGoalTitle = texts.FirstOrDefault(text => text.text == "下一时代跃迁");
        Assert.That(eraGoalTitle, Is.Not.Null,
            "Era transition section must expose a visible title.");
        Button eraGoalButton = rows.Find(
            "EraTransitionSection/Content/EraTransitionTarget/DetailAction")?.GetComponent<Button>();
        Assert.That(eraGoalButton, Is.Not.Null);
        Assert.That(eraGoalButton.interactable, Is.True,
            "Era goal row must navigate through its detail action.");
        eraGoalButton.onClick.Invoke();
        yield return null;
        Button detailAction = root.DetailActionButtonForEditor;
        Assert.That(detailAction, Is.Not.Null);
        Assert.That(detailAction.gameObject.activeSelf, Is.True,
            "Era research navigation must expose the research detail action button.");

        // The transition detail action intentionally navigates to Research. Re-enter
        // Era before inspecting its resource requirement; otherwise this stale
        // transform reference points at an inactive page.
        root.SetPage("Era");
        yield return null;
        rows = root.transform.Find("SafeAreaRoot/Content/PageHost/Era/DataRows") as RectTransform;
        Assert.That(rows, Is.Not.Null);

        Transform resourceButtonTransform = rows.GetComponentsInChildren<Button>(true)
            .Select(button => button.transform.parent)
            .FirstOrDefault(parent => parent != null && parent.name.StartsWith("EraResource_", StringComparison.Ordinal));
        if (resourceButtonTransform != null)
        {
            Button resourceButton = resourceButtonTransform.Find("DetailAction")?.GetComponent<Button>();
            Assert.That(resourceButton, Is.Not.Null, "Resource requirement rows must expose a detail action.");
            resourceButton.onClick.Invoke();
            yield return null;
            Assert.That(rows.gameObject.activeInHierarchy, Is.True,
                "Resource details must update the side panel without leaving the Era page.");
        }

        Transform requirementContent = rows.Find("EraRequirementSection/Content");
        Assert.That(requirementContent, Is.Not.Null);
        Image[] researchRows = requirementContent.GetComponentsInChildren<Image>(true)
            .Where(image => image.name.StartsWith("EraResearch_", StringComparison.Ordinal)).ToArray();
        Image[] resourceRows = requirementContent.GetComponentsInChildren<Image>(true)
            .Where(image => image.name.StartsWith("EraResource_", StringComparison.Ordinal)).ToArray();
        for (int i = 1; i < researchRows.Length; i++)
            Assert.That(researchRows[i].color, Is.Not.EqualTo(researchRows[i - 1].color),
                "Research requirement rows must alternate Panel and PanelRaised backgrounds.");
        for (int i = 1; i < resourceRows.Length; i++)
            Assert.That(resourceRows[i].color, Is.Not.EqualTo(resourceRows[i - 1].color),
                "Resource requirement rows must alternate Panel and PanelRaised backgrounds.");
    }

    [UnityTest]
    public IEnumerator ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null, "SampleScene must contain the runtime KingdomUIRoot.");

        root.SetPage("Research");
        Transform viewport = null;
        Transform content = null;
        int expectedResearchNodes = DataBase<Research>.All.Count;
        Assert.That(expectedResearchNodes, Is.GreaterThanOrEqualTo(79),
            "The research graph must retain the minimum vertical-slice node coverage.");
        bool layoutReady = false;
        for (int frame = 0; frame < 480; frame++)
        {
            yield return null;
            viewport = root.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
            content = viewport == null ? null : viewport.Find("ResearchGraphContent");
            int builtResearchNodes = 0;
            if (content != null)
                foreach (Transform child in content)
                    if (child.name.StartsWith("ResearchNode_", StringComparison.Ordinal))
                        builtResearchNodes++;
            UIResearchGraphGesture gesture = viewport == null
                ? null
                : viewport.GetComponent<UIResearchGraphGesture>();
            RectTransform viewportRect = viewport as RectTransform;
            RectTransform contentRect = content as RectTransform;
            layoutReady = gesture != null && gesture.IsInitialized &&
                viewportRect != null && contentRect != null &&
                viewportRect.rect.width > 1f && viewportRect.rect.height > 1f &&
                contentRect.rect.width > 1f && contentRect.rect.height > 1f;
            if (builtResearchNodes >= expectedResearchNodes && layoutReady)
                break;
        }

        Assert.That(viewport, Is.Not.Null, "ResearchGraphViewport must be authored under the isolated SafeAreaRoot.");
        Assert.That(layoutReady, Is.True,
            "ResearchGraphViewport and ResearchGraphContent must be initialized with measurable bounds before the audit continues.");
        Assert.That(viewport.GetComponent<Canvas>(), Is.Not.Null,
            "ResearchGraphViewport must isolate graph redraws in a nested Canvas.");
        Assert.That(viewport.GetComponent<GraphicRaycaster>(), Is.Not.Null,
            "The isolated research Canvas must retain touch raycasting.");
        content = viewport.Find("ResearchGraphContent");
        Assert.That(content, Is.Not.Null);
        Transform lineLayer = content.Find("ResearchGraphLineLayer");
        Assert.That(lineLayer, Is.Not.Null,
            "ResearchGraphLineLayer must be authored in the scene shell.");
        UIResearchConnectorBatch[] connectorBatches =
            lineLayer.GetComponentsInChildren<UIResearchConnectorBatch>(true);
        Assert.That(connectorBatches.Length, Is.GreaterThan(0));
        Assert.That(connectorBatches.Length, Is.LessThanOrEqualTo(8),
            "Four authored connector textures need at most normal/focused batch renderers.");
        int logicalConnectorParts = connectorBatches
            .Where(batch => !batch.DrawFocused)
            .Sum(batch => batch.PartCount);
        Assert.That(logicalConnectorParts, Is.GreaterThan(0));
        int renderedConnectorVertices = 0;
        for (int i = 0; i < connectorBatches.Length; i++)
        {
            Mesh connectorMesh = connectorBatches[i].canvasRenderer.GetMesh();
            if (connectorMesh != null)
                renderedConnectorVertices += connectorMesh.vertexCount;
        }
        Assert.That(renderedConnectorVertices, Is.EqualTo(logicalConnectorParts * 4),
            "Every logical connector part must render exactly one textured quad.");
        Assert.That(
            lineLayer.GetComponentsInChildren<Transform>(true).Length - 1,
            Is.LessThanOrEqualTo(10),
            "Connector batching must not recreate one Transform per logical segment.");
        Assert.That(
            lineLayer.GetComponentsInChildren<Image>(true)
                .Count(image => image.name.StartsWith(
                    "ResearchLinePart_", StringComparison.Ordinal)),
            Is.EqualTo(0),
            "Legacy per-segment Images must not return.");
        Debug.Log(
            $"[KingdomUI] Research connector batch audit: logicalParts={logicalConnectorParts}, " +
            $"renderedVertices={renderedConnectorVertices}, " +
            $"batchGraphics={connectorBatches.Length}, connectorObjects=" +
            $"{lineLayer.GetComponentsInChildren<Transform>(true).Length - 1}");
        Assert.That(content.Find("ResearchGraphDragSurface"), Is.Not.Null,
            "ResearchGraphDragSurface must be authored in the scene shell.");
        Transform pageTool = root.transform.Find("SafeAreaRoot/Content/PageTool");
        Assert.That(pageTool, Is.Not.Null, "PageTool must be authored under Content.");
        Transform toolbar = pageTool.Find("OverviewNavigationToolbar");
        Assert.That(toolbar, Is.Not.Null, "OverviewNavigationToolbar must be authored beside ResearchQueueViewport.");
        Button targetNavigationButton = toolbar.Find("OverviewCurrentTargetButton")?.GetComponent<Button>();
        Assert.That(targetNavigationButton, Is.Not.Null,
            "Research Tree must expose a target navigation button.");
        TMP_Text targetNavigationLabel = targetNavigationButton.GetComponentInChildren<TMP_Text>(true);
        Assert.That(targetNavigationLabel, Is.Not.Null);
        Assert.That(targetNavigationLabel.gameObject.activeSelf, Is.True,
            "The authored target navigation label must remain visible after binding.");
        Button eraNavigationButton = toolbar.Find("OverviewCurrentEraButton")?.GetComponent<Button>();
        Assert.That(eraNavigationButton, Is.Not.Null,
            "Research Tree must expose a current-era navigation button.");
        TMP_Text eraNavigationLabel = eraNavigationButton.GetComponentInChildren<TMP_Text>(true);
        Assert.That(eraNavigationLabel, Is.Not.Null);
        Assert.That(eraNavigationLabel.gameObject.activeSelf, Is.True,
            "The authored current-era navigation label must remain visible after binding.");
        Transform search = toolbar.Find("Search");
        if (search != null)
            Assert.That(search.gameObject.activeSelf, Is.False,
                "The retired research search control must remain an inactive placeholder.");
        Transform queueViewport = pageTool.Find("ResearchQueueViewport");
        Assert.That(queueViewport, Is.Not.Null,
            "PageTool/ResearchQueueViewport must display the graphic research queue.");
        Transform quantityControls = pageTool.Find("BuildingControls");
        Assert.That(quantityControls, Is.Not.Null);
        Assert.That(queueViewport.parent, Is.SameAs(pageTool),
            "The research queue must be a child of PageTool.");
        Assert.That(toolbar.parent, Is.SameAs(queueViewport.parent),
            "OverviewNavigationToolbar and ResearchQueueViewport must be siblings under PageTool.");
        ScrollRect queueScroll = queueViewport.GetComponent<ScrollRect>();
        Assert.That(queueScroll, Is.Not.Null,
            "The graphic research queue must use a ScrollRect for its drag surface.");
        Assert.That(queueScroll.horizontal, Is.True);
        Assert.That(queueScroll.vertical, Is.False);
        Assert.That(queueScroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
        RectTransform queueContent = queueViewport.Find("ResearchQueueContent") as RectTransform;
        Assert.That(queueContent, Is.Not.Null,
            "The graphic research queue content is missing.");
        Assert.That(queueScroll.content, Is.SameAs(queueContent));
        Assert.That(queueViewport.GetComponent<GraphicRaycaster>(), Is.Not.Null,
            "The queue viewport must retain an active raycaster for node Buttons.");
        Assert.That(queueContent.anchoredPosition.y, Is.EqualTo(0f).Within(.01f));
        Transform pageHost = root.transform.Find("SafeAreaRoot/Content/PageHost");
        Assert.That(pageHost, Is.Not.Null);
        Assert.That(pageHost.GetComponent<Canvas>(), Is.Not.Null,
            "The page viewport must isolate scroll redraws from fixed UI.");
        ScrollRect outerPageScroll = pageHost.GetComponent<ScrollRect>();
        Assert.That(outerPageScroll, Is.Not.Null);
        Assert.That(outerPageScroll.enabled, Is.False,
            "The legacy outer page ScrollRect must not compete with the research graph gesture.");

        RectTransform finalViewportRect = viewport as RectTransform;
        RectTransform finalContentRect = content as RectTransform;
        Assert.That(finalViewportRect, Is.Not.Null);
        Assert.That(finalContentRect, Is.Not.Null);

        var cells = new HashSet<Vector2Int>();
        int nodeCount = 0;
        foreach (Transform child in content)
        {
            if (!child.name.StartsWith("ResearchNode_", System.StringComparison.Ordinal))
                continue;
            RectTransform node = child as RectTransform;
            Assert.That(node, Is.Not.Null);
            Vector2 topLeft = new Vector2(
                node.anchoredPosition.x,
                 finalContentRect.rect.height - node.anchoredPosition.y - node.rect.height);
            Assert.That(cells.Add(new Vector2Int(
                Mathf.RoundToInt(topLeft.x), Mathf.RoundToInt(topLeft.y))), Is.True,
                "Research nodes must not occupy the same integer grid cell.");
            nodeCount++;
        }

        UIResearchGraphGesture finalGesture = viewport.GetComponent<UIResearchGraphGesture>();
        Assert.That(finalGesture, Is.Not.Null);
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        Assert.That(scroll, Is.Not.Null);
        Canvas.ForceUpdateCanvases();
        finalGesture.RefreshLayoutBounds(false);
        Canvas.ForceUpdateCanvases();
        finalGesture.RefreshLayoutBounds(false);
        bool verticalOverflow = finalContentRect.rect.height * Mathf.Abs(finalContentRect.localScale.y) >
            finalViewportRect.rect.height + 0.5f;
        bool horizontalOverflow = finalContentRect.rect.width * Mathf.Abs(finalContentRect.localScale.x) >
            finalViewportRect.rect.width + 0.5f;
        bool canPanVertical = finalGesture.CanPanVertical;
        bool canPanHorizontal = finalGesture.CanPanHorizontal;
        Assert.That(canPanVertical, Is.EqualTo(verticalOverflow));
        Assert.That(canPanHorizontal, Is.EqualTo(horizontalOverflow));
        Transform safeAreaRoot = root.transform.Find("SafeAreaRoot");
        Assert.That(safeAreaRoot, Is.Not.Null);
        bool legacyRootChildrenInactive = true;
        foreach (Transform rootChild in root.transform)
        {
            if (rootChild == safeAreaRoot || rootChild.name == "UnsafeAreaTicker")
                continue;
            legacyRootChildrenInactive &= !rootChild.gameObject.activeSelf;
            Assert.That(rootChild.gameObject.activeSelf, Is.False,
                "Every legacy KingdomUIRoot child outside SafeAreaRoot must remain inactive: " +
                rootChild.name);
        }

        EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("PlayMode-ResearchGraph-EventSystem");
            createdObjects.Add(eventSystemObject);
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }
        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = new Vector2(500f, 600f)
        };
        Vector2 beforeDrag = finalContentRect.anchoredPosition;
        finalGesture.OnPointerDown(pointer);
        finalGesture.OnInitializePotentialDrag(pointer);
        pointer.position = new Vector2(500f, 550f);
        finalGesture.OnBeginDrag(pointer);
        pointer.position = new Vector2(500f, 750f);
        finalGesture.OnDrag(pointer);
        Vector2 afterDrag = finalContentRect.anchoredPosition;
        finalGesture.OnEndDrag(pointer);
        finalGesture.OnPointerUp(pointer);
        if (verticalOverflow)
            Assert.That(afterDrag.y, Is.GreaterThan(beforeDrag.y),
                "A vertical drag in an overflowing research graph must move the graph content within its vertical range.");
        Debug.Log($"[KingdomUI] Research pointer drag audit: before={beforeDrag}, after={afterDrag}, delta={afterDrag - beforeDrag}, verticalDragMoved={afterDrag.y > beforeDrag.y}");

        Transform firstNode = null;
        foreach (Transform child in content)
            if (child.name.StartsWith("ResearchNode_", System.StringComparison.Ordinal))
            {
                firstNode = child;
                break;
            }
        Assert.That(firstNode, Is.Not.Null);
        UIResearchGraphDragForwarder forwarder = firstNode.GetComponent<UIResearchGraphDragForwarder>();
        Assert.That(forwarder, Is.Not.Null);
        finalContentRect.anchoredPosition = beforeDrag;
        PointerEventData nodePointer = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = new Vector2(500f, 600f)
        };
        forwarder.OnPointerDown(nodePointer);
        forwarder.OnInitializePotentialDrag(nodePointer);
        nodePointer.position = new Vector2(500f, 550f);
        forwarder.OnBeginDrag(nodePointer);
        nodePointer.position = new Vector2(500f, 750f);
        forwarder.OnDrag(nodePointer);
        Vector2 afterNodeDrag = finalContentRect.anchoredPosition;
        forwarder.OnEndDrag(nodePointer);
        forwarder.OnPointerUp(nodePointer);
        if (verticalOverflow)
            Assert.That(afterNodeDrag.y, Is.GreaterThan(beforeDrag.y),
                "A drag beginning on a research Button must be forwarded to the graph gesture.");
        Debug.Log($"[KingdomUI] Research node-forwarded drag audit: before={beforeDrag}, after={afterNodeDrag}, delta={afterNodeDrag - beforeDrag}, forwardedVerticalDragMoved={afterNodeDrag.y > beforeDrag.y}");
        Debug.Log($"[KingdomUI] Research runtime playmode audit: expectedNodes={expectedResearchNodes}, nodes={nodeCount}, uniqueCells={cells.Count}, viewport={finalViewportRect.rect.size}, content={finalContentRect.rect.size}, horizontalOverflow={horizontalOverflow}, verticalOverflow={verticalOverflow}, canPanHorizontal={canPanHorizontal}, canPanVertical={canPanVertical}, outerPageScrollEnabled={outerPageScroll.enabled}, legacyRootChildrenInactive={legacyRootChildrenInactive}");
        Assert.That(expectedResearchNodes, Is.GreaterThan(0));
        Assert.That(nodeCount, Is.EqualTo(expectedResearchNodes));
        Assert.That(cells.Count, Is.EqualTo(expectedResearchNodes),
            "Every current research definition must occupy one unique integer cell.");

        Transform researchPage = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Research");
        Assert.That(researchPage, Is.Not.Null);
        CanvasGroup visibility = researchPage.GetComponent<CanvasGroup>();
        Assert.That(visibility, Is.Not.Null,
            "The cached research hierarchy must use one root CanvasGroup for page visibility.");
        root.SetPage("Overview");
        yield return null;
        Assert.That(researchPage.gameObject.activeSelf, Is.True,
            "Leaving Research must not disable thousands of cached graph objects.");
        Assert.That(visibility.alpha, Is.EqualTo(0f).Within(0.001f));
        Assert.That(visibility.interactable, Is.False);
        Assert.That(visibility.blocksRaycasts, Is.False);
        Assert.That(finalGesture.enabled, Is.False,
            "A hidden persistent graph must not continue processing gestures.");

        root.SetPage("Research");
        yield return null;
        Assert.That(researchPage.gameObject.activeSelf, Is.True);
        Assert.That(visibility.alpha, Is.EqualTo(1f).Within(0.001f));
        Assert.That(visibility.interactable, Is.True);
        Assert.That(visibility.blocksRaycasts, Is.True);
        Assert.That(finalGesture.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator DetailPanel_UsesSingleScrollOwnerAndCenteredResearchLabels()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null, "KingdomUIRoot was not created after loading SampleScene.");
        Transform detailPanel = root.transform.Find("SafeAreaRoot/DetailPanel");
        Assert.That(detailPanel, Is.Not.Null, "SafeAreaRoot/DetailPanel is missing under KingdomUIRoot.");

        Transform detailUI = detailPanel.Find("DetailUI");
        Assert.That(detailUI, Is.Not.Null, "DetailUI runtime root is missing under DetailPanel.");
        Transform detailViewport = detailUI.Find("DetailScrollViewport");
        Transform detailContent = detailViewport == null ? null : detailViewport.Find("DetailScrollContent");
        Assert.That(detailViewport, Is.Not.Null, "DetailPanel must expose one runtime scroll viewport.");
        Assert.That(detailContent, Is.Not.Null, "DetailScrollContent is missing under DetailScrollViewport.");
        Assert.That(detailViewport.GetComponent<Canvas>(), Is.Not.Null,
            "Detail scrolling must use an isolated nested Canvas.");
        Assert.That(detailViewport.GetComponent<GraphicRaycaster>(), Is.Not.Null,
            "The isolated detail Canvas must retain touch raycasting.");
        Assert.That((detailViewport as RectTransform).rect.height, Is.GreaterThan(0f),
            "DetailScrollViewport must have a positive runtime height.");
        Assert.That((detailContent as RectTransform).rect.height, Is.GreaterThan(0f),
            "DetailScrollContent must be repaired after the initial layout pass.");
        ScrollRect detailScroll = detailViewport.GetComponent<ScrollRect>();
        Assert.That(detailScroll, Is.Not.Null);
        Assert.That(detailScroll.enabled, Is.False,
            "The native ScrollRect is a geometry owner only; DetailUIInteraction owns dragging.");
        Assert.That(detailScroll.content, Is.SameAs(detailContent));
        Transform header = detailUI.Find("Header");
        Assert.That(header, Is.Not.Null);
        Assert.That(header.GetSiblingIndex(), Is.LessThan(detailViewport.GetSiblingIndex()));

        Transform requirements = detailContent.Find("BuildingRequirements");
        Transform flows = detailContent.Find("BuildingOutput");
        Assert.That(requirements, Is.Not.Null, "BuildingRequirements is missing under DetailScrollContent.");
        Assert.That(flows, Is.Not.Null, "BuildingOutput is missing under DetailScrollContent.");
        Assert.That(requirements.parent, Is.SameAs(detailContent));
        Assert.That(flows.parent, Is.SameAs(detailContent));
        Assert.That(requirements.GetComponent<ScrollRect>(), Is.Null,
            "Requirements must not contain a nested ScrollRect.");
        Assert.That(flows.GetComponent<ScrollRect>(), Is.Null,
            "Flows must not contain a nested ScrollRect.");
        Assert.That(requirements.GetComponent<RectMask2D>(), Is.Null,
            "Requirements must not contain a nested mask.");
        Assert.That(flows.GetComponent<RectMask2D>(), Is.Null,
            "Flows must not contain a nested mask.");

        Transform bodyTransform = detailContent.Find("Body");
        Assert.That(bodyTransform, Is.Not.Null, "Detail body is missing under DetailScrollContent.");
        root.ShowDetailsForEditor("Runtime detail", "Visible body", "detail-test");
        yield return null;
        TMP_Text bodyText = bodyTransform.GetComponent<TMP_Text>();
        Assert.That(bodyText.text, Does.Contain("Runtime detail"));
        Assert.That(bodyText.rectTransform.rect.height, Is.GreaterThan(0f),
            "Detail body must retain a visible rect after being reparented.");

        root.SetPage("Research");
        Transform researchViewport = null;
        Transform researchContent = null;
        Transform firstNode = null;
        for (int frame = 0; frame < 480 && firstNode == null; frame++)
        {
            yield return null;
            researchViewport = root.transform.Find(
                "SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
            researchContent = researchViewport == null
                ? null
                : researchViewport.Find("ResearchGraphContent");
            if (researchContent == null)
                continue;
            foreach (Transform child in researchContent)
                if (child.name.StartsWith("ResearchNode_", StringComparison.Ordinal))
                {
                    firstNode = child;
                    break;
                }
        }
        Assert.That(firstNode, Is.Not.Null);
        foreach (string labelName in new[] { "Label", "Cost", "Progress", "State" })
        {
            TMP_Text label = firstNode.Find(labelName)?.GetComponent<TMP_Text>();
            Assert.That(label, Is.Not.Null, "Research node label is missing: " + labelName);
            Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Assert.That(label.rectTransform.pivot.x, Is.EqualTo(.5f).Within(.001f));
            Assert.That(label.rectTransform.anchorMin.x, Is.LessThan(label.rectTransform.anchorMax.x));
            Assert.That(label.rectTransform.anchorMin.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(label.rectTransform.anchorMax.x, Is.LessThanOrEqualTo(1f));
        }
    }

    [UnityTest]
    public IEnumerator ResourceDetailAndList_ShowOnlyRealizedHappinessAdjustedFlows()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        GameManager gameManager = Object.FindObjectOfType<GameManager>();
        ResourceManager resourceManager = Object.FindObjectOfType<ResourceManager>();
        BuildingManager buildingManager = Object.FindObjectOfType<BuildingManager>();
        SimulationManager simulationManager = Object.FindObjectOfType<SimulationManager>();
        Assert.That(root, Is.Not.Null);
        Assert.That(gameManager, Is.Not.Null);
        Assert.That(resourceManager, Is.Not.Null);
        Assert.That(buildingManager, Is.Not.Null);
        Assert.That(simulationManager, Is.Not.Null);
        simulationManager.SetRunning(false);
        gameManager.InitializeNewGameForEditor();
        Assert.That(gameManager.State.HappinessRewardMultiplier, Is.GreaterThan(ExpantaNum.One));

        Resource resource = ScriptableObject.CreateInstance<Resource>();
        resource.SetIdForEditor("PlayModeDetailFlowResource");
        resource.Label = "Test flow resource";
        resource.Description = "Resource detail flow regression";
        createdObjects.Add(resource);

        Building realized = CreateFlowBuilding(
            "PlayModeRealizedProducer", "Realized producer", resource, new ExpantaNum(2d));
        Building unbuilt = CreateFlowBuilding(
            "PlayModeUnbuiltProducer", "Unbuilt producer", resource, new ExpantaNum(9d));
        Building zeroEfficiency = CreateFlowBuilding(
            "PlayModeZeroEfficiencyProducer", "Zero efficiency producer", resource, new ExpantaNum(7d));

        BuildingState realizedState = buildingManager.EnsureBuilding(realized);
        realizedState.SetAmountForEditor(new ExpantaNum(2d));
        realizedState.SetEfficiencyForEditor(new ExpantaNum(0.5d));
        BuildingState unbuiltState = buildingManager.EnsureBuilding(unbuilt);
        unbuiltState.SetAmountForEditor(ExpantaNum.Zero);
        BuildingState zeroEfficiencyState = buildingManager.EnsureBuilding(zeroEfficiency);
        zeroEfficiencyState.SetAmountForEditor(ExpantaNum.One);
        zeroEfficiencyState.SetEfficiencyForEditor(ExpantaNum.Zero);

        resourceManager.SetAmount(resource, ExpantaNum.Zero);
        resourceManager.SetProductionRate(resource, new ExpantaNum(2d));
        resourceManager.SetConsumptionRate(resource, ExpantaNum.Zero);
        ExpantaNum expectedRate = new ExpantaNum(2d) * gameManager.State.HappinessRewardMultiplier;

        root.ShowResourceDetailsForEditor(resource);
        yield return null;

        Transform bodyTransform = root.transform.Find(
            "SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body");
        TMP_Text body = bodyTransform == null ? null : bodyTransform.GetComponent<TMP_Text>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.text, Does.Contain(realized.Label));
        Assert.That(body.text, Does.Not.Contain(unbuilt.Label));
        Assert.That(body.text, Does.Not.Contain(zeroEfficiency.Label));
        Assert.That(body.text, Does.Contain(expectedRate.ToGameString() + "/s"));
        ExpantaNum expectedBuildingRate = new ExpantaNum(2d) *
            realizedState.Amount * realizedState.Efficiency *
            ProgressionModifierManager.Current.GetBuildingProductionMultiplier(realized) *
            ProgressionModifierManager.Current.GlobalBuildingProductionMultiplier *
            ProgressionModifierManager.Current.GetResourceProductionMultiplier(resource) *
            gameManager.State.HappinessRewardMultiplier;
        string realizedLine = body.text.Split('\n')
            .First(line => line.Contains(realized.Label));
        Assert.That(realizedLine, Does.Contain(expectedBuildingRate.ToGameString() + "/s"));

        resourceManager.Tick(1d);
        Assert.That(resourceManager.GetAmount(resource), Is.GreaterThan(ExpantaNum.Zero));

        root.SetPage("Resources");
        yield return null;
        yield return null;

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetProductionRate(wood, ExpantaNum.One);
        resourceManager.SetConsumptionRate(wood, ExpantaNum.Zero);
        root.RefreshLiveCardValuesForEditor();
        var changeLabels = root.ResourceChangeLabelsForEditor;
        Assert.That(changeLabels.TryGetValue(wood, out TMP_Text changeLabel), Is.True);
        ExpantaNum expectedWoodRate = gameManager.State.HappinessRewardMultiplier;
        Assert.That(changeLabel.text, Is.EqualTo("+" + expectedWoodRate.ToGameString() + "/s"));
    }

    [UnityTest]
    public IEnumerator TopStatus_ShowsNegativePopulationNetRateDuringStarvation()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        GameManager gameManager = Object.FindObjectOfType<GameManager>();
        SimulationManager simulationManager = Object.FindObjectOfType<SimulationManager>();
        Assert.That(root, Is.Not.Null);
        Assert.That(gameManager, Is.Not.Null);
        Assert.That(simulationManager, Is.Not.Null);
        simulationManager.SetRunning(false);

        gameManager.State.ResetDerivedEconomyForEditor(new ExpantaNum(500d));
        gameManager.State.RestoreCoreForEditor(
            0, TechLevel.Animal, ExpantaNum.Zero, 0L);
        gameManager.State.RestorePopulationForEditor(new ExpantaNum(10d));
        gameManager.State.RestorePopulationCapacityExactForEditor(
            new ExpantaNum(20d), ExpantaNum.Zero);
        gameManager.State.AdjustFoodRatesForEditor(
            ExpantaNum.Zero, new ExpantaNum(20d));

        Assert.That(gameManager.CurrentPopulationNetRatePerSecond, Is.LessThan(ExpantaNum.Zero));
        root.RefreshTopInfoForEditor();
        TMP_Text topPopulation = root.TopPopulationValueForEditor;
        Assert.That(topPopulation, Is.Not.Null);
        Assert.That(
            topPopulation.text,
            Does.Contain(gameManager.CurrentPopulationNetRatePerSecond.ToGameString() + "/s"));

        ExpantaNum populationBefore = gameManager.State.Population.Population;
        gameManager.Tick(3600d);
        Assert.That(gameManager.State.Population.Population, Is.LessThan(populationBefore));
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

        Assert.That(resourceManager.GetAmount(wood), Is.LessThanOrEqualTo(ExpantaNum.Zero));
    }

    private static string CaptureOverviewState()
    {
        // Do not call SaveManager/StoryManager.CaptureSaveData here: they refresh
        // story progress and could hide the very mutation this test must detect.
        GameManager game = GameManager.Instance;
        var storyIds = new List<string>(game.State.StoryProgress.CompletedChapterIds);
        storyIds.Sort(StringComparer.Ordinal);
        var snapshot = new SaveManager.KingdomSaveData
        {
            Version = SaveFormat.CurrentVersion,
            General = game.CaptureSaveData(),
            Resources = ResourceManager.Instance.CaptureSaveData(),
            Buildings = BuildingManager.Instance.CaptureSaveData(),
            Researches = ResearchManager.Instance.CaptureSaveData(),
            Workshop = WorkshopManager.Instance.CaptureSaveData(),
            Sectors = game.Sectors.CaptureSaveData(),
            UltraProject = game.UltraProject.State.CaptureSaveData(),
            Tutorial = TutorialManager.Current.CaptureSaveData(),
            Story = new SaveManager.StorySaveData { CompletedChapterIds = storyIds }
        };
        return JsonUtility.ToJson(snapshot);
    }

    private static UltraProjectStateSaveData CreateUltraProjectSave(
        UltraProjectDoctrine doctrine,
        UltraProjectStatus status,
        UltraProjectStage currentStage,
        string progress,
        List<UltraProjectStage> completedStages,
        bool launchFeePaid,
        int stateVersion = 100)
    {
        return new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            SaveVersion = UltraProjectState.CurrentSaveVersion,
            Doctrine = doctrine,
            Status = status,
            CurrentStage = currentStage,
            StageProgress = progress,
            CompletedStages = completedStages,
            LaunchFeePaid = launchFeePaid,
            StateVersion = stateVersion
        };
    }

    private static IEnumerator LoadIsolatedNewGame()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();
        // Verify the scene's manager wiring explicitly before using Instance;
        // this does not claim the yielded startup occurs at exact simulation t=0.
        Assert.That(Object.FindObjectOfType<GameBootstrap>().Completed, Is.True);
        Assert.That(Object.FindObjectOfType<GameManager>(), Is.Not.Null);
        Assert.That(Object.FindObjectOfType<ResourceManager>(), Is.Not.Null);
        Assert.That(Object.FindObjectOfType<BuildingManager>(), Is.Not.Null);
        Assert.That(Object.FindObjectOfType<ResearchManager>(), Is.Not.Null);
        SaveManager saves = Object.FindObjectOfType<SaveManager>();
        SimulationManager simulation = Object.FindObjectOfType<SimulationManager>();
        Assert.That(saves, Is.Not.Null);
        Assert.That(simulation, Is.Not.Null);
        Assert.That(saves.LastLoadCreatedNewGame, Is.True,
            "The real bootstrap must create a new game in the fixture's empty save root.");
        simulation.SetRunning(false);
    }

    private static IEnumerator WaitForRuntimeUiRoot(int maxFrames = 120)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            GameBootstrap bootstrap = Object.FindObjectOfType<GameBootstrap>();
            if (Object.FindObjectOfType<KingdomUIRoot>() != null &&
                bootstrap != null && bootstrap.Completed)
            {
                yield return null;
                yield break;
            }
            yield return null;
        }

        Assert.Fail("SampleScene did not create KingdomUIRoot within the frame budget.");
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

    private static void GrantResearchTestResources(ResourceManager resourceManager)
    {
        var totals = new Dictionary<Resource, ExpantaNum>();
        foreach (Research research in DataBase<Research>.All)
        {
            if (research == null)
                continue;
            foreach (Pair<Resource, ExpantaNum> requirement in research.ResourceRequirements)
            {
                if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                    continue;
                totals[requirement.First] = totals.TryGetValue(
                    requirement.First, out ExpantaNum total)
                    ? total + requirement.Second
                    : requirement.Second;
            }
        }
        foreach (WorkshopUpgrade upgrade in DataBase<WorkshopUpgrade>.All)
        {
            if (upgrade == null)
                continue;
            foreach (Pair<Resource, ExpantaNum> requirement in upgrade.ResourceRequirements)
            {
                if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                    continue;
                totals[requirement.First] = totals.TryGetValue(
                    requirement.First, out ExpantaNum total)
                    ? total + requirement.Second
                    : requirement.Second;
            }
        }
        foreach (Resource resource in DataBase<Resource>.All)
            resourceManager.SetAmount(resource, totals.TryGetValue(
                resource, out ExpantaNum total) ? total : ExpantaNum.Zero);
    }

    private static void CompleteResearchThroughRuntime(
        GameManager gameManager,
        ResearchManager researchManager,
        ResourceManager resourceManager,
        Research target)
    {
        ResearchActionResult action = researchManager.HandleResearchAction(target);
        Assert.That(action, Is.Not.EqualTo(ResearchActionResult.Invalid));
        Assert.That(action, Is.Not.EqualTo(ResearchActionResult.Blocked),
            "Research was blocked before its runtime prerequisite batch could start: " + target.Id);

        for (int guard = 0; guard < 10000 && !researchManager.IsResearchCompleted(target.Id); guard++)
        {
            if (researchManager.ActiveResearch == null && researchManager.ResearchQueue.Count > 0)
            {
                researchManager.TryStartNextQueuedResearch();
            }

            if (researchManager.ActiveResearch != null)
                researchManager.Tick(1000000d);
            else if (researchManager.ResearchQueue.Count == 0)
                break;
        }

        Assert.That(researchManager.IsResearchCompleted(target.Id), Is.True,
            "Research did not complete through the runtime clock: " + target.Id);
        Assert.That(gameManager.State.TechLevel, Is.GreaterThanOrEqualTo(target.TechLevel));
    }

    private static void CompleteResearchPrerequisites(
        GameManager gameManager,
        ResearchManager researchManager,
        ResourceManager resourceManager,
        Research target,
        HashSet<string> visiting)
    {
        if (target == null || researchManager.IsResearchCompleted(target.Id))
            return;
        Assert.That(visiting.Add(target.Id), Is.True,
            "Research prerequisite cycle detected at " + target.Id);

        for (int i = 0; i < target.Prerequisites.Count; i++)
        {
            Research prerequisite = target.Prerequisites[i];
            CompleteResearchPrerequisites(
                gameManager,
                researchManager,
                resourceManager,
                prerequisite,
                visiting);
            CompleteResearchThroughRuntime(
                gameManager,
                researchManager,
                resourceManager,
                prerequisite);
        }

        visiting.Remove(target.Id);
    }

    private Building CreateFlowBuilding(
        string id,
        string label,
        Resource resource,
        ExpantaNum productionRate)
    {
        Building building = ScriptableObject.CreateInstance<Building>();
        building.SetIdForEditor(id);
        building.Label = label;
        building.Description = label;
        building.TechLevel = TechLevel.Animal;
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
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>
            {
                new Pair<Resource, ExpantaNum>(resource, productionRate)
            },
            new List<Pair<Resource, ExpantaNum>>());
        createdObjects.Add(building);
        return building;
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

