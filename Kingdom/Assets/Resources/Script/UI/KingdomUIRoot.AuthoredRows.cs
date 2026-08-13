using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Instantiates authored list-row prefabs and supplies only definition data
/// and interaction callbacks. Geometry and visual hierarchy belong to the
/// corresponding prefab assets.
/// </summary>
public sealed partial class KingdomUIRoot
{
    // Keep the alternating list treatment in one place so Resource, Workshop,
    // Building and Music rows remain visually consistent.
    private static readonly Color ListRowEven = new Color(.25f, .28f, .28f, 1f);
    private static readonly Color ListRowOdd = new Color(.08f, .10f, .10f, 1f);

    private static void ApplyListRowStyle(GameObject row, int index)
    {
        if (row == null)
            return;
        Image surface = row.GetComponent<Image>();
        if (surface == null)
        {
            Debug.LogError("[王国界面] Authored list row is missing its root Image: " + row.name);
            return;
        }
        surface.color = index % 2 == 0 ? ListRowEven : ListRowOdd;
        Button button = row.GetComponent<Button>();
        if (button != null)
        {
            button.targetGraphic = surface;
            // Keep the alternating normal color while restoring a visible
            // hover/pressed state for touch and mouse feedback.
            button.transition = Selectable.Transition.ColorTint;
            ApplyButtonColors(button, surface.color);
        }
    }

    private static void ApplyButtonColors(Button button, Color normal)
    {
        if (button == null)
            return;
        ColorBlock colors = button.colors;
        // Image.color owns the alternating row palette. A white normal tint
        // prevents Unity from multiplying that palette a second time.
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.90f, .90f, .90f, 1f);
        colors.pressedColor = new Color(.54f, .54f, .54f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(.62f, .64f, .63f, .90f);
        button.colors = colors;
    }

    private void BuildAuthoredResourceRows(RectTransform parent)
    {
        int visible = 0;
        IReadOnlyList<Resource> definitions = DataBase<Resource>.All;
        var orderedDefinitions = new List<Resource>();
        for (int i = 0; i < definitions.Count; i++)
        {
            Resource resource = definitions[i];
            if (resource == null || !IsResourceVisible(resource))
                continue;
            orderedDefinitions.Add(resource);
        }

        orderedDefinitions.Sort(CompareResourceRows);
        for (int i = 0; i < orderedDefinitions.Count; i++)
        {
            Resource resource = orderedDefinitions[i];
            ResourceState state = null;
            ResourceManager.Instance?.States.TryGetValue(resource, out state);
            string amount = state == null ? "0" : state.Amount.ToGameString();
            ExpantaNum net = state == null ? ExpantaNum.Zero : state.ProductionRate - state.ConsumptionRate;
            string change = (net >= ExpantaNum.Zero ? "+" : string.Empty) + net.ToGameString() + "/s";
            GameObject row = InstantiateAuthoredRow(KingdomUIPrefabLibrary.ResourceCard, parent, visible++);
            if (row == null)
                continue;
            ApplyListRowStyle(row, visible - 1);
            if (!SetRowText(row, "Label", resource.Label) ||
                !SetRowText(row, "Amount", amount) ||
                !SetRowText(row, "ChangeRate", change, net >= ExpantaNum.Zero ? Positive : Error))
                continue;
            Transform iconTransform = row.transform.Find("Icon");
            Image icon = iconTransform == null ? null : iconTransform.GetComponent<Image>();
            if (icon == null)
            {
                Debug.LogError("[王国界面] ResourceCard prefab is missing authored Icon Image.");
                continue;
            }
            icon.sprite = resource.Sprite;
            icon.color = resource.Color;
            icon.preserveAspect = true;
            Button button = RequireRowButton(row);
            if (button == null)
                continue;
            button.onClick.AddListener(() =>
            {
                UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
                ShowResourceDetails(resource);
            });
            resourceAmountLabels[resource] = row.transform.Find("Amount")?.GetComponent<TMP_Text>();
            resourceChangeLabels[resource] = row.transform.Find("ChangeRate")?.GetComponent<TMP_Text>();
        }
        Debug.Log($"[王国界面] Authored resource rows: visible={visible}, rowsRect={parent.rect.size}");
    }

    private static int CompareResourceRows(Resource left, Resource right)
    {
        int labelComparison = string.CompareOrdinal(
            string.IsNullOrEmpty(left.Label) ? left.Id : left.Label,
            string.IsNullOrEmpty(right.Label) ? right.Id : right.Label);
        return labelComparison != 0
            ? labelComparison
            : string.CompareOrdinal(left.Id, right.Id);
    }

    private void BuildAuthoredBuildingRows(RectTransform parent)
    {
        int visible = 0;
        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Building building = definitions[i];
            if (building == null || !ShouldDisplayBuilding(building))
                continue;
            string amount = "0";
            if (BuildingManager.Instance != null && BuildingManager.Instance.States.TryGetValue(building, out BuildingState state))
                amount = state.Amount.ToGameString();
            GameObject row = InstantiateAuthoredRow(KingdomUIPrefabLibrary.BuildingCard, parent, visible++);
            if (row == null)
                continue;
            ApplyListRowStyle(row, visible - 1);
            if (!SetRowText(row, "Label", building.Label) || !SetRowText(row, "Amount", amount))
                continue;
            Button cardButton = RequireRowButton(row);
            if (cardButton == null)
                continue;
            cardButton.onClick.AddListener(() =>
            {
                UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
                ShowBuildingDetails(building);
            });

            bool canUpgrade = BuildingManager.Instance != null &&
                BuildingManager.Instance.TryGetUnlockedUpgradeTarget(building, out _);
            Button buildButton = RequireChildButton(row, "BuildButton");
            Button deconstructButton = RequireChildButton(row, "DeconstructButton");
            if (buildButton == null || deconstructButton == null)
                continue;
            ExpantaNum buildQuantity = GetSelectedBuildingQuantity(building, canUpgrade, false);
            SetBuildingActionButtonText(
                buildButton,
                (canUpgrade ? "\u5347\u7ea7x" : "\u5efa\u9020x") + buildQuantity.ToGameString());
            buildButton.onClick.RemoveAllListeners();
            buildButton.onClick.AddListener(() =>
            {
                UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Purchase);
                PerformBuildingAction(building, canUpgrade);
            });
            buildButton.interactable = CanPerformBuildingAction(building, canUpgrade);
            SetBuildingActionButtonState(buildButton, buildButton.interactable);
            buildingActionButtons[building] = buildButton;
            buildingActionUpgradeModes[building] = canUpgrade;

            bool hasAmount = BuildingManager.Instance != null &&
                BuildingManager.Instance.States.TryGetValue(building, out BuildingState buildingState) &&
                buildingState.Amount > ExpantaNum.Zero;
            ExpantaNum deconstructQuantity = GetSelectedBuildingQuantity(building, false, true);
            SetBuildingActionButtonText(
                deconstructButton,
                "\u62c6\u9664x" + deconstructQuantity.ToGameString());
            deconstructButton.onClick.RemoveAllListeners();
            deconstructButton.onClick.AddListener(() =>
            {
                UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Sell);
                DeconstructBuilding(building);
            });
            deconstructButton.interactable = hasAmount;
            SetBuildingActionButtonState(deconstructButton, hasAmount);
            buildingDeconstructSurfaces[building] = deconstructButton.targetGraphic as Image;
            buildingDeconstructButtons[building] = deconstructButton;
            buildingAmountLabels[building] = row.transform.Find("Amount")?.GetComponent<TMP_Text>();
        }
        Debug.Log($"[王国界面] Authored building rows: visible={visible}, rowsRect={parent.rect.size}");
    }

    private void BuildAuthoredResearchRows(RectTransform parent)
    {
        int visible = 0;
        IReadOnlyList<Research> definitions = DataBase<Research>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Research research = definitions[i];
            if (research == null)
                continue;
            ResearchState state = null;
            ResearchManager.Instance?.States.TryGetValue(research, out state);
            string percent = state == null ? "0%" : (state.ProgressRatio * 100).ToGameString() + "%";
            ResearchStatus status = state == null ? ResearchStatus.Locked : state.Status;
            GameObject row = InstantiateAuthoredRow(KingdomUIPrefabLibrary.ResearchCard, parent, visible++);
            if (row == null)
                continue;
            if (!SetRowText(row, "Label", research.Label) ||
                !SetRowText(row, "Era", research.TechLevel.GetDescription()) ||
                !SetRowText(row, "Percentage", percent, Copper) ||
                !SetRowText(row, "State", ResearchStateLabel(status),
                    status == ResearchStatus.Completed ? Positive : TextSecondary))
                continue;
            Button button = RequireRowButton(row);
            if (button == null)
                continue;
            button.onClick.AddListener(() =>
            {
                UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
                ShowResearchDetails(research);
            });
        }
        Debug.Log($"[王国界面] Authored research rows: visible={visible}, rowsRect={parent.rect.size}");
    }

    private void BuildAuthoredWorkshopRows(RectTransform parent)
    {
        int visible = 0;
        IReadOnlyList<WorkshopUpgrade> definitions = DataBase<WorkshopUpgrade>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            WorkshopUpgrade definition = definitions[i];
            if (definition == null || !WorkshopPrerequisitesMet(definition))
                continue;
            GameObject row = InstantiateAuthoredRow(KingdomUIPrefabLibrary.TextRow, parent, visible++);
            if (row == null)
                continue;
            ApplyListRowStyle(row, visible - 1);
            if (!SetRowText(row, "Title", definition.Label) ||
                !SetRowText(row, "Subtitle", definition.Id + "   /   工坊物品"))
                continue;
            Button button = RequireRowButton(row);
            if (button == null)
                continue;
            button.onClick.AddListener(() =>
            {
                UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
                ShowDetails(definition.Label, definition.Description, definition.Id);
            });
        }
        Debug.Log($"[王国界面] Authored workshop rows: visible={visible}, rowsRect={parent.rect.size}");
    }

    private GameObject InstantiateAuthoredRow(string prefab, RectTransform parent, int index)
    {
        GameObject row = KingdomUIPrefabLibrary.Instantiate(prefab, parent);
        if (row == null)
        {
            Debug.LogError("[王国界面] Required authored row prefab is unavailable: " + prefab);
            return null;
        }
        RectTransform rect = row.GetComponent<RectTransform>();
        LayoutElement layout = row.GetComponent<LayoutElement>();
        if (rect == null || layout == null || layout.preferredHeight <= 1f)
        {
            Debug.LogError("[王国界面] Authored row prefab LayoutElement is incomplete: " + prefab);
            Destroy(row);
            return null;
        }
        float top = -nextRowTop;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(0f, layout.preferredHeight);
        rect.anchoredPosition = new Vector2(0f, top);
        nextRowTop += layout.preferredHeight;
        row.name = prefab + "_" + index;
        row.SetActive(true);
        return row;
    }

    private static GameObject InstantiateAuthoredDetailRow(string prefab, RectTransform parent, string name, float top)
    {
        GameObject row = KingdomUIPrefabLibrary.Instantiate(prefab, parent);
        if (row == null)
        {
            Debug.LogError("[王国界面] Required authored detail-row prefab is unavailable: " + prefab);
            return null;
        }
        RectTransform rect = row.GetComponent<RectTransform>();
        LayoutElement layout = row.GetComponent<LayoutElement>();
        if (rect == null || layout == null || layout.preferredHeight <= 1f)
        {
            Debug.LogError("[王国界面] Authored detail-row prefab is missing LayoutElement: " + prefab);
            Destroy(row);
            return null;
        }
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(0f, layout.preferredHeight);
        rect.anchoredPosition = new Vector2(0f, top);
        row.name = name;
        row.SetActive(true);
        return row;
    }

    private static bool SetRowText(GameObject row, string childName, string value, Color? color = null)
    {
        Transform child = row.transform.Find(childName);
        TMP_Text text = child == null ? null : child.GetComponent<TMP_Text>();
        if (text == null)
        {
            Debug.LogError("[王国界面] Authored row is missing text child: " + childName);
            return false;
        }
        text.text = value ?? string.Empty;
        if (color.HasValue)
            text.color = color.Value;
        text.enabled = true;
        text.gameObject.SetActive(true);
        text.raycastTarget = false;
        return true;
    }

    private static Button RequireRowButton(GameObject row)
    {
        Button button = row.GetComponent<Button>();
        if (button == null)
            Debug.LogError("[王国界面] Authored row is missing its root Button: " + row.name);
        return button;
    }

    private static Button RequireChildButton(GameObject row, string childName)
    {
        Transform child = row.transform.Find(childName);
        Button button = child == null ? null : child.GetComponent<Button>();
        if (button == null)
            Debug.LogError("[王国界面] Authored building card is missing Button: " + childName);
        return button;
    }
}
