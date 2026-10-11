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
    public void ShowBuildingDetailsForEditor(Building building) => ShowBuildingDetails(building);
    public void ShowResearchDetailsForEditor(Research research) => ShowResearchDetails(research);
    public void ShowSectorDetailsForEditor(SectorDefinition sector) => RefreshSectorDetails(sector);
    public void NavigateReviewLinkForEditor(string id) => NavigateReviewLink(id);
    public Building SelectedBuildingForEditor => selectedBuilding;
    public Button ReviewLocateForEditor => reviewLocate;
    public Button ReviewRepairForEditor => reviewRepair;
    public Button GetDeconstructButtonForEditor(Building building) => buildingDeconstructButtons.TryGetValue(building, out Button button) ? button : null;
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
        reviewLocate = ReviewButton(reviewDetailTools, "Locate", LocateSelectedDetail);
        reviewEarlier = ReviewButton(reviewDetailTools, "Earlier", () => MoveSelectedResearch(-1));
        reviewLater = ReviewButton(reviewDetailTools, "Later", () => MoveSelectedResearch(1));
        reviewRepair = ReviewButton(reviewDetailTools, "Repair", PreviewFleetRepair);
        reviewDetailHeading = detailPanel.Find("DetailUI/Header/HeaderLabel").gameObject;
        reviewPageTools = root.Find("PageTools"); reviewPageTools.SetParent(content.Find("PageTool"), false);
        reviewCurrentResearch = ReviewButton(reviewPageTools, "CurrentResearch", () => NavigateToOverviewResearch(ResearchManager.Instance?.ActiveResearch?.Definition));
        reviewResetResearch = ReviewButton(reviewPageTools, "ResetResearch", () => researchGraphGesture?.RestoreView(Vector2.zero, 1f));
        reviewOffline = ReviewButton(reviewPageTools, "Offline", () =>
        {
            offlineSummaryExpanded = !offlineSummaryExpanded; renderedOfflineSummary = null; RefreshUI();
        });
        reviewSave = ReviewButton(reviewPageTools, "Save", () =>
        {
            if (SaveManager.TryGetInstance(out SaveManager save))
            {
                save.SaveNow(true); ShowTooltip(save.LastSaveFailed ? "保存失败：" + save.LastSaveError : "保存成功"); RefreshUI();
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
        if (reviewDetailTools == null) return;
        ObserveStrategicSupplyPause();
        bool research = selectedResearchNode != null;
        reviewLocate.gameObject.SetActive(research || selectedBuilding != null);
        SetTextIfChanged(reviewLocate.GetComponentInChildren<TMP_Text>(), research ? "定位研究图" : "定位建筑列表");
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
        bool hasDetailTools = research || selectedBuilding != null || (sector != null && sector.CampaignCasualties > ExpantaNum.Zero);
        reviewDetailTools.gameObject.SetActive(hasDetailTools);
        reviewDetailHeading.SetActive(!hasDetailTools);
        bool overview = populatedPage == "Overview", graph = populatedPage == "Research";
        reviewPageTools.gameObject.SetActive(overview || graph);
        reviewCurrentResearch.gameObject.SetActive(graph); reviewResetResearch.gameObject.SetActive(graph);
        reviewCurrentResearch.interactable = manager?.ActiveResearch != null;
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

    private int SelectedQueueIndex()
    {
        var queue = ResearchManager.Instance?.ResearchQueue;
        if (queue != null) for (int i = 0; i < queue.Count; i++) if (queue[i].Definition == selectedResearchNode) return i;
        return -1;
    }
    private void MoveSelectedResearch(int offset)
    {
        int index = SelectedQueueIndex();
        if (index >= 0 && ResearchManager.Instance.MoveQueuedResearch(selectedResearchNode, index + offset))
        { RefreshResearchQueueToolbar(); ShowResearchDetails(selectedResearchNode); }
        RefreshReviewControls();
    }
    private void LocateSelectedDetail()
    {
        if (selectedResearchNode != null) NavigateToOverviewResearch(selectedResearchNode);
        else if (selectedBuilding != null) NavigateToBuildingRow(selectedBuilding);
    }

    private void NavigateToBuildingRow(Building building)
    {
        if (building == null) return;
        Transform row = null;
        if (building is SectorBuilding sectorBuilding)
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
        RefreshReviewControls();
    }
    private void NavigateReviewLink(string id)
    {
        if (id.StartsWith("building:", StringComparison.Ordinal) && DataBase<Building>.TryFind(id.Substring(9), out Building building)) NavigateToBuildingRow(building);
        else if (id.StartsWith("research:", StringComparison.Ordinal) && DataBase<Research>.TryFind(id.Substring(9), out Research research)) NavigateToOverviewResearch(research);
        else if (id.StartsWith("sector:", StringComparison.Ordinal) && DataBase<SectorDefinition>.TryFind(id.Substring(7), out SectorDefinition sector))
        { SetPage("Sectors"); RefreshSectorDetails(sector); }
    }
    private static string ReviewLink(string type, string id, string label) => "<link=\"" + type + ":" + id + "\"><u>" + label + "</u></link>";

    private void HandleResearchUiAction(Research research)
    {
        ResearchManager manager = ResearchManager.Instance;
        if (manager == null || research == null) return;
        var preview = manager.GetCancellationPreview(research);
        if (preview.Count > 1)
        {
            StringBuilder warning = new("移出研究及其依赖队列（已支付材料不退还，付款台账和研究进度保留）：");
            foreach (ResearchState state in preview) warning.Append('\n').Append(state.Definition.Label);
            ConfirmReviewAction(warning.ToString(), () => ExecuteResearchUiAction(research));
        }
        else ExecuteResearchUiAction(research);
    }
    private void ExecuteResearchUiAction(Research research)
    {
        ResearchActionResult result = ResearchManager.Instance.HandleResearchAction(research);
        ShowResearchDetails(research, true); RefreshResearchQueueToolbar(); researchQueueUiDirty = false;
        if (detailBody != null) detailBody.text += "\n\n执行结果：" + result.GetDescription();
        RefreshReviewControls();
    }
    private void PreviewFleetRepair()
    {
        SectorDefinition sector = selectedSectorDefinition;
        if (sector == null || GameManager.Instance == null) return;
        SectorState state = GameManager.Instance.Sectors.GetState(sector);
        ExpantaNum amount = GameManager.Instance.Sectors.GetMaxAffordableFleetRepair(sector, GameManager.Instance.State, ResourceManager.Instance);
        if (amount <= ExpantaNum.Zero) { ShowTooltip("维修材料不足；可以先终止战役停止持续消耗"); return; }
        StringBuilder warning = new("当前舰队损伤："); warning.Append(state.CampaignCasualties.ToGameString()).Append("；本次可支付维修：").Append(amount.ToGameString());
        foreach (var cost in SectorManager.GetFleetRepairCosts(amount)) warning.Append('\n').Append(cost.First.Label).Append("：").Append(cost.Second.ToGameString());
        warning.Append("\n维修不会终止活动战役；确认时再次校验材料。");
        ConfirmReviewAction(warning.ToString(), () => RepairSectorFleet(sector));
    }

    private static void AppendSaveFeedback(StringBuilder text)
    {
        if (!SaveManager.TryGetInstance(out SaveManager save)) return;
        text.Append("\n\n存档：");
        if (save.LastSaveFailed) text.Append("保存失败，使用“保存 / 重试”重试：").Append(save.LastSaveError);
        else if (save.LastSuccessfulSaveUnixSeconds > 0)
            text.Append("最近成功 ").Append(DateTimeOffset.FromUnixTimeSeconds(save.LastSuccessfulSaveUnixSeconds).ToLocalTime().ToString("HH:mm:ss"));
        else text.Append("本次尚未成功保存");
        if (save.LastLoadFailed) text.Append("\n未加载有效存档，已开始新游戏。");
        text.Append("\n食物日常净流含人口与常规生产；战略需粮另列工程、遗迹和远征需求。供给阻塞时实际扣除可能低于需求。储粮和幸福不能代替持续供粮。");
    }

    private void AppendResourceSourceCandidates(StringBuilder text, Resource resource)
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager == null) return;
        bool stoppedHeading = false, availableHeading = false;
        foreach (var entry in manager.States)
        {
            Building building = entry.Key; BuildingState state = entry.Value;
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
            Building building = entry.Key;
            if (entry.Value.Amount > ExpantaNum.Zero || !manager.ShouldDisplay(building)) continue;
            foreach (var flow in building.ResourceGenerationRates)
                if (flow.First == resource && flow.Second > ExpantaNum.Zero)
                {
                    if (!availableHeading) { text.AppendLine("已解锁可建来源（点击定位）："); availableHeading = true; }
                    BuildFailure failure = manager.GetBuildFailure(building, ExpantaNum.One);
                    text.AppendLine(ReviewLink("building", building.Id, building.Label) + "：" + (failure == BuildFailure.None ? "可建" : GetBuildFailureDescription(failure)));
                    break;
                }
        }
    }

    private static string GetStoppedBuildingReason(Building building)
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager.AvailableProductivity < ExpantaNum.Zero) return "生产力不足；补人口、供粮或减少高负荷建筑";
        foreach (var flow in building.ResourceConsumptionRates)
            if (flow.Second > ExpantaNum.Zero && ResourceManager.Instance.GetState(flow.First).GetTickSatisfactionOrFallback() <= ExpantaNum.Zero)
                return "缺少 " + flow.First.Label + "，查看资源详情定位上游";
        GameState state = GameManager.Instance.State;
        if (building.PowerConsumptionRate > ExpantaNum.Zero && state.PowerSatisfaction <= ExpantaNum.Zero) return "电力不足";
        if (building.LogisticsConsumptionRate > ExpantaNum.Zero && state.LogisticsSatisfaction <= ExpantaNum.Zero) return "物流不足";
        return "当前供给或生产力未满足；查看产出与持续消耗";
    }

    private static bool HasRemainingInterstellarTargets()
    {
        if (GameManager.Instance == null) return false;
        foreach (SectorState state in GameManager.Instance.Sectors.OrderedStates)
            if (!state.Definition.IsHomeSystem && !state.Occupied) return true;
        return false;
    }

    private static void AppendBuildingInvestmentExplanation(StringBuilder text, Building building)
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager == null) return;
        if (building.PopulationCapacityGranted > ExpantaNum.Zero)
        {
            text.AppendLine("住房先增加人口容量，居民入住后才增加生产力和持续耗粮；不会即时赠送人口。容量 +" + building.PopulationCapacityGranted.ToGameString());
            text.AppendLine("每座满员新增日常需粮：" + (building.PopulationCapacityGranted * PopulationState.FoodConsumptionPerPerson).ToGameString() + "/s；入住后生产力：" + (building.PopulationCapacityGranted * PopulationState.ProductivityGrantedPerPerson * ProgressionModifierManager.Current.PopulationProductivityMultiplier).ToGameString());
        }
        if (manager.GetState(building)?.Amount > ExpantaNum.Zero && manager.TryGetUnlockedUpgradeTarget(building, out Building target))
        {
            text.AppendLine("每座升级为 " + target.Label + " 的基础增量（产出另受倍率与供给影响）：");
            if (!(building is SectorBuilding)) AppendInvestmentDelta(text, "土地需求", target.SpaceCost - building.SpaceCost);
            AppendInvestmentDelta(text, "生产力需求", target.ProductivityConsumption - building.ProductivityConsumption);
            AppendInvestmentDelta(text, "直接生产力", target.ProductivityGranted - building.ProductivityGranted);
            AppendInvestmentDelta(text, "日常净粮 /s", target.FoodProductionRate - target.FoodConsumptionRate - building.FoodProductionRate + building.FoodConsumptionRate);
            ExpantaNum residents = target.PopulationCapacityGranted - building.PopulationCapacityGranted;
            AppendInvestmentDelta(text, "人口容量", residents);
            if (residents != ExpantaNum.Zero)
            {
                AppendInvestmentDelta(text, "新增居民满员需粮 /s", residents * PopulationState.FoodConsumptionPerPerson);
                AppendInvestmentDelta(text, "入住后生产力", residents * PopulationState.ProductivityGrantedPerPerson * ProgressionModifierManager.Current.PopulationProductivityMultiplier);
            }
            AppendInvestmentDelta(text, "粮食容量", (target.FoodCapacityGranted - building.FoodCapacityGranted) * ProgressionModifierManager.Current.FoodCapacityMultiplier);
            AppendInvestmentDelta(text, "科研能力", target.ResearchPowerGranted - building.ResearchPowerGranted);
            AppendInvestmentDelta(text, "电力需求 /s", target.PowerConsumptionRate - building.PowerConsumptionRate);
            AppendInvestmentDelta(text, "物流需求 /s", target.LogisticsConsumptionRate - building.LogisticsConsumptionRate);
            foreach (var flow in target.ResourceConsumptionRates)
                text.AppendLine("升级后基础持续投入 " + flow.First.Label + "：" + flow.Second.ToGameString() + "/s（生产倍率也增加吞吐需求）");
        }
        if (building.FoodCapacityGranted > ExpantaNum.Zero)
            text.AppendLine("粮仓只延长可离开时间，不增加持续供粮；扩住房前先检查日常净粮和战略需粮。");
        if (building.FoodProductionRate > ExpantaNum.Zero)
        {
            if (building.SpaceCost > ExpantaNum.Zero) text.AppendLine("基础粮食土地密度：" + (building.FoodProductionRate / building.SpaceCost).ToGameString() + "/s/土地");
            if (building.ProductivityConsumption > ExpantaNum.Zero) text.AppendLine("基础单位生产力粮产：" + (building.FoodProductionRate / building.ProductivityConsumption).ToGameString() + "/s；更高土地密度不保证更高劳动效率，升级也不直接释放土地。");
        }
        if (building.ResearchPowerGranted > ExpantaNum.Zero)
            text.AppendLine("科研投资先占用材料与生产力，收益须在同一时段比较；已付款研究保持进度，缺料时投资科研不能替代生产链。");
        if (building.Id == "AutonomousMatterFabricator" || building.PowerConsumptionRate > ExpantaNum.Zero || building.LogisticsConsumptionRate > ExpantaNum.Zero)
        {
            GameState state = GameManager.Instance.State;
            text.AppendLine("新增一座的生产力需求：" + building.ProductivityConsumption.ToGameString() + "；当前余量：" + manager.AvailableProductivity.ToGameString());
            text.AppendLine("新增电力需求：" + building.PowerConsumptionRate.ToGameString() + "/s；当前全负荷余量：" + (state.PowerProductionRate * state.HappinessRewardMultiplier - manager.TotalPowerDemand).ToGameString() + "/s");
            text.AppendLine("新增物流需求：" + building.LogisticsConsumptionRate.ToGameString() + "/s；当前全负荷余量：" + (state.LogisticsProductionRate * state.HappinessRewardMultiplier - manager.TotalLogisticsDemand).ToGameString() + "/s。负余量会拖累现有链，需先筹备供给。");
        }
    }

    private static void AppendInvestmentDelta(StringBuilder text, string label, ExpantaNum delta)
    {
        if (delta != ExpantaNum.Zero)
            text.AppendLine(label + "：" + (delta > ExpantaNum.Zero ? "+" : string.Empty) + delta.ToGameString());
    }

    private static void AppendUltraBudget(StringBuilder text, UltraProjectPreview preview)
    {
        if (preview.ProgressPerSecond <= ExpantaNum.Zero)
        { text.AppendLine("剩余工期：当前供给不能推进，先补持续供给；暂停保留付款与进度。"); return; }
        ExpantaNum seconds = (ExpantaNum.One - ExpantaNum.Clamp01(preview.Progress)) / preview.ProgressPerSecond;
        text.AppendLine("按当前姿态与供给剩余工期：" + seconds.ToGameString() + " 秒（状态变化后重算）");
        text.AppendLine("按当前供给冻结条件预计剩余持续食物投入：" + (preview.FoodPerSecond * seconds * preview.SupplySatisfaction).ToGameString());
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
        if (!preview.HasSupply) text.AppendLine("当前补给不足，无法维持推进；以下预算按补足供给后、当前战力与姿态冻结估计。");
        ExpantaNum seconds = preview.EstimatedSecondsRemaining;
        text.AppendLine("按当前状态冻结预计剩余工期：" + seconds.ToGameString() + " 秒；持续食物投入：" + (preview.FoodCostPerSecond * seconds).ToGameString());
        text.AppendLine("预计剩余伤亡：" + (preview.CasualtiesPerSecond * seconds).ToGameString() + "；当前库存可维持：" + preview.EstimatedSupplySeconds.ToGameString() + " 秒（不计未来产出）");
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
