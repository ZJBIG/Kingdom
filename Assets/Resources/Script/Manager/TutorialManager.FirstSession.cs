using System.Collections.Generic;

public sealed partial class TutorialManager
{
    private string recentFirstSessionProgress = string.Empty;
    private string recentFirstSessionProgressId = string.Empty;

    private bool TryBuildFirstSessionSnapshot(GameManager game, ResourceManager resources,
        BuildingManager buildings, ResearchManager research, out TutorialSnapshot snapshot)
    {
        snapshot = null;
        if (game.State.TechLevel != TechLevel.Animal)
        {
            if (research.IsResearchCompleted("StoneAgeTransition") && !completedStepIds.Contains("era-goal"))
            {
                RecordFirstSessionResult("era-goal", "石器时代已开启，可以发展新建筑与研究。");
                ResolveFirstSessionStage("first-transition");
                ResolveFirstSessionStage("era-goal");
                // Population is parallel feedback, never a delayed operation gate.
                if (!completedStageIds.Contains("population") && !skippedStageIds.Contains("population"))
                    baselineStageIds.Add("population");
                activeStepId = FindStep("era-goal")?.NextStepId ?? FindEraGoalOrTerminalStepId();
                if (activeStageId == "first-transition") ClearActiveStage();
            }
            return false;
        }

        // Results follow the game state; skipping dismisses only the operation lesson.
        RecordFirstSessionResult("orientation", string.Empty);
        if (game.State.HasBuiltHousing)
        {
            // Resource teaching is integrated with the first home's real payment.
            RecordFirstSessionResult("resources", string.Empty);
            ResolveFirstSessionStage("resources");
            RecordFirstSessionResult("building", "木屋已建成，研究已开放；居民会陆续入住。");
            ResolveFirstSessionStage("building");
        }
        if (HasObservedPopulationGrowth(game.State))
        {
            RecordFirstSessionResult("population", "首位居民已入住，正在提供生产力。");
            ResolveFirstSessionStage("population");
        }
        if (research.IsResearchCompleted("Agriculture"))
        {
            RecordFirstSessionResult("research", "农业已完成，接下来让农场实际供粮。");
            ResolveFirstSessionStage("research");
        }
        if (completedStepIds.Contains("farm-supply")) ResolveFirstSessionStage("farm-goal");
        if (completedStepIds.Contains("stone-supply"))
        {
            RecordFirstSessionResult("production-chain", string.Empty);
            ResolveFirstSessionStage("stone-chain");
        }

        snapshot = new TutorialSnapshot { CompletedFeedback = recentFirstSessionProgress, CompletedFeedbackId = recentFirstSessionProgressId };
        if (!game.State.HasBuiltHousing || game.State.Population.PopulationCapacity <= ExpantaNum.Zero)
            SetFirstBuildingAction(snapshot, "building", "WoodHouse",
                "先给族人一个留下的地方。", "研究农业，准备稳定供粮。", game, resources, buildings, research);
        else if (!research.IsResearchCompleted("Agriculture"))
            SetFirstResearchAction(snapshot, "research", "Agriculture",
                "学会耕种，让食物供应支撑更多居民。", "建造农场，让农业开始供粮。", game, resources, buildings, research);
        else if (!completedStepIds.Contains("farm-supply"))
            SetFirstBuildingAction(snapshot, "farm-goal", "Farm",
                "把耕种变成稳定的粮食来源。", "研究采石，建立石材加工链。", game, resources, buildings, research, true);
        else if (!completedStepIds.Contains("stone-supply"))
        {
            if (!research.IsResearchCompleted("Quarry"))
                SetFirstResearchAction(snapshot, "stone-chain", "Quarry", "从采集走向开采。",
                    "建造采石场，取得石料。", game, resources, buildings, research);
            else if (!HasOwnedBuildingId(buildings, "Quarry"))
                SetFirstBuildingAction(snapshot, "stone-chain", "Quarry", "为加工链提供石料。",
                    "研究石料加工。", game, resources, buildings, research);
            else if (!research.IsResearchCompleted("StoneProcessing"))
                SetFirstResearchAction(snapshot, "stone-chain", "StoneProcessing", "把石料变成更有用的材料。",
                    "建造切石工坊，连接原料与加工。", game, resources, buildings, research);
            else
                SetFirstBuildingAction(snapshot, "stone-chain", "StoneCuttingWorkshop", "让采石场支撑切石工坊。",
                    "整合定居聚落，准备文明跃迁。", game, resources, buildings, research, true);
        }
        else
        {
            string target = !research.IsResearchCompleted("StoneAgeSettlement") ? "StoneAgeSettlement" :
                !research.IsResearchCompleted("CivilizationPlanning") ? "CivilizationPlanning" : "StoneAgeTransition";
            SetFirstResearchAction(snapshot, "first-transition", target,
                "让住房、供粮与加工支撑长久定居。",
                target == "StoneAgeSettlement" ? "完成文明规划，开放时代页面。" :
                target == "CivilizationPlanning" ? "完成石器时代跃迁。" : "进入石器时代，发展新的建筑与研究。",
                game, resources, buildings, research);
        }
        snapshot.NextEraGoal = snapshot.NextGoal;
        activeStepId = snapshot.StepId == "farm-goal" ? "research" :
            snapshot.StepId == "stone-chain" ? "production-chain" :
            snapshot.StepId == "first-transition" ? "era-goal" : snapshot.StepId;
        return true;
    }

    private void RecordFirstSessionResult(string id, string feedback)
    {
        if (!completedStepIds.Add(id)) return;
        if (!string.IsNullOrEmpty(feedback))
        {
            recentFirstSessionProgress = feedback;
            recentFirstSessionProgressId = id;
        }
        version++;
    }

    private void ResolveFirstSessionStage(string id)
    {
        if (!skippedStageIds.Contains(id) && !baselineStageIds.Contains(id) && completedStageIds.Add(id)) version++;
    }

    internal void RecordFirstSessionProductionTick()
    {
        GameManager game = GameManager.Instance;
        BuildingManager buildings = BuildingManager.Instance;
        if (game == null || game.State.TechLevel != TechLevel.Animal || buildings == null) return;
        if (DataBase<BuildingDef>.TryFind("Farm", out BuildingDef farm) &&
            IsFirstSessionBuildingRunning(buildings, farm) && game.State.FoodProductionRate > ExpantaNum.Zero)
            RecordFirstSessionResult("farm-supply", "农场开始供粮，人口增长有了稳定支持。");
        if (DataBase<BuildingDef>.TryFind("Quarry", out BuildingDef quarry) && IsFirstSessionBuildingRunning(buildings, quarry) &&
            DataBase<BuildingDef>.TryFind("StoneCuttingWorkshop", out BuildingDef workshop) && IsFirstSessionBuildingRunning(buildings, workshop))
        {
            ResourceDef product = FindFirstResource(workshop.ResourceGenerationRates);
            if (product != null && ResourceManager.Instance.GetAmount(product) > ExpantaNum.Zero)
                RecordFirstSessionResult("stone-supply", "切石工坊已产出材料，第一条加工链开始运转。");
        }
    }

    private static bool IsFirstSessionBuildingRunning(BuildingManager buildings, BuildingDef building)
    {
        buildings.States.TryGetValue(building, out BuildingState state);
        return state != null && state.Amount > ExpantaNum.Zero && state.IsEnabled && state.Efficiency > ExpantaNum.Zero;
    }

    private void SetFirstBuildingAction(TutorialSnapshot snapshot, string stage, string id, string benefit, string next,
        GameManager game, ResourceManager resources, BuildingManager buildings, ResearchManager research, bool requireProduction = false)
    {
        if (!DataBase<BuildingDef>.TryFind(id, out BuildingDef building)) return;
        if (TryFindBuildingResearchBlocker(building, research, out ResearchDef prerequisite) && prerequisite != null)
        {
            SetFirstResearchAction(snapshot, stage, prerequisite.Id, benefit, "建造" + building.Label + "。", game, resources, buildings, research);
            return;
        }
        snapshot.StepId = stage;
        snapshot.ActionId = "build:" + id;
        snapshot.CurrentGoal = "建造" + building.Label;
        snapshot.NavigationPage = "Buildings";
        snapshot.NavigationTargetId = id;
        snapshot.Benefit = benefit;
        snapshot.NextGoal = next;
        snapshot.CanAct = buildings.GetBuildFailure(building, ExpantaNum.One) == BuildFailure.None;
        snapshot.Blocker = snapshot.CanAct ? "材料和土地已备齐。" : DescribeBuildingBlocker(building, game, buildings, resources);
        snapshot.RecommendedAction = "前往建筑页，选择" + building.Label + "并建造 1 座。";
        if (requireProduction && HasOwnedBuildingId(buildings, id))
        {
            snapshot.CurrentGoal = "让" + building.Label + "开始供给";
            snapshot.ActionId = "produce:" + id;
            snapshot.RecommendedAction = "查看" + building.Label + "的实际产出。";
            snapshot.CanAct = false;
            snapshot.IsWaiting = true;
            snapshot.Blocker = IsFirstSessionBuildingRunning(buildings, building)
                ? "等待实际生产；期间可以自由操作。" : "当前没有有效产出；检查启用状态、劳力与原料。";
            snapshot.Benefit = DescribeActualBuildingBenefit(building, buildings, game);
            for (int i = 0; i < building.ResourceConsumptionRates.Count; i++)
            {
                ResourceDef input = building.ResourceConsumptionRates[i].First;
                if (input == null || resources.GetAmount(input) > ExpantaNum.Zero || GetFirstSessionNetRate(resources, input) > ExpantaNum.Zero) continue;
                SetFirstResourceSource(snapshot, input, stage, game, resources, buildings, research, new HashSet<string>());
                return;
            }
            return;
        }
        ResourceDef missing = FindMissingBuildingResource(building, buildings, resources);
        if (!snapshot.CanAct && missing != null && GetFirstSessionNetRate(resources, missing) <= ExpantaNum.Zero && stage != "building")
            SetFirstResourceSource(snapshot, missing, stage, game, resources, buildings, research, new HashSet<string>());
        snapshot.IsWaiting = !snapshot.CanAct;
    }

    private void SetFirstResearchAction(TutorialSnapshot snapshot, string stage, string id, string benefit, string next,
        GameManager game, ResourceManager resources, BuildingManager buildings, ResearchManager research)
    {
        if (!DataBase<ResearchDef>.TryFind(id, out ResearchDef target)) return;
        ResearchDef definition = FindFirstUncompletedResearchLeaf(target, research, new HashSet<string>()) ?? target;
        ResearchState state = research.GetState(definition);
        snapshot.StepId = stage;
        snapshot.ActionId = "research:" + definition.Id;
        snapshot.CurrentGoal = "研究" + definition.Label;
        snapshot.NavigationPage = definition.AdvancesTechLevel && ProgressionVisibilityRules.IsEraUnlocked(research) ? "Era" : "Research";
        snapshot.NavigationTargetId = definition.Id;
        snapshot.Benefit = definition == target ? benefit : "先完成前置知识，才能继续" + target.Label + "。";
        snapshot.NextGoal = next;
        snapshot.RecommendedAction = "选择" + definition.Label + "，加入研究队列。";
        snapshot.CanAct = research.ArePrerequisitesCompleted(definition) && research.CanPayResearchCost(definition, out _);
        if (state != null && state.Status == ResearchStatus.Researching)
        {
            snapshot.CanAct = false;
            snapshot.RecommendedAction = "查看" + definition.Label + "的进度与材料。";
            ExpantaNum speed = research.CurrentResearchSpeed;
            snapshot.Blocker = speed > ExpantaNum.Zero ? definition.Label + "研究中，约 " + ((state.BaseCost - state.Progress) / speed).ToGameString() + " 秒完成。"
                : "研究尚无有效进度；检查研究力和供粮。";
        }
        else if (research.IsQueued(definition))
        {
            snapshot.CanAct = false;
            snapshot.RecommendedAction = "查看" + definition.Label + "的进度与材料。";
            snapshot.Blocker = definition.Label + (state != null && state.CostPaid ? "排队中，材料已支付。" : "排队中，材料未支付。");
        }
        else snapshot.Blocker = snapshot.CanAct ? "前置和材料已满足。" : "先备齐" + definition.Label + "的研究材料。";
        ResourceDef missing = FindMissingResearchResource(research, definition);
        if (missing != null && (state == null || !state.CostPaid))
        {
            ExpantaNum remaining = ExpantaNum.Zero;
            foreach (Pair<ResourceDef, ExpantaNum> cost in definition.ResourceRequirements)
                if (cost.First == missing) remaining = cost.Second - (state?.GetPaidResourceCost(missing) ?? ExpantaNum.Zero) - resources.GetAmount(missing);
            ExpantaNum net = GetFirstSessionNetRate(resources, missing);
            snapshot.Blocker += "还缺" + missing.Label + " " + remaining.ToGameString() +
                (net > ExpantaNum.Zero ? "，约 " + (remaining / net).ToGameString() + " 秒备齐。" : "，当前没有正净产出。");
            if (net <= ExpantaNum.Zero) SetFirstResourceSource(snapshot, missing, stage, game, resources, buildings, research, new HashSet<string>());
        }
        snapshot.IsWaiting = !snapshot.CanAct;
    }

    private static ExpantaNum GetFirstSessionNetRate(ResourceManager resources, ResourceDef resource) =>
        resources.States.TryGetValue(resource, out ResourceState state)
            ? ResourceManager.ApplyCurrentProductionReward(state.ProductionRate) - state.ConsumptionRate : ExpantaNum.Zero;

    private void SetFirstResourceSource(TutorialSnapshot snapshot, ResourceDef resource, string stage, GameManager game,
        ResourceManager resources, BuildingManager buildings, ResearchManager research, HashSet<string> visiting)
    {
        if (!visiting.Add(resource.Id)) return;
        BuildingDef source = null;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
            if (building != null && !(building is SectorBuildingDef) && building.TechLevel <= game.State.TechLevel && BuildingGenerates(building, resource))
            { source = building; break; }
        if (source == null)
        {
            snapshot.RecommendedAction = "检查" + resource.Label + "的来源与消耗，恢复正净产出。";
            return;
        }
        // Keep traversal on one visited set, including construction inputs.
        ResearchDef leaf = null;
        TryFindBuildingResearchBlocker(source, research, out leaf);
        if (leaf != null)
        {
            snapshot.NavigationPage = "Research"; snapshot.NavigationTargetId = leaf.Id;
            snapshot.ActionId = "research:" + leaf.Id; snapshot.CurrentGoal = "研究" + leaf.Label;
            ResearchState state = research.GetState(leaf);
            snapshot.CanAct = research.CanPayResearchCost(leaf, out _) && !research.IsQueued(leaf) && state.Status != ResearchStatus.Researching;
            snapshot.Blocker = state.Status == ResearchStatus.Researching ? leaf.Label + "研究中；期间可以自由操作。" :
                research.IsQueued(leaf) ? leaf.Label + "排队中；期间可以自由操作。" :
                "取得" + resource.Label + "前，先解锁" + source.Label + "。";
            snapshot.RecommendedAction = state.Status == ResearchStatus.Researching || research.IsQueued(leaf)
                ? "查看" + leaf.Label + "的进度与材料。" : "选择" + leaf.Label + "，加入研究队列。";
        }
        else
        {
            snapshot.NavigationPage = "Buildings"; snapshot.NavigationTargetId = source.Id;
            snapshot.ActionId = "build:" + source.Id; snapshot.CurrentGoal = "补充" + source.Label;
            snapshot.CanAct = buildings.GetBuildFailure(source, ExpantaNum.One) == BuildFailure.None;
            snapshot.Blocker = snapshot.CanAct ? "增加" + resource.Label + "来源，恢复正净产出。" : DescribeBuildingBlocker(source, game, buildings, resources);
            snapshot.RecommendedAction = "检查并启用" + source.Label + "，按缺口补充生产。";
            ResourceDef missing = FindMissingBuildingResource(source, buildings, resources);
            if (missing != null && GetFirstSessionNetRate(resources, missing) <= ExpantaNum.Zero)
                SetFirstResourceSource(snapshot, missing, stage, game, resources, buildings, research, visiting);
        }
        snapshot.IsWaiting = !snapshot.CanAct;
    }

    public static string DescribeActualBuildingBenefit(BuildingDef building, BuildingManager buildings, GameManager game)
    {
        buildings.States.TryGetValue(building, out BuildingState state);
        if (state == null || !state.IsEnabled || state.Amount <= ExpantaNum.Zero) return "尚未运行。";
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum scale = state.Amount * state.Efficiency * modifiers.GetBuildingProductionMultiplier(building) *
            modifiers.GlobalBuildingProductionMultiplier * game.State.Events.ProductionMultiplier;
        if (building.FoodProductionRate > ExpantaNum.Zero)
            return "当前实际供粮 +" + (scale * building.FoodProductionRate * modifiers.GlobalFoodProductionMultiplier).ToGameString() + "/s。";
        ResourceDef output = FindFirstResource(building.ResourceGenerationRates);
        if (output != null)
            foreach (Pair<ResourceDef, ExpantaNum> rate in building.ResourceGenerationRates)
                if (rate.First == output)
                    return "当前实际产出" + output.Label + " +" + (scale * rate.Second * modifiers.GetBuildingResourceProductionMultiplier(building, output) *
                        modifiers.GetResourceProductionMultiplier(output) * game.State.HappinessRewardMultiplier).ToGameString() + "/s。";
        return building.PopulationCapacityGranted > ExpantaNum.Zero ? "提供住房容量；居民入住后增加生产力。" : "效果已生效，详情可查看当前状态。";
    }

    private TutorialStageSnapshot EvaluateFirstSessionStage(TutorialSnapshot action)
    {
        string id = action.StepId;
        if (skippedStageIds.Contains(id) || baselineStageIds.Contains(id) || completedStageIds.Contains(id))
        {
            if (!string.IsNullOrEmpty(activeStageId)) ClearActiveStage();
            return null;
        }
        if (activeStageId != id || stageTargetId != action.NavigationTargetId)
        {
            activeStageId = id; stageTargetId = action.NavigationTargetId; stagePhase = 0; version++;
        }
        bool producing = action.ActionId.StartsWith("produce:", System.StringComparison.Ordinal);
        bool queued = (action.NavigationPage == "Research" || action.NavigationPage == "Era") &&
            DataBase<ResearchDef>.TryFind(action.NavigationTargetId, out ResearchDef definition) &&
            (cachedResearchManager.IsQueued(definition) || cachedResearchManager.GetState(definition)?.Status == ResearchStatus.Researching);
        TutorialStageAction operation = queued || producing ? TutorialStageAction.Inspect :
            action.NavigationPage == "Buildings" ? TutorialStageAction.Build : TutorialStageAction.StartResearch;
        return new TutorialStageSnapshot
        {
            StageId = id, Page = action.NavigationPage, TargetId = action.NavigationTargetId, Title = action.CurrentGoal,
            Action = queued || producing ? TutorialStageAction.Inspect : stagePhase == 0 ? TutorialStageAction.Navigate :
                stagePhase == 1 ? TutorialStageAction.Select : operation,
            CanInteract = stagePhase < 2 && !queued && !producing || action.CanAct,
            Instruction = queued || producing || !action.CanAct ? action.Blocker :
                stagePhase == 0 ? action.Benefit + "\n点击“" + StagePageLabel(action.NavigationPage) + "”，开始下一步。" :
                operation == TutorialStageAction.Build ? "建造 1 座，" + action.Benefit : "加入研究队列，完成后" + action.NextGoal
        };
    }
}
