using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class RelicUIAuthoring
{
    private const string PrefabPath = "Assets/Resources/UI/Kingdom/KingdomUIRelicActions.prefab";

    [MenuItem("Tools/Kingdom/Relic/Author Actions")]
    public static void CreatePrefab()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset");
        if (font == null)
            throw new InvalidOperationException("Missing existing Kingdom UI font.");
        GameObject root = Rect("RelicActions", null);
        try
        {
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = Vector2.zero;
            Image image = root.AddComponent<Image>();
            image.color = new Color(.16f, .19f, .19f, 1);
            image.raycastTarget = false;
            VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 18, 18);
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            root.AddComponent<LayoutElement>();
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text("Title", root.transform, font, "回声铸造环", 32);
            Text("Status", root.transform, font, "遗迹状态", 26);
            Text("Costs", root.transform, font, "投入与供给", 26);
            Text("Rules", root.transform, font,
                "修复保留铸造环，持续维护后制备支援；拆解放弃古代装置，经自主认证后在工坊付费制造。两条路线永久互斥，封存保留调查和已付款进度。\n支援仅用于已经开始的非本星系战役：整场食物和材料供应成本降为 85%。最多保有一份待用或服役支援，退出不返还；已派出的支援在封存后仍继续服役。关闭页面不停止调查或制造。", 26);
            Button("Investigate", root.transform, font, "开始调查");
            Button("Repair", root.transform, font, "选择修复路线");
            Button("Dismantle", root.transform, font, "选择拆解路线");
            Button("Prepare", root.transform, font, "制备支援");
            Button("Assign", root.transform, font, "为当前远星战役启用支援");
            Button("Suspend", root.transform, font, "封存遗迹");
            Button("Resume", root.transform, font, "恢复遗迹");
            GameObject confirmation = Rect("Confirmation", root.transform);
            VerticalLayoutGroup confirmationLayout = confirmation.AddComponent<VerticalLayoutGroup>();
            confirmationLayout.spacing = 12;
            confirmationLayout.childControlWidth = true;
            confirmationLayout.childControlHeight = true;
            confirmationLayout.childForceExpandWidth = true;
            confirmationLayout.childForceExpandHeight = false;
            Text("Warning", confirmation.transform, font, "确认永久路线", 26);
            Button("Confirm", confirmation.transform, font, "确认永久选择");
            Button("Cancel", confirmation.transform, font, "取消，保留选择");
            confirmation.SetActive(false);
            Text("Result", root.transform, font, string.Empty, 26);
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("Could not author relic actions prefab.");
            AssetDatabase.SaveAssets();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject Rect(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static TextMeshProUGUI Text(string name, Transform parent,
        TMP_FontAsset font, string value, float size)
    {
        TextMeshProUGUI text = Rect(name, parent).AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = new Color(.92f, .89f, .8f, 1);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static void Button(string name, Transform parent, TMP_FontAsset font, string value)
    {
        GameObject root = Rect(name, parent);
        Image background = root.AddComponent<Image>();
        background.color = new Color(.18f, .31f, .27f, 1);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        root.AddComponent<LayoutElement>().preferredHeight = 64;
        TextMeshProUGUI label = Text("Label", root.transform, font, value, 26);
        label.alignment = TextAlignmentOptions.Center;
        RectTransform rect = (RectTransform)label.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = new Vector2(-16, -8);
    }
}
