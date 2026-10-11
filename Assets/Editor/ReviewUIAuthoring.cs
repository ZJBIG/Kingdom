using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authors the fixed controls used by the October review improvements.</summary>
public static class ReviewUIAuthoring
{
    [MenuItem("Tools/Kingdom/Review/Author Controls")]
    public static void Author()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset");
        if (font == null) throw new InvalidOperationException("Missing Kingdom font.");
        GameObject root = Rect("ReviewControls", null);
        try
        {
            GameObject detail = Rect("DetailTools", root.transform);
            RectTransform d = (RectTransform)detail.transform;
            d.anchorMin = new Vector2(0, 1); d.anchorMax = Vector2.one;
            d.pivot = new Vector2(.5f, 1); d.sizeDelta = new Vector2(-44, 54); d.anchoredPosition = new Vector2(0, -14);
            HorizontalLayoutGroup dl = detail.AddComponent<HorizontalLayoutGroup>();
            dl.spacing = 8; dl.childControlHeight = true; dl.childControlWidth = true;
            dl.childForceExpandWidth = true; dl.childForceExpandHeight = true;
            Button("Locate", detail.transform, font, "定位执行行");
            Button("Earlier", detail.transform, font, "提前");
            Button("Later", detail.transform, font, "后移");
            Button("Repair", detail.transform, font, "维修舰队");

            GameObject tools = Rect("PageTools", root.transform);
            RectTransform t = (RectTransform)tools.transform;
            t.anchorMin = new Vector2(1, .5f); t.anchorMax = new Vector2(1, .5f);
            t.pivot = new Vector2(1, .5f); t.sizeDelta = new Vector2(400, 64); t.anchoredPosition = Vector2.zero;
            HorizontalLayoutGroup tl = tools.AddComponent<HorizontalLayoutGroup>();
            tl.spacing = 8; tl.childControlHeight = true; tl.childControlWidth = true; tl.childForceExpandWidth = true;
            Button("CurrentResearch", tools.transform, font, "定位当前研究");
            Button("ResetResearch", tools.transform, font, "重置研究视图");
            Button("Offline", tools.transform, font, "展开全部离线变化");
            Button("Save", tools.transform, font, "保存 / 重试");

            GameObject confirmation = Rect("Confirmation", root.transform);
            confirmation.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            Image shade = confirmation.AddComponent<Image>(); shade.color = new Color(0, 0, 0, .8f);
            GameObject panel = Rect("Panel", confirmation.transform);
            RectTransform p = (RectTransform)panel.transform;
            p.anchorMin = new Vector2(.15f, .12f); p.anchorMax = new Vector2(.85f, .88f); p.offsetMin = p.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(.075f, .095f, .105f, 1);
            GameObject viewport = Rect("Viewport", panel.transform);
            RectTransform v = (RectTransform)viewport.transform; v.offsetMin = new Vector2(24, 92); v.offsetMax = new Vector2(-24, -24);
            viewport.AddComponent<Image>().color = Color.clear; viewport.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.inertia = false;
            GameObject content = Rect("Content", viewport.transform);
            RectTransform c = (RectTransform)content.transform; c.anchorMin = new Vector2(0, 1); c.anchorMax = Vector2.one;
            c.pivot = new Vector2(.5f, 1); c.sizeDelta = Vector2.zero;
            VerticalLayoutGroup cl = content.AddComponent<VerticalLayoutGroup>(); cl.childControlWidth = true; cl.childControlHeight = true; cl.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text("Warning", content.transform, font, "操作影响"); scroll.viewport = v; scroll.content = c;
            GameObject actions = Rect("Actions", panel.transform);
            RectTransform a = (RectTransform)actions.transform; a.anchorMax = new Vector2(1, 0); a.pivot = new Vector2(.5f, 0);
            a.sizeDelta = new Vector2(-48, 64); a.anchoredPosition = new Vector2(0, 16);
            HorizontalLayoutGroup al = actions.AddComponent<HorizontalLayoutGroup>(); al.spacing = 16; al.childControlWidth = true; al.childControlHeight = true; al.childForceExpandWidth = true;
            Button("Confirm", actions.transform, font, "确认操作"); Button("Cancel", actions.transform, font, "取消");
            confirmation.SetActive(false);
            if (PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/UI/Kingdom/KingdomUIReviewControls.prefab") == null)
                throw new InvalidOperationException("Failed to author review controls.");
            AssetDatabase.SaveAssets();
            ReservePageTools();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void ReservePageTools()
    {
        const string path = "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform pageTool = root.transform.Find("SafeAreaRoot/Content/PageTool");
            if (pageTool == null) throw new InvalidOperationException("Missing authored PageTool.");
            foreach (string name in new[] { "ResearchQueueViewport", "OverviewNavigationToolbar" })
            {
                RectTransform rect = pageTool.Find(name) as RectTransform;
                if (rect == null) throw new InvalidOperationException("Missing authored " + name);
                Vector2 before = rect.offsetMax;
                rect.offsetMax = new Vector2(-420, before.y);
                Debug.Log("[ReviewAuthoring] " + name + " offsetMax " + before + " -> " + rect.offsetMax);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static GameObject Rect(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform)); result.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)result.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return result;
    }
    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value)
    {
        TextMeshProUGUI text = Rect(name, parent).AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = 26;
        text.text = value; text.color = new Color(.92f, .89f, .8f, 1); text.raycastTarget = false; text.enableWordWrapping = true;
        return text;
    }
    private static void Button(string name, Transform parent, TMP_FontAsset font, string value)
    {
        GameObject go = Rect(name, parent); Image image = go.AddComponent<Image>(); image.color = new Color(.18f, .31f, .27f, 1);
        Button button = go.AddComponent<Button>(); button.targetGraphic = image; go.AddComponent<LayoutElement>().preferredHeight = 64;
        TMP_Text label = Text("Label", go.transform, font, value); label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.offsetMin = new Vector2(8, 4); label.rectTransform.offsetMax = new Vector2(-8, -4);
    }
}
