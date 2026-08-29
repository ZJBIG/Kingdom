using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canonical resource paths for reusable Kingdom UI Prefabs.
/// New UI code should resolve templates through this library instead of
/// repeating ad-hoc Resources paths.
/// </summary>
public static class KingdomUIPrefabLibrary
{
    private const string RootPath = "UI/Kingdom/";
    private static readonly Dictionary<string, GameObject> Cache =
        new Dictionary<string, GameObject>();

    public const string Root = "KingdomUIRoot";
    public const string Page = "KingdomUIPage";
    public const string ResourceCard = "KingdomUIResourceCard";
    public const string BuildingCard = "KingdomUIBuildingCard";
    public const string ResearchCard = "KingdomUIResearchCard";
    public const string TextRow = "KingdomUITextRow";
    public const string ResearchNode = "KingdomUIResearchNode";
    public const string ResearchLine = "KingdomUIResearchLine";
    public const string ResearchGraph = "KingdomUIResearchGraph";
    public const string OverviewNavigationToolbar = "KingdomUIOverviewNavigationToolbar";
    public const string ResearchEraBand = "KingdomUIResearchEraBand";
    public const string QuantityControls = "KingdomUIQuantityControls";
    public const string MusicTrack = "KingdomUIMusicTrack";
    public const string SectorRow = "KingdomUISectorRow";

    public static readonly string[] AllReusablePrefabs =
    {
        Root, Page, ResourceCard, BuildingCard, ResearchCard, TextRow,
        ResearchNode, ResearchLine, ResearchGraph, OverviewNavigationToolbar, ResearchEraBand,
        QuantityControls, MusicTrack, SectorRow
    };

    public static GameObject Load(string prefabName)
    {
        if (Cache.TryGetValue(prefabName, out GameObject cached))
            return cached;
        GameObject loaded = Resources.Load<GameObject>(RootPath + prefabName);
        if (loaded == null)
        {
            string path = RootPath + prefabName;
            Debug.LogError("[王国界面] Required reusable UI prefab is missing: " + path);
            throw new InvalidOperationException(
                "Required reusable UI prefab is missing: " + path);
        }
        Cache[prefabName] = loaded;
        return loaded;
    }

    public static int Preload(params string[] prefabNames)
    {
        int loadedCount = 0;
        for (int i = 0; i < prefabNames.Length; i++)
            if (Load(prefabNames[i]) != null)
                loadedCount++;
        return loadedCount;
    }

    public static int PreloadAll() => Preload(AllReusablePrefabs);

    public static GameObject Instantiate(string prefabName, Transform parent)
    {
        GameObject prefab = Load(prefabName);
        return UnityEngine.Object.Instantiate(prefab, parent, false);
    }
}
