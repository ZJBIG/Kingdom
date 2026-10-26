using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class SectorBuildingPlayModeTests
{
    private string saveRoot;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;
    private UIButtonSoundManager initialSound;
    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        initialSound = Object.FindObjectOfType<UIButtonSoundManager>(true);
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        saveRoot = Path.Combine(projectRoot, "Temp", "KingdomSectorBuildingPlayModeTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("SectorBuildingTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            yield return SceneManager.UnloadSceneAsync(sample);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        UIButtonSoundManager sound = Object.FindObjectOfType<UIButtonSoundManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        if (sound != null && sound != initialSound) Object.DestroyImmediate(sound.gameObject);
        Assert.That(Object.FindObjectOfType<SaveManager>(true), Is.Null,
            "Destroy every save writer before releasing the isolated save root.");
        SaveManager.ClearSaveRootOverrideForTests();
        if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot))
        {
            Directory.Delete(saveRoot, true);
            Assert.That(Directory.Exists(saveRoot), Is.False);
        }
        saveRoot = null;
    }

    [UnityTest]
    public IEnumerator SectorNavigationUnlocksAfterRequiredResearchAndOpensPage()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        GameManager game = Object.FindObjectOfType<GameManager>();
        Assert.That(game, Is.Not.Null);
        ResearchManager researchManager = ResearchManager.Instance;
        ResourceManager resourceManager = ResourceManager.Instance;

        game.InitializeNewGameForEditor();
        researchManager.ResetForPerformanceTest();
        BuildingManager buildings = BuildingManager.Instance;
        buildings.SetAmountAndRatesForEditor(
            buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
        Assert.That(game.State.HasBuiltHousing, Is.True,
            "The controlled research fixture must include the housing unlock milestone.");
        GrantResearchTestResources(resourceManager);
        root.RefreshNavigationVisibilityForEditor();

        Button sectorsButton = root.transform.Find(
            "SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Sectors")?.GetComponent<Button>();
        Assert.That(sectorsButton.gameObject.activeSelf, Is.False,
            "The Sectors navigation entry must remain hidden before its research gates complete.");

        CompleteResearchAndPrerequisites(game, researchManager, resourceManager,
            DataBase<ResearchDef>.Find("HomeSystemSurvey"), new HashSet<string>());
        root.RefreshNavigationVisibilityForEditor();
        yield return null;

        Assert.That(researchManager.IsResearchCompleted("HomeSystemSurvey"), Is.True);
        Assert.That(sectorsButton.gameObject.activeSelf, Is.True,
            "Completing HomeSystemSurvey must reveal the home-system Sectors entry.");

        CompleteResearchAndPrerequisites(game, researchManager, resourceManager,
            DataBase<ResearchDef>.Find("InterstellarNavigation"), new HashSet<string>());
        root.RefreshNavigationVisibilityForEditor();
        yield return null;

        Assert.That(researchManager.IsResearchCompleted("HomeSystemSurvey"), Is.True);
        Assert.That(researchManager.IsResearchCompleted("InterstellarNavigation"), Is.True);
        Assert.That(sectorsButton.gameObject.activeSelf, Is.True,
            "Completing the sector research gates must reveal the Sectors navigation entry.");
        Assert.That(sectorsButton.interactable, Is.True);

        sectorsButton.onClick.Invoke();
        yield return null;
        Transform sectorsPage = root.transform.Find("SafeAreaRoot/Content/PageHost/Sectors");
        Assert.That(sectorsPage.gameObject.activeSelf, Is.True,
            "The enabled Sectors button must navigate to the Sectors page.");
    }

    [UnityTest]
    public IEnumerator SectorBuildingMenuIsGatedAndReflowsRows()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        GameManager game = Object.FindObjectOfType<GameManager>();
        game.InitializeNewGameForEditor();
        ResearchManager.Instance.ResetForPerformanceTest();
        SectorDef moon = DataBase<SectorDef>.Find("AzurePool");
        SectorState moonState = game.Sectors.GetState(moon);
        SectorDef prerequisite = DataBase<SectorDef>.Find("DawnRing");
        game.Sectors.GetState(prerequisite).SetOccupiedForEditor(true);
        SectorBuildingDef hub = DataBase<SectorBuildingDef>.Find("EarthMoonLogisticsHub");
        Assert.That(hub, Is.Not.Null);
        foreach (BuildingState state in BuildingManager.Instance.States.Values)
            state.SetAmountForEditor(ExpantaNum.Zero);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, ExpantaNum.Zero);
        moonState.SetOccupiedForEditor(false);

        root.SetPage("Sectors");
        yield return null;

        Transform rows = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Sectors/DataRows");
        Transform moonRow = rows.Find("SectorRow_AzurePool");
        Transform gatedBuildings = moonRow.Find("Buildings");
        Assert.That(gatedBuildings.gameObject.activeSelf, Is.False);

        moonState.SetOccupiedForEditor(true);
        root.RefreshSectorRowSummariesForEditor();
        yield return null;
        Transform buildingButton = moonRow.Find("Buildings");
        Assert.That(buildingButton.gameObject.activeSelf, Is.True);

        float before = moonRow.GetComponent<RectTransform>().rect.height;
        GameObject authoredRow = Resources.Load<GameObject>("UI/Kingdom/KingdomUISectorRow");
        Assert.That(authoredRow, Is.Not.Null);
        float authoredHeight = authoredRow.GetComponent<LayoutElement>().preferredHeight;
        Assert.That(authoredHeight, Is.GreaterThan(0f));
        Assert.That(before, Is.EqualTo(authoredHeight).Within(.5f),
            "Collapsed rows must use the live authored preferred height.");
        RectTransform nextRow = null;
        for (int i = moonRow.GetSiblingIndex() + 1; i < rows.childCount; i++)
            if (rows.GetChild(i).gameObject.activeInHierarchy)
            {
                nextRow = rows.GetChild(i).GetComponent<RectTransform>();
                break;
            }
        Assert.That(nextRow, Is.Not.Null, "The fixture must exercise reflow of a following visible row.");
        float nextRowBefore = nextRow.anchoredPosition.y;
        AssertRowsDoNotOverlap((RectTransform)moonRow, nextRow);
        buildingButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        float expandedHeight = moonRow.GetComponent<RectTransform>().rect.height;
        Assert.That(expandedHeight, Is.GreaterThan(before));
        Transform menu = moonRow.Find("SectorBuildingMenu");
        float menuHeight = menu.GetComponent<RectTransform>().rect.height;
        Assert.That(expandedHeight - before, Is.EqualTo(menuHeight).Within(1f),
            "Expanded row reserves the full authored menu height.");
        Assert.That(menu.GetComponent<RectTransform>().anchoredPosition.y,
            Is.EqualTo(-before).Within(.5f), "The menu must begin below the authored collapsed row.");
        Assert.That(nextRowBefore - nextRow.anchoredPosition.y,
            Is.EqualTo(expandedHeight - before).Within(1f), "Following rows move by the expansion height.");
        AssertRowsDoNotOverlap((RectTransform)moonRow, nextRow);
        Transform build = menu.Find("EarthMoonLogisticsHub/Build");
        Assert.That(build.GetComponent<UnityEngine.UI.Button>().interactable, Is.False);

        PrepareBuildableSectorBuilding(game, hub);
        root.RefreshSectorRowSummariesForEditor();
        yield return null;
        Assert.That(build.GetComponent<UnityEngine.UI.Button>().interactable, Is.True);
        build.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;

        menu = moonRow.Find("SectorBuildingMenu");
        Transform card = menu.Find("EarthMoonLogisticsHub");
        Assert.That(card.Find("Amount").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("1/1"));
        Assert.That(card.Find("Build").gameObject.activeSelf, Is.False,
            "A completed facility must remove its construction action.");
        Assert.That(card.Find("ToggleEnabled").GetComponent<UnityEngine.UI.Button>().interactable, Is.True);

        buildingButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        Assert.That(moonRow.GetComponent<RectTransform>().rect.height, Is.LessThan(expandedHeight));
        Assert.That(moonRow.GetComponent<RectTransform>().rect.height,
            Is.EqualTo(before).Within(.5f), "Collapsing must restore the original authored row height.");
        Assert.That(nextRow.anchoredPosition.y, Is.EqualTo(nextRowBefore).Within(.5f),
            "Collapsing must restore the following row's position.");
        AssertRowsDoNotOverlap((RectTransform)moonRow, nextRow);

        root.SetPage("Buildings");
        yield return null;
        Transform buildingRows = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Buildings/DataRows");
        Assert.That(buildingRows.Find("EarthMoonLogisticsHub"), Is.Null);

        moonState.SetOccupiedForEditor(false);
        root.SetPage("Sectors");
        root.RefreshSectorRowSummariesForEditor();
        yield return null;
        Transform gatedButton = moonRow.Find("Buildings");
        Assert.That(gatedButton.gameObject.activeSelf, Is.False);
        Transform retainedMenu = moonRow.Find("SectorBuildingMenu");
        if (retainedMenu != null)
            Assert.That(retainedMenu.gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator BuildingDetailsShareNonzeroPropertiesAndHudIconsIncludingFoodStorage()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        GameManager game = GameManager.Instance;
        game.InitializeNewGameForEditor();
        SimulationManager.Instance.SetRunning(false);
        game.AdvanceTechLevelForEditor(TechLevel.Archotech);
        KingdomUIRoot ui = Object.FindObjectOfType<KingdomUIRoot>();
        ui.SetPage("Buildings");
        bool foodStorage = false, foodRate = false, research = false, power = false, logistics = false, sector = false;
        foreach (bool full in new[] { false, true })
        {
            ui.transform.Find("SafeAreaRoot/Content/PageTool/BuildingControls/ShowDetails").GetComponent<Toggle>().isOn = full;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            if (!(building is SectorBuildingDef) && building.FoodCapacityGranted <= ExpantaNum.Zero &&
                building.FoodProductionRate == ExpantaNum.Zero && building.FoodConsumptionRate == ExpantaNum.Zero &&
                building.ResearchPowerGranted <= ExpantaNum.Zero && building.PowerContribution == ExpantaNum.Zero &&
                building.LogisticsContribution == ExpantaNum.Zero) continue;
            ui.ShowBuildingDetailsForEditor(building);
            yield return null;
            TMPro.TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMPro.TMP_Text>();
            Canvas.ForceUpdateCanvases();
            body.ForceMeshUpdate();
            Assert.That(body.text, Does.Contain("每座建筑属性"));
            if (building is SectorBuildingDef)
            {
                sector = true;
                Assert.That(body.text, Does.Not.Contain("土地"));
            }
            if (building.FoodCapacityGranted > ExpantaNum.Zero)
            {
                foodStorage = true;
                Assert.That(body.text, Does.Contain("<sprite=\"HudFoodStorage\" index=0>+"));
            }
            if (building.ResearchPowerGranted > ExpantaNum.Zero)
            {
                research = true;
                Assert.That(body.text, Does.Contain("<sprite=\"HudResearchPower\" index=0>+"));
            }
            if (building.PowerContribution != ExpantaNum.Zero)
            {
                power = true;
                Assert.That(body.text, Does.Contain("<sprite=\"HudPower\" index=0>" + (building.PowerContribution > ExpantaNum.Zero ? "+" : "-")));
            }
            if (building.LogisticsContribution != ExpantaNum.Zero)
            {
                logistics = true;
                Assert.That(body.text, Does.Contain("<sprite=\"HudLogistics\" index=0>" + (building.LogisticsContribution > ExpantaNum.Zero ? "+" : "-")));
            }
            string basic = body.text.Substring(body.text.IndexOf("每座建筑属性", StringComparison.Ordinal));
            int efficiency = basic.IndexOf("<sprite=\"HudProductionEfficiency\"", StringComparison.Ordinal);
            if (efficiency >= 0) basic = basic.Substring(0, efficiency);
            int nextStage = basic.IndexOf("下一阶段", StringComparison.Ordinal);
            if (nextStage >= 0) basic = basic.Substring(0, nextStage);
            foreach (var property in new[] {
                new { Icon = "HudPower", Value = building.PowerContribution },
                new { Icon = "HudLogistics", Value = building.LogisticsContribution } })
            {
                string tag = "<sprite=\"" + property.Icon + "\" index=0>";
                int count = 0;
                foreach (string line in basic.Split('\n'))
                {
                    if (!line.StartsWith(tag, StringComparison.Ordinal)) continue;
                    count++;
                    Assert.That(line, Does.Not.Contain(":").And.Not.Contain("：").And.Not.Contain("/s"));
                }
                Assert.That(count, Is.EqualTo(property.Value == ExpantaNum.Zero ? 0 : 1),
                    "Each discrete attribute is hidden at zero or displayed once with its sign.");
            }
            Assert.That(body.text, Does.Not.Contain("电力总供需").And.Not.Contain("物流总供需"));
            foreach (string line in basic.Split('\n'))
                if (line.StartsWith("<sprite=\"HudFood\" index=0>", StringComparison.Ordinal))
                {
                    foodRate = true;
                    Assert.That(line.TrimEnd(), Does.EndWith("/s"), "Food is a rate, unlike power/logistics capacity.");
                }
            Assert.That(body.text, Does.Not.Contain("电力需求").And.Not.Contain("物流需求").And.Not.Contain("研究力供给"));
            // TMP 3.0.9 overwrites spriteCount with an unset field after generating the mesh.
            foreach (string icon in new[] { "HudProductivity", "HudPopulation", "HudFoodStorage", "HudFood", "HudResearchPower", "HudPower", "HudLogistics" })
            {
                if (!body.text.Contains("<sprite=\"" + icon + "\"")) continue;
                bool rendered = false;
                for (int i = 0; i < body.textInfo.characterCount; i++)
                {
                    TMPro.TMP_CharacterInfo character = body.textInfo.characterInfo[i];
                    if (character.elementType != TMPro.TMP_TextElementType.Sprite || character.spriteAsset.name != icon) continue;
                    rendered |= character.isVisible && (character.topRight - character.bottomLeft).sqrMagnitude > 0f;
                }
                Assert.That(rendered, Is.True, $"{building.Id}, full={full}: {icon} must resolve to a visible sprite quad.");
            }
        }
        }
        Assert.That(foodStorage && foodRate && research && power && logistics && sector, Is.True,
            "Current definitions must exercise all requested property types.");
        ui.ShowBuildingDetailsForEditor(DataBase<BuildingDef>.Find("OrbitalStation"));
        yield return null;
    }

    [UnityTest]
    public IEnumerator MegastructureDetailsTrackNextStageAndInventoryWithoutReopening()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        GameManager game = GameManager.Instance;
        game.InitializeNewGameForEditor();
        SimulationManager.Instance.SetRunning(false);
        game.AdvanceTechLevelForEditor(TechLevel.Archotech);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        foreach (SectorDef sector in DataBase<SectorDef>.All)
        {
            SectorState state = game.Sectors.GetState(sector);
            state.SetUnlockedForEditor(true);
            state.SetOccupiedForEditor(true);
            state.SetInteriorExplorationForEditor(ExpantaNum.One, false, false);
        }
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1e12d));
        TutorialManager.Current.RestoreSaveData(null, game.State.TechLevel);
        KingdomUIRoot ui = Object.FindObjectOfType<KingdomUIRoot>();
        ui.RefreshOnboardingForEditor();
        ui.SetPage("Sectors");
        yield return null;
        SectorBuildingDef project = DataBase<SectorBuildingDef>.Find("DawnDysonSphere");
        Transform row = ui.transform.Find("SafeAreaRoot/Content/PageHost/Sectors/DataRows/SectorRow_" + project.Sector.Id);
        row.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Transform card = row.Find("SectorBuildingMenu/" + project.Id);
        Assert.That(card.Find("Amount").gameObject.activeSelf, Is.False);
        Assert.That(card.Find("Build/Label").gameObject.activeSelf, Is.False);
        card.Find("Name").GetComponent<Button>().onClick.Invoke();
        card.Find("Build").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(.35f);
        ui.RefreshAutomaticUIForEditor();
        Transform detail = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent");
        TMPro.TMP_Text body = detail.Find("Body").GetComponent<TMPro.TMP_Text>();
        WorkshopUpgradeDef next = null;
        foreach (WorkshopUpgradeDef candidate in DataBase<WorkshopUpgradeDef>.All)
            if (candidate.RequiredBuilding == project && (next == null || candidate.SortOrder < next.SortOrder)) next = candidate;
        Assert.That(next, Is.Not.Null);
        Assert.That(body.text, Does.Contain(next.Label).And.Not.Contain("土地"));
        Assert.That(body.text, Does.Contain(project.Label).And.Contain(project.Description).And.Contain(next.Description));
        Assert.That(WorkshopManager.Instance.IsSystemUnlocked, Is.False);
        Assert.That(body.text, Does.Not.Contain("工坊尚未解锁"));
        ResourceDef material = next.ResourceRequirements[0].First;
        ResourceManager.Instance.SetAmount(material, new ExpantaNum(123));
        yield return new WaitForSecondsRealtime(.35f);
        ui.RefreshAutomaticUIForEditor();
        bool found = false;
        foreach (TMPro.TMP_Text label in detail.GetComponentsInChildren<TMPro.TMP_Text>())
            if (label.name == "Label" && label.transform.parent.name.StartsWith("Requirement_") && label.text == material.Label)
            {
                string amount = label.transform.parent.Find("Amount").GetComponent<TMPro.TMP_Text>().text;
                Assert.That(amount, Does.Contain("123"), "Next-stage inventory must refresh while the detail stays selected.");
                found = true;
            }
        Assert.That(found, Is.True);
        yield return CaptureSectorCornerLayout(ui, (RectTransform)row, "megastructure");
        row.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        yield return null;
        yield return CaptureSectorCornerLayout(ui, (RectTransform)row, "sector-collapsed");
    }

    private static IEnumerator CaptureSectorCornerLayout(KingdomUIRoot root, RectTransform row, string category = "sector-expanded")
    {
        Canvas canvas = root.GetComponent<Canvas>();
        RenderMode previousMode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance;
        GameObject cameraObject = new GameObject("SectorLayoutEvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            foreach (Vector2Int size in new[] { new Vector2Int(2640, 1200), new Vector2Int(1760, 800) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                RenderTexture previousTarget = RenderTexture.active;
                try
                {
                    target.Create();
                    camera.targetTexture = target;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    RectTransform button = row.Find("Buildings").GetComponent<RectTransform>();
                    Image buttonSurface = button.GetComponent<Image>();
                    Assert.That(buttonSurface.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Sectors/DetailResourcePanel")));
                    Assert.That(buttonSurface.color.a, Is.GreaterThan(.99f), "Expand control has a solid surface.");
                    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(row, button);
                    Assert.That(bounds.max.x, Is.LessThan(row.rect.xMax));
                    Assert.That(bounds.min.x, Is.GreaterThan(row.rect.center.x));
                    Assert.That(bounds.max.y, Is.LessThan(row.rect.yMax));
                    RectTransform name = row.Find("Label").GetComponent<RectTransform>();
                    Bounds title = RectTransformUtility.CalculateRelativeRectTransformBounds(row, name);
                    Assert.That(title.max.x, Is.LessThan(bounds.min.x));
                    RectTransform menu = row.Find("SectorBuildingMenu").GetComponent<RectTransform>();
                    Assert.That(menu.Find("Heading"), Is.Null);
                    Assert.That(menu.Find("Divider"), Is.Null, "Sector nesting uses its own shell rather than a side rail.");
                    RectTransform first = menu.GetChild(0).GetComponent<RectTransform>();
                    Assert.That(first.Find("Metadata"), Is.Null, "No redundant construction-status subtitle remains.");
                    Assert.That(first.anchoredPosition.y, Is.GreaterThan(-first.rect.height * .2f),
                        "No empty heading band remains above the first facility.");
                    Bounds card = new Bounds(menu.InverseTransformPoint(first.TransformPoint(first.rect.center)),
                        first.rect.size);
                    Assert.That(card.size.x / menu.rect.width, Is.InRange(.85f, .94f),
                        "Facilities must be visibly inset from the parent sector shell.");
                    Assert.That(Mathf.Abs(card.center.x - menu.rect.center.x), Is.LessThan(1f),
                        "Facility insets are balanced on both sides.");
                    RectTransform artwork = first.Find("BuildingIcon").GetComponent<RectTransform>();
                    Bounds artworkBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(first, artwork);
                    Assert.That(artworkBounds.min.x - first.rect.xMin, Is.GreaterThan(artwork.rect.width * .2f));
                    Assert.That(first.rect.yMax - artworkBounds.max.y, Is.GreaterThan(artwork.rect.height * .2f));
                    Assert.That(artworkBounds.min.y - first.rect.yMin, Is.GreaterThan(artwork.rect.height * .2f));
                    Assert.That(Mathf.Abs(artworkBounds.center.y - first.rect.center.y), Is.LessThan(1f));
                    foreach (string actionName in new[] { "Build", "ToggleEnabled", "Deconstruct" })
                    {
                        RectTransform action = first.Find(actionName).GetComponent<RectTransform>();
                        Bounds actionBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(first, action);
                        Assert.That(first.rect.xMax - actionBounds.max.x, Is.GreaterThan(action.rect.width * .2f));
                    }
                    RectTransform construct = first.Find("Build").GetComponent<RectTransform>();
                    RectTransform demolish = first.Find("Deconstruct").GetComponent<RectTransform>();
                    RectTransform enable = first.Find("ToggleEnabled").GetComponent<RectTransform>();
                    Assert.That(construct.anchoredPosition.x, Is.LessThan(demolish.anchoredPosition.x));
                    Assert.That(demolish.anchoredPosition.x, Is.LessThan(enable.anchoredPosition.x));
                    RectTransform last = menu.GetChild(menu.childCount - 1).GetComponent<RectTransform>();
                    Vector3 lastBottom = menu.InverseTransformPoint(last.TransformPoint(
                        new Vector3(last.rect.center.x, last.rect.yMin, 0f)));
                    Assert.That(lastBottom.y - menu.rect.yMin, Is.GreaterThan(artwork.rect.height * .25f),
                        "The last facility must clear the bottom of its parent shell.");
                    foreach (Transform sibling in row.parent)
                    {
                        RectTransform sector = sibling as RectTransform;
                        RectTransform icon = sibling.Find("SectorIcon") as RectTransform;
                        if (sector == null || icon == null) continue;
                        RectTransform sectorMenu = sibling.Find("SectorBuildingMenu") as RectTransform;
                        float headerHeight = sectorMenu == null ? sector.rect.height : -sectorMenu.anchoredPosition.y;
                        Bounds iconBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(sector, icon);
                        float headerCenter = sector.rect.yMax - headerHeight * .5f;
                        Assert.That(Mathf.Abs(iconBounds.center.y - headerCenter), Is.LessThan(1f));
                        Assert.That(iconBounds.min.x - sector.rect.xMin, Is.GreaterThan(icon.rect.width * .3f));
                        Assert.That(sector.rect.yMax - iconBounds.max.y, Is.GreaterThan(icon.rect.height * .3f));
                        Assert.That(iconBounds.min.y - (sector.rect.yMax - headerHeight), Is.GreaterThan(icon.rect.height * .3f));
                    }
                    foreach (Image image in row.parent.GetComponentsInChildren<Image>())
                    {
                        if (image.name != "Nebula") continue;
                        AspectRatioFitter fit = image.GetComponent<AspectRatioFitter>();
                        Assert.That(fit.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
                        Assert.That(image.rectTransform.rect.width / image.rectTransform.rect.height,
                            Is.EqualTo(fit.aspectRatio).Within(.001f), "Expanded and collapsed backgrounds retain their image aspect.");
                    }
                    ScrollRect scroll = root.transform.Find("SafeAreaRoot/Content/PageHost").GetComponent<ScrollRect>();
                    scroll.StopMovement();
                    Bounds rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, row);
                    scroll.content.anchoredPosition += Vector2.up * (scroll.viewport.rect.yMax - rowBounds.max.y - 8f);
                    Canvas.ForceUpdateCanvases();
                    AssertDetailResourceRows(root);
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                    pixels.Apply();
                    string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "building-detail-raw-20261026"));
                    Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, category + "-" + size.x + "x" + size.y + ".png");
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    Debug.Log("[SectorCornerLayout] " + size + " button=" + bounds + " card=" + card + " screenshot=" + path);
                }
                finally
                {
                    RenderTexture.active = previousTarget;
                    camera.targetTexture = null;
                    target.Release();
                    Object.Destroy(target);
                    Object.Destroy(pixels);
                }
            }
        }
        finally
        {
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousDistance;
            Object.Destroy(cameraObject);
        }
    }

    private static void AssertDetailResourceRows(KingdomUIRoot root)
    {
        Sprite shell = Resources.Load<Sprite>("Art/UI/Sectors/DetailResourcePanel");
        int rows = 0;
        Transform detail = root.transform.Find("SafeAreaRoot/DetailPanel/DetailUI");
        foreach (TMPro.TMP_Text amount in detail.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            if (amount.name != "Amount") continue;
            Transform row = amount.transform.parent;
            if (!row.name.StartsWith("Flow_", StringComparison.Ordinal) &&
                !row.name.StartsWith("Requirement_", StringComparison.Ordinal)) continue;
            rows++;
            Image background = row.GetComponent<Image>();
            Assert.That(background.sprite, Is.SameAs(shell));
            Assert.That(background.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(background.raycastTarget, Is.False);
            TMPro.TMP_Text label = row.Find("Label").GetComponent<TMPro.TMP_Text>();
            amount.ForceMeshUpdate();
            label.ForceMeshUpdate();
            Assert.That(amount.textInfo.lineCount, Is.EqualTo(1));
            Assert.That(amount.isTextOverflowing, Is.False, amount.text);
            Assert.That(label.textInfo.lineCount, Is.EqualTo(1));
            Assert.That(Mathf.Abs(amount.textInfo.lineInfo[0].baseline - label.textInfo.lineInfo[0].baseline), Is.LessThan(8f));
            if (row.name.StartsWith("Flow_", StringComparison.Ordinal))
                Assert.That(amount.text, Does.EndWith("/s"));
        }
        Assert.That(rows, Is.GreaterThan(0), "The screenshot fixture must exercise the shared resource shell.");
    }

    private static void AssertRowsDoNotOverlap(RectTransform row, RectTransform following)
    {
        var rowCorners = new Vector3[4];
        var followingCorners = new Vector3[4];
        row.GetWorldCorners(rowCorners);
        following.GetWorldCorners(followingCorners);
        Assert.That(followingCorners[1].y, Is.LessThanOrEqualTo(rowCorners[0].y + .5f),
            "A following visible row must start below the full current row, including its expanded menu.");
    }

    [UnityTest]
    public IEnumerator SectorFacilityToggleUsesAuthoredButtonAndPersistsWithoutRefundOrDemolition()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        GameManager game = Object.FindObjectOfType<GameManager>();
        game.InitializeNewGameForEditor();
        SimulationManager.Instance.SetRunning(false);
        game.AdvanceTechLevelForEditor(TechLevel.Archotech);
        foreach (ResearchDef definition in DataBase<ResearchDef>.All)
        {
            var paid = new Dictionary<ResourceDef, ExpantaNum>();
            foreach (var cost in definition.ResourceRequirements)
                paid[cost.First] = cost.Second;
            ResearchState state = ResearchManager.Instance.GetState(definition);
            state.RestoreForEditor(state.BaseCost, true, true, paid);
        }
        foreach (SectorDef definition in DataBase<SectorDef>.All)
        {
            SectorState state = game.Sectors.GetState(definition);
            state.SetUnlockedForEditor(true);
            state.SetOccupiedForEditor(true);
            state.SetCampaignProgressForEditor(1);
            if (definition.InteriorExploration != null)
                state.SetInteriorExplorationForEditor(1);
        }
        SectorBuildingDef hub = DataBase<SectorBuildingDef>.Find("EarthMoonLogisticsHub");
        BuildingManager buildings = BuildingManager.Instance;
        foreach (var cost in hub.ResourceRequirements)
            ResourceManager.Instance.SetAmount(cost.First, cost.Second * 2);
        buildings.SetAmountAndRatesForEditor(buildings.GetState(hub), 1);
        ResourceDef material = hub.ResourceRequirements[0].First;
        ResourceManager.Instance.SetAmount(material, 10000);
        ExpantaNum inventory = ResourceManager.Instance.GetAmount(material);
        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        root.SetPage("Sectors");
        root.RefreshSectorRowSummariesForEditor();
        yield return null;
        Transform row = root.transform.Find("SafeAreaRoot/Content/PageHost/Sectors/DataRows/SectorRow_AzurePool");
        Transform menu = row.Find("SectorBuildingMenu");
        Assert.That(menu.gameObject.activeSelf, Is.False, "Each sector starts collapsed.");
        Transform otherMenu = null;
        foreach (Transform sibling in row.parent)
        {
            if (sibling == row) continue;
            Transform candidate = sibling.Find("SectorBuildingMenu");
            if (candidate != null) { otherMenu = candidate; break; }
        }
        Assert.That(otherMenu, Is.Not.Null, "The fixture exercises independent sector sections.");
        row.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(menu.gameObject.activeSelf, Is.True);
        Assert.That(otherMenu.gameObject.activeSelf, Is.False, "Expanding one sector leaves its neighbor collapsed.");
        root.SetPage("Overview");
        root.SetPage("Sectors");
        yield return null;
        row = root.transform.Find("SafeAreaRoot/Content/PageHost/Sectors/DataRows/SectorRow_AzurePool");
        Assert.That(row.Find("SectorBuildingMenu").gameObject.activeSelf, Is.True,
            "Returning to the sector page retains this session's expansion state.");
        TutorialManager.Current.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            CompletedStageIds = new List<string> { "resources", "building", "population", "research", "era", "power", "logistics", "production" }
        }, game.State.TechLevel);
        for (int i = 0; i < 30; i++) { TutorialManager.Current.SkipCurrentStage(); root.RefreshOnboardingForEditor(); }
        yield return null;
        row.Find("SectorBuildingMenu/EarthMoonLogisticsHub/Name").GetComponent<Button>().onClick.Invoke();
        yield return CaptureSectorCornerLayout(root, (RectTransform)row);
        Transform card = row.Find("SectorBuildingMenu/EarthMoonLogisticsHub");
        Assert.That(card.Find("Deconstruct").gameObject.activeSelf, Is.True);
        Assert.That(card.Find("Deconstruct").GetComponent<Button>().interactable, Is.True);
        Button toggle = card.Find("ToggleEnabled").GetComponent<Button>();
        Assert.That(toggle.interactable, Is.True);
        Assert.That(toggle.transform.Find("Label"), Is.Null, "Facility state actions use icons rather than text.");
        Assert.That(toggle.transform.Find("PauseIcon").gameObject.activeSelf, Is.True);
        Assert.That(toggle.transform.Find("ResumeIcon").gameObject.activeSelf, Is.False);
        Assert.That(toggle.transform.Find("PauseIcon/Glyph").GetComponent<Image>().sprite,
            Is.SameAs(Resources.Load<Sprite>("Art/UI/Buttons/FacilityPause")));
        Assert.That(toggle.transform.Find("ResumeIcon/Glyph").GetComponent<Image>().sprite,
            Is.SameAs(Resources.Load<Sprite>("Art/UI/Buttons/FacilityResume")));
        GameObject ordinaryCard = Resources.Load<GameObject>("UI/Kingdom/KingdomUIBuildingCard");
        RectTransform ordinaryAction = ordinaryCard.transform.Find("DeconstructButton").GetComponent<RectTransform>();
        Assert.That(Vector2.Distance(toggle.GetComponent<RectTransform>().sizeDelta, ordinaryAction.sizeDelta),
            Is.LessThan(.01f), "Facility state buttons match ordinary building action dimensions.");
        Image ordinaryStopSurface = ordinaryAction.GetComponent<Image>();
        Assert.That(((Image)toggle.targetGraphic).sprite, Is.EqualTo(ordinaryStopSurface.sprite));
        Assert.That(Vector4.Distance(toggle.targetGraphic.color, ordinaryStopSurface.color), Is.LessThan(.01f));
        toggle.onClick.Invoke();
        yield return null;
        Assert.That(buildings.GetState(hub).IsEnabled, Is.False);
        Transform disabledAction = row.Find("SectorBuildingMenu/EarthMoonLogisticsHub/ToggleEnabled");
        Assert.That(disabledAction.Find("PauseIcon").gameObject.activeSelf, Is.False);
        Assert.That(disabledAction.Find("ResumeIcon").gameObject.activeSelf, Is.True);
        Image ordinaryStartSurface = ordinaryCard.transform.Find("BuildButton").GetComponent<Image>();
        Button disabledButton = disabledAction.GetComponent<Button>();
        Assert.That(((Image)disabledButton.targetGraphic).sprite, Is.EqualTo(ordinaryStartSurface.sprite));
        Assert.That(Vector4.Distance(disabledButton.targetGraphic.color, ordinaryStartSurface.color), Is.LessThan(.01f));
        Assert.That(buildings.GetState(hub).Amount == ExpantaNum.One, Is.True);
        Assert.That(ResourceManager.Instance.GetAmount(material).ToDouble(), Is.EqualTo(inventory.ToDouble()).Within(1e-6));
        Assert.That(buildings.GetDeconstructionPreview(hub, 1), Is.Not.Null);
        SaveManager save = Object.FindObjectOfType<SaveManager>();
        save.SetMaximumOfflineHoursForEditor(0f);
        Assert.That(save.SaveNow(true), Is.True);
        Assert.That(File.Exists(Path.Combine(saveRoot, "KingdomSave-v1.json")), Is.True);
        Assert.That(buildings.SetSectorBuildingEnabled(hub, true), Is.True);
        Assert.That(save.LoadOrCreateGame(), Is.True);
        Assert.That(save.LastLoadFailed, Is.False);
        Assert.That(buildings.GetState(hub).IsEnabled, Is.False);
        Assert.That(buildings.GetState(hub).Amount == ExpantaNum.One, Is.True);
        buildings.PrepareTickResourceSatisfactionForEditor(1d);
        Assert.That(buildings.GetState(hub).Efficiency.ToDouble(), Is.LessThan(1e-6));
        root.RefreshUI();
        yield return null;
        row = root.transform.Find("SafeAreaRoot/Content/PageHost/Sectors/DataRows/SectorRow_AzurePool");
        card = row.Find("SectorBuildingMenu/EarthMoonLogisticsHub");
        Assert.That(card, Is.Not.Null);
        if (!card.gameObject.activeInHierarchy)
            row.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        card.Find("ToggleEnabled").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(buildings.GetState(hub).IsEnabled, Is.True);
        Assert.That(buildings.GetState(hub).Amount == ExpantaNum.One, Is.True);
        card = row.Find("SectorBuildingMenu/EarthMoonLogisticsHub");
        ExpantaNum beforeDemolition = ResourceManager.Instance.GetAmount(material);
        BuildingDeconstructionPreview preview = buildings.GetDeconstructionPreview(hub, ExpantaNum.One);
        ExpantaNum expectedRefund = preview.Refunds.TryGetValue(material, out ExpantaNum refund) ? refund : ExpantaNum.Zero;
        card.Find("Deconstruct").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(buildings.GetState(hub).Amount == ExpantaNum.Zero, Is.True);
        Assert.That((ResourceManager.Instance.GetAmount(material) - beforeDemolition - expectedRefund).Abs().ToDouble(), Is.LessThan(1e-5d));
        card = row.Find("SectorBuildingMenu/EarthMoonLogisticsHub");
        Assert.That(card.Find("Deconstruct").GetComponent<Button>().interactable, Is.False);
        Assert.That(card.Find("Build").gameObject.activeSelf, Is.True);
        card.Find("Build").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(buildings.GetState(hub).Amount == ExpantaNum.One, Is.True);
        SaveManager.BuildingStateSaveData defaultEntry = JsonUtility.FromJson<SaveManager.BuildingStateSaveData>(
            "{\"BuildingId\":\"EarthMoonLogisticsHub\",\"Amount\":\"1\"}");
        Assert.That(defaultEntry.Disabled, Is.False, "An omitted new flag keeps facilities enabled.");

        SectorBuildingDef staged = DataBase<SectorBuildingDef>.Find("DawnDysonSphere");
        buildings.SetAmountAndRatesForEditor(buildings.GetState(staged), ExpantaNum.One);
        root.RefreshSectorRowSummariesForEditor();
        yield return null;
        Transform stagedRow = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Sectors/DataRows/SectorRow_" + staged.Sector.Id);
        if (!stagedRow.Find("SectorBuildingMenu").gameObject.activeSelf)
            stagedRow.Find("Buildings").GetComponent<Button>().onClick.Invoke();
        Transform stagedCard = stagedRow.Find("SectorBuildingMenu/" + staged.Id);
        Assert.That(stagedCard.Find("Deconstruct").gameObject.activeSelf, Is.False);
        Assert.That(stagedCard.Find("Build").gameObject.activeSelf, Is.True,
            "An unfinished multi-stage project retains construction for the next stage.");
        var completedWorkshop = new SaveManager.WorkshopSaveData { PurchasedUpgradeIds = new List<string>() };
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
            completedWorkshop.PurchasedUpgradeIds.Add(upgrade.Id);
        WorkshopManager.Instance.RestoreSaveDataForEditor(completedWorkshop);
        root.RefreshSectorRowSummariesForEditor();
        yield return null;
        Assert.That(stagedCard.Find("Build").gameObject.activeSelf, Is.False,
            "After all engineering stages are complete, construction disappears.");
    }

    private static void PrepareBuildableSectorBuilding(GameManager game, SectorBuildingDef building)
    {
        game.AdvanceTechLevelForEditor(building.TechLevel);
        game.State.RestorePopulationForEditor(new ExpantaNum(1000d));

        for (int i = 0; i < building.RequiredResearch.Count; i++)
        {
            ResearchDef required = building.RequiredResearch[i];
            ResearchManager.Instance.GetState(required)
                .SetStatusForEditor(ResearchStatus.Completed);
        }

        ExpantaNum constructionCostMultiplier =
            BuildingManager.GetConstructionCostMultiplierForEditor(building);
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = building.ResourceRequirements[i];
            // Derive the fixture from the live construction multiplier. The test
            // must not depend on an exact inventory total or hard-coded costs.
            ResourceManager.Instance.AddAmount(
                requirement.First,
                requirement.Second * constructionCostMultiplier * new ExpantaNum(2d));
        }
    }

    private static void GrantResearchTestResources(ResourceManager resourceManager)
    {
        var totals = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
        {
            if (research == null)
                continue;
            foreach (Pair<ResourceDef, ExpantaNum> requirement in research.ResourceRequirements)
            {
                if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                    continue;
                totals[requirement.First] = totals.TryGetValue(
                    requirement.First, out ExpantaNum total)
                    ? total + requirement.Second
                    : requirement.Second;
            }
        }
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
        {
            if (upgrade == null)
                continue;
            foreach (Pair<ResourceDef, ExpantaNum> requirement in upgrade.ResourceRequirements)
            {
                if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                    continue;
                totals[requirement.First] = totals.TryGetValue(
                    requirement.First, out ExpantaNum total)
                    ? total + requirement.Second
                    : requirement.Second;
            }
        }
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            resourceManager.SetAmount(resource, totals.TryGetValue(
                resource, out ExpantaNum total) ? total : ExpantaNum.Zero);
    }

    private static void CompleteResearchAndPrerequisites(
        GameManager game,
        ResearchManager researchManager,
        ResourceManager resourceManager,
        ResearchDef target,
        HashSet<string> visiting)
    {
        if (target == null || researchManager.IsResearchCompleted(target.Id))
            return;
        while (!researchManager.CanAccessResearch(target))
        {
            TechLevel currentEra = game.State.TechLevel;
            Assert.That(target.TechLevel, Is.GreaterThan(currentEra),
                "An unfinished earlier transition cannot be replayed: " + target.Id);
            ResearchDef transition = null;
            foreach (ResearchDef candidate in DataBase<ResearchDef>.All)
                if (candidate.AdvancesTechLevel && (int)candidate.TechLevel == (int)currentEra + 1)
                {
                    transition = candidate;
                    break;
                }
            Assert.That(transition, Is.Not.Null, "Missing transition after " + currentEra);
            Assert.That(visiting.Contains(transition.Id), Is.False,
                "A future-era prerequisite must not be required by its own era transition: " + target.Id);
            CompleteResearchAndPrerequisites(game, researchManager, resourceManager, transition, visiting);
            Assert.That(game.State.TechLevel, Is.GreaterThan(currentEra), "The paid transition must advance the era.");
        }
        Assert.That(visiting.Add(target.Id), Is.True,
            "Research prerequisite cycle detected at " + target.Id);
        for (int i = 0; i < target.Prerequisites.Count; i++)
            CompleteResearchAndPrerequisites(
                game, researchManager, resourceManager, target.Prerequisites[i], visiting);
        visiting.Remove(target.Id);

        ResearchActionResult action = researchManager.HandleResearchAction(target);
        bool canPay = researchManager.CanPayResearchCost(target, out string paymentBlocker);
        TestContext.WriteLine($"[SectorResearchFixture] research={target.Id} result={action} era={game.State.TechLevel} " +
            $"targetEra={target.TechLevel} access={researchManager.CanAccessResearch(target)} " +
            $"prerequisites={researchManager.ArePrerequisitesCompleted(target)} canPay={canPay} paymentBlocker={paymentBlocker}");
        if (action == ResearchActionResult.Blocked)
            foreach (Pair<ResourceDef, ExpantaNum> cost in target.ResourceRequirements)
                TestContext.WriteLine("[SectorResearchFixture] resource=" + cost.First.Id +
                    " owned=" + resourceManager.GetAmount(cost.First).ToString() + " required=" + cost.Second.ToString());
        Assert.That(action, Is.Not.EqualTo(ResearchActionResult.Invalid));
        Assert.That(action, Is.Not.EqualTo(ResearchActionResult.Blocked),
            "Research was blocked before completion: " + target.Id + "; era=" + game.State.TechLevel + "; " + paymentBlocker);
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
        Assert.That(game.State.TechLevel, Is.GreaterThanOrEqualTo(target.TechLevel));
    }
}
