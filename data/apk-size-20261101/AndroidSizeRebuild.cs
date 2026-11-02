using System;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;

public static class AndroidSizeRebuild
{
    public static void Run()
    {
        foreach (string path in System.IO.Directory.GetFiles(MusicAddressablesConfigurator.MusicRoot, "*.ogg", System.IO.SearchOption.AllDirectories))
            UnityEditor.AssetDatabase.ImportAsset(path.Replace('\\', '/'), UnityEditor.ImportAssetOptions.ForceSynchronousImport | UnityEditor.ImportAssetOptions.ForceUpdate);
        AddressableAssetSettings.CleanPlayerContent();
        BuildCache.PurgeCache(false);
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error))
            throw new InvalidOperationException(result.Error);
        Debug.Log("[AndroidSizeRebuild] Addressables rebuilt after clearing generated cache.");
        KingdomBuild.BuildAndroid();
    }
}
