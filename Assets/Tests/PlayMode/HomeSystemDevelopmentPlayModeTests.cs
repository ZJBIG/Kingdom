using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class HomeSystemDevelopmentPlayModeTests
{
    private string saveRoot;
    private int previousScreenWidth;
    private int previousScreenHeight;
    private FullScreenMode previousScreenMode;

    [SetUp]
    public void SetUp()
    {
        previousScreenWidth = Screen.width;
        previousScreenHeight = Screen.height;
        previousScreenMode = Screen.fullScreenMode;
        saveRoot = Path.Combine(Application.dataPath, "..", "Temp", "PlanetDevelopmentPlayMode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [TearDown]
    public void TearDown()
    {
        Screen.SetResolution(previousScreenWidth, previousScreenHeight, previousScreenMode);
        foreach (KingdomUIRoot ui in Object.FindObjectsOfType<KingdomUIRoot>())
            ui.gameObject.SetActive(false);
        foreach (SaveManager saves in Object.FindObjectsOfType<SaveManager>())
        {
            saves.SetReady(false);
            Object.DestroyImmediate(saves.gameObject);
        }
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }

    [UnityTest]
    public IEnumerator ThunderGateMenuBuildsTheSinglePropellantWorksAndKeepsSectorBuildingsOutOfOrdinaryRows()
    {
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
        yield return null;
        yield return null;
        yield return LoadPreparedGame();
        Assert.That((float)Screen.width / Screen.height, Is.LessThanOrEqualTo(1.8f),
            "The exploration interaction fixture must exercise a narrower landscape viewport.");
        GameManager game = GameManager.Instance;
        SectorBuildingDef works = DataBase<SectorBuildingDef>.Find("ThunderGatePropellantWorks");
        Assert.That(works, Is.Not.Null);
        OccupyWithPrerequisites(game, works.Sector);
        KingdomUIRoot ui = Object.FindObjectOfType<KingdomUIRoot>();
        ui.SetPage("Sectors");
        ui.RefreshSectorRowSummariesForEditor();
        yield return null;
        Transform row = SectorRow(ui, works.Sector.Id);
        Assert.That(row.gameObject.activeInHierarchy, Is.True);
        row.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        Assert.That(row.Find("SectorBuildingMenu/" + works.Id + "/Build").GetComponent<Button>().interactable, Is.False);
        row.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        row.GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(game.Sectors.GetState(works.Sector).InteriorExplorationCompleted, Is.False);
        ClickButton(ui.DetailActionButtonForEditor);
        Assert.That(game.Sectors.GetState(works.Sector).InteriorExplorationActive, Is.True);
        var exploration = works.Sector.InteriorExploration;
        ResourceDef scarce = exploration.ResourceRatesPerSecond[0].First;
        ResourceManager.Instance.SetAmount(scarce, ExpantaNum.Zero);
        ui.RefreshSectorRowSummariesForEditor();
        Assert.That(game.Sectors.GetInteriorExplorationPreview(works.Sector).Status,
            Is.EqualTo(SectorInteriorExplorationStatus.WaitingForSupply));
        AssertSectorSummaryFits(row);
        ClickButton(ui.DetailActionButtonForEditor);
        Assert.That(game.Sectors.GetState(works.Sector).InteriorExplorationActive, Is.False);
        Assert.That(game.Sectors.GetInteriorExplorationPreview(works.Sector).Status,
            Is.EqualTo(SectorInteriorExplorationStatus.Paused), "Pausing before the first supplied tick remains an explicit manual pause.");
        game.Sectors.TickActiveInteriorExploration(10d, game.State, ResourceManager.Instance, out _);
        Assert.That(game.Sectors.GetState(works.Sector).InteriorExplorationProgress, Is.EqualTo(ExpantaNum.Zero));
        ClickButton(ui.DetailActionButtonForEditor);
        ResourceManager.Instance.SetAmount(scarce, new ExpantaNum(1e10d));
        game.Sectors.TickActiveInteriorExploration(exploration.DurationSeconds.ToDouble() / 2d,
            game.State, ResourceManager.Instance, out _);
        ExpantaNum preserved = game.Sectors.GetState(works.Sector).InteriorExplorationProgress;
        Assert.That(preserved, Is.GreaterThan(ExpantaNum.Zero));
        ui.RefreshSectorRowSummariesForEditor();
        ClickButton(ui.DetailActionButtonForEditor);
        game.Sectors.TickActiveInteriorExploration(10d, game.State, ResourceManager.Instance, out _);
        Assert.That((game.Sectors.GetState(works.Sector).InteriorExplorationProgress - preserved).Abs(),
            Is.LessThanOrEqualTo(new ExpantaNum(1e-8d)));
        ClickButton(ui.DetailActionButtonForEditor);
        ui.SetPage("Resources");
        game.Sectors.TickActiveInteriorExploration(exploration.DurationSeconds.ToDouble(),
            game.State, ResourceManager.Instance, out _);
        ui.SetPage("Sectors");
        ui.RefreshSectorRowSummariesForEditor();
        Assert.That(game.Sectors.GetState(works.Sector).InteriorExplorationCompleted, Is.True);
        AssertSectorSummaryFits(row);
        float collapsedHeight = row.GetComponent<RectTransform>().rect.height;
        row.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Transform menu = row.Find("SectorBuildingMenu");
        Transform card = menu.Find(works.Id);
        Assert.That(card, Is.Not.Null);
        Assert.That(menu.Find("ThunderGateHydrogenCollector"), Is.Null);
        Assert.That(DataBase<ResourceDef>.Contains("Hydrogen"), Is.False);
        Assert.That(card.Find("Effect").GetComponent<TMPro.TMP_Text>().text,
            Does.Contain(DataBase<ResourceDef>.Find("RocketFuel").Label));
        Canvas.ForceUpdateCanvases();
        Assert.That(row.GetComponent<RectTransform>().rect.height - collapsedHeight,
            Is.EqualTo(menu.GetComponent<RectTransform>().rect.height).Within(1f));
        Button build = card.Find("Build").GetComponent<Button>();
        Assert.That(build.interactable, Is.True);
        Assert.That(build.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("建设" + works.Label));
        AssertCardGeometry(card);
        build.onClick.Invoke();
        yield return null;
        card = row.Find("SectorBuildingMenu/" + works.Id);
        Assert.That(BuildingManager.Instance.GetState(works).Amount, Is.EqualTo(ExpantaNum.One),
            "The unique building count is a discrete count.");
        foreach (string stageId in new[] { "ThunderCryogenicSeparation", "ThunderParallelRefining", "ThunderOrbitalLoadingRing" })
        {
            WorkshopUpgradeDef stage = DataBase<WorkshopUpgradeDef>.Find(stageId);
            Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.True);
            Assert.That(card.Find("Build").GetComponentInChildren<TMPro.TMP_Text>().text,
                Is.EqualTo("建设" + stage.Label));
            Assert.That(card.Find("Metadata").GetComponent<TMPro.TMP_Text>().text, Does.Not.Contain("阶段"));
            AssertCardGeometry(card);
            card.Find("Build").GetComponent<Button>().onClick.Invoke();
            yield return null;
            card = row.Find("SectorBuildingMenu/" + works.Id);
            Assert.That(WorkshopManager.Instance.IsPurchased(stage), Is.True);
            Assert.That(card.Find("Metadata").GetComponent<TMPro.TMP_Text>().text, Does.Contain(stage.Label));
        }
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.False);
        Assert.That(ProgressionModifierManager.Current.GetBuildingLogisticsProductionMultiplier(works), Is.GreaterThan(ExpantaNum.One));
        ui.RefreshSectorRowSummariesForEditor();
        AssertSectorSummaryFits(row);
        AssertCardGeometry(card);
        ui.SetPage("Buildings");
        yield return null;
        Transform ordinaryRows = ui.transform.Find("SafeAreaRoot/Content/PageHost/Buildings/DataRows");
        Assert.That(ordinaryRows.Find(works.Id), Is.Null);
    }

    [UnityTest]
    public IEnumerator HeliosProjectBuildsStagesRefreshesLocksAndRestoresPurchasedStagesAfterRebuilding()
    {
        yield return LoadPreparedGame();
        GameManager game = GameManager.Instance;
        SectorBuildingDef project = DataBase<SectorBuildingDef>.Find("DawnDysonSphere");
        Assert.That(project, Is.Not.Null);
        SectorDef helios = DataBase<SectorDef>.Find("HeliosCore");
        Assert.That(project.Sector, Is.SameAs(helios));
        Assert.That(BuildingManager.Instance.TryBuild(project, ExpantaNum.One, out BuildFailure buildFailure), Is.False);
        Assert.That(buildFailure, Is.EqualTo(BuildFailure.SectorNotOccupied));
        OccupyWithPrerequisites(game, helios);
        CompleteInteriorExploration(game, helios);
        KingdomUIRoot ui = Object.FindObjectOfType<KingdomUIRoot>();
        ui.SetPage("Sectors");
        ui.RefreshSectorRowSummariesForEditor();
        yield return null;
        Transform row = SectorRow(ui, helios.Id);
        row.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Transform card = row.Find("SectorBuildingMenu/" + project.Id);
        Assert.That(card, Is.Not.Null);
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.True);
        Assert.That(card.Find("Build").GetComponentInChildren<TMPro.TMP_Text>().text,
            Is.EqualTo("建设" + project.Label));
        Assert.That(card.Find("Effect").GetComponent<TMPro.TMP_Text>().text,
            Does.Contain("建成后单座电力").And.Not.Contain("重建后"));
        AssertCardGeometry(card);
        card.Find("Build").GetComponent<Button>().onClick.Invoke();
        yield return null;
        card = row.Find("SectorBuildingMenu/" + project.Id);
        WorkshopUpgradeDef firstStage = DataBase<WorkshopUpgradeDef>.Find("DysonCollectorFleet");
        Assert.That(WorkshopManager.Instance.IsPurchased(firstStage), Is.False);
        Assert.That(card.Find("Build").GetComponentInChildren<TMPro.TMP_Text>().text,
            Is.EqualTo("建设" + firstStage.Label));
        Assert.That(card.Find("Cost").GetComponent<TMPro.TMP_Text>().text,
            Does.Contain(firstStage.ResourceRequirements[0].Second.ToGameString()));
        AssertCardGeometry(card);
        card.Find("Build").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(WorkshopManager.Instance.IsPurchased(firstStage), Is.True);
        card = row.Find("SectorBuildingMenu/" + project.Id);
        string firstPower = card.Find("Effect").GetComponent<TMPro.TMP_Text>().text;
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.True);
        Button stageDetails = card.Find("Stage").GetComponent<Button>();
        Assert.That(stageDetails.targetGraphic, Is.Not.Null);
        Assert.That(stageDetails.targetGraphic.raycastTarget, Is.True,
            "The stage detail link must remain clickable after refreshing its text.");
        Assert.That(stageDetails.IsInteractable(), Is.True);
        stageDetails.onClick.Invoke();
        AssertEngineeringRecord(ui, project, true);
        AssertCardGeometry(card);
        Assert.That(WorkshopManager.Instance.IsPurchased(DataBase<WorkshopUpgradeDef>.Find("DysonStellarBelt")),
            Is.False, "Opening stage details must not purchase it.");
        card.Find("Build").GetComponent<Button>().onClick.Invoke();
        yield return null;
        card = row.Find("SectorBuildingMenu/" + project.Id);
        Assert.That(card.Find("Effect").GetComponent<TMPro.TMP_Text>().text, Is.Not.EqualTo(firstPower));
        yield return new WaitForSecondsRealtime(0.3f);
        ui.RefreshAutomaticUIForEditor();
        AssertEngineeringRecord(ui, project, true);
        WorkshopUpgradeDef finalStage = DataBase<WorkshopUpgradeDef>.Find("DysonControlNetwork");
        ResearchDef phaseMaterial = DataBase<ResearchDef>.Find("PhaseMaterialEngineering");
        ResearchManager.Instance.GetState(phaseMaterial).SetStatusForEditor(ResearchStatus.Available);
        ui.RefreshSectorRowSummariesForEditor();
        Assert.That(WorkshopManager.Instance.GetPurchaseFailure(finalStage),
            Is.EqualTo(WorkshopPurchaseFailure.ResearchIncomplete));
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.False,
            "The final stage remains locked until its material research is completed.");
        ResearchManager.Instance.GetState(phaseMaterial).SetStatusForEditor(ResearchStatus.Completed);
        ui.RefreshSectorRowSummariesForEditor();
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.True);
        AssertCardGeometry(card);
        card.Find("Build").GetComponent<Button>().onClick.Invoke();
        yield return null;
        card = row.Find("SectorBuildingMenu/" + project.Id);
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.False);
        foreach (WorkshopUpgradeDef stage in DataBase<WorkshopUpgradeDef>.All)
            if (stage.RequiredBuilding == project)
                Assert.That(WorkshopManager.Instance.IsPurchased(stage), Is.True, stage.Id);
        AssertCardGeometry(card);
        card.Find("Stage").GetComponent<Button>().onClick.Invoke();
        AssertEngineeringRecord(ui, project, true);
        card.Find("Deconstruct").GetComponent<Button>().onClick.Invoke();
        yield return null;
        card = row.Find("SectorBuildingMenu/" + project.Id);
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.True);
        Assert.That(WorkshopManager.Instance.IsPurchased(firstStage), Is.True);
        Assert.That(card.Find("Metadata").GetComponent<TMPro.TMP_Text>().text,
            Does.Contain("已保留：").And.Contain("工程本体待重建").And.Not.Contain("待建设："));
        Assert.That(card.Find("Effect").GetComponent<TMPro.TMP_Text>().text, Does.Contain("重建后单座电力"));
        yield return new WaitForSecondsRealtime(0.3f);
        ui.RefreshAutomaticUIForEditor();
        AssertEngineeringRecord(ui, project, false);
        AssertCardGeometry(card);
        card.Find("Build").GetComponent<Button>().onClick.Invoke();
        yield return null;
        card = row.Find("SectorBuildingMenu/" + project.Id);
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.False);
        Assert.That(BuildingManager.Instance.GetState(project).Amount, Is.EqualTo(ExpantaNum.One),
            "The rebuilt unique building count is a discrete count.");
    }

    private static IEnumerator LoadPreparedGame()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        SimulationManager.Instance.SetRunning(false);
        GameManager game = GameManager.Instance;
        game.InitializeNewGameForEditor();
        game.AdvanceTechLevelForEditor(TechLevel.Spacer);
        game.State.RestorePopulationForEditor(new ExpantaNum(1e6d));
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        WorkshopManager.Instance.RestoreSaveDataForEditor(new SaveManager.WorkshopSaveData());
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1e10d));
        // This fixture starts as an established Spacer save, after the first-unlock tutorial.
        Object.FindObjectOfType<TutorialManager>().RestoreSaveData(null, game.State.TechLevel);
        Object.FindObjectOfType<KingdomUIRoot>().RefreshOnboardingForEditor();
        Assert.That(OnboardingInputGate.BlocksNavigation, Is.False);
    }

    private static Transform SectorRow(KingdomUIRoot ui, string id) =>
        ui.transform.Find("SafeAreaRoot/Content/PageHost/Sectors/DataRows/SectorRow_" + id);

    private static void CompleteInteriorExploration(GameManager game, SectorDef definition)
    {
        var project = definition.InteriorExploration;
        Assert.That(project, Is.Not.Null);
        Assert.That(game.Sectors.TryStartInteriorExploration(definition, out SectorOperationFailure failure), Is.True, failure.ToString());
        Assert.That(game.Sectors.TickActiveInteriorExploration(project.DurationSeconds.ToDouble() + 1d,
            game.State, ResourceManager.Instance, out failure), Is.True, failure.ToString());
        Assert.That(game.Sectors.GetState(definition).InteriorExplorationCompleted, Is.True);
    }

    private static void AssertEngineeringRecord(KingdomUIRoot ui, SectorBuildingDef project, bool bodyBuilt)
    {
        string body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body")
            .GetComponent<TMPro.TMP_Text>().text;
        Assert.That(body, Does.Contain("工程建设记录").And.Contain(project.Label));
        foreach (WorkshopUpgradeDef structure in DataBase<WorkshopUpgradeDef>.All)
            if (structure.RequiredBuilding == project)
                Assert.That(body, Does.Contain(structure.Label + (WorkshopManager.Instance.IsPurchased(structure)
                    ? bodyBuilt ? "：已建成" : "：记录保留，重建本体后恢复"
                    : "：待建设")));
    }

    private static void AssertCardGeometry(Transform card)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = card.GetComponent<RectTransform>();
        Assert.That(rect.rect.width, Is.GreaterThan(0f));
        string[] names = { "Name", "Amount", "Metadata", "Effect", "Cost", "Stage", "Build", "Deconstruct" };
        for (int i = 0; i < names.Length; i++)
        {
            RectTransform child = card.Find(names[i]).GetComponent<RectTransform>();
            if (!child.gameObject.activeSelf) continue;
            Rect bounds = BoundsInCard(rect, child);
            TMPro.TMP_Text text = child.GetComponentInChildren<TMPro.TMP_Text>();
            if (text != null)
            {
                text.ForceMeshUpdate();
                Assert.That(text.isTextTruncated, Is.False, names[i] + " must show its full text.");
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f),
                    names[i] + " needs enough authored height for every line.");
            }
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(rect.rect.xMin - 1f), names[i]);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(rect.rect.xMax + 1f), names[i]);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(rect.rect.yMin - 1f), names[i]);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(rect.rect.yMax + 1f), names[i]);
            for (int j = 0; j < i; j++)
            {
                RectTransform other = card.Find(names[j]).GetComponent<RectTransform>();
                if (other.gameObject.activeSelf)
                    Assert.That(bounds.Overlaps(BoundsInCard(rect, other)), Is.False, names[i] + " / " + names[j]);
            }
        }
    }

    private static void ClickButton(Button button)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.GetComponent<RectTransform>();
        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty, $"The exploration action must receive a pointer raycast: active={button.gameObject.activeInHierarchy}, " +
            $"graphic={button.targetGraphic.raycastTarget}, position={pointer.position}, screen={Screen.width}x{Screen.height}, rect={rect.rect}, canvas={canvas.name}.");
        GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.That(target, Is.SameAs(button.gameObject), "An overlay must not intercept the exploration action.");
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void AssertSectorSummaryFits(Transform row)
    {
        Canvas.ForceUpdateCanvases();
        TMPro.TMP_Text summary = row.Find("Metadata").GetComponent<TMPro.TMP_Text>();
        summary.ForceMeshUpdate();
        Assert.That(summary.isTextTruncated, Is.False, "Exploration supply and project stage summaries must remain fully visible.");
        Assert.That(summary.preferredWidth, Is.LessThanOrEqualTo(summary.rectTransform.rect.width + 1f));
        RectTransform buildings = row.Find("Buildings").GetComponent<RectTransform>();
        Assert.That(BoundsInCard(row.GetComponent<RectTransform>(), summary.rectTransform).Overlaps(
            BoundsInCard(row.GetComponent<RectTransform>(), buildings)), Is.False,
            "The summary must not overlap the authored building expansion button.");
    }

    private static Rect BoundsInCard(RectTransform card, RectTransform child)
    {
        Vector3[] corners = new Vector3[4];
        child.GetWorldCorners(corners);
        Vector3 min = card.InverseTransformPoint(corners[0]);
        Vector3 max = card.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static void OccupyWithPrerequisites(GameManager game, SectorDef sector)
    {
        foreach (SectorDef prerequisite in sector.PrerequisiteSectors)
            OccupyWithPrerequisites(game, prerequisite);
        game.Sectors.GetState(sector).SetUnlockedForEditor(true);
        game.Sectors.GetState(sector).SetOccupiedForEditor(true);
    }
}

