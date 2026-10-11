using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ultra project controls are intentionally hosted by the existing authored
/// building detail panel. This keeps the late-game flow one tap away without
/// adding another top-level scene or a runtime-authored fixed layout.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private int lastUltraProjectStateVersion = -1;
    private int lastUltraProjectInputSignature = -1;
    private int ultraDetailRequirementCount;
    private int ultraDetailFlowCount;
    private Button detailDoctrineButton;

#if UNITY_EDITOR
    public Button DetailDoctrineButtonForEditor => detailDoctrineButton;
#endif

    private bool IsUltraProjectAccessBuilding(Building building)
    {
        if (building == null)
            return false;
        return building.Id == "PhaseEnergyArray" ||
            building.Id == "AutonomousMatterFabricator";
    }

    private void ConfigureUltraProjectDetails(Building building)
    {
        if (!IsUltraProjectAccessBuilding(building) ||
            !GameManager.TryGetInstance(out GameManager gameManager) ||
            gameManager.UltraProject == null)
            return;

        UltraProjectManager manager = gameManager.UltraProject;
        UltraProjectPreview preview = manager.GetPreview();
        StringBuilder text = new();
        text.AppendLine();
        text.AppendLine("文明工程核心");
        text.AppendLine("阶段: " + GetUltraStageLabel(preview.Stage));
        text.AppendLine("状态: " + GetUltraStatusLabel(preview.Status));
        text.AppendLine("进度: " + (preview.Progress * new ExpantaNum(100)).ToGameString() + "%");
        text.AppendLine("工程姿态: " + GetUltraDoctrineLabel(preview.Doctrine));
        text.AppendLine("供给满足率: " + (preview.SupplySatisfaction * new ExpantaNum(100)).ToGameString() + "%");
        text.AppendLine("持续消耗: 食物 -" + preview.FoodPerSecond.ToGameString() + "/s，电力 -" +
            preview.PowerPerSecond.ToGameString() + "/s，物流 -" +
            preview.LogisticsPerSecond.ToGameString() + "/s");
        AppendUltraMaterialStatus(text, preview);
        AppendUltraBudget(text, preview);
        if (preview.Status != UltraProjectStatus.Committed)
        {
            text.AppendLine("同一当前状态下的工程姿态比较（不会切换姿态或支付）：");
            foreach (UltraProjectDoctrine doctrine in new[] { UltraProjectDoctrine.Stable, UltraProjectDoctrine.Surge })
            {
                UltraProjectPreview option = manager.GetPreview(doctrine);
                text.AppendLine("工程姿态 " + GetUltraDoctrineLabel(doctrine) +
                    (option.Failure == UltraProjectOperationFailure.InvalidDoctrine ? "（当前阶段未开放）" : string.Empty));
                AppendUltraBudget(text, option);
            }
        }
        UltraProjectOperationFailure displayFailure = preview.Failure;
        bool showFailure = preview.Status == UltraProjectStatus.Locked ||
            preview.Status == UltraProjectStatus.Ready;
        if (showFailure && displayFailure != UltraProjectOperationFailure.None)
        {
            text.AppendLine("阻碍: " + GetUltraFailureLabel(displayFailure));
            text.AppendLine("下一步: 先排除阻碍，再启动本阶段");
        }
        else if (preview.Status == UltraProjectStatus.ReadyToCommit)
            text.AppendLine("下一步: 提交阶段认证，解锁新的文明能力");
        else if (preview.Status == UltraProjectStatus.Running)
            text.AppendLine("运行中: 资源不足会自动暂停，不会产生负库存");
        else if (preview.Status == UltraProjectStatus.Paused)
        {
            bool supplyRecovered =
                preview.Failure == UltraProjectOperationFailure.InsufficientSupply &&
                preview.SupplySatisfaction > ExpantaNum.Zero;
            if (supplyRecovered)
                text.AppendLine("暂停原因: 之前供给不足；当前可按满足率渐进恢复");
            else if (preview.Failure != UltraProjectOperationFailure.None)
                text.AppendLine("暂停原因: " + GetUltraFailureLabel(preview.Failure));
            else
                text.AppendLine("暂停原因: 手动暂停");
            text.AppendLine(supplyRecovered
                ? "当前供给已恢复；可在此恢复工程"
                : "暂停期间不消耗资源；确认工程条件后可恢复");
        }
        else if (preview.Status == UltraProjectStatus.Committed)
            text.AppendLine(!HasRemainingInterstellarTargets() ? "现有工程认证与远星据点已完成，可在文明记忆中回顾成果。" : preview.Doctrine == UltraProjectDoctrine.Expedition
                ? "文明工程已完成：当前为远征供给姿态"
                : "文明工程已完成：可切换远征供给姿态");
        else
            text.AppendLine("下一步: 检查研究、建筑与启动材料，然后开始工程");

        detailBody.text += "\n" + text;
        float preservedScrollPosition = requirementGesture == null
            ? 1f
            : requirementGesture.GetNormalizedPosition();
        PlaceRequirementsAfterDescription(
            ultraDetailRequirementCount,
            preservedScrollPosition,
            ultraDetailFlowCount);
        ConfigureUltraProjectAction(manager, preview);
        ConfigureUltraDoctrineAction(manager, preview);
        lastUltraProjectStateVersion = manager.State.Version;
        lastUltraProjectInputSignature = GetUltraProjectInputSignature(manager);
    }

    private void AppendUltraMaterialStatus(
        StringBuilder text,
        UltraProjectPreview preview)
    {
        if (preview == null || !ResourceManager.TryGetInstance(out ResourceManager resourceManager))
            return;

        if (preview.StartupCosts.Count > 0)
        {
            text.AppendLine("启动材料（库存 / 需要）:");
            AppendUltraCostRows(text, resourceManager, preview.StartupCosts, false);
        }
        if (preview.ContinuousCosts.Count > 0)
        {
            text.AppendLine("高级材料持续消耗:");
            AppendUltraCostRows(text, resourceManager, preview.ContinuousCosts, true);
        }
    }

    private static void AppendUltraCostRows(
        StringBuilder text,
        ResourceManager resourceManager,
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs,
        bool rate)
    {
        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            if (cost.First == null || cost.Second <= ExpantaNum.Zero)
                continue;
            ExpantaNum amount = resourceManager.GetAmount(cost.First);
            text.AppendLine("  " + cost.First.Label + ": " +
                (rate
                    ? "-" + cost.Second.ToGameString() + "/s（库存 " + amount.ToGameString() + "）"
                    : amount.ToGameString() + " / " + cost.Second.ToGameString() +
                        (amount < cost.Second
                            ? "（缺 " + (cost.Second - amount).ToGameString() + "）"
                            : "（充足）")));
        }
    }

    private int GetUltraProjectInputSignature(UltraProjectManager manager)
    {
        if (manager == null || !GameManager.TryGetInstance(out GameManager gameManager))
            return -1;
        int signature = gameManager.State.Version;
        UltraProjectPreview preview = manager.GetPreview();
        for (int i = 0; i < preview.ContinuousCosts.Count; i++)
        {
            Resource resource = preview.ContinuousCosts[i].First;
            if (resource != null && ResourceManager.TryGetInstance(out ResourceManager resourceManager) &&
                resourceManager.States.TryGetValue(
                    resource, out ResourceState resourceState))
                signature = unchecked(signature * 31 + resourceState.Version);
        }
        return signature;
    }

    private void ConfigureUltraDoctrineAction(
        UltraProjectManager manager,
        UltraProjectPreview preview)
    {
        if (detailDoctrineButton == null)
            return;
        detailDoctrineButton.gameObject.SetActive(true);
        detailDoctrineButton.onClick.RemoveAllListeners();
        detailDoctrineButton.interactable = false;
        UltraProjectDoctrine currentDoctrine = preview.Doctrine == UltraProjectDoctrine.None
            ? UltraProjectDoctrine.Stable : preview.Doctrine;
        string label = "工程姿态：" + GetUltraDoctrineLabel(currentDoctrine);
        if (preview.Status == UltraProjectStatus.Committed)
        {
            UltraProjectDoctrine nextDoctrine = currentDoctrine == UltraProjectDoctrine.Expedition
                ? UltraProjectDoctrine.Stable
                : UltraProjectDoctrine.Expedition;
            detailDoctrineButton.interactable = manager.IsCampaignDoctrineUnlocked && HasRemainingInterstellarTargets();
            label += !HasRemainingInterstellarTargets()
                ? " · 现有远星目标已完成"
                : !detailDoctrineButton.interactable
                ? " · 连续性认证尚未完成"
                : nextDoctrine == UltraProjectDoctrine.Expedition
                    ? " · 切换远征供给"
                    : " · 切换稳定";
            if (detailDoctrineButton.interactable)
            {
                detailDoctrineButton.onClick.AddListener(() =>
                {
                    manager.TrySetOperationalDoctrine(nextDoctrine, out _);
                    ShowBuildingDetails(selectedBuilding, true);
                });
            }
        }
        else if (preview.Status == UltraProjectStatus.Ready ||
                 preview.Status == UltraProjectStatus.Paused)
        {
            UltraProjectDoctrine nextDoctrine = currentDoctrine == UltraProjectDoctrine.Surge
                ? UltraProjectDoctrine.Stable : UltraProjectDoctrine.Surge;
            detailDoctrineButton.interactable = preview.Stage >= UltraProjectStage.Stabilization;
            label += detailDoctrineButton.interactable
                ? (nextDoctrine == UltraProjectDoctrine.Surge ? " · 切换高压" : " · 切换稳定")
                : " · 第二阶段后开放高压";
            if (detailDoctrineButton.interactable)
            {
                detailDoctrineButton.onClick.AddListener(() =>
                {
                    manager.TrySetOperationalDoctrine(nextDoctrine, out _);
                    ShowBuildingDetails(selectedBuilding, true);
                });
            }
        }
        else if (preview.Status == UltraProjectStatus.Running)
            label += " · 工程运行中不可切换";
        else if (preview.Status == UltraProjectStatus.ReadyToCommit)
            label += " · 提交阶段后再切换";
        TMP_Text text = detailDoctrineButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;
        SetDoctrineActionVisible(true);
    }

    private void ConfigureCampaignDoctrineAction(GameState state)
    {
        if (detailDoctrineButton == null)
            return;

        GameManager gameManager;
        if (state == null || !GameManager.TryGetInstance(out gameManager) ||
            gameManager.UltraProject == null ||
            !gameManager.UltraProject.IsCampaignDoctrineUnlocked)
        {
            HideUltraDoctrineButton();
            return;
        }

        CampaignDoctrine nextDoctrine = state.Campaign.Doctrine == CampaignDoctrine.Surge
            ? CampaignDoctrine.Stable
            : CampaignDoctrine.Surge;
        detailDoctrineButton.gameObject.SetActive(true);
        detailDoctrineButton.onClick.RemoveAllListeners();
        detailDoctrineButton.interactable = HasRemainingInterstellarTargets();
        if (detailDoctrineButton.interactable)
            detailDoctrineButton.onClick.AddListener(() =>
            {
                gameManager.UltraProject.TrySetCampaignDoctrine(nextDoctrine, out _);
                if (selectedSectorDefinition != null)
                    ShowSectorDetails(
                        selectedSectorDefinition,
                        gameManager.Sectors,
                        gameManager.State,
                        ResourceManager.Instance);
            });

        TMP_Text text = detailDoctrineButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = !detailDoctrineButton.interactable
                ? "战役姿态 · 现有远星目标已完成"
                : state.Campaign.Doctrine == CampaignDoctrine.Surge
                    ? "战役姿态：突进 · 切换稳健"
                    : "战役姿态：稳健 · 切换突进";
        SetDoctrineActionVisible(true);
    }

    private void HideUltraDoctrineButton()
    {
        if (detailDoctrineButton != null)
        {
            detailDoctrineButton.onClick.RemoveAllListeners();
            detailDoctrineButton.gameObject.SetActive(false);
        }
        SetDoctrineActionVisible(false);
    }

    private void SetDoctrineActionVisible(bool visible)
    {
        if (detailDoctrineButton != null)
            detailDoctrineButton.gameObject.SetActive(visible);
    }

    private void ConfigureUltraProjectAction(
        UltraProjectManager manager,
        UltraProjectPreview preview)
    {
        if (detailActionButton == null)
            return;
        SetDoctrineActionVisible(false);
        detailActionButton.onClick.RemoveAllListeners();
        detailActionButton.gameObject.SetActive(true);
        detailActionButton.interactable = false;
        string label = "文明工程";
        if (preview.Status == UltraProjectStatus.Running)
        {
            label = "暂停文明工程";
            detailActionButton.interactable = true;
            detailActionButton.onClick.AddListener(() =>
            {
                if (manager.TryPause(out _)) UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicStop);
                ShowBuildingDetails(selectedBuilding, true);
            });
        }
        else if (preview.Status == UltraProjectStatus.Paused)
        {
            label = "恢复文明工程";
            detailActionButton.interactable = true;
            detailActionButton.onClick.AddListener(() =>
            {
                if (manager.TryResume(out _)) UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicStart);
                ShowBuildingDetails(selectedBuilding, true);
            });
        }
        else if (preview.Status == UltraProjectStatus.ReadyToCommit)
        {
            label = "提交阶段认证";
            detailActionButton.interactable = true;
            detailActionButton.onClick.AddListener(() =>
            {
                if (manager.TryCommitCompletedStage(out _)) UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicCommit);
                ShowBuildingDetails(selectedBuilding, true);
            });
        }
        else if (preview.Status == UltraProjectStatus.Ready ||
                 preview.Status == UltraProjectStatus.Locked)
        {
            label = "启动文明工程";
            detailActionButton.interactable = preview.CanStart;
            UltraProjectDoctrine startDoctrine = GetUltraProjectStartDoctrine(preview);
            if (detailActionButton.interactable)
            {
                detailActionButton.onClick.AddListener(() =>
                {
                    if (manager.TryStartStage(startDoctrine, out _)) UIButtonSoundManager.Play(UIButtonSoundManager.Sound.StrategicStart);
                    ShowBuildingDetails(selectedBuilding, true);
                });
            }
        }
        else if (preview.Status == UltraProjectStatus.Committed)
        {
            label = "回顾文明工程成果";
            detailActionButton.interactable = true;
            detailActionButton.onClick.AddListener(() => SetPage("Story"));
        }

        TMP_Text buttonText = detailActionButton.GetComponentInChildren<TMP_Text>(true);
        if (buttonText != null)
            buttonText.text = label;
    }

    private static UltraProjectDoctrine GetUltraProjectStartDoctrine(
        UltraProjectPreview preview)
    {
        return preview == null || preview.Doctrine == UltraProjectDoctrine.None
            ? UltraProjectDoctrine.Stable
            : preview.Doctrine;
    }

    private static string GetUltraStageLabel(UltraProjectStage stage)
    {
        switch (stage)
        {
            case UltraProjectStage.Prototype: return "相位稳定认证";
            case UltraProjectStage.Stabilization: return "物质自主认证";
            case UltraProjectStage.Expansion: return "文明连续性认证";
            case UltraProjectStage.Completed: return "全部认证完成";
            default: return "尚未启动";
        }
    }

    private static string GetUltraStatusLabel(UltraProjectStatus status)
    {
        switch (status)
        {
            case UltraProjectStatus.Ready: return "待启动";
            case UltraProjectStatus.Running: return "运行中";
            case UltraProjectStatus.Paused: return "已暂停";
            case UltraProjectStatus.ReadyToCommit: return "待提交";
            case UltraProjectStatus.Committed: return "已完成";
            default: return "锁定";
        }
    }

    private static string GetUltraDoctrineLabel(UltraProjectDoctrine doctrine)
    {
        switch (doctrine)
        {
            case UltraProjectDoctrine.Stable: return "稳定";
            case UltraProjectDoctrine.Surge: return "高压";
            case UltraProjectDoctrine.Expedition: return "远征供给";
            default: return "未选择";
        }
    }

    private static string GetUltraFailureLabel(UltraProjectOperationFailure failure)
    {
        switch (failure)
        {
            case UltraProjectOperationFailure.NotUltra: return "尚未进入 Ultra 时代";
            case UltraProjectOperationFailure.PrerequisiteResearchMissing: return "研究前置未完成";
            case UltraProjectOperationFailure.RequiredBuildingMissing: return "所需巨构尚未建成";
            case UltraProjectOperationFailure.InsufficientStartupResources: return "启动材料不足";
            case UltraProjectOperationFailure.InsufficientSupply: return "电力、物流、食物或高级材料供给不足";
            case UltraProjectOperationFailure.StageLocked: return "该运行姿态尚未认证";
            case UltraProjectOperationFailure.CampaignDoctrineLocked: return "连续性认证尚未完成";
            case UltraProjectOperationFailure.DefinitionMissing: return "工程定义未加载";
            default: return "当前状态不允许此操作";
        }
    }
}
