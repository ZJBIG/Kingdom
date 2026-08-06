using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps repeatable list-card prefabs authored instead of relying on runtime
/// Rect/Label/Button construction. Runtime presenters only fill their data.
/// </summary>
internal static class KingdomUIRepeatedPrefabGenerator
{
    private const string Root = "Assets/Resources/UI/Kingdom/";
    private static readonly Color PanelRaised = new(.16f, .19f, .19f, 1f);
    private static readonly Color TextPrimary = new(.92f, .89f, .80f, 1f);

    [MenuItem("Tools/Kingdom/UI/Generate Reusable Row Prefabs")]
    private static void GenerateFromMenu() => GenerateAll();

    public static void GenerateAllForBatch() => GenerateAll();

    [InitializeOnLoadMethod]
    private static void EnsureOnEditorLoad()
    {
        EditorApplication.delayCall += GenerateAllIfNeeded;
    }

    private static void GenerateAllIfNeeded()
    {
        if (!File.Exists(Root + "KingdomUIResourceCard.prefab"))
            return;
        GenerateAll();
    }

    private static void GenerateAll()
    {
        EnsureRootDataRowsHosts();
        EnsureCard("KingdomUIResourceCard.prefab", 104f, new[] { "Label", "Amount", "ChangeRate" },
            new[] { new RectSpec("Icon", 0f, .5f, 0f, .5f, 18f, -34f, 86f, 34f),
                new RectSpec("Label", 0f, 0f, .46f, 1f, 108f, 0f, -8f, 0f),
                new RectSpec("Amount", .46f, 0f, .70f, 1f, 8f, 0f, -8f, 0f),
                new RectSpec("ChangeRate", .70f, 0f, 1f, 1f, 8f, 0f, -24f, 0f) }, true);
        EnsureCard("KingdomUIBuildingCard.prefab", 104f, new[] { "Label", "Amount", "BuildButton", "DeconstructButton" },
            new[] { new RectSpec("Label", 0f, 0f, .43f, 1f, 24f, 0f, -8f, 0f),
                new RectSpec("Amount", .43f, 0f, .57f, 1f, 8f, 0f, -8f, 0f),
                new RectSpec("BuildButton", .58f, .18f, .78f, .82f, 6f, 0f, -6f, 0f),
                new RectSpec("DeconstructButton", .79f, .18f, .98f, .82f, 6f, 0f, -6f, 0f) }, true);
        EnsureCard("KingdomUIResearchCard.prefab", 112f, new[] { "Label", "Era", "Percentage", "State" },
            new[] { new RectSpec("Label", 0f, 0f, .38f, 1f, 22f, 0f, -8f, 0f),
                new RectSpec("Era", .38f, 0f, .58f, 1f, 8f, 0f, -8f, 0f),
                new RectSpec("Percentage", .58f, 0f, .76f, 1f, 8f, 0f, -8f, 0f),
                new RectSpec("State", .76f, 0f, 1f, 1f, 8f, 0f, -18f, 0f) }, true);
        EnsureCard("KingdomUIMusicTrack.prefab", 56f, new[] { "Label", "Length", "Type" },
            new[] { new RectSpec("Label", 0f, 0f, .58f, 1f, 12f, 4f, -8f, -4f),
                new RectSpec("Length", .58f, 0f, .78f, 1f, 0f, 4f, 0f, -4f),
                new RectSpec("Type", .78f, 0f, 1f, 1f, 8f, 4f, -12f, -4f) }, true);
        EnsureDetailRow("KingdomUIRequirementRow.prefab", 72f,
            new RectSpec("Icon", 0f, .5f, 0f, .5f, 12f, -24f, 60f, 24f),
            new RectSpec("Label", 0f, 0f, .65f, 1f, 76f, 0f, -8f, 0f),
            new RectSpec("Amount", .65f, 0f, 1f, 1f, 8f, 0f, -16f, 0f));
        EnsureDetailRow("KingdomUIFlowRow.prefab", 56f,
            new RectSpec("Icon", 0f, .5f, 0f, .5f, 8f, -22f, 52f, 22f),
            new RectSpec("Label", 0f, 0f, .62f, 1f, 66f, 0f, -8f, 0f),
            new RectSpec("Amount", .62f, 0f, 1f, 1f, 8f, 0f, -12f, 0f));
        EnsureTextRow();
        AssetDatabase.SaveAssets();
    }

    private static void EnsureRootDataRowsHosts()
    {
        string rootPath = Root + "KingdomUIRoot.prefab";
        if (!File.Exists(rootPath))
            return;
        GameObject root = PrefabUtility.LoadPrefabContents(rootPath);
        if (root == null)
            return;
        try
        {
            string[] pages = { "Overview", "Resources", "Buildings", "Research", "Era", "Workshop", "Music", "Sectors" };
            for (int i = 0; i < pages.Length; i++)
            {
                Transform rows = root.transform.Find("SafeAreaRoot/Content/PageHost/" + pages[i] + "/DataRows");
                if (rows == null)
                    continue;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(rows.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(root, rootPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureCard(string file, float height, string[] children, RectSpec[] layout, bool rootButton)
    {
        string path = Root + file;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
            return;
        try
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(.5f, 1f);
            rootRect.sizeDelta = new Vector2(0f, height);
            LayoutElement layoutElement = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            Image rootImage = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            rootImage.color = PanelRaised;
            rootImage.raycastTarget = true;
            if (rootButton)
            {
                Button rootButtonComponent = root.GetComponent<Button>() ?? root.AddComponent<Button>();
                rootButtonComponent.targetGraphic = rootImage;
            }
            if (root.GetComponent<UIPageScrollDragForwarder>() == null)
                root.AddComponent<UIPageScrollDragForwarder>();
            for (int i = 0; i < layout.Length; i++)
            {
                Transform child = root.transform.Find(layout[i].Name);
                if (child == null)
                    continue;
                SetRect(child as RectTransform, layout[i]);
                if (layout[i].Name == "Icon")
                    EnsureIconChild(root.transform, layout[i]);
                else if (layout[i].Name == "BuildButton" || layout[i].Name == "DeconstructButton")
                    EnsureButton(child.gameObject);
                else
                    EnsureTextChild(root.transform, layout[i].Name, layout[i].MinX, layout[i].MinY,
                        layout[i].MaxX, layout[i].MaxY, layout[i].Left, layout[i].Bottom,
                        layout[i].Right, layout[i].Top);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureTextRow()
    {
        string path = Root + "KingdomUITextRow.prefab";
        bool loadedPrefabContents = File.Exists(path);
        GameObject root = loadedPrefabContents ? PrefabUtility.LoadPrefabContents(path) :
            new GameObject("KingdomUITextRow", typeof(RectTransform));
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(.5f, 1f);
            rootRect.sizeDelta = new Vector2(0f, 104f);
            LayoutElement layout = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            layout.preferredHeight = 104f;
            Image image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            image.color = PanelRaised;
            Button button = root.GetComponent<Button>() ?? root.AddComponent<Button>();
            button.targetGraphic = image;
            if (root.GetComponent<UIPageScrollDragForwarder>() == null)
                root.AddComponent<UIPageScrollDragForwarder>();
            EnsureTextChild(root.transform, "Title", 0f, 0f, .55f, 1f, 24f, 0f, -8f, 0f);
            EnsureTextChild(root.transform, "Subtitle", .55f, 0f, 1f, 1f, 8f, 0f, -24f, 0f);
            if (File.Exists(path))
                PrefabUtility.SaveAsPrefabAsset(root, path);
            else
                PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (loadedPrefabContents && root != null)
                PrefabUtility.UnloadPrefabContents(root);
            else if (root != null)
                Object.DestroyImmediate(root);
        }
    }

    private static void EnsureDetailRow(string file, float height, RectSpec iconSpec,
        RectSpec labelSpec, RectSpec amountSpec)
    {
        string path = Root + file;
        bool loadedPrefabContents = File.Exists(path);
        GameObject root = loadedPrefabContents ? PrefabUtility.LoadPrefabContents(path) :
            new GameObject(Path.GetFileNameWithoutExtension(file), typeof(RectTransform));
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(.5f, 1f);
            rootRect.sizeDelta = new Vector2(0f, height);
            LayoutElement layout = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            EnsureIconChild(root.transform, iconSpec);
            EnsureTextChild(root.transform, labelSpec.Name, labelSpec.MinX, labelSpec.MinY,
                labelSpec.MaxX, labelSpec.MaxY, labelSpec.Left, labelSpec.Bottom, labelSpec.Right, labelSpec.Top);
            EnsureTextChild(root.transform, amountSpec.Name, amountSpec.MinX, amountSpec.MinY,
                amountSpec.MaxX, amountSpec.MaxY, amountSpec.Left, amountSpec.Bottom, amountSpec.Right, amountSpec.Top);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (loadedPrefabContents)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);
        }
    }

    private static void EnsureIconChild(Transform parent, RectSpec spec)
    {
        Transform existing = parent.Find(spec.Name);
        GameObject child = existing == null ? new GameObject(spec.Name, typeof(RectTransform)) : existing.gameObject;
        if (existing == null)
            child.transform.SetParent(parent, false);
        SetRect(child.GetComponent<RectTransform>(), spec);
        Image image = child.GetComponent<Image>() ?? child.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
    }

    private static void EnsureTextChild(Transform parent, string name, float minX, float minY, float maxX, float maxY,
        float left, float bottom, float right, float top)
    {
        Transform existing = parent.Find(name);
        GameObject child = existing == null ? new GameObject(name, typeof(RectTransform)) : existing.gameObject;
        if (existing == null)
            child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>() ?? child.AddComponent<TextMeshProUGUI>();
        text.color = TextPrimary;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
    }

    private static void EnsureButton(GameObject target)
    {
        Image image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
        image.color = PanelRaised;
        Button button = target.GetComponent<Button>() ?? target.AddComponent<Button>();
        button.targetGraphic = image;
        Transform textTransform = target.transform.Find("Text");
        if (textTransform == null)
        {
            GameObject textObject = new("Text", typeof(RectTransform));
            textObject.transform.SetParent(target.transform, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.color = TextPrimary;
            text.fontSize = 30f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }
    }

    private static void SetRect(RectTransform rect, RectSpec spec)
    {
        if (rect == null)
            return;
        rect.anchorMin = new Vector2(spec.MinX, spec.MinY);
        rect.anchorMax = new Vector2(spec.MaxX, spec.MaxY);
        rect.offsetMin = new Vector2(spec.Left, spec.Bottom);
        rect.offsetMax = new Vector2(spec.Right, spec.Top);
    }

    private readonly struct RectSpec
    {
        public readonly string Name;
        public readonly float MinX, MinY, MaxX, MaxY, Left, Bottom, Right, Top;

        public RectSpec(string name, float minX, float minY, float maxX, float maxY,
            float left, float bottom, float right, float top)
        {
            Name = name;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }
    }
}
