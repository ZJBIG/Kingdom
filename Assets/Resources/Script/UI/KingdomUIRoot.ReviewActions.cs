using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private GameObject reviewConfirmation;
    private TMP_Text reviewWarning;
    private Action pendingReviewAction;
    private Transform reviewDetailTools;
    private Transform reviewPageTools;
    private Button reviewLocate, reviewEarlier, reviewLater, reviewRepair;
    private Button reviewCurrentResearch, reviewResetResearch, reviewOffline, reviewSave;
    private GameObject reviewDetailHeading;
    private CanvasGroup reviewInputGroup;
    private bool reviewPreviousInteractable, reviewPreviousRaycasts;
    private GameObject reviewPreviousSelection;
    private bool reviewStrategicObserved;
    private UltraProjectStatus reviewProjectStatus;
    private bool reviewRelicSuspended;

#if UNITY_EDITOR
    public bool ReviewConfirmationPendingForEditor => pendingReviewAction != null;
    public void ShowBuildingDetailsForEditor(BuildingDef building) => ShowBuildingDetails(building);
    public void ShowResearchDetailsForEditor(ResearchDef research) => ShowResearchDetails(research);
    public void ShowSectorDetailsForEditor(SectorDef sector) => RefreshSectorDetails(sector);
    public BuildingDef SelectedBuildingForEditor => selectedBuilding;
    public Button ReviewLocateForEditor => reviewLocate;
    public Button ReviewRepairForEditor => reviewRepair;
    public Button GetDeconstructButtonForEditor(BuildingDef building) => buildingDeconstructButtons.TryGetValue(building, out Button button) ? button : null;
#endif

    private void BindReviewControls(Transform safeArea, Transform content)
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIReviewControls");
        if (prefab == null) { Debug.LogError("Missing authored KingdomUIReviewControls prefab."); return; }
        Transform root = Instantiate(prefab, safeArea, false).transform;
        root.name = "ReviewControls";
        reviewInputGroup = safeArea.GetComponent<CanvasGroup>();
        if (reviewInputGroup == null) reviewInputGroup = safeArea.gameObject.AddComponent<CanvasGroup>();
        reviewConfirmation = root.Find("Confirmation").gameObject;
        reviewWarning = root.Find("Confirmation/Panel/Viewport/Content/Warning").GetComponent<TMP_Text>();
        root.Find("Confirmation/Panel/Actions/Confirm").GetComponent<Button>().onClick.AddListener(() =>
        {
            Action action = pendingReviewAction; CancelReviewConfirmation(); action?.Invoke();
        });
        root.Find("Confirmation/Panel/Actions/Cancel").GetComponent<Button>().onClick.AddListener(CancelReviewConfirmation);
        reviewDetailTools = root.Find("DetailTools");
        reviewDetailTools.SetParent(detailPanel.Find("DetailUI"), false);
        reviewLocate = reviewDetailTools.Find("Locate").GetComponent<Button>();
        reviewEarlier = ReviewButton(reviewDetailTools, "Earlier", () => MoveSelectedResearch(-1));
        reviewLater = ReviewButton(reviewDetailTools, "Later", () => MoveSelectedResearch(1));
        reviewRepair = ReviewButton(reviewDetailTools, "Repair", PreviewFleetRepair);
        reviewDetailHeading = detailPanel.Find("DetailUI/Header/HeaderLabel").gameObject;
        reviewPageTools = root.Find("PageTools"); reviewPageTools.SetParent(content.Find("PageTool"), false);
        reviewCurrentResearch = ReviewButton(reviewPageTools, "CurrentResearch", () => NavigateToOverviewResearch(GetCurrentResearchTarget()));
        reviewResetResearch = ReviewButton(reviewPageTools, "ResetResearch", () => researchGraphGesture?.ResetView(DefaultResearchScale));
        reviewOffline = ReviewButton(reviewPageTools, "Offline", () =>
        {
            offlineSummaryExpanded = !offlineSummaryExpanded; renderedOfflineSummary = null; RefreshUI();
        });
        reviewSave = ReviewButton(reviewPageTools, "Save", () =>
        {
            if (SaveManager.TryGetInstance(out SaveManager save))
            {
                ShowTooltip(save.SaveNow(true) ? "保存成功" : string.IsNullOrEmpty(save.LastSaveError)
                    ? "保存失败" : "保存失败：" + save.LastSaveError);
                RefreshUI();
            }
        });
        detailBody.raycastTarget = true;
        detailBody.gameObject.AddComponent<UIReviewTextLinks>().Bind(detailBody, NavigateReviewLink);
        RefreshReviewControls();
    }

    private static Button ReviewButton(Transform parent, string name, UnityEngine.Events.UnityAction action)
    {
        Button button = parent.Find(name).GetComponent<Button>(); button.onClick.AddListener(action); return button;
    }
    private void ConfirmReviewAction(string warning, Action action)
    {
        if (reviewConfirmation == null) { ShowTooltip("确认控件未就绪，操作未提交"); return; }
        if (pendingReviewAction == null)
        {
            reviewPreviousInteractable = reviewInputGroup.interactable;
            reviewPreviousRaycasts = reviewInputGroup.blocksRaycasts;
            reviewPreviousSelection = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        }
        reviewInputGroup.interactable = false; reviewInputGroup.blocksRaycasts = false;
        pendingReviewAction = action; reviewWarning.text = warning; reviewConfirmation.SetActive(true);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(reviewConfirmation.transform.Find("Panel/Actions/Confirm").gameObject);
        reviewConfirmation.transform.parent.SetAsLastSibling();
        reviewConfirmation.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition = 1;
    }
    private void CancelReviewConfirmation()
    {
        if (pendingReviewAction != null && reviewInputGroup != null)
        {
            reviewInputGroup.interactable = reviewPreviousInteractable; reviewInputGroup.blocksRaycasts = reviewPreviousRaycasts;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(reviewPreviousSelection != null && reviewPreviousSelection.activeInHierarchy ? reviewPreviousSelection : null);
        }
        pendingReviewAction = null; reviewConfirmation?.SetActive(false);
    }

    private void RefreshReviewControls()
    {
        RefreshProductionEntry();
        if (reviewDetailTools == null) return;
        ObserveStrategicSupplyPause();
        bool research = selectedResearchNode != null;
        reviewLocate.gameObject.SetActive(false);
        int index = SelectedQueueIndex();
        ResearchManager manager = ResearchManager.Instance;
        reviewEarlier.gameObject.SetActive(research && index >= 0);
        reviewLater.gameObject.SetActive(research && index >= 0);
        reviewEarlier.interactable = index > 0 && manager.CanMoveQueuedResearch(selectedResearchNode, index - 1);
        reviewLater.interactable = index >= 0 && manager.CanMoveQueuedResearch(selectedResearchNode, index + 1);
        SectorState sector = selectedSectorDefinition == null || GameManager.Instance == null ? null : GameManager.Instance.Sectors.GetState(selectedSectorDefinition);
        reviewRepair.gameObject.SetActive(sector != null && sector.CampaignCasualties > ExpantaNum.Zero);
        if (sector != null && selectedSectorDefinition != null)
            reviewRepair.interactable = GameManager.Instance.Sectors.GetMaxAffordableFleetRepair(selectedSectorDefinition, GameManager.Instance.State, ResourceManager.Instance) > ExpantaNum.Zero;
        bool hasDetailTools = (research && index >= 0) || (sector != null && sector.CampaignCasualties > ExpantaNum.Zero);
        reviewDetailTools.gameObject.SetActive(hasDetailTools);
        reviewDetailHeading.SetActive(!hasDetailTools);
        bool overview = populatedPage == "Overview", graph = populatedPage == "Research";
        reviewPageTools.gameObject.SetActive(overview || graph);
        reviewCurrentResearch.gameObject.SetActive(graph); reviewResetResearch.gameObject.SetActive(graph);
        reviewCurrentResearch.interactable = CanInspectResearch(GetCurrentResearchTarget());
        reviewSave.gameObject.SetActive(overview);
        bool summary = SaveManager.TryGetInstance(out SaveManager save) && save.LastOfflineSummary != null;
        reviewOffline.gameObject.SetActive(overview && summary);
        SetTextIfChanged(reviewOffline.GetComponentInChildren<TMP_Text>(), offlineSummaryExpanded ? "收起离线变化" : "展开全部离线变化");
        if (developmentGuidanceText != null && developmentGuidanceText.GetComponent<UIReviewTextLinks>() == null)
        {
            developmentGuidanceText.raycastTarget = true;
            developmentGuidanceText.gameObject.AddComponent<UIReviewTextLinks>().Bind(developmentGuidanceText, NavigateReviewLink);
        }
    }

    private static ResearchDef GetCurrentResearchTarget()
    {
        ResearchManager manager = ResearchManager.Instance;
        if (manager == null)
            return null;
        if (manager.ActiveResearch != null)
            return manager.ActiveResearch.Definition;
        return manager.ResearchQueue.Count > 0 &&
            manager.ResearchQueue[0].Status == ResearchStatus.WaitingResources
            ? manager.ResearchQueue[0].Definition
            : null;
    }

    private int SelectedQueueIndex()
    {
        return ResearchManager.Instance?.GetResearchOrderIndex(selectedResearchNode) ?? -1;
    }
    private void MoveSelectedResearch(int offset)
    {
        int index = SelectedQueueIndex();
        if (index >= 0 && ResearchManager.Instance.MoveQueuedResearch(selectedResearchNode, index + offset))
            ShowResearchDetails(selectedResearchNode);
        RefreshReviewControls();
    }
    private void NavigateToBuildingRow(BuildingDef building)
    {
        if (building == null) return;
        Transform row = null;
        if (building is SectorBuildingDef sectorBuilding)
        {
            SetPage("Sectors");
            foreach (SectorRowView view in sectorRowViews)
                if (view.Definition == sectorBuilding.Sector)
                {
                    if (GameManager.Instance.Sectors.GetState(view.Definition)?.Occupied == true)
                    { view.Expanded = true; RebuildSectorMenu(view); }
                    row = view.Row; break;
                }
        }
        else
        {
            SetPage("Buildings");
            if (buildingActionButtons.TryGetValue(building, out Button button)) row = button.transform.parent;
        }
        ShowBuildingDetails(building);
        Canvas.ForceUpdateCanvases();
        if (row is RectTransform rect && pageScroll != null && pageScroll.content != null)
        {
            float overflow = pageScroll.content.rect.height - pageScroll.viewport.rect.height;
            if (overflow > 0)
            {
                Vector3 local = pageScroll.content.InverseTransformPoint(rect.position);
                pageScroll.verticalNormalizedPosition = Mathf.Clamp01(1f + local.y / overflow);
            }
        }
    }
    private void NavigateReviewLink(string id)
    {
        if (id.StartsWith("building:", StringComparison.Ordinal) && DataBase<BuildingDef>.TryFind(id.Substring(9), out BuildingDef building)) NavigateToBuildingRow(building);
        else if (id.StartsWith("research:", StringComparison.Ordinal) && DataBase<ResearchDef>.TryFind(id.Substring(9), out ResearchDef research)) NavigateToOverviewResearch(research);
        else if (id.StartsWith("resource:", StringComparison.Ordinal) && DataBase<ResourceDef>.TryFind(id.Substring(9), out ResourceDef resource))
        { SetPage("Resources"); ShowResourceDetails(resource); }
        else if (id.StartsWith("sector:", StringComparison.Ordinal) && DataBase<SectorDef>.TryFind(id.Substring(7), out SectorDef sector))
        { SetPage("Sectors"); RefreshSectorDetails(sector); }
    }
    private static string ReviewLink(string type, string id, string label) => "<link=\"" + type + ":" + id + "\"><u>" + label + "</u></link>";

    private void HandleResearchUiAction(ResearchDef research)
    {
        ResearchManager manager = ResearchManager.Instance;
        if (manager == null || research == null) return;
        var preview = manager.GetCancellationPreview(research);
        if (preview.Count > 1)
        {
            StringBuilder warning = new("移出所选研究及依赖它的后续研究（不退款，付款记录和进度保留）：");
            foreach (ResearchState state in preview) warning.Append('\n').Append(state.Definition.Label);
            ConfirmReviewAction(warning.ToString(), () => ExecuteResearchUiAction(research));
        }
        else ExecuteResearchUiAction(research);
    }
    private void ExecuteResearchUiAction(ResearchDef research)
    {
        ResearchManager.Instance.HandleResearchAction(research);
        ShowResearchDetails(research, true); researchQueueUiDirty = false;
    }
    private void PreviewFleetRepair()
    {
        SectorDef sector = selectedSectorDefinition;
        if (sector == null || GameManager.Instance == null) return;
        SectorState state = GameManager.Instance.Sectors.GetState(sector);
        ExpantaNum amount = GameManager.Instance.Sectors.GetMaxAffordableFleetRepair(sector, GameManager.Instance.State, ResourceManager.Instance);
        if (amount <= ExpantaNum.Zero) { ShowTooltip("维修材料不足；可以先终止战役停止持续消耗"); return; }
        StringBuilder warning = new("当前舰队损伤："); warning.Append(state.CampaignCasualties.ToGameString()).Append("；本次可支付维修：").Append(amount.ToGameString());
        foreach (var cost in SectorManager.GetFleetRepairCosts(amount)) warning.Append('\n').Append(cost.First.Label).Append("：").Append(cost.Second.ToGameString());
        warning.Append("\n维修不会终止活动战役；确认时再次校验材料。");
        ConfirmReviewAction(warning.ToString(), () => RepairSectorFleet(sector, amount));
    }

    private void AppendResourceSourceCandidates(StringBuilder text, ResourceDef resource)
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager == null) return;
        bool stoppedHeading = false, availableHeading = false;
        foreach (var entry in manager.States)
        {
            BuildingDef building = entry.Key; BuildingState state = entry.Value;
            bool source = false;
            foreach (var flow in building.ResourceGenerationRates) if (flow.First == resource && flow.Second > ExpantaNum.Zero) { source = true; break; }
            if (!source) continue;
            if (state.Amount > ExpantaNum.Zero && state.Efficiency <= ExpantaNum.Zero)
            {
                if (!stoppedHeading) { text.AppendLine("已停工来源（点击定位）："); stoppedHeading = true; }
                text.AppendLine(ReviewLink("building", building.Id, building.Label) + "：" + GetStoppedBuildingReason(building));
            }
        }
        foreach (var entry in manager.States)
        {
            BuildingDef building = entry.Key;
            if (entry.Value.Amount > ExpantaNum.Zero || !manager.ShouldDisplay(building)) continue;
            foreach (var flow in building.ResourceGenerationRates)
                if (flow.First == resource && flow.Second > ExpantaNum.Zero)
                {
                    if (!availableHeading) { text.AppendLine("未建生产来源（点击定位）："); availableHeading = true; }
                    BuildFailure failure = manager.GetBuildFailure(building, ExpantaNum.One);
                    text.AppendLine(ReviewLink("building", building.Id, building.Label) + "：" + (failure == BuildFailure.None ? "可建" : GetBuildFailureDescription(failure)));
                    break;
                }
        }
    }

    private static string GetStoppedBuildingReason(BuildingDef building)
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager.GetState(building)?.IsEnabled == false)
            return "已停用";
        if (manager.GetLaborOperatingRatio(building) <= ExpantaNum.Zero)
            return "生产力不足，开工率为 0%";
        foreach (var flow in building.ResourceConsumptionRates)
            if (flow.Second > ExpantaNum.Zero && ResourceManager.Instance.GetState(flow.First).GetTickSatisfactionOrFallback() <= ExpantaNum.Zero)
                return "缺少 " + flow.First.Label + "，查看资源详情定位上游";
        GameState state = GameManager.Instance.State;
        if (building.PowerContribution < ExpantaNum.Zero && state.PowerSatisfaction <= ExpantaNum.Zero) return "电力不足";
        if (building.LogisticsContribution < ExpantaNum.Zero && state.LogisticsSatisfaction <= ExpantaNum.Zero) return "物流不足";
        return "当前供给未满足；查看产出与持续消耗";
    }

    private static bool HasRemainingInterstellarTargets()
    {
        if (GameManager.Instance == null) return false;
        foreach (SectorState state in GameManager.Instance.Sectors.OrderedStates)
            if (!state.Definition.IsHomeSystem && !state.Occupied) return true;
        return false;
    }

    private static void AppendUltraBudget(StringBuilder text, UltraProjectPreview preview)
    {
        if (preview.ProgressPerSecond <= ExpantaNum.Zero)
        { text.AppendLine("剩余工期：当前供给不能推进，先补持续供给；暂停保留付款与进度。"); return; }
        ExpantaNum seconds = (ExpantaNum.One - ExpantaNum.Clamp01(preview.Progress)) / preview.ProgressPerSecond;
        text.AppendLine("按当前姿态与供给剩余工期：" + seconds.ToGameString() + " 秒（状态变化后重算）");
        text.AppendLine("维持当前供给至完成预计耗粮：" + (preview.FoodPerSecond * seconds * preview.SupplySatisfaction).ToGameString());
        foreach (var cost in preview.ContinuousCosts)
        {
            ExpantaNum remaining = cost.Second * seconds * preview.SupplySatisfaction;
            ExpantaNum inventory = ResourceManager.Instance.GetAmount(cost.First);
            text.AppendLine("持续预算 " + cost.First.Label + "：" + remaining.ToGameString() + "；当前库存缺口：" + ExpantaNum.Max(ExpantaNum.Zero, remaining - inventory).ToGameString() + "（未计未来产出）");
        }
    }

    private static void AppendCampaignBudget(StringBuilder text, SectorCampaignPreview preview)
    {
        if (preview.ProgressPerSecond <= ExpantaNum.Zero)
        { text.AppendLine("当前战力 / 供给不能推进；继续会产生伤亡，先整备或终止战役。"); return; }
        if (!preview.HasSupply) text.AppendLine("当前补给不足，无法维持推进；以下预算按补齐供给后、维持当前舰队与姿态估算。");
        ExpantaNum seconds = preview.EstimatedSecondsRemaining;
        text.AppendLine("维持当前舰队与供给，预计还需：" + seconds.ToGameString() + " 秒；持续食物投入：" + (preview.FoodCostPerSecond * seconds).ToGameString());
        text.AppendLine("预计剩余伤亡：" + (preview.CasualtiesPerSecond * seconds).ToGameString() + "；当前库存可维持：" + (preview.HasOngoingSupplyCost ? preview.EstimatedSupplySeconds.ToGameString() + " 秒" : "无限") + "（不计未来产出）");
        foreach (var cost in preview.ResourceCostsPerSecond)
        {
            ExpantaNum total = cost.Second * seconds;
            text.AppendLine("持续预算 " + cost.First.Label + "：" + total.ToGameString() + "；库存缺口：" + ExpantaNum.Max(ExpantaNum.Zero, total - ResourceManager.Instance.GetAmount(cost.First)).ToGameString());
        }
        text.AppendLine("终止后停止持续消耗，未占领进度缓慢回退；遗迹服役支援清除且不返还。");
    }

    private void ObserveStrategicSupplyPause()
    {
        if (GameManager.Instance == null) return;
        UltraProjectState project = GameManager.Instance.UltraProject.State;
        RelicState relic = GameManager.Instance.Relic.State;
        if (reviewStrategicObserved &&
            ((reviewProjectStatus == UltraProjectStatus.Running && project.Status == UltraProjectStatus.Paused && project.PauseReason == UltraProjectPauseReason.InsufficientSupply) ||
             (!reviewRelicSuspended && relic.Suspended && relic.PauseReason == RelicPauseReason.InsufficientSupply)))
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicSupplyPause);
        reviewStrategicObserved = true; reviewProjectStatus = project.Status; reviewRelicSuspended = relic.Suspended;
    }
}
