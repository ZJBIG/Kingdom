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
using TMPro;
using Object = UnityEngine.Object;

public sealed class ResourceProductionTreePlayModeTests
{
    private static readonly string evidenceRun = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    private string saveRoot;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;
    private KingdomUIRoot loadedUi;
    [SetUp] public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ResourceTree-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }
    [UnityTearDown] public IEnumerator CleanUp()
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
        Assert.That(Path.GetDirectoryName(saveRoot), Is.EqualTo(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"))));
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }
    [UnityTest]
    public IEnumerator ViewportResize_PreservesTheProductionTopBeforeClamping()
    {
        GameObject owner = new GameObject("ProductionResizeTest", typeof(RectTransform), typeof(UIResearchGraphGesture));
        GameObject child = new GameObject("Content", typeof(RectTransform));
        try
        {
            RectTransform viewport = owner.GetComponent<RectTransform>();
            RectTransform content = child.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = Vector2.zero;
            content.sizeDelta = new Vector2(500f, 1600f);
            viewport.sizeDelta = new Vector2(500f, 800f);
            UIResearchGraphGesture gesture = owner.GetComponent<UIResearchGraphGesture>();
            gesture.Initialize(viewport, content, true);
            gesture.RestoreView(new Vector2(0f, -800f), 1f);
            for (int resize = 0; resize < 3; resize++)
            {
                viewport.sizeDelta = new Vector2(500f, 1600f);
                gesture.RefreshLayoutBounds(false);
                Assert.That(content.anchoredPosition.y + content.rect.height, Is.EqualTo(viewport.rect.height).Within(.01f));
                viewport.sizeDelta = new Vector2(500f, 800f);
                gesture.RefreshLayoutBounds(false);
                Assert.That(content.anchoredPosition.y + content.rect.height, Is.EqualTo(viewport.rect.height).Within(.01f));
            }
            yield return null;
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [UnityTest]
    public IEnumerator IndependentProductionPage_UsesRealEntriesAndPreservesGraphWhileInspectingNodes()
    {
        yield return LoadProductionScene();
        KingdomUIRoot ui = loadedUi;
        SimulationManager.Instance.SetRunning(false);
        ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
        BuildingDef firstProducer = null;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            if (building is SectorBuildingDef || building.RequiredWorkshopUpgrades.Count > 0) continue;
            foreach (Pair<ResourceDef, ExpantaNum> output in building.ResourceGenerationRates)
                if (output.First == wood && output.Second > ExpantaNum.Zero &&
                    (firstProducer == null || building.TechLevel < firstProducer.TechLevel))
                { firstProducer = building; break; }
        }
        Assert.That(firstProducer, Is.Not.Null, "Real definitions provide an early wood producer without workshop prerequisites.");
        if (GameManager.Instance.State.TechLevel < firstProducer.TechLevel)
            GameManager.Instance.AdvanceTechLevelForEditor(firstProducer.TechLevel);
        foreach (ResearchDef research in firstProducer.RequiredResearch)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        Assert.That(BuildingManager.Instance.ArePrerequisitesMet(firstProducer, out _), Is.True,
            "The initial fixture unlocks only the first real producer's prerequisites.");
        ui.SetPage("Resources");
        ui.ShowResourceDetailsForEditor(wood);
        Transform detail = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent");
        detail.Find("ResourceProductionTree/Mode").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Transform page = ui.transform.Find("SafeAreaRoot/Content/PageHost/Production");
        RectTransform viewport = page.Find("GraphViewport") as RectTransform;
        RectTransform graph = viewport.Find("GraphContent") as RectTransform;
        UIResearchGraphGesture gesture = viewport.GetComponent<UIResearchGraphGesture>();
        Assert.That(page.gameObject.activeInHierarchy, Is.True, "The actual resource detail button enters the wide production page.");
        Assert.That(viewport.rect.width, Is.GreaterThan((detail.parent as RectTransform).rect.width));
        Assert.That(page.parent.GetComponent<ScrollRect>().enabled, Is.False, "The graph owns both axes.");
        Assert.That(gesture.IsInitialized, Is.True);
        Assert.That(graph.childCount, Is.GreaterThan(1));
        Assert.That(graph.localScale.x, Is.InRange(1f, 2f), "Initial framing enlarges a small graph within the fit limit.");
        float collapsedScale = graph.localScale.x;
        graph.GetChild(0).Find("Expand").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(graph.childCount, Is.EqualTo(1), "A collapsed root renders no reserved descendant nodes.");
        Assert.That(graph.localScale.x, Is.EqualTo(collapsedScale).Within(.001f), "Collapse preserves the current zoom.");
        yield return CaptureTree(ui.GetComponent<Canvas>(), "page-resource-collapsed");
        graph.GetChild(0).Find("Expand").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(detail.Find("Body").gameObject.activeSelf, Is.True, "The right detail remains readable.");
        BuildingDef lockedProducer = null;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            if (building is SectorBuildingDef || BuildingManager.Instance.ArePrerequisitesMet(building, out _)) continue;
            foreach (Pair<ResourceDef, ExpantaNum> output in building.ResourceGenerationRates)
                if (output.First == wood && output.Second > ExpantaNum.Zero)
                { lockedProducer = building; break; }
            if (lockedProducer != null) break;
        }
        Assert.That(lockedProducer, Is.Not.Null, "Wood includes a later-era producer for unlock coverage.");
        foreach (Transform node in graph)
        {
            Assert.That(node.Find("Label").GetComponent<TMP_Text>().text, Is.Not.EqualTo(lockedProducer.Label),
                "A future locked producer must not be shown before its prerequisites are met.");
            Assert.That(node.Find("Status").GetComponent<TMP_Text>().text, Does.Not.Contain("待解锁"));
            foreach (BuildingDef building in DataBase<BuildingDef>.All)
                if (node.Find("Label").GetComponent<TMP_Text>().text == building.Label)
                    Assert.That(BuildingManager.Instance.ArePrerequisitesMet(building, out _) ||
                        (BuildingManager.Instance.States.TryGetValue(building, out BuildingState existing) && existing.Amount > ExpantaNum.Zero),
                        Is.True, "Every visible building is unlocked or already owned: " + building.Id);
        }
        int guardedRootInstance = graph.GetChild(0).GetInstanceID();
        ui.ShowProductionTree(lockedProducer);
        yield return null;
        Assert.That(graph.GetChild(0).GetInstanceID(), Is.EqualTo(guardedRootInstance),
            "Opening a locked building directly cannot replace the current graph with hidden content.");
        Vector3 beforeUnlockRoot = viewport.InverseTransformPoint(graph.GetChild(0).position);
        float beforeUnlockScale = graph.localScale.x;
        PointerEventData unlockDrag = new(EventSystem.current) { button = PointerEventData.InputButton.Left,
            pointerId = 56, position = RectTransformUtility.WorldToScreenPoint(null, viewport.position), eligibleForClick = true };
        gesture.OnPointerDown(unlockDrag); gesture.OnBeginDrag(unlockDrag);
        Assert.That(gesture.IsDragging, Is.True);
        int rootDuringDrag = graph.GetChild(0).GetInstanceID();
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Ultra);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        WorkshopManager workshop = Object.FindObjectOfType<WorkshopManager>();
        Assert.That(workshop, Is.Not.Null, "The scene provides the workshop manager for real late-producer prerequisites.");
        List<string> purchasedUpgradeIds = new();
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
            purchasedUpgradeIds.Add(upgrade.Id);
        workshop.RestoreSaveDataForEditor(new SaveManager.WorkshopSaveData { PurchasedUpgradeIds = purchasedUpgradeIds });
        Assert.That(BuildingManager.Instance.ArePrerequisitesMet(lockedProducer, out _), Is.True);
        BuildingManager.Instance.GetState(lockedProducer).SetAmountForEditor(ExpantaNum.One);
        ui.RefreshProductionVisibilityForEditor();
        Assert.That(graph.GetChild(0).GetInstanceID(), Is.EqualTo(rootDuringDrag),
            "Unlocks must not replace captured nodes while a graph drag is active.");
        gesture.OnEndDrag(unlockDrag); gesture.OnPointerUp(unlockDrag);
        ui.RefreshProductionVisibilityForEditor();
        float deadline = Time.realtimeSinceStartup + 3f;
        Transform unlockedNode = null;
        while (unlockedNode == null && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            foreach (Transform node in graph)
                if (node.gameObject.activeSelf && node.Find("Label").GetComponent<TMP_Text>().text == lockedProducer.Label)
                { unlockedNode = node; break; }
        }
        Assert.That(unlockedNode, Is.Not.Null, "The unlocked owned producer appears after refresh.");
        Assert.That(unlockedNode.Find("Label").GetComponent<Button>().interactable, Is.True, "Newly unlocked producers retain their detail action.");
        Assert.That(graph.localScale.x, Is.EqualTo(beforeUnlockScale).Within(.001f), "An unlock preserves the user's zoom.");
        Assert.That(Vector3.Distance(viewport.InverseTransformPoint(graph.GetChild(0).position), beforeUnlockRoot), Is.LessThan(2f),
            "Visibility changes preserve the root's visual anchor.");

        Transform producer = graph.GetChild(1);
        TMP_Text producerLabel = producer.Find("Label").GetComponent<TMP_Text>();
        BuildingDef selected = null;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
            if (building.Label == producerLabel.text) { selected = building; break; }
        Assert.That(selected, Is.Not.Null);
        int rootInstance = graph.GetChild(0).GetInstanceID();
        producerLabel.GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(page.gameObject.activeInHierarchy, Is.True, "Inspecting a producer keeps the production graph visible.");
        Assert.That(graph.GetChild(0).GetInstanceID(), Is.EqualTo(rootInstance), "Inspecting details does not rebuild the graph.");
        RectTransform modeRect = detail.Find("ResourceProductionTree/Mode") as RectTransform;
        RectTransform bodyRect = detail.Find("Body") as RectTransform;
        Vector3[] modeCorners = new Vector3[4], bodyCorners = new Vector3[4];
        modeRect.GetWorldCorners(modeCorners); bodyRect.GetWorldCorners(bodyCorners);
        Assert.That(bodyCorners[1].y, Is.LessThanOrEqualTo(modeCorners[0].y), "Building detail text starts beneath its production-tree entry.");
        detail.Find("ResourceProductionTree/Mode").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(graph.GetChild(0).Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo(selected.Label));
        int selectedInputs = 0;
        foreach (Pair<ResourceDef, ExpantaNum> input in selected.ResourceConsumptionRates)
            if (input.First != null && input.Second > ExpantaNum.Zero) selectedInputs++;
        Assert.That(graph.childCount, Is.EqualTo(1 + selectedInputs + (selected.UpgradeTo != null &&
            BuildingManager.Instance.ArePrerequisitesMet(selected.UpgradeTo, out _) ? 1 : 0)),
            "The building root contains positive ongoing inputs and its upgrade, excluding construction costs.");
        Assert.That(graph.GetChild(0).Find("Status").GetComponent<TMP_Text>().text, Does.Not.Contain("基础产"));
        yield return CaptureTree(ui.GetComponent<Canvas>(), "page-building-inputs");

        BuildingDef multiInput = null;
        int positiveInputs = 0;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            if (building is SectorBuildingDef || !BuildingManager.Instance.ArePrerequisitesMet(building, out _)) continue;
            int count = 0;
            foreach (Pair<ResourceDef, ExpantaNum> input in building.ResourceConsumptionRates)
                if (input.First != null && input.Second > ExpantaNum.Zero) count++;
            if (count >= 2) { multiInput = building; positiveInputs = count; break; }
        }
        Assert.That(multiInput, Is.Not.Null, "Real definitions must provide a building with multiple ongoing inputs.");
        ui.ShowBuildingDetailsForEditor(multiInput);
        detail.Find("ResourceProductionTree/Mode").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(graph.GetChild(0).Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo(multiInput.Label));
        Assert.That(graph.childCount, Is.EqualTo(1 + positiveInputs + (multiInput.UpgradeTo != null &&
            BuildingManager.Instance.ArePrerequisitesMet(multiInput.UpgradeTo, out _) ? 1 : 0)),
            "One building root plus discrete positive ongoing inputs and its upgrade.");
        foreach (Pair<ResourceDef, ExpantaNum> input in multiInput.ResourceConsumptionRates)
        {
            if (input.First == null || input.Second <= ExpantaNum.Zero) continue;
            bool found = false;
            for (int i = 1; i < graph.childCount; i++)
                found |= graph.GetChild(i).Find("Label").GetComponent<TMP_Text>().text == input.First.Label;
            Assert.That(found, Is.True, "The actual building input is represented: " + input.First.Id);
        }
        yield return CaptureTree(ui.GetComponent<Canvas>(), "page-building-active-inputs");

        BuildingDef upgradeSource = null;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
            if (!(building is SectorBuildingDef) && building.UpgradeTo != null &&
                BuildingManager.Instance.ArePrerequisitesMet(building, out _) &&
                BuildingManager.Instance.ArePrerequisitesMet(building.UpgradeTo, out _)) { upgradeSource = building; break; }
        Assert.That(upgradeSource, Is.Not.Null, "Real definitions provide an upgrade relationship.");
        ui.ShowProductionTree(upgradeSource);
        yield return null;
        Transform upgradeNode = null;
        foreach (Transform node in graph)
            if (node.Find("Label").GetComponent<TMP_Text>().text == upgradeSource.UpgradeTo.Label)
            { upgradeNode = node; break; }
        upgradeNode.Find("Label").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(graph.GetChild(0).Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo(upgradeSource.Label),
            "Inspecting an upgrade preserves the original graph root.");
        yield return CaptureTree(ui.GetComponent<Canvas>(), "page-building-upgrades");

        ui.ShowProductionTree(DataBase<ResourceDef>.Find("Steel"));
        yield return null;
        for (int step = 0; step < 24; step++)
        {
            Button next = null;
            foreach (Transform node in graph)
            {
                Button candidate = node.Find("Expand").GetComponent<Button>();
                if (candidate.interactable && candidate.transform.Find("Label").GetComponent<TMP_Text>().text == "+")
                { next = candidate; break; }
            }
            if (next == null) break;
            next.onClick.Invoke();
            yield return null;
        }
        Assert.That(graph.rect.width, Is.GreaterThan(viewport.rect.width), "A real expanded Steel graph overflows horizontally.");
        TutorialSnapshot beforeAutomaticRefresh = ui.TutorialSnapshotForEditor;
        Assert.That(beforeAutomaticRefresh, Is.Not.Null);
        ui.RefreshAutomaticUIForEditor();
        Assert.That(ui.TutorialSnapshotForEditor, Is.SameAs(beforeAutomaticRefresh),
            "Automatic refresh reuses onboarding snapshots within the polling interval.");
        ui.RefreshUI();
        Assert.That(ui.TutorialSnapshotForEditor, Is.Not.SameAs(beforeAutomaticRefresh),
            "Explicit player-action refresh still evaluates onboarding immediately.");
        TutorialSnapshot beforeSaveSession = ui.TutorialSnapshotForEditor;
        TutorialManager.Current.ResetForNewGameForEditor();
        ui.RefreshAutomaticUIForEditor();
        Assert.That(ui.TutorialSnapshotForEditor, Is.Not.SameAs(beforeSaveSession),
            "A new save session invalidates cached onboarding immediately.");

        Assert.That(graph.rect.height, Is.GreaterThan(viewport.rect.height), "The same real graph overflows vertically.");
        for (int i = 0; i < graph.childCount; i++)
        {
            RectTransform a = graph.GetChild(i) as RectTransform;
            for (int j = i + 1; j < graph.childCount; j++)
            {
                RectTransform b = graph.GetChild(j) as RectTransform;
                Assert.That(new Rect(a.anchoredPosition, a.rect.size).Overlaps(new Rect(b.anchoredPosition, b.rect.size)), Is.False);
            }
        }
        gesture.RestoreView((viewport.rect.size - graph.rect.size * 1.25f) * .5f, 1.25f);
        Vector2 saved = graph.anchoredPosition;
        int nodeCount = graph.childCount;
        ui.SetPage("Resources"); yield return null;
        Assert.That(gesture.isActiveAndEnabled, Is.False, "A hidden production graph cannot handle input.");
        ResearchManager.Instance.GetState(DataBase<ResearchDef>.Find(ProgressionVisibilityRules.ProductionResearchId)).SetStatusForEditor(ResearchStatus.Completed);
        ui.SetPage("Production"); yield return null;
        Assert.That(Vector2.Distance(graph.anchoredPosition, saved), Is.LessThan(1f));
        Assert.That(graph.localScale.x, Is.EqualTo(1.25f).Within(.001f));
        Assert.That(graph.childCount, Is.EqualTo(nodeCount), "Page switching keeps the expanded graph cache.");

        UIResearchGraphDragForwarder forwarder = graph.GetChild(0).Find("Expand").GetComponent<UIResearchGraphDragForwarder>();
        Assert.That(forwarder, Is.Not.Null);
        PointerEventData pointer = new(EventSystem.current) { button = PointerEventData.InputButton.Left,
            pointerId = 53, eligibleForClick = true, position = RectTransformUtility.WorldToScreenPoint(null, viewport.position) };
        forwarder.OnPointerDown(pointer); forwarder.OnInitializePotentialDrag(pointer); forwarder.OnBeginDrag(pointer);
        pointer.position += new Vector2(-100, 100);
        forwarder.OnDrag(pointer); forwarder.OnEndDrag(pointer); forwarder.OnPointerUp(pointer);
        Assert.That(Vector2.Distance(graph.anchoredPosition, saved), Is.GreaterThan(1f));
        Assert.That(pointer.eligibleForClick, Is.False);
        Vector2 afterNodeDrag = graph.anchoredPosition;
        PointerEventData background = new(EventSystem.current) { button = PointerEventData.InputButton.Left,
            pointerId = 54, eligibleForClick = true, position = pointer.position };
        gesture.OnPointerDown(background); gesture.OnInitializePotentialDrag(background); gesture.OnBeginDrag(background);
        background.position += new Vector2(80, -80);
        gesture.OnDrag(background); gesture.OnEndDrag(background); gesture.OnPointerUp(background);
        Assert.That(Vector2.Distance(graph.anchoredPosition, afterNodeDrag), Is.GreaterThan(1f));

        // Collapse and re-expand a visible producer after zooming; both rebuilds retain its visual anchor.
        Transform branch = graph.GetChild(1);
        Button branchExpand = branch.Find("Expand").GetComponent<Button>();
        Assert.That(branchExpand.interactable, Is.True);
        Vector2 branchPosition = (branch as RectTransform).anchoredPosition;
        Vector2 branchSize = (branch as RectTransform).rect.size;
        gesture.RestoreView(new Vector2(viewport.rect.width * .5f - (branchPosition.x + branchSize.x * .5f) * 1.25f,
            viewport.rect.height * .5f - (graph.rect.height + branchPosition.y - branchSize.y * .5f) * 1.25f), 1.25f);
        Vector3 beforeExpand = viewport.InverseTransformPoint(branch.position);
        branchExpand.onClick.Invoke(); yield return null;
        Assert.That(graph.localScale.x, Is.EqualTo(1.25f).Within(.001f));
        graph.GetChild(1).Find("Expand").GetComponent<Button>().onClick.Invoke(); yield return null;
        Assert.That(Vector3.Distance(viewport.InverseTransformPoint(graph.GetChild(1).position), beforeExpand), Is.LessThan(2f),
            "Expanding after zoom preserves the clicked producer's visual anchor.");

        float zoomBeforeReset = graph.localScale.x;
        ui.transform.Find("SafeAreaRoot/Content/PageTool/ProductionTools/Reset").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(graph.localScale.x, Is.GreaterThan(0f).And.LessThan(zoomBeforeReset),
            "Reset returns the zoomed graph to its fitted view.");
        yield return CaptureTree(ui.GetComponent<Canvas>(), "page-steel-expanded");
    }

    [UnityTest]
    public IEnumerator SectorProduction_RespectsOccupationExplorationAndAuthoredIcons()
    {
        yield return LoadProductionScene();
        KingdomUIRoot ui = loadedUi;
        SimulationManager.Instance.SetRunning(false);
        SectorDef sector = DataBase<SectorDef>.Find("HeliosCore");
        Assert.That(sector, Is.Not.Null);
        SectorState state = GameManager.Instance.Sectors.GetState(sector);
        ui.ShowProductionTree(DataBase<ResourceDef>.Find("WoodLog"));
        Transform page = ui.transform.Find("SafeAreaRoot/Content/PageHost/Production");
        Transform graph = page.Find("GraphViewport/GraphContent");
        int originalRoot = graph.GetChild(0).GetInstanceID();
        state.SetUnlockedForEditor(false);
        state.SetOccupiedForEditor(false);
        ui.ShowProductionTree(sector);
        Assert.That(graph.GetChild(0).GetInstanceID(), Is.EqualTo(originalRoot), "Locked sectors cannot replace the graph.");

        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Ultra);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        List<string> upgrades = new();
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All) upgrades.Add(upgrade.Id);
        Object.FindObjectOfType<WorkshopManager>().RestoreSaveDataForEditor(
            new SaveManager.WorkshopSaveData { PurchasedUpgradeIds = upgrades });
        state.SetUnlockedForEditor(true);
        state.SetInteriorExplorationForEditor(ExpantaNum.Zero);
        ui.ShowProductionTree(sector);
        yield return null;
        Assert.That(graph.childCount, Is.EqualTo(1), "Unoccupied home sectors expose no local facilities.");
        Assert.That(graph.GetChild(0).Find("Icon").GetComponent<Image>().sprite, Is.EqualTo(sector.Icon));
        Transform detail = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent");
        Assert.That(detail.Find("ResourceProductionTree").gameObject.activeInHierarchy, Is.True);

        state.SetOccupiedForEditor(true);
        ui.RefreshProductionVisibilityForEditor();
        yield return null;
        BuildingDef special = null;
        int ordinary = 0;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
            if (building is SectorBuildingDef local && local.Sector == sector)
            {
                if (local.RequiresInteriorExploration)
                {
                    special = local;
                    Assert.That(FindProductionNode(graph, local.Label), Is.Null, "Exploration gates the special facility.");
                }
                else
                {
                    ordinary++;
                    Assert.That(FindProductionNode(graph, local.Label), Is.Not.Null);
                }
            }
        Assert.That(ordinary, Is.GreaterThan(0));
        Assert.That(special, Is.Not.Null);
        state.SetInteriorExplorationForEditor(ExpantaNum.One);
        ui.RefreshProductionVisibilityForEditor();
        yield return null;
        Assert.That(FindProductionNode(graph, special.Label), Is.Not.Null);
        foreach (Transform node in graph)
        {
            Image icon = node.Find("Icon").GetComponent<Image>();
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.preserveAspect, Is.True);
            Assert.That(icon.raycastTarget, Is.True, "Artwork is an explicit preview target.");
            Assert.That(icon.transform.Find("ArtworkFrame").gameObject.activeSelf, Is.True);
            Assert.That(icon.sprite.texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(icon.sprite.texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(icon.sprite.texture.width, Is.LessThanOrEqualTo(1024));
            RectTransform label = node.Find("Label") as RectTransform;
            RectTransform image = icon.rectTransform;
            Assert.That(label.offsetMin.y, Is.GreaterThan(image.offsetMax.y), "Title sits above the artwork.");
            RectTransform status = node.Find("Status") as RectTransform;
            Assert.That(status.offsetMax.y - ((RectTransform)node).rect.height, Is.LessThan(image.offsetMin.y), "Relationship sits below the artwork.");
        }
        int sectorRoot = graph.GetChild(0).GetInstanceID();
        Transform facility = FindProductionNode(graph, special.Label);
        Assert.That(facility.Find("Icon").GetComponent<Image>().sprite, Is.EqualTo(special.Icon));
        facility.Find("Label").GetComponent<Button>().onClick.Invoke();
        Assert.That(graph.GetChild(0).GetInstanceID(), Is.EqualTo(sectorRoot), "Inspecting a facility preserves the sector graph.");
        yield return CaptureTree(ui.GetComponent<Canvas>(), "sector-local-facilities", true);
        StoryIllustrationPreview preview = ui.GetComponentInChildren<StoryIllustrationPreview>(true);
        Assert.That(preview, Is.Not.Null, "Artwork reuses the authored illustration overlay.");
        RectTransform previewFrame = null;
        foreach (RectTransform candidate in preview.GetComponentsInChildren<RectTransform>(true))
            if (candidate.name == "ArtworkFrame") { previewFrame = candidate; break; }
        Assert.That(previewFrame, Is.Not.Null);
        facility.Find("Icon").GetComponent<Button>().onClick.Invoke();
        Assert.That(preview.IsOpen, Is.True);
        Assert.That(preview.Illustration, Is.EqualTo(special.Icon));
        AssertArtworkFrame(previewFrame);
        AssertArtworkFrame(facility.Find("Icon/ArtworkFrame") as RectTransform);
        Assert.That(graph.GetChild(0).GetInstanceID(), Is.EqualTo(sectorRoot));
        preview.ZoomAt(2f, RectTransformUtility.WorldToScreenPoint(null, preview.transform.position));
        Assert.That(preview.Zoom, Is.GreaterThan(1f));
        preview.Pan(new Vector2(20f, 20f));
        preview.ResetView();
        yield return CaptureTree(ui.GetComponent<Canvas>(), "building-artwork-preview", true);
        preview.Hide();
        graph.GetChild(0).Find("Icon").GetComponent<Button>().onClick.Invoke();
        Assert.That(preview.Illustration, Is.EqualTo(sector.Icon));
        yield return CaptureTree(ui.GetComponent<Canvas>(), "sector-artwork-preview", true);
        preview.Hide();
        detail.Find("ResourceProductionTree/Mode").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(graph.GetChild(0).Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo(special.Label));
        yield return CaptureTree(ui.GetComponent<Canvas>(), "sector-facility-inputs", true);

        BuildingDef old = DataBase<BuildingDef>.Find("Lumberyard");
        Assert.That(old.UpgradeTo, Is.Not.Null);
        BuildingManager.Instance.GetState(old).SetAmountForEditor(ExpantaNum.Zero);
        ui.ShowProductionTree(DataBase<ResourceDef>.Find("WoodLog"));
        yield return null;
        Assert.That(graph.GetChild(0).Find("Icon").GetComponent<Button>().enabled, Is.False,
            "Resource icons retain their normal appearance without disabled-button tinting.");
        Assert.That(FindProductionNode(graph, old.Label), Is.Not.Null,
            "An unlocked earlier producer remains visible even when a higher tier supersedes new construction.");
        BuildingManager.Instance.GetState(old).SetAmountForEditor(ExpantaNum.One);
        ui.RefreshProductionVisibilityForEditor();
        yield return null;
        Assert.That(FindProductionNode(graph, old.Label), Is.Not.Null, "Owned old producers stay visible.");
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            Transform node = FindProductionNode(graph, building.Label);
            if (node != null) Assert.That(BuildingManager.Instance.ArePrerequisitesMet(building, out _) ||
                (BuildingManager.Instance.States.TryGetValue(building, out BuildingState owned) && owned.Amount > ExpantaNum.Zero), Is.True);
        }
        yield return CaptureTree(ui.GetComponent<Canvas>(), "resource-retained-building-chain", true);
        SectorState firstSector = GameManager.Instance.Sectors.GetState(DataBase<SectorDef>.Find("DawnRing"));
        firstSector.SetUnlockedForEditor(true);
        firstSector.SetOccupiedForEditor(true);
        ui.SetPage("Sectors");
        yield return null;
        yield return CaptureTree(ui.GetComponent<Canvas>(), "sector-list-icons", true);
        Button sectorPreviewButton = null;
        foreach (Button button in ui.GetComponentsInChildren<Button>())
            if (button.name == "SectorIcon" && button.transform.parent.name == "SectorRow_DawnRing")
            { sectorPreviewButton = button; break; }
        Assert.That(sectorPreviewButton, Is.Not.Null);
        sectorPreviewButton.onClick.Invoke();
        Assert.That(preview.IsOpen, Is.True);
        Assert.That(preview.Illustration, Is.EqualTo(sectorPreviewButton.GetComponent<Image>().sprite));
        preview.Hide();
        sectorPreviewButton.transform.parent.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Transform facilityCardIcon = null;
        foreach (Image candidate in sectorPreviewButton.transform.parent.GetComponentsInChildren<Image>())
            if (candidate.name == "BuildingIcon")
            {
                facilityCardIcon = candidate.transform;
                AssertArtworkFrame(candidate.transform.Find("ArtworkFrame") as RectTransform);
                Assert.That(candidate.rectTransform.rect.width,
                    Is.GreaterThan(sectorPreviewButton.GetComponent<RectTransform>().rect.width * .9f),
                    "Sector facilities and sector portraits share the enlarged picture scale.");
                break;
            }
        Assert.That(facilityCardIcon, Is.Not.Null, "Expanded sectors expose the actual facility cards.");
        RectTransform menu = sectorPreviewButton.transform.parent.Find("SectorBuildingMenu") as RectTransform;
        Assert.That(menu, Is.Not.Null);
        RectTransform facilityCard = facilityCardIcon.parent as RectTransform;
        Vector3[] cardCorners = new Vector3[4];
        facilityCard.GetWorldCorners(cardCorners);
        foreach (Vector3 corner in cardCorners)
            Assert.That(menu.InverseTransformPoint(corner).y,
                Is.InRange(menu.rect.yMin - .5f, menu.rect.yMax + .5f),
                "Expanded facility cards stay inside their authored menu.");
        yield return CaptureTree(ui.GetComponent<Canvas>(), "sector-building-cards", true);
        ui.SetPage("Buildings");
        yield return null;
        Button buildingPreviewButton = null;
        foreach (Button button in ui.GetComponentsInChildren<Button>())
            if (button.name == "BuildingIcon") { buildingPreviewButton = button; break; }
        Assert.That(buildingPreviewButton, Is.Not.Null);
        RectTransform portrait = buildingPreviewButton.transform as RectTransform;
        AssertArtworkFrame(portrait.Find("ArtworkFrame") as RectTransform);
        RectTransform cardRect = portrait.parent as RectTransform;
        Vector3[] portraitCorners = new Vector3[4];
        (portrait.Find("ArtworkFrame") as RectTransform).GetWorldCorners(portraitCorners);
        foreach (Vector3 corner in portraitCorners)
        {
            Vector3 local = cardRect.InverseTransformPoint(corner);
            Assert.That(local.x, Is.InRange(cardRect.rect.xMin, cardRect.rect.xMax));
            Assert.That(local.y, Is.InRange(cardRect.rect.yMin, cardRect.rect.yMax), "The framed portrait stays inside the actual compact card.");
        }
        buildingPreviewButton.onClick.Invoke();
        Assert.That(preview.IsOpen, Is.True);
        Assert.That(preview.Illustration, Is.EqualTo(buildingPreviewButton.GetComponent<Image>().sprite));
        preview.Hide();
        yield return CaptureTree(ui.GetComponent<Canvas>(), "building-list-icons", true);
    }

    private static void AssertArtworkFrame(RectTransform frame)
    {
        Assert.That(frame, Is.Not.Null, "Artwork uses the shared authored mount.");
        RectTransform artwork = frame.parent as RectTransform;
        Image[] edges = frame.GetComponentsInChildren<Image>(true);
        Assert.That(edges.Length, Is.GreaterThan(0), "The mount must contain visible authored edges.");
        foreach (string name in new[] { "EdgeTop", "EdgeBottom", "EdgeLeft", "EdgeRight" })
        {
            Image edge = frame.Find(name).GetComponent<Image>();
            Assert.That(edge.color.grayscale, Is.GreaterThan(.35f), "All four sides form a readable closed frame.");
            Assert.That(Mathf.Min(edge.rectTransform.rect.width, edge.rectTransform.rect.height), Is.GreaterThanOrEqualTo(1.9f));
        }
        Vector3[] corners = new Vector3[4];
        foreach (Image edge in edges)
        {
            Assert.That(edge.enabled && edge.gameObject.activeSelf, Is.True);
            Assert.That(edge.raycastTarget, Is.False, "Decoration must not block preview clicks or graph dragging.");
            Assert.That(edge.color.a, Is.GreaterThan(.5f));
            edge.rectTransform.GetWorldCorners(corners);
            Vector3 lower = artwork.InverseTransformPoint(corners[0]);
            Vector3 upper = artwork.InverseTransformPoint(corners[2]);
            Rect image = artwork.rect;
            Assert.That(lower.x >= image.xMax - .1f || upper.x <= image.xMin + .1f ||
                        lower.y >= image.yMax - .1f || upper.y <= image.yMin + .1f, Is.True,
                edge.name + " must stay outside the illustration instead of covering it.");
        }
    }

    private static Transform FindProductionNode(Transform graph, string label)
    {
        foreach (Transform node in graph)
            if (node.gameObject.activeSelf && node.Find("Label").GetComponent<TMP_Text>().text == label) return node;
        return null;
    }

    [UnityTest]
    public IEnumerator SupersededProducer_RemainsInspectable_AndRefreshesItsFrame()
    {
        yield return LoadProductionScene();
        SimulationManager.Instance.SetRunning(false);
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Ultra);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        List<string> upgrades = new();
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All) upgrades.Add(upgrade.Id);
        Object.FindObjectOfType<WorkshopManager>().RestoreSaveDataForEditor(new SaveManager.WorkshopSaveData { PurchasedUpgradeIds = upgrades });

        BuildingManager buildings = BuildingManager.Instance;
        BuildingDef lower = null;
        ResearchDef higherPrerequisite = null;
        ResourceDef output = null;
        foreach (BuildingDef candidate in DataBase<BuildingDef>.All)
        {
            if (candidate is SectorBuildingDef || candidate.UpgradeTo == null || candidate.UpgradeTo.UpgradeTo != null ||
                !buildings.ArePrerequisitesMet(candidate, out _) || buildings.CanConstructNew(candidate)) continue;
            foreach (ResearchDef research in candidate.UpgradeTo.RequiredResearch)
            {
                bool shared = false;
                foreach (ResearchDef prerequisite in candidate.RequiredResearch) shared |= prerequisite == research;
                if (!shared) { higherPrerequisite = research; break; }
            }
            if (higherPrerequisite == null) continue;
            foreach (Pair<ResourceDef, ExpantaNum> rate in candidate.ResourceGenerationRates)
                if (rate.First != null && rate.Second > ExpantaNum.Zero) { output = rate.First; break; }
            if (output != null) { lower = candidate; break; }
            higherPrerequisite = null;
        }
        Assert.That(lower, Is.Not.Null, "Real definitions provide a superseded producer with a distinct upgrade prerequisite.");
        buildings.GetState(lower).SetAmountForEditor(ExpantaNum.Zero);
        buildings.GetState(lower.UpgradeTo).SetAmountForEditor(ExpantaNum.One);
        Assert.That(buildings.ShouldDisplay(lower), Is.False, "The ordinary building list still hides an empty superseded tier.");
        loadedUi.ShowProductionTree(output);
        yield return null;
        Transform graph = loadedUi.transform.Find("SafeAreaRoot/Content/PageHost/Production/GraphViewport/GraphContent");
        Transform lowerNode = FindProductionNode(graph, lower.Label);
        Assert.That(lowerNode, Is.Not.Null, "Production keeps the unlocked old source despite its empty stock.");
        Color supersededColor = lowerNode.GetComponent<Image>().color;
        Assert.That(Mathf.Abs(supersededColor.r - supersededColor.g), Is.LessThan(.001f));
        Assert.That(Mathf.Abs(supersededColor.g - supersededColor.b), Is.LessThan(.001f));
        Button details = lowerNode.Find("Label").GetComponent<Button>();
        Assert.That(details.interactable, Is.True);
        int originalNode = lowerNode.GetInstanceID();
        details.onClick.Invoke();
        Assert.That(loadedUi.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body")
            .GetComponent<TMP_Text>().text, Does.Contain(lower.Description));
        Assert.That(lowerNode.Find("Expand").GetComponent<Button>().interactable, Is.True);

        ResearchManager.Instance.GetState(higherPrerequisite).SetStatusForEditor(ResearchStatus.Available);
        Assert.That(buildings.CanConstructNew(lower), Is.True, "The lower tier becomes available when its upgrade is locked.");
        Assert.That(lowerNode.GetInstanceID(), Is.EqualTo(originalNode), "Inspecting the old source preserves the graph.");
        loadedUi.RefreshProductionVisibilityForEditor();
        lowerNode = FindProductionNode(graph, lower.Label);
        Assert.That(lowerNode, Is.Not.Null);
        Color normalColor = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceTreeRow").GetComponent<Image>().color;
        Assert.That(Vector4.Distance(lowerNode.GetComponent<Image>().color, normalColor), Is.LessThan(.001f));
        ResearchManager.Instance.GetState(higherPrerequisite).SetStatusForEditor(ResearchStatus.Completed);
        loadedUi.RefreshProductionVisibilityForEditor();
        lowerNode = FindProductionNode(graph, lower.Label);
        Assert.That(lowerNode, Is.Not.Null);
        Assert.That(Vector4.Distance(lowerNode.GetComponent<Image>().color, supersededColor), Is.LessThan(.001f));
    }

    private IEnumerator LoadProductionScene()
    {
        loadedUi = null;
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        yield return null;
        Scene scene = SceneManager.GetSceneByName("SampleScene");
        Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
        GameBootstrap bootstrap = null;
        for (int i = 0; i < 160; i++)
        {
            // Resolve only this loaded scene, never an object pending destruction from the previous test.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (loadedUi == null) loadedUi = root.GetComponentInChildren<KingdomUIRoot>(true);
                if (bootstrap == null) bootstrap = root.GetComponentInChildren<GameBootstrap>(true);
            }
            if (loadedUi != null && bootstrap != null && bootstrap.Completed) break;
            yield return null;
        }
        Assert.That(loadedUi != null, Is.True);
        Assert.That(bootstrap != null && bootstrap.Completed, Is.True, "The freshly loaded scene must finish bootstrap.");
        Assert.That(loadedUi.gameObject.scene, Is.EqualTo(scene));
        ResearchManager.Instance.GetState(DataBase<ResearchDef>.Find(ProgressionVisibilityRules.ProductionResearchId))
            .SetStatusForEditor(ResearchStatus.Completed);
        loadedUi.RefreshAutomaticUIForEditor();
    }

    private static IEnumerator CaptureTree(Canvas canvas, string name, bool dismissTutorial = false)
    {
        if (dismissTutorial)
        {
            HashSet<string> skipped = new();
            TutorialStageSnapshot stage;
            while ((stage = TutorialManager.Current?.EvaluateStage()) != null)
            {
                Assert.That(skipped.Add(stage.StageId), Is.True, "Skipping must advance the isolated fixture's tutorial.");
                TutorialManager.Current.SkipCurrentStage();
            }
            canvas.GetComponent<KingdomUIRoot>().RefreshOnboardingForEditor();
            yield return null;
        }
        Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null),
            "Canvas evidence requires a graphics device; run Unity without -nographics.");
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        GameObject cameraObject = new("ProductionTreeEvidenceCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
        RenderTexture target = null;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 100;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "experience-20261023", "production-pages", evidenceRun));
            Directory.CreateDirectory(folder);
            foreach (Vector2Int size in new[] { new Vector2Int(2640, 1200), new Vector2Int(1760, 800) })
            {
                target = new RenderTexture(size.x, size.y, 24);
                target.Create(); camera.targetTexture = target;
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Texture2D pixels = new(size.x, size.y, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    camera.Render();
                    AssertProductionCanvasVisibility(canvas, size);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(folder, name + "-final-" + size.x + "x" + size.y + ".png"), pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; Object.Destroy(pixels); }
                camera.targetTexture = null; target.Release(); Object.Destroy(target); target = null;
            }
        }
        finally
        {
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            Object.Destroy(cameraObject);
        }
    }

    private static void AssertProductionCanvasVisibility(Canvas canvas, Vector2Int size)
    {
        Transform page = canvas.transform.Find("SafeAreaRoot/Content/PageHost/Production");
        if (page == null || !page.gameObject.activeInHierarchy) return;
        RectTransform viewport = page.Find("GraphViewport") as RectTransform;
        Transform graph = viewport.Find("GraphContent");
        Assert.That(viewport.rect.width, Is.GreaterThan(0f), size + " actual screenshot viewport width");
        Assert.That(viewport.rect.height, Is.GreaterThan(0f), size + " actual screenshot viewport height");
        TMP_Text rootLabel = graph.GetChild(0).Find("Label").GetComponent<TMP_Text>();
        rootLabel.ForceMeshUpdate();
        if (rootLabel.canvasRenderer.cull)
            Debug.Log($"[ProductionClip] rootActive={graph.GetChild(0).gameObject.activeInHierarchy} root={graph.GetChild(0).GetInstanceID()} labelBounds={RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, rootLabel.rectTransform)} viewport={viewport.rect} graph={((RectTransform)graph).anchoredPosition} scale={graph.localScale} preview={canvas.GetComponentInChildren<StoryIllustrationPreview>(true)?.IsOpen}");
        Assert.That(rootLabel.canvasRenderer.cull, Is.False, size + " root label must survive real Canvas clipping");
        bool visibleGlyph = false;
        foreach (TMP_CharacterInfo character in rootLabel.textInfo.characterInfo) visibleGlyph |= character.isVisible;
        Assert.That(visibleGlyph, Is.True, size + " root text must contain visible glyphs");
        int visibleNodes = 0;
        Vector3[] corners = new Vector3[4];
        foreach (Transform node in graph)
        {
            Image surface = node.GetComponent<Image>();
            TMP_Text label = node.Find("Label").GetComponent<TMP_Text>();
            label.ForceMeshUpdate();
            Assert.That(label.isTextOverflowing, Is.False, size + " production name must fit: " + label.text);
            (node as RectTransform).GetWorldCorners(corners);
            Vector3 min = viewport.InverseTransformPoint(corners[0]), max = viewport.InverseTransformPoint(corners[2]);
            Rect nodeBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (!surface.canvasRenderer.cull && !label.canvasRenderer.cull && nodeBounds.Overlaps(viewport.rect)) visibleNodes++;
        }
        Assert.That(visibleNodes, Is.GreaterThan(0), size + " actual screenshot must render production nodes, not only connection lines");
        Debug.Log("[ProductionPageScreenshot] size=" + size + " viewport=" + viewport.rect.size + " visibleNodes=" + visibleNodes);
        UIResearchGraphGesture gesture = viewport.GetComponent<UIResearchGraphGesture>();
        Debug.Log("[ProductionPageEvidence] size=" + size + " viewport=" + viewport.rect.size + " graph=" + (graph as RectTransform).rect.size +
            " nodes=" + graph.childCount + " horizontal=" + gesture.CanPanHorizontal + " vertical=" + gesture.CanPanVertical);
    }
}
