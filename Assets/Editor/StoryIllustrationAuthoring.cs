using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authors story illustration imports and chapter presentation without changing story gates.</summary>
public static class StoryIllustrationAuthoring
{
    public const string CardPath = "Assets/Resources/UI/Kingdom/StoryChapterCard.prefab";
    private const string ArchivePath = "Assets/Resources/Datas/Story/StoryArchive.asset";
    private static Sprite buttonNormal;
    private static Sprite buttonHighlight;

    [MenuItem("Tools/Kingdom/Story/Import Illustration Assets")]
    public static void ImportIllustrations()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        StoryArchiveDefinition archive = LoadArchive();
        for (int i = 0; i < archive.Chapters.Count; i++)
        {
            string path = IllustrationPath(archive.Chapters[i]);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing story illustration: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                throw new InvalidOperationException("Story illustration did not import as Sprite: " + path);
        }
        Debug.Log("[StoryIllustrations] Imported " + archive.Chapters.Count +
            " single Sprites; no story definitions changed.");
    }

    [MenuItem("Tools/Kingdom/Story/Author Illustrated Chapter Card")]
    public static void AuthorStoryPresentation()
    {
        ImportButtonStyles();
        ImportIllustrations();
        StoryArchiveDefinition archive = LoadArchive();
        for (int i = 0; i < archive.Chapters.Count; i++)
        {
            StoryChapterDefinition chapter = archive.Chapters[i];
            var serialized = new SerializedObject(chapter);
            SerializedProperty illustration = serialized.FindProperty("illustration");
            if (illustration == null)
                throw new InvalidOperationException("Story chapter is missing its authored illustration field.");
            illustration.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(IllustrationPath(chapter));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        CreateCard();
        CreatePreview();
        AssetDatabase.SaveAssets();
        Debug.Log("[StoryIllustrations] Authored chapter card and assigned " +
            archive.Chapters.Count + " chapter illustrations.");
    }

    private static StoryArchiveDefinition LoadArchive()
    {
        var archive = AssetDatabase.LoadAssetAtPath<StoryArchiveDefinition>(ArchivePath);
        if (archive == null || archive.Chapters == null || archive.Chapters.Count == 0)
            throw new InvalidOperationException("Missing or empty story archive: " + ArchivePath);
        for (int i = 0; i < archive.Chapters.Count; i++)
            if (archive.Chapters[i] == null)
                throw new InvalidOperationException("Missing story chapter at archive index " + i);
        return archive;
    }

    private static string IllustrationPath(StoryChapterDefinition chapter) =>
        "Assets/Resources/Art/Story/Story_" + chapter.name + "_Main.png";

    [MenuItem("Tools/Kingdom/Story/Author Background Cards And Preview")]
    public static void AuthorBackgroundPresentation()
    {
        ImportButtonStyles();
        CreateCard();
        CreatePreview();
        AssetDatabase.SaveAssets();
        Debug.Log("[StoryIllustrations] Authored background cards and shared preview; story definitions unchanged.");
    }

    private static void ImportButtonStyles()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        buttonNormal = ImportButtonStyle("Assets/Resources/Art/UI/Story/StoryButtonNormal.png", new Rect(0, 149, 2172, 499));
        buttonHighlight = ImportButtonStyle("Assets/Resources/Art/UI/Story/StoryButtonHighlight.png", new Rect(3, 126, 2168, 505));
    }

    private static Sprite ImportButtonStyle(string path, Rect bounds)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing generated button: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false; importer.isReadable = false;
        importer.maxTextureSize = 2048; importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spritesheet = new[] { new SpriteMetaData {
            name = System.IO.Path.GetFileNameWithoutExtension(path), rect = bounds,
            alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f),
            border = new Vector4(240, 72, 240, 72) } };
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Button Sprite import failed: " + path);
        return sprite;
    }

    private static void CreateCard()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset");
        if (font == null) throw new InvalidOperationException("Missing Kingdom font.");
        GameObject root = RectObject("StoryChapterCard", null);
        try
        {
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(0, 180);
            Image background = root.AddComponent<Image>();
            background.raycastTarget = true;
            root.AddComponent<UIPageScrollDragForwarder>();
            StoryChapterLayout layout = root.AddComponent<StoryChapterLayout>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject frame = RectObject("IllustrationFrame", root.transform);
            Stretch((RectTransform)frame.transform);
            frame.AddComponent<LayoutElement>().ignoreLayout = true;
            Image image = RectObject("Illustration", frame.transform).AddComponent<Image>();
            Stretch(image.rectTransform); image.preserveAspect = true; image.raycastTarget = false;
            Image shade = RectObject("Shade", frame.transform).AddComponent<Image>();
            Stretch(shade.rectTransform); shade.color = new Color(0, 0, 0, .55f); shade.raycastTarget = false;
            frame.SetActive(false);

            GameObject header = RectObject("Header", root.transform);
            TextMeshProUGUI title = Text("Title", header.transform, font, 32, "剧情章节");
            title.enableWordWrapping = true;
            GameObject actions = RectObject("Actions", header.transform);
            HorizontalLayoutGroup actionLayout = actions.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 16; actionLayout.childControlWidth = true; actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = false; actionLayout.childForceExpandHeight = false;
            Button toggle = ButtonObject("StoryChapterToggle", actions.transform, font, "展开", 112, out TextMeshProUGUI toggleLabel);
            Button navigation = ButtonObject("StoryChapterNavigation", actions.transform, font, "前往", 112, out _);
            Button preview = ButtonObject("StoryIllustrationPreview", actions.transform, font, "预览插画", 160, out _);
            toggle.gameObject.AddComponent<UIPageScrollDragForwarder>();
            navigation.gameObject.AddComponent<UIPageScrollDragForwarder>();
            preview.gameObject.AddComponent<UIPageScrollDragForwarder>();

            GameObject viewport = RectObject("BodyViewport", root.transform);
            Image inputSurface = viewport.AddComponent<Image>(); inputSurface.color = Color.clear;
            viewport.AddComponent<RectMask2D>();
            StoryBodyScrollRect scroll = viewport.AddComponent<StoryBodyScrollRect>();
            TextMeshProUGUI body = Text("Body", viewport.transform, font, 28, "章节摘要");
            body.alignment = TextAlignmentOptions.TopLeft; body.enableWordWrapping = true;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0, 1); bodyRect.anchorMax = Vector2.one;
            bodyRect.pivot = new Vector2(.5f, 1); bodyRect.anchoredPosition = Vector2.zero; bodyRect.sizeDelta = Vector2.zero;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = (RectTransform)viewport.transform; scroll.content = bodyRect;
            scroll.horizontal = false; scroll.vertical = true; scroll.inertia = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;

            var serializedLayout = new SerializedObject(layout);
            SetReference(serializedLayout, "header", header.transform);
            SetReference(serializedLayout, "title", title);
            SetReference(serializedLayout, "actions", actions.transform);
            SetReference(serializedLayout, "bodyViewport", viewport.transform);
            SetReference(serializedLayout, "body", body);
            serializedLayout.ApplyModifiedPropertiesWithoutUndo();
            StoryChapterCard card = root.AddComponent<StoryChapterCard>();
            var serialized = new SerializedObject(card);
            SetReference(serialized, "background", background); SetReference(serialized, "title", title);
            SetReference(serialized, "body", body); SetReference(serialized, "illustration", image);
            SetReference(serialized, "illustrationFrame", frame); SetReference(serialized, "layout", layout);
            SetReference(serialized, "bodyScroll", scroll); SetReference(serialized, "previewButton", preview);
            SetReference(serialized, "toggleButton", toggle); SetReference(serialized, "toggleLabel", toggleLabel);
            SetReference(serialized, "navigationButton", navigation);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.SaveAsPrefabAsset(root, CardPath) == null) throw new InvalidOperationException("Could not author story card.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CreatePreview()
    {
        const string path = "Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform safeArea = root.transform.Find("SafeAreaRoot");
            if (safeArea == null) throw new InvalidOperationException("Missing safe area.");
            Transform existing = safeArea.Find("StoryIllustrationPreview");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            GameObject overlay = RectObject("StoryIllustrationPreview", safeArea);
            Stretch((RectTransform)overlay.transform);
            Button backdrop = RectObject("Backdrop", overlay.transform).AddComponent<Button>();
            Image shade = backdrop.gameObject.AddComponent<Image>();
            shade.color = new Color(0, 0, 0, .92f); backdrop.targetGraphic = shade; Stretch((RectTransform)backdrop.transform);
            GameObject viewport = RectObject("Viewport", overlay.transform);
            Stretch((RectTransform)viewport.transform);
            ((RectTransform)viewport.transform).offsetMin = new Vector2(48, 48);
            ((RectTransform)viewport.transform).offsetMax = new Vector2(-48, -128);
            Image input = viewport.AddComponent<Image>(); input.color = Color.clear;
            viewport.AddComponent<RectMask2D>();
            Image image = RectObject("Image", viewport.transform).AddComponent<Image>();
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.pivot = new Vector2(.5f, .5f); image.preserveAspect = true; image.raycastTarget = false;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset");
            GameObject header = RectObject("Header", overlay.transform);
            RectTransform headerRect = (RectTransform)header.transform;
            headerRect.anchorMin = new Vector2(0, 1); headerRect.anchorMax = Vector2.one;
            headerRect.pivot = new Vector2(.5f, 1); headerRect.sizeDelta = new Vector2(-96, 80);
            headerRect.anchoredPosition = new Vector2(0, -24);
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 16; headerLayout.childControlWidth = true; headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false; headerLayout.childForceExpandHeight = false;
            TextMeshProUGUI title = Text("Title", header.transform, font, 32, "插画预览");
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Button reset = ButtonObject("Reset", header.transform, font, "复位", 112, out _);
            Button close = ButtonObject("Close", header.transform, font, "关闭", 112, out _);
            StoryIllustrationPreview preview = overlay.AddComponent<StoryIllustrationPreview>();
            var serialized = new SerializedObject(preview);
            SetReference(serialized, "viewport", viewport.transform); SetReference(serialized, "image", image);
            SetReference(serialized, "title", title); SetReference(serialized, "closeButton", close);
            SetReference(serialized, "resetButton", reset); SetReference(serialized, "backdropButton", backdrop);
            serialized.ApplyModifiedPropertiesWithoutUndo(); overlay.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static GameObject RectObject(string name, Transform parent)
    {
        var result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, string content)
    {
        var text = RectObject(name, parent).AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = new Color(0.92f, 0.89f, 0.8f, 1);
        text.text = content;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private static Button ButtonObject(string name, Transform parent, TMP_FontAsset font, string content,
        float width, out TextMeshProUGUI label)
    {
        GameObject root = RectObject(name, parent);
        Image background = root.AddComponent<Image>();
        background.sprite = buttonNormal; background.type = Image.Type.Sliced;
        background.pixelsPerUnitMultiplier = 10; background.color = Color.white;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState { highlightedSprite = buttonHighlight,
            selectedSprite = buttonHighlight, pressedSprite = buttonHighlight, disabledSprite = buttonNormal };
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = 56;
        label = Text("Label", root.transform, font, 28, content);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        var rect = (RectTransform)label.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = new Vector2(-12, -8);
        return button;
    }

    private static void SetReference(SerializedObject target, string name, UnityEngine.Object value)
    {
        SerializedProperty property = target.FindProperty(name);
        if (property == null)
            throw new InvalidOperationException("Story card is missing serialized reference: " + name);
        property.objectReferenceValue = value;
    }
}
