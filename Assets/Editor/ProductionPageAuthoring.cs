using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ProductionPageAuthoring
{
    private const string RootPath = "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab";

    [MenuItem("Tools/Kingdom/UI/Author Production Page")]
    public static void Author()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RootPath);
        try
        {
            Transform safe = root.transform.Find("SafeAreaRoot");
            Transform host = safe.Find("Content/PageHost");
            Transform navigation = safe.Find("LeftNavigation/NavigationButtons");
            if (host == null || navigation == null) throw new InvalidOperationException("Missing authored page shell.");
            Transform nav = navigation.Find("Nav_Production");
            if (nav == null)
            {
                nav = UnityEngine.Object.Instantiate(navigation.Find("Nav_Resources").gameObject, navigation, false).transform;
                nav.name = "Nav_Production";
                nav.SetSiblingIndex(navigation.Find("Nav_Buildings").GetSiblingIndex() + 1);
            }
            nav.GetComponentInChildren<TMP_Text>(true).text = "生产树";
            // Ten 80px buttons plus nine 10px gaps retain the existing navigation footprint.
            navigation.GetComponent<VerticalLayoutGroup>().spacing = 10f;

            RectTransform page = Rect("Production", host);
            Stretch(page);
            RectTransform viewport = Rect("GraphViewport", page);
            Stretch(viewport);
            Image clipImage = GetOrAdd<Image>(viewport.gameObject);
            clipImage.color = Color.white;
            clipImage.raycastTarget = true;
            GetOrAdd<Mask>(viewport.gameObject).showMaskGraphic = false;
            GetOrAdd<RectMask2D>(viewport.gameObject);
            RectTransform graph = Rect("GraphContent", viewport);
            graph.anchorMin = graph.anchorMax = Vector2.zero;
            graph.pivot = Vector2.zero;
            graph.anchoredPosition = Vector2.zero;
            graph.sizeDelta = new Vector2(400, 400);
            ScrollRect scroll = GetOrAdd<ScrollRect>(viewport.gameObject);
            scroll.viewport = viewport; scroll.content = graph;
            scroll.horizontal = scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.enabled = false;
            GetOrAdd<UIResearchGraphGesture>(viewport.gameObject);
            page.gameObject.SetActive(false);

            TMP_FontAsset font = nav.GetComponentInChildren<TMP_Text>(true).font;
            RectTransform tools = Rect("ProductionTools", safe.Find("Content/PageTool"));
            Stretch(tools);
            TMP_Text heading = GetOrAdd<TextMeshProUGUI>(Rect("Heading", tools).gameObject);
            heading.font = font; heading.fontSize = 22f; heading.color = new Color(.92f, .89f, .8f, 1f);
            heading.text = "从资源或建筑详情打开生产树";
            heading.enableWordWrapping = false; heading.overflowMode = TextOverflowModes.Ellipsis;
            heading.alignment = TextAlignmentOptions.MidlineLeft; heading.raycastTarget = false;
            RectTransform hr = heading.rectTransform;
            hr.anchorMin = new Vector2(0, 1); hr.anchorMax = Vector2.one; hr.pivot = new Vector2(.5f, 1);
            hr.offsetMin = new Vector2(250, -78); hr.offsetMax = new Vector2(-184, -20);
            RectTransform resetRect = Rect("Reset", tools);
            resetRect.anchorMin = resetRect.anchorMax = Vector2.one;
            resetRect.pivot = Vector2.one; resetRect.sizeDelta = new Vector2(144, 56);
            resetRect.anchoredPosition = new Vector2(-28, -20);
            Button reset = GetOrAdd<Button>(resetRect.gameObject);
            reset.targetGraphic = GetOrAdd<Image>(resetRect.gameObject);
            TMP_Text resetLabel = GetOrAdd<TextMeshProUGUI>(Rect("Label", resetRect).gameObject);
            Stretch(resetLabel.rectTransform); resetLabel.font = font; resetLabel.fontSize = 26f;
            resetLabel.text = "定位根节点"; resetLabel.color = heading.color;
            resetLabel.alignment = TextAlignmentOptions.Center; resetLabel.raycastTarget = false;
            KingdomButtonAuthoring.Apply(reset);
            tools.gameObject.SetActive(false);
            if (PrefabUtility.SaveAsPrefabAsset(root, RootPath) == null)
                throw new InvalidOperationException("Failed to save production page.");
            AssetDatabase.SaveAssets();
            Debug.Log("[ProductionUI] Authored independent production page, navigation and graph tools.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
    private static RectTransform Rect(string name, Transform parent)
    {
        if (parent.Find(name) is RectTransform existing) return existing;
        GameObject go = new(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
