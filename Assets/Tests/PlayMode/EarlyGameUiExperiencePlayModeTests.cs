using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class EarlyGameUiExperiencePlayModeTests
{
    private string saveRoot;
    private KingdomUIRoot ui;
    [SetUp] public void SetUp()
    {
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "EarlyGameUi-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }
    [UnityTearDown] public IEnumerator TearDown()
    {
        Scene scene = SceneManager.GetSceneByName("SampleScene");
        if (scene.IsValid() && scene.isLoaded)
            foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        if (tutorial != null) Object.DestroyImmediate(tutorial.gameObject);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (music != null) Object.DestroyImmediate(music.gameObject);
        yield return null;
        SaveManager.ClearSaveRootOverrideForTests();
        Assert.That(Path.GetDirectoryName(saveRoot), Is.EqualTo(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"))));
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }
    [UnityTest] public IEnumerator HudEraAndPurchaseDetails_UseLiveSelectionAndAuthoredIcons()
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
        yield return DismissIntroductions();
        ui.RefreshTopInfoForEditor();
        Transform grid = ui.transform.Find("SafeAreaRoot/TopStatusBar/TopInfoShell/InfoGrid");
        TMP_Text date = ui.transform.Find("SafeAreaRoot/TopStatusBar/Title/Date").GetComponent<TMP_Text>();
        Assert.That(date.text, Is.EqualTo("????/??/??"), "A settlement has no readable date before Calendar.");
        Assert.That(grid.Find("CurrentResearch/Value").GetComponent<TMP_Text>().text,
            Is.EqualTo(BuildingManager.Instance.UsedProductivity.ToGameString() + "/" + BuildingManager.Instance.TotalProductivity.ToGameString()));
        Assert.That(grid.Find("Power").gameObject.activeSelf, Is.False);
        Assert.That(grid.Find("Logistics").gameObject.activeSelf, Is.False);
        foreach (Transform slot in grid)
            Assert.That(slot.Find("Icon").GetComponent<Image>().sprite, Is.Not.Null, slot.name);
        ui.SetPage("Overview"); yield return null;
        AssertHudGeometry(grid, false);
        BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
        Transform primary = ui.transform.Find("SafeAreaRoot/Content/PageHost/Overview/PrimaryCard");
        Assert.That(primary.Find("Text").GetComponent<TMP_Text>().text, Does.Contain(house.Label),
            "Skipping the introduction must leave a concrete first-housing goal.");
        Button next = primary.Find("NavigationButton").GetComponent<Button>();
        Assert.That(next.gameObject.activeInHierarchy && next.interactable, Is.True,
            "The housing goal must provide an actionable building destination.");
        Capture("overview-hud");
        UITouchTooltip tooltip = grid.Find("ResearchPower").GetComponent<UITouchTooltip>();
        var pointer = new PointerEventData(EventSystem.current);
        tooltip.OnPointerDown(pointer);
        yield return new WaitForSecondsRealtime(.65f);
        tooltip.OnPointerUp(pointer);
        Transform help = ui.transform.Find("SafeAreaRoot/Tooltip");
        Assert.That(help.gameObject.activeInHierarchy, Is.True, "Holding the metric still opens its explanation.");
        Assert.That(help.Find("Text").GetComponent<TMP_Text>().text, Is.Not.Empty);
        Capture("tooltip-help");
        help.gameObject.SetActive(false);

        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(house), ExpantaNum.One);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            if (research.TechLevel == TechLevel.Animal && !research.AdvancesTechLevel)
                ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        ui.RefreshAutomaticUIForEditor();
        ui.RefreshTopInfoForEditor();
        Assert.That(date.text, Is.Not.EqualTo("????/??/??"));
        yield return DismissIntroductions();
        ui.SetPage("Era"); yield return null;
        Transform era = ui.transform.Find("SafeAreaRoot/Content/PageHost/Era/DataRows/EraTransitionSection/Content/EraTransitionTarget");
        Assert.That(era, Is.Not.Null);
        era.GetComponent<Button>().onClick.Invoke();
        Assert.That(era.gameObject.activeInHierarchy, Is.True, "Selecting a transition keeps the era page visible.");
        Assert.That(ui.DetailActionButtonForEditor.gameObject.activeInHierarchy, Is.True);
        TMP_Text eraBody = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        Assert.That(eraBody.text, Does.Contain(TechLevel.StoneAge.GetDescription()), "The transition's effect must identify its target era.");
        ResearchDef transition = EraGoalEvaluator.FindTransition(TechLevel.StoneAge);
        Assert.That(transition, Is.Not.Null);
        ui.ShowResearchDetailsForEditor(transition);
        foreach (TMP_Text text in ui.transform.Find("SafeAreaRoot/DetailPanel").GetComponentsInChildren<TMP_Text>())
            if (text.transform.parent.name.StartsWith("Requirement_", StringComparison.Ordinal) && text.name == "Amount")
                Assert.That(text.text, Does.Not.Contain(" / "), "Research requires a single full payment.");
        yield return null; Capture("era-selected-detail");

        ui.SetPage("Resources"); yield return null;
        Capture("resource-pins");
        ui.SetPage("Buildings");
        foreach (var cost in house.ResourceRequirements) ResourceManager.Instance.SetAmount(cost.First, ExpantaNum.Zero);
        ui.ShowBuildingDetailsForEditor(house); yield return null;
        TMP_Text body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
        Assert.That(body.text, Does.Contain("index=5"), "Material shortage belongs in the detail panel.");
        Transform entry = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/ResourceProductionTree");
        Assert.That(entry.gameObject.activeSelf, Is.False, "Production entry stays hidden before its industrial unlock.");
        Capture("building-materials-detail");
        ResearchManager.Instance.GetState(DataBase<ResearchDef>.Find("FactoryOrganization")).SetStatusForEditor(ResearchStatus.Completed);
        ui.RefreshAutomaticUIForEditor();
        ui.ShowBuildingDetailsForEditor(house);
        Assert.That(entry.gameObject.activeSelf, Is.True, "The same detail selection gains its entry after the real research unlock.");
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Industrial);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            if (research.TechLevel <= TechLevel.Industrial)
                ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        ui.RefreshTopInfoForEditor();
        Canvas.ForceUpdateCanvases();
        Assert.That(grid.Find("Power").gameObject.activeSelf, Is.True);
        Assert.That(grid.Find("Logistics").gameObject.activeSelf, Is.True);
        AssertHudGeometry(grid, true);
        Capture("hud-infrastructure-unlocked");
    }
    private static void AssertHudGeometry(Transform grid, bool infrastructureVisible)
    {
        Canvas.ForceUpdateCanvases();
        var layout = grid.GetComponent<GridLayoutGroup>();
        string[] order = { "Food", "Population", "ResearchPower", "Happiness", "Territory", "CurrentResearch", "Power", "Logistics" };
        int count = infrastructureVisible ? order.Length : 6;
        var rectangles = new Rect[count];
        var corners = new Vector3[4];
        for (int i = 0; i < count; i++)
        {
            RectTransform slot = grid.Find(order[i]) as RectTransform;
            slot.GetWorldCorners(corners);
            Vector3 lower = grid.InverseTransformPoint(corners[0]);
            Vector3 upper = grid.InverseTransformPoint(corners[2]);
            rectangles[i] = Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
            Assert.That(rectangles[i].width, Is.GreaterThan(0));
            Assert.That(rectangles[i].height, Is.GreaterThan(0));
            if (i % 2 == 1)
                Assert.That(Mathf.Abs(rectangles[i].xMin - rectangles[i - 1].xMin), Is.LessThan(.5f), "Each pair forms one column.");
            if (i >= 2 && i % 2 == 0)
                Assert.That(rectangles[i].xMin - rectangles[i - 2].xMax, Is.EqualTo(layout.spacing.x).Within(.5f), "Visible columns are contiguous without a hidden infrastructure gap.");
            for (int j = 0; j < i; j++) Assert.That(rectangles[i].Overlaps(rectangles[j]), Is.False, "HUD entries must not overlap.");
        }
        if (infrastructureVisible)
            Assert.That(rectangles[6].xMin, Is.GreaterThan(rectangles[5].xMax), "Power and logistics occupy the rightmost column.");
    }

    private IEnumerator DismissIntroductions()
    {
        for (int i = 0; i < 16; i++)
        {
            ui.RefreshOnboardingForEditor();
            TutorialManager.Current?.SkipCurrentStage();
            yield return null;
        }
        ui.RefreshOnboardingForEditor();
    }
    private void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "experience-20261023"));
        Directory.CreateDirectory(directory);
        Canvas canvas = ui.GetComponent<Canvas>();
        RenderMode mode = canvas.renderMode; Camera previousCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var host = new GameObject("EarlyGameEvidenceCamera"); Camera camera = host.AddComponent<Camera>();
        var target = new RenderTexture(2640, 1200, 24); var pixels = new Texture2D(2640, 1200, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            target.Create(); camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = distance;
            camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(host);
        }
    }
}

