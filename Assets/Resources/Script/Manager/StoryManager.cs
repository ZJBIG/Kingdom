using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Runtime view of one authored story chapter. Story text and chapter order are
/// stored in StoryArchive.asset; completion history is kept separately in GameState.
/// </summary>
public sealed class StoryChapter
{
    public string Id { get; }
    public string Title { get; }
    public string EraLabel { get; }
    public string Summary { get; }
    public string Body { get; }
    public Sprite Illustration { get; }
    public TechLevel RequiredEra { get; }
    public string RequiredTutorialStepId { get; }
    public IReadOnlyList<string> RequiredResearchIds { get; }
    public IReadOnlyList<string> RequiredBuildingIds { get; }
    public IReadOnlyList<string> RequiredWorkshopIds { get; }
    public IReadOnlyList<string> RequiredUnlockedSectorIds { get; }
    public IReadOnlyList<string> RequiredOccupiedSectorIds { get; }

    public StoryChapter(string id, string title, string eraLabel,
        string summary, string body, TechLevel requiredEra,
        string requiredTutorialStepId, IReadOnlyList<string> requiredResearchIds,
        IReadOnlyList<string> requiredBuildingIds,
        IReadOnlyList<string> requiredWorkshopIds,
        IReadOnlyList<string> requiredUnlockedSectorIds,
        IReadOnlyList<string> requiredOccupiedSectorIds, Sprite illustration = null)
    {
        Id = id;
        Title = title;
        EraLabel = eraLabel;
        Summary = summary;
        Body = body;
        Illustration = illustration;
        RequiredEra = requiredEra;
        RequiredTutorialStepId = requiredTutorialStepId ?? string.Empty;
        RequiredResearchIds = requiredResearchIds ?? Array.Empty<string>();
        RequiredBuildingIds = requiredBuildingIds ?? Array.Empty<string>();
        RequiredWorkshopIds = requiredWorkshopIds ?? Array.Empty<string>();
        RequiredUnlockedSectorIds = requiredUnlockedSectorIds ?? Array.Empty<string>();
        RequiredOccupiedSectorIds = requiredOccupiedSectorIds ?? Array.Empty<string>();
    }
}

public static class StoryManager
{
    private const string StoryResourcePath = "Datas/Story/StoryArchive";
    private static readonly string[] ExpectedChapterIds =
    {
        "PrologueAshes_00", "FirstFire_01", "WallsAndShelter_02",
        "TheGrowingClan_03", "RememberedKnowledge_04", "TheFirstChain_05",
        "StoneAgeReturn_06", "MedievalOrder_07", "IndustrialAwakening_08",
        "WorkshopMemory_09", "IndustrialPower_10", "IndustrialMaterials_11",
        "IndustrialChemistry_12", "IndustrialFrontier_13", "FrontierSectors_14",
        "WarBetweenStars_15", "BeyondTheSky_16", "TheOldBoundary_17"
    };
    private static readonly IReadOnlyList<StoryChapter> chapters = LoadChapters();
    private static WorkshopManager cachedWorkshopManager;
    private static GameManager cachedGameManager;

    public static IReadOnlyList<StoryChapter> Chapters => chapters;

    public static int ProgressVersion => GetStoryProgress()?.Version ?? 0;

    private static string First(IReadOnlyList<string> values) =>
        values != null && values.Count > 0 ? values[0] : string.Empty;

    private static IReadOnlyList<StoryChapter> LoadChapters()
    {
        StoryArchiveDefinition archive = Resources.Load<StoryArchiveDefinition>(
            StoryResourcePath);
        if (archive == null || archive.Chapters == null || archive.Chapters.Count == 0)
            throw new InvalidOperationException(
                "剧情档案缺失或为空：Resources/" + StoryResourcePath);

        var loaded = new List<StoryChapter>(archive.Chapters.Count);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < archive.Chapters.Count; i++)
        {
            StoryChapterDefinition data = archive.Chapters[i];
            if (data == null)
                continue;
            AddChapter(data.ToRuntime(), ids, loaded);
        }
        if (loaded.Count == 0)
            throw new InvalidOperationException("剧情档案不包含有效章节。");
        if (loaded.Count != ExpectedChapterIds.Length)
            throw new InvalidOperationException("剧情档案章节数量必须保持 18 章。");
        for (int i = 0; i < ExpectedChapterIds.Length; i++)
            if (!string.Equals(loaded[i].Id, ExpectedChapterIds[i],
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("剧情档案顺序或章节 ID 不符合固定主线：" +
                    loaded[i].Id);
        return loaded.AsReadOnly();
    }

    private static void AddChapter(StoryChapter chapter,
        HashSet<string> ids, List<StoryChapter> loaded)
    {
        if (chapter == null || string.IsNullOrWhiteSpace(chapter.Id) ||
            !ids.Add(chapter.Id.Trim()))
            throw new InvalidOperationException("剧情档案存在空编号或重复编号：" +
                (chapter == null ? "<null>" : chapter.Id));
        ValidateRuntimeReferences(chapter);
        loaded.Add(chapter);
    }

    private static void ValidateRuntimeReferences(StoryChapter chapter)
    {
        int bodyLength = string.IsNullOrWhiteSpace(chapter.Body)
            ? 0 : chapter.Body.Trim().Length;
        if (bodyLength < 200 || bodyLength > 300)
            throw new InvalidOperationException("剧情章节正文长度必须为 200–300 字：" +
                chapter.Id + " -> " + bodyLength);
        string[] forbidden = { "玩家", "页面", "菜单", "按钮", "ID", "数值", "数字", "奖励", "界面" };
        for (int i = 0; i < forbidden.Length; i++)
            if ((chapter.Summary ?? string.Empty).Contains(forbidden[i]) ||
                (chapter.Body ?? string.Empty).Contains(forbidden[i]))
                throw new InvalidOperationException("剧情章节包含禁用元叙事词：" +
                    chapter.Id + " -> " + forbidden[i]);
        ValidateRuntimeList(chapter.RequiredResearchIds, chapter.RequiredEra,
            (id, era) => DataBase<Research>.TryFind(id, out Research definition)
                ? definition.TechLevel <= era : false,
            "研究", chapter.Id);
        ValidateRuntimeList(chapter.RequiredBuildingIds, chapter.RequiredEra,
            (id, era) => DataBase<Building>.TryFind(id, out Building definition)
                ? definition.TechLevel <= era : false,
            "建筑", chapter.Id);
        ValidateRuntimeList(chapter.RequiredWorkshopIds, chapter.RequiredEra,
            (id, era) => DataBase<WorkshopUpgrade>.TryFind(id, out WorkshopUpgrade definition)
                ? definition.TechLevel <= era : false,
            "工坊", chapter.Id);
        ValidateRuntimeList(chapter.RequiredUnlockedSectorIds, chapter.RequiredEra,
            (id, era) => DataBase<SectorDefinition>.TryFind(id, out SectorDefinition definition)
                && definition != null,
            "解锁星区", chapter.Id);
        ValidateRuntimeList(chapter.RequiredOccupiedSectorIds, chapter.RequiredEra,
            (id, era) => DataBase<SectorDefinition>.TryFind(id, out SectorDefinition definition)
                && definition != null,
            "占领星区", chapter.Id);
    }

    private static void ValidateRuntimeList(IReadOnlyList<string> ids,
        TechLevel era, Func<string, TechLevel, bool> predicate, string label,
        string chapterId)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; ids != null && i < ids.Count; i++)
        {
            string id = ids[i];
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id.Trim()) ||
                !predicate(id.Trim(), era))
                throw new InvalidOperationException("剧情章节" + label +
                    "条件无效或时代不兼容：" + chapterId + " -> " + id);
        }
    }

    public static int RefreshProgress(TechLevel currentEra,
        TutorialManager tutorial)
    {
        GameState state = GetGameState();
        if (state == null)
            return 0;

        StoryProgressState progress = state.EnsureStoryProgress();
        int newlyCompleted = 0;
        for (int i = 0; i < chapters.Count; i++)
        {
            StoryChapter chapter = chapters[i];
            if (chapter == null)
                continue;
            if (progress.Contains(chapter.Id))
                continue;
            if (i > 0 && !progress.Contains(chapters[i - 1].Id))
                break;
            if (!AreConditionsMet(chapter, currentEra, tutorial))
                break;
            if (progress.TryComplete(chapter.Id))
            {
                state.MarkStoryProgressChanged();
                newlyCompleted++;
            }
        }
        return newlyCompleted;
    }

    public static int RefreshProgress()
    {
        GameState state = GetGameState();
        return state == null ? 0 :
            RefreshProgress(state.TechLevel, TutorialManager.Current);
    }

    public static bool IsCompleted(StoryChapter chapter)
    {
        StoryProgressState progress = GetStoryProgress();
        return chapter != null && progress != null && progress.Contains(chapter.Id);
    }

    public static SaveManager.StorySaveData CaptureSaveData()
    {
        GameState state = GetGameState();
        if (state == null)
            throw new InvalidOperationException("保存剧情前必须存在活动 GameState。");
        RefreshProgress(state.TechLevel, TutorialManager.Current);
        StoryProgressState progress = state.EnsureStoryProgress();
        var completed = new List<string>();
        for (int i = 0; i < chapters.Count; i++)
        {
            StoryChapter chapter = chapters[i];
            if (chapter == null || !progress.Contains(chapter.Id))
                break;
            completed.Add(chapter.Id);
        }
        return new SaveManager.StorySaveData
        {
            CompletedChapterIds = completed
        };
    }

    public static void RestoreSaveData(SaveManager.StorySaveData data)
    {
        if (data == null || data.CompletedChapterIds == null)
            throw new System.IO.InvalidDataException("剧情存档段缺失或为空。");
        GameState state = GetGameState();
        if (state == null)
            throw new InvalidOperationException("恢复剧情前必须存在活动 GameState。");
        ValidateCompletedChapterIds(data.CompletedChapterIds, state.TechLevel);
        state.RestoreStoryProgress(data.CompletedChapterIds);
        RefreshProgress(state.TechLevel, TutorialManager.Current);
    }

    public static void ResetForNewGame()
    {
        GameState state = GetGameState();
        if (state != null)
            state.ResetStoryProgress();
    }

    internal static void ValidateCompletedChapterIds(
        IReadOnlyList<string> completedChapterIds, TechLevel currentEra)
    {
        if (completedChapterIds == null)
            throw new System.IO.InvalidDataException("剧情完成列表缺失。");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < completedChapterIds.Count; i++)
        {
            string id = completedChapterIds[i];
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id.Trim()))
                throw new System.IO.InvalidDataException("剧情完成列表包含空值或重复章节。");
            if (i >= chapters.Count || !string.Equals(
                chapters[i].Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new System.IO.InvalidDataException(
                    "剧情完成列表必须按 Archive 顺序形成连续前缀。");
            if (chapters[i].RequiredEra > currentEra)
                throw new System.IO.InvalidDataException(
                    "剧情完成记录所需时代高于当前存档时代：" + id);
        }
    }

    private static GameState GetGameState()
    {
        try
        {
            GameManager manager = GameManager.Instance;
            return manager == null ? null : manager.State;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static StoryProgressState GetStoryProgress()
    {
        GameState state = GetGameState();
        return state == null ? null : state.StoryProgress;
    }

    private static bool AreConditionsMet(StoryChapter chapter,
        TechLevel currentEra, TutorialManager tutorial)
    {
        if (chapter == null || currentEra < chapter.RequiredEra ||
            !AllResearchCompleted(chapter.RequiredResearchIds) ||
            !AllBuildingsOwned(chapter.RequiredBuildingIds) ||
            !AllWorkshopsPurchased(chapter.RequiredWorkshopIds) ||
            !AllSectorsInState(chapter.RequiredUnlockedSectorIds, false) ||
            !AllSectorsInState(chapter.RequiredOccupiedSectorIds, true))
            return false;
        return string.IsNullOrEmpty(chapter.RequiredTutorialStepId) ||
            HasCompletedTutorialStep(chapter.RequiredTutorialStepId, tutorial);
    }

    public static string GetProgressSignature(TechLevel currentEra,
        TutorialManager tutorial)
    {
        RefreshProgress(currentEra, tutorial);
        StringBuilder signature = new StringBuilder();
        signature.Append((int)currentEra).Append(':')
            .Append(tutorial == null ? -1 : tutorial.Version)
            .Append("|p=").Append(ProgressVersion);
        StoryProgressState progress = GetStoryProgress();
        for (int i = 0; i < chapters.Count; i++)
            if (chapters[i] != null)
                signature.Append('|').Append(chapters[i].Id).Append('=')
                    .Append(progress != null && progress.Contains(chapters[i].Id) ? '1' : '0');
        if (tutorial != null)
        {
            var tutorialIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < chapters.Count; i++)
            {
                StoryChapter chapter = chapters[i];
                if (chapter == null || string.IsNullOrEmpty(chapter.RequiredTutorialStepId) ||
                    !tutorialIds.Add(chapter.RequiredTutorialStepId))
                    continue;
                signature.Append("|t:").Append(chapter.RequiredTutorialStepId)
                    .Append('=').Append(HasCompletedTutorialStep(
                        chapter.RequiredTutorialStepId, tutorial) ? '1' : '0');
            }
        }
        for (int i = 0; i < chapters.Count; i++)
        {
            StoryChapter chapter = chapters[i];
            if (chapter == null)
                continue;
            AppendResearchSignature(signature, chapter.RequiredResearchIds);
            AppendBuildingSignature(signature, chapter.RequiredBuildingIds);
            AppendWorkshopSignature(signature, chapter.RequiredWorkshopIds);
            AppendSectorSignature(signature, chapter.RequiredUnlockedSectorIds, false);
            AppendSectorSignature(signature, chapter.RequiredOccupiedSectorIds, true);
        }
        if (RequiresSectorProgressRefresh())
            signature.Append("|s=").Append(GetSectorProgressSignature());
        return signature.ToString();
    }

    public static StoryChapter FindLatestUnlocked(TechLevel currentEra,
        TutorialManager tutorial)
    {
        RefreshProgress(currentEra, tutorial);
        StoryChapter latest = null;
        for (int i = 0; i < chapters.Count; i++)
            if (IsCompleted(chapters[i]))
                latest = chapters[i];
        return latest;
    }

    public static int CountUnlocked(TechLevel currentEra, TutorialManager tutorial)
    {
        RefreshProgress(currentEra, tutorial);
        int count = 0;
        for (int i = 0; i < chapters.Count; i++)
            if (IsCompleted(chapters[i]))
                count++;
        return count;
    }

    public static bool IsUnlocked(StoryChapter chapter, TechLevel currentEra,
        TutorialManager tutorial)
    {
        RefreshProgress(currentEra, tutorial);
        return IsCompleted(chapter);
    }

    private static int IndexOf(StoryChapter chapter)
    {
        for (int i = 0; i < chapters.Count; i++)
            if (chapters[i] == chapter)
                return i;
        return -1;
    }

    private static bool AllResearchCompleted(IReadOnlyList<string> ids)
    {
        for (int i = 0; ids != null && i < ids.Count; i++)
            if (!HasCompletedResearch(ids[i]))
                return false;
        return true;
    }

    private static bool AllBuildingsOwned(IReadOnlyList<string> ids)
    {
        for (int i = 0; ids != null && i < ids.Count; i++)
            if (!HasOwnedBuilding(ids[i]))
                return false;
        return true;
    }

    private static bool AllWorkshopsPurchased(IReadOnlyList<string> ids)
    {
        for (int i = 0; ids != null && i < ids.Count; i++)
            if (!HasWorkshopPurchase(ids[i]))
                return false;
        return true;
    }

    private static bool AllSectorsInState(IReadOnlyList<string> ids, bool occupied)
    {
        for (int i = 0; ids != null && i < ids.Count; i++)
        {
            SectorManager manager = GetSectorManager();
            if (manager == null || !DataBase<SectorDefinition>.TryFind(ids[i],
                out SectorDefinition definition) || definition == null)
                return false;
            SectorState state = manager.GetState(definition);
            if (state == null || (occupied ? !state.Occupied : !state.Unlocked))
                return false;
        }
        return true;
    }

    private static void AppendResearchSignature(StringBuilder signature,
        IReadOnlyList<string> ids)
    {
        for (int i = 0; ids != null && i < ids.Count; i++)
            signature.Append("|r:").Append(ids[i]).Append('=').Append(
                GetResearchStatusCode(ids[i]));
    }

    private static void AppendBuildingSignature(StringBuilder signature,
        IReadOnlyList<string> ids)
    {
        for (int i = 0; ids != null && i < ids.Count; i++)
            signature.Append("|b:").Append(ids[i]).Append('=').Append(
                HasOwnedBuilding(ids[i]) ? '1' : '0');
    }

    private static void AppendWorkshopSignature(StringBuilder signature,
        IReadOnlyList<string> ids)
    {
        for (int i = 0; ids != null && i < ids.Count; i++)
            signature.Append("|w:").Append(ids[i]).Append('=').Append(
                HasWorkshopPurchase(ids[i]) ? '1' : '0');
    }

    private static void AppendSectorSignature(StringBuilder signature,
        IReadOnlyList<string> ids, bool occupied)
    {
        for (int i = 0; ids != null && i < ids.Count; i++)
            signature.Append(occupied ? "|so:" : "|su:").Append(ids[i]).Append('=').Append(
                AllSectorsInState(new[] { ids[i] }, occupied) ? '1' : '0');
    }

    private static bool HasCompletedTutorialStep(string stepId,
        TutorialManager tutorial = null)
    {
        if (string.IsNullOrEmpty(stepId))
            return true;
        tutorial = tutorial ?? TutorialManager.Current;
        if (tutorial == null)
            return false;
        foreach (string completedId in tutorial.CompletedStepIds)
            if (completedId == stepId)
                return true;
        return false;
    }

    private static bool HasCompletedResearch(string researchId)
    {
        if (string.IsNullOrEmpty(researchId))
            return true;
        ResearchManager manager;
        try { manager = ResearchManager.Instance; }
        catch (InvalidOperationException) { return false; }
        if (!DataBase<Research>.TryFind(researchId, out Research definition))
            return false;
        return manager != null && definition != null &&
            manager.States.TryGetValue(definition, out ResearchState state) &&
            state != null && state.Status == ResearchStatus.Completed;
    }

    private static int GetResearchStatusCode(string researchId)
    {
        if (string.IsNullOrEmpty(researchId) ||
            !DataBase<Research>.TryFind(researchId, out Research definition) ||
            definition == null)
            return -1;
        ResearchManager manager;
        try { manager = ResearchManager.Instance; }
        catch (InvalidOperationException) { return -1; }
        if (manager == null || !manager.States.TryGetValue(definition,
            out ResearchState state) || state == null)
            return -1;
        return (int)state.Status;
    }

    private static bool HasOwnedBuilding(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId))
            return true;
        BuildingManager manager;
        try { manager = BuildingManager.Instance; }
        catch (InvalidOperationException) { return false; }
        if (!DataBase<Building>.TryFind(buildingId, out Building definition) ||
            manager == null || definition == null ||
            !manager.States.TryGetValue(definition, out BuildingState state) ||
            state == null || state.Amount <= ExpantaNum.Zero)
            return false;
        try
        {
            if (GameManager.Instance == null || GameManager.Instance.State == null ||
                ResearchManager.Instance == null)
                return false;
        }
        catch (InvalidOperationException) { return false; }
        return manager.ArePrerequisitesMet(definition, out _);
    }

    private static bool HasWorkshopPurchase(string requiredWorkshopId = "")
    {
        WorkshopManager manager = cachedWorkshopManager != null
            ? cachedWorkshopManager
            : cachedWorkshopManager = UnityEngine.Object.FindObjectOfType<WorkshopManager>();
        if (manager == null)
            return false;
        foreach (WorkshopUpgradeState state in manager.States.Values)
            if (state != null && state.Purchased && state.Definition != null &&
                (string.IsNullOrEmpty(requiredWorkshopId) ||
                 state.Definition.Id == requiredWorkshopId))
                return true;
        return false;
    }

    private static SectorManager GetSectorManager()
    {
        GameManager gameManager = cachedGameManager != null
            ? cachedGameManager
            : cachedGameManager = UnityEngine.Object.FindObjectOfType<GameManager>();
        return gameManager == null ? null : gameManager.Sectors;
    }

    private static bool RequiresSectorProgressRefresh()
    {
        for (int i = 0; i < chapters.Count; i++)
            if (chapters[i] != null &&
                (chapters[i].RequiredUnlockedSectorIds.Count > 0 ||
                 chapters[i].RequiredOccupiedSectorIds.Count > 0))
                return true;
        return false;
    }

    private static string GetSectorProgressSignature()
    {
        SectorManager manager = GetSectorManager();
        if (manager == null)
            return "none";
        StringBuilder signature = new StringBuilder();
        foreach (SectorState state in manager.OrderedStates)
        {
            if (state == null)
                continue;
            signature.Append(state.Unlocked ? '1' : '0')
                .Append(state.Occupied ? '1' : '0')
                .Append(state.CampaignActive ? '1' : '0')
                .Append(state.CampaignProgress > ExpantaNum.Zero ? '1' : '0')
                .Append(';');
        }
        return signature.ToString();
    }

    public static string GetUnlockHint(StoryChapter chapter, TechLevel currentEra)
    {
        if (chapter == null)
            return string.Empty;
        if (currentEra < chapter.RequiredEra)
            return "进入“" + chapter.RequiredEra.GetDescription() +
                "”后，这段记忆才会进入王国的现实。";
        StoryChapter previous = GetPreviousLockedChapter(
            chapter, currentEra, TutorialManager.Current);
        if (previous != null)
            return "先唤醒上一段" +
                (previous.RequiredEra == TechLevel.Industrial ? "工业" : "文明") +
                "记忆“" + previous.Title + "”：" +
                GetUnlockHint(previous, currentEra);
        string requiredResearchId = First(chapter.RequiredResearchIds);
        if (!string.IsNullOrEmpty(requiredResearchId) &&
            !HasCompletedResearch(requiredResearchId))
            return "完成研究“" + GetResearchLabel(requiredResearchId) +
                "”，解锁这段工业能力。";
        string requiredBuildingId = First(chapter.RequiredBuildingIds);
        if (!string.IsNullOrEmpty(requiredBuildingId) &&
            !HasOwnedBuilding(requiredBuildingId))
        {
            string workshopHint = GetBuildingWorkshopHint(requiredBuildingId);
            return !string.IsNullOrEmpty(workshopHint) ? workshopHint :
                "建成“" + GetBuildingLabel(requiredBuildingId) +
                "”，让这段工业记忆从计划变成现实。";
        }
        return GetUnlockHint(chapter);
    }

    public static string GetChapterProgressHint(StoryChapter chapter,
        TechLevel currentEra)
    {
        string baseline = GetUnlockHint(chapter, currentEra);
        if (chapter == null || currentEra < chapter.RequiredEra)
            return baseline;
        StoryChapter previous = GetPreviousLockedChapter(
            chapter, currentEra, TutorialManager.Current);
        if (previous != null)
            return baseline;
        string requiredResearchId = First(chapter.RequiredResearchIds);
        if (!string.IsNullOrEmpty(requiredResearchId) &&
            !HasCompletedResearch(requiredResearchId))
            return GetResearchProgressHint(requiredResearchId);
        string requiredBuildingId = First(chapter.RequiredBuildingIds);
        if (!string.IsNullOrEmpty(requiredBuildingId) &&
            !HasOwnedBuilding(requiredBuildingId))
        {
            string workshopHint = GetBuildingWorkshopHint(requiredBuildingId);
            return !string.IsNullOrEmpty(workshopHint) ? workshopHint :
                "研究已完成，下一步建造对应建筑，让这段工业记忆成为现实。";
        }
        if (chapter.RequiredWorkshopIds.Count > 0 &&
            !AllWorkshopsPurchased(chapter.RequiredWorkshopIds))
            return "研究已完成，下一步购买一项真实工坊改造，让旧有生产体系继续成长。";
        return baseline;
    }

    private static StoryChapter GetPreviousLockedChapter(
        StoryChapter chapter, TechLevel currentEra, TutorialManager tutorial)
    {
        if (chapter == null || currentEra < chapter.RequiredEra)
            return null;
        int index = IndexOf(chapter);
        if (index <= 0)
            return null;
        StoryChapter previous = chapters[index - 1];
        return previous != null && !IsCompleted(previous)
            ? previous : null;
    }

    private static string GetResearchProgressHint(string researchId)
    {
        if (!DataBase<Research>.TryFind(researchId, out Research definition) ||
            definition == null)
            return "完成研究“" + researchId + "”，继续追踪这段工业记忆。";
        ResearchManager manager = ResearchManager.Instance;
        if (manager == null || !manager.States.TryGetValue(definition,
            out ResearchState state) || state == null)
            return "完成研究“" + definition.Label + "”，继续追踪这段工业记忆。";
        switch (state.Status)
        {
            case ResearchStatus.Researching:
                return "研究进行中：“" + definition.Label + "”；等待知识积累完成。";
            case ResearchStatus.Queued:
                return "研究已排队：“" + definition.Label + "”；前面的知识完成后即可推进。";
            case ResearchStatus.WaitingResources:
                return "研究等待资源：“" + definition.Label + "”；先补齐研究所需资源。";
            case ResearchStatus.Available:
                return "研究可开始：“" + definition.Label + "”；用它建立下一段工业能力。";
            default:
                return "完成研究“" + definition.Label + "”，继续追踪这段工业记忆。";
        }
    }

    public static string GetUnlockHint(StoryChapter chapter)
    {
        if (chapter == null)
            return string.Empty;
        if (chapter.RequiredWorkshopIds.Count > 0)
            return "完成一项真实工坊改造后，这段记忆才会被正式唤醒。";
        if (chapter.RequiredUnlockedSectorIds.Count > 0)
            return "解锁一片真实星区后，这段边疆记忆才会被正式唤醒。";
        if (chapter.RequiredOccupiedSectorIds.Count > 0)
            return "完成一次真实星区占领后，这段记忆才会被正式唤醒。";
        string requiredResearchId = First(chapter.RequiredResearchIds);
        if (!string.IsNullOrEmpty(requiredResearchId))
            return "完成研究“" + GetResearchLabel(requiredResearchId) +
                "”，再用它建立对应的工业能力。";
        string requiredBuildingId = First(chapter.RequiredBuildingIds);
        if (!string.IsNullOrEmpty(requiredBuildingId))
            return "建成“" + GetBuildingLabel(requiredBuildingId) +
                "”，让这段工业记忆从计划变成现实。";
        if (!string.IsNullOrEmpty(chapter.RequiredTutorialStepId) &&
            chapter.RequiredEra == TechLevel.Animal)
            return "完成当前发展行动后，这段记忆会被重新读出。";
        return "进入“" + chapter.RequiredEra.GetDescription() + "”后解锁。";
    }

    private static string GetResearchLabel(string researchId)
    {
        return DataBase<Research>.TryFind(researchId, out Research research) &&
            research != null && !string.IsNullOrEmpty(research.Label)
            ? research.Label : researchId;
    }

    private static string GetBuildingLabel(string buildingId)
    {
        return DataBase<Building>.TryFind(buildingId, out Building building) &&
            building != null && !string.IsNullOrEmpty(building.Label)
            ? building.Label : buildingId;
    }

    private static string GetBuildingWorkshopHint(string buildingId)
    {
        if (!DataBase<Building>.TryFind(buildingId, out Building building) ||
            building == null)
            return string.Empty;
        if (building.RequiredResearch != null)
            for (int i = 0; i < building.RequiredResearch.Count; i++)
            {
                Research prerequisite = building.RequiredResearch[i];
                if (prerequisite != null && !HasCompletedResearch(prerequisite.Id))
                    return "建造“" + building.Label + "”前还需要研究“" +
                        prerequisite.Label + "”。";
            }
        if (building.RequiredWorkshopUpgrades == null)
            return string.Empty;
        WorkshopManager manager = cachedWorkshopManager != null
            ? cachedWorkshopManager
            : cachedWorkshopManager = UnityEngine.Object.FindObjectOfType<WorkshopManager>();
        for (int i = 0; i < building.RequiredWorkshopUpgrades.Count; i++)
        {
            WorkshopUpgrade upgrade = building.RequiredWorkshopUpgrades[i];
            if (upgrade == null || (manager != null && manager.IsPurchased(upgrade)))
                continue;
            if (upgrade.RequiredResearch != null)
                for (int r = 0; r < upgrade.RequiredResearch.Count; r++)
                {
                    Research prerequisite = upgrade.RequiredResearch[r];
                    if (prerequisite != null && !HasCompletedResearch(prerequisite.Id))
                        return "建造“" + building.Label + "”前还需要研究“" +
                            prerequisite.Label + "”，再购买 Workshop 改良“" +
                            upgrade.Label + "”。";
                }
            return "研究已完成，但建造“" + building.Label +
                "”前还需要购买 Workshop 改良“" + upgrade.Label + "”。";
        }
        return string.Empty;
    }
}
