using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class KingdomOnboardingAuthoring
{
    private const string PrefabPath = "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab";
    private static readonly Color Gold = new Color(.96f, .74f, .39f, 1f);

    [MenuItem("Tools/Kingdom/Onboarding/Author Overlay")]
    public static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform safeArea = root.transform.Find("SafeAreaRoot");
            if (safeArea == null)
                throw new InvalidOperationException("Kingdom UI safe area is missing.");
            if (safeArea.Find("OnboardingOverlay") != null)
                throw new InvalidOperationException("Onboarding overlay is already authored; preserve its edits.");
            TMP_Text existing = root.GetComponentInChildren<TMP_Text>(true);
            TMP_FontAsset font = existing != null ? existing.font : null;
            if (font == null)
                throw new InvalidOperationException("Kingdom UI font is missing.");
            if (root.GetComponent<OnboardingInputGate>() == null)
                root.AddComponent<OnboardingInputGate>();

            RectTransform overlay = Rect("OnboardingOverlay", safeArea);
            Stretch(overlay);
            overlay.gameObject.AddComponent<CanvasGroup>();
            foreach (string side in new[] { "Top", "Bottom", "Left", "Right" })
            {
                Image blocker = Image("Dim" + side, overlay, new Color(.015f, .025f, .035f, .78f), true);
                Stretch((RectTransform)blocker.transform);
            }

            RectTransform highlight = Rect("Highlight", overlay);
            highlight.sizeDelta = new Vector2(240, 88);
            Edge("Top", highlight, new Vector2(0, 1), Vector2.one, new Vector2(0, 4));
            Edge("Bottom", highlight, Vector2.zero, new Vector2(1, 0), new Vector2(0, 4));
            Edge("Left", highlight, Vector2.zero, new Vector2(0, 1), new Vector2(4, 0));
            Edge("Right", highlight, new Vector2(1, 0), Vector2.one, new Vector2(4, 0));
            TMP_Text arrow = Text("Arrow", overlay, font, "▼", 48, TextAlignmentOptions.Center);
            arrow.color = Gold;
            ((RectTransform)arrow.transform).sizeDelta = new Vector2(64, 64);

            Image panelImage = Image("Panel", overlay, new Color(.055f, .08f, .09f, .99f), true);
            RectTransform panel = (RectTransform)panelImage.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0);
            panel.pivot = new Vector2(.5f, 0);
            panel.anchoredPosition = new Vector2(0, 32);
            panel.sizeDelta = new Vector2(620, 260);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(2, -2);

            TMP_Text title = Text("Title", panel, font, "新内容已解锁", 30, TextAlignmentOptions.TopLeft);
            Place((RectTransform)title.transform, new Vector2(24, -18), new Vector2(572, 48));
            title.color = Gold;
            TMP_Text body = Text("Body", panel, font, "点击高亮位置，了解刚解锁的内容。", 26, TextAlignmentOptions.TopLeft);
            Place((RectTransform)body.transform, new Vector2(24, -70), new Vector2(572, 108));
            Button("Next", panel, font, "明白了", new Vector2(328, -188), new Vector2(268, 54));
            Button("Skip", panel, font, "跳过本阶段", new Vector2(24, -188), new Vector2(280, 54));
            overlay.gameObject.SetActive(false);
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("Could not save onboarding overlay.");
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private static Image Image(string name, Transform parent, Color color, bool raycast)
    {
        Image image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static void Edge(string name, Transform parent, Vector2 min, Vector2 max, Vector2 size)
    {
        RectTransform edge = (RectTransform)Image(name, parent, Gold, false).transform;
        edge.anchorMin = min;
        edge.anchorMax = max;
        edge.sizeDelta = size;
        edge.anchoredPosition = Vector2.zero;
    }

    private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font,
        string content, float size, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.text = content;
        text.color = new Color(.94f, .91f, .83f, 1);
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Button(string name, Transform parent, TMP_FontAsset font,
        string caption, Vector2 position, Vector2 size)
    {
        Image image = Image(name, parent, new Color(.18f, .28f, .25f, 1), true);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Place((RectTransform)button.transform, position, size);
        TMP_Text label = Text("Label", button.transform, font, caption, 25, TextAlignmentOptions.Center);
        Stretch((RectTransform)label.transform);
        // The shared author treats "Next" as a music icon; this control needs its caption.
        button.name = "Onboarding" + name;
        KingdomButtonAuthoring.Apply(button);
        button.name = name;
    }
}
