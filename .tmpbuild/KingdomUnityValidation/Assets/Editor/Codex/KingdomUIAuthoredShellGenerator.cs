using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the stable Kingdom UI shell into KingdomUIRoot.prefab. This runs
/// in the editor; runtime code is responsible only for binding the shell and
/// instantiating repeated definition-driven content.
/// </summary>
internal static class KingdomUIAuthoredShellGenerator
{
    private const string RootPath = "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab";
    private static readonly Color Background = new(.035f, .055f, .065f, 1f);
    private static readonly Color Panel = new(.10f, .13f, .14f, 1f);
    private static readonly Color PanelRaised = new(.16f, .19f, .19f, 1f);
    private static readonly Color Copper = new(.76f, .50f, .25f, 1f);
    private static readonly Color TextPrimary = new(.92f, .89f, .80f, 1f);
    private static readonly Color TextSecondary = new(.63f, .69f, .67f, 1f);
    private static readonly Color Positive = new(.37f, .72f, .58f, 1f);
    private static readonly Color Error = new(.78f, .31f, .28f, 1f);

    [MenuItem("Tools/Kingdom/UI/Generate Authored Scene Shell")]
    private static void GenerateFromMenu() => Generate();

    [InitializeOnLoadMethod]
    private static void EnsureOnEditorLoad()
    {
        EditorApplication.delayCall += EnsureIfEmpty;
    }

    private static void EnsureIfEmpty()
    {
        if (!File.Exists(RootPath))
            return;
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPath);
        if (root == null)
            return;
        bool hasOrdinaryPageHost = root.transform.Find("SafeAreaRoot/Content/PageHost/Era/DataRows") != null;
        bool hasResearchViewport = root.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport") != null;
        bool hasResearchToolbar = root.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport/ResearchTreeToolbar") != null;
        bool hasResearchLineLayer = root.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport/ResearchGraphContent/ResearchGraphLineLayer") != null;
        bool hasMusicSurface = root.transform.Find("SafeAreaRoot/Content/PageHost/Music/DataRows/MusicSurface/Controls") != null;
        bool hasQuantityControls = root.transform.Find("SafeAreaRoot/Content/BuildingQuantityControls") != null;
        bool hasLegacyDetailChildren = root.transform.Find("SafeAreaRoot/DetailPanel/Body") != null ||
            root.transform.Find("SafeAreaRoot/DetailPanel/BuildingOutput") != null ||
            root.transform.Find("SafeAreaRoot/DetailPanel/BuildingRequirements") != null;
        if (hasOrdinaryPageHost && hasResearchViewport && hasResearchToolbar && hasResearchLineLayer && hasMusicSurface &&
            hasQuantityControls && !hasLegacyDetailChildren)
            return;
        Generate();
    }

    private static void Generate()
    {
        if (!File.Exists(RootPath))
        {
            Debug.LogError("[KingdomUI] Authored shell source prefab missing: " + RootPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(RootPath);
        try
        {
            Transform safeArea = root.transform.Find("SafeAreaRoot");
            if (safeArea == null)
                safeArea = Rect("SafeAreaRoot", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).transform;
            ClearChildren(safeArea);
            BuildShell(root.transform, safeArea as RectTransform);
            PrefabUtility.SaveAsPrefabAsset(root, RootPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[KingdomUI] Authored scene shell generated: " + RootPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildShell(Transform root, RectTransform safeArea)
    {
        PanelRect("Background", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Background);

        RectTransform top = Rect("TopStatusBar", safeArea, new Vector2(0, 1), Vector2.one,
            new Vector2(0, -132), Vector2.zero);
        PanelRect("Surface", top, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
        Label("Title", top, "王国", 34, TextPrimary, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(36, -28), new Vector2(560, 38));
        Label("Status", top, "食物：0/0（0/s）    人口：0/0（0/min）    领土：0/0\n科技水平：未知    当前研究：无    日历：????/??/??", 32, TextSecondary, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(540, -54), new Vector2(-740, 54));
        Button("Notice", top, "通知", PanelRaised, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-730, -40), new Vector2(-520, 40));
        Button("Settings", top, "设置", Copper, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-500, -40), new Vector2(-28, 40));

        RectTransform nav = Rect("LeftNavigation", safeArea, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(270, -132));
        PanelRect("Surface", nav, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
        Label("Caption", nav, "文明管理", 18, TextSecondary, new Vector2(0, 1), Vector2.one, new Vector2(28, -78), new Vector2(-18, -28));
        string[] names = { "Overview", "Resources", "Buildings", "Research", "Era", "Workshop", "Music", "Sectors" };
        string[] labels = { "概览", "资源", "建筑", "研究", "时代", "工坊", "音乐", "星区" };
        for (int i = 0; i < names.Length; i++)
            Button("Nav_" + names[i], nav, labels[i], i == 0 ? Copper : PanelRaised,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -150 - i * 116), new Vector2(-20, -62 - i * 116));

        RectTransform body = Rect("Content", safeArea, Vector2.zero, Vector2.one,
            new Vector2(288, 110), new Vector2(-658, -150));
        PanelRect("Surface", body, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.075f, .095f, .10f, 1f));
        Label("PageTitle", body, "概览", 32, TextPrimary, new Vector2(0, 1), Vector2.one, new Vector2(34, -88), new Vector2(-34, -28));
        RectTransform pageHost = Rect("PageHost", body, Vector2.zero, Vector2.one, new Vector2(32, 24), new Vector2(-32, -108));
        Image pageImage = pageHost.gameObject.AddComponent<Image>();
        pageImage.color = Color.clear;
        pageImage.raycastTarget = true;
        pageHost.gameObject.AddComponent<RectMask2D>();
        ScrollRect pageScroll = pageHost.gameObject.AddComponent<ScrollRect>();
        pageScroll.viewport = pageHost;
        pageScroll.horizontal = false;
        pageScroll.vertical = true;
        pageScroll.movementType = ScrollRect.MovementType.Clamped;

        for (int i = 0; i < names.Length; i++)
        {
            RectTransform page = Rect(names[i], pageHost, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            page.anchorMin = new Vector2(0, 1);
            page.anchorMax = new Vector2(1, 1);
            page.pivot = new Vector2(.5f, 1);
            page.sizeDelta = new Vector2(0, 1400);
            TMP_Text heading = Label("Heading", page, labels[i], 26, Copper, new Vector2(0, 1), Vector2.one, new Vector2(20, -58), new Vector2(-20, -16));
            heading.gameObject.SetActive(false);
            if (names[i] != "Music" && names[i] != "Research")
            {
                RectTransform rows = Rect("DataRows", page, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                rows.pivot = new Vector2(.5f, 1f);
                rows.sizeDelta = new Vector2(0f, 720f);
            }
            if (names[i] == "Overview")
            {
                Card("PrimaryCard", page, new Vector2(0, .52f), new Vector2(1, .95f), new Vector2(20, 0), new Vector2(-20, 0), "当前发展指引\n\n正在读取王国状态，请先检查当前研究、资源和生产链。");
                Card("SecondaryCard", page, new Vector2(0, .05f), new Vector2(1, .46f), new Vector2(20, 0), new Vector2(-20, 0), "当前行动\n\n暂无阻塞提醒。");
            }
        }
        RectTransform musicPage = pageHost.Find("Music") as RectTransform;
        RectTransform musicRows = musicPage == null
            ? null
            : Rect("DataRows", musicPage, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        if (musicRows != null)
        {
            musicRows.pivot = new Vector2(.5f, 1f);
            musicRows.sizeDelta = new Vector2(0f, 720f);
        }
        BuildMusicSurface(musicRows);
        RectTransform researchPage = pageHost.Find("Research") as RectTransform;
        RectTransform researchRows = researchPage == null
            ? null
            : Rect("DataRows", researchPage, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        if (researchRows != null)
        {
            researchRows.pivot = new Vector2(.5f, 1f);
            researchRows.sizeDelta = new Vector2(0f, 900f);
        }
        BuildResearchSurface(researchRows);

        Label("BuildingPageTitle", body, "建筑", 36, TextPrimary,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -88), new Vector2(170, -28)).gameObject.SetActive(false);
        RectTransform quantity = Rect("BuildingQuantityControls", body, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(180, -88), new Vector2(-20, -28));
        PanelRect("Surface", quantity, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
        Label("Caption", quantity, "数量", 30, TextPrimary, Vector2.zero, new Vector2(.09f, 1), new Vector2(12, 0), new Vector2(-8, 0));
        QuantityButton(quantity, "Quantity_One", "x1", .10f, .28f);
        QuantityButton(quantity, "Quantity_Ten", "x10", .30f, .48f);
        QuantityButton(quantity, "Quantity_Max", "xMax", .50f, .68f);
        QuantityButton(quantity, "Quantity_Custom", "Custom", .70f, .84f);
        RectTransform input = Rect("CustomQuantityInput", quantity, new Vector2(.86f, .10f), new Vector2(.99f, .90f), Vector2.zero, Vector2.zero);
        Image inputImage = input.gameObject.AddComponent<Image>();
        inputImage.color = PanelRaised;
        TMP_InputField inputField = input.gameObject.AddComponent<TMP_InputField>();
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        TMP_Text inputText = Label("Text", input, string.Empty, 30, TextPrimary, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
        TMP_Text inputPlaceholder = Label("Placeholder", input, "请输入数量", 24, TextSecondary, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
        inputField.textComponent = inputText;
        inputField.placeholder = inputPlaceholder;
        inputField.textViewport = input;

        RectTransform detail = Rect("DetailPanel", safeArea, new Vector2(1, 0), Vector2.one, new Vector2(-640, 0), new Vector2(-18, -132));
        PanelRect("Surface", detail, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
#if false
        bool legacyDetailAuthoringEnabled = false;
        if (legacyDetailAuthoringEnabled)
        {
        Label("Accent", detail, "详细信息", 18, Copper, new Vector2(0, 1), Vector2.one, new Vector2(34, -76), new Vector2(-34, -28));
        Label("Body", detail, "请选择项目查看需求、产出和下一步操作。", 24, TextSecondary, Vector2.zero, Vector2.one, new Vector2(34, 540), new Vector2(-34, -110));
        StaticScrollViewport("BuildingOutput", detail, new Vector2(0, 0), new Vector2(1, 0), new Vector2(34, 350), new Vector2(-34, 530), "FlowContent");
        StaticScrollViewport("BuildingRequirements", detail, Vector2.zero, new Vector2(1, 0), new Vector2(34, 110), new Vector2(-34, 330), "RequirementContent");
        Transform flowContent = detail.Find("BuildingOutput/FlowContent");
        if (flowContent != null)
            Label("Heading", flowContent, "产出 / 消耗", 22, Copper, new Vector2(0, 1), Vector2.one, new Vector2(0, -32), new Vector2(0, -2));
        Transform requirementContent = detail.Find("BuildingRequirements/RequirementContent");
        if (requirementContent != null)
        {
            Label("Heading", requirementContent, "建筑建造需求", 24, Copper, new Vector2(0, 1), Vector2.one, new Vector2(18, -38), new Vector2(-18, -4));
            Label("None", requirementContent, "暂无支付需求", 22, TextSecondary, Vector2.zero, Vector2.one, new Vector2(18, -92), new Vector2(-18, -20));
        }
        Button("Payment", detail, "支付资源", Positive, new Vector2(0, 0), new Vector2(1, 0), new Vector2(34, 96), new Vector2(-34, 160)).gameObject.SetActive(false);
        Button("Action", detail, "SELECT", Copper, new Vector2(0, 0), new Vector2(1, 0), new Vector2(34, 28), new Vector2(-34, 92)).gameObject.SetActive(false);

        }
#endif
        RectTransform footer = Rect("NotificationBar", safeArea, new Vector2(0, 0), new Vector2(1, 0), new Vector2(288, 18), new Vector2(-658, 110));
        PanelRect("Surface", footer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
        Label("Text", footer, "SYSTEM READY     ·     TOUCH A SECTION TO CONTINUE", 20, TextSecondary, Vector2.zero, Vector2.one, new Vector2(28, 0), new Vector2(-28, 0));
        RectTransform tooltip = Rect("Tooltip", safeArea, new Vector2(.12f, .18f), new Vector2(.88f, .42f), Vector2.zero, Vector2.zero);
        PanelRect("Surface", tooltip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.04f, .05f, .05f, .97f));
        Label("Text", tooltip, string.Empty, 24, TextPrimary, Vector2.zero, Vector2.one, new Vector2(24, 18), new Vector2(-24, -18));
        tooltip.gameObject.SetActive(false);
    }

    private static void StaticScrollViewport(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax, string contentName)
    {
        RectTransform viewport = Rect(name, parent, min, max, offsetMin, offsetMax);
        Image image = viewport.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.vertical = true;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform content = Rect(contentName, viewport, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        content.pivot = new Vector2(.5f, 1f);
        scroll.content = content;
    }

    private static void BuildMusicSurface(RectTransform page)
    {
        if (page == null)
            return;
        RectTransform surface = Rect("MusicSurface", page, Vector2.zero, Vector2.one, new Vector2(18, 18), new Vector2(-18, -18));
        PanelRect("Surface", surface, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
        Label("Heading", surface, "音乐播放器", 32, Copper, new Vector2(0, 1), Vector2.one, new Vector2(28, -64), new Vector2(-28, -18));
        RectTransform controls = Rect("Controls", surface, new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -349), new Vector2(-28, -31));
        PanelRect("Surface", controls, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelRaised);
        Label("Current", controls, "正在播放", 24, TextPrimary, new Vector2(0, 1), new Vector2(.40f, 1), new Vector2(24, -58), new Vector2(-12, -18));
        TMP_Text time = Label("Time", controls, "00:00 / 00:00", 22, TextSecondary, new Vector2(.42f, 1), new Vector2(1, 1), new Vector2(12, -58), new Vector2(-24, -18));
        time.alignment = TextAlignmentOptions.MidlineRight;
        Label("TimeSeekLabel", controls, "TIME / SEEK", 16, TextSecondary, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -116), new Vector2(260, -92));
        MusicSlider("TimeSeek", controls, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -152), new Vector2(-24, -130), 0f, 0f);
        Button("PlayPause", controls, "暂停", Positive, Vector2.zero, Vector2.zero, new Vector2(24, 24), new Vector2(220, 72));
        Button("Previous", controls, "上一首", new Color(.11f, .15f, .15f, 1f), Vector2.zero, Vector2.zero, new Vector2(232, 24), new Vector2(428, 72));
        Button("Next", controls, "下一首", new Color(.11f, .15f, .15f, 1f), Vector2.zero, Vector2.zero, new Vector2(440, 24), new Vector2(636, 72));
        Button("Stop", controls, "停止", Error, Vector2.zero, Vector2.zero, new Vector2(648, 24), new Vector2(844, 72));
        Label("VolumeValue", controls, "音量 100%", 18, TextPrimary, Vector2.zero, new Vector2(.48f, 0), new Vector2(24, 128), new Vector2(-12, 152));
        MusicSlider("Volume", controls, Vector2.zero, new Vector2(.48f, 0), new Vector2(24, 92), new Vector2(-12, 116), 0f, 1f);
        Label("GapValue", controls, "音乐间隙 5.00", 18, TextPrimary, new Vector2(.52f, 0), new Vector2(1, 0), new Vector2(12, 128), new Vector2(-24, 152));
        MusicSlider("Gap", controls, new Vector2(.52f, 0), new Vector2(1, 0), new Vector2(12, 92), new Vector2(-24, 116), 0f, 30f);
        Label("ListHeading", surface, "音乐列表", 26, Copper, new Vector2(0, 1), Vector2.one, new Vector2(28, -397), new Vector2(-28, -359));
        RectTransform viewport = Rect("TrackListViewport", surface, Vector2.zero, Vector2.one, new Vector2(28, 24), new Vector2(-28, -431));
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Panel;
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect listScroll = viewport.gameObject.AddComponent<ScrollRect>();
        listScroll.viewport = viewport;
        listScroll.horizontal = false;
        listScroll.vertical = true;
        listScroll.inertia = true;
        listScroll.movementType = ScrollRect.MovementType.Clamped;
        listScroll.scrollSensitivity = 24f;
        viewport.gameObject.AddComponent<UIMusicListDragForwarder>();
        RectTransform content = Rect("TrackList", viewport, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        content.pivot = new Vector2(.5f, 1f);
        listScroll.content = content;
    }

    private static void BuildResearchSurface(RectTransform parent)
    {
        if (parent == null)
            return;
        RectTransform viewport = Rect("ResearchGraphViewport", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(.045f, .05f, .055f, 1f);
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.inertia = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform content = Rect("ResearchGraphContent", viewport, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        content.pivot = Vector2.zero;
        content.sizeDelta = Vector2.one;
        RectTransform dragSurface = Rect("ResearchGraphDragSurface", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image dragImage = dragSurface.gameObject.AddComponent<Image>();
        dragImage.color = Color.clear;
        dragImage.raycastTarget = true;
        RectTransform lineLayer = Rect("ResearchGraphLineLayer", content, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        lineLayer.pivot = Vector2.zero;
        lineLayer.sizeDelta = Vector2.one;
        scroll.content = content;
        viewport.gameObject.AddComponent<UIResearchGraphGesture>();

        GameObject toolbarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/UI/Kingdom/KingdomUIResearchToolbar.prefab");
        if (toolbarPrefab != null)
        {
            GameObject toolbarObject = PrefabUtility.InstantiatePrefab(toolbarPrefab, viewport) as GameObject;
            if (toolbarObject != null)
            {
                toolbarObject.name = "ResearchTreeToolbar";
                RectTransform toolbar = toolbarObject.GetComponent<RectTransform>();
                toolbar.anchorMin = new Vector2(0, 1);
                toolbar.anchorMax = Vector2.one;
                toolbar.offsetMin = new Vector2(16, -82);
                toolbar.offsetMax = new Vector2(-16, -12);
            }
        }
    }

    private static Slider MusicSlider(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax, float valueMin, float valueMax)
    {
        RectTransform rect = Rect(name, parent, min, max, offsetMin, offsetMax);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(.08f, .10f, .10f, 1f);
        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.minValue = valueMin;
        slider.maxValue = valueMax;
        slider.direction = Slider.Direction.LeftToRight;
        RectTransform fill = Rect("Fill", rect, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = Positive;
        fillImage.raycastTarget = false;
        RectTransform handle = Rect("Handle", rect, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(20, 0));
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = TextPrimary;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        return slider;
    }

    private static void Card(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, string text)
    {
        RectTransform card = Rect(name, parent, min, max, offsetMin, offsetMax);
        PanelRect("Surface", card, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelRaised);
        Label("Text", card, text, 24, TextPrimary, Vector2.zero, Vector2.one, new Vector2(28, 24), new Vector2(-28, -24));
    }

    private static void QuantityButton(Transform parent, string name, string text, float minX, float maxX)
    {
        Button(name, parent, text, PanelRaised, new Vector2(minX, .10f), new Vector2(maxX, .90f), Vector2.zero, Vector2.zero);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private static Image PanelRect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        RectTransform rect = Rect(name, parent, min, max, offsetMin, offsetMax);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text Label(string name, Transform parent, string text, float size, Color color, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = Rect(name, parent, min, max, offsetMin, offsetMax);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(string name, Transform parent, string text, Color color, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = Rect(name, parent, min, max, offsetMin, offsetMax);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(.72f, .72f, .72f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        Label("Text", rect, text, 28, TextPrimary, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        return button;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
