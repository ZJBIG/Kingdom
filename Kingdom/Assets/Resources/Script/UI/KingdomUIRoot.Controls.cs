using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
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
        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (sharedFontAsset != null)
            label.font = sharedFontAsset;
        return label;
    }

    private static Button Button(string name, Transform parent, string text, Color color, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, UnityEngine.Events.UnityAction action = null)
    {
        RectTransform rect = Rect(name, parent, min, max, offsetMin, offsetMax);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ApplyButtonColors(button, color);
        if (action != null)
            button.onClick.AddListener(action);
        Label("Text", rect, text, 28, TextPrimary, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        return button;
    }

    private static void ApplyButtonColors(Button button, Color baseColor)
    {
        ColorBlock colors = button.colors;
        // The Image already owns the button's base color. ColorBlock values
        // are multipliers; using baseColor here would multiply the color twice
        // and make dark panels render almost black.
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(.72f, .72f, .72f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static void Card(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, string text)
    {
        RectTransform card = Rect(name, parent, min, max, offsetMin, offsetMax);
        PanelRect("Surface", card, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelRaised);
        Label("Text", card, text, 24, TextPrimary, Vector2.zero, Vector2.one, new Vector2(28, 24), new Vector2(-28, -24));
    }
}
