using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AndroidSizePrepare
{
    public static void Run()
    {
        var changes = new List<string>();
        foreach (string raw in Directory.GetFiles("Assets/Resources/Art/UI", "*.png", SearchOption.AllDirectories))
        {
            string path = raw.Replace('\\', '/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var original = importer.GetDefaultPlatformTextureSettings();
            var android = importer.GetPlatformTextureSettings("Android");
            bool background = path.Contains("/Backgrounds/") || path.Contains("/EraCards/");
            if (!background && (android.overridden || original.textureCompression != TextureImporterCompression.Uncompressed))
                continue;
            string guid = AssetDatabase.AssetPathToGUID(path);
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = background ? 4096 : original.maxTextureSize;
            android.format = background ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
            android.textureCompression = TextureImporterCompression.CompressedHQ;
            android.compressionQuality = 100;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            if (AssetDatabase.AssetPathToGUID(path) != guid || AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                throw new InvalidDataException("Invalid compressed artwork: " + path);
            changes.Add(path + ": " + android.format + ", max=" + android.maxTextureSize);
        }
        int music = 0;
        foreach (string raw in Directory.GetFiles(MusicAddressablesConfigurator.MusicRoot, "*.ogg", SearchOption.AllDirectories))
        {
            string path = raw.Replace('\\', '/');
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var android = importer.GetOverrideSampleSettings("Android");
            if (!importer.ContainsSampleSettingsOverride("Android") || Math.Abs(android.quality - PMusicAudioImporter.AndroidQuality) > 0.001f ||
                android.sampleRateOverride != PMusicAudioImporter.AndroidSampleRate || android.loadType != AudioClipLoadType.Streaming)
                throw new InvalidDataException("Music import policy mismatch: " + path);
            music++;
        }
        if (music != 56) throw new InvalidDataException("Music inventory changed: " + music);
        AssetDatabase.SaveAssets();
        File.WriteAllLines("data/apk-size-20261101/import-changes.txt", changes);
        Debug.Log("[AndroidSizePrepare] textures=" + changes.Count + "; music=" + music + "; quality=" + PMusicAudioImporter.AndroidQuality);
    }
}
