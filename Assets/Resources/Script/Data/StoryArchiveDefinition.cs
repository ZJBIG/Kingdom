using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StoryArchive", menuName = "数据/剧情档案", order = 0)]
public sealed class StoryArchiveDefinition : ScriptableObject
{
    [Tooltip("按剧情发生顺序排列。排序同时决定同一时代章节的串行解锁顺序。")]
    public List<StoryChapterDefinition> Chapters = new();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Chapters == null)
            return;

        string[] expectedIds =
        {
            "PrologueAshes_00", "FirstFire_01", "WallsAndShelter_02",
            "TheGrowingClan_03", "RememberedKnowledge_04", "TheFirstChain_05",
            "StoneAgeReturn_06", "MedievalOrder_07", "IndustrialAwakening_08",
            "WorkshopMemory_09", "IndustrialPower_10", "IndustrialMaterials_11",
            "IndustrialChemistry_12", "IndustrialFrontier_13", "FrontierSectors_14",
            "WarBetweenStars_15", "BeyondTheSky_16", "TheOldBoundary_17"
        };
        if (Chapters.Count != expectedIds.Length)
            Debug.LogError("剧情档案必须保持固定的 18 章主线顺序。", this);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TechLevel previousEra = TechLevel.Animal;
        for (int i = 0; i < Chapters.Count; i++)
        {
            StoryChapterDefinition chapter = Chapters[i];
            if (chapter == null)
                continue;
            ValidateChapter(chapter.Id, chapter.Title, chapter.Summary,
                chapter.Body, chapter.RequiredEra, ids, ref previousEra, i);
            ValidateReferences(chapter);
            if (i < expectedIds.Length && chapter.Id != expectedIds[i])
                Debug.LogError("剧情档案顺序或章节 ID 不符合固定主线：" + chapter.Id, chapter);
        }
        ValidateExplorationProgression();
    }

    private static void ValidateChapter(string id, string title, string summary,
        string body, TechLevel era, HashSet<string> ids,
        ref TechLevel previousEra, int index)
    {
        if (string.IsNullOrWhiteSpace(id) || !ids.Add(id.Trim()))
            Debug.LogError("剧情档案存在空编号或重复编号：" + id);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(summary) ||
            string.IsNullOrWhiteSpace(body))
            Debug.LogError("剧情章节缺少标题、摘要或正文：" + id);
        if (index > 0 && era < previousEra)
            Debug.LogError("剧情章节时代顺序倒退：" + id);
        previousEra = era;
    }

    private static void ValidateReferences(StoryChapterDefinition chapter)
    {
        ValidateList(chapter.RequiredResearch, chapter.Id, "研究");
        ValidateList(chapter.RequiredBuildings, chapter.Id, "建筑");
        ValidateList(chapter.RequiredWorkshops, chapter.Id, "工坊");
        ValidateList(chapter.RequiredUnlockedSectors, chapter.Id, "解锁星区");
        ValidateList(chapter.RequiredOccupiedSectors, chapter.Id, "占领星区");
        ValidateEraCompatibility(chapter);
    }

    private static void ValidateEraCompatibility(StoryChapterDefinition chapter)
    {
        for (int i = 0; chapter.RequiredResearch != null && i < chapter.RequiredResearch.Count; i++)
            if (chapter.RequiredResearch[i] != null &&
                chapter.RequiredResearch[i].TechLevel > chapter.RequiredEra)
                Debug.LogError("剧情章节研究时代高于章节时代：" + chapter.Id +
                    " -> " + chapter.RequiredResearch[i].Id);
        for (int i = 0; chapter.RequiredBuildings != null && i < chapter.RequiredBuildings.Count; i++)
            if (chapter.RequiredBuildings[i] != null &&
                chapter.RequiredBuildings[i].TechLevel > chapter.RequiredEra)
                Debug.LogError("剧情章节建筑时代高于章节时代：" + chapter.Id +
                    " -> " + chapter.RequiredBuildings[i].Id);
        for (int i = 0; chapter.RequiredWorkshops != null && i < chapter.RequiredWorkshops.Count; i++)
            if (chapter.RequiredWorkshops[i] != null &&
                chapter.RequiredWorkshops[i].TechLevel > chapter.RequiredEra)
                Debug.LogError("剧情章节工坊时代高于章节时代：" + chapter.Id +
                    " -> " + chapter.RequiredWorkshops[i].Id);
    }

    private void ValidateExplorationProgression()
    {
        StoryChapterDefinition moon = FindChapter("FrontierSectors_14");
        StoryChapterDefinition distant = FindChapter("WarBetweenStars_15");
        StoryChapterDefinition solar = FindChapter("BeyondTheSky_16");
        StoryChapterDefinition interstellar = FindChapter("TheOldBoundary_17");
        if (moon == null || distant == null || solar == null || interstellar == null)
            return;

        RequireOccupied(moon, "AzurePool");
        RequireUnlocked(moon, "AzurePool");
        RequireOccupied(distant, "AzurePool");
        RequireOccupied(distant, "Terminus");
        RequireOccupied(solar, "Terminus");
        RequireOccupied(solar, "HeliosCore");
        RequireOccupied(interstellar, "AlphaCentauri");
        RequireOccupied(interstellar, "ProximaB");
        RejectInterstellarSectors(moon, "月球阶段");
        RejectInterstellarSectors(distant, "偏远行星阶段");
        RejectInterstellarSectors(solar, "太阳系阶段");
    }

    private StoryChapterDefinition FindChapter(string id)
    {
        for (int i = 0; i < Chapters.Count; i++)
            if (Chapters[i] != null && Chapters[i].Id == id)
                return Chapters[i];
        return null;
    }

    private static void RequireOccupied(StoryChapterDefinition chapter, string id)
    {
        if (!Contains(chapter.RequiredOccupiedSectors, id))
            Debug.LogError("剧情阶段缺少占领星区条件：" + chapter.Id + " -> " + id);
    }

    private static void RequireUnlocked(StoryChapterDefinition chapter, string id)
    {
        if (!Contains(chapter.RequiredUnlockedSectors, id))
            Debug.LogError("剧情阶段缺少解锁星区条件：" + chapter.Id + " -> " + id);
    }

    private static bool Contains(IReadOnlyList<SectorDefinition> sectors, string id)
    {
        for (int i = 0; sectors != null && i < sectors.Count; i++)
            if (sectors[i] != null && sectors[i].Id == id)
                return true;
        return false;
    }

    private static void RejectInterstellarSectors(
        StoryChapterDefinition chapter, string stage)
    {
        RejectInterstellarUnlockedSectors(chapter, stage);
        for (int i = 0; chapter.RequiredOccupiedSectors != null &&
             i < chapter.RequiredOccupiedSectors.Count; i++)
        {
            SectorDefinition sector = chapter.RequiredOccupiedSectors[i];
            if (sector != null && sector.Domain == SectorDefinition.SectorDomain.Interstellar)
                Debug.LogError(stage + "不得引用系外星区：" + chapter.Id + " -> " + sector.Id);
        }
    }

    private static void RejectInterstellarUnlockedSectors(
        StoryChapterDefinition chapter, string stage)
    {
        for (int i = 0; chapter.RequiredUnlockedSectors != null &&
             i < chapter.RequiredUnlockedSectors.Count; i++)
        {
            SectorDefinition sector = chapter.RequiredUnlockedSectors[i];
            if (sector != null && sector.Domain == SectorDefinition.SectorDomain.Interstellar)
                Debug.LogError(stage + " cannot require an interstellar unlocked sector: " +
                    chapter.Id + " -> " + sector.Id);
        }
    }

    private static void ValidateList<T>(IReadOnlyList<T> values, string chapterId,
        string label) where T : UnityEngine.Object
    {
        if (values == null)
        {
            Debug.LogError("剧情章节缺少" + label + "条件列表：" + chapterId);
            return;
        }
        var references = new HashSet<T>();
        for (int i = 0; i < values.Count; i++)
            if (values[i] == null || !references.Add(values[i]))
                Debug.LogError("剧情章节的" + label + "条件存在空引用或重复引用：" + chapterId);
    }
#endif
}
