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
            "resource-inventory-positive", "Resources",
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
            NextEraGoal = BuildNextEraGoal(game, resources, research),
            IsComplete = step.Kind == TutorialStepKind.LongTerm
        };
        if (showCompletionFeedback && completedStep != null)
            snapshot.CompletedFeedback = BuildCompletedFeedback(
                completedStep, game, resources, buildings);
        Resource coreResource = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
        snapshot.CoreResourceText = coreResource == null
            ? "未知"
            : coreResource.Label + " " + resources.GetAmount(coreResource).ToGameString();
        BuildDetails(snapshot, step, game, resources, buildings, research);
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
            case TechLevel.Neolithic:
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

    internal void RecordPageVisited(string pageName)
    {
        EnsureDefinitions();
        TutorialStep step = FindStep(activeStepId);
        if (step != null &&
            string.Equals(GetNavigationPageForStep(step, GetCurrentGameState()), pageName,
                StringComparison.Ordinal))
            visitedStepId = step.Id;
    }

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

    private static string GetNavigationPageForStep(TutorialStep step, GameState gameState)
    {
        if (step == null)
            return string.Empty;
        if (step.Kind == TutorialStepKind.Population && gameState != null &&
            gameState.HappinessMultiplier < ExpantaNum.One)
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
        ResourceManager resources, BuildingManager buildings, ResearchManager research)
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
                ? "打开资源页面，先选择食物：优先建造能提高食物净产出的设施，避免幸福度和人口增长被饥荒拖慢。"
                : "打开资源页面，按下一步缺口做选择：黏土优先支撑陶器与定居，纤维优先支撑布料与加工；不要同时铺开三条链。";
            snapshot.NavigationPage = "Resources";
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
                game.State.HappinessMultiplier < ExpantaNum.One)
            {
                snapshot.Blocker = "食物净产出或幸福度不足，人口增长会受限。";
                snapshot.RecommendedAction = "打开资源页面，先稳定食物净产出。";
                snapshot.NavigationPage = "Resources";
            }
            else if (game.State.Population.PopulationCapacity <= ExpantaNum.Zero)
            {
                snapshot.Blocker = "需要一个能提供人口容量的建筑。";
                snapshot.RecommendedAction = "打开建筑页面，查看人口容量效果。";
                snapshot.NavigationPage = "Buildings";
            }
            else if (game.State.Population.Population >= game.State.Population.PopulationCapacity)
            {
                snapshot.Blocker = "人口已达到当前容量，需要继续扩展容量。";
                snapshot.RecommendedAction = "打开建筑页面，寻找更高人口容量。";
                snapshot.NavigationPage = "Buildings";
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
            snapshot.Blocker = buildings.AvailableProductivity <= ExpantaNum.Zero
                ? "可用生产力不足。"
                : string.IsNullOrEmpty(chainSummary)
                    ? "尚未拥有一组相连的生产与加工建筑。"
                    : chainSummary;
            snapshot.RecommendedAction = string.IsNullOrEmpty(chainSummary)
                ? "打开建筑页面，查看生产与消耗关系。"
                : "打开建筑页面，继续扩展：" + chainSummary;
            snapshot.NavigationPage = "Buildings";
        }
        else if (step.Kind == TutorialStepKind.EraGoal)
        {
            BuildEraGoalGuidance(snapshot, game, resources, research);
        }
        else if (step.Kind == TutorialStepKind.LongTerm)
        {
            if (game.State.TechLevel == TechLevel.Industrial)
                industrialMainlineActive = BuildIndustrialGuidance(
                    snapshot, game, resources, research, buildings);
            else if (game.State.TechLevel == TechLevel.Spacer)
                industrialMainlineActive = BuildSpacerGuidance(
                    snapshot, game, research, buildings);
            if (!industrialMainlineActive)
                BuildEraGoalGuidance(snapshot, game, resources, research);
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
        }
        if (!industrialMainlineActive && step.Kind != TutorialStepKind.EraGoal &&
            step.Kind != TutorialStepKind.LongTerm)
            industrialMainlineActive = BuildIndustrialGuidance(
                snapshot, game, resources, research, buildings);
        if (industrialMainlineActive)
            snapshot.NextEraGoal = "长期方向（完成当前工业主线后）：" + snapshot.NextEraGoal;

        // Authored navigation is the default for ordinary steps. Population
        // keeps its state-driven Resources/Buildings choice, while EraGoal
        // and industrial guidance must point at the real current blocker.
        if (!industrialMainlineActive &&
            step.Kind != TutorialStepKind.Population &&
            step.Kind != TutorialStepKind.EraGoal &&
            step.Kind != TutorialStepKind.LongTerm &&
            !string.IsNullOrWhiteSpace(step.NavigationPage))
            snapshot.NavigationPage = step.NavigationPage;
    }

    private static void BuildEraGoalGuidance(TutorialSnapshot snapshot,
        GameManager game, ResourceManager resources, ResearchManager research)
    {
        snapshot.RecommendedAction = "打开时代页面，查看下一时代的真实条件。";
        snapshot.NavigationPage = "Era";
        snapshot.NavigationTargetId = string.Empty;

        EraGoalEvaluation evaluation = EraGoalEvaluator.Evaluate(
            game.State.TechLevel, research, resources);
        if (evaluation.Transition == null)
        {
            snapshot.Blocker = snapshot.NextEraGoal;
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

    private static bool BuildIndustrialGuidance(
        TutorialSnapshot snapshot, GameManager game, ResourceManager resources,
        ResearchManager research, BuildingManager buildings)
    {
        if (snapshot == null || game == null || game.State == null ||
            game.State.TechLevel != TechLevel.Industrial)
            return false;

        if (!HasCompletedResearchId(research, "IndustrialWorkshop"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "IndustrialWorkshop", research))
                return true;
            string researchLabel = GetResearchLabel("IndustrialWorkshop");
            snapshot.Blocker = "工业体系还缺少可重复改良的工坊：先完成“" + researchLabel + "”。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel +
                "”，让旧工艺能够被反复验证和改进。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "IndustrialWorkshop";
            return true;
        }
        if (!HasCompletedResearchId(research, "PrecisionManufacturing"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "PrecisionManufacturing", research))
                return true;
            string researchLabel = GetResearchLabel("PrecisionManufacturing");
            snapshot.Blocker = "机器工厂还缺少精密制造知识：先完成“" + researchLabel + "”。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel +
                "”，让工坊经验能够转化为稳定的机器生产。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "PrecisionManufacturing";
            return true;
        }
        if (!HasRelevantWorkshopForBuilding(
            "MachineFactory", research, out WorkshopUpgrade workshopTarget))
        {
            snapshot.Blocker = "工业工坊已经解锁，但还没有一项真实改良。";
            snapshot.RecommendedAction = "打开 Workshop 页面，购买一项真实改良，再继续建设机器工厂。";
            snapshot.NavigationPage = "Workshop";
            snapshot.NavigationTargetId = workshopTarget == null
                ? string.Empty
                : workshopTarget.Id;
            return true;
        }
        if (!HasOwnedBuildingId(buildings, "MachineFactory"))
        {
            if (ApplyBuildingPrerequisiteGuidance(
                snapshot, "MachineFactory", research))
                return true;
            if (ApplyIndustrialBuildingGuidance(
                snapshot, "MachineFactory", game, resources, buildings))
                return true;
            string buildingLabel = GetBuildingLabel("MachineFactory");
            snapshot.Blocker = "精密制造与工坊改良已经准备，但还没有“" + buildingLabel +
                "”把规模变成现实。";
            snapshot.RecommendedAction = "打开建筑页面，建造“" + buildingLabel + "”，让工业时代第一次真正运转。";
            snapshot.NavigationPage = "Buildings";
            snapshot.NavigationTargetId = "MachineFactory";
            return true;
        }
        if (!HasCompletedResearchId(research, "FactoryOrganization"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "FactoryOrganization", research))
                return true;
            string researchLabel = GetResearchLabel("FactoryOrganization");
            snapshot.Blocker = "机器工厂已经启动，但工业规模还需要统一组织：先完成“" +
                researchLabel + "”。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel +
                "”，让工厂经验能够被制度化并复制到更大的王国。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "FactoryOrganization";
            return true;
        }
        if (!HasCompletedResearchId(research, "SteamPower"))
        {
            if (ApplyResearchPrerequisiteGuidance(snapshot, "SteamPower", research))
                return true;
            string researchLabel = GetResearchLabel("SteamPower");
            snapshot.Blocker = "工业能源尚未建立：先完成“" + researchLabel + "”。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel + "”，解锁蒸汽动力。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "SteamPower";
            return true;
        }
        if (!HasOwnedBuildingId(buildings, "SteamPlant"))
        {
            if (ApplyBuildingPrerequisiteGuidance(snapshot, "SteamPlant", research))
                return true;
            if (ApplyIndustrialBuildingGuidance(
                snapshot, "SteamPlant", game, resources, buildings))
                return true;
            string buildingLabel = GetBuildingLabel("SteamPlant");
            snapshot.Blocker = "蒸汽动力已经掌握，但还没有“" + buildingLabel + "”。";
            snapshot.RecommendedAction = "打开建筑页面，建造“" + buildingLabel + "”，让工业生产真正运转。";
            snapshot.NavigationPage = "Buildings";
            snapshot.NavigationTargetId = "SteamPlant";
            return true;
        }
        if (!HasCompletedResearchId(research, "IndustrialHabitationEngineering"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "IndustrialHabitationEngineering", research))
                return true;
            string researchLabel = GetResearchLabel("IndustrialHabitationEngineering");
            snapshot.Blocker = "工业城市即将吸纳更多人口：先完成“" + researchLabel + "”。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel + "”，理解工业人口与城市的关系。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "IndustrialHabitationEngineering";
            return true;
        }
        if (!HasOwnedBuildingId(buildings, "IndustrialHabitationComplex"))
        {
            if (ApplyBuildingPrerequisiteGuidance(
                snapshot, "IndustrialHabitationComplex", research))
                return true;
            if (ApplyIndustrialBuildingGuidance(
                snapshot, "IndustrialHabitationComplex", game, resources, buildings))
                return true;
            string buildingLabel = GetBuildingLabel("IndustrialHabitationComplex");
            snapshot.Blocker = "工业人口需要真正的居住空间：还没有“" + buildingLabel + "”。";
            snapshot.RecommendedAction = "打开建筑页面，建造“" + buildingLabel + "”，给每一代鼠族留下位置。";
            snapshot.NavigationPage = "Buildings";
            snapshot.NavigationTargetId = "IndustrialHabitationComplex";
            return true;
        }
        if (!HasCompletedResearchId(research, "RailwayEngineering"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "RailwayEngineering", research))
                return true;
            string researchLabel = GetResearchLabel("RailwayEngineering");
            snapshot.Blocker = "能源已经稳定，下一项阻碍是把原料送到工厂。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel + "”，建立铁路物流。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "RailwayEngineering";
            return true;
        }
        if (!HasOwnedBuildingId(buildings, "RailHub"))
        {
            if (ApplyBuildingPrerequisiteGuidance(snapshot, "RailHub", research))
                return true;
            if (ApplyIndustrialBuildingGuidance(
                snapshot, "RailHub", game, resources, buildings))
                return true;
            string buildingLabel = GetBuildingLabel("RailHub");
            snapshot.Blocker = "铁路技术已经掌握，但王国还没有“" + buildingLabel + "”。";
            snapshot.RecommendedAction = "打开建筑页面，建造“" + buildingLabel + "”，连接分散的生产链。";
            snapshot.NavigationPage = "Buildings";
            snapshot.NavigationTargetId = "RailHub";
            return true;
        }
        if (!HasCompletedResearchId(research, "IndustrialMetalSmelting"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "IndustrialMetalSmelting", research))
                return true;
            string researchLabel = GetResearchLabel("IndustrialMetalSmelting");
            snapshot.Blocker = "铁路已经把原料送到工厂，但还缺少稳定的标准材料。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel + "”，建立工业冶炼链。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "IndustrialMetalSmelting";
            return true;
        }
        if (!HasOwnedBuildingId(buildings, "IndustrialMetalSmelter"))
        {
            if (ApplyBuildingPrerequisiteGuidance(
                snapshot, "IndustrialMetalSmelter", research))
                return true;
            if (ApplyIndustrialBuildingGuidance(
                snapshot, "IndustrialMetalSmelter", game, resources, buildings))
                return true;
            string buildingLabel = GetBuildingLabel("IndustrialMetalSmelter");
            snapshot.Blocker = "工业冶炼技术已经掌握，但还没有“" + buildingLabel + "”把矿石变成标准材料。";
            snapshot.RecommendedAction = "打开建筑页面，建造“" + buildingLabel + "”，观察矿石如何进入工业链。";
            snapshot.NavigationPage = "Buildings";
            snapshot.NavigationTargetId = "IndustrialMetalSmelter";
            return true;
        }
        if (!HasCompletedResearchId(research, "IndustrialChemistry"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "IndustrialChemistry", research))
                return true;
            string researchLabel = GetResearchLabel("IndustrialChemistry");
            snapshot.Blocker = "物流已经连通，下一步是让旧材料承担更复杂的工业用途。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel + "”，理解化学工业的输入与风险。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "IndustrialChemistry";
            return true;
        }
        if (!HasOwnedBuildingId(buildings, "ChemicalPlant"))
        {
            if (ApplyBuildingPrerequisiteGuidance(snapshot, "ChemicalPlant", research))
                return true;
            if (ApplyIndustrialBuildingGuidance(
                snapshot, "ChemicalPlant", game, resources, buildings))
                return true;
            string buildingLabel = GetBuildingLabel("ChemicalPlant");
            snapshot.Blocker = "化学技术已经掌握，但还没有“" + buildingLabel + "”验证这条新链。";
            snapshot.RecommendedAction = "打开建筑页面，建造“" + buildingLabel + "”，观察材料如何重新组合。";
            snapshot.NavigationPage = "Buildings";
            snapshot.NavigationTargetId = "ChemicalPlant";
            return true;
        }
        if (!HasCompletedResearchId(research, "PowerGridEngineering"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "PowerGridEngineering", research))
                return true;
            string researchLabel = GetResearchLabel("PowerGridEngineering");
            snapshot.Blocker = "化工链已经开始运转，下一步是让能源成为全王国共享的基础设施。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel + "”，建立共享电网。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "PowerGridEngineering";
            return true;
        }
        if (!HasOwnedBuildingId(buildings, "CentralPowerStation"))
        {
            if (ApplyBuildingPrerequisiteGuidance(
                snapshot, "CentralPowerStation", research))
                return true;
            if (ApplyIndustrialBuildingGuidance(
                snapshot, "CentralPowerStation", game, resources, buildings))
                return true;
            string buildingLabel = GetBuildingLabel("CentralPowerStation");
            snapshot.Blocker = "电网技术已经掌握，但还没有“" + buildingLabel + "”连接整座王国。";
            snapshot.RecommendedAction = "打开建筑页面，建造“" + buildingLabel + "”，让能源穿过整座王国。";
            snapshot.NavigationPage = "Buildings";
            snapshot.NavigationTargetId = "CentralPowerStation";
            return true;
        }
        if (!HasCompletedResearchId(research, "ModernUniversity"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "ModernUniversity", research))
                return true;
            string researchLabel = GetResearchLabel("ModernUniversity");
            snapshot.Blocker = "共享电网已经建立，下一步是把工业经验保存成可复制的知识。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel + "”，让知识能够传给下一代。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "ModernUniversity";
            return true;
        }
        if (!HasOwnedBuildingId(buildings, "University"))
        {
            if (ApplyBuildingPrerequisiteGuidance(snapshot, "University", research))
                return true;
            if (ApplyIndustrialBuildingGuidance(
                snapshot, "University", game, resources, buildings))
                return true;
            string buildingLabel = GetBuildingLabel("University");
            snapshot.Blocker = "现代知识已经准备好，但王国还没有“" + buildingLabel + "”保存与传播它。";
            snapshot.RecommendedAction = "打开建筑页面，建造“" + buildingLabel + "”，让每一代人都能从过去继续起步。";
            snapshot.NavigationPage = "Buildings";
            snapshot.NavigationTargetId = "University";
            return true;
        }
        if (!HasCompletedResearchId(research, "TitaniumAlloyEngineering"))
        {
            if (ApplyResearchPrerequisiteGuidance(
                snapshot, "TitaniumAlloyEngineering", research))
                return true;
            string researchLabel = GetResearchLabel("TitaniumAlloyEngineering");
            snapshot.Blocker = "工业体系已经能够传承知识，但还没有为星际结构准备钛合金工艺。";
            snapshot.RecommendedAction = "打开研究页面，完成“" + researchLabel +
                "”，把工业材料能力连接到星际时代。";
            snapshot.NavigationPage = "Research";
            snapshot.NavigationTargetId = "TitaniumAlloyEngineering";
            return true;
        }
        return false;
    }

    private static bool BuildSpacerGuidance(
        TutorialSnapshot snapshot, GameManager game, ResearchManager research,
        BuildingManager buildings)
    {
        if (snapshot == null || game == null || game.State == null ||
            game.State.TechLevel != TechLevel.Spacer)
            return false;

        string[] researchPath =
        {
            "FirstContact",
            "DeepSpaceFleet",
            "InterstellarNavigation"
        };
        for (int i = 0; i < researchPath.Length; i++)
        {
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
            snapshot.NavigationTargetId = prerequisite.Id;
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

    private static bool HasGuidanceEffect(Building building) =>
        building.PopulationCapacityGranted > ExpantaNum.Zero ||
        building.FoodProductionRate > ExpantaNum.Zero ||
        building.ResearchPowerGranted > ExpantaNum.Zero ||
        building.ResourceGenerationRates.Count > 0 ||
        building.ResourceConsumptionRates.Count > 0;

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
        if (changes.Count == 0)
        {
            IReadOnlyList<Building> definitions = DataBase<Building>.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                Building building = definitions[i];
                if (building == null)
                    continue;
                for (int j = 0; j < building.RequiredResearch.Count; j++)
                    if (building.RequiredResearch[j] == research)
                    {
                        changes.Add("解锁" + building.Label);
                        break;
                    }
                if (changes.Count > 0)
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
        buildings != null && HasOwnedProductionChain(buildings.States.Values);

    public static bool HasOwnedProductionChain(IEnumerable<BuildingState> states) =>
        TryFindOwnedProductionChain(states, out _, out _);

    private static bool TryFindOwnedProductionChain(
        BuildingManager buildings, out Resource consumed, out Resource generated)
    {
        consumed = null;
        generated = null;
        return buildings != null &&
            TryFindOwnedProductionChain(buildings.States.Values, out consumed, out generated);
    }

    private static bool TryFindOwnedProductionChain(
        IEnumerable<BuildingState> states, out Resource consumed, out Resource generated)
    {
        consumed = null;
        generated = null;
        if (states == null)
            return false;

        foreach (BuildingState source in states)
        {
            if (source == null || source.Amount <= ExpantaNum.Zero || source.Definition == null)
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
                        target.Amount <= ExpantaNum.Zero || target.Definition == null)
                        continue;
                    IReadOnlyList<Pair<Resource, ExpantaNum>> inputs =
                        target.Definition.ResourceConsumptionRates;
                    for (int inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
                    {
                        Pair<Resource, ExpantaNum> input = inputs[inputIndex];
                        if (input.First == output.First && input.Second > ExpantaNum.Zero)
                        {
                            consumed = output.First;
                            generated = FindFirstResource(
                                target.Definition.ResourceGenerationRates) ?? output.First;
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

    private static string BuildNextEraGoal(GameManager game, ResourceManager resources,
        ResearchManager researchManager)
    {
        EraGoalEvaluation eraGoal = EraGoalEvaluator.Evaluate(
            game.State.TechLevel, researchManager, resources);
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

    internal SaveManager.TutorialSaveData CaptureSaveData()
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

    internal void ResetForNewGame()
    {
        EnsureDefinitions();
        completedStepIds.Clear();
        visitedStepId = null;
        pendingCompletionFeedback = false;
        activeStepId = FindRootStepId();
        version++;
        SaveSessionVersion++;
    }
}
