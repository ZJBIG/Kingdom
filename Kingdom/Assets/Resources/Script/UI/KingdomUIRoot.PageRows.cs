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
    private void AddBuildingRows(RectTransform parent) => BuildAuthoredBuildingRows(parent);

    private void AddWorkshopRows(RectTransform parent) => BuildAuthoredWorkshopRows(parent);

    private void AddResearchRows(RectTransform parent) => BuildAuthoredResearchRows(parent);

    private void AddResourceRows(RectTransform parent) => BuildAuthoredResourceRows(parent);

    private static string ResearchStateLabel(ResearchStatus status)
    {
        return status switch
        {
            ResearchStatus.Completed => "已完成",
            ResearchStatus.Researching => "研究中",
            ResearchStatus.Queued => "已排队",
            ResearchStatus.WaitingResources => "等待资源",
            ResearchStatus.Available => "可研究",
            _ => "未解锁"
        };
    }

    private bool IsResourceVisible(Resource resource)
    {
        if (ResourceManager.Instance == null)
            return false;
        if (!ResourceManager.Instance.States.TryGetValue(resource, out ResourceState state))
            return false;
        return state.Amount > ExpantaNum.Zero || state.ProductionRate > ExpantaNum.Zero || state.ConsumptionRate > ExpantaNum.Zero;
    }

    private static void SetBuildingActionButtonText(Button button, string value)
    {
        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonText == null)
        {
            Debug.LogError("[王国界面] Authored building action button has no Text child: " + button.name);
            return;
        }
        buttonText.text = value;
        buttonText.enabled = true;
        buttonText.gameObject.SetActive(true);
        buttonText.color = TextPrimary;
        buttonText.raycastTarget = false;
    }

    private static void SetBuildingActionButtonState(Button button, bool available)
    {
        button.interactable = available;
        Image surface = button.targetGraphic as Image;
        if (surface != null)
        {
            bool isDeconstruct = button.name == "DeconstructButton";
            surface.color = available
                ? (isDeconstruct ? Error : Copper)
                : Panel;
        }
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        // Keep the state color in the Image while using the ColorBlock only
        // as interaction feedback. This prevents the prefab's white default
        // from erasing the orange/red/disabled button palette.
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.92f, .92f, .92f, 1f);
        colors.pressedColor = new Color(.58f, .58f, .58f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(.68f, .70f, .69f, .92f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.useGraphicAlpha = false;
        outline.effectDistance = new Vector2(1.5f, 1.5f);
        bool deconstruct = button.name == "DeconstructButton";
        Color activeBorder = deconstruct
            ? new Color(1f, .62f, .55f, 1f)
            : new Color(1f, .82f, .48f, 1f);
        outline.effectColor = available ? activeBorder : new Color(.72f, .76f, .74f, .95f);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
        {
            Debug.LogError("[王国界面] Authored building action button has no Text child: " + button.name);
            return;
        }
        text.color = available ? TextPrimary : TextSecondary;
    }

    private bool CanPerformBuildingAction(Building building, bool upgrade)
    {
        if (BuildingManager.Instance == null)
            return false;
        ExpantaNum amount = GetSelectedBuildingQuantity(building, upgrade, false);
        if (amount < ExpantaNum.One)
            return false;
        ExpantaNum maximum = upgrade
            ? BuildingManager.Instance.GetMaxUpgradeable(building, amount)
            : BuildingManager.Instance.GetMaxBuildable(building, amount);
        return maximum >= amount;
    }

    private void PerformBuildingAction(Building building, bool upgrade)
    {
        if (BuildingManager.Instance == null)
        {
            ShowDetails("建筑", "建筑管理器尚未初始化。", building.Id);
            return;
        }
        ExpantaNum amount = GetSelectedBuildingQuantity(building, upgrade, false);
        if (upgrade)
            BuildingManager.Instance.TryUpgrade(building, amount, out _);
        else
            BuildingManager.Instance.TryBuild(building, amount, out _);
        ShowBuildingDetails(building);
    }

    private void DeconstructBuilding(Building building)
    {
        if (BuildingManager.Instance == null)
        {
            ShowDetails("建筑", "建筑管理器尚未初始化。", building.Id);
            return;
        }
        ExpantaNum amount = GetSelectedBuildingQuantity(building, false, true);
        BuildingManager.Instance.TryDeconstruct(building, amount, out _);
        ShowBuildingDetails(building);
    }
}
