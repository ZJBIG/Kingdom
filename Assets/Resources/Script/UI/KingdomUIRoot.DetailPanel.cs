using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Detail panel presenters, requirements, production flows and actions.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private struct ResourceBuildingFlow
    {
        public Building Building;
        public ExpantaNum Rate;
    }

    private readonly Dictionary<Resource, TMP_Text> detailRequirementLabels = new();
    private readonly Dictionary<Resource, TMP_Text> detailRequirementAmounts = new();
    private readonly List<Pair<Resource, ExpantaNum>> buildingRequirementBuffer = new();
    private readonly List<Pair<Resource, ExpantaNum>> buildingOutputFlowBuffer = new();
    private readonly List<Pair<Resource, ExpantaNum>> buildingInputFlowBuffer = new();
    private readonly List<ResourceBuildingFlow> resourceProducerFlowBuffer = new();
    private readonly List<ResourceBuildingFlow> resourceConsumerFlowBuffer = new();
    private readonly StringBuilder resourceDetailTextBuilder = new();

    private void ShowDetails(string title, string description, string id)
    {
        if (detailBody == null)
            return;
        detailBody.fontSize = 30f;
        detailBuildingUpgrade = false;
        detailIsBuilding = false;
        selectedBuilding = null;
        selectedResource = null;
        selectedWorkshop = null;
        selectedSectorDefinition = null;
        detailBody.text = title + "\n\n" + description + "\n\n标识：" + id;
        HideBuildingRequirements();
        if (detailActionButton != null)
            detailActionButton.gameObject.SetActive(false);
        // Generic/workshop details do not go through the requirement
        // presenter. Reapply the body layout after the body was moved under
        // DetailScrollContent, otherwise it keeps the authored panel-space
        // offsets and can be clipped by the unified viewport.
        LayoutResourceDetailsBody();
    }

    private bool ShouldDisplayBuilding(Building definition)
    {
        if (BuildingManager.Instance != null)
            return BuildingManager.Instance.ShouldDisplay(definition);
        return BuildingPrerequisitesMet(definition);
    }

    private List<Pair<Resource, ExpantaNum>> GetNextBuildingRequirements(
        Building building,
        BuildingState state,
        bool upgrading)
    {
        List<Pair<Resource, ExpantaNum>> requirements = buildingRequirementBuffer;
        requirements.Clear();
        ExpantaNum quantity = GetSelectedBuildingQuantity(building, upgrading, false);
        if (quantity < ExpantaNum.One)
            return requirements;

        if (upgrading)
        {
            BuildingManager.Instance.GetUpgradeResourceDeltas(building, quantity, requirements);
            return requirements;
        }

        ExpantaNum owned = state == null ? ExpantaNum.Zero : state.Amount;
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = building.ResourceRequirements[i];
            requirements.Add(new Pair<Resource, ExpantaNum>(
                requirement.First,
                requirement.Second.GeometricSeriesCost(building.CostGrowth, owned, quantity)));
        }
        return requirements;
    }

    private List<Pair<Resource, ExpantaNum>> GetEffectiveBuildingFlows(
        Building building,
        bool output)
    {
        List<Pair<Resource, ExpantaNum>> flows = output
            ? buildingOutputFlowBuffer
            : buildingInputFlowBuffer;
        flows.Clear();
        IReadOnlyList<Pair<Resource, ExpantaNum>> source = output
            ? building.ResourceGenerationRates
            : building.ResourceConsumptionRates;
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum productionMultiplier = modifiers.GetBuildingProductionMultiplier(building) *
            modifiers.GlobalBuildingProductionMultiplier;
        for (int i = 0; i < source.Count; i++)
        {
            Pair<Resource, ExpantaNum> flow = source[i];
            ExpantaNum multiplier = output
                ? productionMultiplier * modifiers.GetResourceProductionMultiplier(flow.First)
                : ExpantaNum.One;
            flows.Add(new Pair<Resource, ExpantaNum>(flow.First, flow.Second * multiplier));
        }
        return flows;
    }

    private void ShowBuildingDetails(Building building, bool preserveScrollPosition = false)
    {
        if (detailBody == null || building == null)
            return;
        float? preservedScrollPosition = preserveScrollPosition &&
            selectedBuilding == building && requirementGesture != null
            ? requirementGesture.GetNormalizedPosition()
            : null;
        detailBody.fontSize = 30f;
        selectedBuilding = building;
        selectedResearchNode = null;
        selectedResource = null;
        selectedWorkshop = null;
        selectedSectorDefinition = null;
        lastSelectedBuildingVersion = -1;
        lastSelectedBuildingResourceVersion = -1;
        detailIsBuilding = true;
        BuildingState state = BuildingManager.Instance != null &&
            BuildingManager.Instance.States.TryGetValue(building, out BuildingState existing)
            ? existing
            : null;
        if (useCompactBuildingDetails)
            SetCompactBuildingDetailBody(building);
        else
            SetBuildingDetailBody(building, state);
        bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
            BuildingManager.Instance != null &&
            BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out _);
        detailBuildingUpgrade = upgrading;
        if (detailActionButton != null)
            detailActionButton.gameObject.SetActive(false);
        IReadOnlyList<Pair<Resource, ExpantaNum>> output = GetEffectiveBuildingFlows(building, true);
        IReadOnlyList<Pair<Resource, ExpantaNum>> input = GetEffectiveBuildingFlows(building, false);
        ShowBuildingFlows(output, input);
        List<Pair<Resource, ExpantaNum>> requirements = GetNextBuildingRequirements(building, state, upgrading);
        ShowBuildingRequirements(requirements, "建筑建造需求");
        PlaceRequirementsAfterDescription(
            requirements == null ? 0 : requirements.Count,
            preservedScrollPosition,
            CountFlows(output) + CountFlows(input));
    }

    private void SetBuildingDetailBody(Building building, BuildingState state)
    {
        StringBuilder text = new();
        text.AppendLine(building.Label);
        text.AppendLine();
        text.AppendLine(building.Description);
        text.AppendLine();
        text.AppendLine("数量: " + (state == null ? "0" : state.Amount.ToGameString()));
        text.AppendLine("时代: " + building.TechLevel.GetDescription());
        text.AppendLine("土地需求: " + building.SpaceCost.ToGameString());
        text.AppendLine("生产力需求: " + building.ProductivityConsumption.ToGameString());
        text.AppendLine();
        text.AppendLine("研究前置");
        AppendResearchPrerequisites(text, building.RequiredResearch);
        text.AppendLine("工坊前置");
        AppendWorkshopPrerequisites(text, building.RequiredWorkshopUpgrades);
        text.AppendLine();
        text.AppendLine("当前状态：" + GetBuildingDetailStatus(building, state));
        text.AppendLine();
        detailBody.text = text.ToString();
    }

    private static string GetBuildingDetailStatus(Building building, BuildingState state)
    {
        BuildingManager manager = BuildingManager.Instance;
        if (building == null || manager == null || GameManager.Instance == null ||
            GameManager.Instance.State == null)
            return "建筑状态未初始化";
        bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
            manager.TryGetUnlockedUpgradeTarget(building, out _);
        if (!manager.ArePrerequisitesMet(building, out BuildFailure prerequisiteFailure))
        {
            switch (prerequisiteFailure)
            {
                case BuildFailure.TechnologyInsufficient:
                    return "需进入" + building.TechLevel.GetDescription();
                case BuildFailure.SectorNotOccupied:
                    return "需先占领所属星区";
                case BuildFailure.ResearchPrerequisiteIncomplete:
                    return "研究前置未完成";
                case BuildFailure.WorkshopPrerequisiteIncomplete:
                    return "工坊前置未完成";
                default:
                    return "前置条件未满足";
            }
        }
        if (!upgrading && !manager.CanConstructNew(building))
            return "已被更高等级建筑替代";
        if (building is SectorBuilding sectorBuilding && state != null &&
            state.Amount >= new ExpantaNum(sectorBuilding.MaxAmount))
            return "已达到该星区建筑上限";
        ExpantaNum spaceCost = state == null ? building.SpaceCost : state.SpaceCost;
        if (!(building is SectorBuilding) &&
            GameManager.Instance.State.AvailableTerritory < spaceCost)
            return "领土不足：还需" +
                (spaceCost - GameManager.Instance.State.AvailableTerritory).ToGameString();
        ExpantaNum productivity = state == null ? building.ProductivityConsumption : state.ProductivityConsumption;
        if (manager.AvailableProductivity < productivity)
            return "生产力不足：还需" +
                (productivity - manager.AvailableProductivity).ToGameString();
        List<Pair<Resource, ExpantaNum>> requirements = new();
        if (upgrading)
            manager.GetUpgradeResourceDeltas(building, ExpantaNum.One, requirements);
        else
        {
            ExpantaNum owned = state == null ? ExpantaNum.Zero : state.Amount;
            for (int i = 0; i < building.ResourceRequirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement = building.ResourceRequirements[i];
                requirements.Add(new Pair<Resource, ExpantaNum>(requirement.First,
                    requirement.Second.GeometricSeriesCost(building.CostGrowth, owned, ExpantaNum.One)));
            }
        }
        ResourceManager resources = ResourceManager.Instance;
        if (resources == null)
            return "资源管理器未初始化";
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null)
                return "资源需求无效";
            resources.States.TryGetValue(requirement.First, out ResourceState resourceState);
            ExpantaNum amount = resourceState == null ? ExpantaNum.Zero : resourceState.Amount;
            if (amount < requirement.Second)
                return "资源不足：" + requirement.First.Label + " 缺 " +
                    (requirement.Second - amount).ToGameString();
        }
        return "可建造";
    }

    private void RefreshSelectedBuildingDetails(Building building)
    {
        BuildingManager buildingManager = BuildingManager.Instance;
        if (building == null || detailBody == null || buildingManager == null)
            return;

        BuildingState state = buildingManager.States.TryGetValue(building, out BuildingState current)
            ? current
            : null;
        bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
            buildingManager.TryGetUnlockedUpgradeTarget(building, out _);

        // A prerequisite/upgrade transition changes the structure of the
        // detail panel. Rebuild only for that structural change; ordinary
        // amount/rate changes update existing rows in place so a gesture
        // never loses its content or pointer state.
        List<Pair<Resource, ExpantaNum>> requirements = GetNextBuildingRequirements(building, state, upgrading);
        if (detailBuildingUpgrade != upgrading || !RefreshRequirementRows(requirements))
        {
            ShowBuildingDetails(building, true);
            return;
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> output = GetEffectiveBuildingFlows(building, true);
        IReadOnlyList<Pair<Resource, ExpantaNum>> input = GetEffectiveBuildingFlows(building, false);
#if UNITY_EDITOR
        float flowRefreshStart = Time.realtimeSinceStartup;
#endif
        bool flowRefreshed = RefreshFlowRows(output, input);
#if UNITY_EDITOR
        RecordUiBranch("dataflow", (Time.realtimeSinceStartup - flowRefreshStart) * 1000f);
#endif
        if (!flowRefreshed)
        {
            ShowBuildingDetails(building, true);
            return;
        }

        if (useCompactBuildingDetails)
            SetCompactBuildingDetailBody(building);
        else
            SetBuildingDetailBody(building, state);
        if (detailActionButton != null)
            detailActionButton.gameObject.SetActive(false);
    }

    private void ShowResourceDetails(Resource resource)
    {
        if (detailBody == null || resource == null)
            return;

        TutorialManager.Current?.RecordDetailViewed("Resources", resource.Id);

        selectedResearchNode = null;
        selectedBuilding = null;
        selectedResource = resource;
        selectedWorkshop = null;
        selectedSectorDefinition = null;
        lastSelectedResourceVersion = -1;
        detailIsBuilding = false;
        detailBuildingUpgrade = false;
        HideBuildingRequirements();
        if (detailActionButton != null)
            detailActionButton.gameObject.SetActive(false);

        RefreshResourceDetails(resource);
    }

    private void RefreshResourceDetails(Resource resource)
    {
        if (detailBody == null || resource == null || selectedResource != resource)
            return;
        float preservedScrollPosition = requirementGesture == null
            ? 1f
            : requirementGesture.GetNormalizedPosition();

        ResourceState state = null;
        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager != null)
            resourceManager.States.TryGetValue(resource, out state);

        ExpantaNum amount = state == null ? ExpantaNum.Zero : state.Amount;
        ExpantaNum production = state == null
            ? ExpantaNum.Zero
            : ResourceManager.ApplyCurrentProductionReward(state.ProductionRate);
        ExpantaNum consumption = state == null ? ExpantaNum.Zero : state.ConsumptionRate;
        ExpantaNum net = NormalizeDisplayedNetRate(production, consumption);
        RefreshResourceBuildingFlows(resource);
        List<ResourceBuildingFlow> producers = resourceProducerFlowBuffer;
        List<ResourceBuildingFlow> consumers = resourceConsumerFlowBuffer;
        StringBuilder text = resourceDetailTextBuilder;
        text.Clear();
        text.AppendLine(resource.Label);
        text.AppendLine();
        text.AppendLine(resource.Description);
        text.AppendLine();

        text.AppendLine("库存: " + amount.ToGameString());

        text.AppendLine(("产出: +" + production.ToGameString() + "/s").Colorize(Positive));
        for (int i = 0; i < producers.Count; i++)
            text.AppendLine("       --" + producers[i].Building.Label +
                ": " +
                ("+" + producers[i].Rate.ToGameString() + "/s").Colorize(Positive));

        text.AppendLine(("消耗: -" + consumption.ToGameString() + "/s").Colorize(Error));
        for (int i = 0; i < consumers.Count; i++)
            text.AppendLine("       --" + consumers[i].Building.Label +
                ": " +
                ("-" + consumers[i].Rate.ToGameString() + "/s").Colorize(Error));

        text.AppendLine(("净产出: " + (net >= ExpantaNum.Zero ? "+" : "") + net.ToGameString() + "/s").Colorize(net >= ExpantaNum.Zero ? Positive : Error));
        text.AppendLine(GetResourceSupplyStatus(state, consumption));
        detailBody.text = text.ToString();
        detailBody.richText = true;
        detailBody.fontSize = 30f;
        LayoutResourceDetailsBody();
        if (requirementGesture != null)
            requirementGesture.SetNormalizedPosition(preservedScrollPosition);
    }

    private void SetCompactBuildingDetailBody(Building building)
    {
        if (detailBody == null)
            return;
        detailBody.text = building.Label;
        detailBody.gameObject.SetActive(true);
    }

    private static string GetResourceSupplyStatus(ResourceState state, ExpantaNum consumption)
    {
        if (state == null)
            return "当前供给状态：未初始化";
        if (consumption <= ExpantaNum.Zero)
            return "当前供给状态：无消耗需求";

        ExpantaNum satisfaction = state.GetTickSatisfactionOrFallback();
        if (satisfaction >= ExpantaNum.One)
            return "当前供给状态：供给充足".Colorize(Positive);
        if (satisfaction > ExpantaNum.Zero)
            return ("当前供给状态：供给不足（满足度 " +
                satisfaction.ToGameString() + "）").Colorize(Error);
        return "当前供给状态：暂无供给".Colorize(Error);
    }

    private void RefreshResourceBuildingFlows(Resource resource)
    {
        resourceProducerFlowBuffer.Clear();
        resourceConsumerFlowBuffer.Clear();
        if (resource == null)
            return;
        BuildingManager buildingManager = BuildingManager.Instance;
        if (buildingManager == null)
            return;

        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        foreach (KeyValuePair<Building, BuildingState> entry in buildingManager.States)
        {
            Building building = entry.Key;
            BuildingState state = entry.Value;
            if (building == null || state == null)
                continue;

            if (state.Amount <= ExpantaNum.Zero || state.Efficiency <= ExpantaNum.Zero)
                continue;

            ExpantaNum scale = state.Amount * state.Efficiency;
            ExpantaNum productionRate = ExpantaNum.Zero;
            IReadOnlyList<Pair<Resource, ExpantaNum>> productionFlows =
                building.ResourceGenerationRates;
            for (int i = 0; i < productionFlows.Count; i++)
            {
                Pair<Resource, ExpantaNum> flow = productionFlows[i];
                if (flow.First == resource)
                    productionRate += flow.Second;
            }

            ExpantaNum consumptionRate = ExpantaNum.Zero;
            IReadOnlyList<Pair<Resource, ExpantaNum>> consumptionFlows =
                building.ResourceConsumptionRates;
            for (int i = 0; i < consumptionFlows.Count; i++)
            {
                Pair<Resource, ExpantaNum> flow = consumptionFlows[i];
                if (flow.First == resource)
                    consumptionRate += flow.Second;
            }
            if (productionRate <= ExpantaNum.Zero &&
                consumptionRate <= ExpantaNum.Zero)
                continue;

            ExpantaNum productionMultiplier =
                modifiers.GetBuildingProductionMultiplier(building) *
                modifiers.GlobalBuildingProductionMultiplier;
            AddResourceBuildingFlow(
                resourceProducerFlowBuffer, building, productionRate, scale,
                productionMultiplier, resource, modifiers, true);
            AddResourceBuildingFlow(
                resourceConsumerFlowBuffer, building, consumptionRate, scale,
                productionMultiplier, resource, modifiers, false);
        }

        SortResourceBuildingFlows(resourceProducerFlowBuffer);
        SortResourceBuildingFlows(resourceConsumerFlowBuffer);
    }

    private static void AddResourceBuildingFlow(
        List<ResourceBuildingFlow> result,
        Building building,
        ExpantaNum rate,
        ExpantaNum scale,
        ExpantaNum productionMultiplier,
        Resource resource,
        ProgressionModifierState modifiers,
        bool production)
    {
        if (rate <= ExpantaNum.Zero)
            return;
        rate *= scale;
        rate *= productionMultiplier;
        if (production)
        {
            rate *= modifiers.GetResourceProductionMultiplier(resource);
            rate = ResourceManager.ApplyCurrentProductionReward(rate);
        }
        if (rate <= ExpantaNum.Zero)
            return;
        result.Add(new ResourceBuildingFlow
        {
            Building = building,
            Rate = rate
        });
    }

    private static void SortResourceBuildingFlows(List<ResourceBuildingFlow> flows)
    {
        flows.Sort((left, right) =>
        {
            int byRate = right.Rate.CompareTo(left.Rate);
            return byRate != 0
                ? byRate
                : string.Compare(left.Building.Id, right.Building.Id, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void LayoutResourceDetailsBody()
    {
        if (detailBody == null)
            return;
        if (flowHost != null)
            flowHost.gameObject.SetActive(false);
        if (requirementHost != null)
            requirementHost.gameObject.SetActive(false);

        RectTransform body = detailBody.rectTransform;
        body.anchorMin = new Vector2(0, 1);
        body.anchorMax = new Vector2(1, 1);
        body.pivot = new Vector2(.5f, 1f);
        float width = GetMeasuredDetailTextWidth(body);
        float bodyHeight = Mathf.Clamp(
            detailBody.GetPreferredValues(detailBody.text, width, 1000f).y + 20f,
            96f,
            1400f);
        // The body height only changes when the line count changes; ordinary
        // value refreshes keep the same height. Skip the two full-canvas
        // immediate layout passes unless the height actually changed, otherwise
        // selecting a resource forces Canvas layout about 10 times per second.
        bool heightChanged = Mathf.Abs(bodyHeight - body.rect.height) > 1f;
        if (heightChanged)
            Canvas.ForceUpdateCanvases();
        body.offsetMin = new Vector2(34f, -bodyHeight);
        body.offsetMax = new Vector2(-34f, 0f);
        if (detailScrollContent != null)
            detailScrollContent.sizeDelta = new Vector2(0f, Mathf.Max(detailScrollViewport.rect.height, 24f + bodyHeight));
        if (heightChanged)
            Canvas.ForceUpdateCanvases();
    }

    private void ShowResearchDetails(Research research, bool preserveScrollPosition = false)
    {
        if (detailBody == null || research == null)
            return;
        detailBody.fontSize = 30f;
        bool keepScrollPosition = preserveScrollPosition && selectedResearchNode == research;
        float savedRequirementScrollPosition = keepScrollPosition && requirementGesture != null
            ? requirementGesture.GetNormalizedPosition()
            : 1f;
        selectedResearchNode = research;
        ResearchManager researchManager = ResearchManager.Instance;
        researchManager?.SetSelectedResearch(research);
        RefreshResearchTreeVisuals();
        // A queue action can open this detail presenter immediately after
        // changing ResearchManager. Update the toolbar in the same UI path
        // instead of waiting for the periodic live-refresh tick.
        RefreshResearchQueueToolbar();
        detailBuildingUpgrade = false;
        detailIsBuilding = false;
        selectedBuilding = null;
        selectedResource = null;
        selectedWorkshop = null;
        selectedSectorDefinition = null;
        ResearchState state = null;
        if (researchManager != null)
            researchManager.States.TryGetValue(research, out state);
        // The payment title is authored by the requirement section below;
        // keep it out of Body so it cannot be duplicated or appear as a
        // stray top line while the sections are being measured.
        StringBuilder text = new();
        text.AppendLine(research.Label);
        text.AppendLine("技术等级: " + research.TechLevel.GetDescription());
        text.AppendLine("研究点需求: " +
            (state == null ? FormatResearchBaseCost(research) : state.BaseCost.ToGameString()));
        text.AppendLine();
        text.AppendLine(research.Description);
        text.AppendLine();
        text.AppendLine("研究前置");
        AppendResearchPrerequisites(text, research.Prerequisites);
        text.AppendLine();
        text.AppendLine("当前状态：" + GetResearchDetailStatus(research, state));
        text.AppendLine();
        text.AppendLine("效果");
        AppendResearchEffects(text, research.Effects);
        text.AppendLine();
        text.AppendLine("文明复兴连接");
        AppendResearchConnections(text, research);
        detailBody.text = text.ToString();
        HideBuildingRequirements();
        ShowBuildingRequirements(research.ResourceRequirements, "研究支付需求");
        PlaceRequirementsAfterDescription(
            research.ResourceRequirements == null ? 0 : research.ResourceRequirements.Count,
            keepScrollPosition ? (float?)savedRequirementScrollPosition : null,
            0);
        ConfigureActionButton("加入研究队列", () => ResearchAction(research));
        if (detailActionButton != null)
            detailActionButton.interactable = IsResearchActionAvailable(research, state);
        CaptureResearchRefreshSignatures();
    }
    private static void AppendResearchEffects(
        StringBuilder builder,
        IReadOnlyList<ResearchEffectDefinition> effects)
    {
        if (effects == null || effects.Count == 0)
        {
            builder.AppendLine("  无");
            return;
        }
        for (int i = 0; i < effects.Count; i++)
        {
            ResearchEffectDefinition effect = effects[i];
            if (effect == null)
                continue;
            string target = effect.Building != null ? effect.Building.Label :
                effect.Resource != null ? effect.Resource.Label : "全局";
            builder.AppendLine("  " + effect.Type.GetDescription() + " / " +
                target + ": " + effect.Value);
        }
    }

    private static void AppendResearchPrerequisites(
        StringBuilder builder, IReadOnlyList<Research> prerequisites)
    {
        if (prerequisites == null || prerequisites.Count == 0)
        {
            builder.AppendLine("  无");
            return;
        }
        for (int i = 0; i < prerequisites.Count; i++)
        {
            Research prerequisite = prerequisites[i];
            if (prerequisite == null)
            {
                builder.AppendLine("  ○ 无效研究");
                continue;
            }
            builder.AppendLine("  " + (IsResearchCompleted(prerequisite) ? "✓ " : "○ ") +
                prerequisite.Label);
        }
    }

    private static void AppendWorkshopPrerequisites(
        StringBuilder builder, IReadOnlyList<WorkshopUpgrade> prerequisites)
    {
        if (prerequisites == null || prerequisites.Count == 0)
        {
            builder.AppendLine("  无");
            return;
        }
        for (int i = 0; i < prerequisites.Count; i++)
        {
            WorkshopUpgrade prerequisite = prerequisites[i];
            if (prerequisite == null)
            {
                builder.AppendLine("  ○ 无效工坊升级");
                continue;
            }
            builder.AppendLine("  " + (IsWorkshopPurchased(prerequisite) ? "✓ " : "○ ") +
                prerequisite.Label);
        }
    }

    private static bool IsResearchCompleted(Research research)
    {
        ResearchManager manager = UnityEngine.Object.FindObjectOfType<ResearchManager>();
        return research != null && manager != null && manager.IsResearchCompleted(research.Id);
    }

    private static bool IsWorkshopPurchased(WorkshopUpgrade upgrade)
    {
        WorkshopManager manager = UnityEngine.Object.FindObjectOfType<WorkshopManager>();
        return upgrade != null && manager != null && manager.IsPurchased(upgrade);
    }

    private static void AppendResearchConnections(StringBuilder builder,
        Research research)
    {
        if (research == null)
            return;

        bool hasConnection = false;
        if (research.AdvancesTechLevel)
        {
            builder.AppendLine("  完成后推进至：" + research.TechLevel.GetDescription());
            hasConnection = true;
        }

        int buildingCount = 0;
        IReadOnlyList<Building> buildings = DataBase<Building>.All;
        for (int i = 0; i < buildings.Count && buildingCount < 3; i++)
        {
            Building building = buildings[i];
            if (building == null || building is SectorBuilding ||
                !ContainsResearch(building.RequiredResearch, research))
                continue;
            builder.AppendLine("  解锁建筑方向：" + building.Label);
            buildingCount++;
            hasConnection = true;
        }

        int researchCount = 0;
        IReadOnlyList<Research> researches = DataBase<Research>.All;
        for (int i = 0; i < researches.Count && researchCount < 3; i++)
        {
            Research next = researches[i];
            if (next == null || next == research ||
                !ContainsResearch(next.Prerequisites, research))
                continue;
            builder.AppendLine("  研究后续方向：" + next.Label);
            researchCount++;
            hasConnection = true;
        }

        if (!hasConnection)
            builder.AppendLine("  这项研究的直接效果会立即作用于王国；请结合上方效果观察变化。");
    }

    private static bool ContainsResearch(
        IReadOnlyList<Research> values, Research target)
    {
        if (values == null || target == null)
            return false;
        for (int i = 0; i < values.Count; i++)
            if (values[i] == target)
                return true;
        return false;
    }

    private void ShowWorkshopDetails(WorkshopUpgrade definition,
        bool preserveScrollPosition = false)
    {
        if (detailBody == null || definition == null)
            return;

        float? preservedScrollPosition = preserveScrollPosition &&
            selectedWorkshop == definition && requirementGesture != null
            ? requirementGesture.GetNormalizedPosition()
            : null;
        detailBody.fontSize = 30f;
        detailBuildingUpgrade = false;
        detailIsBuilding = false;
        selectedBuilding = null;
        selectedResearchNode = null;
        selectedResource = null;
        selectedWorkshop = definition;
        selectedSectorDefinition = null;
        SetWorkshopDetailBody(definition);

        HideBuildingRequirements();
        ShowBuildingRequirements(definition.ResourceRequirements, "工坊支付需求");
        PlaceRequirementsAfterDescription(
            definition.ResourceRequirements == null ? 0 : definition.ResourceRequirements.Count,
            preservedScrollPosition,
            0);
        ConfigureWorkshopPaymentButton(definition);
    }

    private void SetWorkshopDetailBody(WorkshopUpgrade definition)
    {
        StringBuilder text = new();
        text.AppendLine(definition.Label);
        text.AppendLine();
        text.AppendLine("技术等级: " + definition.TechLevel.GetDescription());
        text.AppendLine();
        text.AppendLine(definition.Description);
        text.AppendLine();
        text.AppendLine("研究前置");
        AppendResearchPrerequisites(text, definition.RequiredResearch);
        text.AppendLine("工坊前置");
        AppendWorkshopPrerequisites(text, definition.RequiredUpgrades);
        text.AppendLine();
        text.AppendLine("当前状态：" + GetWorkshopAvailability(
            definition, out _));
        text.AppendLine();
        text.AppendLine("效果");
        AppendWorkshopEffects(text, definition.Effects);
        detailBody.text = text.ToString();
    }

    private static void AppendWorkshopEffects(
        StringBuilder builder,
        IReadOnlyList<WorkshopEffectDefinition> effects)
    {
        if (effects == null || effects.Count == 0)
        {
            builder.AppendLine("  无");
            return;
        }
        for (int i = 0; i < effects.Count; i++)
        {
            WorkshopEffectDefinition effect = effects[i];
            if (effect == null)
                continue;
            string target = effect.Building != null ? effect.Building.Label :
                effect.Resource != null ? effect.Resource.Label : "全局";
            builder.AppendLine("  " + effect.Type.GetDescription() + " / " + target + ": " + effect.Value);
        }
    }

    private static string GetWorkshopAvailability(
        WorkshopUpgrade definition,
        out bool canPurchase)
    {
        canPurchase = false;
        WorkshopManager workshop = WorkshopManager.Instance;
        if (definition == null || workshop == null)
            return "工坊未初始化";
        if (workshop.IsPurchased(definition))
            return "已拥有";
        if (!workshop.IsSystemUnlocked)
            return "工坊尚未解锁";
        GameManager game = GameManager.Instance;
        if (game == null)
            return "王国状态未初始化";
        if (definition.TechLevel > game.State.TechLevel)
            return "需进入" + definition.TechLevel.GetDescription();

        ResearchManager research = ResearchManager.Instance;
        for (int i = 0; i < definition.RequiredResearch.Count; i++)
        {
            Research prerequisite = definition.RequiredResearch[i];
            if (prerequisite == null)
                return "研究前置无效";
            if (research == null || !research.IsResearchCompleted(prerequisite.Id))
                return "需研究：" + prerequisite.Label;
        }

        for (int i = 0; i < definition.RequiredUpgrades.Count; i++)
        {
            WorkshopUpgrade prerequisite = definition.RequiredUpgrades[i];
            if (prerequisite == null)
                return "工坊前置无效";
            if (!workshop.IsPurchased(prerequisite))
                return "需工坊：" + prerequisite.Label;
        }

        ResourceManager resources = ResourceManager.Instance;
        if (resources == null)
            return "资源管理器未初始化";
        for (int i = 0; i < definition.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement =
                definition.ResourceRequirements[i];
            if (requirement.First == null)
                return "资源需求无效";
            if (requirement.Second <= ExpantaNum.Zero)
                continue;
            resources.States.TryGetValue(
                requirement.First, out ResourceState resourceState);
            ExpantaNum amount = resourceState == null
                ? ExpantaNum.Zero
                : resourceState.Amount;
            if (amount >= requirement.Second)
                continue;
            return "资源不足：" + requirement.First.Label + " 缺 " +
                (requirement.Second - amount).ToGameString();
        }

        canPurchase = true;
        return "可购买";
    }

    private void ConfigureWorkshopPaymentButton(WorkshopUpgrade definition, bool bindAction = true)
    {
        if (detailActionButton == null)
            return;
        bool purchased = WorkshopManager.Instance != null &&
            WorkshopManager.Instance.IsPurchased(definition);
        string availability = GetWorkshopAvailability(
            definition, out bool canPurchase);
        ConfigureActionButton(
            purchased ? "已购买" : canPurchase ? "购买工坊升级" : availability,
            () => PayWorkshopUpgrade(definition));
        detailActionButton.interactable = canPurchase;
    }

    private void PurchaseWorkshopFromRow(WorkshopUpgrade definition)
    {
        if (definition == null || WorkshopManager.Instance == null)
            return;
        bool purchased = WorkshopManager.Instance.TryPurchase(
            definition, out WorkshopPurchaseFailure failure);
        Debug.Log("[界面] 工坊行购买：id=" + definition.Id + "，结果=" + failure);
        if (purchased && populatedPage == "Workshop")
            RefreshWorkshopRows();
    }

    private void PayWorkshopUpgrade(WorkshopUpgrade definition)
    {
        if (definition == null || WorkshopManager.Instance == null)
            return;
        bool purchased = WorkshopManager.Instance.TryPurchase(definition, out WorkshopPurchaseFailure failure);
        Debug.Log("[界面] 工坊支付：id=" + definition.Id + "，结果=" + failure);
        if (purchased && populatedPage == "Workshop")
            RefreshWorkshopRows();
        ShowWorkshopDetails(definition, true);
    }

    private void RefreshSelectedResearchDetails(Research research)
    {
        if (research == null || detailBody == null)
            return;

        ResearchState state = null;
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.States.TryGetValue(research, out state);

        RefreshResearchDetailStatus(research, state);

        if (!RefreshRequirementRows(research.ResourceRequirements))
        {
            ShowResearchDetails(research, true);
            return;
        }

        ConfigureActionButton("加入研究队列", () => ResearchAction(research));
        if (detailActionButton != null)
            detailActionButton.interactable = IsResearchActionAvailable(research, state);
    }

    private void RefreshResearchDetailStatus(Research research, ResearchState state)
    {
        if (detailBody == null)
            return;
        const string marker = "\u5f53\u524d\u72b6\u6001\uff1a";
        int markerIndex = detailBody.text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return;
        int lineEnd = detailBody.text.IndexOf('\n', markerIndex);
        if (lineEnd < 0)
            lineEnd = detailBody.text.Length;
        string replacement = marker + GetResearchDetailStatus(research, state);
        string currentLine = detailBody.text.Substring(markerIndex, lineEnd - markerIndex);
        if (string.Equals(currentLine, replacement, StringComparison.Ordinal))
            return;
        detailBody.text = detailBody.text.Substring(0, markerIndex) + replacement +
            detailBody.text.Substring(lineEnd);
    }

    private void RefreshResearchDetailLiveValues(Research research)
    {
        if (research == null || detailBody == null)
            return;

        // During a drag only the live payment amounts need refreshing. Keep
        // the authored text, listeners and layout untouched so the gesture
        // does not compete with a full detail rebuild.
        ResearchState state = null;
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.States.TryGetValue(research, out state);
        RefreshResearchDetailStatus(research, state);
        RefreshRequirementRows(research.ResourceRequirements);
    }

    private static string GetResearchQueueActionLabel(Research research, ResearchState state)
    {
        if (state != null && state.Status == ResearchStatus.Completed)
            return "研究已完成";
        return ResearchManager.Instance != null &&
            (ResearchManager.Instance.ActiveResearch?.Definition == research ||
             ResearchManager.Instance.IsQueued(research))
            ? "删除研究队列"
            : "加入研究队列";
    }

    private static bool IsResearchActionAvailable(Research research, ResearchState state)
    {
        if (research == null || state == null || state.Status == ResearchStatus.Completed)
            return false;
        ResearchManager manager = ResearchManager.Instance;
        if (manager == null)
            return false;
        if (manager.ActiveResearch?.Definition == research || manager.IsQueued(research))
            return true;
        return manager.CanAccessResearch(research);
    }

    private static string GetResearchDetailStatus(Research research, ResearchState state)
    {
        if (research == null || state == null)
            return "\u7814\u7a76\u72b6\u6001\u672a\u521d\u59cb\u5316";
        if (state.Status == ResearchStatus.Completed)
            return "\u5df2\u5b8c\u6210";
        ResearchManager manager = ResearchManager.Instance;
        if (manager == null || GameManager.Instance == null || GameManager.Instance.State == null)
            return "\u7814\u7a76\u7ba1\u7406\u5668\u672a\u521d\u59cb\u5316";
        if (manager.ActiveResearch?.Definition == research)
            return "\u7814\u7a76\u4e2d\uff08\u70b9\u51fb\u53ef\u53d6\u6d88\uff09";
        if (manager.IsQueued(research))
        {
            if (state.Status == ResearchStatus.WaitingResources &&
                !manager.CanPayResearchCost(research, out string queuedBlocker))
                return "\u961f\u5217\u4e2d：" + queuedBlocker;
            return "\u961f\u5217\u4e2d\uff08\u70b9\u51fb\u53ef\u53d6\u6d88\uff09";
        }
        if (!manager.CanAccessResearch(research))
            return "\u9700\u8fdb\u5165" + research.TechLevel.GetDescription();
        IReadOnlyList<Research> prerequisites = research.Prerequisites;
        if (prerequisites != null)
            for (int i = 0; i < prerequisites.Count; i++)
            {
                Research prerequisite = prerequisites[i];
                if (prerequisite == null || !manager.IsResearchCompleted(prerequisite.Id))
                    return "\u9700\u5148\u5b8c\u6210\uff1a" +
                        (prerequisite == null ? "\u65e0\u6548\u524d\u7f6e" : prerequisite.Label);
            }
        if (!manager.CanPayResearchCost(research, out string paymentBlocker))
            return paymentBlocker;
        return "\u53ef\u52a0\u5165\u7814\u7a76\u961f\u5217\uff08\u8d44\u6e90\u4e0d\u8db3\u65f6\u4f1a\u7b49\u5f85\uff09";
    }

    private void HideBuildingRequirements()
    {
        RectTransform content = requirementContent == null ? requirementHost : requirementContent;
        if (flowHost != null)
        {
            flowHost.gameObject.SetActive(false);
            // Keep the authored FlowContent/Heading hierarchy. Only
            // remove rows generated for the previously selected item.
            RectTransform runtimeFlowContent = flowHost.Find("FlowContent") as RectTransform;
            if (runtimeFlowContent != null)
                for (int i = runtimeFlowContent.childCount - 1; i >= 0; i--)
                    if (runtimeFlowContent.GetChild(i).name.StartsWith("Flow_"))
                        Destroy(runtimeFlowContent.GetChild(i).gameObject);
            flowContent = runtimeFlowContent;
        }
        if (requirementHost == null || content == null)
            return;
        requirementHost.gameObject.SetActive(false);
        detailRequirementLabels.Clear();
        detailRequirementAmounts.Clear();
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child.name.StartsWith("Requirement_"))
                Destroy(child.gameObject);
        }
            if (detailScrollContent != null && detailScrollViewport != null)
            {
                detailScrollContent.sizeDelta = new Vector2(0f, Mathf.Max(1f, detailScrollViewport.rect.height));
                detailScrollContent.anchoredPosition = Vector2.zero;
            }
    }

    private void ShowBuildingRequirements(IReadOnlyList<Pair<Resource, ExpantaNum>> requirements, string heading = "建筑需求")
    {
        if (requirementHost == null)
            return;
        RectTransform content = requirementContent;
        if (content == null)
        {
            Debug.LogError("[王国界面] Authored RequirementContent is missing; requirement rows will not be generated.");
            return;
        }
        // Keep authored Heading/None children. Only runtime-generated
        // requirement rows are disposable.
        detailRequirementLabels.Clear();
        detailRequirementAmounts.Clear();
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child.name.StartsWith("Requirement_"))
                Destroy(child.gameObject);
        }
        requirementHost.gameObject.SetActive(true);
        heading = heading.Contains("研究") || heading.Contains("鐮旂┒") ? "研究支付需求" : "建筑建造需求";
        int validCount = 0;
        if (requirements != null)
            for (int i = 0; i < requirements.Count; i++)
                if (requirements[i] != null && requirements[i].First != null)
                    validCount++;
        const float requirementRowStep = 88f;
        // RequirementContent has its own heading row. Start resource rows
        // below that heading; a zero top offset made the first row paint
        // over "研究支付需求" in research details.
        const float firstRowTop = -56f;
        const float requirementContentBottomPadding = 4f;
        content.sizeDelta = new Vector2(0, requirementContentBottomPadding +
            72f + validCount * requirementRowStep);
        TMP_Text headingLabel = content.Find("Heading")?.GetComponent<TMP_Text>();
        if (headingLabel == null)
        {
            Debug.LogError("[王国界面] Authored RequirementContent is missing Heading.");
            return;
        }
        headingLabel.text = heading;
        headingLabel.gameObject.SetActive(true);
        if (requirements == null || requirements.Count == 0)
        {
            TMP_Text emptyLabel = content.Find("None")?.GetComponent<TMP_Text>();
            if (emptyLabel == null)
            {
                Debug.LogError("[王国界面] Authored RequirementContent is missing None state.");
                return;
            }
            emptyLabel.gameObject.SetActive(true);
            return;
        }
        content.Find("None")?.gameObject.SetActive(false);
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null)
                continue;
            GameObject row = CreateRuntimeDetailRow("Requirement_" + i,
                content, firstRowTop - i * requirementRowStep);
            if (row == null)
                continue;
            Transform iconTransform = row.transform.Find("Icon");
            Image icon = iconTransform == null ? null : iconTransform.GetComponent<Image>();
            if (icon == null)
            {
                Debug.LogError("[王国界面] Runtime requirement row is missing Icon Image.");
                continue;
            }
            icon.sprite = requirement.First.Sprite;
            icon.color = requirement.First.Color;
            icon.preserveAspect = true;
            TMP_Text label = row.transform.Find("Label")?.GetComponent<TMP_Text>();
            TMP_Text amount = row.transform.Find("Amount")?.GetComponent<TMP_Text>();
            if (label == null || amount == null)
                continue;
            SetTextIfChanged(label, requirement.First.Label);
            SetTextIfChanged(amount, FormatRequirementAmount(requirement));
            detailRequirementLabels[requirement.First] = label;
            detailRequirementAmounts[requirement.First] = amount;
        }
    }

    private bool RefreshRequirementRows(IReadOnlyList<Pair<Resource, ExpantaNum>> requirements)
    {
        if (requirementHost == null || requirementContent == null || !requirementHost.gameObject.activeSelf)
            return false;

        int validCount = 0;
        if (requirements != null)
            for (int i = 0; i < requirements.Count; i++)
                if (requirements[i] != null && requirements[i].First != null)
                    validCount++;

        int updated = 0;
        if (requirements != null)
            for (int i = 0; i < requirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement = requirements[i];
                if (requirement.First == null)
                    continue;
                if (!detailRequirementAmounts.TryGetValue(requirement.First, out TMP_Text amount) ||
                    !detailRequirementLabels.TryGetValue(requirement.First, out TMP_Text label))
                    return false;
                if (amount == null || label == null)
                    return false;
                SetTextIfChanged(label, requirement.First.Label);
                SetTextIfChanged(amount, FormatRequirementAmount(requirement));
                updated++;
            }
        return updated == validCount;
    }

    private string FormatRequirementAmount(Pair<Resource, ExpantaNum> requirement)
    {
        if (requirement.First == null)
            return string.Empty;

        ExpantaNum owned = ExpantaNum.Zero;
        ExpantaNum paid = ExpantaNum.Zero;
        ResourceManager resourceManager = resourceManagerCache;
        if (resourceManager == null)
            resourceManager = resourceManagerCache = FindObjectOfType<ResourceManager>();
        if (resourceManager != null && resourceManager.States.TryGetValue(requirement.First, out ResourceState resourceState))
            owned = resourceState.Amount;

        ResearchManager researchManager = researchManagerCache;
        if (researchManager == null)
            researchManager = researchManagerCache = FindObjectOfType<ResearchManager>();
        Research selected = selectedResearchNode;
        if (researchManager != null && selected != null &&
            researchManager.States.TryGetValue(selected, out ResearchState researchState))
            paid = researchState.GetPaidResourceCost(requirement.First);

        if (detailIsBuilding)
            return requirement.Second.ToGameString() + " (" + owned.ToGameString() + ")";

        if (selectedWorkshop != null)
            return requirement.Second.ToGameString() + " (" + owned.ToGameString() + ")";

        return paid.ToGameString() + " / " + requirement.Second.ToGameString() +
            " (" + owned.ToGameString() + ")";
    }

    private void PlaceRequirementsAfterDescription(
        int requirementCount,
        float? preservedScrollPosition = null,
        int flowCount = 0)
    {
        if (detailBody == null || requirementHost == null)
            return;
        if (requirementGesture == null)
        {
            requirementGesture = detailScrollViewport == null
                ? null
                : detailScrollViewport.GetComponent<UIDetailRequirementScrollGesture>();
            if (requirementGesture == null)
                requirementGesture = detailScrollViewport == null
                    ? null
                    : detailScrollViewport.gameObject.AddComponent<UIDetailRequirementScrollGesture>();
            if (requirementGesture == null)
            {
                Debug.LogError("[王国界面] DetailScrollViewport is missing; requirement drag owner was not created.");
                return;
            }
            requirementGesture.Initialize(detailScrollViewport, detailScrollContent);
            Debug.Log("[王国界面] Requirement gesture attached during detail layout");
        }
        // A late layout pass must not resurrect the nested ScrollRect.
        // The custom gesture remains the single owner for row-started
        // drags and moves the unified detail content.
        if (requirementGesture != null)
            requirementGesture.enabled = true;
        RectTransform body = detailBody.rectTransform;
        body.anchorMin = new Vector2(0, 1);
        body.anchorMax = new Vector2(1, 1);
        body.pivot = new Vector2(.5f, 1f);
        Canvas.ForceUpdateCanvases();
        float width = GetMeasuredDetailTextWidth(body);
        // Body is an authored DetailPanel child. Keep only a small
        // baseline after the final information line so the authored
        // requirement panel can sit directly beneath it.
        // TMP's preferred height ends at the final glyph line. Keep a real
        // baseline gap before the next authored section; without it, short
        // building descriptions can let the first requirement card visually
        // crowd the final "生产力需求" line.
        float preferredBodyHeight = detailBody.GetPreferredValues(detailBody.text, width, 1000f).y;
        float bodyHeight = Mathf.Max(96f, preferredBodyHeight + 18f);
        body.offsetMin = new Vector2(34f, -bodyHeight);
        body.offsetMax = new Vector2(-34f, 0f);

        // Body already contains the white "研究支付需求" line. Start
        // the requirement content directly below it; the old orange
        // Heading is hidden and must not leave another header gap.
        float sectionTop = bodyHeight;
        bool hasFlows = flowHost != null && flowHost.gameObject.activeSelf &&
            flowContent != null && flowCount > 0;
        if (hasFlows)
        {
            int flowRows = flowCount;
            // FlowContent is a normal section inside the single outer
            // DetailScrollContent. Its height is the actual heading plus
            // all rows; it is not a nested viewport.
            float flowHeight = 52f + flowRows * 88f;
            flowHost.anchorMin = new Vector2(0, 1);
            flowHost.anchorMax = new Vector2(1, 1);
            flowHost.pivot = new Vector2(.5f, 1f);
            flowHost.offsetMin = new Vector2(34f, -sectionTop - flowHeight);
            flowHost.offsetMax = new Vector2(-34f, -sectionTop);
            sectionTop += flowHeight + 8f;
        }
        else if (flowHost != null)
            flowHost.gameObject.SetActive(false);

        float detailHeight = detailScrollViewport == null ? 0f : detailScrollViewport.rect.height;
        // Requirements are the next normal section in the same outer
        // content. Do not clamp this section to a fake 420px viewport:
        // seven or more rows must increase the outer scroll range.
        float requirementHeight = requirementContent == null
            ? 90f + Mathf.Max(0, requirementCount) * 88f
            : Mathf.Max(90f + Mathf.Max(0, requirementCount) * 88f,
                requirementContent.rect.height);
        requirementHost.anchorMin = new Vector2(0, 1);
        requirementHost.anchorMax = new Vector2(1, 1);
        requirementHost.pivot = new Vector2(.5f, 1f);
        requirementHost.offsetMin = new Vector2(34f, -sectionTop - requirementHeight);
        requirementHost.offsetMax = new Vector2(-34f, -sectionTop);
        Canvas.ForceUpdateCanvases();
        // Reassert the content extent after the viewport is measured. A
        // legacy ScrollRect/layout pass can otherwise restore the old
        // 72px row height and leave only a 16px drag range.
        RectTransform measuredContent = requirementContent == null ? requirementHost : requirementContent;
        if (measuredContent != null)
        {
            int rowCount = Mathf.Max(0, requirementCount);
            measuredContent.sizeDelta = new Vector2(0f, 90f + rowCount * 88f);
            Canvas.ForceUpdateCanvases();
        }
        if (detailScrollContent != null)
        {
            // The only scrollable content is DetailScrollContent. Include
            // both sections and the actual requirement rows in its
            // measured extent.
            // Keep a real touch-safe tail after the last row. Without it,
            // a long list can technically have a tiny range but feel
            // immovable because its final row sits directly at the
            // viewport edge.
            const float detailContentBottomPadding = 72f;
            float contentHeight = Mathf.Max(detailHeight,
                sectionTop + requirementHeight + detailContentBottomPadding);
            detailScrollContent.sizeDelta = new Vector2(0f, contentHeight);
        }
        // Apply the position only after the final content height is known.
        // Otherwise the old height can produce a stale offset and expose
        // blank space above the first research line.
        if (requirementGesture != null)
            requirementGesture.SetNormalizedPosition(preservedScrollPosition.HasValue
                ? Mathf.Clamp01(preservedScrollPosition.Value)
                : 1f);
            float outerRangeY = detailScrollViewport == null || detailScrollContent == null
            ? 0f
            : Mathf.Max(0f, detailScrollContent.rect.height - detailScrollViewport.rect.height);
        Debug.Log($"[王国界面] Detail content bounds: viewport={(detailScrollViewport == null ? Vector2.zero : detailScrollViewport.rect.size)}, content={(detailScrollContent == null ? Vector2.zero : detailScrollContent.rect.size)}, rangeY={outerRangeY:0.0}, flowSection={(flowHost == null ? Vector2.zero : flowHost.rect.size)}, requirementSection={requirementHost.rect.size}, requirementRows={requirementCount}, active={requirementHost.gameObject.activeSelf}, customGestureEnabled={requirementGesture != null && requirementGesture.enabled}");
        Canvas.ForceUpdateCanvases();
    }

    private float GetMeasuredDetailTextWidth(RectTransform body)
    {
        const float horizontalPadding = 68f;
        float width = body == null ? 0f : body.rect.width;
        if (width <= 1f && detailScrollViewport != null)
            width = detailScrollViewport.rect.width - horizontalPadding;
        if (width <= 1f && detailScrollContent != null)
            width = detailScrollContent.rect.width - horizontalPadding;
        if (width <= 1f && detailPanel != null)
            width = detailPanel.rect.width - horizontalPadding;
        return Mathf.Max(320f, width);
    }

    private void ShowBuildingFlows(IReadOnlyList<Pair<Resource, ExpantaNum>> output, IReadOnlyList<Pair<Resource, ExpantaNum>> input)
    {
        if (flowHost == null)
            return;
        flowContent = flowHost.Find("FlowContent") as RectTransform;
        if (flowContent == null)
        {
            Debug.LogError("[王国界面] Authored FlowContent is missing; flow rows will not be generated.");
            return;
        }
        for (int i = flowContent.childCount - 1; i >= 0; i--)
        {
            Transform child = flowContent.GetChild(i);
            if (child.name.StartsWith("Flow_"))
                Destroy(child.gameObject);
        }
        flowContent.pivot = new Vector2(.5f, 1f);
        int flowCount = CountFlows(output) + CountFlows(input);
        flowContent.sizeDelta = new Vector2(0f, 52f + flowCount * 88f);
        flowHost.gameObject.SetActive(true);
        TMP_Text flowHeading = flowContent.Find("Heading")?.GetComponent<TMP_Text>();
        if (flowHeading == null)
        {
            Debug.LogError("[王国界面] Authored FlowContent is missing Heading.");
            return;
        }
        flowHeading.gameObject.SetActive(true);
        int rowIndex = 0;
        rowIndex = AddFlowRows(flowContent, output, rowIndex, Positive, false);
        AddFlowRows(flowContent, input, rowIndex, Error, true);
    }

    private bool RefreshFlowRows(IReadOnlyList<Pair<Resource, ExpantaNum>> output,
        IReadOnlyList<Pair<Resource, ExpantaNum>> input)
    {
        if (flowHost == null || flowContent == null || !flowHost.gameObject.activeSelf)
            return false;

        int expected = CountFlows(output) + CountFlows(input);
        if (flowContent.childCount - 1 != expected)
            return false;

        int index = 0;
        if (!RefreshFlowRowsInPlace(flowContent, output, ref index, false) ||
            !RefreshFlowRowsInPlace(flowContent, input, ref index, true))
            return false;
        return index == expected;
    }

    private static bool RefreshFlowRowsInPlace(RectTransform host,
        IReadOnlyList<Pair<Resource, ExpantaNum>> flows, ref int index, bool consumption)
    {
        if (flows == null)
            return true;
        for (int i = 0; i < flows.Count; i++)
        {
            Pair<Resource, ExpantaNum> flow = flows[i];
            if (flow.First == null)
                continue;
            Transform row = host.Find("Flow_" + index);
            TMP_Text label = row?.Find("Label")?.GetComponent<TMP_Text>();
            TMP_Text amount = row?.Find("Amount")?.GetComponent<TMP_Text>();
            if (row == null || label == null || amount == null)
                return false;
            SetTextIfChanged(label, flow.First.Label);
            SetTextIfChanged(amount, FormatFlowAmount(flow, consumption));
            index++;
        }
        return true;
    }

    private static int CountFlows(IReadOnlyList<Pair<Resource, ExpantaNum>> flows)
    {
        if (flows == null)
            return 0;
        int count = 0;
        for (int i = 0; i < flows.Count; i++)
            if (flows[i].First != null)
                count++;
        return count;
    }

    private int AddFlowRows(RectTransform host, IReadOnlyList<Pair<Resource, ExpantaNum>> flows, int startIndex, Color tint, bool consumption)
    {
        if (flows == null)
            return startIndex;
        int index = startIndex;
        for (int i = 0; i < flows.Count; i++)
        {
            Pair<Resource, ExpantaNum> flow = flows[i];
            if (flow.First == null)
                continue;
            GameObject row = CreateRuntimeDetailRow("Flow_" + index,
                host, -56f - index * 88f);
            if (row == null)
                continue;
            Transform iconTransform = row.transform.Find("Icon");
            Image icon = iconTransform == null ? null : iconTransform.GetComponent<Image>();
            if (icon == null)
            {
                Debug.LogError("[王国界面] Runtime flow row is missing Icon Image.");
                continue;
            }
            icon.sprite = flow.First.Sprite;
            icon.color = flow.First.Color;
            icon.preserveAspect = true;
            SetRowText(row, "Label", flow.First.Label);
            SetRowText(row, "Amount", FormatFlowAmount(flow, consumption), tint);
            index++;
        }
        return index;
    }

    private static string FormatFlowAmount(Pair<Resource, ExpantaNum> flow, bool consumption)=> (consumption ? "-" : "+") + flow.Second.ToGameString() + "/s";

    private static ExpantaNum NormalizeDisplayedNetRate(
        ExpantaNum production, ExpantaNum consumption)
    {
        return production.ApproximatelyEquals(consumption)
            ? ExpantaNum.Zero
            : production - consumption;
    }

    private void ConfigureActionButton(string label, UnityEngine.Events.UnityAction action)
    {
        if (!detailIsBuilding && selectedResearchNode != null)
        {
            ResearchState researchState = null;
            if (ResearchManager.Instance != null)
                ResearchManager.Instance.States.TryGetValue(selectedResearchNode, out researchState);
            label = GetResearchQueueActionLabel(selectedResearchNode, researchState);
        }
        if (detailActionButton == null)
            return;
        detailActionButton.gameObject.SetActive(true);
        detailActionButton.interactable = true;
        detailActionButton.onClick.RemoveAllListeners();
        bool isBuildingAction = detailIsBuilding;
        detailActionButton.onClick.AddListener(() =>
        {
            UIButtonSoundManager.Play(isBuildingAction
                ? UIButtonSoundManager.Sound.Purchase
                : UIButtonSoundManager.Sound.Detail);
            action();
        });
        TMP_Text text = GetDetailButtonText(detailActionButton, ref detailActionButtonText);
        if (text != null)
            text.text = detailIsBuilding
                ? (detailBuildingUpgrade ? "升级 1 个" : "建造 1 个")
                : label;
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] ResearchActionButton active={detailActionButton.gameObject.activeSelf} " +
            $"interactable={detailActionButton.interactable} label={label} " +
            $"research={(selectedResearchNode == null ? string.Empty : selectedResearchNode.Id)}");
#endif
    }

    private static TMP_Text GetDetailButtonText(Button button, ref TMP_Text cached)
    {
        if (cached == null && button != null)
            cached = button.GetComponentInChildren<TMP_Text>(true);
        return cached;
    }

    private void BuildOne(Building building)
    {
        if (BuildingManager.Instance == null)
        {
            ShowDetails("建筑", "建筑管理器尚未初始化。", building.Id);
            return;
        }
        if (BuildingManager.Instance.States.TryGetValue(building, out BuildingState state) &&
            state.Amount > ExpantaNum.Zero &&
            BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out _))
        {
            PerformBuildingAction(building, true);
            return;
        }
        bool success = BuildingManager.Instance.TryBuild(building, ExpantaNum.One, out BuildFailure failure);
        ShowBuildingDetails(building, true);
    }

    private void ResearchAction(Research research)
    {
        if (ResearchManager.Instance == null)
        {
            ShowDetails("研究", "研究管理器尚未初始化。", research.Id);
            return;
        }
        ResearchActionResult result = ResearchManager.Instance.HandleResearchAction(research);
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] ResearchActionInvoked id={research.Id} result={result} " +
            $"active={(ResearchManager.Instance.ActiveResearch?.Definition == research)} " +
            $"queueCount={ResearchManager.Instance.ResearchQueue.Count}");
#endif
        ShowResearchDetails(research, true);
        RefreshResearchQueueToolbar();
        researchQueueUiDirty = false;
        if (detailBody != null)
            detailBody.text += "\n\n执行结果：" + result.GetDescription();
    }

    private static void AppendCosts(StringBuilder builder, IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            builder.AppendLine("  无");
            return;
        }
        for (int i = 0; i < costs.Count; i++)
            if (costs[i].First != null)
                builder.AppendLine("  " + costs[i].First.Label + ": " + costs[i].Second.ToGameString());
    }

    private static void AppendFlows(StringBuilder builder, string prefix, IReadOnlyList<Pair<Resource, ExpantaNum>> flows)
    {
        if (flows == null || flows.Count == 0)
            return;
        builder.AppendLine(prefix);
        for (int i = 0; i < flows.Count; i++)
            if (flows[i].First != null)
                builder.AppendLine("  " + flows[i].First.Label + ": " + flows[i].Second.ToGameString() + "/s");
    }

    private static bool BuildingPrerequisitesMet(Building definition)
    {
        if (definition.RequiredResearch != null && definition.RequiredResearch.Count > 0)
        {
            if (ResearchManager.Instance == null)
                return false;
            for (int i = 0; i < definition.RequiredResearch.Count; i++)
                if (definition.RequiredResearch[i] == null || !ResearchManager.Instance.IsResearchCompleted(definition.RequiredResearch[i].Id))
                    return false;
        }
        if (definition.RequiredWorkshopUpgrades != null && definition.RequiredWorkshopUpgrades.Count > 0)
        {
            if (WorkshopManager.Instance == null)
                return false;
            for (int i = 0; i < definition.RequiredWorkshopUpgrades.Count; i++)
                if (definition.RequiredWorkshopUpgrades[i] == null || !WorkshopManager.Instance.IsPurchased(definition.RequiredWorkshopUpgrades[i]))
                    return false;
        }
        return true;
    }

}

/// <summary>
/// Touch-first scrolling for the long research/building requirement list.
/// The requirement viewport owns the gesture so icons and text cannot make
/// scrolling depend on which child received the initial raycast.
/// </summary>
public sealed class UIDetailRequirementScrollGesture : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler,
    IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform viewport;
    private RectTransform content;
    private bool pointerHeld;
    private bool dragging;
    private int pointerId = int.MinValue;
    private int movementSamples;
    private float movementPathLength;
    private bool loggedDrag;
    private bool loggedMovement;

    public bool IsDragging => dragging;

    public void Initialize(RectTransform targetViewport, RectTransform targetContent)
    {
        viewport = targetViewport;
        content = targetContent;
        pointerHeld = false;
        dragging = false;
        pointerId = int.MinValue;
        movementSamples = 0;
        movementPathLength = 0f;
        loggedDrag = false;
        loggedMovement = false;
        Canvas.ForceUpdateCanvases();
        ClampContent();
    }

    public float GetNormalizedPosition()
    {
        if (viewport == null || content == null)
            return 1f;
        float range = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        if (range <= 0.001f)
            return 1f;
        // DetailScrollContent uses a top anchor and top pivot. In this
        // coordinate system y=0 is the top and y=range is the bottom.
        return Mathf.Clamp01(1f - content.anchoredPosition.y / range);
    }

    public void SetNormalizedPosition(float value)
    {
        if (viewport == null || content == null)
            return;
        float range = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        content.anchoredPosition = new Vector2(0f, range * (1f - Mathf.Clamp01(value)));
        ClampContent();
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            viewport == null || content == null)
            return;
        pointerHeld = true;
        dragging = false;
        pointerId = eventData.pointerId;
        movementSamples = 0;
        movementPathLength = 0f;
        Debug.Log($"[王国界面] Requirement pointer down: pointer={pointerId}, position={eventData.position}, viewport={viewport.rect.size}, content={content.rect.size}, rangeY={Mathf.Max(0f, content.rect.height - viewport.rect.height)}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!pointerHeld || eventData.pointerId != pointerId)
            return;
        dragging = true;
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write($"[KingdomPerf] DetailDrag begin pointer={eventData.pointerId} touchCount={Input.touchCount}");
#endif
        if (!loggedDrag)
        {
            loggedDrag = true;
            Debug.Log($"[王国界面] Requirement drag started: viewport={viewport.rect.size}, content={content.rect.size}, rangeY={Mathf.Max(0f, content.rect.height - viewport.rect.height)}");
        }
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerHeld || eventData.pointerId != pointerId ||
            viewport == null || content == null)
            return;
        // Use Unity's event delta exactly once. The previous implementation
        // also polled Input in Update, which could apply the same movement a
        // second time and made short touch drags appear reversed or jumpy.
        float scale = Mathf.Max(.001f, Mathf.Abs(viewport.lossyScale.y));
        float deltaY = eventData.delta.y / scale;
        if (Mathf.Abs(deltaY) > 0.001f)
        {
            dragging = true;
            // Screen coordinates use a bottom-left origin, so a finger
            // moving up produces a positive deltaY. The top-anchored
            // content follows that delta toward its positive scroll range.
            content.anchoredPosition += new Vector2(0f, deltaY);
            ClampContent();
            movementSamples++;
            movementPathLength += Mathf.Abs(deltaY);
            if (!loggedMovement)
            {
                loggedMovement = true;
                Debug.Log($"[王国界面] Requirement drag moved: deltaY={deltaY:0.00}, position={content.anchoredPosition}, rangeY={Mathf.Max(0f, content.rect.height - viewport.rect.height)}");
            }
        }
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!pointerHeld || eventData.pointerId != pointerId)
            return;
        if (dragging)
            ClampContent();
        eventData.Use();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pointerHeld || eventData.pointerId != pointerId)
            return;
        if (dragging)
            eventData.eligibleForClick = false;
        Debug.Log($"[王国界面] Requirement drag ended: samples={movementSamples}, pathY={movementPathLength:0.00}, position={content.anchoredPosition}");
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write($"[KingdomPerf] DetailDrag end samples={movementSamples} pathY={movementPathLength:0.00} touchCount={Input.touchCount}");
#endif
        ResetPointer();
    }

    private void LateUpdate()
    {
        // Layout/Canvas rebuilds can rewrite anchoredPosition after the
        // detail presenter has measured the content. Re-apply the same
        // single outer-content clamp after layout so the viewport never
        // exposes positive top overscroll or loses the bottom range.
        if (!pointerHeld)
            ClampContent();
    }

    private void ClampContent()
    {
        if (viewport == null || content == null)
            return;
        float range = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        Vector2 position = content.anchoredPosition;
        Vector2 clamped = new Vector2(0f, Mathf.Clamp(position.y, 0f, range));
        // Only write when the value actually changes. Assigning the same
        // anchoredPosition every LateUpdate frame dirties the RectTransform
        // and forces a Canvas layout pass each frame, which is a sustained
        // CPU cost even when nothing moved.
        if (position != clamped)
            content.anchoredPosition = clamped;
    }

    private void ResetPointer()
    {
        pointerHeld = false;
        dragging = false;
        pointerId = int.MinValue;
        movementSamples = 0;
        movementPathLength = 0f;
        loggedDrag = false;
        loggedMovement = false;
    }
}
