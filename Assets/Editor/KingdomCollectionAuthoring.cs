using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class KingdomCollectionAuthoring
{
    private const string Folder = "Assets/Resources/UI/Kingdom/";

    [MenuItem("Tools/Kingdom/UI/Author Collection Cards")]
    public static void Author()
    {
        foreach (string name in new[] { "ResourceCard", "BuildingCard", "WorkshopCard", "SectorRow" })
        {
            string path = Folder + "KingdomUI" + name + ".prefab";
            GameObject row = PrefabUtility.LoadPrefabContents(path);
            try { AuthorRow(row, name); PrefabUtility.SaveAsPrefabAsset(row, path); }
            finally { PrefabUtility.UnloadPrefabContents(row); }
        }
        AuthorSectorBuilding();
        string rootPath = Folder + "KingdomUIRoot.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(rootPath);
        try
        {
            Transform surface = root.transform.Find("SafeAreaRoot/DetailPanel/Surface");
            Button pin = Button(surface, "PinButton", "置顶");
            RectTransform rect = (RectTransform)pin.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-24, -14);
            rect.sizeDelta = new Vector2(52, 48);
            TMP_Text pinText = pin.GetComponentInChildren<TMP_Text>();
            pinText.fontSize = 18;
            pinText.rectTransform.offsetMin = new Vector2(2, 2);
            pinText.rectTransform.offsetMax = new Vector2(-2, -2);
            pin.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, rootPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        string reviewPath = Folder + "KingdomUIReviewControls.prefab";
        GameObject review = PrefabUtility.LoadPrefabContents(reviewPath);
        try
        {
            RectTransform tools = (RectTransform)review.transform.Find("DetailTools");
            tools.offsetMin = new Vector2(22, tools.offsetMin.y);
            tools.offsetMax = new Vector2(-88, tools.offsetMax.y);
            PrefabUtility.SaveAsPrefabAsset(review, reviewPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(review); }
        ResourceTreeAuthoring.Author();
        AssetDatabase.SaveAssets();
        Debug.Log("[CollectionUI] Authored pin controls, era accents, metadata and production tree templates.");
    }

    private static void AuthorRow(GameObject row, string kind)
    {
        Transform root = row.transform;
        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.preferredHeight = Mathf.Max(104, layout.preferredHeight);
        RectTransform rowRect = (RectTransform)root;
        rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, layout.preferredHeight);
        bool resource = kind == "ResourceCard";
        TMP_Text label = root.Find("Label").GetComponent<TMP_Text>();
        float right = label.rectTransform.anchorMax.x;
        float left = resource ? 154 : 74;
        SetTextRect(label, 0, right, .40f, 1, left, 8);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text metadata = Text(root, "Metadata", 20);
        SetTextRect(metadata, 0, right, 0, .40f, left, 8);
        metadata.color = new Color(.70f, .73f, .69f);
        metadata.enableWordWrapping = false;
        metadata.overflowMode = TextOverflowModes.Ellipsis;
        if (resource)
            ((RectTransform)root.Find("Icon")).anchoredPosition = new Vector2(108, 0);
        Button pin = Button(root, "PinButton", "置顶");
        RectTransform rect = (RectTransform)pin.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
        rect.pivot = new Vector2(0, .5f);
        rect.anchoredPosition = new Vector2(12, 0);
        rect.sizeDelta = new Vector2(52, 48);
        pin.GetComponentInChildren<TMP_Text>().fontSize = 18;
        pin.GetComponentInChildren<TMP_Text>().rectTransform.offsetMin = new Vector2(2, 2);
        pin.GetComponentInChildren<TMP_Text>().rectTransform.offsetMax = new Vector2(-2, -2);
        Transform stripe = root.Find("EraAccent");
        if (stripe == null)
        {
            stripe = new GameObject("EraAccent", typeof(RectTransform), typeof(Image)).transform;
            stripe.SetParent(root, false);
        }
        RectTransform stripeRect = (RectTransform)stripe;
        stripeRect.anchorMin = Vector2.zero;
        stripeRect.anchorMax = new Vector2(0, 1);
        stripeRect.pivot = new Vector2(0, .5f);
        stripeRect.anchoredPosition = Vector2.zero;
        stripeRect.sizeDelta = new Vector2(4, -12);
        stripe.GetComponent<Image>().raycastTarget = false;
        stripe.SetAsFirstSibling();
        if (kind == "SectorRow")
        {
            label.rectTransform.anchorMin = new Vector2(0, 1);
            label.rectTransform.anchorMax = new Vector2(right, 1);
            label.rectTransform.offsetMin = new Vector2(left, -62);
            label.rectTransform.offsetMax = new Vector2(-8, -8);
            metadata.rectTransform.anchorMin = new Vector2(0, 1);
            metadata.rectTransform.anchorMax = new Vector2(right, 1);
            metadata.rectTransform.offsetMin = new Vector2(left, -98);
            metadata.rectTransform.offsetMax = new Vector2(-8, -63);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(12, -52);
            RectTransform buildings = root.Find("Buildings") as RectTransform;
            buildings.anchorMin = new Vector2(.82f, 1);
            buildings.anchorMax = new Vector2(.98f, 1);
            buildings.offsetMin = new Vector2(4, -80);
            buildings.offsetMax = new Vector2(-4, -24);
        }
    }

    private static void AuthorSectorBuilding()
    {
        string path = Folder + "KingdomUISectorBuildingCard.prefab";
        bool loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject card = !loaded
            ? new GameObject("KingdomUISectorBuildingCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement))
            : PrefabUtility.LoadPrefabContents(path);
        try
        {
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 124);
            card.GetComponent<LayoutElement>().preferredHeight = 124;
            card.GetComponent<Image>().raycastTarget = false;
            TMP_Text name = Text(card.transform, "Name", 28);
            name.fontStyle |= FontStyles.Underline;
            SetTextRect(name, 0, .28f, .50f, 1, 74, 8);
            Button nameButton = name.GetComponent<Button>() ?? name.gameObject.AddComponent<Button>();
            nameButton.targetGraphic = name; name.raycastTarget = true;
            if (name.GetComponent<UIPageScrollDragForwarder>() == null) name.gameObject.AddComponent<UIPageScrollDragForwarder>();
            SetTextRect(Text(card.transform, "Amount", 22), .28f, .43f, .50f, 1, 8, 8);
            SetTextRect(Text(card.transform, "Metadata", 20), 0, .28f, 0, .50f, 74, 8);
            SetTextRect(Text(card.transform, "Effect", 20), .28f, .64f, 0, .50f, 8, 8);
            SetTextRect(Text(card.transform, "Cost", 20), .43f, .64f, .50f, 1, 8, 8);
            foreach (string action in new[] { "Build", "Deconstruct" })
            {
                Button button = Button(card.transform, action, action == "Build" ? "建造" : "拆除");
                RectTransform rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(action == "Build" ? .83f : .66f, 0);
                rect.anchorMax = new Vector2(action == "Build" ? .99f : .82f, 1);
                rect.offsetMin = new Vector2(4, 24); rect.offsetMax = new Vector2(-4, -24);
            }
            Button pin = Button(card.transform, "PinButton", "置顶");
            RectTransform pinRect = (RectTransform)pin.transform;
            pinRect.anchorMin = pinRect.anchorMax = new Vector2(0, .5f);
            pinRect.pivot = new Vector2(0, .5f); pinRect.anchoredPosition = new Vector2(12, 0);
            pinRect.sizeDelta = new Vector2(52, 48);
            pin.GetComponentInChildren<TMP_Text>().fontSize = 18;
            pin.GetComponentInChildren<TMP_Text>().rectTransform.offsetMin = new Vector2(2, 2);
            pin.GetComponentInChildren<TMP_Text>().rectTransform.offsetMax = new Vector2(-2, -2);
            PrefabUtility.SaveAsPrefabAsset(card, path);
        }
        finally { if (loaded) PrefabUtility.UnloadPrefabContents(card); else UnityEngine.Object.DestroyImmediate(card); }
    }

    private static Button Button(Transform parent, string name, string caption)
    {
        Transform existing = parent.Find(name);
        if (existing == null)
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "KingdomUIButton.prefab");
            if (template == null) throw new InvalidOperationException("Missing authored action button template.");
            existing = UnityEngine.Object.Instantiate(template, parent, false).transform;
            existing.name = name;
        }
        Button button = existing.GetComponent<Button>();
        button.GetComponentInChildren<TMP_Text>().text = caption;
        return button;
    }

    private static TMP_Text Text(Transform parent, string name, float size)
    {
        Transform existing = parent.Find(name);
        if (existing == null)
        {
            existing = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).transform;
            existing.SetParent(parent, false);
        }
        TMP_Text text = existing.GetComponent<TMP_Text>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset");
        text.fontSize = size; text.raycastTarget = false;
        text.color = new Color(.92f, .89f, .80f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void SetTextRect(TMP_Text text, float left, float right, float bottom, float top, float inset, float end)
    {
        text.rectTransform.anchorMin = new Vector2(left, bottom);
        text.rectTransform.anchorMax = new Vector2(right, top);
        text.rectTransform.offsetMin = new Vector2(inset, 6);
        text.rectTransform.offsetMax = new Vector2(-end, -6);
    }
}
