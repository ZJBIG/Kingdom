using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public sealed class ReviewInteractionPlayModeTests
{
    private string saveRoot;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;
    private KingdomUIRoot ui;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ReviewInteraction-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene scene = SceneManager.GetSceneByName("SampleScene");
        if (scene.IsValid() && scene.isLoaded)
            foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        yield return null;
        SaveManager.ClearSaveRootOverrideForTests();
        string parent = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
        Assert.That(Path.GetDirectoryName(saveRoot), Is.EqualTo(parent), "Temporary cleanup stays inside the project Temp directory.");
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }

    private IEnumerator Load()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        for (int i = 0; i < 160; i++)
        {
            ui = Object.FindObjectOfType<KingdomUIRoot>();
            if (ui != null && Object.FindObjectOfType<GameBootstrap>()?.Completed == true) break;
            yield return null;
        }
        Assert.That(ui, Is.Not.Null);
        SimulationManager.Instance.SetRunning(false);
        yield return null;
    }

    [UnityTest]
    public IEnumerator BatchDeconstruction_PreviewAndCancelDoNotCommit_ConfirmCommitsOnce()
    {
        yield return Load();
        Building building = DataBase<Building>.Find("WoodHouse");
        BuildingState state = BuildingManager.Instance.States[building];
        BuildingManager.Instance.SetAmountAndRatesForEditor(state, new ExpantaNum(10));
        ui.SetPage("Buildings");
        ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls/Quantity_Ten").GetComponent<Button>().onClick.Invoke();
        ui.RefreshLiveCardValuesForEditor();
        Button demolish = ui.GetDeconstructButtonForEditor(building);
        Assert.That(demolish, Is.Not.Null);
        ExpantaNum before = state.Amount;
        demolish.onClick.Invoke();
        Assert.That(ui.ReviewConfirmationPendingForEditor, Is.True);
        Assert.That(ui.transform.Find("SafeAreaRoot").GetComponent<CanvasGroup>().interactable, Is.False, "The modal prevents commands beneath it.");
        Assert.That(state.Amount >= before && state.Amount <= before, Is.True, "Preview preserves inventory.");
        ClickConfirmation("Cancel");
        Assert.That(ui.ReviewConfirmationPendingForEditor, Is.False);
        Assert.That(ui.transform.Find("SafeAreaRoot").GetComponent<CanvasGroup>().interactable, Is.True);
        Assert.That(state.Amount >= before && state.Amount <= before, Is.True);
        demolish.onClick.Invoke();
        ClickConfirmation("Confirm");
        Assert.That(state.Amount, Is.LessThan(before));
        ExpantaNum committed = state.Amount;
        ClickConfirmation("Confirm");
        Assert.That(state.Amount >= committed && state.Amount <= committed, Is.True, "A second click has no pending command.");
    }

    [UnityTest]
    public IEnumerator ResourceSources_ContainClickableLinks_AndNavigateToExecutableBuildingRow()
    {
        yield return Load();
        Building building = DataBase<Building>.Find("Lumberyard");
        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.States[building], ExpantaNum.One);
        Resource wood = DataBase<Resource>.Find("WoodLog");
        ui.SetPage("Resources"); ui.ShowResourceDetailsForEditor(wood);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        body.ForceMeshUpdate(true);
        bool linked = false;
        for (int i = 0; i < body.textInfo.linkCount; i++)
            if (body.textInfo.linkInfo[i].GetLinkID() == "building:" + building.Id) linked = true;
        Assert.That(linked, Is.True, "Production source is a semantic link, including stopped owned producers.");
        for (int i = 0; i < body.textInfo.linkCount; i++)
            if (body.textInfo.linkInfo[i].GetLinkID() == "building:" + building.Id)
            {
                TMP_CharacterInfo character = body.textInfo.characterInfo[body.textInfo.linkInfo[i].linkTextfirstCharacterIndex];
                Vector3 world = body.transform.TransformPoint((character.bottomLeft + character.topRight) * .5f);
                body.GetComponent<UIReviewTextLinks>().OnPointerClick(new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, world) });
                break;
            }
        Assert.That(ui.SelectedBuildingForEditor, Is.SameAs(building));
        Assert.That(ui.ReviewLocateForEditor.gameObject.activeInHierarchy, Is.True);
        Assert.That(ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls").gameObject.activeInHierarchy, Is.True);
    }

    [UnityTest]
    public IEnumerator ActiveCampaignWithCasualties_RetreatStaysReachable_RepairIsSeparate()
    {
        yield return Load();
        SectorDefinition sector = DataBase<SectorDefinition>.Find("TauCetiFoundry");
        SectorState state = GameManager.Instance.Sectors.GetState(sector);
        state.SetUnlockedForEditor(true); state.SetCampaignActiveForEditor(true); state.SetCampaignCasualtiesForEditor(ExpantaNum.One);
        GameManager.Instance.State.RestoreCampaignForEditor(true, sector.Id, ExpantaNum.One, ExpantaNum.One);
        ui.SetPage("Sectors"); ui.ShowSectorDetailsForEditor(sector);
        Assert.That(ui.DetailActionButtonForEditor.interactable, Is.True);
        Assert.That(ui.ReviewRepairForEditor.gameObject.activeInHierarchy, Is.True);
        List<UIButtonSoundManager.Sound> requests = new();
        Action<UIButtonSoundManager.Sound> observer = requests.Add;
        UIButtonSoundManager.PlayRequested += observer;
        try
        {
            ui.DetailActionButtonForEditor.onClick.Invoke();
            Assert.That(state.CampaignActive, Is.False, "The primary operation stops the active campaign even while repair materials are unavailable.");
            Assert.That(GameManager.Instance.State.Campaign.Active, Is.False);
            Assert.That(requests, Does.Contain(UIButtonSoundManager.Sound.StrategicStop));
        }
        finally { UIButtonSoundManager.PlayRequested -= observer; }
    }

    [UnityTest]
    public IEnumerator AuthoredOverviewTools_SaveAndExpandActualOfflineChanges()
    {
        yield return Load();
        SaveManager save = SaveManager.Instance;
        save.ApplyOfflineProgressForEditor(1000, 1010);
        ui.SetPage("Overview");
        Button saveButton = ui.transform.Find("SafeAreaRoot/Content/PageTool/PageTools/Save").GetComponent<Button>();
        Button offlineButton = ui.transform.Find("SafeAreaRoot/Content/PageTool/PageTools/Offline").GetComponent<Button>();
        Assert.That(saveButton.gameObject.activeInHierarchy, Is.True);
        Assert.That(offlineButton.gameObject.activeInHierarchy, Is.True);
        saveButton.onClick.Invoke();
        Assert.That(save.LastSuccessfulSaveUnixSeconds, Is.GreaterThan(0));
        Assert.That(save.LastSaveFailed, Is.False);
        offlineButton.onClick.Invoke();
        Assert.That(save.LastOfflineSummary, Is.Not.Null, "Expanding changes presentation, not the actual settlement evidence.");
        ui.SetPage("Research");
        Assert.That(saveButton.gameObject.activeInHierarchy, Is.False);
        Assert.That(ui.transform.Find("SafeAreaRoot/Content/PageTool/PageTools/ResetResearch").gameObject.activeInHierarchy, Is.True);
        Canvas.ForceUpdateCanvases();
        Transform pageTool = ui.transform.Find("SafeAreaRoot/Content/PageTool");
        Bounds queue = RectBounds(pageTool, (RectTransform)pageTool.Find("ResearchQueueViewport"));
        Bounds tools = RectBounds(pageTool, (RectTransform)pageTool.Find("PageTools"));
        Assert.That(queue.max.x, Is.LessThanOrEqualTo(tools.min.x), "Authored page tools have a separate reserved area beside the research queue.");
    }

    private void ClickConfirmation(string name) => ui.transform.Find("SafeAreaRoot/ReviewControls/Confirmation/Panel/Actions/" + name).GetComponent<Button>().onClick.Invoke();

    [UnityTest]
    public IEnumerator HousingRecoveryAndUpgradeDetails_UseManagerConstraintsAndRealInvestmentValues()
    {
        yield return Load();
        BuildingManager buildings = BuildingManager.Instance;
        Building house = DataBase<Building>.Find("WoodHouse");
        Building quarry = DataBase<Building>.Find("Quarry");
        ResourceManager.Instance.SetAmount(DataBase<Resource>.Find("WoodLog"), new ExpantaNum(10000));
        ui.SetPage("Buildings");
        ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls/ShowDetails").GetComponent<Toggle>().isOn = true;
        ui.ShowBuildingDetailsForEditor(house);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        string availableHousingDetails = body.text;
        buildings.SetAmountAndRatesForEditor(buildings.States[quarry], new ExpantaNum(100));
        Assert.That(buildings.AvailableProductivity, Is.LessThan(ExpantaNum.Zero));
        ui.ShowBuildingDetailsForEditor(house);
        Assert.That(body.text, Is.EqualTo(availableHousingDetails), "Zero-consumption housing keeps the same availability display under negative productivity.");
        Assert.That(body.text, Does.Contain((house.PopulationCapacityGranted * PopulationState.FoodConsumptionPerPerson).ToGameString() + "/s"));
        Assert.That(buildings.GetBuildFailure(house, ExpantaNum.One), Is.EqualTo(BuildFailure.None));
        Assert.That(buildings.TryBuild(house, ExpantaNum.One, out _), Is.True);
        GameManager.Instance.State.AdvanceTechLevelForEditor(TechLevel.StoneAge);
        Research masonry = DataBase<Research>.Find("Masonry");
        ResearchState masonryState = ResearchManager.Instance.States[masonry];
        masonryState.SetProgressForEditor(masonryState.BaseCost);
        masonryState.SetStatusForEditor(ResearchStatus.Completed);
        buildings.SetAmountAndRatesForEditor(buildings.States[quarry], ExpantaNum.Zero);
        foreach (var entry in ResourceManager.Instance.States)
            ResourceManager.Instance.SetAmount(entry.Key, new ExpantaNum(100000));
        Assert.That(buildings.TryGetUnlockedUpgradeTarget(house, out Building target), Is.True);
        ui.ShowBuildingDetailsForEditor(house);
        Assert.That(body.text, Does.Contain(target.Label), "Upgrade comparison identifies its actual unlocked target.");
        ExpantaNum capacityDelta = target.PopulationCapacityGranted - house.PopulationCapacityGranted;
        Assert.That(body.text, Does.Contain("+" + capacityDelta.ToGameString()));
        Assert.That(body.text, Does.Contain("+" + (capacityDelta * PopulationState.FoodConsumptionPerPerson).ToGameString()));
        ExpantaNum population = GameManager.Instance.State.Population.Population;
        Assert.That(buildings.GetUpgradeFailure(house, ExpantaNum.One), Is.EqualTo(BuildFailure.None));
        Assert.That(buildings.TryUpgrade(house, ExpantaNum.One, out _), Is.True);
        Assert.That(GameManager.Instance.State.Population.Population.ApproximatelyEquals(population), Is.True, "Housing capacity does not grant residents immediately.");
    }

    [UnityTest]
    public IEnumerator ResearchCancellation_ListsDependentItems_AndDoesNotMutateUntilConfirmation()
    {
        yield return Load();
        Research target = DataBase<Research>.Find("StoneTools");
        ResearchManager manager = ResearchManager.Instance;
        manager.HandleResearchAction(target);
        Research prerequisite = target.Prerequisites[0];
        Assert.That(manager.GetCancellationPreview(prerequisite).Count, Is.GreaterThan(1));
        ui.SetPage("Research"); ui.ShowResearchDetailsForEditor(prerequisite);
        int countBefore = manager.ResearchQueue.Count;
        ResearchState activeBefore = manager.ActiveResearch;
        ui.DetailActionButtonForEditor.onClick.Invoke();
        Assert.That(ui.ReviewConfirmationPendingForEditor, Is.True);
        Assert.That(manager.ResearchQueue.Count, Is.EqualTo(countBefore), "Queue length is a discrete count.");
        Assert.That(manager.ActiveResearch, Is.SameAs(activeBefore));
        Canvas.ForceUpdateCanvases();
        Transform panel = ui.transform.Find("SafeAreaRoot/ReviewControls/Confirmation/Panel");
        Bounds viewport = RectBounds(panel, (RectTransform)panel.Find("Viewport"));
        Bounds actions = RectBounds(panel, (RectTransform)panel.Find("Actions"));
        Assert.That(viewport.min.y, Is.GreaterThanOrEqualTo(actions.max.y), "Scrollable impact text does not obscure confirm/cancel controls.");
        ClickConfirmation("Cancel");
        Assert.That(manager.ResearchQueue.Count, Is.EqualTo(countBefore));
        ui.DetailActionButtonForEditor.onClick.Invoke(); ClickConfirmation("Confirm");
        Assert.That(manager.IsQueued(target), Is.False, "Dependent research is removed only at confirmation.");
        Assert.That(manager.ResearchQueue.Count, Is.LessThan(countBefore));
    }

    private static Bounds RectBounds(Transform parent, RectTransform rect)
    {
        Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Bounds bounds = new(parent.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(parent.InverseTransformPoint(corners[i]));
        return bounds;
    }

    [UnityTest]
    public IEnumerator ResearchQueue_EarlierButtonMovesIndependentWaitingItemWithoutCancellingProgress()
    {
        yield return Load();
        ResearchManager manager = ResearchManager.Instance;
        foreach (string id in new[] { "Agriculture", "AnimalHusbandry", "ControlledFire", "ClayExtraction", "KnowledgeSharing" })
            manager.HandleResearchAction(DataBase<Research>.Find(id));
        ResearchState selected = null; int originalIndex = -1;
        for (int i = 1; i < manager.ResearchQueue.Count; i++)
            if (manager.CanMoveQueuedResearch(manager.ResearchQueue[i].Definition, i - 1))
            { selected = manager.ResearchQueue[i]; originalIndex = i; break; }
        Assert.That(selected, Is.Not.Null, "Fixture contains independent waiting research.");
        int count = manager.ResearchQueue.Count;
        ResearchState active = manager.ActiveResearch;
        bool paid = selected.CostPaid;
        ExpantaNum progress = selected.Progress;
        ui.SetPage("Research"); ui.ShowResearchDetailsForEditor(selected.Definition);
        Button earlier = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailTools/Earlier").GetComponent<Button>();
        Assert.That(earlier.gameObject.activeInHierarchy && earlier.interactable, Is.True);
        earlier.onClick.Invoke();
        Assert.That(manager.ResearchQueue[originalIndex - 1], Is.SameAs(selected), "Queue indices are discrete order semantics.");
        Assert.That(manager.ResearchQueue.Count, Is.EqualTo(count));
        Assert.That(manager.ActiveResearch, Is.SameAs(active));
        Assert.That(selected.CostPaid, Is.EqualTo(paid));
        Assert.That(selected.Progress.ApproximatelyEquals(progress), Is.True, "Reordering preserves earned progress.");
        Assert.That(ui.ReviewConfirmationPendingForEditor, Is.False, "Moving queue order does not cancel dependents.");
    }
}
