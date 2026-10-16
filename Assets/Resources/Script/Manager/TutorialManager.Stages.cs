using System;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialStageAction { Navigate, Select, Build, StartResearch, Inspect }

public sealed class TutorialStageSnapshot
{
    public string StageId { get; internal set; }
    public string Page { get; internal set; }
    public string TargetId { get; internal set; }
    public string Title { get; internal set; }
    public string Instruction { get; internal set; }
    public TutorialStageAction Action { get; internal set; }
    public bool CanInteract { get; internal set; }
}

public sealed partial class TutorialManager
{
    private static readonly string[] StageIds =
        { "resources", "building", "population", "research", "production-chain", "era-goal", "workshop", "sectors" };
    private readonly HashSet<string> completedStageIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> skippedStageIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> baselineStageIds = new HashSet<string>(StringComparer.Ordinal);
    private bool stagesInitialized = true;
    private string activeStageId = string.Empty;
    private string stageTargetId = string.Empty;
    private int stagePhase;
    private ExpantaNum stageBuildingAmount = ExpantaNum.Zero;
    private bool restoreBuildingBaseline;
    private List<Pair<Building, Building>> productionStageLinks;

    public IReadOnlyCollection<string> CompletedStageIds => completedStageIds;
    public IReadOnlyCollection<string> SkippedStageIds => skippedStageIds;

    public TutorialStageSnapshot EvaluateStage()
    {
        GameManager game = cachedGameManager != null ? cachedGameManager : cachedGameManager = FindObjectOfType<GameManager>();
        ResourceManager resources = cachedResourceManager != null ? cachedResourceManager : cachedResourceManager = FindObjectOfType<ResourceManager>();
        BuildingManager buildings = cachedBuildingManager != null ? cachedBuildingManager : cachedBuildingManager = FindObjectOfType<BuildingManager>();
        ResearchManager research = cachedResearchManager != null ? cachedResearchManager : cachedResearchManager = FindObjectOfType<ResearchManager>();
        if (game == null || game.State == null || resources == null || buildings == null || research == null)
            return null;
        if (!stagesInitialized)
            EstablishStageBaseline(game, resources, buildings, research);
        if (string.IsNullOrEmpty(activeStageId))
            for (int i = 0; i < StageIds.Length; i++)
            {
                string id = StageIds[i];
                if (completedStageIds.Contains(id) || skippedStageIds.Contains(id) || baselineStageIds.Contains(id) ||
                    !TryGetStage(id, game, resources, buildings, research, out TutorialStageSnapshot candidate))
                    continue;
                activeStageId = id;
                stageTargetId = candidate.TargetId;
                stagePhase = 0;
                version++;
                break;
            }
        if (string.IsNullOrEmpty(activeStageId)) return null;
        bool available = TryGetStage(activeStageId, game, resources, buildings, research, out TutorialStageSnapshot snapshot);
        if (!available && activeStageId != "building" && activeStageId != "research") return null;
        bool replaceTarget = activeStageId == "building" &&
            (!DataBase<Building>.TryFind(stageTargetId, out Building currentBuilding) || !buildings.CanConstructNew(currentBuilding));
        if (activeStageId == "production-chain" && !HasEligibleProductionTarget(stageTargetId, buildings))
            replaceTarget = true;
        if (replaceTarget)
        {
            if (!available) return null;
            stageTargetId = snapshot.TargetId;
            stagePhase = 0;
            stageBuildingAmount = ExpantaNum.Zero;
            restoreBuildingBaseline = false;
            version++;
        }
        snapshot.TargetId = stageTargetId;
        snapshot.Action = stagePhase == 0 ? TutorialStageAction.Navigate :
            stagePhase == 1 && !string.IsNullOrEmpty(stageTargetId) ? TutorialStageAction.Select : TutorialStageAction.Inspect;
        if (stagePhase >= 2 && activeStageId == "building")
        {
            if (!DataBase<Building>.TryFind(stageTargetId, out Building building)) return null;
            BuildingState state = buildings.GetState(building);
            if (state != null && state.Amount > stageBuildingAmount)
            {
                CompleteCurrentStage();
                return EvaluateStage();
            }
            snapshot.Action = TutorialStageAction.Build;
            snapshot.CanInteract = buildings.GetBuildFailure(building, ExpantaNum.One) == BuildFailure.None;
        }
        if (stagePhase >= 2 && activeStageId == "research")
        {
            if (!DataBase<Research>.TryFind(stageTargetId, out Research definition)) return null;
            ResearchState state = research.GetState(definition);
            if (state != null && (state.Status == ResearchStatus.Researching || state.Status == ResearchStatus.Completed))
            {
                CompleteCurrentStage();
                return EvaluateStage();
            }
            if (research.IsQueued(definition))
            {
                snapshot.Action = TutorialStageAction.Inspect;
                snapshot.Instruction = "该研究已加入队列；资源全额支付后才开始推进，可在研究页查看当前缺口。";
                return snapshot;
            }
            snapshot.Action = TutorialStageAction.StartResearch;
            snapshot.CanInteract = research.CanAccessResearch(definition) && research.ArePrerequisitesCompleted(definition) &&
                research.CanPayResearchCost(definition, out _);
        }
        if (snapshot.Action == TutorialStageAction.Navigate)
            snapshot.Instruction = "点击“" + StagePageLabel(snapshot.Page) + "”进入页面。";
        else if (snapshot.Action == TutorialStageAction.Select)
            snapshot.Instruction = "点击高亮条目，查看详情与条件。";
        else if (snapshot.Action == TutorialStageAction.Build)
            snapshot.Instruction = "点击建造，完成一次建筑建设。";
        else if (snapshot.Action == TutorialStageAction.StartResearch)
            snapshot.Instruction = "查看解锁效果与费用，点击研究操作开始研究。";
        return snapshot;
    }

    public void RecordStagePageVisited(string page)
    {
        TutorialStageSnapshot snapshot = EvaluateStage();
        if (snapshot == null || snapshot.Action != TutorialStageAction.Navigate || snapshot.Page != page) return;
        stagePhase = string.IsNullOrEmpty(stageTargetId) ? 2 : 1;
        version++;
    }

    public void RecordStageTargetViewed(string page, string targetId)
    {
        TutorialStageSnapshot snapshot = EvaluateStage();
        if (snapshot == null || snapshot.Action != TutorialStageAction.Select || snapshot.Page != page || snapshot.TargetId != targetId) return;
        stagePhase = 2;
        version++;
        if (activeStageId == "building" && DataBase<Building>.TryFind(targetId, out Building building))
        {
            if (!restoreBuildingBaseline)
                stageBuildingAmount = cachedBuildingManager.GetState(building)?.Amount ?? ExpantaNum.Zero;
            restoreBuildingBaseline = false;
        }
    }

    public void AcknowledgeCurrentStage()
    {
        TutorialStageSnapshot snapshot = EvaluateStage();
        if (snapshot != null && snapshot.Action == TutorialStageAction.Inspect && snapshot.CanInteract)
            CompleteCurrentStage();
    }

    public void SkipCurrentStage()
    {
        if (string.IsNullOrEmpty(activeStageId)) return;
        skippedStageIds.Add(activeStageId);
        ClearActiveStage();
    }

    public void ReopenCurrentStage()
    {
        if (string.IsNullOrEmpty(activeStageId)) return;
        restoreBuildingBaseline = activeStageId == "building" && stagePhase >= 2;
        stagePhase = 0;
        version++;
    }

    private void CompleteCurrentStage()
    {
        completedStageIds.Add(activeStageId);
        ClearActiveStage();
    }

    private void ClearActiveStage()
    {
        activeStageId = string.Empty;
        stageTargetId = string.Empty;
        stagePhase = 0;
        stageBuildingAmount = ExpantaNum.Zero;
        restoreBuildingBaseline = false;
        version++;
    }

    private static string StagePageLabel(string page)
    {
        switch (page)
        {
            case "Resources": return "资源";
            case "Buildings": return "建筑";
            case "Research": return "研究";
            case "Production": return "生产树";
            case "Era": return "时代";
            case "Workshop": return "工坊";
            case "Sectors": return "星区";
            default: return "概览";
        }
    }

    private bool TryGetStage(string id, GameManager game, ResourceManager resources,
        BuildingManager buildings, ResearchManager research, out TutorialStageSnapshot snapshot, bool baseline = false)
    {
        snapshot = new TutorialStageSnapshot { StageId = id, TargetId = string.Empty, CanInteract = true };
        switch (id)
        {
            case "resources":
                snapshot.Title = "认识资源"; snapshot.Page = "Resources";
                snapshot.TargetId = ResourceManager.StartingResourceId;
                snapshot.Instruction = "库存显示现有数量，净产出帮助判断资源来源与消耗。";
                return resources.States.Count > 0;
            case "building":
                snapshot.Title = "建造第一座建筑"; snapshot.Page = "Buildings";
                foreach (Building definition in DataBase<Building>.All)
                    if (definition != null && !(definition is SectorBuilding) && buildings.CanConstructNew(definition) &&
                        (baseline || buildings.GetBuildFailure(definition, ExpantaNum.One) == BuildFailure.None))
                    { snapshot.TargetId = definition.Id; return true; }
                return false;
            case "population":
                snapshot.Title = "发展人口"; snapshot.Page = "Overview";
                snapshot.Instruction = "人口容量提供容身之处；维持食物与幸福度，人口才能持续增长并提供生产力。";
                return game.State.Population != null && game.State.Population.PopulationCapacity > ExpantaNum.Zero;
            case "research":
                snapshot.Title = "研究新知识"; snapshot.Page = "Research";
                foreach (Research definition in DataBase<Research>.All)
                    if (definition != null && research.CanAccessResearch(definition) && research.ArePrerequisitesCompleted(definition) &&
                        (baseline || research.GetState(definition)?.Status != ResearchStatus.Completed))
                    { snapshot.TargetId = definition.Id; return true; }
                return false;
            case "production-chain":
                snapshot.Title = "连接生产链"; snapshot.Page = "Production";
                snapshot.Instruction = "查看上下游：原料来源需要支撑加工消耗，避免缺料使建筑停产。";
                EnsureProductionStageLinks();
                for (int i = 0; i < productionStageLinks.Count; i++)
                {
                    Pair<Building, Building> link = productionStageLinks[i];
                    if (!buildings.ArePrerequisitesMet(link.First, out _) || !buildings.ArePrerequisitesMet(link.Second, out _)) continue;
                    foreach (Pair<Resource, ExpantaNum> input in link.Second.ResourceConsumptionRates)
                        foreach (Pair<Resource, ExpantaNum> output in link.First.ResourceGenerationRates)
                            if (input.First != null && input.First == output.First && input.Second > ExpantaNum.Zero && output.Second > ExpantaNum.Zero)
                            { snapshot.TargetId = input.First.Id; return true; }
                }
                return false;
            case "era-goal":
                snapshot.Title = "迈向下一时代"; snapshot.Page = "Era";
                snapshot.Instruction = "时代页列出关键研究与材料；条件满足后，通过时代研究推进文明。";
                EraGoalEvaluation era = EraGoalEvaluator.Evaluate(game.State.TechLevel, research, resources);
                if (baseline && game.State.TechLevel > TechLevel.Animal) return true;
                if (era.Transition == null) return false;
                foreach (EraGoalConditionEvaluation condition in era.Conditions) if (!condition.Met) return false;
                return true;
            case "workshop":
                snapshot.Title = "使用工坊"; snapshot.Page = "Workshop";
                snapshot.Instruction = "工坊升级会改善生产能力；查看配方、前置条件和费用，再决定制作。";
                WorkshopManager workshop = FindObjectOfType<WorkshopManager>();
                if (workshop == null || !workshop.IsSystemUnlocked) return false;
                foreach (WorkshopUpgrade upgrade in DataBase<WorkshopUpgrade>.All)
                    if (upgrade != null && upgrade.TechLevel <= game.State.TechLevel && workshop.ArePrerequisitesMet(upgrade))
                    { snapshot.TargetId = upgrade.Id; return true; }
                return false;
            case "sectors":
                snapshot.Title = "探索星区"; snapshot.Page = "Sectors";
                snapshot.Instruction = "查看星区的探索、占领条件及消耗，准备好后再开展探索。";
                if (!ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.HomeSystemSurvey)) return false;
                foreach (SectorState state in game.Sectors.OrderedStates)
                {
                    if (state == null || state.Definition == null) continue;
                    bool visible = true;
                    IReadOnlyList<SectorDefinition> prerequisites = state.Definition.PrerequisiteSectors;
                    for (int i = 0; prerequisites != null && i < prerequisites.Count; i++)
                        if (prerequisites[i] == null || !game.Sectors.GetState(prerequisites[i]).Occupied)
                        { visible = false; break; }
                    if (visible) { snapshot.TargetId = state.Definition.Id; return true; }
                }
                return false;
            default: return false;
        }
    }

    private void EnsureProductionStageLinks()
    {
        if (productionStageLinks != null) return;
        productionStageLinks = new List<Pair<Building, Building>>();
        foreach (Building source in DataBase<Building>.All)
        {
            if (source == null || source is SectorBuilding) continue;
            foreach (Building target in DataBase<Building>.All)
            {
                if (target == null || target == source || target is SectorBuilding) continue;
                bool connected = false;
                foreach (Pair<Resource, ExpantaNum> output in source.ResourceGenerationRates)
                    foreach (Pair<Resource, ExpantaNum> input in target.ResourceConsumptionRates)
                        if (output.First != null && output.First == input.First && output.Second > ExpantaNum.Zero && input.Second > ExpantaNum.Zero)
                            connected = true;
                if (connected) productionStageLinks.Add(new Pair<Building, Building>(source, target));
            }
        }
    }

    private void EstablishStageBaseline(GameManager game, ResourceManager resources,
        BuildingManager buildings, ResearchManager research)
    {
        for (int i = 0; i < StageIds.Length; i++)
            if (TryGetStage(StageIds[i], game, resources, buildings, research, out _, true))
                baselineStageIds.Add(StageIds[i]);
        stagesInitialized = true;
    }

    private bool HasEligibleProductionTarget(string resourceId, BuildingManager buildings)
    {
        EnsureProductionStageLinks();
        for (int i = 0; i < productionStageLinks.Count; i++)
        {
            Pair<Building, Building> link = productionStageLinks[i];
            if (!buildings.ArePrerequisitesMet(link.First, out _) || !buildings.ArePrerequisitesMet(link.Second, out _)) continue;
            foreach (Pair<Resource, ExpantaNum> input in link.Second.ResourceConsumptionRates)
                if (input.First != null && input.First.Id == resourceId && input.Second > ExpantaNum.Zero)
                    foreach (Pair<Resource, ExpantaNum> output in link.First.ResourceGenerationRates)
                        if (output.First == input.First && output.Second > ExpantaNum.Zero) return true;
        }
        return false;
    }

    private void CaptureStages(SaveManager.TutorialSaveData data)
    {
        data.StagesInitialized = stagesInitialized;
        data.CompletedStageIds = new List<string>(completedStageIds);
        data.SkippedStageIds = new List<string>(skippedStageIds);
        data.BaselineStageIds = new List<string>(baselineStageIds);
        data.ActiveStageId = activeStageId;
        data.StageTargetId = stageTargetId;
        data.StagePhase = stagePhase;
        data.StageBuildingAmount = stageBuildingAmount.ToString();
    }

    private void RestoreStages(SaveManager.TutorialSaveData data)
    {
        completedStageIds.Clear(); skippedStageIds.Clear(); baselineStageIds.Clear(); ClearActiveStage();
        stagesInitialized = data != null && data.StagesInitialized;
        if (!stagesInitialized)
        {
            // SaveManager restores every gameplay Manager before this call; baseline precedes offline progress.
            GameManager game = FindObjectOfType<GameManager>();
            ResourceManager resources = FindObjectOfType<ResourceManager>();
            BuildingManager buildings = FindObjectOfType<BuildingManager>();
            ResearchManager research = FindObjectOfType<ResearchManager>();
            if (game != null && game.State != null && resources != null && buildings != null && research != null)
                EstablishStageBaseline(game, resources, buildings, research);
            return;
        }
        RestoreStageSet(data.CompletedStageIds, completedStageIds);
        RestoreStageSet(data.SkippedStageIds, skippedStageIds);
        RestoreStageSet(data.BaselineStageIds, baselineStageIds);
        if (Array.IndexOf(StageIds, data.ActiveStageId) < 0 || completedStageIds.Contains(data.ActiveStageId) ||
            skippedStageIds.Contains(data.ActiveStageId) || baselineStageIds.Contains(data.ActiveStageId)) return;
        activeStageId = data.ActiveStageId;
        stageTargetId = data.StageTargetId ?? string.Empty;
        // Navigation is re-established after load; never retain a stale UI selection.
        stagePhase = 0;
        if (data.StagePhase >= 2 && activeStageId == "building" && !string.IsNullOrEmpty(data.StageBuildingAmount))
        {
            try { stageBuildingAmount = new ExpantaNum(data.StageBuildingAmount); restoreBuildingBaseline = stageBuildingAmount.IsFinite && stageBuildingAmount >= ExpantaNum.Zero; }
            catch (FormatException) { stageBuildingAmount = ExpantaNum.Zero; }
            catch (OverflowException) { stageBuildingAmount = ExpantaNum.Zero; }
        }
    }

    private static void RestoreStageSet(List<string> source, HashSet<string> destination)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++)
            if (Array.IndexOf(StageIds, source[i]) >= 0) destination.Add(source[i]);
    }

    private void ResetStages()
    {
        completedStageIds.Clear(); skippedStageIds.Clear(); baselineStageIds.Clear();
        stagesInitialized = true; ClearActiveStage();
    }
}
