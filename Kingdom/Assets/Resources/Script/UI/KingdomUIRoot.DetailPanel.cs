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
    private sealed class ResourceBuildingFlow
    {
        public Building Building;
        public ExpantaNum Rate;
        public bool Active;
    }

    private void ShowDetails(string title, string description, string id)
    {
        if (detailBody == null)
            return;
        detailBody.fontSize = 24f;
        detailBuildingUpgrade = false;
        detailIsBuilding = false;
        selectedBuilding = null;
        selectedResource = null;
        detailBody.text = title + "\n\n" + description + "\n\n标识：" + id;
        HideBuildingRequirements();
        HideResearchPaymentButton();
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
        var requirements = new List<Pair<Resource, ExpantaNum>>();
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

    private static List<Pair<Resource, ExpantaNum>> GetEffectiveBuildingFlows(
        Building building,
        bool output)
    {
        var flows = new List<Pair<Resource, ExpantaNum>>();
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

    private void ShowBuildingDetails(Building building)
    {
        if (detailBody == null || building == null)
            return;
        detailBody.fontSize = 30f;
        selectedBuilding = building;
        selectedResource = null;
        detailIsBuilding = true;
        string amount = "0";
        BuildingState state = BuildingManager.Instance != null &&
            BuildingManager.Instance.States.TryGetValue(building, out BuildingState existing)
            ? existing
            : null;
        if (state != null)
            amount = state.Amount.ToGameString();
        StringBuilder text = new();
        text.AppendLine(building.Label);
        text.AppendLine();
        text.AppendLine(building.Description);
        text.AppendLine();
        text.AppendLine("数量: " + amount);
        text.AppendLine("时代: " + building.TechLevel.GetDescription());
        detailBody.text = text.ToString();
        if (detailActionButton != null)
            detailActionButton.gameObject.SetActive(false);
        HideResearchPaymentButton();
        bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
            BuildingManager.Instance != null &&
            BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out _);
        detailBuildingUpgrade = upgrading;
        IReadOnlyList<Pair<Resource, ExpantaNum>> output = GetEffectiveBuildingFlows(building, true);
        IReadOnlyList<Pair<Resource, ExpantaNum>> input = GetEffectiveBuildingFlows(building, false);
        ShowBuildingFlows(output, input);
        List<Pair<Resource, ExpantaNum>> requirements = GetNextBuildingRequirements(building, state, upgrading);
        ShowBuildingRequirements(requirements, "建筑建造需求");
        PlaceRequirementsAfterDescription(
            requirements == null ? 0 : requirements.Count,
            null,
            CountFlows(output) + CountFlows(input));
    }

    private void SetBuildingDetailBody(Building building, BuildingState state)
    {
        string amount = state == null ? "0" : state.Amount.ToGameString();
        StringBuilder text = new();
        text.AppendLine(building.Label);
        text.AppendLine();
        text.AppendLine(building.Description);
        text.AppendLine();
        text.AppendLine("数量: " + amount);
        text.AppendLine("时代: " + building.TechLevel.GetDescription());
        detailBody.text = text.ToString();
    }

    private void RefreshSelectedBuildingDetails(Building building)
    {
        if (building == null || detailBody == null || BuildingManager.Instance == null)
            return;

        BuildingState state = BuildingManager.Instance.States.TryGetValue(building, out BuildingState current)
            ? current
            : null;
        bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
            BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out _);

        // A prerequisite/upgrade transition changes the structure of the
        // detail panel. Rebuild only for that structural change; ordinary
        // amount/rate changes update existing rows in place so a gesture
        // never loses its content or pointer state.
        List<Pair<Resource, ExpantaNum>> requirements = GetNextBuildingRequirements(building, state, upgrading);
        if (detailBuildingUpgrade != upgrading || !RefreshRequirementRows(requirements))
        {
            ShowBuildingDetails(building);
            return;
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> output = GetEffectiveBuildingFlows(building, true);
        IReadOnlyList<Pair<Resource, ExpantaNum>> input = GetEffectiveBuildingFlows(building, false);
        if (!RefreshFlowRows(output, input))
        {
            ShowBuildingDetails(building);
            return;
        }

        SetBuildingDetailBody(building, state);
    }

    private void ShowResourceDetails(Resource resource)
    {
        if (detailBody == null || resource == null)
            return;

        selectedResearchNode = null;
        selectedBuilding = null;
        selectedResource = resource;
        detailIsBuilding = false;
        detailBuildingUpgrade = false;
        HideBuildingRequirements();
        HideResearchPaymentButton();
        if (detailActionButton != null)
            detailActionButton.gameObject.SetActive(false);

        RefreshResourceDetails(resource);
    }

    private void RefreshResourceDetails(Resource resource)
    {
        if (detailBody == null || resource == null || selectedResource != resource)
            return;

        ResourceState state = null;
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.States.TryGetValue(resource, out state);

        ExpantaNum amount = state == null ? ExpantaNum.Zero : state.Amount;
        ExpantaNum production = state == null ? ExpantaNum.Zero : state.ProductionRate;
        ExpantaNum consumption = state == null ? ExpantaNum.Zero : state.ConsumptionRate;
        ExpantaNum net = production - consumption;
        List<ResourceBuildingFlow> producers = GetResourceBuildingFlows(resource, true);
        List<ResourceBuildingFlow> consumers = GetResourceBuildingFlows(resource, false);
        StringBuilder text = new();
        text.AppendLine(resource.Label);
        text.AppendLine();
        text.AppendLine(resource.Description);
        text.AppendLine();

        text.AppendLine("库存: " + amount.ToGameString());
        text.AppendLine(("产出: +" + production.ToGameString()).Colorize(Positive));
        for (int i = 0; i < producers.Count; i++)
            text.AppendLine("       --" + producers[i].Building.Label +
                (producers[i].Active ? string.Empty : "（待建造）") + ": " +
                ("+" + producers[i].Rate.ToGameString() + "/s").Colorize(Positive));
        text.AppendLine(("消耗: -" + consumption.ToGameString()).Colorize(Error));
        for (int i = 0; i < consumers.Count; i++)
            text.AppendLine("       --" + consumers[i].Building.Label +
                (consumers[i].Active ? string.Empty : "（待建造）") + ": " +
                ("-" + consumers[i].Rate.ToGameString() + "/s").Colorize(Error));
        text.AppendLine(("净变化: " + (net >= ExpantaNum.Zero ? "+" : "") + net.ToGameString() + "/s").Colorize(net >= ExpantaNum.Zero ? Positive : Error));
        detailBody.text = text.ToString();
        detailBody.richText = true;
        detailBody.fontSize = 30f;
        LayoutResourceDetailsBody();
    }

    private static List<ResourceBuildingFlow> GetResourceBuildingFlows(Resource resource, bool production)
    {
        var result = new List<ResourceBuildingFlow>();
        if (resource == null || BuildingManager.Instance == null)
            return result;

        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        foreach (KeyValuePair<Building, BuildingState> entry in BuildingManager.Instance.States)
        {
            Building building = entry.Key;
            BuildingState state = entry.Value;
            if (building == null || state == null)
                continue;

            bool active = state.Amount > ExpantaNum.Zero;
            if (!active && !BuildingManager.Instance.ArePrerequisitesMet(building, out _))
                continue;

            IReadOnlyList<Pair<Resource, ExpantaNum>> flows = production
                ? building.ResourceGenerationRates
                : building.ResourceConsumptionRates;
            ExpantaNum rate = ExpantaNum.Zero;
            for (int i = 0; i < flows.Count; i++)
            {
                Pair<Resource, ExpantaNum> flow = flows[i];
                if (flow.First == resource)
                    rate += flow.Second;
            }
            if (rate <= ExpantaNum.Zero)
                continue;

            if (active)
            {
                rate *= state.Amount * state.Efficiency;
                if (production)
                {
                    rate *= modifiers.GetBuildingProductionMultiplier(building);
                    rate *= modifiers.GlobalBuildingProductionMultiplier;
                    rate *= modifiers.GetResourceProductionMultiplier(resource);
                }
            }
            result.Add(new ResourceBuildingFlow
            {
                Building = building,
                Rate = rate,
                Active = active
            });
        }

        result.Sort((left, right) =>
        {
            int byActive = right.Active.CompareTo(left.Active);
            if (byActive != 0)
                return byActive;
            int byRate = right.Rate.CompareTo(left.Rate);
            return byRate != 0
                ? byRate
                : string.Compare(left.Building.Id, right.Building.Id, StringComparison.OrdinalIgnoreCase);
        });
        return result;
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
        Canvas.ForceUpdateCanvases();
        float width = GetMeasuredDetailTextWidth(body);
        float bodyHeight = Mathf.Clamp(
            detailBody.GetPreferredValues(detailBody.text, width, 1000f).y + 20f,
            96f,
            1400f);
        body.offsetMin = new Vector2(34f, -bodyHeight);
        body.offsetMax = new Vector2(-34f, 0f);
        if (detailScrollContent != null)
            detailScrollContent.sizeDelta = new Vector2(0f, Mathf.Max(detailScrollViewport.rect.height, 24f + bodyHeight));
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
        ResearchManager.Instance?.SetSelectedResearch(research);
        RefreshResearchTreeVisuals();
        detailBuildingUpgrade = false;
        detailIsBuilding = false;
        selectedBuilding = null;
        selectedResource = null;
        ResearchState state = null;
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.States.TryGetValue(research, out state);
        // The payment title is authored by the requirement section below;
        // keep it out of Body so it cannot be duplicated or appear as a
        // stray top line while the sections are being measured.
        detailBody.text = "研究说明\n\n" + research.Description;
        HideBuildingRequirements();
        ShowBuildingRequirements(research.ResourceRequirements, "研究支付需求");
        PlaceRequirementsAfterDescription(
            research.ResourceRequirements == null ? 0 : research.ResourceRequirements.Count,
            keepScrollPosition ? (float?)savedRequirementScrollPosition : null,
            0);
        ConfigureResearchPaymentButton(research, state);
        ConfigureActionButton("加入研究队列", () => ResearchAction(research));
    }

    private void RefreshSelectedResearchDetails(Research research)
    {
        if (research == null || detailBody == null)
            return;

        ResearchState state = null;
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.States.TryGetValue(research, out state);

        if (!RefreshRequirementRows(research.ResourceRequirements))
        {
            ShowResearchDetails(research, true);
            return;
        }

        ConfigureResearchPaymentButton(research, state);
        ConfigureActionButton("加入研究队列", () => ResearchAction(research));
    }

    private static string GetResearchQueueActionLabel(Research research, ResearchState state)
    {
        if (state != null && state.Status == ResearchStatus.Completed)
            return "研究已完成";
        if (state != null && state.Status == ResearchStatus.Researching)
            return "研究进行中";
        return ResearchManager.Instance != null && ResearchManager.Instance.IsQueued(research)
            ? "删除研究队列"
            : "加入研究队列";
    }

    private void ConfigureResearchPaymentButton(Research research, ResearchState state)
    {
        if (detailPaymentButton == null)
            return;
        detailPaymentButton.gameObject.SetActive(true);
        detailPaymentButton.onClick.RemoveAllListeners();
        detailPaymentButton.onClick.AddListener(() => PayResearchResources(research));
        string paymentBlocker = string.Empty;
        bool canPay = ResearchManager.Instance != null &&
            ResearchManager.Instance.CanPayResearchCost(research, out paymentBlocker);
        detailPaymentButton.interactable = state != null &&
            state.Status != ResearchStatus.Completed && !state.CostPaid && canPay;
        TMP_Text text = detailPaymentButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null && !canPay && !string.IsNullOrEmpty(paymentBlocker))
            text.text = paymentBlocker;
        if (text != null)
            text.text = state != null && state.CostPaid ? "资源已支付" : "支付资源";
    }

    private void HideResearchPaymentButton()
    {
        if (detailPaymentButton == null)
            return;
        detailPaymentButton.onClick.RemoveAllListeners();
        detailPaymentButton.interactable = false;
        detailPaymentButton.gameObject.SetActive(false);
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
            SetRowText(row, "Label", requirement.First.Label);
            SetRowText(row, "Amount", FormatRequirementAmount(requirement));
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
                Transform row = requirementContent.Find("Requirement_" + i);
                if (row == null)
                    return false;
                TMP_Text amount = row.Find("Amount")?.GetComponent<TMP_Text>();
                TMP_Text label = row.Find("Label")?.GetComponent<TMP_Text>();
                if (amount == null || label == null)
                    return false;
                label.text = requirement.First.Label;
                amount.text = FormatRequirementAmount(requirement);
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
        ResourceManager resourceManager = FindObjectOfType<ResourceManager>();
        if (resourceManager != null && resourceManager.States.TryGetValue(requirement.First, out ResourceState resourceState))
            owned = resourceState.Amount;

        ResearchManager researchManager = FindObjectOfType<ResearchManager>();
        if (researchManager != null && researchManager.SelectedResearchId != null &&
            DataBase<Research>.TryFind(researchManager.SelectedResearchId, out Research selected) &&
            researchManager.States.TryGetValue(selected, out ResearchState researchState))
            paid = researchState.GetPaidResourceCost(requirement.First);

        if (detailIsBuilding)
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
        float bodyHeight = Mathf.Clamp(detailBody.GetPreferredValues(detailBody.text, width, 1000f).y, 96f, 390f);
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
            label.text = flow.First.Label;
            amount.text = FormatFlowAmount(flow, consumption);
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
        detailActionButton.onClick.RemoveAllListeners();
        bool isBuildingAction = detailIsBuilding;
        detailActionButton.onClick.AddListener(() =>
        {
            UIButtonSoundManager.Play(isBuildingAction
                ? UIButtonSoundManager.Sound.Purchase
                : UIButtonSoundManager.Sound.Detail);
            action();
        });
        TMP_Text text = detailActionButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = detailIsBuilding
                ? (detailBuildingUpgrade ? "升级 1 个" : "建造 1 个")
                : label;
    }

    private void PayResearchResources(Research research)
    {
        if (research == null || ResearchManager.Instance == null)
            return;
        ResearchPaymentResult result = ResearchManager.Instance.PayResearchCost(research);
            Debug.Log($"[界面] 研究支付：id={research.Id}，结果={result.GetDescription()}");
        ShowResearchDetails(research);
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
        ShowBuildingDetails(building);
    }

    private void ResearchAction(Research research)
    {
        if (ResearchManager.Instance == null)
        {
            ShowDetails("研究", "研究管理器尚未初始化。", research.Id);
            return;
        }
        ResearchActionResult result = ResearchManager.Instance.HandleResearchAction(research);
        ShowResearchDetails(research);
        if (detailBody != null)
            detailBody.text += "\n\n执行结果：" + result;
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

    private static bool WorkshopPrerequisitesMet(WorkshopUpgrade definition)
    {
        if (definition.RequiredResearch != null && definition.RequiredResearch.Count > 0)
        {
            if (ResearchManager.Instance == null)
                return false;
            for (int i = 0; i < definition.RequiredResearch.Count; i++)
                if (definition.RequiredResearch[i] == null || !ResearchManager.Instance.IsResearchCompleted(definition.RequiredResearch[i].Id))
                    return false;
        }
        if (definition.RequiredUpgrades != null && definition.RequiredUpgrades.Count > 0)
        {
            if (WorkshopManager.Instance == null)
                return false;
            for (int i = 0; i < definition.RequiredUpgrades.Count; i++)
                if (definition.RequiredUpgrades[i] == null || !WorkshopManager.Instance.IsPurchased(definition.RequiredUpgrades[i]))
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
        position.x = 0f;
        position.y = Mathf.Clamp(position.y, 0f, range);
        content.anchoredPosition = position;
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
