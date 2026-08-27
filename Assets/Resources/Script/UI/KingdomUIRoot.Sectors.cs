using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the sector loop without changing sector rules. Home-system sectors
/// are shown as exploration objectives; interstellar sectors are shown as
/// supply-heavy campaigns. The manager remains the only authority for actions.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private readonly Dictionary<SectorDefinition, TMP_Text> sectorSummaryLabels = new();
    private readonly StringBuilder sectorDetailTextBuilder = new(1024);
    private readonly StringBuilder sectorCostTextBuilder = new(256);
    private SectorDefinition selectedSectorDefinition;
    private int lastSelectedSectorActionSignature = int.MinValue;
    private float sectorPageRefreshTimer;

    private void BuildSectorRows(RectTransform parent)
    {
        if (parent == null)
            return;

        sectorSummaryLabels.Clear();
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        SectorManager sectorManager = gameManager == null ? null : gameManager.Sectors;
        GameState state = gameManager == null ? null : gameManager.State;
        IReadOnlyList<SectorDefinition> definitions = DataBase<SectorDefinition>.All;
        int visible = 0;

        for (int i = 0; i < definitions.Count; i++)
        {
            SectorDefinition definition = definitions[i];
            if (definition == null)
                continue;

            GameObject row = InstantiateAuthoredRow(KingdomUIPrefabLibrary.TextRow, parent, visible++);
            if (row == null)
                continue;
            ApplyListRowStyle(row, visible - 1);

            string title = string.IsNullOrEmpty(definition.Label) ? definition.Id : definition.Label;
            string subtitle = BuildSectorSummary(definition, sectorManager, state, resourceManager);
            if (!SetRowText(row, "Title", title) || !SetRowText(row, "Subtitle", subtitle))
                continue;
            sectorSummaryLabels[definition] =
                row.transform.Find("Subtitle").GetComponent<TMP_Text>();

            Button button = RequireRowButton(row);
            if (button == null)
                continue;
            button.onClick.AddListener(() => ShowSectorDetails(definition, sectorManager, state, resourceManager));
        }

        Debug.Log($"[王国界面] Sector rows: visible={visible}, rowsRect={parent.rect.size}");
    }

    private void RefreshSectorRowSummaries()
    {
        if (gameManagerCache == null)
            return;
        SectorManager sectorManager = gameManagerCache.Sectors;
        GameState state = gameManagerCache.State;
        ObserveSectorMilestones(sectorManager);
        foreach (KeyValuePair<SectorDefinition, TMP_Text> pair in sectorSummaryLabels)
            SetTextIfChanged(
                pair.Value,
                BuildSectorSummary(
                    pair.Key,
                    sectorManager,
                    state,
                    resourceManagerCache));
        RefreshSelectedSectorDetails(sectorManager, state, resourceManagerCache);
    }

    private void ObserveSectorMilestones(SectorManager sectorManager)
    {
        if (sectorManager == null)
            return;

        foreach (SectorState state in sectorManager.OrderedStates)
        {
            SectorDefinition definition = state == null ? null : state.Definition;
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                continue;

            string id = definition.Id;
            ExpantaNum progress = state.CampaignProgress;
            ExpantaNum previousCasualties = observedSectorCasualties.TryGetValue(
                id, out ExpantaNum recordedCasualties)
                ? recordedCasualties
                : ExpantaNum.Zero;
            bool wasOccupied = observedOccupiedSectorIds.Contains(id);
            if (sectorObservationInitialized)
            {
                if (!wasOccupied && state.Occupied)
                {
                    EnqueueRecentNotice("星区纳入王国：" + definition.Label +
                        " 已成为鼠族文明新的责任与资源来源。");
                }
                else if (observedSectorProgress.TryGetValue(id, out ExpantaNum previousProgress) &&
                    previousProgress < ExpantaNum.One && progress >= ExpantaNum.One)
                {
                    EnqueueRecentNotice("远征完成：" + definition.Label +
                        " 的道路已经打通，现在可以决定是否接纳这片星区。");
                }
                else if (previousCasualties <= ExpantaNum.Zero &&
                    state.CampaignCasualties > ExpantaNum.Zero)
                {
                    EnqueueRecentNotice("战报：" + definition.Label +
                        " 的远征出现伤亡；补给与维修是继续前进的代价。");
                }
            }

            observedSectorProgress[id] = progress;
            observedSectorCasualties[id] = state.CampaignCasualties;
            if (state.Occupied)
                observedOccupiedSectorIds.Add(id);
        }

        sectorObservationInitialized = true;
    }

    private string BuildSectorSummary(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        SectorState sectorState = sectorManager == null ? null : sectorManager.GetState(definition);
        if (sectorState != null && sectorState.Occupied)
            return "已占领  |  持续产出 " + FormatResourceCosts(
                definition.OccupiedResourceRatesPerSecond,
                ProgressionModifierManager.Current) + "/s";
        string progress = sectorState == null ? "0%" : (sectorState.CampaignProgress * 100).ToGameString() + "%";
        if (definition.IsHomeSystem)
        {
            string attack = state == null ? "0" : state.AttackPower.ToGameString();
            string defense = state == null ? "0" : state.DefensePower.ToGameString();
            return "本星系探索  |  攻击 " + attack + "/" + definition.EnemyPower.ToGameString() +
                "  防御 " + defense + "/" + definition.EnemyPower.ToGameString() +
                "  | 进度 " + progress;
        }

        if (state == null || sectorManager == null)
            return "远星战役  |  需要深空舰队  |  进度 " + progress;

        SectorCampaignPreview preview = sectorManager.GetCampaignPreview(definition, state, resourceManager);
        return "远星战役  |  战斗比率 " + preview.CombatRatio.ToGameString() +
            "  生存倍率 " + preview.FleetSurvivalFactor.ToGameString() +
            "  | 进度 " + progress;
    }

    private void ShowSectorDetails(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        if (definition == null)
            return;

        // 星区详情不能继承残留的研究节点选择，否则通用按钮会被改回研究队列操作。
        selectedResearchNode = null;
        ShowDetails(
            definition.Label,
            BuildSectorDetailDescription(
                definition, sectorManager, state, resourceManager),
            definition.Id);
        selectedSectorDefinition = definition;
        ConfigureSectorAction(definition, sectorManager, state, resourceManager);
        lastSelectedSectorActionSignature = GetSectorActionSignature(
            definition, sectorManager, state, resourceManager);
    }

    private string BuildSectorDetailDescription(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        StringBuilder body = sectorDetailTextBuilder;
        body.Clear();
        body.AppendLine(definition.Label ?? definition.Id);
        body.AppendLine();
        body.AppendLine(definition.Description ?? string.Empty);
        body.AppendLine();
        body.AppendLine(definition.IsHomeSystem ? "本星系探索" : "远星星区战役");
        body.AppendLine("状态：" + GetSectorStateText(sectorManager, definition));
        IReadOnlyList<SectorDefinition> prerequisites = definition.PrerequisiteSectors;
        if (prerequisites == null || prerequisites.Count == 0)
            body.AppendLine("直接前置星区：无");
        else
        {
            body.AppendLine("直接前置星区：");
            for (int i = 0; i < prerequisites.Count; i++)
            {
                SectorDefinition prerequisite = prerequisites[i];
                if (prerequisite == null)
                    continue;
                string prerequisiteState = sectorManager == null
                    ? "状态未知"
                    : sectorManager.GetState(prerequisite).Occupied ? "已占领" : "未占领";
                body.Append("  ")
                    .Append(string.IsNullOrEmpty(prerequisite.Label) ? prerequisite.Id : prerequisite.Label)
                    .Append(" / ")
                    .AppendLine(prerequisiteState);
            }
        }
        IReadOnlyList<Pair<Resource, ExpantaNum>> occupiedRates =
            definition.OccupiedResourceRatesPerSecond;
        body.AppendLine("当前占领持续产出：" +
            (occupiedRates == null || occupiedRates.Count == 0
                ? "无"
                : FormatResourceCosts(
                    occupiedRates,
                    ProgressionModifierManager.Current) + "/s"));

        if (state == null)
        {
            body.AppendLine("运行时状态尚未初始化。");
        }
        else if (definition.IsHomeSystem)
        {
            SectorExplorationPreview explorationPreview = sectorManager == null
                ? null
                : sectorManager.GetExplorationPreview(definition, state, resourceManager);
            if (explorationPreview != null)
            {
                body.AppendLine("探索能力：" + explorationPreview.ExplorationPower.ToGameString() +
                    "/" + explorationPreview.RequiredPower.ToGameString());
                body.AppendLine("预计剩余：" + explorationPreview.EstimatedSecondsRemaining.ToGameString() + " 秒");
                body.AppendLine("食物补给：" + explorationPreview.FoodCostPerSecond.ToGameString() + "/s");
                body.AppendLine("战略资源补给：" + FormatResourceCosts(explorationPreview.ResourceCostsPerSecond) + "/s");
                body.AppendLine(explorationPreview.HasSupply ? "当前补给：充足" : "当前补给：不足");
            }
            body.AppendLine("探索要求：攻击力 ≥ " + definition.EnemyPower.ToGameString());
            body.AppendLine("舰队生存要求：防御力 ≥ " + definition.EnemyPower.ToGameString());
            body.AppendLine("当前攻击力：" + state.AttackPower.ToGameString());
            body.AppendLine("当前防御力：" + state.DefensePower.ToGameString());
            body.AppendLine("提示：本星系不进行星区战役。先发展生产和舰队，再回来完成探索。");
        }
        else if (sectorManager != null)
        {
            SectorCampaignPreview preview = sectorManager.GetCampaignPreview(definition, state, resourceManager);
            body.AppendLine("领土回报：" + definition.TerritoryReward.ToGameString());
            body.AppendLine("占领资源回报：" + FormatResourceCosts(definition.ResourceRewards));
            body.AppendLine("战斗比率：" + preview.CombatRatio.ToGameString());
            body.AppendLine("舰队生存倍率：" + preview.FleetSurvivalFactor.ToGameString());
            body.AppendLine("舰队整备度：" + preview.FleetReadiness.ToGameString());
            body.AppendLine("当前伤亡：" + preview.CurrentCasualties.ToGameString());
            body.AppendLine("补给满意度：" + preview.SupplySatisfaction.ToGameString());
            body.AppendLine("电力满意度：" + preview.PowerSatisfaction.ToGameString());
            body.AppendLine("物流满意度：" + preview.LogisticsSatisfaction.ToGameString());
                body.AppendLine("推进速度：" + preview.ProgressPerSecond.ToGameString() + "/s");
            body.AppendLine(preview.EstimatedSecondsRemaining > ExpantaNum.Zero
                ? "预计完成：" + preview.EstimatedSecondsRemaining.ToGameString() + " 秒"
                : "预计完成：无法估算（当前条件不支持推进）");
            body.AppendLine("预计伤亡：" + preview.CasualtiesPerSecond.ToGameString() + "/s");
            body.AppendLine("食物补给：" + preview.FoodCostPerSecond.ToGameString() + "/s");
            body.AppendLine("战略资源补给：" + FormatResourceCosts(preview.ResourceCostsPerSecond) + "/s");
            body.AppendLine(preview.HasSupply ? "当前补给：足够" : "当前补给：不足");
            if (preview.ProgressPerSecond <= ExpantaNum.Zero)
                body.AppendLine("警告：当前战斗或后勤条件不足，战役不会推进，继续行动只会增加伤亡。");
            else if (preview.LogisticsSatisfaction < ExpantaNum.One ||
                preview.SupplySatisfaction < ExpantaNum.One ||
                preview.PowerSatisfaction < ExpantaNum.One)
                body.AppendLine("提示：补给尚未完善，战役推进速度和舰队安全性低于最佳状态。");
            body.AppendLine("提示：远星战役是长期后勤行动，建议先完成舰队、燃料和先进材料准备。");
        }
        return body.ToString();
    }

    private void RefreshSelectedSectorDetails(
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        SectorDefinition definition = selectedSectorDefinition;
        if (definition == null || detailBody == null)
            return;

        string value = definition.Label + "\n\n" +
            BuildSectorDetailDescription(
                definition, sectorManager, state, resourceManager) +
            "\n\n标识：" + definition.Id;
        if (detailBody.text != value)
        {
            detailBody.text = value;
            LayoutResourceDetailsBody();
        }

        int actionSignature = GetSectorActionSignature(
            definition, sectorManager, state, resourceManager);
        if (actionSignature == lastSelectedSectorActionSignature)
            return;
        ConfigureSectorAction(definition, sectorManager, state, resourceManager);
        lastSelectedSectorActionSignature = actionSignature;
    }

    private static int GetSectorActionSignature(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        if (definition == null || sectorManager == null || state == null)
            return 0;
        SectorState sectorState = sectorManager.GetState(definition);
        int signature = sectorState.Unlocked ? 1 : 0;
        if (sectorState.Occupied) signature |= 2;
        if (sectorState.ColonizationActive) signature |= 4;
        if (sectorState.CampaignActive) signature |= 8;
        if (sectorState.CampaignProgress >= ExpantaNum.One) signature |= 16;
        if (sectorState.CampaignCasualties > ExpantaNum.Zero) signature |= 32;
        if (resourceManager != null) signature |= 64;
        if (!sectorState.Unlocked)
            signature |= (int)sectorManager.GetUnlockFailure(definition) << 7;
        return signature;
    }

    private void ConfigureSectorAction(
        SectorDefinition definition,
        SectorManager sectorManager,
        GameState state,
        ResourceManager resourceManager)
    {
        if (definition == null || sectorManager == null || state == null)
            return;

        SectorState sectorState = sectorManager.GetState(definition);
        if (sectorState == null)
            return;
        if (sectorState.Occupied)
        {
            if (detailActionButton != null)
                detailActionButton.gameObject.SetActive(false);
            return;
        }

        // 已完成的远星战役必须直接显示占领操作。
        if (sectorState.CampaignProgress >= ExpantaNum.One)
        {
            ConfigureActionButton("\u5360\u9886\u661f\u533a", () => OccupySector(definition));
            return;
        }

        // 保持现有单按钮详情结构。舰队有伤亡时优先维修，避免损坏舰队被带入新战役。
        if (sectorState.CampaignCasualties > ExpantaNum.Zero && resourceManager != null)
        {
            ConfigureActionButton("\u7ef4\u4fee\u8230\u961f", () => RepairSectorFleet(definition));
            return;
        }

        if (!sectorState.Unlocked)
        {
            SectorOperationFailure failure = sectorManager.GetUnlockFailure(definition);
            bool canUnlock = failure == SectorOperationFailure.None;
            ConfigureActionButton(
                canUnlock ? "\u89e3\u9501\u661f\u533a" : failure.GetDescription(),
                () => UnlockSector(definition));
            if (detailActionButton != null)
                detailActionButton.interactable = canUnlock;
            return;
        }

        if (definition.IsHomeSystem)
        {
            ConfigureActionButton(
                sectorState.ColonizationActive ? "\u6682\u505c\u661f\u533a\u63a2\u7d22" : "\u5f00\u59cb\u661f\u533a\u63a2\u7d22",
                sectorState.ColonizationActive
                    ? (UnityEngine.Events.UnityAction)(() => PauseColonization(definition))
                    : () => StartColonization(definition));
            return;
        }

        if (sectorState.CampaignActive)
        {
            ConfigureActionButton("\u6682\u505c\u8fdc\u661f\u6218\u5f79", () => PauseCampaign(definition));
            return;
        }

        ConfigureActionButton("\u5f00\u59cb\u8fdc\u661f\u6218\u5f79", () => StartCampaign(definition));
    }

    private void UnlockSector(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        SectorOperationFailure failure = SectorOperationFailure.None;
        if (gameManager == null || !gameManager.Sectors.TryUnlock(definition, out failure))
        {
            ShowTooltip("\u661f\u533a\u89e3\u9501\u5931\u8d25\uff1a" + failure.GetDescription());
            return;
        }

        ShowTooltip("\u661f\u533a\u5df2\u89e3\u9501");
        RefreshSectorDetails(definition);
    }

    private void StartColonization(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        SectorOperationFailure failure = SectorOperationFailure.None;
        if (gameManager == null || !gameManager.Sectors.TryAdvanceColonization(
                definition, 0d, gameManager.State, resourceManager, out failure))
        {
            ShowTooltip("\u5f00\u59cb\u63a2\u7d22\u5931\u8d25\uff1a" + failure.GetDescription());
            return;
        }

        ShowTooltip("\u661f\u533a\u63a2\u7d22\u5df2\u5f00\u59cb");
        RefreshSectorDetails(definition);
    }

    private void PauseColonization(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        if (gameManager == null || !gameManager.Sectors.CancelColonization(definition))
        {
            ShowTooltip("\u6682\u505c\u63a2\u7d22\u5931\u8d25");
            return;
        }

        ShowTooltip("\u661f\u533a\u63a2\u7d22\u5df2\u6682\u505c");
        RefreshSectorDetails(definition);
    }

    private void StartCampaign(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        SectorOperationFailure failure = SectorOperationFailure.None;
        if (gameManager == null || !gameManager.Sectors.TryAdvanceCampaign(
                definition, 0d, gameManager.State, resourceManager, out failure))
        {
            ShowTooltip("\u5f00\u59cb\u6218\u5f79\u5931\u8d25\uff1a" + failure.GetDescription());
            return;
        }

        ShowTooltip("\u8fdc\u661f\u6218\u5f79\u5df2\u5f00\u59cb");
        RefreshSectorDetails(definition);
    }

    private void PauseCampaign(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        if (gameManager == null || !gameManager.Sectors.CancelCampaign(definition, gameManager.State))
        {
            ShowTooltip("\u6682\u505c\u6218\u5f79\u5931\u8d25");
            return;
        }

        ShowTooltip("\u8fdc\u661f\u6218\u5f79\u5df2\u6682\u505c");
        RefreshUI();
    }

    private void OccupySector(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        SectorOperationFailure failure = SectorOperationFailure.None;
        if (gameManager == null || !gameManager.Sectors.TryOccupy(definition, out failure))
        {
            ShowTooltip("\u5360\u9886\u661f\u533a\u5931\u8d25\uff1a" + failure.GetDescription());
            return;
        }

        EnqueueRecentNotice("占领完成：" + definition.Label +
            " 已纳入鼠族文明；领土 +" + definition.TerritoryReward.ToGameString() +
            "，资源奖励：" + FormatResourceCosts(definition.ResourceRewards) + "。");
        ShowTooltip("\u661f\u533a\u5df2\u5360\u9886");
        RefreshSectorDetails(definition);
    }

    private void RefreshSectorDetails(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        if (gameManager != null)
            ShowSectorDetails(definition, gameManager.Sectors, gameManager.State, resourceManager);
    }

    private void RepairSectorFleet(SectorDefinition definition)
    {
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        ResourceManager resourceManager = resourceManagerCache;
        if (gameManager == null || resourceManager == null || definition == null)
            return;

        bool repaired = gameManager.Sectors.TryRepairFleet(
            definition,
            gameManager.State,
            resourceManager,
            gameManager.Sectors.GetState(definition).CampaignCasualties,
            out ExpantaNum repairedAmount,
            out SectorOperationFailure failure);
        if (!repaired)
        {
            ShowTooltip("舰队维修失败：" + failure.GetDescription());
            return;
        }

        ShowTooltip("舰队已维修：" + repairedAmount.ToGameString());
        EnqueueRecentNotice("舰队维修完成：" + definition.Label +
            " 已修复 " + repairedAmount.ToGameString() +
            " 点损伤，可以继续承担这场远征。");
        ShowSectorDetails(
            definition,
            gameManager.Sectors,
            gameManager.State,
            resourceManager);
    }

    private static string GetSectorStateText(SectorManager manager, SectorDefinition definition)
    {
        if (manager == null)
            return "未初始化";
        SectorState state = manager.GetState(definition);
        if (state.Occupied)
            return "已占领";
        if (!state.Unlocked)
            return "未解锁";
        if (state.ColonizationActive)
            return "探索中";
        if (state.CampaignActive)
            return "远星战役中";
        if (state.CampaignCasualties > ExpantaNum.Zero)
            return "等待维修";
        if (state.CampaignProgress >= ExpantaNum.One)
            return "等待占领";
        return state.CampaignProgress > ExpantaNum.Zero ? "已暂停" : "已解锁";
    }

    private string FormatResourceCosts(IReadOnlyList<Pair<Resource, ExpantaNum>> costs) =>
        FormatResourceCosts(costs, ExpantaNum.One);

    private string FormatResourceCosts(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs,
        ExpantaNum multiplier)
    {
        if (costs == null || costs.Count == 0)
            return "无";
        StringBuilder result = sectorCostTextBuilder;
        result.Clear();
        for (int i = 0; i < costs.Count; i++)
        {
            if (i > 0)
                result.Append("、");
            Resource resource = costs[i].First;
            result.Append(resource == null ? "未知资源" : resource.Label)
                .Append(" ")
                .Append((costs[i].Second * multiplier).ToGameString());
        }
        return result.ToString();
    }

    private string FormatResourceCosts(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs,
        ProgressionModifierState modifiers)
    {
        if (costs == null || costs.Count == 0)
            return "无";
        StringBuilder result = sectorCostTextBuilder;
        result.Clear();
        for (int i = 0; i < costs.Count; i++)
        {
            if (i > 0)
                result.Append("、");
            Resource resource = costs[i].First;
            ExpantaNum multiplier = modifiers == null
                ? ExpantaNum.One
                : modifiers.OccupiedResourceProductionMultiplier *
                  modifiers.GetOccupiedResourceProductionMultiplier(resource);
            result.Append(resource == null ? "未知资源" : resource.Label)
                .Append(" ")
                .Append((costs[i].Second * multiplier).ToGameString());
        }
        return result.ToString();
    }
}
