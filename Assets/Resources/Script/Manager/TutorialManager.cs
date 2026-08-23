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
    private int version;

    public static TutorialManager Current => instance;
    public static TutorialManager Ensure()
    {
        if (instance != null)
            return instance;
        instance = FindObjectOfType<TutorialManager>();
        if (instance != null)
            return instance;
        GameObject host = new GameObject("TutorialManager");
        instance = host.AddComponent<TutorialManager>();
        DontDestroyOnLoad(host);
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
            "先观察王国的时代、人口和核心资源。资源会持续变化，所有发展都从这里开始。",
            TutorialStepKind.Orientation, "resources"));
        steps.Add(new TutorialStep("resources", "理解资源来源",
            "查看木材等核心资源的数量和净产出，确认王国已经拥有可持续的资源来源。",
            TutorialStepKind.Resources, "building"));
        steps.Add(new TutorialStep("building", "让建筑解决问题",
            "建造一个真实可用的建筑，观察它如何改变生产、研究力、人口容量或其他能力。",
            TutorialStepKind.Building, "population"));
        steps.Add(new TutorialStep("population", "发展人口",
            "人口需要人口容量、食物和稳定的幸福度。查看人口变化，理解人口如何转化为生产力。",
            TutorialStepKind.Population, "research"));
        steps.Add(new TutorialStep("research", "用研究打开下一步",
            "选择一项可用研究，查看它连接的建筑、生产链或时代目标。",
            TutorialStepKind.Research, "production-chain"));
        steps.Add(new TutorialStep("production-chain", "连接生产链",
            "观察原材料、加工资源和高级建筑之间的关系，优先解决当前真正的资源阻碍。",
            TutorialStepKind.ProductionChain, "era-goal"));
        steps.Add(new TutorialStep("era-goal", "看懂下一时代",
            "时代推进由关键研究和真实条件共同决定。查看每个条件的当前值、阻碍和入口。",
            TutorialStepKind.EraGoal, "long-term"));
        steps.Add(new TutorialStep("long-term", "形成长期目标",
            "继续完成当前时代目标，并让生产链、人口和研究共同支持王国的下一次扩张。",
            TutorialStepKind.LongTerm, string.Empty));
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
        GameManager game = FindObjectOfType<GameManager>();
        ResourceManager resources = FindObjectOfType<ResourceManager>();
        BuildingManager buildings = FindObjectOfType<BuildingManager>();
        ResearchManager research = FindObjectOfType<ResearchManager>();
        if (game == null || resources == null || buildings == null || research == null)
            return new TutorialSnapshot { CurrentGoal = "正在读取王国状态……" };

        int previousVersion = version;
        AdvanceCompletedSteps(game, resources, buildings, research);
        TutorialStep step = FindStep(activeStepId) ?? steps[steps.Count - 1];
        TutorialSnapshot snapshot = new TutorialSnapshot
        {
            StepId = step.Id,
            CurrentEra = game.State.TechLevel.GetDescription(),
            CivilizationContext = GetCivilizationContext(game.State.TechLevel),
            PopulationText = game.State.Population.Population.ToGameString() + "/" + game.State.Population.PopulationCapacity.ToGameString(),
            FoodText = game.State.FoodAmount.ToGameString() + "/" + game.State.FoodCapacity.ToGameString(),
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
                return "鼠族只保住了最初的火种，复兴从稳定生存开始。";
            case TechLevel.Neolithic:
                return "鼠族重新定居，开始把零散族群组织成文明。";
            case TechLevel.Medieval:
                return "鼠族正在恢复制度与秩序，让更大的社会得以延续。";
            case TechLevel.Industrial:
                return "鼠族重新掌握规模化生产，文明复兴进入加速阶段。";
            case TechLevel.Spacer:
                return "鼠族已经走出母星，开始把复兴带向星际。";
            case TechLevel.Ultra:
                return "鼠族正在探索超越旧文明边界的力量。";
            case TechLevel.Archotech:
                return "鼠族文明开始触及远古技术留下的更深层秘密。";
            default:
                return "鼠族文明正在重新寻找自己的未来。";
        }
    }

    private void AdvanceCompletedSteps(GameManager game, ResourceManager resources,
        BuildingManager buildings, ResearchManager research)
    {
        TutorialStep step = FindStep(activeStepId);
        if (step == null || step.Kind == TutorialStepKind.LongTerm)
            return;
        if (completedStepIds.Contains(step.Id) ||
            !IsStepTriggered(step, game) ||
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
        if (step.Kind == TutorialStepKind.Resources)
        {
            snapshot.Blocker = "核心资源尚未形成可见库存。";
            snapshot.RecommendedAction = "打开资源页面，查看木材的数量与净产出。";
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
                snapshot.Blocker = "鼠族复兴当前缺少能解决实际问题的建筑。";
                snapshot.RecommendedAction = "打开建筑页面，查看“" + recommendation.Label +
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
    }

    private static string BuildOwnedProductionChainSummary(BuildingManager buildings)
    {
        if (!TryFindOwnedProductionChain(buildings, out Resource consumed, out Resource generated))
            return string.Empty;
        return consumed.Label + " 鈫?" + generated.Label;
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

    private static Research FindResearchGuidance(ResearchManager researchManager)
    {
        if (researchManager == null)
            return null;
        if (researchManager.ActiveResearch != null)
            return researchManager.ActiveResearch.Definition;
        foreach (ResearchState state in researchManager.States.Values)
            if (state != null && state.Status == ResearchStatus.Available)
                return state.Definition;
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
                ? "它会带来“" + effect.Type + "”效果，帮助恢复失落的文明能力。"
                : "它会对“" + target + "”产生“" + effect.Type +
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
                            consumed = FindFirstResource(
                                source.Definition.ResourceConsumptionRates) ?? output.First;
                            generated = output.First;
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
        if (data != null && data.CompletedStepIds != null)
            for (int i = 0; i < data.CompletedStepIds.Count; i++)
                if (FindStep(data.CompletedStepIds[i]) != null)
                    completedStepIds.Add(data.CompletedStepIds[i]);
        activeStepId = FindStep(data == null ? string.Empty : data.ActiveStepId)?.Id;
        if (string.IsNullOrEmpty(activeStepId))
            activeStepId = currentEra == TechLevel.Animal
                ? steps[0].Id
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
        activeStepId = steps[0].Id;
        version++;
    }
}
