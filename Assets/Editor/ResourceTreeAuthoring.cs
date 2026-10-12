using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ResourceTreeAuthoring
{
    [MenuItem("Tools/Kingdom/UI/Author Resource Production Tree")]
    public static void Author()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset");
        if (font == null) throw new InvalidOperationException("Missing Kingdom font.");
        GameObject surface = Rect("ResourceProductionTree", null);
        GameObject row = null;
        try
        {
            RectTransform root = (RectTransform)surface.transform;
            root.anchorMin = new Vector2(0, 1); root.anchorMax = Vector2.one;
            root.pivot = new Vector2(.5f, 1); root.sizeDelta = new Vector2(0, 80);
            GameObject mode = Button("Mode", surface.transform, font, "查看递归生产链");
            RectTransform m = (RectTransform)mode.transform;
            m.anchorMin = new Vector2(0, 1); m.anchorMax = Vector2.one;
            m.pivot = new Vector2(.5f, 1); m.offsetMin = new Vector2(34, -64); m.offsetMax = new Vector2(-34, -8);
            GameObject rows = Rect("Rows", surface.transform);
            RectTransform r = (RectTransform)rows.transform;
            r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one;
            r.pivot = new Vector2(.5f, 1); r.sizeDelta = new Vector2(-48, 0); r.anchoredPosition = new Vector2(0, -80);
            VerticalLayoutGroup layout = rows.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            rows.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Save(surface, "KingdomUIResourceTree");

            row = Rect("ProductionNode", null);
            row.AddComponent<Image>().color = new Color(.1f, .16f, .18f, .12f);
            row.AddComponent<LayoutElement>().preferredHeight = 110;
            GameObject accent = Rect("EraAccent", row.transform);
            accent.AddComponent<Image>().raycastTarget = false;
            RectTransform ar = (RectTransform)accent.transform;
            ar.anchorMax = new Vector2(0, 1); ar.offsetMin = Vector2.zero; ar.offsetMax = new Vector2(3, 0);
            GameObject expand = Button("Expand", row.transform, font, "+");
            RectTransform e = (RectTransform)expand.transform;
            e.anchorMax = new Vector2(0, 1); e.offsetMin = new Vector2(4, 10); e.offsetMax = new Vector2(52, -10);
            GameObject icon = Rect("Icon", row.transform);
            Image image = icon.AddComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
            RectTransform ir = (RectTransform)icon.transform;
            ir.anchorMax = new Vector2(0, 1); ir.offsetMin = new Vector2(58, 62); ir.offsetMax = new Vector2(90, -16);
            TMP_Text label = Text("Label", row.transform, font, string.Empty);
            label.fontSize = 25; label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = true;
            label.fontStyle |= FontStyles.Underline;
            label.gameObject.AddComponent<Button>().targetGraphic = label;
            label.rectTransform.offsetMin = new Vector2(96, 48); label.rectTransform.offsetMax = new Vector2(-12, -6);
            TMP_Text status = Text("Status", row.transform, font, string.Empty);
            status.fontSize = 19; status.color = new Color(.63f, .76f, .74f, 1);
            status.enableWordWrapping = true; status.overflowMode = TextOverflowModes.Ellipsis;
            status.rectTransform.offsetMin = new Vector2(58, 3); status.rectTransform.offsetMax = new Vector2(-12, -58);
            Save(row, "KingdomUIResourceTreeRow");
            AssetDatabase.SaveAssets();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(surface);
            if (row != null) UnityEngine.Object.DestroyImmediate(row);
        }
    }

    private static void Save(GameObject root, string name)
    {
        if (PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/UI/Kingdom/" + name + ".prefab") == null)
            throw new InvalidOperationException("Failed to author " + name);
    }
    private static GameObject Rect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero; return go;
    }
    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value)
    {
        TMP_Text text = Rect(name, parent).AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = 26;
        text.text = value; text.color = new Color(.92f, .89f, .8f, 1); text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft; return text;
    }
    private static GameObject Button(string name, Transform parent, TMP_FontAsset font, string value)
    {
        GameObject go = Rect(name, parent); Image image = go.AddComponent<Image>(); image.color = new Color(.16f, .29f, .28f, 1);
        go.AddComponent<Button>().targetGraphic = image;
        TMP_Text text = Text("Label", go.transform, font, value); text.alignment = TextAlignmentOptions.Center;
        KingdomButtonAuthoring.Apply(go.GetComponent<Button>());
        return go;
    }
}
