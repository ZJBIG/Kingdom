using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Page list presenters and building/resource actions.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private void AddBuildingRows(RectTransform parent)
    {
        int visible = 0;
        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Building definition = definitions[i];
            if (definition == null || !ShouldDisplayBuilding(definition))
                continue;
            string amount = "0";
            if (BuildingManager.Instance != null && BuildingManager.Instance.States.TryGetValue(definition, out BuildingState state))
                amount = state.Amount.ToGameString();
            AddBuildingRow(parent, visible++, definition, amount);
        }
        if (visible == 0)
            Label("Empty", parent, "当前没有可显示的建筑。", 22, TextSecondary, Vector2.zero, Vector2.one, new Vector2(18, 18), new Vector2(-18, -18));
        Debug.Log($"[KingdomUI] Building rows: definitions={definitions.Count}, visible={visible}, rowsRect={parent.rect.size}, pageHost={(pageHost == null ? Vector2.zero : pageHost.rect.size)}");
    }

    private void AddWorkshopRows(RectTransform parent)
    {
        int visible = 0;
        IReadOnlyList<WorkshopUpgradeDefinition> definitions = DataBase<WorkshopUpgradeDefinition>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            WorkshopUpgradeDefinition definition = definitions[i];
            if (definition == null || !WorkshopPrerequisitesMet(definition))
                continue;
            AddTextRow(parent, visible++, definition.Label, definition.Id + "   /   " + definition.Category,
                () => ShowDetails(definition.Label, definition.Description, definition.Id));
        }
        if (visible == 0)
            Label("Empty", parent, "当前没有可显示的工坊升级。", 22, TextSecondary, Vector2.zero, Vector2.one, new Vector2(18, 18), new Vector2(-18, -18));
    }

    private void AddResearchRows(RectTransform parent)
    {
        int visible = 0;
        IReadOnlyList<Research> definitions = DataBase<Research>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research research = definitions[i];
            if (research == null)
                continue;
            AddResearchRow(parent, visible++, research);
        }
        if (visible == 0)
            Label("Empty", parent, "当前没有可显示的研究。", 26, TextSecondary, Vector2.zero, Vector2.one, new Vector2(18, 18), new Vector2(-18, -18));
    }

    private void AddResearchRow(RectTransform parent, int index, Research research)
    {
        ResearchState state = null;
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.States.TryGetValue(research, out state);
        string percent = state == null ? "0%" : (state.ProgressRatio * 100).ToGameString() + "%";
        ResearchStatus researchStatus = state == null ? ResearchStatus.Locked : state.Status;
        float rowHeight = 112f;
        float top = -nextRowTop;
        RectTransform row = Rect("ResearchRow_" + index, parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, top - rowHeight), new Vector2(0, top));
        nextRowTop += rowHeight + 12f;
        Image surface = PanelRect("Surface", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelRaised);
        Button button = row.gameObject.AddComponent<Button>();
        row.gameObject.AddComponent<UIPageScrollDragForwarder>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.ColorTint;
        ApplyButtonColors(button, PanelRaised);
        button.onClick.AddListener(() => ShowResearchDetails(research));
        Label("Label", row, research.Label, 27, TextPrimary, new Vector2(0, 0), new Vector2(.38f, 1), new Vector2(22, 0), new Vector2(-8, 0));
        Label("Era", row, research.TechLevel.ToString(), 22, TextSecondary, new Vector2(.38f, 0), new Vector2(.58f, 1), new Vector2(8, 0), new Vector2(-8, 0));
        Label("Percentage", row, percent, 24, Copper, new Vector2(.58f, 0), new Vector2(.76f, 1), new Vector2(8, 0), new Vector2(-8, 0));
        Label("State", row, ResearchStateLabel(researchStatus), 22,
            researchStatus == ResearchStatus.Completed ? Positive : TextSecondary,
            new Vector2(.76f, 0), Vector2.one, new Vector2(8, 0), new Vector2(-18, 0));
    }

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

    private void AddResourceRows(RectTransform parent)
    {
        int visible = 0;
        IReadOnlyList<Resource> definitions = DataBase<Resource>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Resource resource = definitions[i];
            if (resource == null || !IsResourceVisible(resource))
                continue;
            AddResourceRow(parent, visible++, resource, () => ShowResourceDetails(resource));
        }
        if (visible == 0)
            Label("Empty", parent, "当前没有可显示的资源。", 22, TextSecondary, Vector2.zero, Vector2.one, new Vector2(18, 18), new Vector2(-18, -18));
    }

    private bool IsResourceVisible(Resource resource)
    {
        if (ResourceManager.Instance == null)
            return false;
        if (!ResourceManager.Instance.States.TryGetValue(resource, out ResourceState state))
            return false;
        return state.Amount > ExpantaNum.Zero || state.ProductionRate > ExpantaNum.Zero || state.ConsumptionRate > ExpantaNum.Zero;
    }

    private void AddTextRow(RectTransform parent, int index, string title, string subtitle, UnityEngine.Events.UnityAction action)
    {
        float rowHeight = 104f;
        float top = -nextRowTop;
        RectTransform row = Rect("TextRow_" + index, parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, top - rowHeight), new Vector2(0, top));
        nextRowTop += rowHeight + 12f;
        Image surface = PanelRect("Surface", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelRaised);
        Button button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.ColorTint;
        ApplyButtonColors(button, PanelRaised);
        button.onClick.AddListener(action);
        Label("Title", row, title, 30, TextPrimary, Vector2.zero, new Vector2(.55f, 1), new Vector2(24, 0), new Vector2(-8, 0));
        Label("Subtitle", row, subtitle, 22, TextSecondary, new Vector2(.55f, 0), Vector2.one, new Vector2(8, 0), new Vector2(-24, 0));
    }

    private void AddBuildingRow(RectTransform parent, int index, Building building, string amount)
    {
        float rowHeight = 104f;
        float top = -nextRowTop;
        RectTransform row = Rect("BuildingRow_" + index, parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, top - rowHeight), new Vector2(0, top));
        nextRowTop += rowHeight + 12f;
        Image cardSurface = PanelRect("Surface", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelRaised);
        Button cardButton = row.gameObject.AddComponent<Button>();
        row.gameObject.AddComponent<UIPageScrollDragForwarder>();
        cardButton.targetGraphic = cardSurface;
        cardButton.transition = Selectable.Transition.ColorTint;
        ApplyButtonColors(cardButton, PanelRaised);
        cardButton.onClick.AddListener(() => ShowBuildingDetails(building));
        Label("Label", row, building.Label, 30, TextPrimary, Vector2.zero, new Vector2(.43f, 1), new Vector2(24, 0), new Vector2(-8, 0));
        TMP_Text amountLabel = Label("Amount", row, amount, 30, TextPrimary, new Vector2(.43f, 0), new Vector2(.57f, 1), new Vector2(8, 0), new Vector2(-8, 0));
        buildingAmountLabels[building] = amountLabel;

        bool canUpgrade = BuildingManager.Instance != null && BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out _);
        string actionLabel = canUpgrade ? "升级" : "建造";
        Button buildButton = Button(actionLabel, row, actionLabel, Copper,
            new Vector2(.58f, .18f), new Vector2(.78f, .82f), new Vector2(6, 0), new Vector2(-6, 0),
            () => PerformBuildingAction(building, canUpgrade));
        buildButton.transition = Selectable.Transition.ColorTint;
        SetBuildingActionButtonFontSize(buildButton);
        SetBuildingActionButtonText(buildButton, canUpgrade ? "\u5347\u7ea7" : "\u5efa\u9020");
        buildingActionButtons[building] = buildButton;
        buildingActionUpgradeModes[building] = canUpgrade;
        SetBuildingActionButtonState(buildButton, CanPerformBuildingAction(building, canUpgrade));

        bool hasAmount = BuildingManager.Instance != null &&
            BuildingManager.Instance.States.TryGetValue(building, out BuildingState buildingState) &&
            buildingState.Amount > ExpantaNum.Zero;
        Button deconstructButton = Button("Deconstruct", row, "拆除", hasAmount ? Error : Panel,
            new Vector2(.79f, .18f), new Vector2(.98f, .82f), new Vector2(6, 0), new Vector2(-6, 0),
            () => DeconstructBuilding(building));
        deconstructButton.transition = Selectable.Transition.ColorTint;
        SetBuildingActionButtonFontSize(deconstructButton);
        SetBuildingActionButtonText(deconstructButton, "\u62c6\u9664");
        buildingDeconstructSurfaces[building] = deconstructButton.targetGraphic as Image;
    }

    private static void SetBuildingActionButtonFontSize(Button button)
    {
        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonText == null)
            buttonText = Label("Text", button.transform, string.Empty, 30, TextPrimary,
                Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        if (buttonText != null)
        {
            buttonText.enabled = true;
            buttonText.gameObject.SetActive(true);
            buttonText.fontSize = 30;
            buttonText.color = TextPrimary;
            buttonText.alignment = TextAlignmentOptions.MidlineLeft;
            buttonText.enableWordWrapping = false;
            buttonText.overflowMode = TextOverflowModes.Overflow;
            if (sharedFontAsset != null)
                buttonText.font = sharedFontAsset;
            buttonText.transform.SetAsLastSibling();
        }
    }

    private static void SetBuildingActionButtonText(Button button, string value)
    {
        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonText == null)
            buttonText = Label("Text", button.transform, value, 30, TextPrimary,
                Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        buttonText.text = value;
        buttonText.enabled = true;
        buttonText.gameObject.SetActive(true);
        buttonText.fontSize = 30;
        buttonText.color = TextPrimary;
        buttonText.alignment = TextAlignmentOptions.MidlineLeft;
        buttonText.enableWordWrapping = false;
        buttonText.overflowMode = TextOverflowModes.Overflow;
        buttonText.raycastTarget = false;
        if (sharedFontAsset != null)
            buttonText.font = sharedFontAsset;
        buttonText.transform.SetAsLastSibling();
    }

    private static void SetBuildingActionButtonState(Button button, bool available)
    {
        button.interactable = available;
        Image surface = button.targetGraphic as Image;
        if (surface != null)
            surface.color = available ? Copper : Panel;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null)
            text = Label("Text", button.transform, string.Empty, 30, TextPrimary,
                Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        if (text != null)
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

    private void AddResourceRow(RectTransform parent, int index, Resource resource, UnityEngine.Events.UnityAction action)
    {
        ResourceState state = null;
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.States.TryGetValue(resource, out state);
        string amount = state == null ? "0" : state.Amount.ToGameString();
        ExpantaNum net = state == null ? ExpantaNum.Zero : state.ProductionRate - state.ConsumptionRate;
        string change = (net >= ExpantaNum.Zero ? "+" : string.Empty) + net.ToGameString() + "/s";
        float rowHeight = 104f;
        float top = -nextRowTop;
        RectTransform row = Rect("ResourceRow_" + index, parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, top - rowHeight), new Vector2(0, top));
        nextRowTop += rowHeight + 12f;
        Image surface = PanelRect("Surface", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelRaised);
        Button button = row.gameObject.AddComponent<Button>();
        row.gameObject.AddComponent<UIPageScrollDragForwarder>();
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.ColorTint;
        ApplyButtonColors(button, PanelRaised);
        button.onClick.AddListener(action);
        RectTransform iconRect = Rect("Texture", row, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, -34), new Vector2(86, 34));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = resource.Sprite;
        icon.color = resource.Color;
        icon.preserveAspect = true;
        Label("Label", row, resource.Label, 30, TextPrimary, Vector2.zero, new Vector2(.46f, 1), new Vector2(108, 0), new Vector2(-8, 0));
        TMP_Text amountLabel = Label("Amount", row, amount, 28, TextPrimary, new Vector2(.46f, 0), new Vector2(.70f, 1), new Vector2(8, 0), new Vector2(-8, 0));
        TMP_Text changeLabel = Label("ChangeRate", row, change, 25, net >= ExpantaNum.Zero ? Positive : Error, new Vector2(.70f, 0), Vector2.one, new Vector2(8, 0), new Vector2(-24, 0));
        resourceAmountLabels[resource] = amountLabel;
        resourceChangeLabels[resource] = changeLabel;
    }
}
