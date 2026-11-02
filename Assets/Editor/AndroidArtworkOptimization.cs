using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reapplies the Android illustration compression policy without changing sprite identities.</summary>
public static class AndroidArtworkOptimization
{
    private const string Output = "data/android-size-20261022";
    private static readonly string[] MainRoots = {
        "Assets/Resources/Art/Building", "Assets/Resources/Art/WorkshopIcons", "Assets/Resources/Art/Sector"
    };

    [Serializable] private sealed class TextureRecord
    {
        public string path, guid, importedFormat;
        public int width, height;
        public long runtimeBytes, astcPayloadBytes;
        public bool mipmaps;
    }
    [Serializable] private sealed class ImportReport
    {
        public string activeTarget;
        public int buildings, workshops, resources, sectors, music;
        public List<TextureRecord> textures = new();
    }

    [MenuItem("Tools/Kingdom/Optimize Android Artwork Imports")]
    public static void Apply()
    {
        Directory.CreateDirectory(Output);
        var report = new ImportReport { activeTarget = EditorUserBuildSettings.activeBuildTarget.ToString() };
        foreach (string root in MainRoots)
            foreach (string path in TexturePaths(root))
                Configure(path, 512, TextureImporterFormat.ASTC_6x6, true, report);
        foreach (string path in TexturePaths("Assets/Resources/Art/Story"))
            Configure(path, 2048, TextureImporterFormat.ASTC_6x6, false, report);
        Configure("Assets/Resources/Art/Metals/MetalInventoryAtlas.png", 1024,
            TextureImporterFormat.ASTC_4x4, false, report);
        foreach (string root in new[] { "Assets/Resources/Art/UI/Backgrounds", "Assets/Resources/Art/UI/EraCards" })
            foreach (string path in TexturePaths(root))
                Configure(path, 4096, TextureImporterFormat.ASTC_6x6, false, report);

        foreach (string path in Directory.GetFiles("Assets/Musics/PMusic", "*.ogg", SearchOption.AllDirectories))
        {
            // The postprocessor owns the audio policy; reimport applies its Android-only override.
            AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            AudioImporterSampleSettings settings = importer.GetOverrideSampleSettings("Android");
            if (!importer.ContainsSampleSettingsOverride("Android") || Mathf.Abs(settings.quality - PMusicAudioImporter.AndroidQuality) > 0.001f ||
                settings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate ||
                settings.sampleRateOverride != PMusicAudioImporter.AndroidSampleRate ||
                settings.loadType != AudioClipLoadType.Streaming || settings.compressionFormat != AudioCompressionFormat.Vorbis)
                throw new InvalidDataException("Android music settings differ: " + path);
            report.music++;
        }
        foreach (var item in Resources.LoadAll<BuildingDef>("Datas/Building")) { Require(item.Icon, item.Id); report.buildings++; }
        foreach (var item in Resources.LoadAll<WorkshopUpgradeDef>("Datas/Workshop")) { Require(item.Icon, item.Id); report.workshops++; }
        foreach (var item in Resources.LoadAll<ResourceDef>("Datas/Resource")) { Require(item.Sprite, item.Id); report.resources++; }
        foreach (var item in Resources.LoadAll<SectorDef>("Datas/Sector")) { Require(item.Icon, item.Id); report.sectors++; }
        if (report.buildings != 79 || report.workshops != 92 || report.resources != 33 || report.sectors != 10 || report.music != 56)
            throw new InvalidDataException("Definition/music inventory changed; review optimization scope.");
        File.WriteAllText(Output + "/imports.json", JsonUtility.ToJson(report, true));
        Capture();
        Debug.Log($"[AndroidArtwork] Textures={report.textures.Count}; Buildings={report.buildings}; Workshops={report.workshops}; Resources={report.resources}; Sectors={report.sectors}; Music={report.music}; Target={report.activeTarget}; complete.");
    }

    public static void ApplyFromCommandLine()
    {
        try { Apply(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static IEnumerable<string> TexturePaths(string root)
    {
        string[] paths = Directory.GetFiles(root, "*.png", SearchOption.AllDirectories);
        Array.Sort(paths, StringComparer.Ordinal);
        foreach (string path in paths) yield return path.Replace('\\', '/');
    }

    private static void Configure(string path, int maxSize, TextureImporterFormat format, bool mipmaps, ImportReport report)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        string guid = AssetDatabase.AssetPathToGUID(path);
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = mipmaps;
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Android");
        settings.name = "Android";
        settings.overridden = true;
        settings.maxTextureSize = maxSize;
        settings.format = format;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.compressionQuality = 100;
        settings.crunchedCompression = false; // ASTC has no Crunch encoding.
        importer.SetPlatformTextureSettings(settings);
        importer.SaveAndReimport();
        if (AssetDatabase.AssetPathToGUID(path) != guid) throw new InvalidDataException("GUID changed: " + path);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new InvalidDataException("Missing texture: " + path);
        if (maxSize == 512 && (texture.width != 512 || texture.height != 512))
            throw new InvalidDataException("Main illustration must be 512 square: " + path);
        long payload = 0;
        int width = texture.width, height = texture.height;
        int block = format == TextureImporterFormat.ASTC_6x6 ? 6 : 4;
        do
        {
            payload += (long)((width + block - 1) / block) * ((height + block - 1) / block) * 16;
            if (!mipmaps || (width == 1 && height == 1)) break;
            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
        } while (true);
        report.textures.Add(new TextureRecord { path = path, guid = guid, width = texture.width, height = texture.height,
            importedFormat = texture.format.ToString(), runtimeBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),
            astcPayloadBytes = payload, mipmaps = mipmaps });
    }

    private static void Require(Sprite sprite, string id)
    {
        if (sprite == null || sprite.rect.xMin < 0 || sprite.rect.yMin < 0 ||
            sprite.rect.xMax > sprite.texture.width || sprite.rect.yMax > sprite.texture.height)
            throw new InvalidDataException("Invalid sprite: " + id);
    }

    private static void Capture()
    {
        var canvasObject = new GameObject("ArtworkCompressionPreview", typeof(Canvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        var cameraObject = new GameObject("ArtworkCompressionCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.04f, 0.07f);
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        var target = new RenderTexture(1200, 640, 24);
        camera.targetTexture = target;
        string[] paths = { "Art/WorkshopIcons/DraftingTables", "Art/WorkshopIcons/ReinforcedBoilers",
            "Art/Building/Industrial/ChemicalPlant", "Art/Sector/AzurePool" };
        for (int i = 0; i < paths.Length; i++)
        {
            var tile = new GameObject(paths[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tile.transform.SetParent(canvas.transform, false);
            var rect = tile.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(24 + i * 294, -24);
            rect.sizeDelta = new Vector2(270, 270);
            tile.GetComponent<Image>().sprite = Resources.Load<Sprite>(paths[i]);
            var small = UnityEngine.Object.Instantiate(tile, canvas.transform);
            var smallRect = small.GetComponent<RectTransform>();
            smallRect.anchoredPosition = new Vector2(24 + i * 294, -340);
            smallRect.sizeDelta = new Vector2(136, 136);
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var screenshot = new Texture2D(1200, 640, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, 1200, 640), 0, 0);
        screenshot.Apply();
        File.WriteAllBytes(Output + "/compressed-preview.png", screenshot.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        target.Release();
        UnityEngine.Object.DestroyImmediate(screenshot);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(canvasObject);
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }
}
