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
        GameObject surface = Load("KingdomUIResourceTree", "ResourceProductionTree");
        GameObject row = null;
        try
        {
            RectTransform root = (RectTransform)surface.transform;
            root.anchorMin = new Vector2(0, 1); root.anchorMax = Vector2.one;
            root.pivot = new Vector2(.5f, 1); root.sizeDelta = new Vector2(0, 80);
            GameObject mode = Button("Mode", surface.transform, font, "查看生产树");
            RectTransform m = (RectTransform)mode.transform;
            m.anchorMin = new Vector2(0, 1); m.anchorMax = Vector2.one;
            m.pivot = new Vector2(.5f, 1); m.offsetMin = new Vector2(34, -64); m.offsetMax = new Vector2(-34, -8);
            Transform oldRows = surface.transform.Find("Rows");
            if (oldRows != null) UnityEngine.Object.DestroyImmediate(oldRows.gameObject);
            Save(surface, "KingdomUIResourceTree");

            row = Load("KingdomUIResourceTreeRow", "ProductionNode");
            UnityEngine.Object.DestroyImmediate(row.GetComponent<LayoutElement>());
            RectTransform nodeRect = (RectTransform)row.transform;
            nodeRect.anchorMin = nodeRect.anchorMax = new Vector2(0, 1);
            nodeRect.pivot = new Vector2(0, 1); nodeRect.sizeDelta = new Vector2(248, 88);
            Image nodeSurface = GetOrAdd<Image>(row);
            nodeSurface.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Buttons/ControlMetal.png");
            if (nodeSurface.sprite == null) throw new InvalidOperationException("Missing authored metal node material.");
            nodeSurface.type = Image.Type.Sliced;
            nodeSurface.pixelsPerUnitMultiplier = 8;
            nodeSurface.color = new Color(.32f, .36f, .34f, 1f);
            nodeSurface.raycastTarget = true;
            GameObject accent = Rect("EraAccent", row.transform);
            GetOrAdd<Image>(accent).raycastTarget = false;
            RectTransform ar = (RectTransform)accent.transform;
            ar.anchorMax = new Vector2(0, 1); ar.offsetMin = Vector2.zero; ar.offsetMax = new Vector2(3, 0);
            GameObject expand = Button("Expand", row.transform, font, "+");
            RectTransform e = (RectTransform)expand.transform;
            e.anchorMin = e.anchorMax = Vector2.one; e.pivot = Vector2.one;
            e.sizeDelta = new Vector2(28, 28); e.anchoredPosition = new Vector2(-4, -4);
            expand.transform.Find("Label").GetComponent<TMP_Text>().fontSize = 22;
            GameObject icon = Rect("Icon", row.transform);
            Image image = GetOrAdd<Image>(icon); image.preserveAspect = true; image.raycastTarget = false;
            RectTransform ir = (RectTransform)icon.transform;
            ir.anchorMin = ir.anchorMax = new Vector2(0, 1); ir.pivot = new Vector2(0, 1);
            ir.sizeDelta = new Vector2(32, 32); ir.anchoredPosition = new Vector2(12, -16);
            TMP_Text label = Text("Label", row.transform, font, string.Empty);
            label.fontSize = 24; label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = true;
            label.fontStyle &= ~FontStyles.Underline;
            GetOrAdd<Button>(label.gameObject).targetGraphic = label;
            label.rectTransform.offsetMin = new Vector2(48, 32); label.rectTransform.offsetMax = new Vector2(-40, -8);
            TMP_Text status = Text("Status", row.transform, font, string.Empty);
            status.fontSize = 16; status.color = new Color(.72f, .70f, .62f, 1);
            status.enableWordWrapping = false; status.overflowMode = TextOverflowModes.Ellipsis;
            status.rectTransform.offsetMin = new Vector2(12, 8); status.rectTransform.offsetMax = new Vector2(-12, -58);
            Save(row, "KingdomUIResourceTreeRow");
            GameObject link = Load("KingdomUIResourceTreeLink", "ProductionConnection");
            try
            {
                RectTransform line = (RectTransform)link.transform;
                line.anchorMin = line.anchorMax = new Vector2(0, 1);
                line.pivot = new Vector2(.5f, .5f); line.sizeDelta = new Vector2(2, 2);
                Image wire = GetOrAdd<Image>(link); wire.color = new Color(.64f, .59f, .45f, .72f); wire.raycastTarget = false;
                Save(link, "KingdomUIResourceTreeLink");
            }
            finally { Release(link); }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            Release(surface);
            if (row != null) Release(row);
        }
    }

    private static void Save(GameObject root, string name)
    {
        if (PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/UI/Kingdom/" + name + ".prefab") == null)
            throw new InvalidOperationException("Failed to author " + name);
    }
    private static T GetOrAdd<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();

    private static GameObject Load(string name, string objectName)
    {
        string path = "Assets/Resources/UI/Kingdom/" + name + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab == null ? Rect(objectName, null) : PrefabUtility.LoadPrefabContents(path);
    }

    private static void Release(GameObject go)
    {
        if (go.scene.IsValid() && UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(go.scene)) PrefabUtility.UnloadPrefabContents(go);
        else UnityEngine.Object.DestroyImmediate(go);
    }

    private static GameObject Rect(string name, Transform parent)
    {
        if (parent != null && parent.Find(name) != null) return parent.Find(name).gameObject;
        GameObject go = new(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero; return go;
    }
    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value)
    {
        TMP_Text text = GetOrAdd<TextMeshProUGUI>(Rect(name, parent)); text.font = font; text.fontSize = 26;
        text.text = value; text.color = new Color(.92f, .89f, .8f, 1); text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft; return text;
    }
    private static GameObject Button(string name, Transform parent, TMP_FontAsset font, string value)
    {
        GameObject go = Rect(name, parent); Image image = GetOrAdd<Image>(go); image.color = new Color(.16f, .29f, .28f, 1);
        GetOrAdd<Button>(go).targetGraphic = image;
        TMP_Text text = Text("Label", go.transform, font, value); text.alignment = TextAlignmentOptions.Center;
        KingdomButtonAuthoring.Apply(go.GetComponent<Button>());
        return go;
    }
}
