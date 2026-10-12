using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authors button materials while retaining their existing bindings and semantic colours.</summary>
public static class KingdomButtonAuthoring
{
    private const string Folder = "Assets/Resources/UI/Kingdom";
    private const string Art = "Assets/Resources/Art/UI/Buttons/";
    private static Sprite stone;
    private static Sprite wood;
    private static Sprite metal;

    [MenuItem("Tools/Kingdom/UI/Author All Button Materials")]
    public static void Author()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        stone = Import("NavigationStone");
        wood = Import("ActionWood");
        metal = Import("ControlMetal");
        CreateActionButton();
        int buttons = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                if (root.name == "KingdomUIRoot") { KingdomBackgroundAuthoring.Apply(root); changed = true; }
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                    if (Apply(button)) { buttons++; changed = true; }
                foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true))
                {
                    if (toggle.targetGraphic is Image image)
                    {
                        image.sprite = metal; image.type = Image.Type.Sliced;
                        image.pixelsPerUnitMultiplier = 14;
                        changed = true;
                    }
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[KingdomButtons] Authored " + buttons + " visible source buttons; transparent surfaces preserved.");
    }

    public static bool Apply(Button button)
    {
        if (button.name == "Backdrop" || !(button.targetGraphic is Image image)) return false;
        if (image.color.a < .01f && image.sprite == null) return false;
        bool story = HasAncestor(button.transform, "StoryChapterCard") || HasAncestor(button.transform, "StoryIllustrationPreview");
        if (story) return true;
        EnsureSprites();
        bool icon = button.name == "Previous" || button.name == "Next" || button.name == "Pause" || button.name == "PlayPause";
        bool row = button.name.StartsWith("KingdomUI", StringComparison.Ordinal) && button.name != "KingdomUIButton";
        bool research = button.name == "KingdomUIResearchNode";
        if (research) AuthorResearchLabels(button.transform);
        bool navigation = button.name.StartsWith("Nav_", StringComparison.Ordinal) || button.name == "OverviewCurrentTargetButton" || button.name == "OverviewCurrentEraButton";
        bool danger = button.name.Contains("Deconstruct") || button.name.Contains("Dismantle");
        bool primary = button.name == "Build" || button.name == "BuildButton" || button.name == "Upgrade" || button.name == "Purchase" || button.name == "PurchaseButton" || button.name == "Repair"
            || button.name == "Investigate" || button.name == "Prepare" || button.name == "Assign" || button.name == "Confirm"
            || button.name == "Action" || button.name == "DetailAction" || button.name == "MainActionButton" || button.name == "KingdomUIButton";
        if (icon) AuthorIcon(button, image);
        image.sprite = navigation ? stone : primary && !danger ? wood : metal;
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
        image.pixelsPerUnitMultiplier = research ? 40 : row ? 60 : 14;
        if (button.name == "KingdomUIBuildingCard" || button.name == "KingdomUIResourceCard" ||
            button.name == "KingdomUIResearchCard" || button.name == "KingdomUITextRow" || button.name == "KingdomUIWorkshopCard" ||
            button.name == "KingdomUIMusicTrack" || button.name == "KingdomUISectorRow")
            image.color = new Color(1, 1, 1, .05f);
        if (danger) image.color = new Color(.42f, .18f, .16f, 1);
        else if (icon || image.color == Color.white)
            image.color = primary ? new Color(.55f, .38f, .22f, 1) : new Color(.30f, .36f, .36f, 1);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1);
        colors.pressedColor = new Color(.66f, .66f, .66f, 1);
        colors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1);
        colors.disabledColor = new Color(.55f, .57f, .57f, .90f);
        colors.fadeDuration = .08f; colors.colorMultiplier = 1;
        button.colors = colors;
        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
        if (!row && !icon)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = true;
                label.overflowMode = TextOverflowModes.Overflow;
                label.raycastTarget = false;
                if (button.name.StartsWith("Quantity_", StringComparison.Ordinal)) label.fontSize = 30;
            }
        }
        return true;
    }

    private static void EnsureSprites()
    {
        if (stone == null) stone = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "NavigationStone.png");
        if (wood == null) wood = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "ActionWood.png");
        if (metal == null) metal = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "ControlMetal.png");
        if (stone == null || wood == null || metal == null) throw new InvalidOperationException("Author button material imports first.");
    }

    private static void AuthorResearchLabels(Transform node)
    {
        foreach (string name in new[] { "Label", "Cost", "Progress", "State" })
        {
            TMP_Text text = node.Find(name).GetComponent<TMP_Text>();
            bool title = name == "Label";
            float left = title || name == "Cost" ? 0 : name == "Progress" ? .27f : .67f;
            float right = title || name == "State" ? 1 : name == "Cost" ? .26f : .66f;
            text.rectTransform.anchorMin = new Vector2(left, title ? .42f : 0);
            text.rectTransform.anchorMax = new Vector2(right, title ? 1 : .42f);
            text.rectTransform.offsetMin = new Vector2(title ? 8 : 2, 1);
            text.rectTransform.offsetMax = new Vector2(title ? -8 : -2, -1);
            text.fontSize = title ? 18 : 13;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
        }
        node.Find("EraFrame").GetComponent<Image>().color = new Color(1, 1, 1, .16f);
    }

    private static bool HasAncestor(Transform current, string name)
    {
        for (; current != null; current = current.parent) if (current.name == name) return true;
        return false;
    }

    private static void AuthorIcon(Button button, Image background)
    {
        Transform existing = button.transform.Find("Icon");
        if (existing != null) return;
        GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(button.transform, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 12); rect.offsetMax = new Vector2(-12, -12);
        Image icon = go.GetComponent<Image>();
        icon.sprite = background.sprite; icon.color = Color.white;
        icon.type = Image.Type.Simple; icon.preserveAspect = true; icon.raycastTarget = false;
    }

    private static void CreateActionButton()
    {
        GameObject go = new GameObject("KingdomUIButton", typeof(RectTransform), typeof(Image), typeof(Button));
        try
        {
            ((RectTransform)go.transform).sizeDelta = new Vector2(160, 64);
            Button button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(go.transform, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset");
            text.fontSize = 28; text.color = new Color(.92f, .89f, .80f, 1); text.text = "操作";
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12, 6); text.rectTransform.offsetMax = new Vector2(-12, -6);
            go.AddComponent<UIPageScrollDragForwarder>();
            Apply(button);
            PrefabUtility.SaveAsPrefabAsset(go, Folder + "/KingdomUIButton.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static Sprite Import(string name)
    {
        string path = Art + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing generated button material: " + path);
        importer.textureType = TextureImporterType.Default; importer.isReadable = true;
        importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
        // Ignore isolated flecks outside the generated frame, retaining antialiased edges.
        for (int y = 0; y < texture.height; y++)
        {
            int coverage = 0;
            for (int x = 0; x < texture.width; x++) if (pixels[y * texture.width + x].a > 8) coverage++;
            if (coverage > texture.width / 20) { minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
        }
        minY = Math.Max(0, minY - 12); maxY = Math.Min(texture.height - 1, maxY + 12);
        for (int y = minY; y <= maxY; y++) for (int x = 0; x < texture.width; x++)
            if (pixels[y * texture.width + x].a > 8) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); }
        if (maxX < minX) throw new InvalidOperationException("Generated material is fully transparent: " + path);
        float height = maxY - minY + 1;
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritesheet = new[] { new SpriteMetaData { name = name,
            rect = new Rect(minX, minY, maxX - minX + 1, height), alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(.5f, .5f), border = new Vector4(height * .36f, height * .18f, height * .36f, height * .18f) } };
        importer.maxTextureSize = 2048; importer.isReadable = false; importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
