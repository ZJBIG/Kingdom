using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class MusicAddressablesConfigurator
{
    public const string MusicRoot = "Assets/Musics/PMusic";
    public const string GroupName = "Kingdom Music";
    public const string CatalogAssetPath = "Assets/AddressableAssets/Music/MusicCatalog.asset";
    public const string MusicLabel = "music";

    private static readonly string[] Categories = { "Tense", "Day", "Night", "AllTime" };

    [MenuItem("Tools/Kingdom/Rebuild Music Addressables")]
    public static void Configure()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        AddressableAssetGroup group = GetOrCreateMusicGroup(settings);
        MusicCatalog catalog = GetOrCreateCatalog();
        List<MusicCatalogEntry> catalogEntries = new();
        HashSet<string> desiredGuids = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> trackIds = new(StringComparer.OrdinalIgnoreCase);

        settings.AddLabel(MusicLabel, false);
        for (int categoryIndex = 0; categoryIndex < Categories.Length; categoryIndex++)
        {
            string category = Categories[categoryIndex];
            string categoryLabel = MusicLabel + ":" + category.ToLowerInvariant();
            settings.AddLabel(categoryLabel, false);
            string categoryPath = MusicRoot + "/" + category;
            string[] assetPaths = Directory.GetFiles(categoryPath, "*.ogg", SearchOption.TopDirectoryOnly);
            Array.Sort(assetPaths, CompareAssetPaths);

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i].Replace('\\', '/');
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null)
                {
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                }
                if (clip == null)
                    throw new InvalidOperationException("Failed to import music clip: " + assetPath);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!trackIds.Add(clip.name))
                    throw new InvalidOperationException("Duplicate music track id: " + clip.name);

                string address = "music/" + category.ToLowerInvariant() + "/" + clip.name;
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = address;
                entry.SetLabel(MusicLabel, true, true, false);
                for (int labelIndex = 0; labelIndex < Categories.Length; labelIndex++)
                {
                    string knownCategoryLabel = MusicLabel + ":" +
                        Categories[labelIndex].ToLowerInvariant();
                    entry.SetLabel(knownCategoryLabel, false, true, false);
                }
                entry.SetLabel(categoryLabel, true, true, false);
                desiredGuids.Add(guid);
                catalogEntries.Add(new MusicCatalogEntry(clip.name,
                    MusicManager.DisplayNameFor(clip.name), category, address, clip.length));
            }
        }

        string catalogGuid = AssetDatabase.AssetPathToGUID(CatalogAssetPath);
        AddressableAssetEntry catalogEntry = settings.CreateOrMoveEntry(catalogGuid, group, false, false);
        catalogEntry.address = MusicCatalog.AddressableAddress;
        desiredGuids.Add(catalogGuid);

        List<AddressableAssetEntry> staleEntries = new(group.entries);
        for (int i = 0; i < staleEntries.Count; i++)
        {
            if (!desiredGuids.Contains(staleEntries[i].guid))
                settings.RemoveAssetEntry(staleEntries[i].guid, false);
        }

        catalog.ReplaceEntries(catalogEntries);
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("[MusicAddressables] Configured " + catalogEntries.Count + " tracks.");
    }

    [MenuItem("Tools/Kingdom/Build Music Addressables")]
    public static void ConfigureAndBuildFromCommandLine()
    {
        Configure();
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error))
            throw new InvalidOperationException("Addressables build failed: " + result.Error);
    }

    private static AddressableAssetGroup GetOrCreateMusicGroup(AddressableAssetSettings settings)
    {
        AddressableAssetGroup group = settings.FindGroup(GroupName);
        if (group == null)
        {
            group = settings.CreateGroup(GroupName, false, false, false, null,
                typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
        }

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema == null)
            schema = group.AddSchema<BundledAssetGroupSchema>();
        schema.BuildPath.SetVariableByName(settings, "Local.BuildPath");
        schema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
        schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
        schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        return group;
    }

    private static MusicCatalog GetOrCreateCatalog()
    {
        EnsureFolder("Assets/AddressableAssets");
        EnsureFolder("Assets/AddressableAssets/Music");
        MusicCatalog catalog = AssetDatabase.LoadAssetAtPath<MusicCatalog>(CatalogAssetPath);
        if (catalog != null)
            return catalog;
        catalog = ScriptableObject.CreateInstance<MusicCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        return catalog;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static int CompareAssetPaths(string leftPath, string rightPath)
    {
        string left = Path.GetFileNameWithoutExtension(leftPath);
        string right = Path.GetFileNameWithoutExtension(rightPath);
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
