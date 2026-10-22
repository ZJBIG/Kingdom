using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class KingdomCollectionPlayModeTests
{
    private const string PagesPath = "SafeAreaRoot/Content/PageHost/";
    private readonly Dictionary<string, int?> savedPins = new Dictionary<string, int?>();
    private string saveRoot;
    private KingdomUIRoot root;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        saveRoot = Path.Combine(Application.dataPath, "..", "Temp", "CollectionTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
        savedPins.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("CollectionTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded) yield return SceneManager.UnloadSceneAsync(sample);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        foreach (var pin in savedPins)
            if (pin.Value.HasValue) PlayerPrefs.SetInt(pin.Key, pin.Value.Value);
            else PlayerPrefs.DeleteKey(pin.Key);
        PlayerPrefs.Save();
        root = null;
    }

    private void ClearPinForFixture(string kind, string id)
    {
        // Protocol key is exact so tests restore missing keys as well as existing user preferences.
        string key = "Kingdom.UI.Pin." + kind.Length + ":" + kind + ":" + id;
        if (!savedPins.ContainsKey(key)) savedPins[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
        PlayerPrefs.DeleteKey(key);
    }

    private IEnumerator LoadCollections()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        SimulationManager.Instance.SetRunning(false);
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Ultra);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
        {
            ClearPinForFixture(UIPinPreferences.Research, research.Id);
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        }
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.IndustrialWorkshop);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
        {
            ClearPinForFixture(UIPinPreferences.Resource, resource.Id);
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1000000));
        }
        foreach (BuildingDef building in DataBase<BuildingDef>.All) ClearPinForFixture(UIPinPreferences.Building, building.Id);
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All) ClearPinForFixture(UIPinPreferences.Workshop, upgrade.Id);
        ClearPinForFixture(UIPinPreferences.Workshop, UIPinPreferences.RelicSupportId);
        foreach (SectorDef sector in DataBase<SectorDef>.All)
        {
            ClearPinForFixture(UIPinPreferences.Sector, sector.Id);
            GameManager.Instance.Sectors.GetState(sector).SetOccupiedForEditor(true);
        }
        root.RefreshNavigationVisibilityForEditor();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ResourceAndBuildingPinsSortWithoutInvokingTheMainAction()
    {
        yield return LoadCollections();
        root.SetPage("Resources");
        yield return null;
        ResourceDef targetResource = null;
        Transform resourceRow = null;
        foreach (var pair in root.ResourceChangeLabelsForEditor)
            if (resourceRow == null || pair.Value.transform.parent.GetSiblingIndex() > resourceRow.GetSiblingIndex())
            { targetResource = pair.Key; resourceRow = pair.Value.transform.parent; }
        Assert.That(targetResource, Is.Not.Null);
        Assert.That(root.ResourceChangeLabelsForEditor.Count, Is.GreaterThan(1));
        AssertPinClickIsIndependent(resourceRow);
        yield return null;
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Resource, targetResource.Id), Is.True);
        Transform pinnedResourceRow = root.ResourceChangeLabelsForEditor[targetResource].transform.parent;
        AssertFirstCollectionRow(pinnedResourceRow);
        AssertPinAppearance(pinnedResourceRow.Find("PinButton").GetComponent<Button>(), true);
        Assert.That(pinnedResourceRow.Find("Metadata").gameObject.activeSelf, Is.False);
        Assert.That(pinnedResourceRow.GetComponent<Image>().sprite,
            Is.SameAs(Resources.Load<Sprite>("Art/UI/EraCards/" + targetResource.TechLevel)),
            "Pinned rows retain their authored era artwork.");
        root.SetPage("Buildings");
        yield return null;
        BuildingDef targetBuilding = null;
        Transform buildingRow = null;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            Button deconstruct = root.GetDeconstructButtonForEditor(building);
            if (deconstruct != null && (buildingRow == null || deconstruct.transform.parent.GetSiblingIndex() > buildingRow.GetSiblingIndex()))
            { targetBuilding = building; buildingRow = deconstruct.transform.parent; }
        }
        Assert.That(targetBuilding, Is.Not.Null);
        ExpantaNum before = BuildingManager.Instance.States[targetBuilding].Amount;
        AssertPinClickIsIndependent(buildingRow);
        yield return null;
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Building, targetBuilding.Id), Is.True);
        Assert.That(BuildingManager.Instance.States[targetBuilding].Amount == before, Is.True, "Pin must preserve the discrete building count.");
        AssertFirstCollectionRow(root.GetDeconstructButtonForEditor(targetBuilding).transform.parent);
        root.SetPage("Resources");
        yield return null;
        AssertFirstCollectionRow(root.ResourceChangeLabelsForEditor[targetResource].transform.parent);
    }

    private static void AssertFirstCollectionRow(Transform pinned)
    {
        foreach (Transform sibling in pinned.parent)
            if (sibling != pinned && sibling.gameObject.activeInHierarchy && sibling.Find("PinButton") != null)
                Assert.That(pinned.GetSiblingIndex(), Is.LessThan(sibling.GetSiblingIndex()),
                    "Pinned definition must precede every unpinned collection row.");
    }

    private static void AssertPinClickIsIndependent(Transform row)
    {
        int mainClicks = 0;
        row.GetComponent<Button>().onClick.AddListener(() => mainClicks++);
        Button pin = row.Find("PinButton").GetComponent<Button>();
        var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(pin.gameObject, click, ExecuteEvents.pointerClickHandler);
        Assert.That(mainClicks, Is.Zero, "Child pin pointer click must not invoke the row detail action.");
    }

    [UnityTest]
    public IEnumerator WorkshopSectorSectorBuildingAndResearchPinsBindToTheirOwnDefinitions()
    {
        yield return LoadCollections();
        root.SetPage("Workshop");
        yield return null;
        Transform workshopRows = root.transform.Find(PagesPath + "Workshop/DataRows");
        WorkshopUpgradeDef upgrade = null;
        Transform upgradeRow = null;
        foreach (WorkshopUpgradeDef candidate in DataBase<WorkshopUpgradeDef>.All)
        {
            Transform row = FindLabelRow(workshopRows, candidate.Label);
            if (row != null && (upgradeRow == null || row.GetSiblingIndex() > upgradeRow.GetSiblingIndex()))
            { upgrade = candidate; upgradeRow = row; }
        }
        Assert.That(upgrade, Is.Not.Null);
        bool purchased = WorkshopManager.Instance.IsPurchased(upgrade);
        AssertPinClickIsIndependent(upgradeRow);
        yield return null;
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Workshop, upgrade.Id), Is.True);
        Assert.That(WorkshopManager.Instance.IsPurchased(upgrade), Is.EqualTo(purchased));
        upgradeRow = FindLabelRow(workshopRows, upgrade.Label);
        AssertFirstCollectionRow(upgradeRow);
        upgradeRow.Find("PinButton").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Workshop, upgrade.Id), Is.False);

        root.SetPage("Sectors");
        yield return null;
        Transform sectorRows = root.transform.Find(PagesPath + "Sectors/DataRows");
        SectorDef target = null;
        Transform targetRow = null;
        foreach (SectorDef sector in DataBase<SectorDef>.All)
        {
            Transform row = sectorRows.Find("SectorRow_" + sector.Id);
            if (row != null && row.gameObject.activeInHierarchy &&
                (targetRow == null || row.GetSiblingIndex() > targetRow.GetSiblingIndex()))
            { target = sector; targetRow = row; }
        }
        Assert.That(target, Is.Not.Null);
        AssertPinClickIsIndependent(targetRow);
        yield return null;
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Sector, target.Id), Is.True);
        AssertFirstCollectionRow(targetRow);
        targetRow.Find("PinButton").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Sector, target.Id), Is.False);

        Transform sectorBuildingCard = null;
        Transform expandedSector = null;
        foreach (Transform row in sectorRows)
        {
            Button expand = row.Find("Buildings")?.GetComponent<Button>();
            if (expand == null || !expand.gameObject.activeInHierarchy) continue;
            expand.onClick.Invoke();
            yield return null;
            Transform menu = row.Find("SectorBuildingMenu");
            foreach (Transform card in menu)
                if (card.Find("PinButton") != null) { sectorBuildingCard = card; expandedSector = row; }
            if (sectorBuildingCard != null) break;
        }
        Assert.That(sectorBuildingCard, Is.Not.Null, "Occupied sector must expose real building cards.");
        SectorBuildingDef sectorBuilding = DataBase<SectorBuildingDef>.Find(sectorBuildingCard.name);
        Assert.That(sectorBuilding, Is.Not.Null);
        ExpantaNum before = BuildingManager.Instance.States[sectorBuilding].Amount;
        Canvas.ForceUpdateCanvases();
        AssertRectsDoNotOverlap((RectTransform)sectorBuildingCard.Find("PinButton"),
            (RectTransform)sectorBuildingCard.Find("Name"));
        sectorBuildingCard.Find("PinButton").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Building, sectorBuilding.Id), Is.True);
        Assert.That(BuildingManager.Instance.States[sectorBuilding].Amount == before, Is.True, "Pin preserves discrete building count.");
        sectorBuildingCard = expandedSector.Find("SectorBuildingMenu/" + sectorBuilding.Id);
        AssertFirstCollectionRow(sectorBuildingCard);
        Assert.That(sectorBuildingCard.Find("Metadata").GetComponent<TMP_Text>().text, Is.Not.Empty);
        AssertRectsDoNotOverlap((RectTransform)expandedSector.Find("PinButton"), (RectTransform)expandedSector.Find("Label"));

        root.SetPage("Research");
        ResearchDef research = DataBase<ResearchDef>.All[0];
        Transform graph = null;
        for (int frame = 0; frame < 480; frame++)
        {
            yield return null;
            graph = root.transform.Find(PagesPath + "Research/DataRows/ResearchGraphViewport/ResearchGraphContent");
            if (graph != null && graph.Find("ResearchNode_" + research.Id) != null) break;
        }
        Transform node = graph.Find("ResearchNode_" + research.Id);
        Assert.That(node, Is.Not.Null);
        root.ShowResearchDetailsForEditor(research);
        // Activation needs one player-loop pass to register graphics with the raycaster.
        yield return null;
        Canvas.ForceUpdateCanvases();
        Button detailPin = root.transform.Find("SafeAreaRoot/DetailPanel/Surface/PinButton").GetComponent<Button>();
        Assert.That(detailPin.gameObject.activeInHierarchy, Is.False,
            "Detail pages omit pin actions; sorting remains on collection rows.");
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Research, research.Id), Is.False);
    }

    private static Transform FindLabelRow(Transform rows, string label)
    {
        foreach (Transform row in rows)
            if (row.gameObject.activeInHierarchy && row.Find("Label")?.GetComponent<TMP_Text>().text == label) return row;
        return null;
    }

    private static void AssertRectsDoNotOverlap(RectTransform first, RectTransform second)
    {
        Bounds a = RectTransformUtility.CalculateRelativeRectTransformBounds(first.parent, first);
        Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(first.parent, second);
        Assert.That(a.max.x <= b.min.x + 1 || b.max.x <= a.min.x + 1 ||
            a.max.y <= b.min.y + 1 || b.max.y <= a.min.y + 1, Is.True, first.name + " overlaps " + second.name);
    }

    private static void AssertPinAppearance(Button pin, bool pinned)
    {
        Image icon = pin.transform.Find("Icon")?.GetComponent<Image>();
        Assert.That(icon.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Pins/" + (pinned ? "PinFilled" : "PinOutline"))), "Generated outline/filled sprite must follow the stored preference.");
        Assert.That(icon.raycastTarget, Is.False, "The small glyph does not limit the click area.");
        Assert.That(pin.targetGraphic, Is.SameAs(icon));
        Image hitSurface = pin.GetComponent<Image>();
        Assert.That(hitSurface.sprite, Is.Null);
        Assert.That(hitSurface.color.a, Is.LessThan(.01f), "Pin has no visible button background.");
        Assert.That(hitSurface.raycastTarget, Is.True);
        RectTransform target = (RectTransform)pin.transform;
        Assert.That(target.rect.width, Is.GreaterThanOrEqualTo(47.5f));
        Assert.That(target.rect.height, Is.GreaterThanOrEqualTo(47.5f));
        foreach (TMP_Text label in pin.GetComponentsInChildren<TMP_Text>(true))
            Assert.That(label.gameObject.activeInHierarchy, Is.False, "Pin uses an icon without an active text label.");
    }



    [UnityTest]
    public IEnumerator PopulatedCollectionsRenderAndClipLongTitlesAtTwoLandscapeSizes()
    {
        yield return LoadCollections();
        Canvas canvas = root.GetComponent<Canvas>();
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        GameObject cameraObject = new GameObject("CollectionEvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000;
        RenderTexture target = null;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            foreach (Vector2Int size in new[] { new Vector2Int(2640, 1200), new Vector2Int(1760, 800) })
            {
                target = new RenderTexture(size.x, size.y, 24);
                target.Create();
                camera.targetTexture = target;
                foreach (string page in new[] { "Resources", "Buildings", "Sectors", "Workshop" })
                {
                    root.SetPage(page);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Transform rows = root.transform.Find(PagesPath + page + "/DataRows");
                    foreach (Transform row in rows)
                    {
                        Button pin = row.gameObject.activeInHierarchy ? row.Find("PinButton")?.GetComponent<Button>() : null;
                        if (pin == null) continue;
                        Image icon = pin.transform.Find("Icon").GetComponent<Image>();
                        if (icon.sprite == Resources.Load<Sprite>("Art/UI/Pins/PinOutline")) { pin.onClick.Invoke(); yield return null; }
                        break;
                    }
                    Canvas.ForceUpdateCanvases();
                    int populated = 0;
                    foreach (Transform row in rows)
                    {
                        if (!row.gameObject.activeInHierarchy || row.Find("PinButton") == null) continue;
                        populated++;
                        Button pin = row.Find("PinButton").GetComponent<Button>();
                        AssertPinAppearance(pin, pin.transform.Find("Icon").GetComponent<Image>().sprite == Resources.Load<Sprite>("Art/UI/Pins/PinFilled"));
                        TMP_Text metadata = row.Find("Metadata").GetComponent<TMP_Text>();
                        if (page != "Sectors") Assert.That(metadata.gameObject.activeSelf, Is.False);
                        else Assert.That(metadata.text, Is.Not.Empty, page + " metadata");
                        TMP_Text label = row.Find("Label").GetComponent<TMP_Text>();
                        if (page != "Sectors")
                        {
                            label.ForceMeshUpdate();
                            Assert.That(label.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Left));
                            Assert.That(Mathf.Abs(label.textBounds.center.y - label.rectTransform.rect.center.y),
                                Is.LessThan(label.fontSize * .12f), page + " vertical glyph center");
                        }
                        string original = label.text;
                        try
                        {
                            label.text = new string('W', 180);
                            label.ForceMeshUpdate();
                            Assert.That(label.isTextTruncated, Is.True, page + " long title must truncate");
                            Assert.That(label.textBounds.size.x, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 2), page);
                        }
                        finally { label.text = original; label.ForceMeshUpdate(); }
                    }
                    Assert.That(populated, Is.GreaterThan(0), page + " screenshot must show real definitions");
                    if (page == "Resources") root.ShowResourceDetailsForEditor(DataBase<ResourceDef>.All[0]);
                    if (page == "Buildings")
                        foreach (BuildingDef building in DataBase<BuildingDef>.All)
                            if (root.GetDeconstructButtonForEditor(building) != null)
                            { root.ShowBuildingDetailsForEditor(building); break; }
                    Canvas.ForceUpdateCanvases();
                    Capture(camera, target, page.ToLowerInvariant() + "-" + size.x + "x" + size.y);
                    Debug.Log("[CollectionUIEvidence] page=" + page + " size=" + size + " populated=" + populated);
                }
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                target = null;
            }
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            Object.Destroy(cameraObject);
        }
    }

    private static void Capture(Camera camera, RenderTexture target, string name)
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "production-compact-20261013"));
        Directory.CreateDirectory(directory);
        Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            string path = Path.Combine(directory, name + ".png");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(0));
        }
        finally { RenderTexture.active = previous; Object.Destroy(pixels); }
    }
}
