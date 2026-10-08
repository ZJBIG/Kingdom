using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private RectTransform relicActions;
    private TMP_Text relicStatusText;
    private TMP_Text relicCostsText;
    private TMP_Text relicResultText;
    private TMP_Text relicConfirmationText;
    private GameObject relicConfirmation;
    private Button relicInvestigateButton;
    private Button relicRepairButton;
    private Button relicDismantleButton;
    private Button relicPrepareButton;
    private Button relicAssignButton;
    private Button relicSuspendButton;
    private Button relicResumeButton;
    private RelicRoute pendingRelicRoute;
    private SectorDefinition relicSelectedSector;
    private int lastRelicDetailSignature = -1;

    private RectTransform RelicActionsRectForLayout =>
        relicActions != null && relicActions.gameObject.activeSelf ? relicActions : null;

    private float RelicActionsPreferredHeight => RelicActionsRectForLayout == null
        ? 0 : LayoutUtility.GetPreferredHeight(relicActions);

#if UNITY_EDITOR
    public RectTransform RelicActionsForEditor => relicActions;
    public bool RelicConfirmationPendingForEditor => pendingRelicRoute != RelicRoute.None;
    public void ShowRelicDetailsForEditor()
    {
        if (DataBase<SectorDefinition>.TryFind("TauCetiFoundry", out SectorDefinition sector) &&
            GameManager.TryGetInstance(out GameManager game))
            ShowSectorDetails(sector, game.Sectors, game.State, ResourceManager.Instance);
    }
#endif

    private bool BindRelicActions()
    {
        if (relicActions != null)
            return true;
        if (detailScrollContent == null)
            return false;
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRelicActions");
        if (prefab == null)
        {
            Debug.LogError("[王国界面] Missing authored relic actions prefab.");
            return false;
        }
        relicActions = Instantiate(prefab, detailScrollContent, false).transform as RectTransform;
        relicStatusText = relicActions.Find("Status")?.GetComponent<TMP_Text>();
        relicCostsText = relicActions.Find("Costs")?.GetComponent<TMP_Text>();
        relicResultText = relicActions.Find("Result")?.GetComponent<TMP_Text>();
        relicConfirmation = relicActions.Find("Confirmation")?.gameObject;
        relicConfirmationText = relicActions.Find("Confirmation/Warning")?.GetComponent<TMP_Text>();
        relicInvestigateButton = RelicButton("Investigate");
        relicRepairButton = RelicButton("Repair");
        relicDismantleButton = RelicButton("Dismantle");
        relicPrepareButton = RelicButton("Prepare");
        relicAssignButton = RelicButton("Assign");
        relicSuspendButton = RelicButton("Suspend");
        relicResumeButton = RelicButton("Resume");
        Button confirm = RelicButton("Confirmation/Confirm");
        Button cancel = RelicButton("Confirmation/Cancel");
        if (relicStatusText == null || relicCostsText == null || relicResultText == null ||
            relicConfirmation == null || relicConfirmationText == null ||
            relicInvestigateButton == null || relicRepairButton == null ||
            relicDismantleButton == null || relicPrepareButton == null ||
            relicAssignButton == null || relicSuspendButton == null ||
            relicResumeButton == null || confirm == null || cancel == null)
        {
            Debug.LogError("[王国界面] Authored relic actions prefab is incomplete.");
            relicActions.gameObject.SetActive(false);
            return false;
        }
        relicInvestigateButton.onClick.AddListener(() => ExecuteRelicCommand(RelicUiCommand.Investigate));
        relicRepairButton.onClick.AddListener(() => PreviewRelicRoute(RelicRoute.Repair));
        relicDismantleButton.onClick.AddListener(() => PreviewRelicRoute(RelicRoute.Dismantle));
        relicPrepareButton.onClick.AddListener(() => ExecuteRelicCommand(RelicUiCommand.Prepare));
        relicAssignButton.onClick.AddListener(() => ExecuteRelicCommand(RelicUiCommand.Assign));
        relicSuspendButton.onClick.AddListener(() => ExecuteRelicCommand(RelicUiCommand.Suspend));
        relicResumeButton.onClick.AddListener(() => ExecuteRelicCommand(RelicUiCommand.Resume));
        confirm.onClick.AddListener(() => ExecuteRelicCommand(RelicUiCommand.ConfirmRoute));
        cancel.onClick.AddListener(CancelRelicRoute);
        return true;
    }

    private Button RelicButton(string path) => relicActions.Find(path)?.GetComponent<Button>();

    private void ConfigureRelicDetails(SectorDefinition sector)
    {
        if (sector == null || sector.Id != "TauCetiFoundry" ||
            !GameManager.TryGetInstance(out GameManager game) || game.Relic.Definition == null)
        {
            HideRelicDetails();
            return;
        }
        if (!BindRelicActions())
            return;
        int signature = GetRelicDetailSignature(game.Relic);
        if (relicSelectedSector == sector && relicActions.gameObject.activeSelf &&
            signature == lastRelicDetailSignature)
            return;
        if (relicSelectedSector != sector)
        {
            pendingRelicRoute = RelicRoute.None;
            relicResultText.text = string.Empty;
        }
        relicSelectedSector = sector;
        RelicManager manager = game.Relic;
        RelicState state = manager.State;
        relicActions.gameObject.SetActive(true);
        if (state.Status != RelicStatus.AwaitingChoice || state.Suspended)
            pendingRelicRoute = RelicRoute.None;
        relicConfirmation.SetActive(pendingRelicRoute != RelicRoute.None);

        StringBuilder text = new();
        text.AppendLine(manager.Definition.Description);
        text.AppendLine("准入：Ultra · 占领 " + sector.Label + " · 相位稳定认证");
        text.Append("前置研究：");
        for (int i = 0; i < manager.Definition.RequiredResearch.Count; i++)
        {
            Research research = manager.Definition.RequiredResearch[i];
            if (research != null)
                text.Append(i == 0 ? string.Empty : "、").Append(research.Label);
        }
        text.AppendLine();
        text.Append("所需建筑：");
        for (int i = 0; i < manager.Definition.RequiredBuildings.Count; i++)
        {
            Building building = manager.Definition.RequiredBuildings[i];
            if (building != null)
                text.Append(i == 0 ? string.Empty : "、").Append(building.Label).Append(" ×1");
        }
        text.AppendLine();
        text.AppendLine("状态：" + GetRelicStatusLabel(state));
        text.AppendLine("路线：" + GetRelicRouteLabel(state.Route));
        text.AppendLine("阶段进度：" + (state.Progress * new ExpantaNum(100)).ToGameString() + "%");
        if (state.Suspended)
            text.AppendLine(state.PauseReason == RelicPauseReason.InsufficientSupply
                ? "暂停原因：当前供给不足；补足供给后恢复。暂停期间不消耗，调查与已付款进度保留。"
                : "已手动封存：保留信息、路线与进度；恢复后继续。");
        if (state.SupportReady)
            text.AppendLine("支援：已制备，等待为当前活动的远星战役启用。");
        else if (!string.IsNullOrEmpty(state.SupportedSectorId))
            text.AppendLine("支援：服役于 " + GetRelicSectorLabel(state.SupportedSectorId) + "；结束或撤退后消耗，不返还。");
        else
            text.AppendLine(state.CommissionActive ? "支援：维护委托制备中。" : "支援：尚未制备。");
        RelicOperationFailure failure = manager.GetAvailabilityFailure();
        if (failure != RelicOperationFailure.None)
            text.AppendLine("当前阻碍：" + GetRelicFailureLabel(failure));
        relicStatusText.text = text.ToString();

        text.Clear();
        bool startupPaid = state.Status == RelicStatus.Investigating || state.Status == RelicStatus.Repairing ||
            state.Status == RelicStatus.ReverseEngineering || state.CommissionActive;
        AppendRelicWorkCosts(text, "当前阶段", manager.GetCurrentWork(), startupPaid);
        if (state.Status <= RelicStatus.AwaitingChoice)
        {
            AppendRelicWorkCosts(text, "修复认证", manager.Definition.Repair);
            AppendRelicWorkCosts(text, "拆解认证", manager.Definition.ReverseEngineering);
        }
        if (state.Route == RelicRoute.Dismantle || state.Route == RelicRoute.None)
        {
            text.AppendLine("工坊制造单份支援（即时付款）：");
            AppendRelicCosts(text, manager.Definition.SupportCraftCosts, false);
        }
        if (state.Route == RelicRoute.None ||
            (state.Route == RelicRoute.Repair && state.Status != RelicStatus.Operational))
            AppendRelicWorkCosts(text, "修复路线的单份支援维护委托", manager.Definition.Commission);
        relicCostsText.text = text.ToString();

        bool choosing = state.Status == RelicStatus.AwaitingChoice && !state.Suspended;
        bool available = failure == RelicOperationFailure.None;
        SetRelicButton(relicInvestigateButton, state.Status == RelicStatus.Discovered && !state.Suspended, available);
        SetRelicButton(relicRepairButton, choosing && pendingRelicRoute == RelicRoute.None, available);
        SetRelicButton(relicDismantleButton, choosing && pendingRelicRoute == RelicRoute.None, available);
        bool preparing = state.Status == RelicStatus.Operational && !state.Suspended &&
            !state.SupportReady && !state.CommissionActive && string.IsNullOrEmpty(state.SupportedSectorId);
        SetRelicButton(relicPrepareButton, preparing, available);
        if (preparing)
            relicPrepareButton.GetComponentInChildren<TMP_Text>(true).text = state.Route == RelicRoute.Repair
                ? "开始维护委托，制备支援" : "工坊付费制造支援";
        SetRelicButton(relicAssignButton, state.SupportReady && !state.Suspended, available);
        SetRelicButton(relicSuspendButton, !state.Suspended);
        SetRelicButton(relicResumeButton, state.Suspended, available);
        lastRelicDetailSignature = GetRelicDetailSignature(manager);
        LayoutRebuilder.ForceRebuildLayoutImmediate(relicActions);
        LayoutResourceDetailsBody();
    }

    private void HideRelicDetails()
    {
        if (relicActions != null)
            relicActions.gameObject.SetActive(false);
        pendingRelicRoute = RelicRoute.None;
        relicSelectedSector = null;
        lastRelicDetailSignature = -1;
    }

    private static int GetRelicDetailSignature(RelicManager manager)
    {
        if (manager == null || manager.Definition == null)
            return -1;
        int signature = unchecked(manager.State.Version * 31 + (int)manager.GetAvailabilityFailure());
        if (GameManager.TryGetInstance(out GameManager game))
            signature = unchecked(signature * 31 + game.State.Version);
        if (ResourceManager.TryGetInstance(out ResourceManager resources))
            foreach (ResourceState state in resources.States.Values)
                signature = unchecked(signature * 31 + state.Version);
        return signature;
    }

    private void PreviewRelicRoute(RelicRoute route)
    {
        if (!GameManager.TryGetInstance(out GameManager game) ||
            game.Relic.State.Status != RelicStatus.AwaitingChoice || game.Relic.State.Suspended)
            return;
        pendingRelicRoute = route;
        relicConfirmationText.text = route == RelicRoute.Repair
            ? "永久选择修复：保留古代铸造环，放弃自主拆解路线；认证后每份支援都需要持续维护委托。确认才支付修复认证启动材料。"
            : "永久选择拆解：放弃古代装置与修复路线；自主认证后在工坊即时付费制造支援。确认才支付拆解认证启动材料。";
        RefreshRelicDetailsAfterCommand();
    }

    private void CancelRelicRoute()
    {
        pendingRelicRoute = RelicRoute.None;
        RefreshRelicDetailsAfterCommand();
    }

    private enum RelicUiCommand { Investigate, ConfirmRoute, Suspend, Resume, Prepare, Assign }

    private void ExecuteRelicCommand(RelicUiCommand command)
    {
        if (!GameManager.TryGetInstance(out GameManager game))
            return;
        RelicManager manager = game.Relic;
        RelicOperationFailure failure = RelicOperationFailure.InvalidState;
        bool success = false;
        switch (command)
        {
            case RelicUiCommand.Investigate: success = manager.TryInvestigate(out failure); break;
            case RelicUiCommand.ConfirmRoute:
                if (pendingRelicRoute != RelicRoute.None)
                    success = manager.TryChooseRoute(pendingRelicRoute, out failure);
                if (success) pendingRelicRoute = RelicRoute.None;
                break;
            case RelicUiCommand.Suspend: success = manager.TrySuspend(out failure); break;
            case RelicUiCommand.Resume: success = manager.TryResume(out failure); break;
            case RelicUiCommand.Prepare:
                success = manager.State.Route == RelicRoute.Repair
                    ? manager.TryBeginCommission(out failure) : WorkshopManager.Instance.TryCraftRelicSupport(out failure);
                break;
            case RelicUiCommand.Assign: success = manager.TryAssignSupport(out failure); break;
        }
        relicResultText.text = success ? "操作已完成。" : "未执行：" + GetRelicFailureLabel(failure);
        if (success)
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
        RefreshRelicDetailsAfterCommand();
    }

    private void RefreshRelicDetailsAfterCommand()
    {
        if (relicSelectedSector == null || !GameManager.TryGetInstance(out GameManager game))
            return;
        lastRelicDetailSignature = -1;
        ConfigureRelicDetails(relicSelectedSector);
        RefreshSelectedSectorDetails(game.Sectors, game.State, ResourceManager.Instance);
    }

    private static void SetRelicButton(Button button, bool visible, bool available = true)
    {
        button.gameObject.SetActive(visible);
        button.interactable = visible && available;
    }

    private static void AppendRelicWorkCosts(StringBuilder text, string label, RelicWorkDefinition work,
        bool startupPaid = false)
    {
        if (work == null)
            return;
        text.AppendLine(label + "：" + work.DurationSeconds.ToGameString() + " 秒");
        text.AppendLine(startupPaid ? "启动材料已支付（当前库存 / 原需求，恢复不重付）："
            : "启动材料（库存 / 需要）：");
        AppendRelicCosts(text, work.StartupCosts, false);
        text.AppendLine("持续材料：");
        AppendRelicCosts(text, work.ContinuousCosts, true);
        text.AppendLine("持续供给：食物 -" + work.FoodConsumptionRate.ToGameString() +
            "/s，电力 -" + work.PowerConsumptionRate.ToGameString() +
            "/s，物流 -" + work.LogisticsConsumptionRate.ToGameString() + "/s");
    }

    private static void AppendRelicCosts(StringBuilder text,
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs, bool rate)
    {
        if (costs == null || costs.Count == 0)
        {
            text.AppendLine("  无额外材料");
            return;
        }
        ResourceManager.TryGetInstance(out ResourceManager resources);
        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            if (cost.First == null)
                continue;
            ExpantaNum owned = resources == null ? ExpantaNum.Zero : resources.GetAmount(cost.First);
            text.AppendLine("  " + cost.First.Label + "：" + (rate
                ? "-" + cost.Second.ToGameString() + "/s（库存 " + owned.ToGameString() + "）"
                : owned.ToGameString() + " / " + cost.Second.ToGameString()));
        }
    }

    private static string GetRelicSectorLabel(string id)
    {
        IReadOnlyList<SectorDefinition> sectors = DataBase<SectorDefinition>.All;
        for (int i = 0; i < sectors.Count; i++)
            if (sectors[i] != null && sectors[i].Id == id)
                return sectors[i].Label;
        return id;
    }

    private static string GetRelicRouteLabel(RelicRoute route) => route == RelicRoute.Repair
        ? "修复（永久）" : route == RelicRoute.Dismantle ? "拆解（永久）" : "尚未选择";

    private static string GetRelicStatusLabel(RelicState state)
    {
        switch (state.Status)
        {
            case RelicStatus.Investigating: return "调查中";
            case RelicStatus.AwaitingChoice: return "调查完成，等待永久路线选择";
            case RelicStatus.Repairing: return "修复认证中";
            case RelicStatus.ReverseEngineering: return "自主制造认证中";
            case RelicStatus.Operational: return state.CommissionActive ? "维护委托进行中" : "认证完成";
            default: return "信号已发现，等待调查";
        }
    }

    private static string GetRelicFailureLabel(RelicOperationFailure failure)
    {
        switch (failure)
        {
            case RelicOperationFailure.None: return "无";
            case RelicOperationFailure.DefinitionMissing: return "遗迹定义尚未加载";
            case RelicOperationFailure.NotUltra: return "尚未进入 Ultra 时代";
            case RelicOperationFailure.SectorNotOccupied: return "尚未占领鲸鱼座工业前哨";
            case RelicOperationFailure.PrerequisiteResearchMissing: return "前置研究尚未完成";
            case RelicOperationFailure.RequiredBuildingMissing: return "所需建筑尚未建成";
            case RelicOperationFailure.CertificationMissing: return "所需文明工程认证尚未完成";
            case RelicOperationFailure.InsufficientStartupResources: return "启动或制造材料不足";
            case RelicOperationFailure.InsufficientSupply: return "食物、电力、物流或持续材料供给不足";
            case RelicOperationFailure.SupportAlreadyPrepared: return "已有待用或服役支援";
            case RelicOperationFailure.CampaignMissing: return "没有可启用支援的活动远星战役";
            case RelicOperationFailure.InvalidRoute: return "永久路线不允许此操作";
            default: return "当前状态不允许此操作";
        }
    }
}
