using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class KingdomBackgroundAuthoring
{
    private const string Art = "Assets/Resources/Art/UI/Backgrounds/";
    private const string Prefab = "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab";

    [MenuItem("Tools/Kingdom/UI/Author Background Materials")]
    public static void Author()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Import("KingdomEnvironment", false);
        Import("ContentSlate", true);
        Import("NavigationTimber", true);
        GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            Apply(root);
            if (PrefabUtility.SaveAsPrefabAsset(root, Prefab) == null)
                throw new InvalidOperationException("Could not save background materials.");
            AssetDatabase.SaveAssets();
            foreach (string name in new[] { "BuildingCard", "ResourceCard", "ResearchCard", "TextRow", "MusicTrack", "SectorRow", "OverviewNavigationToolbar" })
            {
                string path = "Assets/Resources/UI/Kingdom/KingdomUI" + name + ".prefab";
                GameObject row = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (name == "OverviewNavigationToolbar") AuthorOverviewToolbar(row.transform);
                    else
                    {
                        row.GetComponent<Image>().color = new Color(1, 1, 1, .05f);
                        if (name == "BuildingCard")
                        {
                            TMP_Text label = row.transform.Find("Label").GetComponent<TMP_Text>();
                            label.rectTransform.anchorMax = new Vector2(.23f, 1);
                            label.enableWordWrapping = true;
                            label.overflowMode = TextOverflowModes.Ellipsis;
                            SetColumn(row.transform.Find("TechLevel") as RectTransform, .24f, .37f);
                            SetColumn(row.transform.Find("Amount") as RectTransform, .39f, .47f);
                            SetColumn(row.transform.Find("Effect") as RectTransform, .48f, .62f);
                            SetColumn(row.transform.Find("BuildButton") as RectTransform, .64f, .80f);
                            SetColumn(row.transform.Find("DeconstructButton") as RectTransform, .82f, .98f);
                            row.transform.Find("Effect").GetComponent<TMP_Text>().enableWordWrapping = true;
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(row, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(row); }
            }
            AuthorWorkshopCard();
            Debug.Log("[KingdomBackgrounds] Authored environment and five panel surfaces; input surfaces preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void AuthorWorkshopCard()
    {
        const string path = "Assets/Resources/UI/Kingdom/KingdomUIWorkshopCard.prefab";
        GameObject row = PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(path) == null
            ? "Assets/Resources/UI/Kingdom/KingdomUIBuildingCard.prefab" : path);
        try
        {
            row.name = "KingdomUIWorkshopCard";
            TMP_Text label = row.transform.Find("Label").GetComponent<TMP_Text>();
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(.43f, 1);
            label.rectTransform.offsetMin = new Vector2(24, 8);
            label.rectTransform.offsetMax = new Vector2(-24, -8);
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text state = row.transform.Find("TechLevel").GetComponent<TMP_Text>();
            state.rectTransform.anchorMin = new Vector2(.45f, 0);
            state.rectTransform.anchorMax = new Vector2(.76f, 1);
            state.rectTransform.offsetMin = new Vector2(8, 8);
            state.rectTransform.offsetMax = new Vector2(-8, -8);
            state.enableWordWrapping = true;
            SetColumn(row.transform.Find("BuildButton") as RectTransform, .82f, .98f);
            SetColumn(row.transform.Find("DeconstructButton") as RectTransform, .82f, .98f);
            row.transform.Find("Amount").gameObject.SetActive(false);
            row.transform.Find("Effect").gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(row, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(row); }
    }

    private static void SetColumn(RectTransform rect, float left, float right)
    {
        rect.anchorMin = new Vector2(left, rect.anchorMin.y);
        rect.anchorMax = new Vector2(right, rect.anchorMax.y);
        rect.offsetMin = new Vector2(8, rect.offsetMin.y);
        rect.offsetMax = new Vector2(-8, rect.offsetMax.y);
    }

    public static void Apply(GameObject root)
    {
        Sprite environment = Load("KingdomEnvironment");
        Sprite slate = Load("ContentSlate");
        Sprite timber = Load("NavigationTimber");
        Transform existing = root.transform.Find("EnvironmentBackground");
        if (existing == null)
        {
            GameObject go = new GameObject("EnvironmentBackground", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            go.transform.SetParent(root.transform, false);
            existing = go.transform;
        }
        existing.SetAsFirstSibling();
        Image backdrop = existing.GetComponent<Image>();
        backdrop.sprite = environment;
        backdrop.color = Color.white;
        backdrop.type = Image.Type.Simple;
        backdrop.raycastTarget = false;
        AspectRatioFitter fit = existing.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = environment.rect.width / environment.rect.height;
        Image rootSurface = root.GetComponent<Image>();
        rootSurface.raycastTarget = false;
        Image oldBackground = Required(root, "SafeAreaRoot/Background");
        oldBackground.color = Color.clear;
        oldBackground.raycastTarget = false;
        Panel(root, "Content", slate, .96f, .68f);
        Panel(root, "DetailPanel", slate, .96f, .68f);
        Panel(root, "LeftNavigation", timber, .95f, .85f);
        Panel(root, "TopStatusBar", timber, .95f, .85f);
        Panel(root, "NotificationBar", timber, .95f, .85f);
        const string pages = "SafeAreaRoot/Content/PageHost/";
        foreach (string path in new[] { "Overview/PrimaryCard/Surface", "Overview/SecondaryCard/Surface",
            "Music/DataRows/MusicSurface/Surface", "Music/DataRows/MusicSurface/Controls/Surface", "Music/DataRows/MusicSurface/TrackListViewport" })
            HideContentSurface(Required(root, pages + path));
        Image graph = Required(root, pages + "Research/DataRows/ResearchGraphViewport");
        graph.sprite = null;
        graph.type = Image.Type.Simple;
        if (graph.GetComponent<Mask>() == null) graph.gameObject.AddComponent<Mask>();
        HideContentSurface(graph);
        AuthorFrame(Required(root, "SafeAreaRoot/Content/Surface"));
        AuthorFrame(Required(root, "SafeAreaRoot/DetailPanel/Surface"));
        Transform info = root.transform.Find("SafeAreaRoot/TopStatusBar/TopInfoShell/InfoGrid");
        foreach (Transform cell in info)
        {
            Image image = cell.GetComponent<Image>();
            image.sprite = slate;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 12;
            image.color = new Color(.75f, .82f, .77f, .85f);
            image.raycastTarget = false;
        }
        Transform controls = root.transform.Find(pages + "Music/DataRows/MusicSurface/Controls");
        RectTransform icon = controls.Find("CurrentCategoryIcon") as RectTransform;
        icon.anchorMin = icon.anchorMax = new Vector2(0, 1);
        icon.pivot = new Vector2(0, .5f);
        icon.anchoredPosition = new Vector2(28, -40);
        icon.sizeDelta = new Vector2(48, 48);
        TMP_Text nameLabel = controls.Find("CurrentPlayName").GetComponent<TMP_Text>();
        nameLabel.rectTransform.anchorMin = new Vector2(0, 1);
        nameLabel.rectTransform.anchorMax = new Vector2(.72f, 1);
        nameLabel.rectTransform.offsetMin = new Vector2(104, -64);
        nameLabel.rectTransform.offsetMax = new Vector2(-12, -16);
        nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
        nameLabel.enableWordWrapping = false;
        nameLabel.overflowMode = TextOverflowModes.Ellipsis;
        AuthorOverviewToolbar(root.transform.Find("SafeAreaRoot/Content/PageTool/OverviewNavigationToolbar"));
        AuthorOverview(root.transform.Find(pages + "Overview"));
        KingdomPlannerAuthoring.ApplyOverview(root);
    }

    private static void AuthorFrame(Image surface)
    {
        Transform frame = surface.transform.Find("PanelFrame");
        if (frame == null)
        {
            frame = new GameObject("PanelFrame", typeof(RectTransform)).transform;
            frame.SetParent(surface.transform, false);
        }
        RectTransform frameRect = (RectTransform)frame;
        frameRect.anchorMin = Vector2.zero; frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = new Vector2(2, 2); frameRect.offsetMax = new Vector2(-2, -2);
        foreach (string name in new[] { "Top", "Bottom", "Left", "Right" })
        {
            Transform side = frame.Find(name);
            if (side == null)
            {
                side = new GameObject(name, typeof(RectTransform), typeof(Image)).transform;
                side.SetParent(frame, false);
            }
            RectTransform rect = (RectTransform)side;
            bool horizontal = name == "Top" || name == "Bottom";
            bool far = name == "Top" || name == "Right";
            rect.anchorMin = horizontal ? new Vector2(0, far ? 1 : 0) : new Vector2(far ? 1 : 0, 0);
            rect.anchorMax = horizontal ? new Vector2(1, far ? 1 : 0) : new Vector2(far ? 1 : 0, 1);
            rect.pivot = horizontal ? new Vector2(.5f, far ? 1 : 0) : new Vector2(far ? 1 : 0, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = horizontal ? new Vector2(0, 2) : new Vector2(2, 0);
            Image image = side.GetComponent<Image>();
            image.color = new Color(.30f, .22f, .13f, .72f);
            image.raycastTarget = false;
        }
    }

    private static void AuthorOverviewToolbar(Transform toolbar)
    {
        foreach (string name in new[] { "OverviewCurrentTargetButton", "OverviewCurrentEraButton" })
        {
            RectTransform button = toolbar.Find(name) as RectTransform;
            button.sizeDelta = new Vector2(name == "OverviewCurrentEraButton" ? 288 : 208, 56);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.fontSize = 28;
            label.enableWordWrapping = false;
        }
    }

    private static void AuthorOverview(Transform page)
    {
        VerticalLayoutGroup layout = page.GetComponent<VerticalLayoutGroup>() ?? page.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24); layout.spacing = 24;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        ContentSizeFitter fit = page.GetComponent<ContentSizeFitter>() ?? page.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (string name in new[] { "PrimaryCard", "SecondaryCard" })
        {
            Transform card = page.Find(name);
            ContentSizeFitter oldFit = card.GetComponent<ContentSizeFitter>();
            if (oldFit != null) UnityEngine.Object.DestroyImmediate(oldFit);
            VerticalLayoutGroup group = card.GetComponent<VerticalLayoutGroup>() ?? card.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(28, 28, 24, 24); group.spacing = 16;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            Transform surface = card.Find("Surface");
            LayoutElement ignored = surface.GetComponent<LayoutElement>() ?? surface.gameObject.AddComponent<LayoutElement>();
            ignored.ignoreLayout = true;
        }
    }

    private static void HideContentSurface(Image image)
    {
        Mask mask = image.GetComponent<Mask>();
        if (mask != null)
        {
            mask.showMaskGraphic = false;
            image.color = Color.white;
        }
        else image.color = Color.clear;
    }

    private static void Panel(GameObject root, string name, Sprite sprite, float alpha, float brightness)
    {
        Image image = Required(root, "SafeAreaRoot/" + name + "/Surface");
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 8;
        image.color = new Color(brightness, brightness, brightness, alpha);
        image.raycastTarget = false;
    }

    private static Image Required(GameObject root, string path)
    {
        Image image = root.transform.Find(path)?.GetComponent<Image>();
        if (image == null) throw new InvalidOperationException("Missing authored background surface: " + path);
        return image;
    }

    private static Sprite Load(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
        if (sprite == null) throw new InvalidOperationException("Import background material first: " + name);
        return sprite;
    }

    private static void Import(string name, bool panel)
    {
        var importer = AssetImporter.GetAtPath(Art + name + ".png") as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing generated background: " + name);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = panel ? new Vector4(64, 40, 64, 40) : Vector4.zero;
        if (!panel)
        {
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int unit = Mathf.Min(width / 11, height / 5);
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = new[] { new SpriteMetaData { name = name,
                rect = new Rect((width - unit * 11) * .5f, (height - unit * 5) * .5f, unit * 11, unit * 5),
                alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) } };
        }
        importer.spritePixelsPerUnit = 100;
        importer.maxTextureSize = 2048;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }
}
