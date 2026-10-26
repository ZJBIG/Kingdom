using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class LaborGlyphPlayModeTests
{
    private string saveRoot;

    [SetUp]
    public void SetUp()
    {
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "LaborGlyph-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            foreach (GameObject owner in sample.GetRootGameObjects()) Object.DestroyImmediate(owner);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        if (tutorial != null) Object.DestroyImmediate(tutorial.gameObject);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (music != null) Object.DestroyImmediate(music.gameObject);
        yield return null;
        SaveManager.ClearSaveRootOverrideForTests();
        Assert.That(Path.GetDirectoryName(saveRoot), Is.EqualTo(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"))));
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }

    [UnityTest]
    public IEnumerator AuthoredLaborHudAndUpgradeGlyphFollowActualBuildingMode()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        KingdomUIRoot ui = null;
        for (int i = 0; i < 160; i++)
        {
            ui = Object.FindObjectOfType<KingdomUIRoot>();
            if (ui != null && Object.FindObjectOfType<GameBootstrap>()?.Completed == true) break;
            yield return null;
        }
        Assert.That(ui, Is.Not.Null);
        SimulationManager.Instance.SetRunning(false);
        BuildingManager manager = BuildingManager.Instance;
        BuildingDef source = DataBase<BuildingDef>.Find("WoodHouse");
        manager.SetAmountAndRatesForEditor(manager.EnsureBuilding(source), ExpantaNum.One);
        for (int i = 0; i < 16; i++)
        {
            ui.RefreshOnboardingForEditor();
            TutorialManager.Current?.SkipCurrentStage();
            yield return null;
        }
        ui.SetPage("Buildings");
        yield return null;
        Transform action = ui.GetDeconstructButtonForEditor(source).transform.parent.Find("BuildButton");
        Assert.That(action.GetComponent<UIBuildingActionIcon>(), Is.Not.Null);
        Assert.That(action.Find("ActionIcon").gameObject.activeSelf, Is.True);
        Assert.That(action.Find("UpgradeIcon").gameObject.activeSelf, Is.False);

        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Archotech);
        GameManager.Instance.State.AdjustTerritoryTotalForEditor(new ExpantaNum(1000000));
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1000000000000d));
        Assert.That(manager.TryGetUnlockedUpgradeTarget(source, out BuildingDef target), Is.True);
        float refreshDeadline = Time.realtimeSinceStartup + 5f;
        do
        {
            yield return null;
            ui.RefreshAutomaticUIForEditor();
            action = ui.GetDeconstructButtonForEditor(source).transform.parent.Find("BuildButton");
        } while (!action.Find("UpgradeIcon").gameObject.activeSelf && Time.realtimeSinceStartup < refreshDeadline);
        action = ui.GetDeconstructButtonForEditor(source).transform.parent.Find("BuildButton");
        Transform buildGlyph = action.Find("ActionIcon");
        Transform upgradeGlyph = action.Find("UpgradeIcon");
        Assert.That(buildGlyph.gameObject.activeSelf, Is.False);
        Assert.That(upgradeGlyph.gameObject.activeSelf, Is.True);
        Assert.That(Vector2.Distance(((RectTransform)buildGlyph).sizeDelta, ((RectTransform)upgradeGlyph).sizeDelta), Is.LessThan(.01f));
        Assert.That(upgradeGlyph.Find("ArrowShaft"), Is.Not.Null);
        Assert.That(upgradeGlyph.Find("ArrowLeft"), Is.Not.Null);
        Assert.That(upgradeGlyph.Find("ArrowRight"), Is.Not.Null);
        for (int i = 0; i < 16; i++)
        {
            ui.RefreshOnboardingForEditor();
            TutorialManager.Current?.SkipCurrentStage();
            yield return null;
        }
        ui.RefreshTopInfoForEditor();
        ScrollRect list = ui.transform.Find("SafeAreaRoot/Content/PageHost").GetComponent<ScrollRect>();
        Canvas.ForceUpdateCanvases();
        Vector2 offset = (Vector2)list.viewport.InverseTransformPoint(action.position) - list.viewport.rect.center;
        list.content.anchoredPosition -= new Vector2(0f, offset.y);
        Canvas.ForceUpdateCanvases();
        Capture(ui);
        ExpantaNum previousTarget = manager.EnsureBuilding(target).Amount;
        Button upgrade = action.GetComponent<Button>();
        Assert.That(upgrade.interactable, Is.True);
        upgrade.onClick.Invoke();
        Assert.That(manager.GetState(source).Amount, Is.EqualTo(ExpantaNum.Zero), "One real source building is replaced.");
        Assert.That(manager.GetState(target).Amount, Is.EqualTo(previousTarget + ExpantaNum.One), "Exactly one target building is created.");

        ui.RefreshTopInfoForEditor();
        Transform grid = ui.transform.Find("SafeAreaRoot/TopStatusBar/TopInfoShell/InfoGrid");
        Image laborIcon = grid.Find("CurrentResearch/Icon").GetComponent<Image>();
        Assert.That(laborIcon.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Status/HudProductivity")));
        Assert.That(Vector2.Distance(laborIcon.rectTransform.sizeDelta,
            ((RectTransform)grid.Find("Population/Icon")).sizeDelta), Is.LessThan(.01f));
        Assert.That(grid.Find("CurrentResearch/Value").GetComponent<TMP_Text>().text,
            Is.EqualTo(manager.UsedProductivity.ToGameString() + "/" + manager.TotalProductivity.ToGameString()));
    }

    private static void Capture(KingdomUIRoot ui)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "experience-20261023"));
        Directory.CreateDirectory(directory);
        Canvas canvas = ui.GetComponent<Canvas>();
        RenderMode previousMode = canvas.renderMode;
        Camera previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance;
        GameObject cameraOwner = new GameObject("LaborGlyphEvidenceCamera");
        Camera camera = cameraOwner.AddComponent<Camera>();
        RenderTexture target = new RenderTexture(1760, 800, 24);
        Texture2D pixels = new Texture2D(1760, 800, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1000f;
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory, "labor-upgrade-glyph.png"), pixels.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousDistance;
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraOwner);
        }
    }
}
