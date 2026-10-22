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
        AssertBuildingStatusIcons(effect, 3, 4);
        CaptureBuildingStatus("land-shortage");
        yield return null;
        string single = effect.text;
        ui.ShowBuildingDetailsForEditor(building);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        string singleDetails = body.text;
        Assert.That(singleDetails, Does.Contain("土地：需求 " + building.SpaceCost.ToGameString()
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
        AssertBuildingStatusIcons(effect, 3, 4, 5);
        ui.ShowBuildingDetailsForEditor(building);
        CaptureBuildingStatus("all-shortages");
        yield return null;
        foreach (var cost in building.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, new ExpantaNum(1000000));

        game.AdjustTerritoryTotalForEditor(new ExpantaNum(10000));
        controls.Find("Quantity_One").GetComponent<Button>().onClick.Invoke();
        Assert.That(manager.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.ProductivityInsufficient));
        ui.RefreshLiveCardValuesForEditor();
        effect.ForceMeshUpdate(true, true);
        Assert.That(effect.textInfo.characterInfo[0].elementType, Is.EqualTo(TMP_TextElementType.Sprite));
        Assert.That(effect.textInfo.characterInfo[0].spriteIndex, Is.EqualTo(4), "Productivity shortage uses the warning resident-group glyph, not the neutral category icon.");
        CaptureBuildingStatus("productivity-shortage");
        yield return null;
        ui.ShowBuildingDetailsForEditor(building);
        Assert.That(body.text, Does.Contain((building.ProductivityConsumption - manager.AvailableProductivity).ToGameString()));
        body.ForceMeshUpdate(true, true);
        var detailIcons = new HashSet<int>();
        for (int i = 0; i < body.textInfo.characterCount; i++)
            if (body.textInfo.characterInfo[i].elementType == TMP_TextElementType.Sprite)
                detailIcons.Add(body.textInfo.characterInfo[i].spriteIndex);
        Assert.That(detailIcons, Does.Contain(0), "Available land uses the neutral land icon.");
        Assert.That(detailIcons, Does.Contain(4), "Insufficient productivity uses the warning icon.");

        game.Population.AdjustPopulationCapacityForEditor(new ExpantaNum(100));
        game.RestorePopulationForEditor(new ExpantaNum(100));
        Assert.That(manager.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.None));
        ui.RefreshLiveCardValuesForEditor();
        effect.ForceMeshUpdate(true, true);
        Assert.That(effect.textInfo.characterCount, Is.EqualTo(1));
        Assert.That(effect.textInfo.characterInfo[0].elementType, Is.EqualTo(TMP_TextElementType.Sprite));
        Assert.That(effect.textInfo.characterInfo[0].spriteIndex, Is.EqualTo(2), "Sufficient resources retain a large neutral materials icon.");
        ui.ShowBuildingDetailsForEditor(building);
        Assert.That(body.text, Does.Contain("土地充足"));
        Assert.That(body.text, Does.Contain("生产力充足"));
        CaptureBuildingStatus("ready");
        yield return null;
        foreach (var cost in building.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, ExpantaNum.Zero);
        Assert.That(manager.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.ResourceInsufficient));
        ui.RefreshLiveCardValuesForEditor();
        effect.ForceMeshUpdate(true, true);
        Assert.That(effect.textInfo.characterCount, Is.EqualTo(1));
        Assert.That(effect.textInfo.characterInfo[0].spriteIndex, Is.EqualTo(5));
        ui.ShowBuildingDetailsForEditor(building);
        CaptureBuildingStatus("materials-shortage");
        yield return null;
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
                Is.GreaterThanOrEqualTo(((RectTransform)effect.transform.parent).rect.height * .65f),
                "Even three icons must occupy most of the card height.");
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
        Assert.That(body.text, Does.Contain("土地充足"));
        Assert.That(body.text, Does.Contain("生产力充足"));

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
            "overview-events-20261015", "large-icons"));
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
        Assert.That(effect.textInfo.characterCount, Is.EqualTo(1));
        Assert.That(effect.textInfo.characterInfo[0].spriteIndex, Is.EqualTo(5));
        ui.ShowBuildingDetailsForEditor(building);
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        Assert.That(body.text, Does.Contain("生产效率：" + (state.Efficiency * 100).ToGameString() + "%"));
        state.SetEfficiencyForEditor(ExpantaNum.Zero);
        ui.ShowBuildingDetailsForEditor(building);
        Assert.That(body.text, Does.Contain("生产效率：0%"));
        Assert.That(body.text, Does.Contain("停工原因"));
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
        Assert.That(body.text, Is.EqualTo(availableHousingDetails), "Zero-consumption housing still has zero shortage under productivity overcommit.");
        Assert.That(body.text, Does.Contain((house.PopulationCapacityGranted * PopulationState.FoodConsumptionPerPerson).ToGameString() + "/s"));
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
        ResearchDef target = DataBase<ResearchDef>.Find("StoneProcessing");
        ResearchManager manager = ResearchManager.Instance;
        manager.HandleResearchAction(target);
        ResearchDef prerequisite = target.Prerequisites[0];
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
            manager.HandleResearchAction(DataBase<ResearchDef>.Find(id));
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
