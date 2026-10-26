using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Controlled visual fixture: late-game grants are only for layout evidence.
public sealed class PlayerLayoutRequestPlayModeTests
{
    private string saveRoot;
    private readonly string output = Path.GetFullPath("data/hud-efficiency-audit-20261026/player-details");
    private readonly HashSet<Object> existingPersistentManagers = new();
    private KingdomUIRoot ui;
    private Camera camera;
    private GameObject cameraHost;
    private RenderTexture target;
    private Texture2D pixels;
    private ScrollRect detail;

    [SetUp]
    public void SetUp()
    {
        Assert.That(Object.FindObjectsOfType<SaveManager>(true), Is.Empty, "An existing save writer must not be redirected by this fixture.");
        foreach (TutorialManager manager in Object.FindObjectsOfType<TutorialManager>(true)) existingPersistentManagers.Add(manager);
        foreach (MusicManager manager in Object.FindObjectsOfType<MusicManager>(true)) existingPersistentManagers.Add(manager);
        foreach (UIButtonSoundManager manager in Object.FindObjectsOfType<UIButtonSoundManager>(true)) existingPersistentManagers.Add(manager);
        saveRoot = Path.GetFullPath("Temp/PlayerLayoutRequest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        Directory.CreateDirectory(output);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (saveRoot == null) yield break;
        foreach (SaveManager writer in Object.FindObjectsOfType<SaveManager>(true)) Object.DestroyImmediate(writer.gameObject);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            foreach (GameObject root in sample.GetRootGameObjects()) if (root != null) Object.DestroyImmediate(root);
        foreach (TutorialManager manager in Object.FindObjectsOfType<TutorialManager>(true))
            if (!existingPersistentManagers.Contains(manager)) Object.DestroyImmediate(manager.gameObject);
        foreach (MusicManager manager in Object.FindObjectsOfType<MusicManager>(true))
            if (!existingPersistentManagers.Contains(manager)) Object.DestroyImmediate(manager.gameObject);
        foreach (UIButtonSoundManager manager in Object.FindObjectsOfType<UIButtonSoundManager>(true))
            if (!existingPersistentManagers.Contains(manager)) Object.DestroyImmediate(manager.gameObject);
        if (pixels != null) Object.DestroyImmediate(pixels);
        if (cameraHost != null) Object.DestroyImmediate(cameraHost);
        if (target != null) { target.Release(); Object.DestroyImmediate(target); }
        yield return null;
        Assert.That(Object.FindObjectsOfType<SaveManager>(true), Is.Empty);
        SaveManager.ClearSaveRootOverrideForTests();
        string expectedParent = Path.GetFullPath("Temp");
        Assert.That(Path.GetDirectoryName(saveRoot), Is.EqualTo(expectedParent), "Cleanup remains within the fixture's project Temp directory.");
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        Assert.That(Directory.Exists(saveRoot), Is.False);
    }

    [UnityTest]
    public IEnumerator RequestedDetails_RemoveRedundantCopyAndCaptureRepresentativeLayouts()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        for (int i = 0; i < 240; i++)
        {
            ui = Object.FindObjectOfType<KingdomUIRoot>();
            if (ui != null && Object.FindObjectOfType<GameBootstrap>()?.Completed == true) break;
            yield return null;
        }
        Assert.That(ui, Is.Not.Null);
        Assert.That(SaveManager.Instance.LastLoadCreatedNewGame, Is.True);
        SimulationManager.Instance.SetRunning(false);
        Object.FindObjectOfType<MusicManager>()?.Stop();
        cameraHost = new GameObject("PlayerLayoutRequestCamera", typeof(Camera));
        camera = cameraHost.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        target = new RenderTexture(2640, 1200, 24);
        target.Create(); camera.targetTexture = target;
        pixels = new Texture2D(2640, 1200, TextureFormat.RGB24, false);
        Canvas canvas = ui.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 100;
        for (int i = 0; i < 5; i++) yield return null;
        detail = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport").GetComponent<ScrollRect>();
        PrepareControlledState();
        for (int i = 0; i < 3; i++) yield return null;

        yield return Page("Buildings");
        yield return Click(FindCard("Buildings", DataBase<BuildingDef>.Find("Farm").Label));
        string farm = Capture("01-building-farm");
        foreach (string removed in new[] { "本次新增劳力需求", "本次释放劳力需求", "劳力需求不限制建造", "探索并占领新地区", "当前状态" })
            Assert.That(farm, Does.Not.Contain(removed), "The requested UI copy must be removed: " + removed);
        Assert.That(farm, Does.Contain(DataBase<BuildingDef>.Find("Farm").Label));
        Assert.That(farm, Does.Not.Contain("全局开工率"));
        BuildingState farmState = BuildingManager.Instance.GetState(DataBase<BuildingDef>.Find("Farm"));
        BuildingDef farmDefinition = farmState.Definition;
        ExpantaNum foodEfficiency = farmState.Efficiency * ProgressionModifierManager.Current.GetBuildingProductionMultiplier(farmDefinition) *
            ProgressionModifierManager.Current.GlobalBuildingProductionMultiplier * ProgressionModifierManager.Current.GlobalFoodProductionMultiplier *
            GameManager.Instance.State.Events.ProductionMultiplier;
        Assert.That(farm, Does.Contain("<sprite=\"HudProductionEfficiency\" index=0>" + (foodEfficiency * 100).ToGameString() + "%"));
        Assert.That(farm, Does.Not.Contain("生产效率").And.Not.Contain("运行效率"));
        Assert.That(farm, Does.Contain("<sprite=\"HudTerritory\" index=0>" + (-farmDefinition.SpaceCost).ToGameString(showPositiveSign: true)));
        TMP_SpriteAsset territorySprite = Resources.Load<TMP_SpriteAsset>("Sprite Assets/HudTerritory");
        Assert.That(territorySprite.spriteSheet, Is.SameAs(Resources.Load<Texture2D>("Art/UI/Status/HudTerritory")));
        Assert.That(territorySprite.spriteGlyphTable[0].glyphRect.width, Is.EqualTo(territorySprite.spriteSheet.width));
        TMP_Text farmBody = detail.content.Find("Body").GetComponent<TMP_Text>();
        farmBody.ForceMeshUpdate(true, true);
        bool sawTerritory = false;
        for (int i = 0; i < farmBody.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = farmBody.textInfo.characterInfo[i];
            if (character.elementType != TMP_TextElementType.Sprite) continue;
            if (character.spriteAsset.name == "HudTerritory") sawTerritory |= character.isVisible;
            if (character.spriteAsset.name != "HudProductionEfficiency") continue;
            Assert.That(character.isVisible, Is.True);
            Assert.That(farmBody.textInfo.characterInfo[i + 1].lineNumber, Is.EqualTo(character.lineNumber), "Efficiency icon and percentage share one line.");
        }
        Assert.That(sawTerritory, Is.True);
        Assert.That(farmState.Efficiency.ToDouble(), Is.LessThanOrEqualTo(BuildingManager.Instance.LaborOperatingRatio.ToDouble() + 1e-6));
        Assert.That(farm, Does.Contain("每座建筑属性").And.Not.Contain("含全部增益").And.Not.Contain("可供").And.Not.Contain("建筑总需求"));
        Assert.That(farm, Does.Contain("<sprite=\"HudFood\" index=0>" + farmDefinition.FoodProductionRate.ToGameString(showPositiveSign: true) + "/s"));
        Assert.That(farm.IndexOf("本次升级", StringComparison.Ordinal), Is.LessThan(farm.IndexOf("升级为", StringComparison.Ordinal)));
        Assert.That(farm.IndexOf("资源充足", StringComparison.Ordinal), Is.LessThan(farm.IndexOf("每座建筑属性", StringComparison.Ordinal)));
        Assert.That(farm.IndexOf("每座建筑属性", StringComparison.Ordinal), Is.LessThan(farm.IndexOf("HudProductionEfficiency", StringComparison.Ordinal)));
        AssertTopHappiness();
        AssertDetailNumbersStayOnOneLine();
        AssertProductionEntry();
        ResizeCapture(1760, 800);
        Capture("01-building-farm-1760x800");
        AssertTopHappiness();
        AssertDetailNumbersStayOnOneLine();
        ResizeCapture(2640, 1200);

        yield return Page("Workshop");
        Transform workshop = Rows("Workshop").Cast<Transform>().First(x => x.gameObject.activeInHierarchy && x.GetComponent<Button>() != null && x.Find("Label") != null);
        yield return Click(workshop.GetComponent<Button>());
        string owned = Capture("02-workshop-owned");
        Assert.That(owned, Does.Not.Contain("升级已生效"));
        Assert.That(owned, Is.Not.Empty);
        TMP_Text purchaseText = workshop.Find("PurchaseButton").GetComponentInChildren<TMP_Text>();
        purchaseText.ForceMeshUpdate();
        Assert.That(purchaseText.textInfo.lineCount, Is.EqualTo(1), "Purchase state stays inside its button on one line.");
        Assert.That(purchaseText.isTextOverflowing, Is.False);

        yield return Page("Research");
        yield return new WaitForSecondsRealtime(2f);
        Button node = ui.GetComponentsInChildren<Button>(true).First(x => x.name == "ResearchNode_Industrialization");
        UIResearchGraphGesture graph = node.GetComponentInParent<UIResearchGraphGesture>();
        if (graph != null)
        {
            RectTransform content = (RectTransform)node.transform.parent;
            RectTransform viewport = (RectTransform)content.parent;
            RectTransform rect = (RectTransform)node.transform;
            graph.RestoreView(viewport.rect.size * .5f - rect.anchoredPosition - rect.rect.size * .5f, 1f);
        }
        Assert.That(node.gameObject.activeInHierarchy && node.interactable, Is.True);
        node.onClick.Invoke(); yield return null; Canvas.ForceUpdateCanvases();
        string research = Capture("03-research-industrialization");
        Assert.That(research, Does.Not.Contain("当前研究速度"));
        Assert.That(research, Does.Contain(DataBase<ResearchDef>.Find("Industrialization").Label));

        yield return Page("Sectors");
        Transform sector = Rows("Sectors").Find("SectorRow_AzurePool");
        Assert.That(sector, Is.Not.Null);
        yield return Click(sector.GetComponent<Button>());
        string sectorText = Capture("04-sector-azure-pool");
        Assert.That(sectorText, Does.Contain(DataBase<SectorDef>.Find("AzurePool").Label));
        AssertProductionEntry();
        DumpSectorImages();
        Transform menu = sector.Find("SectorBuildingMenu");
        if (menu == null || !menu.gameObject.activeInHierarchy)
            yield return Click(sector.Find("Buildings").GetComponent<Button>());
        menu = sector.Find("SectorBuildingMenu");
        Assert.That(menu != null && menu.gameObject.activeInHierarchy, Is.True);
        Transform facility = menu.Cast<Transform>().First(x => x.gameObject.activeInHierarchy &&
            x.GetComponent<LayoutElement>() != null && x.Find("Name")?.GetComponent<Button>() != null);
        yield return Click(facility.Find("Name").GetComponent<Button>());
        Capture("05-sector-buildings");
        Debug.Log("[PlayerLayoutRequest] Five 2640x1200 GPU captures; isolated controlled fixture; Unity Button events, not native OS input.");
    }

    private void PrepareControlledState()
    {
        GameManager game = GameManager.Instance;
        game.State.RestoreCoreForEditor(600, TechLevel.Archotech, new ExpantaNum(1e6), 0L);
        foreach (ResearchDef def in DataBase<ResearchDef>.All)
        {
            var paid = new Dictionary<ResourceDef, ExpantaNum>();
            foreach (var cost in def.ResourceRequirements)
                if (cost.First != null && cost.Second > ExpantaNum.Zero) paid[cost.First] = cost.Second;
            ResearchState state = ResearchManager.Instance.GetState(def);
            state.RestoreForEditor(state.BaseCost, true, true, paid);
        }
        foreach (WorkshopUpgradeState state in WorkshopManager.Instance.States.Values) state.SetPurchasedForEditor(true);
        ProgressionModifierManager.Rebuild(new List<ResearchState>(ResearchManager.Instance.States.Values), new List<WorkshopUpgradeState>(WorkshopManager.Instance.States.Values));
        foreach (ResourceDef def in DataBase<ResourceDef>.All) ResourceManager.Instance.SetAmount(def, new ExpantaNum(12345678));
        foreach (BuildingDef def in DataBase<BuildingDef>.All)
            BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(def), ExpantaNum.One);
        foreach (SectorDef def in DataBase<SectorDef>.All)
        {
            SectorState state = game.Sectors.GetState(def);
            state.SetUnlockedForEditor(true); state.SetOccupiedForEditor(true);
            state.SetCampaignProgressForEditor(ExpantaNum.One);
            state.SetInteriorExplorationForEditor(ExpantaNum.One, false, false);
        }
        game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
        game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
        BuildingManager.Instance.PrepareTickResourceSatisfactionForEditor(1d);
        TutorialManager.Current.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            CompletedStageIds = new List<string> { "resources", "building", "population", "research", "era", "power", "logistics", "production" }
        }, game.State.TechLevel);
        for (int i = 0; i < 30; i++) { TutorialManager.Current.SkipCurrentStage(); ui.RefreshOnboardingForEditor(); }
        ui.RefreshNavigationVisibilityForEditor(); ui.RefreshUI();
        Toggle owned = ui.transform.Find("SafeAreaRoot/Content/PageTool/WorkshopFilters/ShowOwned")?.GetComponent<Toggle>();
        if (owned != null) owned.isOn = true;
    }

    private Transform Rows(string page) => ui.transform.Find("SafeAreaRoot/Content/PageHost/" + page + "/DataRows");
    private Button FindCard(string page, string label) => Rows(page).Cast<Transform>().First(x => x.gameObject.activeInHierarchy && x.GetComponent<Button>() != null && x.Find("Label")?.GetComponent<TMP_Text>()?.text == label).GetComponent<Button>();
    private IEnumerator Page(string page)
    {
        ui.SetPage(page); yield return null;
        ui.RefreshAutomaticUIForEditor(); Canvas.ForceUpdateCanvases(); yield return null;
    }
    private IEnumerator Click(Button button)
    {
        Assert.That(button != null && button.gameObject.activeInHierarchy && button.interactable, Is.True);
        ScrollRect scroll = button.GetComponentsInParent<ScrollRect>(true).FirstOrDefault(x => x != detail && x.content != null && button.transform.IsChildOf(x.content));
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases(); scroll.StopMovement();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, button.transform);
            Vector2 position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + scroll.viewport.rect.center.y - bounds.center.y, 0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height));
            scroll.content.anchoredPosition = position;
        }
        button.onClick.Invoke(); yield return null; Canvas.ForceUpdateCanvases();
    }
    private string Capture(string name)
    {
        detail.GetComponent<UIDetailRequirementScrollGesture>().SetNormalizedPosition(1f);
        Canvas.ForceUpdateCanvases();
        string text = string.Join("\n", detail.content.GetComponentsInChildren<TMP_Text>().Where(x => x.gameObject.activeInHierarchy).Select(x => x.text));
        File.WriteAllText(Path.Combine(output, name + ".txt"), text);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; }
        return text;
    }
    private void ResizeCapture(int width, int height)
    {
        camera.targetTexture = null;
        target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
        target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
        pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        Canvas.ForceUpdateCanvases();
    }

    private void AssertTopHappiness()
    {
        ui.RefreshTopInfoForEditor();
        TMP_Text value = ui.transform.Find("SafeAreaRoot/TopStatusBar/TopInfo/Happiness/Value")?.GetComponent<TMP_Text>();
        if (value == null)
            foreach (TMP_Text candidate in ui.GetComponentsInChildren<TMP_Text>(true))
                if (candidate.name == "Value" && candidate.transform.parent.name == "Happiness") { value = candidate; break; }
        Assert.That(value, Is.Not.Null);
        GameState game = GameManager.Instance.State;
        Assert.That(value.text, Is.EqualTo((game.HappinessMultiplier * 100).ToDouble().ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "%"));
        Assert.That(value.text, Does.Not.Contain("HudProductionEfficiency").And.Not.Contain("("));
        value.ForceMeshUpdate(true, true);
        Assert.That(value.textInfo.lineCount, Is.EqualTo(1), "HUD happiness stays on one line.");
        Assert.That(value.isTextOverflowing, Is.False, "The happiness percentage fits the authored HUD slot.");
    }
    private void AssertProductionEntry()
    {
        Assert.That(detail.GetComponentsInChildren<Button>(true).Any(x => x.name == "Mode" && x.gameObject.activeInHierarchy && x.interactable), Is.True, "The independent production-tree entry remains available.");
    }

    private void AssertDetailNumbersStayOnOneLine()
    {
        foreach (TMP_Text amount in detail.content.GetComponentsInChildren<TMP_Text>())
        {
            if (amount.name != "Amount" || !amount.transform.parent.name.StartsWith("Requirement_", StringComparison.Ordinal)) continue;
            TMP_Text label = amount.transform.parent.Find("Label").GetComponent<TMP_Text>();
            amount.ForceMeshUpdate(); label.ForceMeshUpdate();
            Assert.That(amount.enableWordWrapping, Is.False);
            Assert.That(amount.textInfo.lineCount, Is.EqualTo(1), "A resource requirement must stay on one line: " + amount.text);
            Assert.That(amount.isTextOverflowing, Is.False, "The complete requirement and inventory must fit.");
            Assert.That(label.textInfo.lineCount, Is.EqualTo(1));
            Assert.That(Mathf.Abs(amount.textInfo.lineInfo[0].baseline - label.textInfo.lineInfo[0].baseline), Is.LessThan(8f), "Resource label and amount share a visual baseline.");
        }
    }
    private void DumpSectorImages()
    {
        Transform root = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI");
        var report = new StringBuilder("Active Images under sector DetailUI; controlled 2640x1200 render\n");
        foreach (Image image in root.GetComponentsInChildren<Image>())
        {
            string path = image.name;
            for (Transform parent = image.transform.parent; parent != null && parent != ui.transform; parent = parent.parent) path = parent.name + "/" + path;
            Vector3[] corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
            report.AppendLine($"{path} | color={image.color} | sprite={(image.sprite == null ? "none" : image.sprite.name)} | rect={image.rectTransform.rect} | world={corners[0]} .. {corners[2]} | raycast={image.raycastTarget}");
        }
        string text = report.ToString();
        File.WriteAllText(Path.Combine(output, "04-sector-active-images.txt"), text);
        Debug.Log("[PlayerLayoutRequest]\n" + text);
    }
}
