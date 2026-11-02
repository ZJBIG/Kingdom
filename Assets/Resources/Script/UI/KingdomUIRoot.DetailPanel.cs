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
        public BuildingDef Building;
        public ExpantaNum Rate;
    }

    private readonly Dictionary<ResourceDef, TMP_Text> detailRequirementLabels = new();
    private readonly Dictionary<ResourceDef, TMP_Text> detailRequirementAmounts = new();
    private readonly List<Pair<ResourceDef, ExpantaNum>> buildingRequirementBuffer = new();
    private readonly List<Pair<ResourceDef, ExpantaNum>> buildingOutputFlowBuffer = new();
    private readonly List<Pair<ResourceDef, ExpantaNum>> buildingInputFlowBuffer = new();
    private readonly List<ResourceBuildingFlow> resourceProducerFlowBuffer = new();
    private readonly List<ResourceBuildingFlow> resourceConsumerFlowBuffer = new();
    private readonly StringBuilder resourceDetailTextBuilder = new();

    private void ShowDetails(string title, string description)
    {
        if (detailBody == null)
            return;
        HideRelicDetails();
        detailBody.fontSize = 30f;
        detailBuildingUpgrade = false;
        detailIsBuilding = false;
        selectedBuilding = null;
        selectedResearchNode = null;
        selectedResource = null;
        selectedWorkshop = null;
        selectedSectorDefinition = null;
        detailBody.text = title + "\n\n" + description;
        HideBuildingRequirements();
        if (detailActionButton != null)
            detailActionButton.gameObject.SetActive(false);
        HideUltraDoctrineButton();
        // Generic/workshop details do not go through the requirement
        // presenter. Reapply the body layout after the body was moved under
        // DetailScrollContent, otherwise it keeps the authored panel-space
        // offsets and can be clipped by the unified viewport.
        LayoutResourceDetailsBody();
        RefreshReviewControls();
    }

    private bool ShouldDisplayBuilding(BuildingDef definition)
    {
        if (BuildingManager.Instance != null)
            return BuildingManager.Instance.ShouldDisplay(definition);
        return BuildingPrerequisitesMet(definition);
    }

    private List<Pair<ResourceDef, ExpantaNum>> GetNextBuildingRequirements(
        BuildingDef building,
        BuildingState state,
        bool upgrading)
    {
        List<Pair<ResourceDef, ExpantaNum>> requirements = buildingRequirementBuffer;
        requirements.Clear();
        if (TryGetBuildingDetailStage(building, state, out WorkshopUpgradeDef nextStage))
        {
            if (nextStage != null)
                requirements.AddRange(nextStage.ResourceRequirements);
            return requirements;
        }
        ExpantaNum quantity = building is SectorBuildingDef ? ExpantaNum.One
            : GetSelectedBuildingQuantity(building, upgrading, false);

        if (upgrading)
        {
            BuildingManager.Instance.GetUpgradeResourceDeltas(building, quantity, requirements);
            return requirements;
        }

        ExpantaNum owned = state == null ? ExpantaNum.Zero : state.Amount;
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = building.ResourceRequirements[i];
            requirements.Add(new Pair<ResourceDef, ExpantaNum>(
                requirement.First,
                requirement.Second.GeometricSeriesCost(building.CostGrowth, owned, quantity)
                    * BuildingManager.GetConstructionCostMultiplier(building)));
        }
        return requirements;
    }

    private List<Pair<ResourceDef, ExpantaNum>> GetEffectiveBuildingFlows(
        BuildingDef building,
        bool output)
    {
        List<Pair<ResourceDef, ExpantaNum>> flows = output
            ? buildingOutputFlowBuffer
            : buildingInputFlowBuffer;
        flows.Clear();
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> source = output
            ? building.ResourceGenerationRates
            : building.ResourceConsumptionRates;
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum productionMultiplier = DisplayedBuildingOperatingEfficiency(building) *
            modifiers.GetBuildingProductionMultiplier(building) * modifiers.GlobalBuildingProductionMultiplier;
        for (int i = 0; i < source.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> flow = source[i];
            ExpantaNum multiplier = output
                ? DisplayedBuildingResourceMultiplier(building, flow.First)
                : productionMultiplier;
            flows.Add(new Pair<ResourceDef, ExpantaNum>(flow.First, flow.Second * multiplier));
        }
        return flows;
    }

    private static ExpantaNum DisplayedBuildingResourceMultiplier(BuildingDef building, ResourceDef resource) =>
        DisplayedBuildingOperatingEfficiency(building) *
        ProgressionModifierManager.Current.GetBuildingProductionMultiplier(building) *
        ProgressionModifierManager.Current.GlobalBuildingProductionMultiplier *
        ProgressionModifierManager.Current.GetBuildingResourceProductionMultiplier(building, resource) *
        ProgressionModifierManager.Current.GetResourceProductionMultiplier(resource) *
        (GameManager.Instance == null ? ExpantaNum.One : GameManager.Instance.State.Events.ProductionMultiplier) *
        ResourceManager.GetHappinessRewardMultiplier();

    private static ExpantaNum DisplayedBuildingOperatingEfficiency(BuildingDef building)
    {
        if (BuildingManager.Instance != null && BuildingManager.Instance.States.TryGetValue(building, out BuildingState state) &&
            state.Amount > ExpantaNum.Zero)
            return state.IsEnabled ? state.Efficiency : ExpantaNum.Zero;
        return ExpantaNum.One;
    }

    private static ExpantaNum DisplayedBuildingFood(BuildingDef building) => building.FoodProductionRate *
        DisplayedBuildingOperatingEfficiency(building) *
        ProgressionModifierManager.Current.GetBuildingProductionMultiplier(building) *
        ProgressionModifierManager.Current.GlobalBuildingProductionMultiplier *
        ProgressionModifierManager.Current.GlobalFoodProductionMultiplier *
        GameManager.Instance.State.Events.ProductionMultiplier;

    private static ExpantaNum DisplayedBuildingPower(BuildingDef building) => building.PowerContribution <= ExpantaNum.Zero
        ? building.PowerContribution * DisplayedBuildingOperatingEfficiency(building) : building.PowerContribution *
        DisplayedBuildingOperatingEfficiency(building) *
        ProgressionModifierManager.Current.PowerMultiplier *
        ProgressionModifierManager.Current.GetBuildingPowerProductionMultiplier(building) *
        GameManager.Instance.State.HappinessRewardMultiplier;

    private static ExpantaNum DisplayedBuildingLogistics(BuildingDef building) => building.LogisticsContribution <= ExpantaNum.Zero
        ? building.LogisticsContribution * DisplayedBuildingOperatingEfficiency(building) : building.LogisticsContribution *
        DisplayedBuildingOperatingEfficiency(building) *
        ProgressionModifierManager.Current.GlobalLogisticsMultiplier *
        ProgressionModifierManager.Current.GetBuildingLogisticsProductionMultiplier(building) *
        GameManager.Instance.State.HappinessRewardMultiplier;

    private static ExpantaNum DisplayedBuildingResearch(BuildingDef building) => building.ResearchPowerGranted *
        DisplayedBuildingOperatingEfficiency(building) *
        ProgressionModifierManager.Current.GetBuildingResearchPowerMultiplier(building) *
        ProgressionModifierManager.Current.GlobalResearchMultiplier *
        (ResearchManager.Instance == null ? ExpantaNum.One : ResearchManager.Instance.GlobalEfficiencyFactor) *
        GameManager.Instance.State.HappinessMultiplier;

    private void ShowBuildingDetails(BuildingDef building, bool preserveScrollPosition = false)
    {
        if (detailBody == null || building == null)
            return;
        if (!preserveScrollPosition) RecordOnboardingDetail(building.Id);
        HideRelicDetails();
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
        lastSelectedBuildingPrerequisiteSignature = null;
        selectedBuildingPrerequisiteOnlyRefresh = false;
        detailIsBuilding = true;
        BuildingState state = BuildingManager.Instance != null &&
            BuildingManager.Instance.States.TryGetValue(building, out BuildingState existing)
            ? existing
            : null;
        SetBuildingDetailBody(building, state);
        bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
            BuildingManager.Instance != null &&
            BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out _);
        detailBuildingUpgrade = upgrading;
        if (detailActionButton != null)
            detailActionButton.gameObject.SetActive(false);
        HideUltraDoctrineButton();
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> output = GetEffectiveBuildingFlows(building, true);
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> input = GetEffectiveBuildingFlows(building, false);
        ShowBuildingFlows(output, input);
        List<Pair<ResourceDef, ExpantaNum>> requirements = GetNextBuildingRequirements(building, state, upgrading);
        ultraDetailRequirementCount = requirements == null ? 0 : requirements.Count;
        ultraDetailFlowCount = CountFlows(output) + CountFlows(input);
        ShowBuildingRequirements(requirements, GetBuildingRequirementHeading(building, state, upgrading));
        PlaceRequirementsAfterDescription(
            requirements == null ? 0 : requirements.Count,
            preservedScrollPosition,
            CountFlows(output) + CountFlows(input));
        ConfigureUltraProjectDetails(building);
        RefreshReviewControls();
    }

    private void SetBuildingDetailBody(BuildingDef building, BuildingState state)
    {
        if (useCompactBuildingDetails) state = BuildingManager.Instance?.GetState(building);
        StringBuilder text = new();
        if (BuildingManager.IsMegastructure(building)) AppendBuildingIdentity(text, building, state);
        else text.AppendLine(building.Label);
        AppendBuildingCapacityRequirements(text, building);
        AppendBuildingBasicAttributes(text, building);
        if (state != null && state.Amount > ExpantaNum.Zero)
            AppendBuildingEfficiency(text, building);
        AppendBuildingStageDetails(text, building, state);
        if (useCompactBuildingDetails)
        {
            detailBody.text = text.ToString();
            if (state != null && state.IsEnabled && state.Amount > ExpantaNum.Zero && state.Efficiency <= ExpantaNum.Zero)
                detailBody.text += "\n停工原因：" + GetStoppedBuildingReason(building);
            detailBody.gameObject.SetActive(true);
            return;
        }
        if (state != null && state.IsEnabled && state.Amount > ExpantaNum.Zero && state.Efficiency <= ExpantaNum.Zero)
            text.AppendLine("停工原因：" + GetStoppedBuildingReason(building));
        text.AppendLine();
        text.AppendLine("研究前置");
        AppendResearchPrerequisites(text, building.RequiredResearch);
        text.AppendLine("工坊前置");
        AppendWorkshopPrerequisites(text, building.RequiredWorkshopUpgrades);
        text.AppendLine();
        text.AppendLine();
        detailBody.text = text.ToString();
    }

    private static bool TryGetBuildingDetailStage(BuildingDef building, BuildingState state,
        out WorkshopUpgradeDef nextStage)
    {
        nextStage = null;
        if (!(building is SectorBuildingDef) || state == null || state.Amount <= ExpantaNum.Zero)
            return false;
        GetSectorBuildingStage(building, out nextStage, out _, out _, out int total);
        return total > 0;
    }

    private static string GetBuildingRequirementHeading(BuildingDef building, BuildingState state, bool upgrading)
    {
        if (TryGetBuildingDetailStage(building, state, out WorkshopUpgradeDef nextStage))
            return nextStage == null ? "工程建设已完成" : nextStage.Label + " · 建造需求";
        return upgrading ? "建筑升级需求" : "建筑建造需求";
    }

    private static void AppendBuildingAttribute(StringBuilder text, string icon, ExpantaNum amount, string unit = "")
    {
        if (amount == ExpantaNum.Zero) return;
        text.Append(icon).Append(amount.ToGameString(showPositiveSign: true)).AppendLine(unit);
    }

    private static void AppendBuildingBasicAttributes(StringBuilder text, BuildingDef building)
    {
        text.AppendLine("每座建筑属性");
        if (!(building is SectorBuildingDef))
            AppendBuildingAttribute(text, LandIcon, -building.SpaceCost);
        AppendBuildingAttribute(text, "<sprite=\"HudProductivity\" index=0>", -building.ProductivityConsumption);
        AppendBuildingAttribute(text, "<sprite=\"HudProductivity\" index=0>", building.ProductivityGranted);
        AppendBuildingAttribute(text, "<sprite=\"HudPopulation\" index=0>", building.PopulationCapacityGranted);
        AppendBuildingAttribute(text, "<sprite=\"HudFoodStorage\" index=0>", building.FoodCapacityGranted);
        AppendBuildingAttribute(text, "<sprite=\"HudFood\" index=0>", building.FoodProductionRate, "/s");
        AppendBuildingAttribute(text, "<sprite=\"HudFood\" index=0>", -building.FoodConsumptionRate, "/s");
        AppendBuildingAttribute(text, "<sprite=\"HudResearchPower\" index=0>", building.ResearchPowerGranted);
        AppendBuildingAttribute(text, "<sprite=\"HudPower\" index=0>", building.PowerContribution);
        AppendBuildingAttribute(text, "<sprite=\"HudLogistics\" index=0>", building.LogisticsContribution);
        AppendBuildingAttribute(text, "舰队：", building.FleetPowerGranted);
        AppendBuildingAttribute(text, "防御：", building.DefensePowerGranted);
    }

    private void AppendBuildingEfficiency(StringBuilder text, BuildingDef building)
    {
        int outputCount = 0;
        foreach (var output in building.ResourceGenerationRates)
            if (output.First != null && output.Second > ExpantaNum.Zero) outputCount++;
        if (building.FoodProductionRate > ExpantaNum.Zero) outputCount++;
        if (building.ResearchPowerGranted > ExpantaNum.Zero) outputCount++;
        if (building.PowerContribution > ExpantaNum.Zero) outputCount++;
        if (building.LogisticsContribution > ExpantaNum.Zero) outputCount++;
        foreach (var output in building.ResourceGenerationRates)
            if (output.First != null && output.Second > ExpantaNum.Zero)
                AppendEfficiency(output.First.Label, DisplayedBuildingResourceMultiplier(building, output.First));
        if (building.FoodProductionRate > ExpantaNum.Zero)
            AppendEfficiency("<sprite=\"HudFood\" index=0>", DisplayedBuildingFood(building) / building.FoodProductionRate);
        if (building.ResearchPowerGranted > ExpantaNum.Zero)
            AppendEfficiency("<sprite=\"HudResearchPower\" index=0>", DisplayedBuildingResearch(building) / building.ResearchPowerGranted);
        if (building.PowerContribution > ExpantaNum.Zero)
            AppendEfficiency("<sprite=\"HudPower\" index=0>", DisplayedBuildingPower(building) / building.PowerContribution);
        if (building.LogisticsContribution > ExpantaNum.Zero)
            AppendEfficiency("<sprite=\"HudLogistics\" index=0>", DisplayedBuildingLogistics(building) / building.LogisticsContribution);
        if (outputCount == 0)
            text.AppendLine("<sprite=\"HudProductionEfficiency\" index=0>" + (DisplayedBuildingOperatingEfficiency(building) * 100).ToGameString() + "%");

        void AppendEfficiency(string label, ExpantaNum multiplier)
        {
            text.Append("<sprite=\"HudProductionEfficiency\" index=0>");
            if (outputCount > 1) text.Append(label).Append("：");
            text.Append((multiplier * 100).ToGameString()).AppendLine("%");
        }
    }

    private void AppendBuildingStageDetails(StringBuilder text, BuildingDef building, BuildingState state)
    {
        if (!TryGetBuildingDetailStage(building, state, out WorkshopUpgradeDef nextStage) || nextStage == null)
            return;
        text.AppendLine("下一阶段：" + nextStage.Label);
        text.AppendLine(nextStage.Description);
        text.AppendLine("建成后增益：");
        AppendWorkshopEffects(text, nextStage.Effects);
    }

    private static void AppendBuildingIdentity(StringBuilder text, BuildingDef building, BuildingState state)
    {
        WorkshopUpgradeDef latest = null;
        if (state != null && state.Amount > ExpantaNum.Zero)
            GetSectorBuildingStage(building, out _, out latest, out _, out _);
        text.AppendLine("当前阶段：" + (latest == null ? building.Label : latest.Label));
        text.AppendLine(latest == null ? building.Description : latest.Description);
    }

    private void AppendBuildingCapacityRequirements(StringBuilder text, BuildingDef building)
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager == null || GameManager.Instance == null) return;
        bool upgrading = manager.TryGetUnlockedUpgradeTarget(building, out BuildingDef target);
        ExpantaNum quantity = building is SectorBuildingDef ? ExpantaNum.One
            : GetSelectedBuildingQuantity(building, upgrading, false);
        if (TryGetBuildingDetailStage(building, manager.GetState(building), out WorkshopUpgradeDef nextStage))
        {
            if (nextStage == null) text.AppendLine("工程已全部建成");
            if (nextStage != null)
            {
                string availability = GetWorkshopAvailability(nextStage, out _);
                if (!string.IsNullOrEmpty(availability)) text.AppendLine(availability);
            }
            return;
        }
        ExpantaNum space = upgrading ? (target.SpaceCost - building.SpaceCost) * quantity
            : building is SectorBuildingDef ? ExpantaNum.Zero : building.SpaceCost * quantity;
        text.AppendLine((upgrading ? "本次升级 ×" : "本次建造 ×") + quantity.ToGameString());
        if (upgrading) text.AppendLine("升级为：" + target.Label);
        bool landShortage = space > ExpantaNum.Zero && space > GameManager.Instance.State.AvailableTerritory;
        if (!(building is SectorBuildingDef))
            AppendBuildingCapacityLine(text, landShortage ? LandShortageIcon : LandIcon, space,
                GameManager.Instance.State.AvailableTerritory);
        AppendBuildingCapacityLine(text, "<sprite=\"HudProductivity\" index=0>", manager.UsedProductivity,
            manager.TotalProductivity);
    }

    private static void AppendBuildingCapacityLine(StringBuilder text, string label, ExpantaNum required, ExpantaNum available)
    {
        if (required < ExpantaNum.Zero)
        {
            text.Append(label).Append(" 释放 ").AppendLine(required.Abs().ToGameString());
            return;
        }
        ExpantaNum shortage = required > ExpantaNum.Zero
            ? ExpantaNum.Max(ExpantaNum.Zero, required - available) : ExpantaNum.Zero;
        text.Append(label).Append(" 需求 ").Append(required.ToGameString())
            .Append("，缺少 ").AppendLine(shortage.ToGameString());
    }

    private void RefreshSelectedBuildingDetails(BuildingDef building)
    {
        BuildingManager buildingManager = BuildingManager.Instance;
        if (building == null || detailBody == null || buildingManager == null)
            return;

        if (selectedBuildingPrerequisiteOnlyRefresh)
        {
            selectedBuildingPrerequisiteOnlyRefresh = false;
            RefreshBuildingPrerequisiteText(building);
            return;
        }
        selectedBuildingPrerequisiteOnlyRefresh = false;

        BuildingState state = buildingManager.States.TryGetValue(building, out BuildingState current)
            ? current
            : null;
        bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
            buildingManager.TryGetUnlockedUpgradeTarget(building, out _);

        // A prerequisite/upgrade transition changes the structure of the
        // detail panel. Rebuild only for that structural change; ordinary
        // amount/rate changes update existing rows in place so a gesture
        // never loses its content or pointer state.
        List<Pair<ResourceDef, ExpantaNum>> requirements = GetNextBuildingRequirements(building, state, upgrading);
        TMP_Text requirementHeading = requirementContent == null ? null
            : requirementContent.Find("Heading")?.GetComponent<TMP_Text>();
        if (detailBuildingUpgrade != upgrading || requirementHeading == null ||
            requirementHeading.text != GetBuildingRequirementHeading(building, state, upgrading) || !RefreshRequirementRows(requirements))
        {
            ShowBuildingDetails(building, true);
            return;
        }

        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> output = GetEffectiveBuildingFlows(building, true);
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> input = GetEffectiveBuildingFlows(building, false);
        ultraDetailRequirementCount = requirements == null ? 0 : requirements.Count;
        ultraDetailFlowCount = CountFlows(output) + CountFlows(input);
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

        string previousBody = detailBody.text;
        SetBuildingDetailBody(building, state);
        if (!string.Equals(previousBody, detailBody.text, StringComparison.Ordinal))
            ResizeDetailBodyIfNeeded(ultraDetailRequirementCount, ultraDetailFlowCount);
        if (detailActionButton != null)
            detailActionButton.gameObject.SetActive(false);
        HideUltraDoctrineButton();
        ConfigureUltraProjectDetails(building);
    }

#if UNITY_EDITOR
    public void ShowResourceDetailsForEditor(ResourceDef resource) =>
        ShowResourceDetails(resource);
    public void ShowDetailsForEditor(string title, string description) =>
        ShowDetails(title, description);
#endif

    private void ShowResourceDetails(ResourceDef resource)
    {
        if (resource != null) RecordOnboardingDetail(resource.Id);
        if (detailBody == null || resource == null)
            return;
        HideRelicDetails();

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
        HideUltraDoctrineButton();

        RefreshResourceDetails(resource);
        RefreshReviewControls();
    }

    private void RefreshResourceDetails(ResourceDef resource)
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
        ExpantaNum net = production - consumption;
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
            text.AppendLine("       --" + ReviewLink("building", producers[i].Building.Id, producers[i].Building.Label) +
                ": " +
                ("+" + producers[i].Rate.ToGameString() + "/s").Colorize(Positive));

        text.AppendLine(("消耗: -" + consumption.ToGameString() + "/s").Colorize(Error));
        for (int i = 0; i < consumers.Count; i++)
            text.AppendLine("       --" + ReviewLink("building", consumers[i].Building.Id, consumers[i].Building.Label) +
                ": " +
                ("-" + consumers[i].Rate.ToGameString() + "/s").Colorize(Error));

        text.AppendLine(("净产出: " + (net >= ExpantaNum.Zero ? "+" : "") + net.ToGameString() + "/s").Colorize(net >= ExpantaNum.Zero ? Positive : Error));
        text.AppendLine(GetResourceSupplyStatus(state, consumption));
        AppendResourceSourceCandidates(text, resource);
        detailBody.text = text.ToString();
        detailBody.richText = true;
        detailBody.fontSize = 30f;
        LayoutResourceDetailsBody();
        if (requirementGesture != null)
            requirementGesture.SetNormalizedPosition(preservedScrollPosition);
    }

    private static string GetResourceSupplyStatus(ResourceState state, ExpantaNum consumption)
    {
        if (state == null)
            return "当前供给状态：未初始化";
        if (consumption <= ExpantaNum.Zero && state.TickPotentialConsumptionRate <= ExpantaNum.Zero)
            return "当前供给状态：无消耗需求";

        ExpantaNum satisfaction = state.GetTickSatisfactionOrFallback();
        if (satisfaction >= ExpantaNum.One)
            return "当前供给状态：供给充足".Colorize(Positive);
        if (satisfaction > ExpantaNum.Zero)
            return ("当前供给状态：供给不足（满足度 " +
                (satisfaction * new ExpantaNum(100)).ToGameString() + "%）").Colorize(Error);
        return "当前供给状态：暂无供给".Colorize(Error);
    }

    private void RefreshResourceBuildingFlows(ResourceDef resource)
    {
        resourceProducerFlowBuffer.Clear();
        resourceConsumerFlowBuffer.Clear();
        if (resource == null)
            return;
        BuildingManager buildingManager = BuildingManager.Instance;
        if (buildingManager == null)
            return;

        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        foreach (KeyValuePair<BuildingDef, BuildingState> entry in buildingManager.States)
        {
            BuildingDef building = entry.Key;
            BuildingState state = entry.Value;
            if (building == null || state == null)
                continue;

            if (state.Amount <= ExpantaNum.Zero || state.Efficiency <= ExpantaNum.Zero)
                continue;

            ExpantaNum scale = state.Amount * state.Efficiency;
            ExpantaNum productionRate = ExpantaNum.Zero;
            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> productionFlows =
                building.ResourceGenerationRates;
            for (int i = 0; i < productionFlows.Count; i++)
            {
                Pair<ResourceDef, ExpantaNum> flow = productionFlows[i];
                if (flow.First == resource)
                    productionRate += flow.Second;
            }

            ExpantaNum consumptionRate = ExpantaNum.Zero;
            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> consumptionFlows =
                building.ResourceConsumptionRates;
            for (int i = 0; i < consumptionFlows.Count; i++)
            {
                Pair<ResourceDef, ExpantaNum> flow = consumptionFlows[i];
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
        BuildingDef building,
        ExpantaNum rate,
        ExpantaNum scale,
        ExpantaNum productionMultiplier,
        ResourceDef resource,
        ProgressionModifierState modifiers,
        bool production)
    {
        if (rate <= ExpantaNum.Zero)
            return;
        rate *= scale;
        rate *= productionMultiplier;
        if (production)
        {
            rate *= modifiers.GetResourceProductionMultiplier(resource)
                * modifiers.GetBuildingResourceProductionMultiplier(building, resource)
                * GameManager.Instance.State.Events.ProductionMultiplier;
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
        float bodyHeight = Mathf.Max(96f,
            detailBody.GetPreferredValues(detailBody.text, width, 1000f).y + 20f);
        // The body height only changes when the line count changes; ordinary
        // value refreshes keep the same height. Skip the two full-canvas
        // immediate layout passes unless the height actually changed, otherwise
        // selecting a resource forces Canvas layout about 10 times per second.
        bool heightChanged = Mathf.Abs(bodyHeight - body.rect.height) > 1f;
        if (heightChanged)
            Canvas.ForceUpdateCanvases();
        float resourceToolbarHeight = CanShowProductionEntry() && resourceTreeSurface != null ? 80f : 0f;
        body.offsetMin = new Vector2(34f, -bodyHeight - resourceToolbarHeight);
        body.offsetMax = new Vector2(-34f, -resourceToolbarHeight);
        float contentHeight = 24f + bodyHeight + resourceToolbarHeight;
        RectTransform relicSection = RelicActionsRectForLayout;
        if (relicSection != null)
        {
            float relicHeight = RelicActionsPreferredHeight;
            relicSection.offsetMax = new Vector2(-34f, -bodyHeight - resourceToolbarHeight - 24f);
            relicSection.offsetMin = new Vector2(34f, -bodyHeight - resourceToolbarHeight - 24f - relicHeight);
            contentHeight += relicHeight + 72f;
        }
        if (detailScrollContent != null)
            detailScrollContent.sizeDelta = new Vector2(0f, Mathf.Max(detailScrollViewport.rect.height, contentHeight));
        if (heightChanged)
            Canvas.ForceUpdateCanvases();
    }

    private void ShowResearchDetails(ResearchDef research, bool preserveScrollPosition = false)
    {
        if (detailBody == null || !CanInspectResearch(research))
            return;
        if (!preserveScrollPosition) RecordOnboardingDetail(research.Id);
        HideRelicDetails();
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
        text.AppendLine("效果");
        if (research.AdvancesTechLevel)
            text.AppendLine("进入" + research.TechLevel.GetDescription() + "，开放该时代的研究与生产内容。");
        if (!research.AdvancesTechLevel || research.Effects.Count > 0)
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
        ConfigureActionButton(GetResearchQueueActionLabel(research, state), () => ResearchAction(research));
        if (detailActionButton != null)
            detailActionButton.interactable = IsResearchActionAvailable(research, state);
        CaptureResearchRefreshSignatures();
        RefreshReviewControls();
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
        StringBuilder builder, IReadOnlyList<ResearchDef> prerequisites)
    {
        if (prerequisites == null || prerequisites.Count == 0)
        {
            builder.AppendLine("  无");
            return;
        }
        for (int i = 0; i < prerequisites.Count; i++)
        {
            ResearchDef prerequisite = prerequisites[i];
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
        StringBuilder builder, IReadOnlyList<WorkshopUpgradeDef> prerequisites)
    {
        if (prerequisites == null || prerequisites.Count == 0)
        {
            builder.AppendLine("  无");
            return;
        }
        for (int i = 0; i < prerequisites.Count; i++)
        {
            WorkshopUpgradeDef prerequisite = prerequisites[i];
            if (prerequisite == null)
            {
                builder.AppendLine("  ○ 无效工坊升级");
                continue;
            }
            builder.AppendLine("  " + (IsWorkshopPurchased(prerequisite) ? "✓ " : "○ ") +
                prerequisite.Label);
        }
    }

    private static bool IsResearchCompleted(ResearchDef research)
    {
        ResearchManager manager = ResearchManager.Instance;
        return research != null && manager != null && manager.IsResearchCompleted(research.Id);
    }

    private static bool IsWorkshopPurchased(WorkshopUpgradeDef upgrade)
    {
        WorkshopManager manager = WorkshopManager.Instance;
        return upgrade != null && manager != null && manager.IsPurchased(upgrade);
    }

    private static void AppendResearchConnections(StringBuilder builder,
        ResearchDef research)
    {
        if (research == null)
            return;

        int buildingCount = 0;
        IReadOnlyList<BuildingDef> buildings = DataBase<BuildingDef>.All;
        for (int i = 0; i < buildings.Count && buildingCount < 3; i++)
        {
            BuildingDef building = buildings[i];
            if (building == null || building is SectorBuildingDef ||
                !ContainsResearch(building.RequiredResearch, research))
                continue;
            builder.AppendLine("  关联建筑：" + building.Label);
            buildingCount++;
        }

        int researchCount = 0;
        IReadOnlyList<ResearchDef> researches = DataBase<ResearchDef>.All;
        for (int i = 0; i < researches.Count && researchCount < 3; i++)
        {
            ResearchDef next = researches[i];
            if (next == null || next == research ||
                !ContainsResearch(next.Prerequisites, research))
                continue;
            builder.AppendLine("  研究后续方向：" + next.Label);
            researchCount++;
        }

        if (buildingCount == 0 && researchCount == 0)
            builder.AppendLine("  没有以本研究为直接前置的普通建筑或后续研究。");
    }

    private static bool ContainsResearch(
        IReadOnlyList<ResearchDef> values, ResearchDef target)
    {
        if (values == null || target == null)
            return false;
        for (int i = 0; i < values.Count; i++)
            if (values[i] == target)
                return true;
        return false;
    }

    private void ShowWorkshopDetails(WorkshopUpgradeDef definition,
        bool preserveScrollPosition = false)
    {
        if (detailBody == null || definition == null)
            return;
        if (!preserveScrollPosition) RecordOnboardingDetail(definition.Id);
        HideRelicDetails();

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
        RefreshReviewControls();
    }

    private bool SetWorkshopDetailBody(WorkshopUpgradeDef definition)
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
        if (definition.RequiredBuilding != null)
            text.AppendLine("工程本体：" + definition.RequiredBuilding.Label);
        if (definition.RequiredSector != null)
            text.AppendLine("占领星区：" + definition.RequiredSector.Label);
        text.AppendLine();
        text.AppendLine("效果");
        AppendWorkshopEffects(text, definition.Effects);
        AppendWorkshopBenefitPreview(text, definition);
        if (definition.RequiredBuilding is SectorBuildingDef project)
        {
            var structures = new List<WorkshopUpgradeDef>();
            foreach (WorkshopUpgradeDef candidate in DataBase<WorkshopUpgradeDef>.All)
                if (candidate.RequiredBuilding == project) structures.Add(candidate);
            structures.Sort(CompareSectorStages);
            text.AppendLine();
            text.AppendLine("工程建设记录");
            BuildingState bodyState = BuildingManager.Instance.GetState(project);
            bool bodyBuilt = bodyState != null && bodyState.Amount > ExpantaNum.Zero;
            text.AppendLine(project.Label + (bodyBuilt ? "：已建成" : "：待建设"));
            foreach (WorkshopUpgradeDef structure in structures)
                text.AppendLine(structure.Label + (WorkshopManager.Instance.IsPurchased(structure)
                    ? bodyBuilt ? "：已建成" : "：记录保留，重建本体后恢复"
                    : "：待建设"));
        }
        string body = text.ToString();
        if (detailBody.text == body)
            return false;
        detailBody.text = body;
        return true;
    }

    private static void AppendWorkshopBenefitPreview(StringBuilder text, WorkshopUpgradeDef definition)
    {
        WorkshopManager manager = WorkshopManager.Instance;
        if (manager == null)
            return;
        text.AppendLine();
        if (manager.IsPurchased(definition))
        {
            BuildingState bodyState = definition.RequiredBuilding == null ? null
                : BuildingManager.Instance.GetState(definition.RequiredBuilding);
            if (definition.RequiredBuilding != null &&
                (bodyState == null || bodyState.Amount <= ExpantaNum.Zero))
                text.AppendLine("结构记录已保留；重建工程本体后恢复收益，无需重复购买。");
            return;
        }
        IReadOnlyList<WorkshopBenefitRatePreview> rates = manager.GetPurchaseBenefitPreview(definition);
        if (rates.Count == 0) return;
        text.AppendLine("购买后效果预览（按当前持有数量和效率）");
        text.AppendLine("资源产出包含当前幸福加成；持续原料不受该加成放大。");
        for (int i = 0; i < rates.Count; i++)
        {
            WorkshopBenefitRatePreview rate = rates[i];
            string target = rate.Building != null ? rate.Building.Label : "基础供给";
            string metric = rate.Kind switch
            {
                WorkshopBenefitRateKind.ResourceProduction => rate.Resource.Label + "产出",
                WorkshopBenefitRateKind.ResourceConsumption => rate.Resource.Label + "持续原料",
                WorkshopBenefitRateKind.FoodProduction => "粮食产出",
                WorkshopBenefitRateKind.ResearchPower => "有效研究力",
                WorkshopBenefitRateKind.PowerProduction => "电力供给",
                WorkshopBenefitRateKind.LogisticsProduction => "物流供给",
                _ => string.Empty
            };
            string unit = rate.Kind == WorkshopBenefitRateKind.PowerProduction ||
                rate.Kind == WorkshopBenefitRateKind.LogisticsProduction ? string.Empty : "/s";
            text.Append("  ").Append(target).Append(" / ").Append(metric).Append(": ")
                .Append(rate.Before.ToGameString()).Append(" → ").Append(rate.After.ToGameString())
                .Append(unit).Append("（变化 ").Append(rate.Change > ExpantaNum.Zero ? "+" : string.Empty)
                .Append(rate.Change.ToGameString()).Append(unit).AppendLine("）");
        }
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
            string icon = effect.Type switch
            {
                WorkshopEffectType.GlobalResearchMultiplier or WorkshopEffectType.BuildingResearchPowerMultiplier => "<sprite=\"HudResearchPower\" index=0> ",
                WorkshopEffectType.PowerMultiplier or WorkshopEffectType.BuildingPowerProductionMultiplier => "<sprite=\"HudPower\" index=0> ",
                WorkshopEffectType.GlobalLogisticsMultiplier or WorkshopEffectType.BuildingLogisticsProductionMultiplier => "<sprite=\"HudLogistics\" index=0> ",
                WorkshopEffectType.GlobalFoodProductionMultiplier => "<sprite=\"HudFood\" index=0> ",
                WorkshopEffectType.PopulationGrowthMultiplier => "<sprite=\"HudPopulation\" index=0> ",
                WorkshopEffectType.TerritoryGranted => LandIcon + " ",
                _ => string.Empty
            };
            builder.AppendLine("  " + icon + effect.Type.GetDescription() + " / " + target + ": " + effect.Value);
        }
    }

    private static string GetWorkshopAvailability(
        WorkshopUpgradeDef definition,
        out bool canPurchase)
    {
        canPurchase = false;
        WorkshopManager workshop = WorkshopManager.Instance;
        if (definition == null || workshop == null)
            return "工坊未初始化";
        WorkshopPurchaseFailure failure = workshop.GetPurchaseFailure(definition);
        switch (failure)
        {
            case WorkshopPurchaseFailure.None:
                canPurchase = true;
                return "可购买";
            case WorkshopPurchaseFailure.AlreadyPurchased: return "已拥有";
            case WorkshopPurchaseFailure.SystemLocked: return string.Empty;
            case WorkshopPurchaseFailure.TechnologyInsufficient:
                return "需进入" + definition.TechLevel.GetDescription();
            case WorkshopPurchaseFailure.BuildingMissing:
                return "需建造：" + (definition.RequiredBuilding == null ? "工程本体" : definition.RequiredBuilding.Label);
            case WorkshopPurchaseFailure.SectorNotOccupied:
                return "需占领：" + (definition.RequiredSector == null ? "前置星区" : definition.RequiredSector.Label);
            case WorkshopPurchaseFailure.ResearchIncomplete:
                foreach (ResearchDef required in definition.RequiredResearch)
                    if (required == null || !ResearchManager.Instance.IsResearchCompleted(required.Id))
                        return "需研究：" + (required == null ? "无效前置" : required.Label);
                return "研究前置未完成";
            case WorkshopPurchaseFailure.UpgradeIncomplete:
                foreach (WorkshopUpgradeDef required in definition.RequiredUpgrades)
                    if (required == null || !workshop.IsPurchased(required))
                        return "需工坊：" + (required == null ? "无效前置" : required.Label);
                return "工坊前置未完成";
            case WorkshopPurchaseFailure.ResourceInsufficient:
                var totals = new Dictionary<ResourceDef, ExpantaNum>();
                foreach (Pair<ResourceDef, ExpantaNum> requirement in definition.ResourceRequirements)
                {
                    if (requirement.First == null || requirement.Second <= ExpantaNum.Zero) continue;
                    totals[requirement.First] = totals.TryGetValue(requirement.First, out ExpantaNum cost)
                        ? cost + requirement.Second : requirement.Second;
                }
                foreach (KeyValuePair<ResourceDef, ExpantaNum> cost in totals)
                {
                    ResourceManager.Instance.States.TryGetValue(cost.Key, out ResourceState resourceState);
                    ExpantaNum amount = resourceState == null ? ExpantaNum.Zero : resourceState.Amount;
                    if (amount < cost.Value)
                        return "资源不足：" + cost.Key.Label + " 缺 " + (cost.Value - amount).ToGameString();
                }
                return "资源不足";
            default: return "工坊定义无效";
        }
    }

    private void ConfigureWorkshopPaymentButton(WorkshopUpgradeDef definition)
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
        detailActionButton.gameObject.SetActive(!string.IsNullOrEmpty(availability));
    }

    private void PurchaseWorkshopUpgrade(WorkshopUpgradeDef definition)
    {
        if (definition == null || WorkshopManager.Instance == null)
            return;
        bool purchased = WorkshopManager.Instance.TryPurchase(
            definition, out WorkshopPurchaseFailure failure);
        Debug.Log("[界面] 工坊购买：id=" + definition.Id + "，结果=" + failure);
        if (purchased)
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.WorkshopPurchase);
        else if (failure != WorkshopPurchaseFailure.SystemLocked)
            ShowTooltip("购买失败：" + GetWorkshopAvailability(definition, out _));
        if (purchased && selectedWorkshop != null && SetWorkshopDetailBody(selectedWorkshop))
            ResizeDetailBodyIfNeeded(selectedWorkshop.ResourceRequirements?.Count ?? 0, 0);
        if (purchased && populatedPage == "Workshop")
            RefreshWorkshopRows();
    }

    private void PayWorkshopUpgrade(WorkshopUpgradeDef definition)
    {
        if (definition == null || WorkshopManager.Instance == null)
            return;
        PurchaseWorkshopUpgrade(definition);
        ShowWorkshopDetails(definition, true);
    }

    private void RefreshSelectedResearchDetails(ResearchDef research)
    {
        if (research == null || detailBody == null)
            return;

        ResearchState state = null;
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.States.TryGetValue(research, out state);

        RefreshResearchPrerequisiteText(research);

        if (!RefreshRequirementRows(research.ResourceRequirements))
        {
            ShowResearchDetails(research, true);
            return;
        }

        ConfigureActionButton(GetResearchQueueActionLabel(research, state), () => ResearchAction(research));
        if (detailActionButton != null)
            detailActionButton.interactable = IsResearchActionAvailable(research, state);
    }

    private void RefreshResearchPrerequisiteText(ResearchDef research)
    {
        if (detailBody == null || research == null)
            return;
        StringBuilder rows = new();
        AppendResearchPrerequisites(rows, research.Prerequisites);
        ReplaceDetailSection("研究前置\n", "\n效果\n", rows.ToString());
    }

    private void RefreshBuildingPrerequisiteText(BuildingDef building)
    {
        if (detailBody == null || building == null)
            return;
        StringBuilder researchRows = new();
        AppendResearchPrerequisites(researchRows, building.RequiredResearch);
        ReplaceDetailSection("研究前置\n", "工坊前置\n", researchRows.ToString());

        StringBuilder workshopRows = new();
        AppendWorkshopPrerequisites(workshopRows, building.RequiredWorkshopUpgrades);
        ReplaceDetailSection("工坊前置\n", null, workshopRows.ToString());
    }

    private void ReplaceDetailSection(string heading, string nextHeading, string replacement)
    {
        if (detailBody == null || string.IsNullOrEmpty(heading))
            return;
        string current = detailBody.text;
        int start = current.IndexOf(heading, StringComparison.Ordinal);
        if (start < 0)
            return;
        start += heading.Length;
        int end = string.IsNullOrEmpty(nextHeading) ? current.Length
            : current.IndexOf(nextHeading, start, StringComparison.Ordinal);
        if (end < start || string.Equals(current.Substring(start, end - start), replacement, StringComparison.Ordinal))
            return;
        detailBody.text = current.Substring(0, start) + replacement + current.Substring(end);
    }

    private void RefreshResearchDetailLiveValues(ResearchDef research)
    {
        if (research == null || detailBody == null)
            return;

        // During a drag only the live payment amounts need refreshing. Keep
        // the authored text, listeners and layout untouched so the gesture
        // does not compete with a full detail rebuild.
        RefreshRequirementRows(research.ResourceRequirements);
    }

    private static string GetResearchQueueActionLabel(ResearchDef research, ResearchState state)
    {
        if (state != null && state.Status == ResearchStatus.Completed)
            return "研究已完成";
        ResearchManager manager = ResearchManager.Instance;
        if (manager?.ActiveResearch?.Definition == research) return "移出研究队列";
        if (manager != null && manager.IsQueued(research)) return "移出研究队列";
        return manager != null && research != null && !manager.ArePrerequisitesCompleted(research)
            ? "加入队列（前置优先）" : "加入研究队列";
    }

    private static bool IsResearchActionAvailable(ResearchDef research, ResearchState state)
    {
        if (research == null || state == null || state.Status == ResearchStatus.Completed)
            return false;
        ResearchManager manager = ResearchManager.Instance;
        if (manager == null)
            return false;
        return manager.ActiveResearch?.Definition == research ||
            manager.IsQueued(research) || manager.CanAccessResearch(research);
    }

    private static bool CanInspectResearch(ResearchDef research)
    {
        if (research == null || !ResearchManager.TryGetInstance(out ResearchManager manager))
            return false;
        return manager.States.TryGetValue(research, out ResearchState state) &&
            (state.Status == ResearchStatus.Completed || manager.CanAccessResearch(research));
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

    private void ShowBuildingRequirements(IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements, string heading = "建筑需求")
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
            Pair<ResourceDef, ExpantaNum> requirement = requirements[i];
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

    private bool RefreshRequirementRows(IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements)
    {
        if (requirementHost == null || requirementContent == null || !requirementHost.gameObject.activeSelf)
            return false;

        if (requirements != null)
            for (int i = 0; i < requirements.Count; i++)
            {
                Pair<ResourceDef, ExpantaNum> requirement = requirements[i];
                if (requirement.First == null)
                    continue;
                if (!detailRequirementAmounts.TryGetValue(requirement.First, out TMP_Text amount) ||
                    !detailRequirementLabels.TryGetValue(requirement.First, out TMP_Text label))
                    return false;
                if (amount == null || label == null)
                    return false;
                SetTextIfChanged(label, requirement.First.Label);
                SetTextIfChanged(amount, FormatRequirementAmount(requirement));
            }
        return true;
    }

    private string FormatRequirementAmount(Pair<ResourceDef, ExpantaNum> requirement)
    {
        if (requirement.First == null)
            return string.Empty;

        if ((selectedResearchNode != null && ResearchManager.TryGetInstance(out ResearchManager research) &&
            research.GetState(selectedResearchNode)?.CostPaid == true) || IsWorkshopPurchased(selectedWorkshop))
            return requirement.Second.ToGameString() + "（已支付）";

        ExpantaNum owned = ExpantaNum.Zero;
        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager != null && resourceManager.States.TryGetValue(requirement.First, out ResourceState resourceState))
            owned = resourceState.Amount;
        string amount = detailBuildingUpgrade && requirement.Second < ExpantaNum.Zero
            ? "返还 " + requirement.Second.Abs().ToGameString()
            : requirement.Second.ToGameString();
        return amount + " (" + owned.ToGameString() + ")";
    }

    private void ResizeDetailBodyIfNeeded(int requirementCount, int flowCount)
    {
        RectTransform body = detailBody.rectTransform;
        float bodyHeight = Mathf.Max(96f,
            detailBody.GetPreferredValues(detailBody.text, GetMeasuredDetailTextWidth(body), 1000f).y + 18f);
        if (!Mathf.Approximately(body.rect.height, bodyHeight))
            PlaceRequirementsAfterDescription(requirementCount,
                requirementGesture == null ? (float?)null : requirementGesture.GetNormalizedPosition(), flowCount);
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
        float productionToolbarHeight = CanShowProductionEntry() && resourceTreeSurface != null ? 80f : 0f;
        body.offsetMin = new Vector2(34f, -bodyHeight - productionToolbarHeight);
        body.offsetMax = new Vector2(-34f, -productionToolbarHeight);

        // Body already contains the white "研究支付需求" line. Start
        // the requirement content directly below it; the old orange
        // Heading is hidden and must not leave another header gap.
        float sectionTop = bodyHeight + productionToolbarHeight;
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

    private void ShowBuildingFlows(IReadOnlyList<Pair<ResourceDef, ExpantaNum>> output, IReadOnlyList<Pair<ResourceDef, ExpantaNum>> input)
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

    private bool RefreshFlowRows(IReadOnlyList<Pair<ResourceDef, ExpantaNum>> output,
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> input)
    {
        if (flowHost == null || flowContent == null)
            return false;

        int expected = CountFlows(output) + CountFlows(input);
        if (flowContent.childCount - 1 != expected)
            return false;
        if (!flowHost.gameObject.activeSelf)
            return expected == 0;

        int index = 0;
        if (!RefreshFlowRowsInPlace(flowContent, output, ref index, false) ||
            !RefreshFlowRowsInPlace(flowContent, input, ref index, true))
            return false;
        return index == expected;
    }

    private static bool RefreshFlowRowsInPlace(RectTransform host,
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> flows, ref int index, bool consumption)
    {
        if (flows == null)
            return true;
        for (int i = 0; i < flows.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> flow = flows[i];
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

    private static int CountFlows(IReadOnlyList<Pair<ResourceDef, ExpantaNum>> flows)
    {
        if (flows == null)
            return 0;
        int count = 0;
        for (int i = 0; i < flows.Count; i++)
            if (flows[i].First != null)
                count++;
        return count;
    }

    private int AddFlowRows(RectTransform host, IReadOnlyList<Pair<ResourceDef, ExpantaNum>> flows, int startIndex, Color tint, bool consumption)
    {
        if (flows == null)
            return startIndex;
        int index = startIndex;
        for (int i = 0; i < flows.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> flow = flows[i];
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

    private static string FormatFlowAmount(Pair<ResourceDef, ExpantaNum> flow, bool consumption)=> (consumption ? "-" : "+") + flow.Second.ToGameString() + "/s";

    private void ConfigureActionButton(string label, UnityEngine.Events.UnityAction action)
    {
        HideUltraDoctrineButton();
        if (detailActionButton == null)
            return;
        detailActionButton.gameObject.SetActive(true);
        detailActionButton.interactable = true;
        detailActionButton.onClick.RemoveAllListeners();
        detailActionButton.onClick.AddListener(action);
        if (detailActionButtonText == null)
            detailActionButtonText = detailActionButton.GetComponentInChildren<TMP_Text>(true);
        if (detailActionButtonText != null)
            detailActionButtonText.text = label;
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] ResearchActionButton active={detailActionButton.gameObject.activeSelf} " +
            $"interactable={detailActionButton.interactable} label={label} " +
            $"research={(selectedResearchNode == null ? string.Empty : selectedResearchNode.Id)}");
#endif
    }

    private void ResearchAction(ResearchDef research)
    {
        if (ResearchManager.Instance == null)
        {
            ShowDetails("研究", "研究管理器尚未初始化。");
            return;
        }
        HandleResearchUiAction(research);
    }

    private static bool BuildingPrerequisitesMet(BuildingDef definition)
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
    IEndDragHandler, IPointerDownHandler, IPointerUpHandler, IScrollHandler
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
    private Vector2 lastPointerLocalPosition;

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

    public void OnScroll(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || viewport == null || content == null)
            return;
        content.anchoredPosition -= new Vector2(0f, eventData.scrollDelta.y * 80f);
        ClampContent();
        eventData.Use();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !isActiveAndEnabled ||
            viewport == null || content == null || (pointerHeld && eventData.pointerId != pointerId))
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
        if (eventData.button != PointerEventData.InputButton.Left || !isActiveAndEnabled || viewport == null || content == null)
            return;
        // A child Button can own PointerDown while its parent owns the drag lifecycle.
        if (!pointerHeld)
            OnPointerDown(eventData);
        if (eventData.pointerId != pointerId)
            return;
        lastPointerLocalPosition = GetLocalPointerPosition(eventData);
        dragging = true;
        eventData.eligibleForClick = false;
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
        Vector2 localPosition = GetLocalPointerPosition(eventData);
        float deltaY = localPosition.y - lastPointerLocalPosition.y;
        lastPointerLocalPosition = localPosition;
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
        eventData.eligibleForClick = false;
        eventData.Use();
        ResetPointer();
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

    private void OnDisable() => ResetPointer();

    private Vector2 GetLocalPointerPosition(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, eventData.position, eventData.pressEventCamera, out Vector2 localPosition);
        return localPosition;
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
