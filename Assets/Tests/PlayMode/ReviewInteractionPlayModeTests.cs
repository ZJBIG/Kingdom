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

    [UnityTest, Category("Performance")]
    public IEnumerator UiHotPaths_RecordMaxCardsAndResourceDetails()
    {
        yield return Load();
        GameManager.Instance.State.AdvanceTechLevelForEditor(TechLevel.Archotech);
        GameManager.Instance.State.AdjustTerritoryTotalForEditor(new ExpantaNum(1e9));
        foreach (ResearchState research in ResearchManager.Instance.States.Values)
            research.SetStatusForEditor(ResearchStatus.Completed);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1e9));
        foreach (BuildingState building in BuildingManager.Instance.States.Values)
            BuildingManager.Instance.SetAmountAndRatesForEditor(building, ExpantaNum.One);
        ui.SetPage("Buildings");
        Transform controls = ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls");
        controls.Find("Quantity_Max").GetComponent<Button>().onClick.Invoke();
        yield return null;
        RecordUiMedian("building-max-cards", ui.RefreshLiveCardValuesForEditor, 10);
        ui.SetPage("Resources");
        ui.ShowResourceDetailsForEditor(DataBase<ResourceDef>.Find("WoodLog"));
        yield return null;
        RecordUiMedian("resource-detail-sources", ui.RefreshSelectedResourceDetailsForEditor, 20);
    }

    private static void RecordUiMedian(string name, Action refresh, int iterations)
    {
        for (int i = 0; i < 5; i++) refresh();
        double[] samples = new double[7];
        for (int batch = 0; batch < samples.Length; batch++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) refresh();
            samples[batch] = watch.Elapsed.TotalMilliseconds / iterations;
        }
        Array.Sort(samples);
        Debug.Log($"[HotPathPerf2] {name} medianMs={samples[3]:F6} iterations={iterations} batches={samples.Length} controlledFixture=true");
    }

    [UnityTest]
    public IEnumerator ResourceDetails_RefreshLiveQuantitiesEfficiencyAndSourceAffordability()
    {
        yield return Load();
        GameManager.Instance.State.AdvanceTechLevelForEditor(TechLevel.Archotech);
        GameManager.Instance.State.AdjustTerritoryTotalForEditor(new ExpantaNum(1e9));
        foreach (ResearchState research in ResearchManager.Instance.States.Values)
            research.SetStatusForEditor(ResearchStatus.Completed);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1e9));
        BuildingDef building = DataBase<BuildingDef>.Find("Lumberyard");
        BuildingManager manager = BuildingManager.Instance;
        BuildingState state = manager.GetState(building);
        manager.SetAmountAndRatesForEditor(state, ExpantaNum.One);
        state.SetEfficiencyForEditor(ExpantaNum.One);
        ui.SetPage("Resources");
        ui.ShowResourceDetailsForEditor(DataBase<ResourceDef>.Find("WoodLog"));
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        string first = body.text;
        manager.SetAmountAndRatesForEditor(state, new ExpantaNum(2));
        ui.RefreshSelectedResourceDetailsForEditor();
        Assert.That(body.text, Is.Not.EqualTo(first), "Cached relationships must still read current quantities.");
        string producing = body.text;
        state.SetEfficiencyForEditor(ExpantaNum.Zero);
        ui.RefreshSelectedResourceDetailsForEditor();
        Assert.That(body.text, Is.Not.EqualTo(producing), "A stopped source must replace its live production breakdown.");
        manager.SetAmountAndRatesForEditor(state, ExpantaNum.Zero);
        ui.RefreshSelectedResourceDetailsForEditor();
        Assert.That(manager.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.None));
        string affordable = body.text;
        foreach (var cost in building.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, ExpantaNum.Zero);
        Assert.That(manager.GetBuildFailure(building, ExpantaNum.One), Is.Not.EqualTo(BuildFailure.None));
        ui.RefreshSelectedResourceDetailsForEditor();
        Assert.That(body.text, Is.Not.EqualTo(affordable), "Unbuilt source affordability must not be cached.");
        body.ForceMeshUpdate(true);
        bool linked = false;
        for (int i = 0; i < body.textInfo.linkCount; i++)
            linked |= body.textInfo.linkInfo[i].GetLinkID() == "building:" + building.Id;
        Assert.That(linked, Is.True);
        float wideWidth = body.rectTransform.rect.width;
        float wideHeight = body.rectTransform.rect.height;
        RectTransform viewport = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport") as RectTransform;
        viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, wideWidth * .5f + 68f);
        Canvas.ForceUpdateCanvases();
        ui.RefreshSelectedResourceDetailsForEditor();
        Assert.That(body.rectTransform.rect.width, Is.LessThan(wideWidth));
        float expectedHeight = Mathf.Max(96f, body.GetPreferredValues(body.text,
            Mathf.Max(320f, body.rectTransform.rect.width), 1000f).y + 20f);
        Assert.That(body.rectTransform.rect.height, Is.EqualTo(expectedHeight).Within(1f),
            "The same resource text must be measured again when its available width changes.");
        Assert.That(body.rectTransform.rect.height, Is.GreaterThan(wideHeight),
            "The narrower fixture must wrap the resource text onto more lines.");
    }

    [UnityTest]
    public IEnumerator StableBuildingButtons_DoNotDirtyTheirGraphicVertices()
    {
        yield return Load();
        ui.SetPage("Buildings");
        ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls/Quantity_One")
            .GetComponent<Button>().onClick.Invoke();
        ui.RefreshLiveCardValuesForEditor();
        Canvas.ForceUpdateCanvases();
        yield return null;
        var graphics = new List<Graphic>();
        foreach (Outline outline in ui.GetComponentsInChildren<Outline>())
            if (outline.name == "BuildButton" || outline.name == "DeconstructButton")
                graphics.Add(outline.GetComponent<Graphic>());
        Assert.That(graphics, Is.Not.Empty, "The fixture must observe actual active building action graphics.");
        int dirties = 0;
        UnityEngine.Events.UnityAction observed = () => dirties++;
        foreach (Graphic graphic in graphics) graphic.RegisterDirtyVerticesCallback(observed);
        try
        {
            ui.RefreshLiveCardValuesForEditor();
            ui.RefreshLiveCardValuesForEditor();
        }
        finally
        {
            foreach (Graphic graphic in graphics) graphic.UnregisterDirtyVerticesCallback(observed);
        }
        Debug.Log($"[HotPathPerf2] stable-action-outline dirtyCallbacks={dirties} graphics={graphics.Count} refreshes=2");
        Assert.That(dirties, Is.Zero, "Unchanged button outlines must not request mesh rebuilds.");
    }

    [UnityTest, Category("Performance")]
    public IEnumerator MobileCadence_RecordsBoundedSixtyThirtyFrameComparison()
    {
        int previousFrameRate = Application.targetFrameRate;
        int previousVSync = QualitySettings.vSyncCount;
        try
        {
            yield return Load();
            Assert.That(Object.FindObjectOfType<GameBootstrap>()?.Completed, Is.True);
            ui.SetPage("Overview");
            Object.FindObjectOfType<MusicManager>()?.Stop();
            QualitySettings.vSyncCount = 0;
            var rows = new List<string>
            {
                "targetFps,frames,seconds,actualFps,mainThreadAllocatedBytes,gc0,gc1,gc2,activeObjectsBefore,activeObjectsAfter,audioClipsBefore,audioClipsAfter,totalMemoryBefore,totalMemoryAfter,monoMemoryBefore,monoMemoryAfter"
            };
            foreach (int target in new[] { 60, 30 })
            {
                Application.targetFrameRate = target;
                yield return new WaitForSecondsRealtime(1f);
                int objectsBefore = Object.FindObjectsOfType<GameObject>().Length;
                int clipsBefore = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
                long totalBefore = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                long monoBefore = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                int gc0 = GC.CollectionCount(0);
                int gc1 = GC.CollectionCount(1);
                int gc2 = GC.CollectionCount(2);
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                double startedAt = Time.realtimeSinceStartupAsDouble;
                int frames = 0;
                do
                {
                    yield return null;
                    frames++;
                } while (Time.realtimeSinceStartupAsDouble - startedAt < 3d);
                double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                string allocated = allocatedBefore > 0L ? (allocatedAfter - allocatedBefore).ToString(System.Globalization.CultureInfo.InvariantCulture) : "NA";
                int gc0Delta = GC.CollectionCount(0) - gc0;
                int gc1Delta = GC.CollectionCount(1) - gc1;
                int gc2Delta = GC.CollectionCount(2) - gc2;
                long totalAfter = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                long monoAfter = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                int objectsAfter = Object.FindObjectsOfType<GameObject>().Length;
                int clipsAfter = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
                Assert.That(SimulationManager.Instance.IsRunning, Is.False);
                Assert.That(Application.targetFrameRate, Is.EqualTo(target), "The requested render cap must remain selected throughout this sample.");
                rows.Add(FormattableString.Invariant($"{target},{frames},{elapsed:F4},{frames / elapsed:F2},{allocated},{gc0Delta},{gc1Delta},{gc2Delta},{objectsBefore},{objectsAfter},{clipsBefore},{clipsAfter},{totalBefore},{totalAfter},{monoBefore},{monoAfter}"));
            }
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "startup-mobile-20261103"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "frame-cadence-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
            File.WriteAllLines(path, rows);
            var saves = new List<string> { "sample,totalSaveMilliseconds" };
            for (int sample = 0; sample < 3; sample++)
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                Assert.That(SaveManager.Instance.SaveNow(true), Is.True);
                double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                saves.Add(FormattableString.Invariant($"{sample},{milliseconds:F3}"));
            }
            File.WriteAllLines(Path.Combine(directory, "save-cost-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv"), saves);
            Debug.Log("[KingdomPerf] Same-scene Overview, paused simulation, music stopped; Editor frame/allocation observations, not device power or leak acceptance. " + path + "\n" + string.Join("\n", rows));
        }
        finally
        {
            Application.targetFrameRate = previousFrameRate;
            QualitySettings.vSyncCount = previousVSync;
        }
    }

    [UnityTest]
    public IEnumerator BuildingDescription_RemainsVisibleInBothDetailModes()
    {
        yield return Load();
        BuildingDef building = DataBase<BuildingDef>.Find("WoodHouse");
        Assert.That(building.Description, Is.Not.Null.And.Not.Empty);
        ui.SetPage("Buildings");
        Toggle details = ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls/ShowDetails").GetComponent<Toggle>();
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        ui.ShowBuildingDetailsForEditor(building);
        foreach (bool expanded in new[] { false, true })
        {
            details.isOn = expanded;
            ui.RefreshLiveCardValuesForEditor();
            Assert.That(body.text, Does.Contain(building.Description));
        }
    }

    [UnityTest]
    public IEnumerator BuildingQuantity_UpdatesBatchAvailabilityAndClipsCustomInput()
    {
        yield return Load();
        BuildingDef building = DataBase<BuildingDef>.Find("WoodHouse");
        BuildingManager buildings = BuildingManager.Instance;
        buildings.SetAmountAndRatesForEditor(buildings.States[building], ExpantaNum.Zero);
        foreach (var cost in building.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, cost.Second * new ExpantaNum(1.5));
        Assert.That(buildings.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.None));
        Assert.That(buildings.GetBuildFailure(building, new ExpantaNum(10)), Is.Not.EqualTo(BuildFailure.None));
        ui.SetPage("Buildings");
        Transform controls = ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls");
        controls.Find("ShowDetails").GetComponent<Toggle>().isOn = false;
        controls.Find("Quantity_One").GetComponent<Button>().onClick.Invoke();
        ui.ShowBuildingDetailsForEditor(building);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        string singleStatus = body.text;
        controls.Find("Quantity_Ten").GetComponent<Button>().onClick.Invoke();
        string batchStatus = body.text;
        Assert.That(batchStatus, Is.Not.EqualTo(singleStatus), "The compact availability text must explain the same selected quantity as the disabled purchase button.");
        Assert.That(ui.GetDeconstructButtonForEditor(building).transform.parent.Find("BuildButton").GetComponent<Button>().interactable, Is.False);
        controls.Find("Quantity_One").GetComponent<Button>().onClick.Invoke();
        Assert.That(body.text, Is.EqualTo(singleStatus));

        Button action = ui.GetDeconstructButtonForEditor(building).transform.parent.Find("BuildButton").GetComponent<Button>();
        controls.Find("Quantity_Max").GetComponent<Button>().onClick.Invoke();
        ExpantaNum maximum = buildings.GetMaxBuildable(building, ExpantaNum.PositiveInfinity);
        Assert.That(maximum >= ExpantaNum.One, Is.True);
        Assert.That(action.interactable, Is.True);
        Assert.That(action.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("×" + maximum.ToGameString()));
        controls.Find("Quantity_Custom").GetComponent<Button>().onClick.Invoke();
        TMP_InputField custom = controls.Find("CustomQuantityInput").GetComponent<TMP_InputField>();
        custom.text = "100";
        Assert.That(action.interactable, Is.True);
        Assert.That(action.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("×" + maximum.ToGameString()));
        foreach (var cost in building.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, ExpantaNum.Zero);
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(action.interactable, Is.False, "Custom quantity keeps the minimum label without enabling an unaffordable action.");
        controls.Find("Quantity_Max").GetComponent<Button>().onClick.Invoke();
        Assert.That(action.interactable, Is.False, "Max with no affordable building must remain unavailable.");

        TMP_InputField input = controls.Find("CustomQuantityInput").GetComponent<TMP_InputField>();
        AssertTextVisuallyCentered((TMP_Text)input.placeholder);
        input.SetTextWithoutNotify("12345");
        yield return null;
        AssertTextVisuallyCentered(input.textComponent);
        RectMask2D mask = input.GetComponent<RectMask2D>();
        Assert.That(mask, Is.Not.Null, "The quantity field owns its clipping boundary.");
        input.SetTextWithoutNotify(new string('9', 100));
        Canvas.ForceUpdateCanvases();
        yield return null;
        Assert.That(input.textComponent.canvasRenderer.hasRectClipping, Is.True,
            "Long quantities must be clipped before reaching neighboring controls.");
        input.SetTextWithoutNotify(string.Empty);
    }

    [UnityTest]
    public IEnumerator BuildingCapacityShortages_ShowResolvedIconsAndSelectedBatchDeficits()
    {
        yield return Load();
        BuildingDef building = DataBase<BuildingDef>.Find("Quarry");
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        BuildingManager manager = BuildingManager.Instance;
        manager.SetAmountAndRatesForEditor(manager.States[building], ExpantaNum.Zero);
        GameState game = GameManager.Instance.State;
        game.RestorePopulationForEditor(ExpantaNum.Zero);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1000000));
        game.CommitConstructionForEditor(game.AvailableTerritory);
        Assert.That(manager.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.SpaceInsufficient));
        ui.SetPage("Buildings");
        Transform controls = ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls");
        TMP_InputField quantityInput = controls.Find("CustomQuantityInput").GetComponent<TMP_InputField>();
        Assert.That(quantityInput.GetComponent<Image>().sprite,
            Is.SameAs(Resources.Load<Sprite>("Art/UI/Buttons/ControlMetal")), "Quantity entry uses the authored control frame.");
        controls.Find("Quantity_One").GetComponent<Button>().onClick.Invoke();
        ui.RefreshLiveCardValuesForEditor();
        TMP_Text effect = ui.GetDeconstructButtonForEditor(building).transform.parent.Find("Effect").GetComponent<TMP_Text>();
        effect.ForceMeshUpdate(true, true);
        AssertBuildingStatusIcons(effect, 3);
        CaptureBuildingStatus("land-shortage");
        yield return null;
        string single = effect.text;
        ui.ShowBuildingDetailsForEditor(building);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        string singleDetails = body.text;
        Assert.That(singleDetails, Does.Contain("<sprite=\"BuildingStatusIcons\" index=3> 需求 " + building.SpaceCost.ToGameString()
            + "，缺少 " + building.SpaceCost.ToGameString()));
        Transform card = effect.transform.parent;
        Assert.That(card.Find("Metadata").gameObject.activeSelf, Is.False);
        TMP_Text title = card.Find("Label").GetComponent<TMP_Text>();
        title.ForceMeshUpdate();
        AssertTextVisuallyCentered(title, false);
        Assert.That(title.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Left));
        TMP_Text hint = (TMP_Text)quantityInput.placeholder;
        hint.ForceMeshUpdate();
        AssertTextVisuallyCentered(hint);
        Assert.That(title.fontSize, Is.GreaterThanOrEqualTo(30));
        Assert.That(title.isTextTruncated, Is.False, "The building name must remain readable in its enlarged area.");
        controls.Find("Quantity_Ten").GetComponent<Button>().onClick.Invoke();
        ui.RefreshLiveCardValuesForEditor();
        Assert.That(effect.text, Is.EqualTo(single), "The icon represents the same shortage at either quantity.");
        Assert.That(body.text, Is.Not.EqualTo(singleDetails));
        Assert.That(body.text, Does.Contain((building.SpaceCost * 10).ToGameString()), "Details retain the selected batch deficit.");

        foreach (var cost in building.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, ExpantaNum.Zero);
        ui.RefreshLiveCardValuesForEditor();
        AssertBuildingStatusIcons(effect, 3);
        ui.ShowBuildingDetailsForEditor(building);
        AssertMaterialShortageRows(building);
        CaptureBuildingStatus("all-shortages");
        yield return null;
        foreach (var cost in building.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, new ExpantaNum(1000000));

        game.AdjustTerritoryTotalForEditor(new ExpantaNum(10000));
        controls.Find("Quantity_One").GetComponent<Button>().onClick.Invoke();
        Assert.That(manager.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.None),
            "Zero labor supply must not block construction.");
        Assert.That(manager.TryBuild(building, ExpantaNum.One, out BuildFailure laborBuildFailure),
            Is.True, laborBuildFailure.ToString());
        ui.RefreshLiveCardValuesForEditor();
        effect.ForceMeshUpdate(true, true);
        Assert.That(effect.text, Is.Empty, "Labor shortage is an operating rate, not a construction warning.");
        CaptureBuildingStatus("labor-operating-rate");
        yield return null;
        ui.ShowBuildingDetailsForEditor(building);
        Assert.That(manager.LaborOperatingRatio, Is.LessThanOrEqualTo(ExpantaNum.Zero));
        Assert.That(body.text, Does.Not.Contain("全局开工率"), "The actual efficiency replaces the separate global ratio line.");
        Assert.That(body.text, Does.Contain("<sprite=\"HudProductivity\" index=0> 需求 " + manager.UsedProductivity.ToGameString() +
            "，缺少 " + ExpantaNum.Max(ExpantaNum.Zero, manager.UsedProductivity - manager.TotalProductivity).ToGameString()));
        Assert.That(body.text, Does.Not.Contain("可供").And.Not.Contain("建筑总需求"));
        body.ForceMeshUpdate(true, true);
        var detailIcons = new HashSet<int>();
        var detailIconAssets = new HashSet<string>();
        for (int i = 0; i < body.textInfo.characterCount; i++)
            if (body.textInfo.characterInfo[i].elementType == TMP_TextElementType.Sprite)
            {
                detailIcons.Add(body.textInfo.characterInfo[i].spriteIndex);
                detailIconAssets.Add(body.textInfo.characterInfo[i].spriteAsset.name);
            }
        Assert.That(detailIcons, Does.Contain(0), "Available land uses the neutral land icon.");
        Assert.That(detailIconAssets, Does.Contain("HudProductivity"), "Productivity uses its authored HUD category glyph.");
        Assert.That(detailIcons, Has.No.Member(4), "The old hard-block warning must not return.");

        game.Population.AdjustPopulationCapacityForEditor(new ExpantaNum(100));
        game.RestorePopulationForEditor(new ExpantaNum(100));
        Assert.That(manager.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.None));
        ui.RefreshLiveCardValuesForEditor();
        effect.ForceMeshUpdate(true, true);
        Assert.That(effect.text, Is.Empty, "Material status is displayed in the right detail panel.");
        ui.ShowBuildingDetailsForEditor(building);
        Assert.That(body.text, Does.Contain("<sprite=\"HudTerritory\" index=0> 需求 " + building.SpaceCost.ToGameString() + "，缺少 0"));
        Assert.That(body.text, Does.Not.Contain("全局开工率"), "The actual efficiency replaces the separate global ratio line.");
        CaptureBuildingStatus("ready");
        yield return null;
        foreach (var cost in building.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, ExpantaNum.Zero);
        Assert.That(manager.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.ResourceInsufficient));
        ui.RefreshLiveCardValuesForEditor();
        effect.ForceMeshUpdate(true, true);
        Assert.That(effect.text, Is.Empty, "Material shortage belongs only in the selected detail panel.");
        ui.ShowBuildingDetailsForEditor(building);
        AssertMaterialShortageRows(building);
        CaptureBuildingStatus("materials-shortage");
        yield return null;
    }

    private void AssertMaterialShortageRows(BuildingDef building)
    {
        Transform content = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/BuildingRequirements/RequirementContent");
        Assert.That(content != null && content.gameObject.activeInHierarchy, Is.True);
        Assert.That(building.ResourceRequirements.Count, Is.GreaterThan(0));
        foreach (var cost in building.ResourceRequirements)
        {
            TMP_Text amount = null;
            // Old rows await deferred destruction; inspect the newest matching row.
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform row = content.GetChild(i);
                if (row.name.StartsWith("Requirement_", StringComparison.Ordinal) &&
                    row.Find("Label").GetComponent<TMP_Text>().text == cost.First.Label)
                { amount = row.Find("Amount").GetComponent<TMP_Text>(); break; }
            }
            Assert.That(amount, Is.Not.Null, cost.First.Id);
            ExpantaNum owned = ResourceManager.Instance.GetAmount(cost.First);
            amount.ForceMeshUpdate();
            string parsed = amount.GetParsedText();
            Assert.That(parsed, Does.EndWith(" (" + owned.ToGameString() + ")"),
                "The material row must show the current inventory beside its demand.");
            int inventoryStart = parsed.IndexOf(" (", StringComparison.Ordinal);
            ExpantaNum demand = parsed.Substring(0, inventoryStart);
            Assert.That(demand, Is.GreaterThan(owned), "The displayed material requirement must expose the actual shortage.");
        }
    }

    private static void AssertBuildingStatusIcons(TMP_Text effect, params int[] indices)
    {
        effect.ForceMeshUpdate(true, true);
        Assert.That(effect.textInfo.characterCount, Is.EqualTo(indices.Length), "Show every shortage once, with no adjacent text.");
        for (int i = 0; i < indices.Length; i++)
        {
            TMP_CharacterInfo glyph = effect.textInfo.characterInfo[i];
            Assert.That(glyph.elementType, Is.EqualTo(TMP_TextElementType.Sprite));
            Assert.That(glyph.spriteIndex, Is.EqualTo(indices[i]), "Sprite indices are the authored status protocol.");
            Assert.That(glyph.topRight.y - glyph.bottomLeft.y,
                Is.GreaterThanOrEqualTo(effect.fontSize * .8f),
                "The authored status symbol must retain a readable glyph height within its text slot.");
            Assert.That(glyph.bottomLeft.x, Is.GreaterThanOrEqualTo(effect.rectTransform.rect.xMin));
            Assert.That(glyph.topRight.x, Is.LessThanOrEqualTo(effect.rectTransform.rect.xMax));
        }
    }

    private static void AssertTextVisuallyCentered(TMP_Text text, bool horizontal = true)
    {
        text.ForceMeshUpdate();
        Vector3 center = text.textBounds.center;
        Vector2 slot = text.rectTransform.rect.center;
        if (horizontal) Assert.That(Mathf.Abs(center.x - slot.x), Is.LessThan(text.fontSize * .12f), text.name + " horizontal glyph center");
        Assert.That(Mathf.Abs(center.y - slot.y), Is.LessThan(text.fontSize * .12f), text.name + " vertical glyph center");
    }

    [UnityTest]
    public IEnumerator BuildingDetails_RefreshCapacityAndUseDiscountedMaterialCosts()
    {
        yield return Load();
        BuildingDef building = DataBase<BuildingDef>.Find("Quarry");
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        BuildingManager manager = BuildingManager.Instance;
        GameState game = GameManager.Instance.State;
        game.RestorePopulationForEditor(ExpantaNum.Zero);
        game.CommitConstructionForEditor(game.AvailableTerritory);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1000000));
        ui.SetPage("Buildings");
        ui.ShowBuildingDetailsForEditor(building);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        ui.RefreshAutomaticUIForEditor();
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(body.text, Does.Contain("缺少"));
        game.AdjustTerritoryTotalForEditor(new ExpantaNum(10000));
        game.Population.AdjustPopulationCapacityForEditor(new ExpantaNum(100));
        game.RestorePopulationForEditor(new ExpantaNum(100));
        ui.RefreshAutomaticUIForEditor();
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(body.text, Does.Contain("<sprite=\"HudTerritory\" index=0> 需求 " + building.SpaceCost.ToGameString() + "，缺少 0"));
        Assert.That(body.text, Does.Not.Contain("全局开工率"), "The actual efficiency replaces the separate global ratio line.");

        var drafting = new WorkshopUpgradeState(DataBase<WorkshopUpgradeDef>.Find("DraftingTables"));
        drafting.SetPurchasedForEditor(true);
        ProgressionModifierManager.Rebuild(null, new[] { drafting });
        ui.RefreshAutomaticUIForEditor();
        yield return new WaitForSecondsRealtime(.35f);
        // Compare the displayed requirement with the transaction's actual payment, not a duplicated formula.
        var before = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (var cost in building.ResourceRequirements)
            before[cost.First] = ResourceManager.Instance.GetAmount(cost.First);
        var displayed = new List<string>();
        foreach (TMP_Text label in ui.transform.Find("SafeAreaRoot/DetailPanel").GetComponentsInChildren<TMP_Text>())
            displayed.Add(label.text);
        Assert.That(manager.TryBuild(building, ExpantaNum.One, out BuildFailure failure), Is.True, failure.ToString());
        foreach (var cost in building.ResourceRequirements)
        {
            string paid = (before[cost.First] - ResourceManager.Instance.GetAmount(cost.First)).ToGameString();
            Assert.That(displayed.Exists(value => value.Contains(paid)), Is.True, "Details must show the discounted transaction payment: " + paid);
        }
    }

    private void CaptureBuildingStatus(string status)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data",
            "building-detail-compact-20261026", "review-status-captures"));
        Directory.CreateDirectory(directory);
        Canvas canvas = ui.GetComponent<Canvas>();
        RenderMode mode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        var host = new GameObject("BuildingStatusEvidenceCamera");
        Camera camera = host.AddComponent<Camera>();
        var target = new RenderTexture(1760, 800, 24);
        var pixels = new Texture2D(1760, 800, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1000;
            target.Create();
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory, status + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            canvas.renderMode = mode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = distance;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(pixels);
            Object.Destroy(host);
        }
    }

    [UnityTest]
    public IEnumerator OwnedBuildingEfficiency_RemainsInDetailsWhileTheCardShowsPurchaseStatus()
    {
        yield return Load();
        BuildingDef building = DataBase<BuildingDef>.Find("WoodHouse");
        BuildingState state = BuildingManager.Instance.States[building];
        BuildingManager.Instance.SetAmountAndRatesForEditor(state, ExpantaNum.One);
        foreach (var cost in building.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, ExpantaNum.Zero);
        state.SetEfficiencyForEditor(ExpantaNum.One);
        ui.SetPage("Buildings");
        ui.RefreshLiveCardValuesForEditor();
        Transform row = ui.GetDeconstructButtonForEditor(building).transform.parent;
        TMP_Text effect = row.Find("Effect").GetComponent<TMP_Text>();
        Assert.That(row.Find("BuildButton").GetComponent<Button>().interactable, Is.False);
        effect.ForceMeshUpdate(true, true);
        Assert.That(effect.text, Is.Empty, "Material warnings belong to the right detail panel.");
        ui.ShowBuildingDetailsForEditor(building);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        AssertMaterialShortageRows(building);
        Assert.That(body.text, Does.Contain("<sprite=\"HudProductionEfficiency\" index=0>" + (state.Efficiency * 100).ToGameString() + "%"));
        state.SetEfficiencyForEditor(ExpantaNum.Zero);
        ui.ShowBuildingDetailsForEditor(building);
        Assert.That(body.text, Does.Contain("<sprite=\"HudProductionEfficiency\" index=0>0%"));
        Assert.That(body.text, Does.Contain("停工原因"));
    }

    [UnityTest]
    public IEnumerator BuildingDetails_RawAttributesAndFinalEfficiencyRemainDistinct()
    {
        yield return Load();
        BuildingDef building = Object.Instantiate(DataBase<BuildingDef>.Find("Lumberyard"));
        ResearchDef bonus = Object.Instantiate(DataBase<ResearchDef>.All[0]);
        building.SetIdForEditor("FinalValueDisplayFixture");
        bonus.SetIdForEditor("FinalValueDisplayBonus");
        ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
        ResourceDef stone = DataBase<ResourceDef>.Find("StoneChunk");
        ResourceDef input = DataBase<ResourceDef>.Find("Biomass");
        Assert.That(wood != null && stone != null && input != null, Is.True);
        building.ConfigureEconomyForEditor(1.15, 0, 0, 0, 7, 10, 20, 2, 100, 30, -11, 0, 0,
            new List<Pair<ResourceDef, ExpantaNum>>(),
            new List<Pair<ResourceDef, ExpantaNum>> { new(wood, 10), new(stone, 5) },
            new List<Pair<ResourceDef, ExpantaNum>> { new(input, 3) });
        bonus.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new() { Type = ResearchEffectType.GlobalBuildingProductionMultiplier, Value = "1.5" },
            new() { Type = ResearchEffectType.BuildingProductionMultiplier, Building = building, Value = "2" },
            new() { Type = ResearchEffectType.BuildingProductionMultiplier, Building = building, Resource = wood, Value = "3" },
            new() { Type = ResearchEffectType.ResourceProductionMultiplier, Resource = stone, Value = "4" },
            new() { Type = ResearchEffectType.GlobalResearchMultiplier, Value = "1.6" },
            new() { Type = ResearchEffectType.BuildingResearchPowerMultiplier, Building = building, Value = "2.5" },
            new() { Type = ResearchEffectType.PowerMultiplier, Value = "1.7" },
            new() { Type = ResearchEffectType.BuildingPowerProductionMultiplier, Building = building, Value = "2.2" },
            new() { Type = ResearchEffectType.GlobalLogisticsMultiplier, Value = "3" },
            new() { Type = ResearchEffectType.GlobalFoodProductionMultiplier, Value = "1.8" },
            new() { Type = ResearchEffectType.HappinessBonus, Value = "0.5" },
            new() { Type = ResearchEffectType.FoodCapacityMultiplier, Value = "2" }
        });
        ResearchState completed = new(bonus);
        completed.SetStatusForEditor(ResearchStatus.Completed);
        try
        {
            ProgressionModifierManager.Rebuild(new[] { completed });
            ResearchManager.Instance.GlobalEfficiencyFactor = new ExpantaNum(.7);
            BuildingManager manager = BuildingManager.Instance;
            GameState game = GameManager.Instance.State;
            BuildingState state = manager.EnsureBuilding(building);
            ExpantaNum baseFood = game.FoodProductionRate;
            ExpantaNum baseWood = ResourceManager.Instance.GetState(wood).ProductionRate;
            ExpantaNum baseStone = ResourceManager.Instance.GetState(stone).ProductionRate;
            ExpantaNum baseInput = ResourceManager.Instance.GetState(input).ConsumptionRate;
            manager.SetAmountAndRatesForEditor(state, 1);
            ExpantaNum previous = state.Efficiency;
            foreach (ExpantaNum efficiency in new ExpantaNum[] { .4, 0, 1 })
            {
                BuildingManager.ApplyRateDeltaForEditor(state, 1, previous, 1, efficiency, false);
                state.SetEfficiencyForEditor(efficiency);
                previous = efficiency;
                Assert.That(game.HappinessRewardMultiplier > ExpantaNum.One, Is.True);
                ui.ShowBuildingDetailsForEditor(building);
                yield return null;
                Transform detail = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent");
                string body = detail.Find("Body").GetComponent<TMP_Text>().text;
                Assert.That(body, Does.Contain("每座建筑属性"));
                ExpantaNum food = game.FoodProductionRate - baseFood;
                ExpantaNum research = ResearchManager.CalculateResearchPower(new[] { state }, 0) *
                    ResearchManager.Instance.GlobalEfficiencyFactor * ProgressionModifierManager.Current.GlobalResearchMultiplier * game.HappinessMultiplier;
                Assert.That(body, Does.Contain("<sprite=\"HudFood\" index=0>" + building.FoodProductionRate.ToGameString(showPositiveSign: true) + "/s"));
                Assert.That(body, Does.Contain("<sprite=\"HudPower\" index=0>" + building.PowerContribution.ToGameString(showPositiveSign: true)));
                Assert.That(body, Does.Contain("<sprite=\"HudLogistics\" index=0>" + building.LogisticsContribution.ToGameString(showPositiveSign: true)));
                Assert.That(body, Does.Contain("<sprite=\"HudResearchPower\" index=0>" + building.ResearchPowerGranted.ToGameString(showPositiveSign: true)));
                Assert.That(body, Does.Contain("<sprite=\"HudPopulation\" index=0>+7").And.Contain("<sprite=\"HudFoodStorage\" index=0>+100"));
                Assert.That(body, Does.Contain("<sprite=\"HudProductionEfficiency\" index=0><sprite=\"HudFood\" index=0>：" + (food / building.FoodProductionRate * 100).ToGameString() + "%"));
                Assert.That(body, Does.Contain("<sprite=\"HudProductionEfficiency\" index=0><sprite=\"HudResearchPower\" index=0>：" + (research / building.ResearchPowerGranted * 100).ToGameString() + "%"));
                foreach (var output in new[] { new Pair<ResourceDef, ExpantaNum>(wood, baseWood), new(stone, baseStone) })
                {
                    ExpantaNum rate = (ResourceManager.Instance.GetState(output.First).ProductionRate - output.Second) * game.HappinessRewardMultiplier;
                    ExpantaNum definitionRate = output.First == wood ? 10 : 5;
                    Assert.That(body, Does.Contain("<sprite=\"HudProductionEfficiency\" index=0>" + output.First.Label + "：" + (rate / definitionRate * 100).ToGameString() + "%"));
                    AssertFlow(output.First, rate, false);
                }
                AssertFlow(input, ResourceManager.Instance.GetState(input).ConsumptionRate - baseInput, true);

                void AssertFlow(ResourceDef resource, ExpantaNum rate, bool consumption)
                {
                    bool found = false;
                    foreach (TMP_Text label in detail.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (label.name != "Label" || label.text != resource.Label || !label.transform.parent.name.StartsWith("Flow_")) continue;
                        Assert.That(label.transform.parent.Find("Amount").GetComponent<TMP_Text>().text,
                            Is.EqualTo((consumption ? "-" : "+") + rate.ToGameString() + "/s"), "Formatted runtime contribution must match the visible rate.");
                        found = true;
                    }
                    Assert.That(found, Is.True, resource.Id);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(building);
            Object.DestroyImmediate(bonus);
        }
    }

    [UnityTest]
    public IEnumerator WorkshopPurchase_ResourcesSpentAfterRefresh_ExplainsFailureFromBothButtons()
    {
        yield return Load();
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Ultra);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.IndustrialWorkshop);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1000000));
        ui.SetPage("Workshop");
        Transform rows = ui.transform.Find("SafeAreaRoot/Content/PageHost/Workshop/DataRows");
        WorkshopUpgradeDef target = null;
        Transform targetRow = null;
        foreach (Button candidate in rows.GetComponentsInChildren<Button>())
        {
            if (candidate.name != "PurchaseButton" || !candidate.interactable) continue;
            string label = candidate.transform.parent.Find("Label").GetComponent<TMP_Text>().text;
            foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
                if (upgrade.Label == label && upgrade.ResourceRequirements.Count > 0 &&
                    upgrade.ResourceRequirements[0].Second > ExpantaNum.Zero)
                { target = upgrade; targetRow = candidate.transform.parent; break; }
            if (target != null) break;
        }
        ResourceDef spent = target.ResourceRequirements[0].First;
        TMP_Text tooltip = ui.transform.Find("SafeAreaRoot/Tooltip/Text").GetComponent<TMP_Text>();
        ResourceManager.Instance.SetAmount(spent, ExpantaNum.Zero);
        ui.ShowTooltip("pending-row");
        targetRow.Find("PurchaseButton").GetComponent<Button>().onClick.Invoke();
        Assert.That(WorkshopManager.Instance.IsPurchased(target), Is.False);
        Assert.That(tooltip.text, Is.Not.EqualTo("pending-row"));
        Assert.That(tooltip.text, Does.Contain(spent.Label));

        ResourceManager.Instance.SetAmount(spent, new ExpantaNum(1000000));
        targetRow.GetComponent<Button>().onClick.Invoke();
        Assert.That(ui.DetailActionButtonForEditor.interactable, Is.True);
        ResourceManager.Instance.SetAmount(spent, ExpantaNum.Zero);
        ui.ShowTooltip("pending-detail");
        ui.DetailActionButtonForEditor.onClick.Invoke();
        Assert.That(WorkshopManager.Instance.IsPurchased(target), Is.False);
        Assert.That(tooltip.text, Is.Not.EqualTo("pending-detail"));
        Assert.That(tooltip.text, Does.Contain(spent.Label));
    }

    [UnityTest]
    public IEnumerator BatchDeconstruction_PreviewAndCancelDoNotCommit_ConfirmCommitsOnce()
    {
        yield return Load();
        BuildingDef building = DataBase<BuildingDef>.Find("WoodHouse");
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
    public IEnumerator BatchDeconstruction_DescribesRemovedBuildingsCurrentContribution()
    {
        yield return Load();
        BuildingDef building = DataBase<BuildingDef>.Find("Lumberyard");
        BuildingState state = BuildingManager.Instance.States[building];
        BuildingManager.Instance.SetAmountAndRatesForEditor(state, new ExpantaNum(10));
        ui.SetPage("Buildings");
        ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls/Quantity_Ten").GetComponent<Button>().onClick.Invoke();
        ui.RefreshLiveCardValuesForEditor();
        ui.GetDeconstructButtonForEditor(building).onClick.Invoke();
        Assert.That(ui.ReviewConfirmationPendingForEditor, Is.True);
        TMP_Text warning = ui.transform.Find("SafeAreaRoot/ReviewControls/Confirmation/Panel/Viewport/Content/Warning").GetComponent<TMP_Text>();
        Assert.That(warning.text, Does.Contain("所拆建筑当前数据"));
        Assert.That(warning.text, Does.Contain("拆除后总量可能不同"));
        Assert.That(warning.text, Does.Contain("当前产出："));
        ClickConfirmation("Cancel");
        Assert.That(state.Amount, Is.GreaterThan(ExpantaNum.Zero));
    }

    [UnityTest]
    public IEnumerator ResearchRequirements_ColorInventoryAndKeepColumnsStable()
    {
        yield return Load();
        GameManager.Instance.State.AdvanceTechLevelForEditor(TechLevel.Archotech);
        BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(house), ExpantaNum.One);
        ResearchDef research = null;
        foreach (ResearchDef candidate in DataBase<ResearchDef>.All)
            if (candidate.ResourceRequirements.Count > 0 && candidate.ResourceRequirements[0].Second > ExpantaNum.Zero)
            { research = candidate; break; }
        Assert.That(research, Is.Not.Null);
        var cost = research.ResourceRequirements[0];
        ResourceManager.Instance.SetAmount(cost.First, ExpantaNum.Zero);
        ui.SetPage("Research");
        UIResearchGraphGesture graph = null;
        for (int frame = 0; frame < 480; frame++)
        {
            graph = ui.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport")?.GetComponent<UIResearchGraphGesture>();
            if (graph != null && graph.IsInitialized) break;
            yield return null;
        }
        Assert.That(graph != null && graph.IsInitialized, Is.True, "Measure the detail only after the real research graph has initialized.");
        yield return null;
        yield return null;
        ui.ShowResearchDetailsForEditor(research);
        yield return null;
        const string amountPath = "SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/BuildingRequirements/RequirementContent/Requirement_0/Amount";
        TMP_Text amount = ui.transform.Find(amountPath).GetComponent<TMP_Text>();
        amount.ForceMeshUpdate();
        Assert.That(amount.enableAutoSizing, Is.False);
        Assert.That(amount.isTextOverflowing, Is.False);
        float demandX = amount.textInfo.characterInfo[0].origin;
        int inventoryStart = amount.GetParsedText().IndexOf('(');
        Assert.That(inventoryStart, Is.GreaterThan(0));
        float inventoryX = amount.textInfo.characterInfo[inventoryStart].origin;
        Color32 shortage = amount.textInfo.characterInfo[inventoryStart + 1].color;
        Assert.That(shortage.r, Is.GreaterThan(shortage.g), "Insufficient inventory must use the shortage tint.");
        ResourceManager.Instance.SetAmount(cost.First, cost.Second * new ExpantaNum(123.45));
        ui.RefreshAutomaticUIForEditor();
        yield return new WaitForSecondsRealtime(.9f);
        ui.RefreshAutomaticUIForEditor();
        yield return null;
        amount = ui.transform.Find(amountPath).GetComponent<TMP_Text>();
        amount.ForceMeshUpdate();
        int updatedInventoryStart = amount.GetParsedText().IndexOf('(');
        Assert.That(amount.textInfo.characterInfo[0].origin, Is.EqualTo(demandX).Within(.1f));
        Assert.That(amount.textInfo.characterInfo[updatedInventoryStart].origin, Is.EqualTo(inventoryX).Within(.1f),
            "Changing inventory length must not move either numeric column.");
        Color32 enough = amount.textInfo.characterInfo[updatedInventoryStart + 1].color;
        Assert.That(enough.g, Is.GreaterThan(enough.r), "Affordable inventory must use the positive tint.");
        Assert.That(amount.isTextOverflowing, Is.False);
    }

    [UnityTest]
    public IEnumerator ResearchDetails_ShowAllDirectUsesAndHideEmptySections()
    {
        yield return Load();
        GameManager.Instance.State.AdvanceTechLevelForEditor(TechLevel.Ultra);
        BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(house), ExpantaNum.One);
        ResearchDef mostConnected = null;
        int largest = -1;
        var targets = new HashSet<ResearchDef>();
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
        {
            int count = 0;
            foreach (BuildingDef building in DataBase<BuildingDef>.All)
                if (HasResearch(building.RequiredResearch, research)) count++;
            foreach (ResearchDef next in DataBase<ResearchDef>.All)
                if (HasResearch(next.Prerequisites, research)) count++;
            if (count > largest) { mostConnected = research; largest = count; }
        }
        Assert.That(largest, Is.GreaterThan(3), "The real definitions must exercise the former three-item truncation.");
        targets.Add(mostConnected);
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
            if (building is SectorBuildingDef && building.RequiredResearch.Count > 0)
            { targets.Add(building.RequiredResearch[0]); break; }
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
            if (upgrade.RequiredResearch.Count > 0)
            { targets.Add(upgrade.RequiredResearch[0]); break; }
        foreach (SectorDef sector in DataBase<SectorDef>.All)
            if (sector.InteriorExploration != null && sector.InteriorExploration.RequiredResearch.Count > 0)
            { targets.Add(sector.InteriorExploration.RequiredResearch[0]); break; }
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        foreach (ResearchDef research in targets)
        {
            ui.ShowResearchDetailsForEditor(research);
            foreach (BuildingDef building in DataBase<BuildingDef>.All)
                if (HasResearch(building.RequiredResearch, research))
                    Assert.That(body.text, Does.Contain(building.Label), research.Id + " -> " + building.Id);
            foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
                if (HasResearch(upgrade.RequiredResearch, research))
                    Assert.That(body.text, Does.Contain(upgrade.Label), research.Id + " -> " + upgrade.Id);
            foreach (ResearchDef next in DataBase<ResearchDef>.All)
                if (HasResearch(next.Prerequisites, research))
                    Assert.That(body.text, Does.Contain("research:" + next.Id), research.Id + " -> " + next.Id);
            foreach (SectorDef sector in DataBase<SectorDef>.All)
                if (sector.InteriorExploration != null && HasResearch(sector.InteriorExploration.RequiredResearch, research))
                    Assert.That(body.text, Does.Contain(sector.InteriorExploration.Label).And.Contain("sector:" + sector.Id),
                        research.Id + " -> " + sector.Id);
        }
        ResearchDef agriculture = DataBase<ResearchDef>.Find("Agriculture");
        ui.ShowResearchDetailsForEditor(agriculture);
        Assert.That(body.text, Does.Not.Contain("研究前置\n  无"));
        Assert.That(body.text, Does.Contain("×" + agriculture.Effects[0].NumericValue.ToGameString()),
            "Multipliers must be visibly distinguished from added amounts.");
        ResearchDef processing = DataBase<ResearchDef>.Find("StoneProcessing");
        ui.ShowResearchDetailsForEditor(processing);
        foreach (ResearchEffectDefinition effect in processing.Effects)
            if (effect.Type == ResearchEffectType.DeconstructionReturnRate)
                Assert.That(body.text, Does.Contain((effect.NumericValue * 100).ToGameString() + "%"),
                    "A refund fraction must be presented as a percentage.");
        foreach (var system in new[]
        {
            (ProgressionVisibilityRules.EraResearchId, "开放时代页"),
            (ProgressionVisibilityRules.ProductionResearchId, "开放生产页"),
            ("Calendar", "历法日期")
        })
        {
            ui.ShowResearchDetailsForEditor(DataBase<ResearchDef>.Find(system.Item1));
            Assert.That(body.text, Does.Contain(system.Item2), "Real system uses must remain visible outside narrative descriptions.");
            ui.RefreshLiveCardValuesForEditor();
            Assert.That(body.text, Does.Contain(system.Item2), "Refreshing prerequisite status must preserve system uses.");
        }
    }

    private static bool HasResearch(System.Collections.Generic.IReadOnlyList<ResearchDef> values, ResearchDef target)
    {
        foreach (ResearchDef value in values) if (value == target) return true;
        return false;
    }

    [UnityTest]
    public IEnumerator BuildingResearchPrerequisiteLink_OpensAndFramesItsResearch()
    {
        yield return Load();
        BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(house), ExpantaNum.One);
        ui.SetPage("Buildings");
        ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls/ShowDetails").GetComponent<Toggle>().isOn = true;
        ui.ShowBuildingDetailsForEditor(DataBase<BuildingDef>.Find("Farm"));
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        body.ForceMeshUpdate(true);
        bool clicked = false;
        for (int i = 0; i < body.textInfo.linkCount; i++)
            if (body.textInfo.linkInfo[i].GetLinkID() == "research:Agriculture")
            {
                TMP_CharacterInfo character = body.textInfo.characterInfo[body.textInfo.linkInfo[i].linkTextfirstCharacterIndex];
                Vector3 world = body.transform.TransformPoint((character.bottomLeft + character.topRight) * .5f);
                body.GetComponent<UIReviewTextLinks>().OnPointerClick(new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, world) });
                clicked = true;
                break;
            }
        Assert.That(clicked, Is.True, "Research prerequisites must expose executable links.");
        UIResearchGraphGesture gesture = null;
        for (int frame = 0; frame < 480; frame++)
        {
            gesture = ui.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport")?.GetComponent<UIResearchGraphGesture>();
            if (gesture != null && gesture.IsInitialized && ResearchManager.Instance.SelectedResearchId == "Agriculture") break;
            yield return null;
        }
        Assert.That(gesture != null && gesture.IsInitialized, Is.True);
        Assert.That(ResearchManager.Instance.SelectedResearchId, Is.EqualTo("Agriculture")); // Stable navigation ID.
        RectTransform viewport = (RectTransform)gesture.transform;
        Transform node = viewport.Find("ResearchGraphContent/ResearchNode_Agriculture");
        Assert.That(node, Is.Not.Null);
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, node);
        Assert.That(viewport.rect.Overlaps(new Rect(bounds.min, bounds.size)), Is.True,
            "Navigation must bring the selected node into the graph viewport.");
    }

    [UnityTest]
    public IEnumerator ResourceSources_ContainClickableLinks_AndNavigateToExecutableBuildingRow()
    {
        yield return Load();
        BuildingDef building = DataBase<BuildingDef>.Find("Lumberyard");
        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.States[building], ExpantaNum.One);
        ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
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
        Assert.That(ui.ReviewLocateForEditor.gameObject.activeInHierarchy, Is.False);
        Assert.That(ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls").gameObject.activeInHierarchy, Is.True);
    }

    [UnityTest]
    public IEnumerator ActiveCampaignWithCasualties_RetreatStaysReachable_RepairIsSeparate()
    {
        yield return Load();
        SectorDef sector = DataBase<SectorDef>.Find("TauCetiFoundry");
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
    public IEnumerator AuthoredOverviewTools_SaveAndResetResearch()
    {
        yield return Load();
        SaveManager save = SaveManager.Instance;
        ui.SetPage("Overview");
        Button saveButton = ui.transform.Find("SafeAreaRoot/Content/PageTool/PageTools/Save").GetComponent<Button>();
        Assert.That(saveButton.gameObject.activeInHierarchy, Is.True);
        saveButton.onClick.Invoke();
        Assert.That(save.LastSuccessfulSaveUnixSeconds, Is.GreaterThan(0));
        Assert.That(save.LastSaveFailed, Is.False);
        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
        ui.SetPage("Research");
        Assert.That(saveButton.gameObject.activeInHierarchy, Is.False);
        Assert.That(ui.transform.Find("SafeAreaRoot/Content/PageTool/PageTools/ResetResearch").gameObject.activeInHierarchy, Is.True);
        Canvas.ForceUpdateCanvases();
        Transform pageTool = ui.transform.Find("SafeAreaRoot/Content/PageTool");
        Bounds queue = RectBounds(pageTool, (RectTransform)pageTool.Find("ResearchQueueViewport"));
        Bounds tools = RectBounds(pageTool, (RectTransform)pageTool.Find("PageTools"));
        Assert.That(queue.max.x, Is.LessThanOrEqualTo(tools.min.x), "Authored page tools have a separate reserved area beside the research queue.");

        UIResearchGraphGesture gesture = null;
        for (int frame = 0; frame < 480; frame++)
        {
            Transform researchViewport = ui.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
            gesture = researchViewport?.GetComponent<UIResearchGraphGesture>();
            if (gesture != null && gesture.IsInitialized) break;
            yield return null;
        }
        Assert.That(gesture.IsInitialized, Is.True);
        RectTransform viewport = (RectTransform)gesture.transform;
        RectTransform graph = (RectTransform)viewport.Find("ResearchGraphContent");
        Canvas.ForceUpdateCanvases();
        Assert.That(graph.rect.height, Is.GreaterThan(viewport.rect.height), "The real research tree must expose vertical overflow for the reset regression.");
        gesture.RestoreView(Vector2.zero, 1.25f);
        pageTool.Find("PageTools/ResetResearch").GetComponent<Button>().onClick.Invoke();
        Assert.That(graph.anchoredPosition.x, Is.EqualTo(0f).Within(.1f));
        Assert.That(graph.anchoredPosition.y,
            Is.EqualTo(Mathf.Min(0f, viewport.rect.height - graph.rect.height * graph.localScale.y)).Within(.1f),
            "Reset restores the visible top-left of the research tree instead of its empty bottom-left.");
    }

    private void ClickConfirmation(string name) => ui.transform.Find("SafeAreaRoot/ReviewControls/Confirmation/Panel/Actions/" + name).GetComponent<Button>().onClick.Invoke();

    [UnityTest]
    public IEnumerator HousingRecoveryAndUpgradeDetails_UseManagerConstraintsAndRealInvestmentValues()
    {
        yield return Load();
        BuildingManager buildings = BuildingManager.Instance;
        BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
        BuildingDef quarry = DataBase<BuildingDef>.Find("Quarry");
        ResourceManager.Instance.SetAmount(DataBase<ResourceDef>.Find("WoodLog"), new ExpantaNum(10000));
        ui.SetPage("Buildings");
        ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls/ShowDetails").GetComponent<Toggle>().isOn = true;
        ui.ShowBuildingDetailsForEditor(house);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        string availableHousingDetails = body.text;
        buildings.SetAmountAndRatesForEditor(buildings.States[quarry], new ExpantaNum(100));
        Assert.That(buildings.AvailableProductivity, Is.LessThan(ExpantaNum.Zero));
        ui.ShowBuildingDetailsForEditor(house);
        Assert.That(body.text, Is.Not.EqualTo(availableHousingDetails), "The settlement productivity deficit updates when existing building demand changes.");
        Assert.That(body.text, Does.Contain("<sprite=\"HudProductivity\" index=0> 需求 " + buildings.UsedProductivity.ToGameString() +
            "，缺少 " + ExpantaNum.Max(ExpantaNum.Zero, buildings.UsedProductivity - buildings.TotalProductivity).ToGameString()));
        Assert.That(body.text, Does.Not.Contain("可供").And.Not.Contain("建筑总需求"));
        Assert.That(body.text, Does.Not.Contain("全局开工率"), "Housing details must not show an unrelated global ratio.");
        Assert.That(buildings.GetLaborOperatingRatio(house).ApproximatelyEquals(ExpantaNum.One), Is.True,
            "Housing has no labor demand and must remain independent of the global operating ratio.");
        Assert.That(body.text, Does.Contain("<sprite=\"HudPopulation\" index=0>+" + house.PopulationCapacityGranted.ToGameString()));
        Assert.That(buildings.GetBuildFailure(house, ExpantaNum.One), Is.EqualTo(BuildFailure.None));
        Assert.That(buildings.TryBuild(house, ExpantaNum.One, out _), Is.True);
        GameManager.Instance.State.AdvanceTechLevelForEditor(TechLevel.StoneAge);
        ResearchDef masonry = DataBase<ResearchDef>.Find("Masonry");
        ResearchState masonryState = ResearchManager.Instance.States[masonry];
        masonryState.SetProgressForEditor(masonryState.BaseCost);
        masonryState.SetStatusForEditor(ResearchStatus.Completed);
        buildings.SetAmountAndRatesForEditor(buildings.States[quarry], ExpantaNum.Zero);
        foreach (var entry in ResourceManager.Instance.States)
            ResourceManager.Instance.SetAmount(entry.Key, new ExpantaNum(100000));
        Assert.That(buildings.TryGetUnlockedUpgradeTarget(house, out BuildingDef target), Is.True);
        ui.ShowBuildingDetailsForEditor(house);
        Assert.That(body.text, Does.Contain(target.Label), "Upgrade comparison identifies its actual unlocked target.");
        ExpantaNum capacityDelta = target.PopulationCapacityGranted - house.PopulationCapacityGranted;
        Assert.That(body.text, Does.Not.Contain("<sprite=\"HudFood\" index=0>"),
            "Housing capacity must not be presented as a fabricated food flow.");
        ExpantaNum capacity = GameManager.Instance.State.Population.PopulationCapacity;
        ExpantaNum foodDemand = GameManager.Instance.State.FoodTotalConsumptionRate;
        ExpantaNum population = GameManager.Instance.State.Population.Population;
        Assert.That(buildings.GetUpgradeFailure(house, ExpantaNum.One), Is.EqualTo(BuildFailure.None));
        Assert.That(buildings.TryUpgrade(house, ExpantaNum.One, out _), Is.True);
        Assert.That(GameManager.Instance.State.Population.PopulationCapacity.ApproximatelyEquals(capacity + capacityDelta), Is.True);
        Assert.That(GameManager.Instance.State.FoodTotalConsumptionRate.ApproximatelyEquals(foodDemand), Is.True,
            "Changing housing capacity without adding residents must not add food consumption.");
        Assert.That(GameManager.Instance.State.Population.Population.ApproximatelyEquals(population), Is.True, "Housing capacity does not grant residents immediately.");
    }

    [UnityTest]
    public IEnumerator ResearchCancellation_ListsDependentItems_AndDoesNotMutateUntilConfirmation()
    {
        yield return Load();
        ResearchDef target = DataBase<ResearchDef>.Find("StoneProcessing");
        ResearchManager manager = ResearchManager.Instance;
        manager.HandleResearchAction(target);
        ResearchDef prerequisite = target.Prerequisites[0];
        Assert.That(manager.GetCancellationPreview(prerequisite).Count, Is.GreaterThan(1));
        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
        ui.SetPage("Research"); ui.ShowResearchDetailsForEditor(prerequisite);
        int countBefore = manager.ResearchQueue.Count;
        ResearchState activeBefore = manager.ActiveResearch;
        ui.DetailActionButtonForEditor.onClick.Invoke();
        Assert.That(ui.ReviewConfirmationPendingForEditor, Is.True);
        Assert.That(manager.ResearchQueue.Count, Is.EqualTo(countBefore), "Queue length is a discrete count.");
        Assert.That(manager.ActiveResearch, Is.SameAs(activeBefore));
        Canvas.ForceUpdateCanvases();
        Transform panel = ui.transform.Find("SafeAreaRoot/ReviewControls/Confirmation/Panel");
        string warning = panel.Find("Viewport/Content/Warning").GetComponent<TMP_Text>().text;
        Assert.That(warning, Does.Contain("依赖它的后续研究"));
        Assert.That(warning, Does.Contain(prerequisite.Label));
        Assert.That(warning, Does.Contain(target.Label));
        Bounds viewport = RectBounds(panel, (RectTransform)panel.Find("Viewport"));
        Bounds actions = RectBounds(panel, (RectTransform)panel.Find("Actions"));
        Assert.That(viewport.min.y, Is.GreaterThanOrEqualTo(actions.max.y), "Scrollable impact text does not obscure confirm/cancel controls.");
        ClickConfirmation("Cancel");
        Assert.That(manager.ResearchQueue.Count, Is.EqualTo(countBefore));
        ui.DetailActionButtonForEditor.onClick.Invoke(); ClickConfirmation("Confirm");
        Assert.That(manager.IsQueued(target), Is.False, "Dependent research is removed only at confirmation.");
        Assert.That(manager.ResearchQueue.Count, Is.LessThan(countBefore));
    }

    [UnityTest]
    public IEnumerator ResearchSelection_BlocksFutureDetailsAndKeepsCurrentQueuePlanning()
    {
        yield return Load();
        BuildingManager buildings = BuildingManager.Instance;
        buildings.SetAmountAndRatesForEditor(
            buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
        ui.SetPage("Research");
        ResearchManager manager = ResearchManager.Instance;
        ResearchDef current = DataBase<ResearchDef>.Find("StoneProcessing");
        ResearchDef future = DataBase<ResearchDef>.Find("SteamPower");
        Transform graph = null;
        Button futureNode = null;
        for (int i = 0; i < 600; i++)
        {
            graph = ui.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport/ResearchGraphContent");
            futureNode = graph?.Find("ResearchNode_" + future.Id)?.GetComponent<Button>();
            if (futureNode != null && graph.Find("ResearchNode_" + current.Id) != null) break;
            yield return null;
        }
        Assert.That(futureNode, Is.Not.Null);
        ui.ShowResearchDetailsForEditor(current);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        string currentDetails = body.text;
        Assert.That(futureNode.interactable, Is.False);
        Assert.That(futureNode.targetGraphic.canvasRenderer.GetAlpha(), Is.LessThan(1f),
            "Future-era nodes remain in the graph but are visibly dimmed.");
        futureNode.onClick.Invoke();
        Assert.That(body.text, Is.EqualTo(currentDetails), "Even a direct event invocation cannot inspect future research.");
        Assert.That(manager.SelectedResearchId, Is.EqualTo(current.Id));
        Assert.That(body.text, Does.Not.Contain("当前状态"));
        Assert.That(body.text, Does.Not.Contain("研究能力"));
        Assert.That(body.text, Does.Not.Contain("先付清材料"));

        Assert.That(manager.ArePrerequisitesCompleted(current), Is.False);
        TMP_Text actionLabel = ui.DetailActionButtonForEditor.GetComponentInChildren<TMP_Text>();
        Assert.That(actionLabel.text, Does.Contain("前置优先"));
        foreach (var entry in ResourceManager.Instance.States)
            ResourceManager.Instance.SetAmount(entry.Key, ExpantaNum.Zero);
        Assert.That(ui.DetailActionButtonForEditor.interactable, Is.True);
        ui.DetailActionButtonForEditor.onClick.Invoke();
        Assert.That(manager.IsQueued(current), Is.True, "Current-era research can plan unmet prerequisites without materials.");
        Assert.That(actionLabel.text, Does.Contain("移出"));
        foreach (ResearchDef prerequisite in current.Prerequisites)
            Assert.That(manager.IsQueued(prerequisite) || manager.ActiveResearch?.Definition == prerequisite ||
                manager.IsResearchCompleted(prerequisite.Id), Is.True);
        Assert.That(body.text, Does.Not.Contain("执行结果"));

        manager.GetState(future).SetStatusForEditor(ResearchStatus.Completed);
        ui.ShowResearchDetailsForEditor(future);
        Assert.That(manager.SelectedResearchId, Is.EqualTo(future.Id), "Completed research can always be reviewed.");
        Assert.That(futureNode.interactable, Is.True);
        Assert.That(body.text, Does.Contain(future.Description));
        Assert.That(ui.DetailActionButtonForEditor.interactable, Is.False);
    }

    [UnityTest]
    public IEnumerator ResearchEraChange_RefreshesLockedNodeLabelWithoutStateVersionChange()
    {
        yield return Load();
        BuildingManager buildings = BuildingManager.Instance;
        buildings.SetAmountAndRatesForEditor(
            buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
        ui.SetPage("Research");
        ResearchDef research = DataBase<ResearchDef>.Find("IndustrialAgriculture");
        Button node = null;
        for (int frame = 0; frame < 600; frame++)
        {
            Transform graph = ui.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport/ResearchGraphContent");
            node = graph?.Find("ResearchNode_" + research.Id)?.GetComponent<Button>();
            if (node != null) break;
            yield return null;
        }
        Assert.That(node, Is.Not.Null);
        TMP_Text label = node.transform.Find("State").GetComponent<TMP_Text>();
        ResearchState state = ResearchManager.Instance.GetState(research);
        Assert.That(state.Status, Is.EqualTo(ResearchStatus.Locked));
        Assert.That(label.text, Is.EqualTo("未解锁"));
        int version = state.Version;
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Industrial);
        ui.ShowResearchDetailsForEditor(research);
        Assert.That(state.Version, Is.EqualTo(version), "Only the era changes, not this research state.");
        Assert.That(node.interactable, Is.True);
        Assert.That(label.text, Is.EqualTo("需前置研究"));
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
            manager.HandleResearchAction(DataBase<ResearchDef>.Find(id));
        ResearchState selected = null; int originalIndex = -1;
        for (int i = 1; i < manager.ResearchQueue.Count; i++)
            if (manager.CanMoveQueuedResearch(manager.ResearchQueue[i].Definition,
                    manager.GetResearchOrderIndex(manager.ResearchQueue[i].Definition) - 1))
            { selected = manager.ResearchQueue[i]; originalIndex = i; break; }
        Assert.That(selected, Is.Not.Null, "Fixture contains independent waiting research.");
        int count = manager.ResearchQueue.Count;
        ResearchState active = manager.ActiveResearch;
        bool paid = selected.CostPaid;
        ExpantaNum progress = selected.Progress;
        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
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

    [UnityTest]
    public IEnumerator ResearchQueue_EarlierButtonPromotesSecondOverActiveAndRetainsPaidProgress()
    {
        yield return Load();
        ResearchManager manager = ResearchManager.Instance;
        manager.ResetForLoadForEditor();
        foreach (var pair in ResourceManager.Instance.States)
            ResourceManager.Instance.SetAmount(pair.Key, new ExpantaNum(10000));
        ResearchDef first = DataBase<ResearchDef>.Find("Quarry");
        ResearchDef second = DataBase<ResearchDef>.Find("Agriculture");
        manager.HandleResearchAction(first);
        ResearchState original = manager.ActiveResearch;
        original.SetProgressForEditor(original.BaseCost / new ExpantaNum(4));
        ExpantaNum progress = original.Progress;
        manager.HandleResearchAction(second);
        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
        ui.SetPage("Research"); ui.ShowResearchDetailsForEditor(second);
        Button earlier = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailTools/Earlier").GetComponent<Button>();
        Assert.That(earlier.gameObject.activeInHierarchy && earlier.interactable, Is.True);
        earlier.onClick.Invoke();
        Assert.That(manager.ActiveResearch.Definition, Is.SameAs(second));
        Assert.That(manager.ResearchQueue.Count, Is.EqualTo(1));
        Assert.That(manager.ResearchQueue[0], Is.SameAs(original));
        Assert.That(original.CostPaid, Is.True);
        Assert.That(original.Progress.ApproximatelyEquals(progress), Is.True);
        Assert.That(earlier.interactable, Is.False, "The promoted first item cannot move earlier again.");
        Assert.That(ui.ReviewConfirmationPendingForEditor, Is.False);
    }
}
