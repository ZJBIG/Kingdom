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
    public string CurrentGoal { get; internal set; } = string.Empty;
    public string GoalDescription { get; internal set; } = string.Empty;
    public string NarrativeText { get; internal set; } = string.Empty;
    public string NextEraGoal { get; internal set; } = string.Empty;
    public string Blocker { get; internal set; } = string.Empty;
    public string RecommendedAction { get; internal set; } = string.Empty;
    public string NavigationPage { get; internal set; } = "Overview";
    public bool IsComplete { get; internal set; }
}

[DisallowMultipleComponent]
public sealed class TutorialManager : MonoBehaviour
{
    private static TutorialManager instance;
    private readonly List<TutorialStep> steps = new List<TutorialStep>();
    private readonly HashSet<string> completedStepIds = new HashSet<string>();
    private string activeStepId;
    private string visitedStepId;
    private int version;
    private GameManager cachedGameManager;
    private ResourceManager cachedResourceManager;
    private BuildingManager cachedBuildingManager;
    private ResearchManager cachedResearchManager;

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
        if (game == null || resources == null || buildings == null || research == null)
            return new TutorialSnapshot { CurrentGoal = "正在读取王国状态……" };

        int previousVersion = version;
        AdvanceCompletedSteps(game, resources, buildings, research);
        TutorialStep step = FindStep(activeStepId) ?? steps[steps.Count - 1];
        TutorialStep completedStep = FindPreviousCompletedStep(step.Id);
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
        Resource coreResource = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
        snapshot.CoreResourceText = coreResource == null
            ? "未知"
            : coreResource.Label + " " + resources.GetAmount(coreResource).ToGameString();
        BuildDetails(snapshot, step, game, resources, buildings, research);
        if (version != previousVersion)
            SnapshotChanged?.Invoke(snapshot);
        return snapshot;
    }

    internal static string GetCivilizationContext(TechLevel era)
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
            string.Equals(step.NavigationPage, pageName, StringComparison.Ordinal))
            visitedStepId = step.Id;
    }

    private bool HasVisitedPageForStep(TutorialStep step) =>
        step != null &&
        string.Equals(visitedStepId, step.Id, StringComparison.Ordinal);

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
            case "population-capacity-positive":
                // Capacity is the result of the preceding building action.
                // This step is complete only after the population state has
                // actually grown (or a save already contains population).
                return HasObservedPopulationGrowth(game.State);
            case "Research":
            case "research-complete":
            case "research-started":
                return HasCompletedResearch(research.States.Values);
            case "ProductionChain":
            case "production-chain-owned":
                return HasOwnedProductionChain(buildings);
            case "EraGoal":
            case "era-reached":
                return game.State.TechLevel != TechLevel.Animal;
            default:
                return false;
        }
    }

    private void BuildDetails(TutorialSnapshot snapshot, TutorialStep step, GameManager game,
        ResourceManager resources, BuildingManager buildings, ResearchManager research)
    {
        snapshot.Blocker = "暂无阻碍";
        snapshot.RecommendedAction = "查看概览，确认当前王国状态。";
        snapshot.NavigationPage = "Overview";
        if (step.Kind == TutorialStepKind.Orientation)
        {
            snapshot.Blocker = "王国时间尚未推进满一天。";
            snapshot.RecommendedAction = "让王国时间继续推进，观察人口与核心资源的变化；等待期间也可查看资源页。";
        }
        else if (step.Kind == TutorialStepKind.Resources)
        {
            snapshot.Blocker = "核心资源尚未形成可见库存。";
            snapshot.RecommendedAction = "打开资源页面，查看原木的数量与净产出，为第一座建筑准备材料。";
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
            }
            snapshot.NavigationPage = "Buildings";
        }
        else if (step.Kind == TutorialStepKind.Population)
        {
            if (game.State.HappinessMultiplier < ExpantaNum.One)
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
            Research guidanceResearch = FindResearchGuidance(research);
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
        else if (step.Kind == TutorialStepKind.EraGoal || step.Kind == TutorialStepKind.LongTerm)
        {
            snapshot.RecommendedAction = "打开时代页面，查看下一时代的真实条件。";
            snapshot.NavigationPage = "Era";
            snapshot.Blocker = snapshot.NextEraGoal;
        }
        if (step.Kind != TutorialStepKind.LongTerm &&
            !HasVisitedPageForStep(step))
        {
            snapshot.Blocker = "尚未查看本步骤对应页面。";
            if (step.Kind == TutorialStepKind.Population)
                snapshot.RecommendedAction = "打开建筑页面，查看人口容量与人口变化。";
            snapshot.NavigationPage = step.NavigationPage;
        }
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

    private static string DescribeBuildingRole(Building building)
    {
        if (building.PopulationCapacityGranted > ExpantaNum.Zero)
            return "每座增加人口容量 " + building.PopulationCapacityGranted.ToGameString() +
                "，让更多鼠族能够留下。";
        if (building.FoodProductionRate > ExpantaNum.Zero)
            return "每座提供食物 " + building.FoodProductionRate.ToGameString() +
                "/s，为人口增长保留余力。";
        if (building.ResearchPowerGranted > ExpantaNum.Zero)
            return "每座提供研究力 " + building.ResearchPowerGranted.ToGameString() +
                "，帮助找回失落知识。";
        Resource output = FindFirstResource(building.ResourceGenerationRates);
        Resource input = FindFirstResource(building.ResourceConsumptionRates);
        if (input != null && output != null)
            return "消耗 " + input.Label + " 并产出 " + output.Label +
                "，帮助鼠族重新建立加工生产。";
        if (output != null)
            return "产出 " + output.Label + "，补足王国当前的生产来源。";
        if (input != null)
            return "消耗 " + input.Label + "，是后续生产链的一部分。";
        return "提供新的发展能力。";
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

    private static Research FindResearchGuidance(ResearchManager researchManager)
    {
        if (researchManager == null)
            return null;
        if (researchManager.ActiveResearch != null)
            return researchManager.ActiveResearch.Definition;
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

    private static string DescribeResearchRole(Research research)
    {
        if (research.AdvancesTechLevel)
            return "完成它将推进鼠族文明的下一个时代。";
        for (int i = 0; i < research.Effects.Count; i++)
        {
            ResearchEffectDefinition effect = research.Effects[i];
            if (effect == null)
                continue;
            string target = effect.Building != null
                ? effect.Building.Label
                : effect.Resource != null ? effect.Resource.Label : string.Empty;
            return string.IsNullOrEmpty(target)
                ? "它会带来“" + effect.Type.GetDescription() + "”效果，帮助恢复失落的文明能力。"
                : "它会对“" + target + "”产生“" + effect.Type.GetDescription() +
                    "”效果，帮助恢复失落的文明能力。";
        }
        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Building building = definitions[i];
            if (building == null)
                continue;
            for (int j = 0; j < building.RequiredResearch.Count; j++)
                if (building.RequiredResearch[j] == research)
                    return "它会解锁“" + building.Label +
                        "”，让鼠族重新掌握新的发展能力。";
        }
        return "它连接着后续发展路径，值得在研究页面查看详情。";
    }

    internal static bool HasObservedPopulationGrowth(GameState gameState) =>
        gameState != null && gameState.Population != null &&
        gameState.Population.Population > ExpantaNum.Zero;

    internal static bool HasCompletedResearch(IEnumerable<ResearchState> states)
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

    internal static bool HasOwnedProductionChain(IEnumerable<BuildingState> states) =>
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
        for (int i = 0; i < eraGoal.Conditions.Count; i++)
        {
            EraGoalConditionEvaluation condition = eraGoal.Conditions[i];
            if (condition.Met)
                continue;
            if (condition.Kind == EraGoalConditionKind.PrerequisiteResearch)
                return targetPrefix + "需要完成关键研究：" + condition.Research.Label;
            return targetPrefix + "需要资源：" + condition.Resource.Label + " 剩余 " +
                condition.RemainingAmount.ToGameString() + "，当前可用 " +
                condition.AvailableAmount.ToGameString();
        }
        return targetPrefix + "时代研究条件已满足，可以推进：" + eraGoal.Transition.Label;
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

    internal void RestoreSaveData(SaveManager.TutorialSaveData data, TechLevel currentEra)
    {
        EnsureDefinitions();
        completedStepIds.Clear();
        visitedStepId = null;
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
        activeStepId = FindRootStepId();
        version++;
    }
}
