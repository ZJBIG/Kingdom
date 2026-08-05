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
        private void ShowDetails(string title, string description, string id)
        {
            if (detailBody == null)
                return;
            detailBuildingUpgrade = false;
            detailIsBuilding = false;
            selectedBuilding = null;
            selectedResource = null;
            detailBody.text = title + "\n\n" + description + "\n\nID: " + id;
            HideBuildingRequirements();
            HideResearchPaymentButton();
            if (detailActionButton != null)
                detailActionButton.gameObject.SetActive(false);
        }
    
        private bool ShouldDisplayBuilding(Building definition)
        {
            if (BuildingManager.Instance != null)
                return BuildingManager.Instance.ShouldDisplay(definition);
            return BuildingPrerequisitesMet(definition);
        }
    
        private static List<Pair<Resource, ExpantaNum>> GetNextBuildingRequirements(
            Building building,
            BuildingState state,
            bool upgrading)
        {
            var requirements = new List<Pair<Resource, ExpantaNum>>();
            if (upgrading)
            {
                BuildingManager.Instance.GetUpgradeResourceDeltas(building, ExpantaNum.One, requirements);
                return requirements;
            }
    
            ExpantaNum owned = state == null ? ExpantaNum.Zero : state.Amount;
            for (int i = 0; i < building.ResourceRequirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement = building.ResourceRequirements[i];
                requirements.Add(new Pair<Resource, ExpantaNum>(
                    requirement.First,
                    requirement.Second.GeometricSeriesCost(building.CostGrowth, owned, ExpantaNum.One)));
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
            text.AppendLine("Amount: " + amount);
            text.AppendLine("时代: " + building.TechLevel);
            detailBody.text = text.ToString();
            if (detailActionButton != null)
                detailActionButton.gameObject.SetActive(false);
            HideResearchPaymentButton();
            bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
                BuildingManager.Instance != null &&
                BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out _);
            detailBuildingUpgrade = upgrading;
            ShowBuildingFlows(GetEffectiveBuildingFlows(building, true), GetEffectiveBuildingFlows(building, false));
            List<Pair<Resource, ExpantaNum>> requirements = GetNextBuildingRequirements(building, state, upgrading);
            ShowBuildingRequirements(requirements, "建筑建造需求");
            PlaceRequirementsAfterDescription(requirements == null ? 0 : requirements.Count);
        }

        private void SetBuildingDetailBody(Building building, BuildingState state)
        {
            string amount = state == null ? "0" : state.Amount.ToGameString();
            StringBuilder text = new();
            text.AppendLine(building.Label);
            text.AppendLine();
            text.AppendLine(building.Description);
            text.AppendLine();
            text.AppendLine("Amount: " + amount);
            text.AppendLine("时代: " + building.TechLevel);
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
            StringBuilder text = new();
            text.AppendLine(resource.Label);
            text.AppendLine();
            text.AppendLine(resource.Description);
            text.AppendLine();
            string positiveColor = "#" + ColorUtility.ToHtmlStringRGB(Positive);
            string errorColor = "#" + ColorUtility.ToHtmlStringRGB(Error);
            string netColor = net >= ExpantaNum.Zero ? positiveColor : errorColor;
            text.AppendLine("库存: " + amount.ToGameString());
            text.AppendLine("<color=" + positiveColor + ">产出: +" + production.ToGameString() + "/s</color>");
            text.AppendLine("<color=" + errorColor + ">消耗: -" + consumption.ToGameString() + "/s</color>");
            text.AppendLine("<color=" + netColor + ">净变化: " + (net >= ExpantaNum.Zero ? "+" : "") + net.ToGameString() + "/s</color>");
            detailBody.text = text.ToString();
            detailBody.richText = true;
            LayoutResourceDetailsBody();
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
            float width = Mathf.Max(320f, body.rect.width);
            float bodyHeight = Mathf.Clamp(
                detailBody.GetPreferredValues(detailBody.text, width, 1000f).y + 20f,
                96f,
                600f);
            body.offsetMin = new Vector2(34f, -100f - bodyHeight);
            body.offsetMax = new Vector2(-34f, -100f);
            Canvas.ForceUpdateCanvases();
        }
    
        private void ShowResearchDetails(Research research, bool preserveScrollPosition = false)
        {
            if (detailBody == null || research == null)
                return;
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
            detailBody.text = "研究说明\n\n" + research.Description + "\n\n研究支付需求";
            HideBuildingRequirements();
            ShowBuildingRequirements(research.ResourceRequirements, "研究支付需求");
            PlaceRequirementsAfterDescription(
                research.ResourceRequirements == null ? 0 : research.ResourceRequirements.Count,
                keepScrollPosition ? (float?)savedRequirementScrollPosition : null);
            ConfigureResearchPaymentButton(research, state);
            ConfigureActionButton("研究 / 加入队列", () => ResearchAction(research));
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
            ConfigureActionButton("研究 / 加入队列", () => ResearchAction(research));
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
            detailPaymentButton.interactable = state == null ||
                (state.Status != ResearchStatus.Completed && !state.CostPaid);
            TMP_Text text = detailPaymentButton.GetComponentInChildren<TMP_Text>(true);
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
                for (int i = flowHost.childCount - 1; i >= 0; i--)
                    Destroy(flowHost.GetChild(i).gameObject);
            }
            if (requirementHost == null || content == null)
                return;
            requirementHost.gameObject.SetActive(false);
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }
    
        private void ShowBuildingRequirements(IReadOnlyList<Pair<Resource, ExpantaNum>> requirements, string heading = "建筑需求")
        {
            if (requirementHost == null)
                return;
            RectTransform content = requirementContent == null ? requirementHost : requirementContent;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
            requirementHost.gameObject.SetActive(true);
            heading = heading.Contains("研究") || heading.Contains("鐮旂┒") ? "研究支付需求" : "建筑建造需求";
            int validCount = 0;
            if (requirements != null)
                for (int i = 0; i < requirements.Count; i++)
                    if (requirements[i] != null && requirements[i].First != null)
                        validCount++;
            const float requirementRowStep = 88f;
            const float requirementHeaderHeight = 88f;
            content.sizeDelta = new Vector2(0, requirementHeaderHeight + validCount * requirementRowStep);
            Label("Heading", content, heading, 24, Copper, new Vector2(0, 1), Vector2.one,
                new Vector2(18, -38), new Vector2(-18, -4));
            if (requirements == null || requirements.Count == 0)
            {
                Label("None", content, "暂无支付需求", 22, TextSecondary, Vector2.zero, Vector2.one,
                    new Vector2(18, -92), new Vector2(-18, -20));
                return;
            }
            for (int i = 0; i < requirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement = requirements[i];
                if (requirement.First == null)
                    continue;
                RectTransform row = Rect("Requirement_" + i, content, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -156 - i * requirementRowStep), new Vector2(0, -88 - i * requirementRowStep));
                RectTransform iconRect = Rect("Texture", row, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, -24), new Vector2(60, 24));
                Image icon = iconRect.gameObject.AddComponent<Image>(); icon.sprite = requirement.First.Sprite; icon.color = requirement.First.Color; icon.preserveAspect = true; icon.raycastTarget = false;
                TMP_Text rowLabel = Label("Label", row, requirement.First.Label, 22, TextPrimary, Vector2.zero, new Vector2(.65f, 1), new Vector2(76, 0), new Vector2(-8, 0));
                TMP_Text rowAmount = Label("Amount", row, FormatRequirementAmount(requirement), 24, TextPrimary, new Vector2(.65f, 0), Vector2.one, new Vector2(8, 0), new Vector2(-16, 0));
                rowLabel.raycastTarget = false;
                rowAmount.raycastTarget = false;
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
                    if (requirement == null || requirement.First == null)
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

        private static string FormatRequirementAmount(Pair<Resource, ExpantaNum> requirement)
        {
            ExpantaNum owned = ExpantaNum.Zero;
            if (requirement != null && requirement.First != null && ResourceManager.Instance != null &&
                ResourceManager.Instance.States.TryGetValue(requirement.First, out ResourceState resourceState))
                owned = resourceState.Amount;

            return requirement.Second.ToGameString() + " (" + owned.ToGameString() + ")";
        }
    
        private void PlaceRequirementsAfterDescription(int requirementCount, float? preservedScrollPosition = null)
        {
            if (detailBody == null || requirementHost == null)
                return;
            if (requirementGesture == null)
            {
                requirementGesture = requirementHost.GetComponent<UIDetailRequirementScrollGesture>();
                if (requirementGesture == null)
                    requirementGesture = requirementHost.gameObject.AddComponent<UIDetailRequirementScrollGesture>();
                requirementGesture.Initialize(requirementHost, requirementContent);
                Debug.Log("[KingdomUI] Requirement gesture attached during detail layout");
            }
            // A late layout pass must not resurrect the legacy gesture or
            // disable the native ScrollRect. Keep one owner for the list.
            if (requirementScroll != null)
                requirementScroll.enabled = true;
            if (requirementGesture != null)
                requirementGesture.enabled = false;
            RectTransform body = detailBody.rectTransform;
            body.anchorMin = new Vector2(0, 1);
            body.anchorMax = new Vector2(1, 1);
            body.pivot = new Vector2(.5f, 1f);
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Max(320f, body.rect.width);
            float bodyHeight = Mathf.Clamp(detailBody.GetPreferredValues(detailBody.text, width, 1000f).y + 20f, 96f, 390f);
            body.offsetMin = new Vector2(34f, -100f - bodyHeight);
            body.offsetMax = new Vector2(-34f, -100f);
    
            float sectionTop = 100f + bodyHeight + 18f;
            bool hasFlows = flowHost != null && flowHost.gameObject.activeSelf &&
                flowContent != null && flowContent.childCount > 1;
            if (hasFlows)
            {
                int flowRows = Mathf.Max(1, flowContent.childCount - 1);
                float flowHeight = Mathf.Clamp(76f + flowRows * 62f, 140f, 290f);
                flowHost.anchorMin = new Vector2(0, 1);
                flowHost.anchorMax = new Vector2(1, 1);
                flowHost.pivot = new Vector2(.5f, 1f);
                flowHost.offsetMin = new Vector2(34f, -sectionTop - flowHeight);
                flowHost.offsetMax = new Vector2(-34f, -sectionTop);
                sectionTop += flowHeight + 18f;
                if (flowScroll != null && flowScroll.content != null)
                    flowScroll.verticalNormalizedPosition = 1f;
            }
            else if (flowHost != null)
            {
                flowHost.gameObject.SetActive(false);
            }

            RectTransform detailRect = body.parent as RectTransform;
            float detailHeight = detailRect == null ? 0f : detailRect.rect.height;
            const float bottomActionReserve = 182f;
            float availableRequirementHeight = detailHeight > 0f
                ? detailHeight - sectionTop - bottomActionReserve
                : 360f;
            // The viewport occupies the actual free space above the two
            // bottom actions. Only the content grows with the number of
            // requirements, so long lists scroll instead of pushing buttons
            // outside the detail panel or leaving an arbitrary black block.
            float requirementHeight = Mathf.Clamp(availableRequirementHeight, 180f, 420f);
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
                measuredContent.sizeDelta = new Vector2(0f, 88f + rowCount * 88f);
                Canvas.ForceUpdateCanvases();
            }
            if (requirementGesture != null)
                requirementGesture.SetNormalizedPosition(preservedScrollPosition.HasValue
                    ? Mathf.Clamp01(preservedScrollPosition.Value)
                    : 1f);
            Debug.Log($"[KingdomUI] Requirement scroll bounds: viewport={requirementHost.rect.size}, content={requirementContent.rect.size}, rangeY={Mathf.Max(0f, requirementContent.rect.height - requirementHost.rect.height)}, sectionTop={sectionTop:0.0}, detailHeight={detailHeight:0.0}, active={requirementHost.gameObject.activeSelf}, nativeScroll={requirementScroll != null && requirementScroll.enabled}, customGestureEnabled={requirementGesture != null && requirementGesture.enabled}");
            Canvas.ForceUpdateCanvases();
        }
    
        private void ShowBuildingFlows(IReadOnlyList<Pair<Resource, ExpantaNum>> output, IReadOnlyList<Pair<Resource, ExpantaNum>> input)
        {
            if (flowHost == null)
                return;
            for (int i = flowHost.childCount - 1; i >= 0; i--)
                Destroy(flowHost.GetChild(i).gameObject);
            flowContent = Rect("FlowContent", flowHost, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            flowContent.pivot = new Vector2(.5f, 1f);
            flowContent.sizeDelta = new Vector2(0, Mathf.Max(86f, (CountFlows(output) + CountFlows(input)) * 62f + 76f));
            flowScroll.content = flowContent;
            flowHost.gameObject.SetActive(true);
            Label("Heading", flowContent, "产出 / 消耗", 22, Copper, new Vector2(0, 1), Vector2.one, new Vector2(0, -32), new Vector2(0, -2));
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
                if (flow == null || flow.First == null)
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
    
        private static int AddFlowRows(RectTransform host, IReadOnlyList<Pair<Resource, ExpantaNum>> flows, int startIndex, Color tint, bool consumption)
        {
            if (flows == null)
                return startIndex;
            int index = startIndex;
            for (int i = 0; i < flows.Count; i++)
            {
                Pair<Resource, ExpantaNum> flow = flows[i];
                if (flow.First == null)
                    continue;
                RectTransform row = Rect("Flow_" + index, host, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -98 - index * 62), new Vector2(0, -42 - index * 62));
                RectTransform iconRect = Rect("Texture", row, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(8, -22), new Vector2(52, 22));
                Image icon = iconRect.gameObject.AddComponent<Image>(); icon.sprite = flow.First.Sprite; icon.color = flow.First.Color; icon.preserveAspect = true;
                TMP_Text flowLabel = Label("Label", row, flow.First.Label, 21, TextPrimary, Vector2.zero, new Vector2(.62f, 1), new Vector2(66, 0), new Vector2(-8, 0));
                TMP_Text flowAmount = Label("Amount", row, FormatFlowAmount(flow, consumption), 22, tint, new Vector2(.62f, 0), Vector2.one, new Vector2(8, 0), new Vector2(-12, 0));
                flowLabel.raycastTarget = false;
                flowAmount.raycastTarget = false;
                index++;
            }
            return index;
        }

        private static string FormatFlowAmount(Pair<Resource, ExpantaNum> flow, bool consumption)
        {
            return (consumption ? "-" : "+") + flow.Second.ToGameString() + "/s";
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
            detailActionButton.onClick.RemoveAllListeners();
            detailActionButton.onClick.AddListener(action);
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
            Debug.Log($"[KingdomUI] Research payment: id={research.Id}, result={result}");
            ShowResearchDetails(research);
        }
    
        private void BuildOne(Building building)
        {
            if (BuildingManager.Instance == null)
            {
                ShowDetails("Building", "BuildingManager is not initialized.", building.Id);
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
                ShowDetails("Research", "ResearchManager is not initialized.", research.Id);
                return;
            }
            ResearchActionResult result = ResearchManager.Instance.HandleResearchAction(research);
            ShowResearchDetails(research);
            if (detailBody != null)
                detailBody.text += "\n\nAction result: " + result;
        }
    
        private static void AppendCosts(StringBuilder builder, IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                builder.AppendLine("  None");
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
    
        private static bool WorkshopPrerequisitesMet(WorkshopUpgradeDefinition definition)
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
    private Vector2 lastLocalPosition;
    private Vector2 lastScreenPosition;
    private bool hasScreenPosition;
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
        lastScreenPosition = Vector2.zero;
        hasScreenPosition = false;
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
        return Mathf.Clamp01((content.anchoredPosition.y + range) / range);
    }

    public void SetNormalizedPosition(float value)
    {
        if (viewport == null || content == null)
            return;
        float range = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        content.anchoredPosition = new Vector2(0f, -range * Mathf.Clamp01(1f - value));
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
        lastLocalPosition = GetLocalPosition(eventData);
        lastScreenPosition = eventData.position;
        hasScreenPosition = true;
        movementSamples = 0;
        movementPathLength = 0f;
        Debug.Log($"[KingdomUI] Requirement pointer down: pointer={pointerId}, position={eventData.position}, viewport={viewport.rect.size}, content={content.rect.size}, rangeY={Mathf.Max(0f, content.rect.height - viewport.rect.height)}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!pointerHeld || eventData.pointerId != pointerId)
            return;
        dragging = true;
        lastLocalPosition = GetLocalPosition(eventData);
        if (!loggedDrag)
        {
            loggedDrag = true;
            Debug.Log($"[KingdomUI] Requirement drag started: viewport={viewport.rect.size}, content={content.rect.size}, rangeY={Mathf.Max(0f, content.rect.height - viewport.rect.height)}");
        }
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!pointerHeld || eventData.pointerId != pointerId ||
            viewport == null || content == null)
            return;
        // Continuous real touch is sampled once per frame in Update. Device
        // Simulator can emit only one synthetic move event, however; use its
        // delta only when the polling path has not already consumed that same
        // screen position. This keeps both paths smooth without double-moving.
        Vector2 eventScreenPosition = eventData.position;
        Vector2 eventDelta = eventScreenPosition - lastScreenPosition;
        if (eventDelta.sqrMagnitude > 0.001f)
        {
            lastScreenPosition = eventScreenPosition;
            float scale = Mathf.Max(.001f, Mathf.Abs(viewport.lossyScale.y));
            float deltaY = -eventDelta.y / scale;
            if (Mathf.Abs(deltaY) > 0.001f)
            {
                dragging = true;
                content.anchoredPosition += new Vector2(0f, deltaY);
                ClampContent();
                movementSamples++;
                movementPathLength += Mathf.Abs(deltaY);
                if (!loggedMovement)
                {
                    loggedMovement = true;
                    Debug.Log($"[KingdomUI] Requirement drag moved by event fallback: deltaY={deltaY:0.00}, position={content.anchoredPosition}, rangeY={Mathf.Max(0f, content.rect.height - viewport.rect.height)}");
                }
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
        Debug.Log($"[KingdomUI] Requirement drag ended: samples={movementSamples}, pathY={movementPathLength:0.00}, position={content.anchoredPosition}");
        ResetPointer();
    }

    private void Update()
    {
        if (!pointerHeld || viewport == null || content == null)
            return;
        if (!TryGetPointerScreenPosition(out Vector2 screenPosition))
            return;
        if (!hasScreenPosition)
        {
            lastScreenPosition = screenPosition;
            lastLocalPosition = GetLocalPosition(screenPosition);
            hasScreenPosition = true;
            return;
        }

        Vector2 screenDelta = screenPosition - lastScreenPosition;
        lastScreenPosition = screenPosition;
        if (screenDelta.sqrMagnitude <= 0.001f)
            return;
        float threshold = EventSystem.current == null ? 3f :
            Mathf.Max(1f, EventSystem.current.pixelDragThreshold);
        if (!dragging && screenDelta.sqrMagnitude < threshold * threshold)
            return;
        dragging = true;
        float scale = Mathf.Max(.001f, Mathf.Abs(viewport.lossyScale.y));
        float deltaY = -screenDelta.y / scale;
        content.anchoredPosition += new Vector2(0f, deltaY);
        ClampContent();
        movementSamples++;
        movementPathLength += Mathf.Abs(deltaY);
        if (!loggedMovement)
        {
            loggedMovement = true;
            Debug.Log($"[KingdomUI] Requirement drag moved by polling: deltaY={deltaY:0.00}, position={content.anchoredPosition}, rangeY={Mathf.Max(0f, content.rect.height - viewport.rect.height)}");
        }
    }

    private Vector2 GetLocalPosition(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, eventData.position, eventData.pressEventCamera, out Vector2 localPosition);
        return localPosition;
    }

    private Vector2 GetLocalPosition(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, screenPosition, null, out Vector2 localPosition);
        return localPosition;
    }

    private bool TryGetPointerScreenPosition(out Vector2 screenPosition)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == pointerId)
            {
                screenPosition = touch.position;
                return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            }
        }
        if (Input.GetMouseButton(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
        screenPosition = Vector2.zero;
        return false;
    }

    private void ClampContent()
    {
        if (viewport == null || content == null)
            return;
        float range = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        Vector2 position = content.anchoredPosition;
        position.x = 0f;
        position.y = Mathf.Clamp(position.y, -range, 0f);
        content.anchoredPosition = position;
    }

    private void ResetPointer()
    {
        pointerHeld = false;
        dragging = false;
        pointerId = int.MinValue;
        lastScreenPosition = Vector2.zero;
        hasScreenPosition = false;
        movementSamples = 0;
        movementPathLength = 0f;
        loggedDrag = false;
        loggedMovement = false;
    }
}
