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

    private static void CreateCard()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Fonts/SIMSUN SDF.asset");
        if (font == null)
            throw new InvalidOperationException("Missing existing Kingdom UI font.");
        GameObject root = RectObject("StoryChapterCard", null);
        try
        {
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 180);
            var background = root.AddComponent<Image>();
            background.color = new Color(0.16f, 0.19f, 0.19f, 1);
            background.raycastTarget = false;
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject header = RectObject("Header", root.transform);
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 16;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            header.AddComponent<LayoutElement>().minHeight = 64;
            TextMeshProUGUI title = Text("Title", header.transform, font, 32, "剧情章节");
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            title.enableWordWrapping = true;
            title.overflowMode = TextOverflowModes.Overflow;
            Button toggle = ButtonObject("StoryChapterToggle", header.transform, font, "展开", 112, out TextMeshProUGUI toggleLabel);
            Button navigation = ButtonObject("StoryChapterNavigation", header.transform, font, "前往", 160, out _);
            toggle.gameObject.AddComponent<UIPageScrollDragForwarder>();
            navigation.gameObject.AddComponent<UIPageScrollDragForwarder>();

            GameObject frame = RectObject("IllustrationFrame", root.transform);
            var illustrationLayout = frame.AddComponent<LayoutElement>();
            illustrationLayout.preferredHeight = 360;
            GameObject imageObject = RectObject("Illustration", frame.transform);
            var imageRect = (RectTransform)imageObject.transform;
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.sizeDelta = Vector2.zero;
            var image = imageObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            frame.SetActive(false);

            TextMeshProUGUI body = Text("Body", root.transform, font, 28, "章节摘要");
            body.alignment = TextAlignmentOptions.TopLeft;
            body.enableWordWrapping = true;
            var card = root.AddComponent<StoryChapterCard>();
            var serialized = new SerializedObject(card);
            SetReference(serialized, "background", background);
            SetReference(serialized, "title", title);
            SetReference(serialized, "body", body);
            SetReference(serialized, "illustration", image);
            SetReference(serialized, "illustrationLayout", illustrationLayout);
            SetReference(serialized, "toggleButton", toggle);
            SetReference(serialized, "toggleLabel", toggleLabel);
            SetReference(serialized, "navigationButton", navigation);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.SaveAsPrefabAsset(root, CardPath) == null)
                throw new InvalidOperationException("Could not author story chapter card prefab.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
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
        background.color = new Color(0.18f, 0.31f, 0.27f, 1);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
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
