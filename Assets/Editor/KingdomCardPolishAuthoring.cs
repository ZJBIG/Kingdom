using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authors readable card boundaries without changing gameplay data.</summary>
public static class KingdomCardPolishAuthoring
{
    private const string Folder = "Assets/Resources/UI/Kingdom/";
    private static readonly Color Border = new(.57f, .49f, .32f, .55f);

    [MenuItem("Tools/Kingdom/UI/Author Card Boundaries")]
    public static void Author()
    {
        foreach (string name in new[] { "StoryChapterCard", "KingdomUIBuildingCard", "KingdomUIResourceCard",
            "KingdomUIWorkshopCard", "KingdomUISectorRow", "KingdomUISectorBuildingCard", "KingdomUIMusicTrack" })
        {
            string path = Folder + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyCard(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AuthorEra(false);
        AuthorEra(true);
        AssetDatabase.SaveAssets();
        Debug.Log("[CardPolish] Authored story/list borders and era section/card templates.");
    }

    public static void ApplyCard(GameObject root)
    {
        Frame(root.transform);
        StoryChapterLayout layout = root.GetComponent<StoryChapterLayout>();
        if (layout == null) return;
        layout.padding = new RectOffset(28, 28, 24, 24);
        var serialized = new SerializedObject(layout);
        serialized.FindProperty("spacing").floatValue = 26;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        TMP_Text title = root.transform.Find("Header/Title").GetComponent<TMP_Text>();
        title.fontSize = 34;
        Line(root.transform.Find("Header"), "Divider", false, new Vector2(0, -10), new Vector2(0, 2));
    }

    private static void AuthorEra(bool section)
    {
        string name = section ? "KingdomUIEraSection" : "KingdomUIEraCard";
        string path = Folder + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(path) : Rect(name, null);
        try
        {
            Image surface = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            surface.color = section ? new Color(.07f, .09f, .08f, .48f) : new Color(.16f, .19f, .17f, .94f);
            surface.raycastTarget = false;
            if (root.GetComponent<UIPageScrollDragForwarder>() == null) root.AddComponent<UIPageScrollDragForwarder>();
            Frame(root.transform);
            TMP_Text title = Text(root.transform, "Title", 30, true);
            title.color = new Color(.93f, .85f, .65f);
            title.rectTransform.anchoredPosition = new Vector2(24, -20);
            title.rectTransform.sizeDelta = new Vector2(800, 48);
            if (section)
            {
                RectTransform content = (RectTransform)Rect("Content", root.transform).transform;
                content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f, 1); content.anchoredPosition = new Vector2(0, -78);
                content.sizeDelta = new Vector2(-24, 100);
                Line(root.transform, "Divider", true, new Vector2(0, -70), new Vector2(-48, 2));
            }
            else
            {
                Text(root.transform, "Body", 28, false);
                Line(root.transform, "Divider", true, new Vector2(0, -76), new Vector2(-48, 1));
                foreach (string action in new[] { "DetailAction", "QueueAction" })
                {
                    Transform found = root.transform.Find(action);
                    GameObject buttonObject = found != null ? found.gameObject :
                        (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "KingdomUIButton.prefab"), root.transform);
                    buttonObject.name = action;
                    RectTransform rect = (RectTransform)buttonObject.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1);
                    rect.anchoredPosition = new Vector2(action == "DetailAction" ? -24 : -286, -20);
                    rect.sizeDelta = new Vector2(250, 58);
                    buttonObject.GetComponentInChildren<TMP_Text>().text = action == "DetailAction" ? "查看详情" : "加入研究队列";
                    buttonObject.SetActive(false);
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (existing != null) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static TMP_Text Text(Transform parent, string name, float size, bool bold)
    {
        GameObject root = Rect(name, parent);
        TMP_Text text = root.GetComponent<TMP_Text>() ?? root.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset");
        text.fontSize = size; text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = new Color(.92f, .89f, .8f); text.raycastTarget = false;
        text.enableWordWrapping = true; text.alignment = TextAlignmentOptions.TopLeft;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        return text;
    }

    private static GameObject Rect(string name, Transform parent)
    {
        Transform existing = parent == null ? null : parent.Find(name);
        if (existing != null) return existing.gameObject;
        GameObject root = new(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        return root;
    }

    private static void Frame(Transform parent)
    {
        RectTransform frame = (RectTransform)Rect("CardFrame", parent).transform;
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(1, 1); frame.offsetMax = new Vector2(-1, -1);
        var layout = frame.GetComponent<LayoutElement>() ?? frame.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        foreach (string name in new[] { "Top", "Bottom", "Left", "Right" })
        {
            bool horizontal = name == "Top" || name == "Bottom";
            bool far = name == "Top" || name == "Right";
            Image image = Rect(name, frame).GetComponent<Image>();
            if (image == null) image = frame.Find(name).gameObject.AddComponent<Image>();
            RectTransform rect = image.rectTransform;
            rect.anchorMin = horizontal ? new Vector2(0, far ? 1 : 0) : new Vector2(far ? 1 : 0, 0);
            rect.anchorMax = horizontal ? new Vector2(1, far ? 1 : 0) : new Vector2(far ? 1 : 0, 1);
            rect.pivot = horizontal ? new Vector2(.5f, far ? 1 : 0) : new Vector2(far ? 1 : 0, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = horizontal ? new Vector2(0, 2) : new Vector2(2, 0);
            image.color = Border; image.raycastTarget = false;
        }
        frame.SetAsLastSibling();
    }

    private static void Line(Transform parent, string name, bool top, Vector2 position, Vector2 size)
    {
        GameObject root = Rect(name, parent);
        Image image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0, top ? 1 : 0); rect.anchorMax = new Vector2(1, top ? 1 : 0);
        rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        image.color = Border; image.raycastTarget = false;
    }
}
