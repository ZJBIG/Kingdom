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
        card.Find("Name").GetComponent<Button>().onClick.Invoke();
        yield return null;
        AssertDetailContains(ui, DataBase<ResourceDef>.Find("RocketFuel").Label);
        AssertDetailRequirements(ui, works.ResourceRequirements, works);
        Canvas.ForceUpdateCanvases();
        Assert.That(row.GetComponent<RectTransform>().rect.height - collapsedHeight,
            Is.EqualTo(menu.GetComponent<RectTransform>().rect.height).Within(1f));
        Button build = card.Find("Build").GetComponent<Button>();
        Assert.That(build.interactable, Is.True);
        yield return AssertBuildActionNamesStructure(ui, card, works.Label);
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
            yield return AssertBuildActionNamesStructure(ui, card, stage.Label);
            Assert.That(card.Find("Metadata"), Is.Null);
            AssertCardGeometry(card);
            card.Find("Name").GetComponent<Button>().onClick.Invoke();
            yield return null;
            AssertDetailContains(ui, stage.Label);
            AssertDetailContains(ui, stage.Description);
            AssertDetailRequirements(ui, stage.ResourceRequirements);
            yield return PurchaseStageAndAssertPayment(card, stage);
            card = row.Find("SectorBuildingMenu/" + works.Id);
            Assert.That(WorkshopManager.Instance.IsPurchased(stage), Is.True);
            Assert.That(card.Find("Name").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(stage.Label));
            card.Find("Name").GetComponent<Button>().onClick.Invoke();
            yield return null;
            AssertDetailContains(ui, stage.Description);
        }
        Assert.That(card.Find("Build").gameObject.activeSelf, Is.False);
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
    public IEnumerator HeliosProjectBuildsStagesRefreshesLocksAndPreservesPurchasedStagesWhenPaused()
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
        yield return AssertBuildActionNamesStructure(ui, card, project.Label);
        card.Find("Name").GetComponent<Button>().onClick.Invoke();
        yield return null;
        AssertDetailRequirements(ui, project.ResourceRequirements, project);
        AssertDetailContains(ui, "<sprite=\"HudPower\" index=0>+");
        AssertCardGeometry(card);
        card.Find("Build").GetComponent<Button>().onClick.Invoke();
        yield return null;
        WorkshopUpgradeDef firstStage = DataBase<WorkshopUpgradeDef>.Find("DysonCollectorFleet");
        WorkshopUpgradeDef middleStage = DataBase<WorkshopUpgradeDef>.Find("DysonStellarBelt");
        WorkshopUpgradeDef finalStage = DataBase<WorkshopUpgradeDef>.Find("DysonControlNetwork");
        foreach (WorkshopUpgradeDef stage in new[] { firstStage, middleStage })
        {
            card = row.Find("SectorBuildingMenu/" + project.Id);
            Assert.That(WorkshopManager.Instance.IsPurchased(stage), Is.False);
            yield return AssertBuildActionNamesStructure(ui, card, stage.Label);
            card.Find("Name").GetComponent<Button>().onClick.Invoke();
            yield return null;
            AssertDetailContains(ui, stage.Label);
            AssertDetailContains(ui, stage.Description);
            AssertDetailRequirements(ui, stage.ResourceRequirements);
            Assert.That(WorkshopManager.Instance.IsPurchased(stage), Is.False,
                "Opening building details must not buy an engineering structure.");
            AssertCardGeometry(card);
            yield return PurchaseStageAndAssertPayment(card, stage);
        }
        card = row.Find("SectorBuildingMenu/" + project.Id);
        ResearchDef phaseMaterial = DataBase<ResearchDef>.Find("PhaseMaterialEngineering");
        ResearchManager.Instance.GetState(phaseMaterial).SetStatusForEditor(ResearchStatus.Available);
        ui.RefreshSectorRowSummariesForEditor();
        Assert.That(WorkshopManager.Instance.GetPurchaseFailure(finalStage),
            Is.EqualTo(WorkshopPurchaseFailure.ResearchIncomplete));
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.False,
            "The final structure remains locked until its material research is completed.");
        card.Find("Name").GetComponent<Button>().onClick.Invoke();
        yield return null;
        AssertDetailContains(ui, finalStage.Label);
        AssertDetailContains(ui, finalStage.Description);
        Assert.That(card.Find("Name").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(middleStage.Label));
        Assert.That(card.Find("Deconstruct").gameObject.activeSelf, Is.False);
        AssertDetailRequirements(ui, finalStage.ResourceRequirements);
        var lockedBalances = CaptureBalances(finalStage.ResourceRequirements);
        Assert.That(WorkshopManager.Instance.TryPurchase(finalStage, out _), Is.False);
        AssertBalancesUnchanged(lockedBalances);
        Assert.That(WorkshopManager.Instance.IsPurchased(finalStage), Is.False);
        ResearchManager.Instance.GetState(phaseMaterial).SetStatusForEditor(ResearchStatus.Completed);
        ui.RefreshSectorRowSummariesForEditor();
        Assert.That(card.Find("Build").GetComponent<Button>().interactable, Is.True);
        AssertCardGeometry(card);
        yield return PurchaseStageAndAssertPayment(card, finalStage);
        card = row.Find("SectorBuildingMenu/" + project.Id);
        Assert.That(card.Find("Name").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(finalStage.Label));
        Assert.That(card.Find("Build").gameObject.activeSelf, Is.False);
        AssertEngineeringPurchases(project);
        AssertCardGeometry(card);
        card.Find("Name").GetComponent<Button>().onClick.Invoke();
        yield return null;
        AssertDetailContains(ui, finalStage.Description);
        foreach (TMPro.TMP_Text label in DetailContent(ui).GetComponentsInChildren<TMPro.TMP_Text>())
            Assert.That(label.transform.parent.name, Does.Not.StartWith("Requirement_"),
                "Completed engineering must not keep showing the base construction bill.");
        var preservedBalances = CaptureBalances(project.ResourceRequirements);
        Assert.That(BuildingManager.Instance.TryDeconstruct(project, ExpantaNum.One, out BuildFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.DeconstructionUnavailable));
        AssertBalancesUnchanged(preservedBalances);
        BuildingState state = BuildingManager.Instance.GetState(project);
        ExpantaNum completedPowerMultiplier = ProgressionModifierManager.Current.GetBuildingPowerProductionMultiplier(project);
        card.Find("ToggleEnabled").GetComponent<Button>().onClick.Invoke();
        yield return null;
        card = row.Find("SectorBuildingMenu/" + project.Id);
        Assert.That(state.IsEnabled, Is.False);
        Assert.That(state.Amount, Is.EqualTo(ExpantaNum.One), "A unique facility's discrete count survives pausing.");
        AssertEngineeringPurchases(project);
        AssertBalancesUnchanged(preservedBalances);
        Assert.That(card.Find("Build").gameObject.activeSelf, Is.False);
        Assert.That(card.Find("ToggleEnabled/ResumeIcon").gameObject.activeSelf, Is.True);
        AssertCardGeometry(card);
        card.Find("ToggleEnabled").GetComponent<Button>().onClick.Invoke();
        yield return null;
        card = row.Find("SectorBuildingMenu/" + project.Id);
        Assert.That(state.IsEnabled, Is.True);
        Assert.That(state.Amount, Is.EqualTo(ExpantaNum.One), "Resuming preserves the discrete facility count.");
        AssertEngineeringPurchases(project);
        AssertBalancesUnchanged(preservedBalances);
        Assert.That(ProgressionModifierManager.Current.GetBuildingPowerProductionMultiplier(project)
            .ApproximatelyEquals(completedPowerMultiplier), Is.True);
        Assert.That(card.Find("ToggleEnabled/PauseIcon").gameObject.activeSelf, Is.True);
        Assert.That(card.Find("Build").gameObject.activeSelf, Is.False);
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

    private static Transform DetailContent(KingdomUIRoot ui) =>
        ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent");

    private static void AssertDetailContains(KingdomUIRoot ui, string text)
    {
        foreach (TMPro.TMP_Text label in DetailContent(ui).GetComponentsInChildren<TMPro.TMP_Text>())
            if (label.text.Contains(text)) return;
        Assert.Fail("Selected building detail is missing: " + text);
    }

    private static void AssertDetailRequirements(KingdomUIRoot ui,
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements, BuildingDef construction = null)
    {
        foreach (var requirement in requirements)
        {
            bool found = false;
            foreach (TMPro.TMP_Text label in DetailContent(ui).GetComponentsInChildren<TMPro.TMP_Text>())
            {
                if (!label.transform.parent.name.StartsWith("Requirement_") ||
                    label.name != "Label" || label.text != requirement.First.Label) continue;
                string amount = label.transform.parent.Find("Amount").GetComponent<TMPro.TMP_Text>().text;
                ExpantaNum expected = requirement.Second * (construction == null ? ExpantaNum.One
                    : BuildingManager.GetConstructionCostMultiplierForEditor(construction));
                Assert.That(amount, Does.StartWith(expected.ToGameString() + " "));
                found = true;
                break;
            }
            Assert.That(found, Is.True, "Missing construction material: " + requirement.First.Id);
        }
    }

    private static Dictionary<ResourceDef, ExpantaNum> CaptureBalances(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements)
    {
        var balances = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (var requirement in requirements)
            balances[requirement.First] = ResourceManager.Instance.GetAmount(requirement.First);
        return balances;
    }

    private static void AssertBalancesUnchanged(Dictionary<ResourceDef, ExpantaNum> balances)
    {
        foreach (var balance in balances)
            Assert.That(ResourceManager.Instance.GetAmount(balance.Key).ApproximatelyEquals(balance.Value),
                Is.True, balance.Key.Id + " must not be charged.");
    }

    private static IEnumerator PurchaseStageAndAssertPayment(Transform card, WorkshopUpgradeDef stage)
    {
        Transform row = card.parent.parent;
        string buildingId = card.name;
        KingdomUIRoot ui = card.GetComponentInParent<KingdomUIRoot>();
        var balances = CaptureBalances(stage.ResourceRequirements);
        card.Find("Build").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(WorkshopManager.Instance.IsPurchased(stage), Is.True, stage.Id);
        foreach (var requirement in stage.ResourceRequirements)
        {
            ExpantaNum paid = balances[requirement.First] - ResourceManager.Instance.GetAmount(requirement.First);
            ExpantaNum tolerance = ExpantaNum.Max(ExpantaNum.One, requirement.Second.Abs()) * new ExpantaNum(1e-6d);
            Assert.That((paid - requirement.Second).Abs(), Is.LessThanOrEqualTo(tolerance),
                stage.Id + " must pay the complete material bill exactly once.");
        }
        Transform refreshedCard = row.Find("SectorBuildingMenu/" + buildingId);
        Assert.That(refreshedCard.Find("Name").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo(stage.Label));
        refreshedCard.Find("Name").GetComponent<Button>().onClick.Invoke();
        yield return null;
        AssertDetailContains(ui, "当前阶段：" + stage.Label);
        AssertDetailContains(ui, stage.Description);
    }

    private static void AssertEngineeringPurchases(SectorBuildingDef project)
    {
        foreach (WorkshopUpgradeDef structure in DataBase<WorkshopUpgradeDef>.All)
            if (structure.RequiredBuilding == project)
                Assert.That(WorkshopManager.Instance.IsPurchased(structure), Is.True, structure.Id);
    }

    private static IEnumerator AssertBuildActionNamesStructure(KingdomUIRoot ui, Transform card, string structureName)
    {
        Button build = card.Find("Build").GetComponent<Button>();
        bool staged = BuildingManager.IsMegastructure(DataBase<SectorBuildingDef>.Find(card.name));
        Assert.That(card.Find("Amount").gameObject.activeSelf, Is.EqualTo(!staged));
        Assert.That(build.transform.Find("Label").gameObject.activeSelf, Is.EqualTo(!staged));
        if (staged)
        {
            Image icon = build.transform.Find("MegastructureIcon").GetComponent<Image>();
            Assert.That(icon.gameObject.activeSelf, Is.True);
            Assert.That(icon.sprite, Is.Not.Null);
        }
        else
            Assert.That(build.transform.Find("Label").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("×1"));
        RectTransform buildRect = build.GetComponent<RectTransform>();
        Assert.That(buildRect.rect.width, Is.EqualTo(88f).Within(1f));
        Assert.That(buildRect.rect.height, Is.EqualTo(104f).Within(1f));
        UITouchTooltip tooltip = build.GetComponent<UITouchTooltip>();
        Assert.That(tooltip, Is.Not.Null);
        PointerEventData pointer = new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left };
        tooltip.OnPointerDown(pointer);
        yield return new WaitForSecondsRealtime(.6f);
        tooltip.OnPointerUp(pointer);
        Transform help = ui.transform.Find("SafeAreaRoot/Tooltip");
        Assert.That(help.gameObject.activeInHierarchy, Is.True);
        Assert.That(help.Find("Text").GetComponent<TMPro.TMP_Text>().text, Does.Contain(structureName),
            "Holding the quantity action must explain the actual structure that it will build.");
    }

    private static void AssertCardGeometry(Transform card)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = card.GetComponent<RectTransform>();
        Assert.That(rect.rect.width, Is.GreaterThan(0f));
        foreach (string removed in new[] { "Effect", "Cost", "Stage", "Metadata" })
            Assert.That(card.Find(removed), Is.Null, removed + " does not belong in the compact facility card.");
        string[] names = { "Name", "Amount", "Build", "ToggleEnabled", "Deconstruct" };
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

