using System;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialStepKind
{
    Orientation,
    Resources,
    Building,
    Population,
    Research,
    ProductionChain,
    EraGoal,
    LongTerm
}

[Serializable]
public sealed class TutorialStep
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string NarrativeText { get; }
    public TutorialStepKind Kind { get; }
    public string NextStepId { get; }
    public string TriggerCondition { get; }
    public string CompletionCondition { get; }
    public string RewardId { get; }
    public string NavigationPage { get; }

    public TutorialStep(string id, string title, string description,
        TutorialStepKind kind, string nextStepId)
        : this(id, title, description, kind, nextStepId, string.Empty, string.Empty, string.Empty)
    {
    }

    public TutorialStep(string id, string title, string description,
        TutorialStepKind kind, string nextStepId, string rewardId,
        string triggerCondition, string navigationPage)
        : this(id, title, description, kind, nextStepId, rewardId,
            triggerCondition, kind.ToString(), navigationPage)
    {
    }

    public TutorialStep(string id, string title, string description,
        TutorialStepKind kind, string nextStepId, string rewardId,
        string triggerCondition, string completionCondition, string navigationPage)
        : this(id, title, description, kind, nextStepId, rewardId,
            triggerCondition, completionCondition, navigationPage, string.Empty)
    {
    }

    public TutorialStep(string id, string title, string description,
        TutorialStepKind kind, string nextStepId, string rewardId,
        string triggerCondition, string completionCondition, string navigationPage,
        string narrativeText)
    {
        Id = id;
        Title = title;
        Description = description;
        NarrativeText = narrativeText ?? string.Empty;
        Kind = kind;
        NextStepId = nextStepId;
        TriggerCondition = string.IsNullOrWhiteSpace(triggerCondition)
            ? "game-state"
            : triggerCondition;
        CompletionCondition = string.IsNullOrWhiteSpace(completionCondition)
            ? kind.ToString()
            : completionCondition;
        RewardId = rewardId ?? string.Empty;
        NavigationPage = string.IsNullOrWhiteSpace(navigationPage)
            ? kind == TutorialStepKind.EraGoal || kind == TutorialStepKind.LongTerm
                ? "Era"
                : kind == TutorialStepKind.Research ? "Research" : "Overview"
            : navigationPage;
    }
}

public sealed class TutorialSnapshot
{
    public string StepId { get; internal set; } = string.Empty;
    public string CurrentEra { get; internal set; } = string.Empty;
    public string CivilizationContext { get; internal set; } = string.Empty;
    public string PopulationText { get; internal set; } = string.Empty;
    public string FoodText { get; internal set; } = string.Empty;
    public string CoreResourceText { get; internal set; } = string.Empty;
    public string CompletedGoal { get; internal set; } = string.Empty;
    public string CompletedFeedback { get; internal set; } = string.Empty;
    public string CurrentGoal { get; internal set; } = string.Empty;
    public string GoalDescription { get; internal set; } = string.Empty;
    public string NarrativeText { get; internal set; } = string.Empty;
    public string NextEraGoal { get; internal set; } = string.Empty;
    public string Blocker { get; internal set; } = string.Empty;
    public string RecommendedAction { get; internal set; } = string.Empty;
    public string NavigationPage { get; internal set; } = "Overview";
    public string NavigationTargetId { get; internal set; } = string.Empty;
    public string IndustrialCurrentStep { get; internal set; } = string.Empty;
    public string IndustrialNextStep { get; internal set; } = string.Empty;
    public string IndustrialAfterStep { get; internal set; } = string.Empty;
    public bool IsComplete { get; internal set; }
}

[DisallowMultipleComponent]
public sealed class TutorialManager : MonoBehaviour
{
    private static readonly HashSet<string> SupportedTriggerConditions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "game-state",
            "calendar-days>=1",
            "era-reached"
        };
    private static readonly HashSet<string> SupportedCompletionConditions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Orientation",
            "Resources",
            "Building",
            "Population",
            "Research",
            "ProductionChain",
            "EraGoal",
            "LongTerm",
            "calendar-days>=1",
            "resource-inventory-positive",
            "building-owned",
            "population-grown",
            "population-capacity-positive",
            "research-complete",
            "research-started",
            "production-chain-owned",
            "era-reached",
            "resource-detail-viewed",
            "long-term"
        };
    private static readonly HashSet<string> SupportedNavigationPages =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Overview",
            "Resources",
            "Buildings",
            "Research",
            "Era",
            "Workshop",
            "Music",
            "Sectors",
            "Story"
        };
    private static TutorialManager instance;
    private readonly List<TutorialStep> steps = new List<TutorialStep>();
    private readonly HashSet<string> completedStepIds = new HashSet<string>();
    private string activeStepId;
    private string visitedStepId;
    private string visitedDetailStepId;
    private string visitedDetailTargetId;
    private bool pendingCompletionFeedback;
    private int version;
    private GameManager cachedGameManager;
    private ResourceManager cachedResourceManager;
    private BuildingManager cachedBuildingManager;
    private ResearchManager cachedResearchManager;
    private WorkshopManager cachedWorkshopManager;
    private bool dependencyErrorLogged;

    public static TutorialManager Current => instance;
    public static TutorialManager Ensure()
    {
        if (instance == null)
        {
            instance = FindObjectOfType<TutorialManager>();
            if (instance == null)
            {
                GameObject host = new GameObject("TutorialManager");
                instance = host.AddComponent<TutorialManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(host);
            }
        }
        instance.EnsureDefinitions();
        return instance;
    }

    public IReadOnlyList<TutorialStep> Steps => steps;
    public IReadOnlyCollection<string> CompletedStepIds => completedStepIds;
    public string ActiveStepId => activeStepId;
    public int Version => version;
    public int SaveSessionVersion { get; private set; }
    public event Action<TutorialSnapshot> SnapshotChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildDefaultSteps();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void BuildDefaultSteps()
    {
        steps.Clear();
        visitedStepId = null;
        TutorialStepDefinition[] authoredSteps = Resources.LoadAll<TutorialStepDefinition>("Datas/Tutorial");
        if (authoredSteps != null && authoredSteps.Length > 0)
        {
            Array.Sort(authoredSteps, CompareDefinitions);
            for (int i = 0; i < authoredSteps.Length; i++)
                if (authoredSteps[i] != null && !string.IsNullOrWhiteSpace(authoredSteps[i].Id))
                    steps.Add(authoredSteps[i].ToRuntime());
            if (steps.Count > 0 && ValidateStepChain())
            {
                if (string.IsNullOrEmpty(activeStepId) || FindStep(activeStepId) == null)
                    activeStepId = FindRootStepId();
                return;
            }
            if (steps.Count > 0)
                Debug.LogWarning("[Kingdom Onboarding] Tutorial assets contain an invalid step chain; using the built-in fallback chain.");
            steps.Clear();
            activeStepId = string.Empty;
        }
        steps.Add(new TutorialStep("orientation", "建立一个不断发展的王国",
            "打开概览并让王国时间推进一天；等待期间可查看资源页，观察原木和食物如何开始变化。",
            TutorialStepKind.Orientation, "resources", string.Empty, "game-state",
            "calendar-days>=1", "Overview",
            "鼠族文明曾经中断，如今只剩下最初的火种。先让时间向前，让资源流动起来，复兴才有立足之处。"));
        steps.Add(new TutorialStep("resources", "理解资源来源",
            "打开资源页面，查看原木等核心资源的数量和净产出，确认建设前的资源来源。",
            TutorialStepKind.Resources, "building", string.Empty, "game-state",
            "resource-detail-viewed", "Resources",
            "复兴不能只靠勇气。食物维持族群，原木支撑最初的建设；先看清来源与净产出，再决定下一步把力量投向哪里。"));
        steps.Add(new TutorialStep("building", "让建筑解决问题",
            "在建筑页面建造一座可用建筑，观察它如何改变生产、研究力、人口容量或其他能力。",
            TutorialStepKind.Building, "population", string.Empty, "game-state",
            "building-owned", "Buildings",
            "第一座建筑不是终点，而是鼠族重新定居的证据。选择能解决当前阻碍的建筑，让土地、资源和生产力开始互相支持。"));
        steps.Add(new TutorialStep("population", "发展人口",
            "先提供人口容量，再维持食物和幸福度，观察人口增长并理解人口如何转化为生产力。",
            TutorialStepKind.Population, "research", string.Empty, "game-state",
            "population-grown", "Buildings",
            "分散的族群开始回到火种旁。容身之处提供人口容量，食物和幸福度决定他们能否留下；人口增长后，新的生产力也会回到王国。"));
        steps.Add(new TutorialStep("research", "用研究打开下一步",
            "在研究页面选择并完成一项可用研究，查看它连接的建筑、生产链或时代目标。",
            TutorialStepKind.Research, "production-chain", string.Empty, "game-state",
            "research-complete", "Research",
            "废墟中仍保存着失落的知识。研究不是孤立的清单：它会解锁建筑、改变生产方式，或把王国推向下一时代。"));
        steps.Add(new TutorialStep("production-chain", "连接生产链",
            "建立一组相互连接的生产与加工建筑，观察原材料如何转化为更高价值的资源。",
            TutorialStepKind.ProductionChain, "era-goal", string.Empty, "game-state",
            "production-chain-owned", "Buildings",
            "一个文明不能只会采集。鼠族必须把原料送进加工建筑，让上游产出真正支撑下游，而不是让资源停在仓库里。"));
        steps.Add(new TutorialStep("era-goal", "看懂下一时代",
            "打开时代页面，查看关键研究和真实条件的当前值、阻碍与入口，并完成它们以推进时代。",
            TutorialStepKind.EraGoal, "long-term", string.Empty, "game-state",
            "era-reached", "Era",
            "当知识、人口和生产重新连接，时代跃迁就不再只是一个名称变化。查看每个真实条件，补上最先阻断王国的那一环。"));
        steps.Add(new TutorialStep("long-term", "形成长期目标",
            "继续完成当前时代目标，让生产链、人口和研究共同支持王国的下一次扩张。",
            TutorialStepKind.LongTerm, string.Empty, string.Empty, "game-state",
            "long-term", "Era",
            "这场复兴没有终点。让旧时代的资源继续服务于新的生产链，用人口、研究和基础设施共同支撑下一次扩张。"));
        if (string.IsNullOrEmpty(activeStepId))
            activeStepId = steps[0].Id;
    }

    private static int CompareDefinitions(TutorialStepDefinition left, TutorialStepDefinition right)
    {
        if (left == null)
            return right == null ? 0 : 1;
        if (right == null)
            return -1;
        return string.CompareOrdinal(left.Id, right.Id);
    }

    private string FindRootStepId()
    {
        for (int i = 0; i < steps.Count; i++)
        {
            bool referenced = false;
            for (int j = 0; j < steps.Count; j++)
                if (steps[j].NextStepId == steps[i].Id)
                {
                    referenced = true;
                    break;
                }
            if (!referenced)
                return steps[i].Id;
        }
        return steps[0].Id;
    }

    private bool ValidateStepChain()
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < steps.Count; i++)
        {
            TutorialStep step = steps[i];
            if (step == null || !ids.Add(step.Id))
                return false;
            if (!SupportedTriggerConditions.Contains(step.TriggerCondition))
            {
                Debug.LogError("[Kingdom Onboarding] Unknown Tutorial trigger condition '" +
                    step.TriggerCondition + "' on step '" + step.Id + "'.");
                return false;
            }
            if (!SupportedCompletionConditions.Contains(step.CompletionCondition))
            {
                Debug.LogError("[Kingdom Onboarding] Unknown Tutorial completion condition '" +
                    step.CompletionCondition + "' on step '" + step.Id + "'.");
                return false;
            }
            if (!SupportedNavigationPages.Contains(step.NavigationPage))
            {
                Debug.LogError("[Kingdom Onboarding] Unknown Tutorial navigation page '" +
                    step.NavigationPage + "' on step '" + step.Id + "'.");
                return false;
            }
            if (!string.IsNullOrEmpty(step.NextStepId) && FindStep(step.NextStepId) == null)
                return false;
        }

        string rootId = FindRootStepId();
        int rootCount = 0;
        for (int i = 0; i < steps.Count; i++)
        {
            bool referenced = false;
            for (int j = 0; j < steps.Count; j++)
                if (steps[j].NextStepId == steps[i].Id)
                {
                    referenced = true;
                    break;
                }
            if (!referenced)
                rootCount++;
        }
        if (rootCount != 1 || string.IsNullOrEmpty(rootId))
            return false;

        var visited = new HashSet<string>();
        TutorialStep current = FindStep(rootId);
        while (current != null)
        {
            if (!visited.Add(current.Id))
                return false;
            current = string.IsNullOrEmpty(current.NextStepId)
                ? null
                : FindStep(current.NextStepId);
        }
        return visited.Count == steps.Count;
    }

    public TutorialSnapshot Evaluate()
    {
        EnsureDefinitions();
        GameManager game = cachedGameManager != null
            ? cachedGameManager
            : cachedGameManager = FindObjectOfType<GameManager>();
        ResourceManager resources = cachedResourceManager != null
            ? cachedResourceManager
            : cachedResourceManager = FindObjectOfType<ResourceManager>();
        BuildingManager buildings = cachedBuildingManager != null
            ? cachedBuildingManager
            : cachedBuildingManager = FindObjectOfType<BuildingManager>();
        ResearchManager research = cachedResearchManager != null
            ? cachedResearchManager
            : cachedResearchManager = FindObjectOfType<ResearchManager>();
        WorkshopManager workshop = null;
        if (game != null && game.State != null &&
            game.State.TechLevel >= TechLevel.Industrial)
            workshop = cachedWorkshopManager != null
                ? cachedWorkshopManager
                : cachedWorkshopManager = FindObjectOfType<WorkshopManager>();
        if (game == null || game.State == null || resources == null ||
            buildings == null || research == null ||
            (game != null && game.State != null &&
             game.State.TechLevel >= TechLevel.Industrial && workshop == null))
        {
            string missing = string.Empty;
            if (game == null) missing += "GameManager ";
            else if (game.State == null) missing += "GameState ";
            if (resources == null) missing += "ResourceManager ";
            if (buildings == null) missing += "BuildingManager ";
            if (research == null) missing += "ResearchManager ";
            if (game != null && game.State != null &&
                game.State.TechLevel >= TechLevel.Industrial && workshop == null)
                missing += "WorkshopManager ";
            if (!dependencyErrorLogged)
            {
                dependencyErrorLogged = true;
                Debug.LogError("[Kingdom Onboarding] Required runtime managers are missing: " +
                    missing.Trim());
            }
            return new TutorialSnapshot
            {
                CurrentGoal = "王国状态不可用",
                GoalDescription = "引导需要先完成核心 Manager 初始化。",
                Blocker = "缺少必要系统：" + missing.Trim(),
                RecommendedAction = "检查 GameManager、ResourceManager、BuildingManager 和 ResearchManager 的场景初始化。",
                NavigationPage = "Overview"
            };
        }
        dependencyErrorLogged = false;

        int previousVersion = version;
        AdvanceCompletedSteps(game, resources, buildings, research);
        TutorialStep step = FindStep(activeStepId) ?? steps[steps.Count - 1];
        TutorialStep completedStep = FindPreviousCompletedStep(step.Id);
        bool showCompletionFeedback = pendingCompletionFeedback;
        pendingCompletionFeedback = false;
        EraGoalEvaluation eraGoal = EraGoalEvaluator.Evaluate(
            game.State.TechLevel, research, resources);
        TutorialSnapshot snapshot = new TutorialSnapshot
        {
            StepId = step.Id,
            CurrentEra = game.State.TechLevel.GetDescription(),
            CivilizationContext = GetCivilizationContext(game.State.TechLevel),
            PopulationText = game.State.Population.Population.ToGameString() + "/" + game.State.Population.PopulationCapacity.ToGameString(),
            FoodText = game.State.FoodAmount.ToGameString() + "/" + game.State.FoodCapacity.ToGameString(),
            CompletedGoal = completedStep == null ? string.Empty : completedStep.Title,
            CurrentGoal = step.Title,
            GoalDescription = step.Description,
            NarrativeText = step.NarrativeText,
            NextEraGoal = BuildNextEraGoal(game, research, eraGoal),
            IsComplete = step.Kind == TutorialStepKind.LongTerm
        };
        if (showCompletionFeedback && completedStep != null)
            snapshot.CompletedFeedback = BuildCompletedFeedback(
                completedStep, game, resources, buildings);
        Resource coreResource = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
        snapshot.CoreResourceText = coreResource == null
            ? "未知"
            : coreResource.Label + " " + resources.GetAmount(coreResource).ToGameString();
        BuildDetails(snapshot, step, game, resources, buildings, research, eraGoal);
        if (version != previousVersion)
            SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    private static string BuildCompletedFeedback(TutorialStep completedStep,
        GameManager game, ResourceManager resources, BuildingManager buildings)
    {
        if (completedStep == null)
            return string.Empty;

        switch (completedStep.Kind)
        {
            case TutorialStepKind.Orientation:
                return "王国时间已经开始推进，资源生产循环已进入运行状态。";
            case TutorialStepKind.Resources:
                Resource coreResource = DataBase<Resource>.Find(
                    ResourceManager.StartingResourceId);
                if (coreResource != null && resources != null &&
                    resources.States.TryGetValue(coreResource, out ResourceState coreState) &&
                    coreState != null)
                    return coreResource.Label + " 当前 " + coreState.Amount.ToGameString() +
                        "，净产出 " + (
                            ResourceManager.ApplyCurrentProductionReward(
                                coreState.ProductionRate) - coreState.ConsumptionRate
                        ).ToGameString() + "/s。";
                return "你已经看到了核心资源的来源与净产出。";
            case TutorialStepKind.Building:
                return "建筑已经进入王国；它的生产、人口容量或研究效果现在可以在建筑页观察。";
            case TutorialStepKind.Population:
                return "当前人口 " + game.State.Population.Population.ToGameString() +
                    "/" + game.State.Population.PopulationCapacity.ToGameString() +
                    "，食物与幸福度正在决定下一批族人能否留下。";
            case TutorialStepKind.Research:
                return "研究已经完成；它的真实解锁内容可以在研究页和对应详情中继续追踪。";
            case TutorialStepKind.ProductionChain:
                string chainSummary = BuildOwnedProductionChainSummary(buildings);
                return string.IsNullOrEmpty(chainSummary)
                    ? "生产链已经连接；观察上游产出如何成为下游建筑的输入。"
                    : "生产链已经连接：" + chainSummary + "。观察上游产出如何成为下游建筑的输入。";
            case TutorialStepKind.EraGoal:
                return "时代已经推进，旧有生产链将继续承担新阶段的基础供给。";
            default:
                return string.Empty;
        }
    }

    public static string GetCivilizationContext(TechLevel era)
    {
        switch (era)
        {
            case TechLevel.Animal:
                return "鼠族只保住了最初的火种，复兴要从食物、原木和第一处定居点开始。";
            case TechLevel.StoneAge:
                return "鼠族重新定居，灌溉、储粮、陶器与文字让零散族群开始组织成文明。";
            case TechLevel.Medieval:
                return "鼠族正在恢复行政、贸易、城市与标准化生产，让更大的社会得以延续。";
            case TechLevel.Industrial:
                return "鼠族重新掌握电力、铁路与机器制造，文明复兴进入规模化加速阶段。";
            case TechLevel.Spacer:
                return "鼠族已经走出母星，轨道设施、航行与舰队把复兴带向星际。";
            case TechLevel.Ultra:
                return "鼠族正在探索超越旧文明边界的力量；当前阶段仍应先完成既有时代的跃迁。";
            case TechLevel.Archotech:
                return "鼠族文明开始触及远古技术留下的更深层秘密；这里目前仍是远期框架。";
            default:
                return "鼠族文明正在重新寻找自己的未来，从当前时代的真实能力继续前进。";
        }
    }

#if UNITY_EDITOR
    public void RecordPageVisitedForEditor(string pageName) =>
        RecordPageVisited(pageName);
#endif
    internal void RecordPageVisited(string pageName)
    {
        EnsureDefinitions();
        TutorialStep step = FindStep(activeStepId);
        if (step != null &&
            string.Equals(GetNavigationPageForStep(step, GetCurrentGameState()), pageName,
                StringComparison.Ordinal))
            visitedStepId = step.Id;
    }

    public void RecordDetailViewed(string pageName, string targetId)
    {
        EnsureDefinitions();
        if (string.Equals(pageName, "Resources", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(targetId) &&
            DataBase<Resource>.TryFind(targetId, out Resource viewedResource) &&
            viewedResource != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProgressionMilestoneRecorder.NotifyResourceDetailViewed();
#endif
        }
        TutorialStep step = FindStep(activeStepId);
        if (step == null || string.IsNullOrWhiteSpace(targetId) ||
            !string.Equals(GetNavigationPageForStep(step, GetCurrentGameState()), pageName,
                StringComparison.Ordinal))
            return;
        if (step.Kind != TutorialStepKind.Resources ||
            (!string.Equals(targetId, "Food", StringComparison.Ordinal) &&
             !string.Equals(targetId, ResourceManager.StartingResourceId,
                 StringComparison.Ordinal)))
            return;

        visitedDetailStepId = step.Id;
        visitedDetailTargetId = targetId;
        version++;
    }

#if UNITY_EDITOR
    public bool HasVisitedPageForStepForEditor(TutorialStep step) =>
        HasVisitedPageForStep(step);
#endif
    private bool HasVisitedPageForStep(TutorialStep step) =>
        step != null &&
        string.Equals(visitedStepId, step.Id, StringComparison.Ordinal);

    private GameState GetCurrentGameState()
    {
        GameManager game = cachedGameManager != null
            ? cachedGameManager
            : cachedGameManager = FindObjectOfType<GameManager>();
        return game == null ? null : game.State;
    }

#if UNITY_EDITOR
    public static string GetNavigationPageForStepForEditor(
        TutorialStep step, GameState gameState) =>
        GetNavigationPageForStep(step, gameState);
#endif
    private static string GetNavigationPageForStep(TutorialStep step, GameState gameState)
    {
        if (step == null)
            return string.Empty;
        if (step.Kind == TutorialStepKind.Population && gameState != null &&
            gameState.Population != null &&
            gameState.Population.PopulationCapacity > ExpantaNum.Zero &&
            (gameState.FoodNetRate <= ExpantaNum.Zero ||
             gameState.HappinessMultiplier < ExpantaNum.One))
            return "Resources";
        return step.NavigationPage;
    }

    private void AdvanceCompletedSteps(GameManager game, ResourceManager resources,
        BuildingManager buildings, ResearchManager research)
    {
        TutorialStep step = FindStep(activeStepId);
        if (step == null || step.Kind == TutorialStepKind.LongTerm)
            return;
        if (completedStepIds.Contains(step.Id) ||
            !IsStepTriggered(step, game) ||
            !HasVisitedPageForStep(step) ||
            !IsStepComplete(step, game, resources, buildings, research))
            return;

        // Evaluate is also called by live UI refreshes. Advance only the
        // currently visible step so that a passive state update cannot skip
        // several learning moments in one call.
        completedStepIds.Add(step.Id);
        activeStepId = step.NextStepId;
        pendingCompletionFeedback = true;
        version++;
    }

    private static bool IsStepTriggered(TutorialStep step, GameManager game)
    {
        switch (step.TriggerCondition)
        {
            case "game-state":
                return true;
            case "calendar-days>=1":
                return game.State.CalendarDays >= 1;
            case "era-reached":
                return game.State.TechLevel != TechLevel.Animal;
            default:
                Debug.LogError("[Kingdom Onboarding] Unsupported Tutorial trigger condition: " +
                    step.TriggerCondition);
                return false;
        }
    }

#if UNITY_EDITOR
    public static bool IsStepCompleteForEditor(TutorialStep step, GameManager game,
        ResourceManager resources, BuildingManager buildings, ResearchManager research) =>
        IsStepComplete(step, game, resources, buildings, research);
#endif
    private static bool IsStepComplete(TutorialStep step, GameManager game,
        ResourceManager resources, BuildingManager buildings, ResearchManager research)
    {
        switch (step.CompletionCondition)
        {
            case "Orientation":
            case "calendar-days>=1":
                return game.State.CalendarDays >= 1;
            case "Resources":
            case "resource-inventory-positive":
                Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
                return wood != null && resources.GetAmount(wood) > ExpantaNum.Zero;
            case "resource-detail-viewed":
                TutorialManager current = instance;
                return current != null &&
                    string.Equals(current.visitedDetailStepId, step.Id, StringComparison.Ordinal) &&
                    (string.Equals(current.visitedDetailTargetId, "Food", StringComparison.Ordinal) ||
                     string.Equals(current.visitedDetailTargetId, ResourceManager.StartingResourceId,
                         StringComparison.Ordinal));
            case "Building":
            case "building-owned":
                foreach (BuildingState state in buildings.States.Values)
                    if (state.Amount > ExpantaNum.Zero)
                        return true;
                return false;
            case "Population":
            case "population-grown":
                // Capacity is the result of the preceding building action.
                // This step is complete only after the population state has
                // actually grown (or a save already contains population).
                return HasObservedPopulationGrowth(game.State);
            case "population-capacity-positive":
                return game.State.Population != null &&
                    game.State.Population.PopulationCapacity > ExpantaNum.Zero;
            case "Research":
            case "research-complete":
                return HasCompletedResearch(research.States.Values);
            case "research-started":
                foreach (ResearchState state in research.States.Values)
                    if (state != null && (state.Status == ResearchStatus.Researching ||
                        state.Status == ResearchStatus.Queued ||
                        state.Status == ResearchStatus.WaitingResources))
                        return true;
                return false;
            case "ProductionChain":
            case "production-chain-owned":
                return HasOwnedProductionChain(buildings);
            case "EraGoal":
            case "era-reached":
                return game.State.TechLevel != TechLevel.Animal;
            default:
                Debug.LogError("[Kingdom Onboarding] Unsupported Tutorial completion condition: " +
                    step.CompletionCondition);
                return false;
        }
    }

    private void BuildDetails(TutorialSnapshot snapshot, TutorialStep step, GameManager game,
        ResourceManager resources, BuildingManager buildings, ResearchManager research,
        EraGoalEvaluation eraGoal)
    {
        snapshot.Blocker = "暂无阻碍";
        snapshot.RecommendedAction = "查看概览，确认当前王国状态。";
        snapshot.NavigationPage = "Overview";
        bool industrialMainlineActive = false;
        if (step.Kind == TutorialStepKind.Orientation)
        {
            snapshot.Blocker = "王国时间尚未推进满一天。";
            snapshot.RecommendedAction = "让王国时间继续推进，观察人口与核心资源的变化；等待期间也可查看资源页。";
        }
        else if (step.Kind == TutorialStepKind.Resources)
        {
            snapshot.Blocker = "核心资源尚未形成可见库存。";
            snapshot.RecommendedAction = game.State.FoodNetRate <= ExpantaNum.Zero
                ? "打开资源页面并查看食物详情：先确认库存与净产出，避免幸福度和人口增长被饥荒拖慢。"
                : "打开资源页面并查看原木详情：库存决定现在能否行动，净产出决定下一步需要等待多久。";
            snapshot.NavigationPage = "Resources";
            snapshot.NavigationTargetId = GetResourcesNavigationTarget(game.State);
        }
        else if (step.Kind == TutorialStepKind.Building)
        {
            Building recommendation = FindBuildingRecommendation(game, buildings, research);
            if (recommendation == null)
            {
                snapshot.Blocker = "当前没有可以根据王国状态明确推荐的建筑。";
                snapshot.RecommendedAction = "打开建筑页面，查看已解锁建筑的真实条件。";
            }
            else
            {
                snapshot.Blocker = DescribeBuildingBlocker(
                    recommendation, game, buildings, resources);
                snapshot.RecommendedAction = "打开建筑页面，选择能解决当前阻碍的“" + recommendation.Label +
                    "”：" + DescribeBuildingRole(recommendation);
                snapshot.NavigationTargetId = recommendation.Id;
            }
            snapshot.NavigationPage = "Buildings";
        }
        else if (step.Kind == TutorialStepKind.Population)
        {
            if (game.State.Population.PopulationCapacity > ExpantaNum.Zero &&
                (game.State.FoodNetRate <= ExpantaNum.Zero ||
                 game.State.HappinessMultiplier < ExpantaNum.One))
            {
                snapshot.Blocker = "食物净产出或幸福度不足，人口增长会受限。";
                snapshot.RecommendedAction = "打开资源页面，先稳定食物净产出。";
                snapshot.NavigationPage = "Resources";
                snapshot.NavigationTargetId = "Food";
            }
            else if (game.State.Population.PopulationCapacity <= ExpantaNum.Zero)
            {
                BuildPopulationCapacityGuidance(
                    snapshot, "需要一个能提供人口容量的建筑。",
                    game, resources, buildings, research);
            }
            else if (game.State.Population.Population >= game.State.Population.PopulationCapacity)
            {
                BuildPopulationCapacityGuidance(
                    snapshot, "人口已达到当前容量，需要继续扩展容量。",
                    game, resources, buildings, research);
            }
            else
            {
                snapshot.Blocker = "人口正在向现有人口容量增长。";
                snapshot.RecommendedAction = "继续维持食物和幸福度，观察人口转化为生产力。";
                snapshot.NavigationPage = "Buildings";
            }
        }
        else if (step.Kind == TutorialStepKind.Research)
        {
            Research guidanceResearch = FindResearchGuidance(
                research, game.State.TechLevel);
            snapshot.Blocker = research.ActiveResearch == null ? "尚未开始一项研究。" : "研究正在推进。";
            if (research.ActiveResearch != null &&
                research.ActiveResearch.Status == ResearchStatus.WaitingResources &&
                !research.CanPayResearchCost(research.ActiveResearch.Definition, out string researchBlocker))
                snapshot.Blocker = researchBlocker;
            snapshot.RecommendedAction = guidanceResearch == null
                ? "打开研究页面，查看研究效果和前置条件。"
                : "打开研究页面，查看“" + guidanceResearch.Label +
                    "”：" + DescribeResearchRole(guidanceResearch);
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = guidanceResearch == null ? string.Empty : guidanceResearch.Id;
        }
        else if (step.Kind == TutorialStepKind.ProductionChain)
        {
            string chainSummary = BuildOwnedProductionChainSummary(buildings);
            Building chainRecommendation = FindProductionChainRecommendation(game, buildings);
            bool lacksProductivity = chainRecommendation != null &&
                chainRecommendation.ProductivityConsumption > ExpantaNum.Max(ExpantaNum.Zero, buildings.AvailableProductivity);
            Building capacityRecommendation = lacksProductivity
                ? FindPopulationCapacityRecommendation(game, buildings)
                : null;
            snapshot.Blocker = lacksProductivity
                ? "可用生产力不足。"
                : string.IsNullOrEmpty(chainSummary)
                    ? "尚未拥有一组相连的生产与加工建筑。"
                    : chainSummary;
            snapshot.RecommendedAction = lacksProductivity
                ? capacityRecommendation == null
                    ? "打开建筑页面，先解决人口容量与生产力问题。"
                    : "打开建筑页面，先建造“" + capacityRecommendation.Label + "”获得人口容量。"
                : string.IsNullOrEmpty(chainSummary)
                    ? "打开建筑页面，查看生产与消耗关系。"
                    : "打开建筑页面，继续扩展：" + chainSummary;
            snapshot.NavigationPage = "Buildings";
            Building navigationRecommendation = lacksProductivity
                ? capacityRecommendation
                : chainRecommendation;
            snapshot.NavigationTargetId = navigationRecommendation == null
                ? string.Empty
                : navigationRecommendation.Id;
            if (lacksProductivity && game.State.Population.Population < game.State.Population.PopulationCapacity)
            {
                snapshot.Blocker = "目标建筑生产力不足，现有住房仍可入住。";
                snapshot.RecommendedAction = "先保持正净粮，等待居民入住；每位新居民提供基础2生产力并消耗0.8食物/秒。";
                snapshot.NavigationPage = "Resources";
                snapshot.NavigationTargetId = "Food";
            }
            if (!lacksProductivity && string.IsNullOrEmpty(chainSummary))
            {
                bool hasChainAction = TryFindNextProductionChainAction(
                    chainRecommendation, resources, buildings, research,
                    out Building actionBuilding, out Research actionResearch,
                    out Resource actionResource, out WorkshopUpgrade actionWorkshop);
                if (hasChainAction && actionResearch != null)
                {
                    snapshot.Blocker = "\u751f\u4ea7\u94fe\u5efa\u7b51\u8fd8\u7f3a\u5c11\u524d\u7f6e\u7814\u7a76\uff1a" + actionResearch.Label;
                    snapshot.RecommendedAction = "\u6253\u5f00\u7814\u7a76\u9875\u9762\uff0c\u5148\u5b8c\u6210\u201c" + actionResearch.Label + "\u201d\u3002";
                    snapshot.NavigationPage = "Research";
                    snapshot.NavigationTargetId = actionResearch.Id;
                }
                else if (hasChainAction && actionResource != null)
                {
                    snapshot.Blocker = "\u751f\u4ea7\u94fe\u5efa\u7b51\u8fd8\u7f3a\u5c11\u8d44\u6e90\uff1a" + actionResource.Label;
                    snapshot.RecommendedAction = "\u6253\u5f00\u8d44\u6e90\u9875\u9762\uff0c\u67e5\u770b\u201c" + actionResource.Label + "\u201d\u3002";
                    snapshot.NavigationPage = "Resources";
                    snapshot.NavigationTargetId = actionResource.Id;
                }
                else if (hasChainAction && actionWorkshop != null)
                {
                    snapshot.Blocker = "生产链建筑还缺少工坊改良：" + actionWorkshop.Label;
                    snapshot.RecommendedAction = "打开 Workshop 页面，先完成“" +
                        actionWorkshop.Label + "”。";
                    snapshot.NavigationPage = "Workshop";
                    snapshot.NavigationTargetId = actionWorkshop.Id;
                }
                else if (hasChainAction && actionBuilding != null)
                {
                    snapshot.Blocker = DescribeBuildingBlocker(
                        actionBuilding, game, buildings, resources);
                    snapshot.RecommendedAction = "\u6253\u5f00\u5efa\u7b51\u9875\u9762\uff0c\u67e5\u770b\u201c" + actionBuilding.Label + "\u201d\u5e76\u8865\u9f50\u7f3a\u53e3\u3002";
                    snapshot.NavigationPage = "Buildings";
                    snapshot.NavigationTargetId = actionBuilding.Id;
                }
                else
                {
                    // No current-era pair exists. Reuse the era evaluator so
                    // this step points to a real next blocker rather than an
                    // empty Buildings navigation.
                    BuildEraGoalGuidance(snapshot, eraGoal);
                }
            }
        }
        else if (step.Kind == TutorialStepKind.EraGoal)
        {
            BuildEraGoalGuidance(snapshot, eraGoal);
        }
        else if (step.Kind == TutorialStepKind.LongTerm)
        {
            if (game.State.TechLevel == TechLevel.Industrial)
            {
                industrialMainlineActive = BuildIndustrialGuidance(
                    snapshot, game, resources, research, buildings);
            }
            else if (game.State.TechLevel == TechLevel.Spacer)
                industrialMainlineActive = BuildSpacerGuidance(
                    snapshot, game, research, buildings);
            if (!industrialMainlineActive)
            {
                BuildEraGoalGuidance(snapshot, eraGoal);
                if (game.State.TechLevel == TechLevel.StoneAge || game.State.TechLevel == TechLevel.Medieval)
                    BuildEarlyInvestmentGuidance(snapshot, game);
            }
        }
        if (step.Kind != TutorialStepKind.LongTerm &&
            !HasVisitedPageForStep(step))
        {
            snapshot.Blocker = "尚未查看本步骤对应页面。";
            if (step.Kind == TutorialStepKind.Population)
                snapshot.RecommendedAction = "打开建筑页面，查看人口容量与人口变化。";
            if (step.Kind == TutorialStepKind.Population &&
                GetNavigationPageForStep(step, game.State) == "Resources")
                snapshot.RecommendedAction = "打开资源页面，观察食物供给与幸福度。";
            snapshot.NavigationPage = GetNavigationPageForStep(step, game.State);
            snapshot.NavigationTargetId = string.Empty;
            if (step.Kind == TutorialStepKind.Resources)
                snapshot.NavigationTargetId = GetResourcesNavigationTarget(game.State);
            else if (step.Kind == TutorialStepKind.Population)
                snapshot.NavigationTargetId = GetPopulationNavigationTarget(game, buildings);
        }
        if (step.Kind == TutorialStepKind.Resources)
        {
            snapshot.Blocker = "\u5148\u67e5\u770b\u4e00\u4e2a\u6838\u5fc3\u8d44\u6e90\u8be6\u60c5\uff0c\u7406\u89e3\u5e93\u5b58\u4e0e\u51c0\u4ea7\u51fa\u3002";
            snapshot.RecommendedAction = game.State.FoodNetRate <= ExpantaNum.Zero
                ? "\u6253\u5f00\u8d44\u6e90\u9875\u9762\u5e76\u67e5\u770b\u98df\u7269\u8be6\u60c5\uff1a\u5148\u786e\u8ba4\u98df\u7269\u5e93\u5b58\u4e0e\u51c0\u4ea7\u51fa\u3002"
                : "\u6253\u5f00\u8d44\u6e90\u9875\u9762\u5e76\u67e5\u770b\u539f\u6728\u8be6\u60c5\uff1a\u5e93\u5b58\u51b3\u5b9a\u73b0\u5728\u80fd\u5426\u884c\u52a8\uff0c\u51c0\u4ea7\u51fa\u51b3\u5b9a\u9700\u8981\u7b49\u5f85\u591a\u4e45\u3002";
        }
        if (!industrialMainlineActive && step.Kind != TutorialStepKind.EraGoal &&
            step.Kind != TutorialStepKind.LongTerm)
            industrialMainlineActive = BuildIndustrialGuidance(
                snapshot, game, resources, research, buildings);
        if (game.State.TechLevel == TechLevel.Industrial)
            BuildIndustrialRoute(snapshot, research, buildings);
        if (industrialMainlineActive)
            snapshot.NextEraGoal = "长期方向（完成当前工业主线后）：" + snapshot.NextEraGoal;

        // Authored navigation is the default for ordinary steps. Population
        // keeps its state-driven Resources/Buildings choice, while EraGoal
        // and industrial guidance must point at the real current blocker.
        if (!industrialMainlineActive &&
            step.Kind != TutorialStepKind.Population &&
            step.Kind != TutorialStepKind.EraGoal &&
            step.Kind != TutorialStepKind.LongTerm &&
            step.Kind != TutorialStepKind.ProductionChain &&
            !string.IsNullOrWhiteSpace(step.NavigationPage))
            snapshot.NavigationPage = step.NavigationPage;
    }

    private static string GetResourcesNavigationTarget(GameState state)
    {
        return state != null && state.FoodNetRate <= ExpantaNum.Zero
            ? "Food"
            : ResourceManager.StartingResourceId;
    }

    private static void BuildIndustrialRoute(
        TutorialSnapshot snapshot, ResearchManager research, BuildingManager buildings)
    {
        if (snapshot == null || research == null || buildings == null)
            return;

        string[] route =
        {
            "IndustrialWorkshop", "SteamPower", "SteamPlant", "PowerGridEngineering", "CentralPowerStation", "IndustrialMetalSmelting", "IndustrialMetalSmelter", "IndustrialChemistry", "ChemicalPlant", "PrecisionManufacturing", "MachineFactory", "FactoryOrganization", "IndustrialHabitationEngineering", "IndustrialHabitationComplex", "RailwayEngineering", "RailHub", "ModernUniversity", "University", "TitaniumAlloyEngineering"
        };
        var pending = new List<string>();
        for (int i = 0; i < route.Length; i++)
            if (!IsIndustrialRouteStepComplete(route[i], research, buildings))
                pending.Add(route[i]);

        if (pending.Count > 0)
            snapshot.IndustrialCurrentStep = GetRouteStepLabel(pending[0]);
        if (pending.Count > 1)
            snapshot.IndustrialNextStep = GetRouteStepLabel(pending[1]);
        if (pending.Count > 2)
            snapshot.IndustrialAfterStep = GetRouteStepLabel(pending[2]);
    }

    private static bool IsIndustrialRouteStepComplete(
        string id, ResearchManager research, BuildingManager buildings)
    {
        if (DataBase<Research>.TryFind(id, out Research researchDefinition))
            return research.States.TryGetValue(researchDefinition, out ResearchState state) &&
                state.Status == ResearchStatus.Completed;
        if (DataBase<Building>.TryFind(id, out Building buildingDefinition))
            return HasOwnedBuildingId(buildings, id);
        return true;
    }

    private static string GetRouteStepLabel(string id)
    {
        if (DataBase<Research>.TryFind(id, out Research research))
            return research.Label;
        if (DataBase<Building>.TryFind(id, out Building building))
            return building.Label;
        return id;
    }

    private static void BuildEraGoalGuidance(TutorialSnapshot snapshot,
        EraGoalEvaluation evaluation)
    {
        snapshot.RecommendedAction = "打开时代页面，查看下一时代的真实条件。";
        snapshot.NavigationPage = "Era";
        snapshot.NavigationTargetId = string.Empty;

        if (evaluation.Transition == null)
        {
            snapshot.Blocker = snapshot.NextEraGoal;
            snapshot.RecommendedAction = "查看已完成的文明工程、据点与剧情，继续完成仍未完成的现有目标。";
            snapshot.NavigationPage = "Story";
            return;
        }

        for (int i = 0; i < evaluation.Conditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = evaluation.Conditions[i];
            if (condition == null || condition.Met)
                continue;

            if (condition.Kind == EraGoalConditionKind.PrerequisiteResearch)
            {
                string label = condition.Research == null
                    ? "关键研究"
                    : condition.Research.Label;
                snapshot.Blocker = "下一时代仍缺少关键研究：“" + label + "”。";
                snapshot.RecommendedAction = "打开研究页面，查看并完成“" + label +
                    "”，然后回到时代页面确认推进条件。";
                snapshot.NavigationPage = "Research";
                snapshot.NavigationTargetId = condition.Research == null
                    ? string.Empty
                    : condition.Research.Id;
                return;
            }

            string resourceLabel = condition.Resource == null
                ? "时代资源"
                : condition.Resource.Label;
            snapshot.Blocker = "下一时代还需要“" + resourceLabel +
                "”：" + condition.RemainingAmount.ToGameString() +
                "，当前可用 " + condition.AvailableAmount.ToGameString() + "。";
            snapshot.RecommendedAction = "打开资源页面，查看“" + resourceLabel +
                "”的来源与净产出，继续完善生产链。";
            snapshot.NavigationPage = "Resources";
            return;
        }

        snapshot.Blocker = "下一时代的研究前置与资源条件已满足。";
        snapshot.RecommendedAction = "打开研究页面，推进时代研究“" +
            evaluation.Transition.Label + "”。";
        snapshot.NavigationPage = "Research";
        snapshot.NavigationTargetId = evaluation.Transition.Id;
    }

    private static void BuildEarlyInvestmentGuidance(TutorialSnapshot snapshot,
        GameManager game)
    {
        if (game.State.FoodNetRate <= ExpantaNum.Zero)
        {
            snapshot.Blocker = "日常粮食供给没有余量，住房和科研投资需要先稳住供给。";
            snapshot.RecommendedAction = "查看食物来源，补粮或减少持续负担；容量只提供缓冲，不增加粮产。";
            snapshot.NavigationPage = "Resources";
            snapshot.NavigationTargetId = "Food";
            return;
        }
        snapshot.RecommendedAction += game.State.TechLevel == TechLevel.StoneAge
            ? " 可选投资：灌溉改善土地利用与储粮，文书支持科研；生产力紧张时保留部分旧农场。"
            : " 可选投资：先扩钢材供给、推进机械化，或在准入后完善公共建设与知识设施；这些支线不是工业时代硬门。";
        if (game.State.Population.Population < game.State.Population.PopulationCapacity)
            snapshot.RecommendedAction += " 住房容量须等待入住才转为生产力，满员后需持续补粮。";
    }

    private enum GuidanceKind
    {
        Research,
        Building,
        Workshop
    }

    private readonly struct IndustrialGuidanceRow
    {
        public IndustrialGuidanceRow(
            GuidanceKind kind, string id, string blockerPrefix, string blockerSuffix,
            bool blockerUsesLabel, string actionPrefix, string actionSuffix)
        {
            Kind = kind;
            Id = id;
            BlockerPrefix = blockerPrefix;
            BlockerSuffix = blockerSuffix;
            BlockerUsesLabel = blockerUsesLabel;
            ActionPrefix = actionPrefix;
            ActionSuffix = actionSuffix;
        }

        public GuidanceKind Kind { get; }
        public string Id { get; }
        public string BlockerPrefix { get; }
        public string BlockerSuffix { get; }
        public bool BlockerUsesLabel { get; }
        public string ActionPrefix { get; }
        public string ActionSuffix { get; }
    }

    private static readonly IndustrialGuidanceRow[] IndustrialGuidancePath =
    {
        new IndustrialGuidanceRow(GuidanceKind.Research, "IndustrialWorkshop",
            "工业体系还缺少可重复改良的工坊：先完成“", "”。", true,
            "打开研究页面，完成“", "”，让旧工艺能够被反复验证和改进。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "SteamPower",
            "工业能源尚未建立：先完成“", "”。", true,
            "打开研究页面，完成“", "”，解锁蒸汽动力。"),
        new IndustrialGuidanceRow(GuidanceKind.Building, "SteamPlant",
            "蒸汽动力已经掌握，但还没有“", "”。", true,
            "打开建筑页面，建造“", "”，让工业生产真正运转。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "PowerGridEngineering",
            "蒸汽动力已经建立，接下来连通共享电网，为化工与精密制造提供能源。", string.Empty, false,
            "打开研究页面，完成“", "”，建立共享电网。"),
        new IndustrialGuidanceRow(GuidanceKind.Building, "CentralPowerStation",
            "电网技术已经掌握，但还没有“", "”连接整座王国。", true,
            "打开建筑页面，建造“", "”，让能源穿过整座王国。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "IndustrialMetalSmelting",
            "工坊与能源已经具备基础，接下来用工业冶炼准备标准材料。", string.Empty, false,
            "打开研究页面，完成“", "”，建立工业冶炼链。"),
        new IndustrialGuidanceRow(GuidanceKind.Building, "IndustrialMetalSmelter",
            "工业冶炼技术已经掌握，但还没有“", "”把矿石变成标准材料。", true,
            "打开建筑页面，建造“", "”，观察矿石如何进入工业链。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "IndustrialChemistry",
            "共享电网已具备基础，下一步让旧材料承担化工与精密制造的新用途。", string.Empty, false,
            "打开研究页面，完成“", "”，理解化学工业的输入与风险。"),
        new IndustrialGuidanceRow(GuidanceKind.Building, "ChemicalPlant",
            "化学技术已经掌握，但还没有“", "”验证这条新链。", true,
            "打开建筑页面，建造“", "”，观察材料如何重新组合。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "PrecisionManufacturing",
            "机器工厂还缺少精密制造知识：先完成“", "”。", true,
            "打开研究页面，完成“", "”，让工坊经验能够转化为稳定的机器生产。"),
        new IndustrialGuidanceRow(GuidanceKind.Workshop, "MachineFactory",
            "工业工坊已经解锁，但还没有一项真实改良。", string.Empty, false,
            "打开 Workshop 页面，购买一项真实改良，再继续建设机器工厂。", string.Empty),
        new IndustrialGuidanceRow(GuidanceKind.Building, "MachineFactory",
            "精密制造与工坊改良已经准备，但还没有“", "”把规模变成现实。", true,
            "打开建筑页面，建造“", "”，让工业时代第一次真正运转。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "FactoryOrganization",
            "机器工厂已经启动，但工业规模还需要统一组织：先完成“", "”。", true,
            "打开研究页面，完成“", "”，让工厂经验能够被制度化并复制到更大的王国。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "IndustrialHabitationEngineering",
            "工业城市即将吸纳更多人口：先完成“", "”。", true,
            "打开研究页面，完成“", "”，理解工业人口与城市的关系。"),
        new IndustrialGuidanceRow(GuidanceKind.Building, "IndustrialHabitationComplex",
            "工业人口需要真正的居住空间：还没有“", "”。", true,
            "打开建筑页面，建造“", "”，给每一代鼠族留下位置。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "RailwayEngineering",
            "能源已经稳定，下一项阻碍是把原料送到工厂。", string.Empty, false,
            "打开研究页面，完成“", "”，建立铁路物流。"),
        new IndustrialGuidanceRow(GuidanceKind.Building, "RailHub",
            "铁路技术已经掌握，但王国还没有“", "”。", true,
            "打开建筑页面，建造“", "”，连接分散的生产链。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "ModernUniversity",
            "共享电网已经建立，下一步是把工业经验保存成可复制的知识。", string.Empty, false,
            "打开研究页面，完成“", "”，让知识能够传给下一代。"),
        new IndustrialGuidanceRow(GuidanceKind.Building, "University",
            "现代知识已经准备好，但王国还没有“", "”保存与传播它。", true,
            "打开建筑页面，建造“", "”，让每一代人都能从过去继续起步。"),
        new IndustrialGuidanceRow(GuidanceKind.Research, "TitaniumAlloyEngineering",
            "工业体系已经能够传承知识，但还没有为星际结构准备钛合金工艺。", string.Empty, false,
            "打开研究页面，完成“", "”，把工业材料能力连接到星际时代。")
    };

    private static bool BuildIndustrialGuidance(
        TutorialSnapshot snapshot, GameManager game, ResourceManager resources,
        ResearchManager research, BuildingManager buildings)
    {
        if (snapshot == null || game == null || game.State == null ||
            game.State.TechLevel != TechLevel.Industrial)
            return false;

        for (int i = 0; i < IndustrialGuidancePath.Length; i++)
        {
            IndustrialGuidanceRow row = IndustrialGuidancePath[i];
            string label = string.Empty;
            switch (row.Kind)
            {
                case GuidanceKind.Workshop:
                    if (HasRelevantWorkshopForBuilding(
                        row.Id, research, out WorkshopUpgrade workshopTarget))
                        continue;
                    snapshot.Blocker = row.BlockerPrefix;
                    snapshot.RecommendedAction = row.ActionPrefix;
                    snapshot.NavigationPage = "Workshop";
                    snapshot.NavigationTargetId = workshopTarget == null
                        ? string.Empty
                        : workshopTarget.Id;
                    return true;
                case GuidanceKind.Research:
                    if (HasCompletedResearchId(research, row.Id))
                        continue;
                    if (ApplyResearchPrerequisiteGuidance(snapshot, row.Id, research))
                        return true;
                    label = GetResearchLabel(row.Id);
                    snapshot.NavigationPage = "Research";
                    break;
                case GuidanceKind.Building:
                    if (HasOwnedBuildingId(buildings, row.Id))
                        continue;
                    if (ApplyBuildingPrerequisiteGuidance(snapshot, row.Id, research))
                        return true;
                    if (ApplyIndustrialBuildingGuidance(
                        snapshot, row.Id, game, resources, buildings))
                        return true;
                    label = GetBuildingLabel(row.Id);
                    snapshot.NavigationPage = "Buildings";
                    break;
            }

            snapshot.Blocker = row.BlockerPrefix +
                (row.BlockerUsesLabel ? label : string.Empty) + row.BlockerSuffix;
            snapshot.RecommendedAction = row.ActionPrefix + label + row.ActionSuffix;
            snapshot.NavigationTargetId = row.Id;
            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    public static bool BuildSpacerGuidanceForEditor(
        TutorialSnapshot snapshot, GameManager game, ResearchManager research,
        BuildingManager buildings) =>
        BuildSpacerGuidance(snapshot, game, research, buildings);
#endif
    private static bool BuildSpacerGuidance(
        TutorialSnapshot snapshot, GameManager game, ResearchManager research,
        BuildingManager buildings)
    {
        if (snapshot == null || game == null || game.State == null ||
            game.State.TechLevel != TechLevel.Spacer)
            return false;

        string[] researchPath =
        {
            "HomeSystemSurvey",
            "DeepSpaceFleet",
            "InterstellarNavigation"
        };
        for (int i = 0; i < researchPath.Length; i++)
        {
            if (i == 1 && BuildHomeSystemGuidance(snapshot, game, research, buildings))
                return true;
            string researchId = researchPath[i];
            if (HasCompletedResearchId(research, researchId))
                continue;
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, researchId, research))
                return true;

            string label = GetResearchLabel(researchId);
            snapshot.Blocker = "星际主线还缺少关键研究：“" + label + "”。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + label +
                "”，把工业能力连接到星区与远航。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = researchId;
            return true;
        }

        string[] buildingPath =
        {
            "LaunchCenter",
            "OrbitalStation",
            "Shipyard"
        };
        for (int i = 0; i < buildingPath.Length; i++)
        {
            string buildingId = buildingPath[i];
            if (HasOwnedBuildingId(buildings, buildingId))
                continue;
            if (ApplyBuildingPrerequisiteGuidance(
                snapshot, buildingId, research))
                return true;
            if (!DataBase<Building>.TryFind(
                buildingId, out Building building) || building == null)
                return false;

            snapshot.Blocker = DescribeBuildingBlocker(
                building, game, buildings, ResourceManager.Instance);
            snapshot.RecommendedAction = "打开建筑页面，建造“" + building.Label +
                "”，让王国把生产能力延伸到星海。";
            snapshot.NavigationPage = "Buildings";
            snapshot.NavigationTargetId = building.Id;
            return true;
        }

        return BuildSpacerSectorGuidance(snapshot, game);
    }

    private static bool BuildHomeSystemGuidance(
        TutorialSnapshot snapshot, GameManager game, ResearchManager research,
        BuildingManager buildings)
    {
        game.Sectors.InitializeDefinitions();
        for (int i = 0; i < game.Sectors.OrderedStates.Count; i++)
        {
            SectorState state = game.Sectors.OrderedStates[i];
            if (state.Definition != null && state.Definition.IsHomeSystem && state.Occupied)
                return false;
        }
        // OrderedStates uses stable IDs. Continue an existing operation before
        // choosing the first reachable, unfinished home-system destination.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < game.Sectors.OrderedStates.Count; i++)
            {
                SectorState state = game.Sectors.OrderedStates[i];
                SectorDefinition definition = state.Definition;
                if (definition == null || !definition.IsHomeSystem || state.Occupied ||
                    (pass == 0 && !state.ColonizationActive) ||
                    !game.Sectors.CanAccess(definition))
                    continue;

                SectorOperationFailure failure = game.Sectors.GetUnlockFailure(definition);
                if (failure == SectorOperationFailure.LaunchCenterRequired)
                {
                    if (ApplyBuildingPrerequisiteGuidance(snapshot, "LaunchCenter", research))
                        return true;
                    Building launchCenter = DataBase<Building>.Find("LaunchCenter");
                    snapshot.Blocker = DescribeBuildingBlocker(
                        launchCenter, game, buildings, ResourceManager.Instance);
                    snapshot.RecommendedAction = "打开建筑页面，建造“" + launchCenter.Label +
                        "”，再开始本星系探索。";
                    snapshot.NavigationPage = "Buildings";
                    snapshot.NavigationTargetId = launchCenter.Id;
                    return true;
                }
                if (failure != SectorOperationFailure.None &&
                    failure != SectorOperationFailure.AlreadyUnlocked)
                    continue;

                SectorExplorationPreview preview = game.Sectors.GetExplorationPreview(
                    definition, game.State, ResourceManager.Instance);
                snapshot.Blocker = state.CampaignProgress >= ExpantaNum.One
                    ? "本星系“" + definition.Label + "”的探索已经完成，可以确认占领。"
                    : !preview.HasSupply
                        ? "本星系“" + definition.Label + "”的探索补给不足，先补齐详情列出的持续消耗。"
                        : state.ColonizationActive
                            ? "本星系“" + definition.Label + "”正在殖民，继续观察补给与探索进度。"
                            : "本星系“" + definition.Label + "”已满足探索前置，可以建立下一处据点。";
                snapshot.RecommendedAction = "打开星区页面，查看“" + definition.Label +
                    "”的解锁、补给与殖民状态。";
                snapshot.NavigationPage = "Sectors";
                snapshot.NavigationTargetId = definition.Id;
                return true;
            }
        }
        return false;
    }

    private static bool BuildSpacerSectorGuidance(
        TutorialSnapshot snapshot, GameManager game)
    {
        if (snapshot == null || game == null || game.Sectors == null)
            return false;

        bool hasUnlockedSector = false;
        for (int i = 0; i < game.Sectors.OrderedStates.Count; i++)
        {
            SectorState state = game.Sectors.OrderedStates[i];
            if (state == null || !state.Unlocked)
                continue;

            hasUnlockedSector = true;
            if (state.Occupied)
                continue;

            string sectorLabel = state.Definition == null
                ? "当前星区"
                : state.Definition.Label;
            if (state.CampaignActive)
            {
                snapshot.Blocker = "星区“" + sectorLabel +
                    "”的远征正在进行，先观察真实补给、进度与伤亡。";
                snapshot.RecommendedAction = "打开星区页面，查看“" +
                    sectorLabel + "”的远征状态。";
            }
            else if (state.CampaignProgress > ExpantaNum.Zero)
            {
                snapshot.Blocker = "星区“" + sectorLabel +
                    "”的远征已经完成阶段性推进，下一步确认占领条件。";
                snapshot.RecommendedAction = "打开星区页面，完成“" +
                    sectorLabel + "”的真实占领流程。";
            }
            else if (state.ColonizationActive)
            {
                snapshot.Blocker = "星区“" + sectorLabel +
                    "”正在殖民，补给与探索进度决定它何时能够成为前线。";
                snapshot.RecommendedAction = "打开星区页面，观察“" +
                    sectorLabel + "”的殖民进度。";
            }
            else
            {
                snapshot.Blocker = "星区“" + sectorLabel +
                    "”已经解锁，下一步是用真实探索与补给建立前线。";
                snapshot.RecommendedAction = "打开星区页面，查看“" +
                    sectorLabel + "”可执行的殖民或远征行动。";
            }
            snapshot.NavigationPage = "Sectors";
            snapshot.NavigationTargetId = state.Definition == null
                ? string.Empty
                : state.Definition.Id;
            return true;
        }

        if (!hasUnlockedSector)
        {
            snapshot.Blocker = "太空基础设施已经建立，但还没有解锁真实星区。";
            snapshot.RecommendedAction = "打开星区页面，查看第一片可解锁的真实星区。";
            snapshot.NavigationPage = "Sectors";
            snapshot.NavigationTargetId = string.Empty;
            return true;
        }

        return false;
    }

    private static bool HasCompletedResearchId(
        ResearchManager research, string id)
    {
        if (research == null || !DataBase<Research>.TryFind(id, out Research definition) ||
            !research.States.TryGetValue(definition, out ResearchState state))
            return false;
        return state != null && state.Status == ResearchStatus.Completed;
    }

    private static bool HasRelevantWorkshopForBuilding(
        string buildingId, ResearchManager research,
        out WorkshopUpgrade missingUpgrade)
    {
        missingUpgrade = null;
        if (!DataBase<Building>.TryFind(buildingId, out Building building) ||
            building == null || building.RequiredWorkshopUpgrades.Count == 0)
            return true;

        for (int r = 0; r < building.RequiredResearch.Count; r++)
        {
            Research prerequisite = building.RequiredResearch[r];
            if (prerequisite == null || research == null ||
                !research.IsResearchCompleted(prerequisite.Id))
                return true;
        }

        WorkshopManager workshop = WorkshopManager.Instance;
        for (int i = 0; i < building.RequiredWorkshopUpgrades.Count; i++)
        {
            WorkshopUpgrade upgrade = building.RequiredWorkshopUpgrades[i];
            if (upgrade == null || workshop == null || workshop.IsPurchased(upgrade))
                continue;

            // Leave research and upgrade prerequisites to the existing
            // building-prerequisite guidance until this upgrade is actually
            // purchasable. This avoids sending the player to Workshop too
            // early, while still rejecting unrelated purchases.
            bool prerequisitesMet = true;
            for (int r = 0; r < upgrade.RequiredResearch.Count; r++)
            {
                Research prerequisite = upgrade.RequiredResearch[r];
                if (prerequisite == null || research == null ||
                    !research.IsResearchCompleted(prerequisite.Id))
                {
                    prerequisitesMet = false;
                    break;
                }
            }
            if (prerequisitesMet)
                for (int u = 0; u < upgrade.RequiredUpgrades.Count; u++)
                    if (upgrade.RequiredUpgrades[u] == null ||
                        workshop == null ||
                        !workshop.IsPurchased(upgrade.RequiredUpgrades[u]))
                    {
                        prerequisitesMet = false;
                        break;
                    }

            if (prerequisitesMet)
            {
                missingUpgrade = upgrade;
                return false;
            }
        }
        return true;
    }

    private static bool ApplyBuildingPrerequisiteGuidance(
        TutorialSnapshot snapshot, string buildingId, ResearchManager research)
    {
        if (snapshot == null ||
            !DataBase<Building>.TryFind(buildingId, out Building building) ||
            building == null)
            return false;

        for (int i = 0; i < building.RequiredResearch.Count; i++)
        {
            Research prerequisite = building.RequiredResearch[i];
            if (prerequisite == null || research == null ||
                !research.IsResearchCompleted(prerequisite.Id))
            {
                string label = prerequisite == null ? "研究前置" : prerequisite.Label;
                snapshot.Blocker = "“" + building.Label + "”还缺少研究前置：“" +
                    label + "”。";
                snapshot.RecommendedAction = "打开研究页面，先完成“" + label +
                    "”，再回到建筑页面建造“" + building.Label + "”。";
                snapshot.NavigationPage = "Research";
                snapshot.NavigationTargetId = prerequisite == null
                    ? string.Empty
                    : prerequisite.Id;
                return true;
            }
        }

        WorkshopManager workshop = FindObjectOfType<WorkshopManager>();
        for (int i = 0; i < building.RequiredWorkshopUpgrades.Count; i++)
        {
            WorkshopUpgrade prerequisite = building.RequiredWorkshopUpgrades[i];
            if (prerequisite == null || workshop == null ||
                !workshop.IsPurchased(prerequisite))
            {
                if (prerequisite != null && workshop != null)
                {
                    for (int r = 0; r < prerequisite.RequiredResearch.Count; r++)
                    {
                        Research researchPrerequisite = prerequisite.RequiredResearch[r];
                        if (researchPrerequisite == null || research == null ||
                            !research.IsResearchCompleted(researchPrerequisite.Id))
                        {
                            string researchLabel = researchPrerequisite == null
                                ? "工坊研究前置"
                                : researchPrerequisite.Label;
                            snapshot.Blocker = "工坊改良“" + prerequisite.Label +
                                "”还缺少研究前置：“" + researchLabel + "”。";
                            snapshot.RecommendedAction = "打开研究页面，先完成“" +
                                researchLabel + "”，再购买“" + prerequisite.Label + "”。";
                            snapshot.NavigationPage = "Research";
                            snapshot.NavigationTargetId = researchPrerequisite == null
                                ? string.Empty
                                : researchPrerequisite.Id;
                            return true;
                        }
                    }

                    for (int u = 0; u < prerequisite.RequiredUpgrades.Count; u++)
                    {
                        WorkshopUpgrade upgradePrerequisite =
                            prerequisite.RequiredUpgrades[u];
                        if (upgradePrerequisite == null ||
                            !workshop.IsPurchased(upgradePrerequisite))
                        {
                            string upgradeLabel = upgradePrerequisite == null
                                ? "工坊升级前置"
                                : upgradePrerequisite.Label;
                            snapshot.Blocker = "工坊改良“" + prerequisite.Label +
                                "”还缺少升级前置：“" + upgradeLabel + "”。";
                            snapshot.RecommendedAction = "打开 Workshop 页面，先完成“" +
                                upgradeLabel + "”，再购买“" + prerequisite.Label + "”。";
                            snapshot.NavigationPage = "Workshop";
                            snapshot.NavigationTargetId = upgradePrerequisite == null
                                ? string.Empty
                                : upgradePrerequisite.Id;
                            return true;
                        }
                    }
                }
                string label = prerequisite == null ? "工坊前置" : prerequisite.Label;
                snapshot.Blocker = "“" + building.Label + "”还需要工坊改良：“" +
                    label + "”。";
                snapshot.RecommendedAction = "打开 Workshop 页面，完成“" + label +
                    "”，再回到建筑页面建造“" + building.Label + "”。";
                snapshot.NavigationPage = "Workshop";
                snapshot.NavigationTargetId = prerequisite == null
                    ? string.Empty
                    : prerequisite.Id;
                return true;
            }
        }
        return false;
    }

    private static bool ApplyResearchPrerequisiteGuidance(
        TutorialSnapshot snapshot, string researchId, ResearchManager research)
    {
        if (snapshot == null ||
            !DataBase<Research>.TryFind(researchId, out Research definition) ||
            definition == null)
            return false;

        Research prerequisite = FindFirstUncompletedResearchLeaf(
            definition, research, new HashSet<string>());
        if (prerequisite == null)
            return false;

        if (!research.CanAccessResearch(prerequisite) ||
            !research.States.TryGetValue(prerequisite,
                out ResearchState prerequisiteState) ||
            prerequisiteState == null)
            return false;

        if (prerequisiteState.Status == ResearchStatus.Researching)
        {
            snapshot.Blocker = "研究正在进行：“" + prerequisite.Label + "”。";
            snapshot.RecommendedAction = "等待当前研究完成，或打开研究页面查看进度。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = prerequisite.Id;
            return true;
        }
        if (prerequisiteState.Status == ResearchStatus.Queued)
        {
            snapshot.Blocker = "研究已进入队列：“" + prerequisite.Label + "”。";
            snapshot.RecommendedAction = "等待队列推进，不需要重复开始这项研究。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = prerequisite.Id;
            return true;
        }
        string researchBlocker = string.Empty;
        if (prerequisiteState.Status == ResearchStatus.WaitingResources ||
            !research.CanPayResearchCost(prerequisite, out researchBlocker))
        {
            snapshot.Blocker = string.IsNullOrEmpty(researchBlocker)
                ? "研究资源尚未准备好。"
                : researchBlocker;
            snapshot.RecommendedAction = "打开资源页面，补齐研究资源后再继续“" +
                prerequisite.Label + "”。";
            snapshot.NavigationPage = "Resources";
            Resource missingResource = FindMissingResearchResource(research, prerequisite);
            snapshot.NavigationTargetId = missingResource == null
                ? string.Empty
                : missingResource.Id;
            return true;
        }
        if (prerequisite == definition)
        {
            snapshot.Blocker = "研究前置已经满足：“" + prerequisite.Label + "”可以开始。";
            snapshot.RecommendedAction = "打开研究页面，开始“" + prerequisite.Label + "”。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = prerequisite.Id;
            return true;
        }

        snapshot.Blocker = "“" + definition.Label + "”还缺少研究前置：“" +
            prerequisite.Label + "”。";
        snapshot.RecommendedAction = "打开研究页面，先完成“" + prerequisite.Label +
            "”，再继续研究“" + definition.Label + "”。";
        snapshot.NavigationPage = "Research";
        snapshot.NavigationTargetId = prerequisite.Id;
        return true;
    }

    private static Resource FindMissingResearchResource(
        ResearchManager research, Research definition)
    {
        if (research == null || definition == null ||
            !research.States.TryGetValue(definition, out ResearchState state) ||
            state == null)
            return null;

        ResourceManager resources = FindObjectOfType<ResourceManager>();
        if (resources == null)
            return null;

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements =
            definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;
            ExpantaNum remaining = ExpantaNum.Max(
                ExpantaNum.Zero,
                requirement.Second - state.GetPaidResourceCost(requirement.First));
            if (remaining > ExpantaNum.Zero &&
                resources.GetAmount(requirement.First) < remaining)
                return requirement.First;
        }
        return null;
    }

    private static Research FindFirstUncompletedResearchLeaf(
        Research target, ResearchManager research, HashSet<string> visiting)
    {
        if (target == null || research == null ||
            research.IsResearchCompleted(target.Id))
            return null;
        if (!visiting.Add(target.Id))
            return target;

        for (int i = 0; i < target.Prerequisites.Count; i++)
        {
            Research prerequisite = target.Prerequisites[i];
            if (prerequisite == null)
                throw new InvalidOperationException(
                    "Research definition contains a null prerequisite: " + target.Id);
            if (research.IsResearchCompleted(prerequisite.Id))
                continue;

            Research leaf = FindFirstUncompletedResearchLeaf(
                prerequisite, research, visiting);
            visiting.Remove(target.Id);
            return leaf ?? prerequisite;
        }

        visiting.Remove(target.Id);
        return target;
    }

    private static bool ApplyIndustrialBuildingGuidance(
        TutorialSnapshot snapshot, string buildingId, GameManager game,
        ResourceManager resources, BuildingManager buildings)
    {
        if (snapshot == null ||
            !DataBase<Building>.TryFind(buildingId, out Building building) ||
            building == null)
            return false;

        snapshot.Blocker = DescribeBuildingBlocker(
            building, game, buildings, resources);
        snapshot.RecommendedAction = "打开建筑页面，建造“" + building.Label +
            "”，然后观察它对工业生产的真实影响。";
        snapshot.NavigationPage = "Buildings";
        snapshot.NavigationTargetId = building.Id;
        return true;
    }

    private static string GetResearchLabel(string id)
    {
        return DataBase<Research>.TryFind(id, out Research definition) &&
            definition != null && !string.IsNullOrWhiteSpace(definition.Label)
            ? definition.Label
            : id;
    }

    private static string GetBuildingLabel(string id)
    {
        return DataBase<Building>.TryFind(id, out Building definition) &&
            definition != null && !string.IsNullOrWhiteSpace(definition.Label)
            ? definition.Label
            : id;
    }

    private static bool HasOwnedBuildingId(BuildingManager buildings, string id)
    {
        if (buildings == null || !DataBase<Building>.TryFind(id, out Building definition) ||
            !buildings.States.TryGetValue(definition, out BuildingState state))
            return false;
        return state != null && state.Amount > ExpantaNum.Zero;
    }

    private static string BuildOwnedProductionChainSummary(BuildingManager buildings)
    {
        if (!TryFindOwnedProductionChain(buildings, out Resource consumed, out Resource generated))
            return string.Empty;
        return consumed.Label + " → " + generated.Label;
    }

    private static Building FindBuildingRecommendation(
        GameManager game, BuildingManager buildings, ResearchManager research)
    {
        if (game == null || buildings == null)
            return null;

        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        Building fallback = null;
        for (int i = 0; i < definitions.Count; i++)
        {
            Building building = definitions[i];
            if (building == null || !buildings.ShouldDisplay(building))
                continue;
            if (fallback == null && HasGuidanceEffect(building))
                fallback = building;
            if (game.State.Population.PopulationCapacity <= ExpantaNum.Zero &&
                building.PopulationCapacityGranted > ExpantaNum.Zero)
                return building;
            if (game.State.FoodNetRate <= ExpantaNum.Zero &&
                building.FoodProductionRate > ExpantaNum.Zero)
                return building;
            if (research != null && research.ResearchPower <= ExpantaNum.Zero &&
                building.ResearchPowerGranted > ExpantaNum.Zero)
                return building;
        }
        return fallback;
    }

#if UNITY_EDITOR
    public static string GetPopulationNavigationTargetForEditor(
        GameManager game, BuildingManager buildings) =>
        GetPopulationNavigationTarget(game, buildings);
#endif
    private static string GetPopulationNavigationTarget(GameManager game,
        BuildingManager buildings)
    {
        if (game == null || buildings == null)
            return string.Empty;
        if (game.State != null && game.State.Population != null &&
            game.State.Population.PopulationCapacity > ExpantaNum.Zero &&
            (game.State.FoodNetRate <= ExpantaNum.Zero ||
             game.State.HappinessMultiplier < ExpantaNum.One))
            return "Food";
        Building recommendation = FindPopulationCapacityRecommendation(game, buildings);
        return recommendation == null ? string.Empty : recommendation.Id;
    }

    private static void BuildPopulationCapacityGuidance(
        TutorialSnapshot snapshot, string defaultBlocker, GameManager game,
        ResourceManager resources, BuildingManager buildings,
        ResearchManager research)
    {
        snapshot.NavigationPage = "Buildings";
        Building recommendation = FindPopulationCapacityRecommendation(game, buildings);
        if (recommendation != null)
        {
            snapshot.Blocker = defaultBlocker;
            snapshot.RecommendedAction = "打开建筑页面，查看人口容量效果：容量即时增加，居民入住后才增加生产力；每位居民基础耗粮0.8/秒，先确认持续供粮。";
            snapshot.NavigationTargetId = recommendation.Id;
            return;
        }

        Building blocker = FindPopulationCapacityBlocker(game, buildings);
        if (blocker != null && ApplyBuildingPrerequisiteGuidance(
                snapshot, blocker.Id, research))
            return;

        snapshot.Blocker = blocker == null
            ? defaultBlocker + "当前没有可用的人口容量建筑定义。"
            : DescribeBuildingBlocker(blocker, game, buildings, resources);
        snapshot.RecommendedAction = blocker == null
            ? "打开建筑页面，确认当前时代的住房解锁条件。"
            : "打开建筑页面，查看“" + blocker.Label + "”的真实解锁条件。";
        snapshot.NavigationTargetId = blocker == null ? string.Empty : blocker.Id;
    }

    private static Building FindPopulationCapacityBlocker(
        GameManager game, BuildingManager buildings)
    {
        if (game == null || buildings == null)
            return null;

        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        Building futureBlocker = null;
        for (int i = 0; i < definitions.Count; i++)
        {
            Building building = definitions[i];
            if (building == null || HasOwnedBuildingId(buildings, building.Id) ||
                building.PopulationCapacityGranted <= ExpantaNum.Zero)
                continue;
            if (IsCurrentEraBuilding(game, building))
                return building;
            if (futureBlocker == null)
                futureBlocker = building;
        }
        return futureBlocker;
    }

    private static Building FindPopulationCapacityRecommendation(GameManager game,
        BuildingManager buildings)
    {
        if (game == null || buildings == null)
            return null;

        if (game.State.TechLevel == TechLevel.Animal &&
            DataBase<Building>.TryFind("WoodHouse", out Building woodHouse) &&
            !HasOwnedBuildingId(buildings, woodHouse.Id) &&
            woodHouse.PopulationCapacityGranted > ExpantaNum.Zero &&
            buildings.ShouldDisplay(woodHouse))
            return woodHouse;

        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Building building = definitions[i];
            if (building != null && !HasOwnedBuildingId(buildings, building.Id) &&
                building.PopulationCapacityGranted > ExpantaNum.Zero &&
                buildings.ShouldDisplay(building))
                return building;
        }
        return null;
    }

    private static bool HasGuidanceEffect(Building building) =>
        building.PopulationCapacityGranted > ExpantaNum.Zero ||
        building.FoodProductionRate > ExpantaNum.Zero ||
        building.ResearchPowerGranted > ExpantaNum.Zero ||
        building.ResourceGenerationRates.Count > 0 ||
        building.ResourceConsumptionRates.Count > 0;

    private static Building FindProductionChainRecommendation(GameManager game,
        BuildingManager buildings)
    {
        if (game == null || buildings == null)
            return null;

        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        foreach (BuildingState sourceState in buildings.States.Values)
        {
            if (sourceState == null || sourceState.Amount <= ExpantaNum.Zero ||
                sourceState.Definition == null)
                continue;
            IReadOnlyList<Pair<Resource, ExpantaNum>> outputs =
                sourceState.Definition.ResourceGenerationRates;
            for (int outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
            {
                Resource output = outputs[outputIndex].First;
                if (output == null || outputs[outputIndex].Second <= ExpantaNum.Zero)
                    continue;
                for (int i = 0; i < definitions.Count; i++)
                {
                    Building candidate = definitions[i];
                    if (candidate != null && !HasOwnedBuildingId(buildings, candidate.Id) &&
                        IsCurrentEraBuilding(game, candidate) &&
                        BuildingConsumes(candidate, output))
                        return candidate;
                }
            }
        }

        foreach (BuildingState consumerState in buildings.States.Values)
        {
            if (consumerState == null || consumerState.Amount <= ExpantaNum.Zero ||
                consumerState.Definition == null)
                continue;
            IReadOnlyList<Pair<Resource, ExpantaNum>> inputs =
                consumerState.Definition.ResourceConsumptionRates;
            for (int inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
            {
                Resource input = inputs[inputIndex].First;
                if (input == null || inputs[inputIndex].Second <= ExpantaNum.Zero)
                    continue;
                for (int i = 0; i < definitions.Count; i++)
                {
                    Building candidate = definitions[i];
                    if (candidate != null && !HasOwnedBuildingId(buildings, candidate.Id) &&
                        IsCurrentEraBuilding(game, candidate) &&
                        BuildingGenerates(candidate, input))
                        return candidate;
                }
            }
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            Building producer = definitions[i];
            if (producer == null || HasOwnedBuildingId(buildings, producer.Id) ||
                !IsCurrentEraBuilding(game, producer) ||
                producer.ResourceGenerationRates.Count == 0)
                continue;
            for (int j = 0; j < definitions.Count; j++)
            {
                Building consumer = definitions[j];
                if (consumer == null || consumer == producer ||
                    !IsCurrentEraBuilding(game, consumer))
                    continue;
                for (int outputIndex = 0;
                    outputIndex < producer.ResourceGenerationRates.Count;
                    outputIndex++)
                {
                    Resource output = producer.ResourceGenerationRates[outputIndex].First;
                    if (output != null && BuildingConsumes(consumer, output))
                        return producer;
                }
            }
        }
        return null;
    }

    private static bool IsCurrentEraBuilding(GameManager game, Building building)
    {
        return game != null && game.State != null && building != null &&
            building.TechLevel <= game.State.TechLevel;
    }

    private static bool TryFindNextProductionChainAction(
        Building chainRecommendation, ResourceManager resources, BuildingManager buildings,
        ResearchManager research, out Building buildingTarget,
        out Research researchTarget, out Resource resourceTarget,
        out WorkshopUpgrade workshopTarget)
    {
        buildingTarget = chainRecommendation;
        researchTarget = null;
        resourceTarget = null;
        workshopTarget = null;
        if (buildingTarget != null)
        {
            if (TryFindBuildingResearchBlocker(
                buildingTarget, research, out researchTarget))
                return true;
            if (TryFindBuildingWorkshopBlocker(
                buildingTarget, research, resources,
                out researchTarget, out resourceTarget, out workshopTarget))
                return true;
            resourceTarget = FindMissingBuildingResource(
                buildingTarget, buildings, resources);
            return true;
        }
        return false;
    }

    private static bool TryFindBuildingResearchBlocker(
        Building building, ResearchManager research, out Research target)
    {
        target = null;
        if (building == null || research == null)
            return false;
        for (int i = 0; i < building.RequiredResearch.Count; i++)
        {
            Research required = building.RequiredResearch[i];
            if (required == null || research.IsResearchCompleted(required.Id))
                continue;
            target = FindFirstUncompletedResearchLeaf(
                required, research, new HashSet<string>()) ?? required;
            return target != null;
        }
        return false;
    }

    private static bool TryFindBuildingWorkshopBlocker(
        Building building, ResearchManager research, ResourceManager resources,
        out Research researchTarget, out Resource resourceTarget,
        out WorkshopUpgrade workshopTarget)
    {
        researchTarget = null;
        resourceTarget = null;
        workshopTarget = null;
        WorkshopManager workshop = FindObjectOfType<WorkshopManager>();
        if (building == null)
            return false;

        for (int i = 0; i < building.RequiredWorkshopUpgrades.Count; i++)
        {
            WorkshopUpgrade required = building.RequiredWorkshopUpgrades[i];
            if (required == null || workshop != null && workshop.IsPurchased(required))
                continue;
            if (TryFindWorkshopResearchBlocker(
                required, research, out researchTarget))
                return true;
            if (TryFindWorkshopUpgradeBlocker(
                required, workshop, research, out researchTarget, out workshopTarget))
                return true;
            resourceTarget = FindMissingWorkshopResource(required, resources);
            workshopTarget = required;
            return true;
        }
        return false;
    }

    private static bool TryFindWorkshopResearchBlocker(
        WorkshopUpgrade upgrade, ResearchManager research, out Research target)
    {
        target = null;
        if (upgrade == null || research == null)
            return false;
        for (int i = 0; i < upgrade.RequiredResearch.Count; i++)
        {
            Research required = upgrade.RequiredResearch[i];
            if (required == null || research.IsResearchCompleted(required.Id))
                continue;
            target = FindFirstUncompletedResearchLeaf(
                required, research, new HashSet<string>()) ?? required;
            return target != null;
        }
        return false;
    }

    private static bool TryFindWorkshopUpgradeBlocker(
        WorkshopUpgrade upgrade, WorkshopManager workshop,
        ResearchManager research, out Research researchTarget,
        out WorkshopUpgrade workshopTarget)
    {
        researchTarget = null;
        workshopTarget = null;
        if (upgrade == null || workshop == null)
            return false;
        for (int i = 0; i < upgrade.RequiredUpgrades.Count; i++)
        {
            WorkshopUpgrade required = upgrade.RequiredUpgrades[i];
            if (required == null || workshop.IsPurchased(required))
                continue;
            if (TryFindWorkshopResearchBlocker(
                required, research, out researchTarget))
                return true;
            workshopTarget = required;
            return true;
        }
        return false;
    }

    private static Resource FindMissingWorkshopResource(
        WorkshopUpgrade upgrade, ResourceManager resources)
    {
        if (upgrade == null || resources == null)
            return null;
        for (int i = 0; i < upgrade.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = upgrade.ResourceRequirements[i];
            if (requirement.First != null && requirement.Second > ExpantaNum.Zero &&
                resources.GetAmount(requirement.First) < requirement.Second)
                return requirement.First;
        }
        return null;
    }

    private static bool IsProductionChainEndpoint(
        Building candidate, IReadOnlyList<Building> definitions)
    {
        if (candidate.ResourceGenerationRates.Count == 0 &&
            candidate.ResourceConsumptionRates.Count == 0)
            return false;
        for (int i = 0; i < definitions.Count; i++)
        {
            Building other = definitions[i];
            if (other == null || other == candidate)
                continue;
            for (int outputIndex = 0;
                outputIndex < candidate.ResourceGenerationRates.Count; outputIndex++)
            {
                Resource output = candidate.ResourceGenerationRates[outputIndex].First;
                if (output != null && BuildingConsumes(other, output))
                    return true;
            }
            for (int inputIndex = 0;
                inputIndex < candidate.ResourceConsumptionRates.Count; inputIndex++)
            {
                Resource input = candidate.ResourceConsumptionRates[inputIndex].First;
                if (input != null && BuildingGenerates(other, input))
                    return true;
            }
        }
        return false;
    }

    private static Resource FindMissingBuildingResource(
        Building building, BuildingManager buildings, ResourceManager resources)
    {
        if (building == null || buildings == null || resources == null)
            return null;
        ExpantaNum owned = buildings.States.TryGetValue(
            building, out BuildingState state) && state != null
            ? state.Amount
            : ExpantaNum.Zero;
        ExpantaNum multiplier = BuildingManager.GetConstructionCostMultiplier(building);
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = building.ResourceRequirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;
            ExpantaNum cost = requirement.Second.GeometricSeriesCost(
                building.CostGrowth, owned, ExpantaNum.One) * multiplier;
            if (resources.GetAmount(requirement.First) < cost)
                return requirement.First;
        }
        return null;
    }

    private static bool BuildingConsumes(Building building, Resource resource)
    {
        IReadOnlyList<Pair<Resource, ExpantaNum>> inputs = building.ResourceConsumptionRates;
        for (int i = 0; i < inputs.Count; i++)
            if (inputs[i].First == resource && inputs[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static bool BuildingGenerates(Building building, Resource resource)
    {
        IReadOnlyList<Pair<Resource, ExpantaNum>> outputs = building.ResourceGenerationRates;
        for (int i = 0; i < outputs.Count; i++)
            if (outputs[i].First == resource && outputs[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    public static string DescribeBuildingRole(Building building)
    {
        if (building == null)
            return "提供新的发展能力。";

        List<string> changes = new List<string>();
        AddPositiveChange(changes, building.PopulationCapacityGranted,
            "人口容量 +" + building.PopulationCapacityGranted.ToGameString());
        AddPositiveChange(changes, building.FoodProductionRate,
            "食物 +" + building.FoodProductionRate.ToGameString() + "/s");
        AddPositiveChange(changes, building.ResearchPowerGranted,
            "研究力 +" + building.ResearchPowerGranted.ToGameString() + "/s");
        AddRateChanges(changes, "电力", building.PowerProductionRate,
            building.PowerConsumptionRate);
        AddRateChanges(changes, "物流", building.LogisticsProductionRate,
            building.LogisticsConsumptionRate);
        AddResourceFlowChanges(changes, building.ResourceConsumptionRates,
            "消耗 ");
        AddResourceFlowChanges(changes, building.ResourceGenerationRates,
            "产出 ");

        if (changes.Count == 0)
            return "提供新的发展能力。";
        return string.Join("；", changes) + "。";
    }

    private static void AddPositiveChange(List<string> changes,
        ExpantaNum amount, string text)
    {
        if (amount > ExpantaNum.Zero && changes.Count < 5)
            changes.Add(text);
    }

    private static void AddRateChanges(List<string> changes, string label,
        ExpantaNum production, ExpantaNum consumption)
    {
        if (production > ExpantaNum.Zero && changes.Count < 5)
            changes.Add(label + " +" + production.ToGameString() + "/s");
        if (consumption > ExpantaNum.Zero && changes.Count < 5)
            changes.Add(label + " -" + consumption.ToGameString() + "/s");
    }

    private static void AddResourceFlowChanges(List<string> changes,
        IReadOnlyList<Pair<Resource, ExpantaNum>> rates, string prefix)
    {
        if (rates == null)
            return;
        for (int i = 0; i < rates.Count && changes.Count < 5; i++)
        {
            Pair<Resource, ExpantaNum> rate = rates[i];
            if (rate.First == null || rate.Second <= ExpantaNum.Zero)
                continue;
            changes.Add(prefix + rate.First.Label + " " + rate.Second.ToGameString() + "/s");
        }
    }

    private static string DescribeBuildingBlocker(
        Building building,
        GameManager game,
        BuildingManager buildings,
        ResourceManager resources)
    {
        if (building == null || game == null || buildings == null ||
            resources == null)
            return "当前建筑条件尚未满足。";

        ExpantaNum owned = buildings.States.TryGetValue(
            building, out BuildingState state)
            ? state.Amount
            : ExpantaNum.Zero;
        string action = (owned > ExpantaNum.Zero ? "再建一座“" : "建造“") +
            building.Label + "”还缺";
        ExpantaNum territoryMissing = ExpantaNum.Max(
            ExpantaNum.Zero,
            building.SpaceCost - game.State.AvailableTerritory);
        if (territoryMissing > ExpantaNum.Zero)
            return action + "领土 " + territoryMissing.ToGameString() + "。";

        ExpantaNum productivityMissing = ExpantaNum.Max(
            ExpantaNum.Zero,
            building.ProductivityConsumption - buildings.AvailableProductivity);
        if (productivityMissing > ExpantaNum.Zero)
            return action + "生产力 " + productivityMissing.ToGameString() + "。";

        ExpantaNum costMultiplier =
            BuildingManager.GetConstructionCostMultiplier(building);
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement =
                building.ResourceRequirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;

            ExpantaNum cost = requirement.Second.GeometricSeriesCost(
                building.CostGrowth, owned, ExpantaNum.One) * costMultiplier;
            resources.States.TryGetValue(
                requirement.First, out ResourceState resourceState);
            ExpantaNum available = resourceState == null
                ? ExpantaNum.Zero
                : resourceState.Amount;
            ExpantaNum missing = ExpantaNum.Max(
                ExpantaNum.Zero,
                cost - available);
            if (missing <= ExpantaNum.Zero)
                continue;

            ExpantaNum netRate = resourceState == null
                ? ExpantaNum.Zero
                : ResourceManager.ApplyCurrentProductionReward(
                    resourceState.ProductionRate) - resourceState.ConsumptionRate;
            string prefix = action + requirement.First.Label + " " +
                missing.ToGameString();
            return netRate > ExpantaNum.Zero
                ? prefix + "，按当前净产出约 " +
                    (missing / netRate).ToGameString() + " 秒。"
                : prefix + "，当前没有正净产出。";
        }

        return "“" + building.Label +
            "”的领土、生产力和资源已经满足；可以前往建筑页面建造。";
    }

    private static Research FindResearchGuidance(
        ResearchManager researchManager, TechLevel currentEra)
    {
        if (researchManager == null)
            return null;
        if (researchManager.ActiveResearch != null)
            return researchManager.ActiveResearch.Definition;

        // Prefer the real next-era path over database order. The recursive
        // leaf is the first research the player can meaningfully pursue;
        // research rules and queue behavior remain owned by ResearchManager.
        if ((int)currentEra < (int)TechLevel.Archotech)
        {
            Research transition = EraGoalEvaluator.FindTransition(
                (TechLevel)((int)currentEra + 1));
            Research leaf = FindFirstUncompletedResearchLeaf(
                transition, researchManager, new HashSet<string>());
            if (leaf != null)
                return leaf;
        }

        IReadOnlyList<Research> definitions = DataBase<Research>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research definition = definitions[i];
            if (definition != null &&
                researchManager.States.TryGetValue(definition, out ResearchState state) &&
                state != null && state.Status == ResearchStatus.Available)
                return definition;
        }
        return null;
    }

    public static string DescribeResearchRole(Research research)
    {
        if (research == null)
            return "它连接着后续发展路径，值得在研究页面查看详情。";

        List<string> changes = new List<string>();
        if (research.AdvancesTechLevel)
            changes.Add("推进至" + research.TechLevel.GetDescription());
        for (int i = 0; i < research.Effects.Count && changes.Count < 4; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect == null)
                continue;
            string target = effect.Building != null
                ? effect.Building.Label
                : effect.Resource != null ? effect.Resource.Label : string.Empty;
            changes.Add(string.IsNullOrEmpty(target)
                ? effect.Type.GetDescription()
                : target + "：" + effect.Type.GetDescription());
        }
        {
            IReadOnlyList<Building> definitions = DataBase<Building>.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                Building building = definitions[i];
                if (building == null || changes.Count >= 5)
                    continue;
                for (int j = 0; j < building.RequiredResearch.Count; j++)
                    if (building.RequiredResearch[j] == research)
                    {
                        changes.Add("解锁" + building.Label);
                        break;
                    }
                if (changes.Count >= 5)
                    break;
            }
        }
        if (changes.Count == 0)
            return "它连接着后续发展路径，值得在研究页面查看详情。";
        return "改变：" + string.Join("；", changes) +
            (research.Effects.Count > 3 ? "；等" : string.Empty) + "。";
    }

    public static bool HasObservedPopulationGrowth(GameState gameState) =>
        gameState != null && gameState.Population != null &&
        gameState.Population.Population > ExpantaNum.Zero;

    public static bool HasCompletedResearch(IEnumerable<ResearchState> states)
    {
        if (states == null)
            return false;
        foreach (ResearchState state in states)
            if (state != null && state.Status == ResearchStatus.Completed)
                return true;
        return false;
    }

    private static bool HasOwnedProductionChain(BuildingManager buildings) =>
        buildings != null && TryFindOwnedProductionChain(buildings.States.Values, out _, out _, true);

    public static bool HasOwnedProductionChain(IEnumerable<BuildingState> states) =>
        TryFindOwnedProductionChain(states, out _, out _);

    private static bool TryFindOwnedProductionChain(
        BuildingManager buildings, out Resource consumed, out Resource generated)
    {
        consumed = null;
        generated = null;
        return buildings != null &&
            TryFindOwnedProductionChain(buildings.States.Values, out consumed, out generated, true);
    }

#if UNITY_EDITOR
    public static bool TryFindOwnedProductionChainForEditor(
        IEnumerable<BuildingState> states, out Resource consumed, out Resource generated) =>
        TryFindOwnedProductionChain(states, out consumed, out generated);
#endif
    private static bool TryFindOwnedProductionChain(
        IEnumerable<BuildingState> states, out Resource consumed, out Resource generated,
        bool requireProducedStock = false)
    {
        consumed = null;
        generated = null;
        if (states == null)
            return false;

        foreach (BuildingState source in states)
        {
            if (source == null || source.Amount <= ExpantaNum.Zero || source.Efficiency <= ExpantaNum.Zero || source.Definition == null)
                continue;
            IReadOnlyList<Pair<Resource, ExpantaNum>> outputs =
                source.Definition.ResourceGenerationRates;
            for (int outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
            {
                Pair<Resource, ExpantaNum> output = outputs[outputIndex];
                if (output.First == null || output.Second <= ExpantaNum.Zero)
                    continue;

                foreach (BuildingState target in states)
                {
                    if (target == null || target == source ||
                        target.Amount <= ExpantaNum.Zero || target.Efficiency <= ExpantaNum.Zero || target.Definition == null)
                        continue;
                    IReadOnlyList<Pair<Resource, ExpantaNum>> inputs =
                        target.Definition.ResourceConsumptionRates;
                    for (int inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
                    {
                        Pair<Resource, ExpantaNum> input = inputs[inputIndex];
                        if (input.First == output.First && input.Second > ExpantaNum.Zero)
                        {
                            Resource product = FindFirstResource(target.Definition.ResourceGenerationRates);
                            if (product == null || requireProducedStock &&
                                ResourceManager.Instance.GetAmount(product) <= ExpantaNum.Zero)
                                continue;
                            consumed = output.First;
                            generated = product;
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private static Resource FindFirstResource(
        IReadOnlyList<Pair<Resource, ExpantaNum>> rates)
    {
        if (rates == null)
            return null;
        for (int i = 0; i < rates.Count; i++)
        {
            Pair<Resource, ExpantaNum> rate = rates[i];
            if (rate.First != null && rate.Second > ExpantaNum.Zero)
                return rate.First;
        }
        return null;
    }

    private static string BuildNextEraGoal(GameManager game,
        ResearchManager researchManager, EraGoalEvaluation eraGoal)
    {
        if (eraGoal.Transition == null)
            return "当前内容已接近终点。";
        string targetPrefix = "下一时代（" + eraGoal.TargetEra.GetDescription() + "）：";
        string readiness = BuildEraReadinessSummary(game, researchManager);
        readiness = AppendProductionReadiness(
            readiness, BuildingManager.Instance);
        for (int i = 0; i < eraGoal.Conditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = eraGoal.Conditions[i];
            if (condition.Met)
                continue;
            if (condition.Kind == EraGoalConditionKind.PrerequisiteResearch)
                return targetPrefix + "需要完成关键研究：" + condition.Research.Label +
                    "\n" + readiness;
            return targetPrefix + "需要资源：" + condition.Resource.Label + " 剩余 " +
                condition.RemainingAmount.ToGameString() + "，当前可用 " +
                condition.AvailableAmount.ToGameString() + "\n" + readiness;
        }
        return targetPrefix + "时代研究条件已满足，可以推进：" + eraGoal.Transition.Label +
            "\n" + readiness;
    }

    private static string AppendProductionReadiness(
        string readiness, BuildingManager buildings)
    {
        bool hasProductionChain = buildings != null &&
            HasOwnedProductionChain(buildings.States.Values);
        return (readiness ?? string.Empty) + "\n" +
            "\u57fa\u7840\u4ea7\u4e1a\u51c6\u5907\uff1a" +
            (hasProductionChain
                ? "\u5df2\u5f62\u6210\u771f\u5b9e\u7684\u539f\u6599\u2192\u52a0\u5de5\u2192\u4ea7\u51fa\u94fe\u3002"
                : "\u5c1a\u672a\u5f62\u6210\u539f\u6599\u2192\u52a0\u5de5\u2192\u4ea7\u51fa\u94fe\uff08\u53d1\u5c55\u51c6\u5907\uff0c\u4e0d\u6539\u53d8\u65f6\u4ee3\u89c4\u5219\uff09\u3002");
    }

    private static string BuildEraReadinessSummary(GameManager game,
        ResearchManager researchManager)
    {
        if (game == null || game.State == null || game.State.Population == null)
            return "当前准备度：正在读取人口、生产力与研究力。";

        BuildingManager buildings = BuildingManager.Instance;
        string productivity = buildings == null
            ? "未知"
            : buildings.UsedProductivity.ToGameString() + "/" +
              buildings.TotalProductivity.ToGameString();
        string researchPower = researchManager == null
            ? "未知"
            : researchManager.ResearchPower.ToGameString() + "/s";
        return "当前准备度：人口 " + game.State.Population.Population.ToGameString() +
            "/" + game.State.Population.PopulationCapacity.ToGameString() +
            "；生产力 " + productivity + "；研究力 " + researchPower +
            "。这些是发展准备信息，不会额外改变时代规则。";
    }

    private TutorialStep FindStep(string id)
    {
        for (int i = 0; i < steps.Count; i++)
            if (steps[i].Id == id)
                return steps[i];
        return null;
    }

#if UNITY_EDITOR
    public TutorialStep FindPreviousCompletedStepForEditor(string stepId) =>
        FindPreviousCompletedStep(stepId);
#endif
    private TutorialStep FindPreviousCompletedStep(string stepId)
    {
        for (int i = 0; i < steps.Count; i++)
            if (steps[i].NextStepId == stepId &&
                completedStepIds.Contains(steps[i].Id))
                return steps[i];
        return null;
    }

    private void EnsureDefinitions()
    {
        if (steps.Count == 0)
            BuildDefaultSteps();
    }

    public SaveManager.TutorialSaveData CaptureSaveData()
    {
        EnsureDefinitions();
        return new SaveManager.TutorialSaveData
        {
            ActiveStepId = activeStepId,
            CompletedStepIds = new List<string>(completedStepIds)
        };
    }

    public void RestoreSaveData(SaveManager.TutorialSaveData data, TechLevel currentEra)
    {
        EnsureDefinitions();
        completedStepIds.Clear();
        visitedStepId = null;
        visitedDetailStepId = null;
        visitedDetailTargetId = null;
        pendingCompletionFeedback = false;
        if (data != null && data.CompletedStepIds != null)
            for (int i = 0; i < data.CompletedStepIds.Count; i++)
                if (FindStep(data.CompletedStepIds[i]) != null)
                    completedStepIds.Add(data.CompletedStepIds[i]);
        activeStepId = FindStep(data == null ? string.Empty : data.ActiveStepId)?.Id;
        if (string.IsNullOrEmpty(activeStepId))
            activeStepId = currentEra == TechLevel.Animal
                ? FindRootStepId()
                : FindEraGoalOrTerminalStepId();
        version++;
        SaveSessionVersion++;
    }

    private string FindEraGoalOrTerminalStepId()
    {
        for (int i = 0; i < steps.Count; i++)
            if (steps[i].Kind == TutorialStepKind.EraGoal)
                return steps[i].Id;
        for (int i = 0; i < steps.Count; i++)
            if (string.IsNullOrEmpty(steps[i].NextStepId))
                return steps[i].Id;
        return steps[0].Id;
    }

#if UNITY_EDITOR
    public void ResetForNewGameForEditor() => ResetForNewGame();
#endif
    internal void ResetForNewGame()
    {
        EnsureDefinitions();
        completedStepIds.Clear();
        visitedStepId = null;
        visitedDetailStepId = null;
        visitedDetailTargetId = null;
        pendingCompletionFeedback = false;
        activeStepId = FindRootStepId();
        version++;
        SaveSessionVersion++;
    }
}
