using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Page presenters. Fixed row hierarchy and geometry are authored in Prefabs;
/// this partial keeps only data filtering and gameplay callbacks.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private static string ResearchStateLabel(ResearchDef research, ResearchStatus status)
    {
        switch (status)
        {
            case ResearchStatus.Completed: return "已完成";
            case ResearchStatus.Researching: return "研究中";
            case ResearchStatus.Queued: return "已排队";
            case ResearchStatus.WaitingResources: return "等待资源";
        }
        if (research != null && research.AdvancesTechLevel)
            return "时代进步";
        if (research != null && GameManager.Instance != null &&
            research.TechLevel > GameManager.Instance.State.TechLevel)
            return "未解锁";
        if (status == ResearchStatus.Locked)
            return "需前置研究";
        return "可研究";
    }

    private bool IsResourceVisible(ResourceDef resource)
    {
        if (ResourceManager.Instance == null)
            return false;
        if (!ResourceManager.Instance.States.TryGetValue(resource, out ResourceState state))
            return false;
        return state.Amount > ExpantaNum.Zero || state.ProductionRate > ExpantaNum.Zero || state.ConsumptionRate > ExpantaNum.Zero;
    }

    private readonly Dictionary<Button, UITouchTooltip> buildingActionTooltips = new();
    private readonly Dictionary<Button, UIBuildingActionIcon> buildingActionIcons = new();
    private readonly Dictionary<Button, bool> buildingActionIconUpgradeModes = new();

    private void SetBuildingActionQuantity(Button button, ExpantaNum amount, string action)
    {
        string quantity = "×" + amount.ToGameString();
        SetBuildingActionButtonText(button, quantity);
        if (!buildingActionTooltips.TryGetValue(button, out UITouchTooltip tooltip))
        {
            tooltip = button.GetComponent<UITouchTooltip>();
            buildingActionTooltips[button] = tooltip;
        }
        if (tooltip != null) tooltip.SetTooltip(action + " " + quantity);
        if (!buildingActionIcons.TryGetValue(button, out UIBuildingActionIcon icon))
        {
            icon = button.GetComponent<UIBuildingActionIcon>();
            buildingActionIcons[button] = icon;
        }
        if (icon != null) icon.SetUpgrade(buildingActionIconUpgradeModes.TryGetValue(button, out bool upgrade) && upgrade);
    }

    private void SetBuildingActionButtonText(Button button, string value)
    {
        TMP_Text buttonText = GetBuildingActionButtonText(button);
        if (buttonText == null)
        {
            Debug.LogError("[王国界面] Authored building action button has no Text child: " + button.name);
            return;
        }
        if (buttonText.text != value)
            buttonText.text = value;
        buttonText.enabled = true;
        buttonText.gameObject.SetActive(true);
        buttonText.color = TextPrimary;
        buttonText.raycastTarget = false;
    }

    private TMP_Text GetBuildingActionButtonText(Button button)
    {
        if (!buildingActionButtonTexts.TryGetValue(button, out TMP_Text text) || text == null)
        {
            text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                buildingActionButtonTexts[button] = text;
        }
        return text;
    }

    private void SetBuildingActionButtonState(Button button, bool available)
    {
        if (button.interactable != available)
            button.interactable = available;
        bool isDeconstruct = button.name == "DeconstructButton";
        Image surface = button.targetGraphic as Image;
        if (surface != null)
            SetColorIfChanged(surface, available
                ? (isDeconstruct ? Error : BasicMetalIngot)
                : Panel);
        button.transition = Selectable.Transition.ColorTint;
        ApplyButtonColors(button);
        if (!buildingActionButtonOutlines.TryGetValue(button, out Outline outline) || outline == null)
        {
            outline = button.GetComponent<Outline>();
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();
            buildingActionButtonOutlines[button] = outline;
        }
        outline.enabled = true;
        outline.useGraphicAlpha = false;
        outline.effectDistance = new Vector2(1.5f, 1.5f);
        Color activeBorder = isDeconstruct
            ? new Color(1f, .62f, .55f, 1f)
            : BuildableActionColor;
        outline.effectColor = available ? activeBorder : new Color(.72f, .76f, .74f, .95f);
        TMP_Text text = GetBuildingActionButtonText(button);
        if (text == null)
        {
            Debug.LogError("[王国界面] Authored building action button has no Text child: " + button.name);
            return;
        }
        text.color = available ? TextPrimary : TextSecondary;
    }

    private bool CanPerformBuildingAction(BuildingDef building, bool upgrade, ExpantaNum amount)
    {
        if (BuildingManager.Instance == null)
            return false;
        if (amount < ExpantaNum.One)
            return false;
        ExpantaNum maximum = upgrade
            ? BuildingManager.Instance.GetMaxUpgradeable(building, amount)
            : BuildingManager.Instance.GetMaxBuildable(building, amount);
        return maximum >= amount;
    }

    private void PerformBuildingAction(BuildingDef building, bool upgrade)
    {
        if (BuildingManager.Instance == null)
        {
            ShowDetails("建筑", "建筑数据尚未载入，请稍候。");
            return;
        }
        ExpantaNum amount = GetSelectedBuildingQuantity(building, upgrade, false);
        BuildingDef upgradeTarget = null;
        if (upgrade)
            BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out upgradeTarget);
        BuildFailure failure;
        bool success = upgrade
            ? BuildingManager.Instance.TryUpgrade(building, amount, out failure)
            : BuildingManager.Instance.TryBuild(building, amount, out failure);
        if (success)
            UIButtonSoundManager.Play(upgrade
                ? UIButtonSoundManager.Sound.Upgrade
                : UIButtonSoundManager.Sound.Build);
        else
            ShowTooltip((upgrade ? "升级失败：" : "建造失败：") + GetBuildFailureDescription(failure));
        if (success && upgradeTarget != null &&
            (!BuildingManager.Instance.States.TryGetValue(building, out BuildingState sourceState) || sourceState.Amount <= ExpantaNum.Zero))
            building = upgradeTarget;
        ShowBuildingDetails(building, true);
    }

    private void DeconstructBuilding(BuildingDef building)
    {
        if (BuildingManager.Instance == null)
        {
            ShowDetails("建筑", "建筑数据尚未载入，请稍候。");
            return;
        }
        ExpantaNum amount = GetSelectedBuildingQuantity(building, false, true);
        BuildingDeconstructionPreview preview = BuildingManager.Instance.GetDeconstructionPreview(building, amount);
        if (preview == null) { ShowTooltip("当前没有可拆除建筑"); return; }
        if (amount > ExpantaNum.One || buildingQuantityMode == BuildingQuantityMode.Max ||
            preview.FoodOverflow > ExpantaNum.Zero)
        {
            var warning = new System.Text.StringBuilder("拆除 ").Append(building.Label).Append(" × ").Append(preview.Amount.ToGameString());
            if (preview.PopulationCapacityRemoved > ExpantaNum.Zero)
                warning.Append("\n住房容量减少：").Append(preview.PopulationCapacityRemoved.ToGameString())
                    .Append("（现有居民不会立即减少；增长受新容量限制）");
            if (preview.FoodCapacityRemoved > ExpantaNum.Zero)
                warning.Append("\n粮仓容量减少：").Append(preview.FoodCapacityRemoved.ToGameString());
            if (preview.FoodOverflow > ExpantaNum.Zero)
                warning.Append("\n超容量食物损失：").Append(preview.FoodOverflow.ToGameString());
            if (preview.ProductivityReleased != ExpantaNum.Zero)
                warning.Append("\n可用生产力变化：").Append(preview.ProductivityReleased > ExpantaNum.Zero ? "+" : string.Empty)
                    .Append(preview.ProductivityReleased.ToGameString());
            ExpantaNum scale = preview.Amount;
            ExpantaNum foodProduction = DisplayedBuildingFood(building) * scale;
            ExpantaNum foodConsumption = building.FoodConsumptionRate * DisplayedBuildingOperatingEfficiency(building) * scale;
            var outputs = GetEffectiveBuildingFlows(building, true);
            var inputs = GetEffectiveBuildingFlows(building, false);
            if (building.PowerContribution != ExpantaNum.Zero || building.LogisticsContribution != ExpantaNum.Zero ||
                foodProduction > ExpantaNum.Zero || foodConsumption > ExpantaNum.Zero || outputs.Count > 0 || inputs.Count > 0)
                warning.Append("\n所拆建筑当前数据（拆除后总量可能不同）：");
            if (building.PowerContribution != ExpantaNum.Zero)
                warning.Append("\n电力变化：").Append(building.PowerContribution < ExpantaNum.Zero ? "+" : string.Empty).Append((-DisplayedBuildingPower(building) * scale).ToGameString());
            if (building.LogisticsContribution != ExpantaNum.Zero)
                warning.Append("\n物流变化：").Append(building.LogisticsContribution < ExpantaNum.Zero ? "+" : string.Empty).Append((-DisplayedBuildingLogistics(building) * scale).ToGameString());
            if (foodProduction > ExpantaNum.Zero)
                warning.Append("\n当前粮食产出：").Append(foodProduction.ToGameString()).Append("/s");
            if (foodConsumption > ExpantaNum.Zero)
                warning.Append("\n当前建筑耗粮：").Append(foodConsumption.ToGameString()).Append("/s（居民需粮不变）");
            foreach (var flow in outputs) warning.Append("\n当前产出：").Append(flow.First.Label).Append(' ').Append((flow.Second * scale).ToGameString()).Append("/s");
            foreach (var flow in inputs) warning.Append("\n当前持续投入：").Append(flow.First.Label).Append(' ').Append((flow.Second * scale).ToGameString()).Append("/s");
            foreach (var refund in preview.Refunds) warning.Append("\n返还：").Append(refund.Key.Label).Append(' ').Append(refund.Value.ToGameString());
            ConfirmReviewAction(warning.ToString(), () => ExecuteBuildingDeconstruction(building, preview.Amount));
            return;
        }
        ExecuteBuildingDeconstruction(building, amount);
    }

    private void ExecuteBuildingDeconstruction(BuildingDef building, ExpantaNum amount)
    {
        bool success = BuildingManager.Instance.TryDeconstruct(building, amount, out BuildFailure failure);
        if (success)
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Deconstruct);
        else
            ShowTooltip("拆除失败：" + GetBuildFailureDescription(failure));
        ShowBuildingDetails(building, true);
    }

    private static string GetBuildFailureDescription(BuildFailure failure)
    {
        switch (failure)
        {
            case BuildFailure.InvalidAmount: return "数量无效";
            case BuildFailure.TechnologyInsufficient: return "技术等级不足";
            case BuildFailure.ResearchPrerequisiteIncomplete: return "研究前置未完成";
            case BuildFailure.WorkshopPrerequisiteIncomplete: return "工坊前置未完成";
            case BuildFailure.ResourceInsufficient: return "资源不足";
            case BuildFailure.SpaceInsufficient: return "空闲土地不足；拆除闲置建筑可腾出土地";
            case BuildFailure.DeconstructionUnavailable: return "该建筑不可拆除";
            case BuildFailure.BuildingTierSuperseded: return "该设施已转为后续建筑；请选取当前建筑";
            case BuildFailure.UpgradeUnavailable: return "升级不可用";
            case BuildFailure.SectorNotOccupied: return "所属星区尚未占领";
            case BuildFailure.InteriorExplorationIncomplete: return "需完成所属星球的深入探索";
            case BuildFailure.BuildingLimitReached: return "已达到建筑上限";
            default: return "前置条件或库存不足";
        }
    }
}
