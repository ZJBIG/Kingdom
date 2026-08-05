using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Building batch-operation controls and quantity normalization.
/// This partial contains no gameplay rules; it only selects the amount passed
/// to BuildingManager.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private void RefreshBuildingQuantityHeader()
    {
        if (buildingPageTitle == null || buildingQuantityControls == null)
            return;
        bool visible = populatedPage == "Buildings";
        buildingPageTitle.text = "建筑";
        buildingPageTitle.text = "\u5efa\u7b51";
        buildingPageTitle.enabled = true;
        buildingPageTitle.gameObject.SetActive(visible);
        RectTransform titleRect = buildingPageTitle.rectTransform;
        titleRect.SetParent(buildingQuantityControls.transform.parent, false);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(34f, -88f);
        titleRect.offsetMax = new Vector2(170f, -28f);
        buildingPageTitle.transform.SetAsLastSibling();
        buildingQuantityControls.gameObject.SetActive(visible);
    }

    private void BuildBuildingQuantityControls(Transform parent)
    {
        Transform existing = parent.Find("BuildingQuantityControls");
        if (existing != null)
        {
            buildingQuantityControls = existing as RectTransform;
            RepairBuildingQuantityControls(buildingQuantityControls);
            return;
        }

        RectTransform controls = Rect("BuildingQuantityControls", parent,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(180, -88), new Vector2(-20, -28));
        buildingQuantityControls = controls;
        buildingPageTitle = Label("BuildingPageTitle", parent, "建筑", 36, TextPrimary,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -88), new Vector2(170, -28));
        buildingPageTitle.alignment = TextAlignmentOptions.MidlineLeft;
        buildingPageTitle.text = "\u5efa\u7b51";
        buildingPageTitle.gameObject.SetActive(false);
        PanelRect("Surface", controls, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
        Label("Caption", controls, "数量", 30, TextPrimary,
            new Vector2(0, 0), new Vector2(.09f, 1), new Vector2(12, 0), new Vector2(-8, 0));

        AddBuildingQuantityButton(controls, BuildingQuantityMode.One, "x1", .10f, .28f);
        AddBuildingQuantityButton(controls, BuildingQuantityMode.Ten, "x10", .30f, .48f);
        AddBuildingQuantityButton(controls, BuildingQuantityMode.Max, "xMax", .50f, .68f);
        AddBuildingQuantityButton(controls, BuildingQuantityMode.Custom, "Custom", .70f, .84f);

        RectTransform inputRect = Rect("CustomQuantityInput", controls,
            new Vector2(.86f, .10f), new Vector2(.99f, .90f), Vector2.zero, Vector2.zero);
        Image inputSurface = inputRect.gameObject.AddComponent<Image>();
        inputSurface.color = PanelRaised;
        customQuantityInput = inputRect.gameObject.AddComponent<TMP_InputField>();
        customQuantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        customQuantityInput.lineType = TMP_InputField.LineType.SingleLine;
        customQuantityInput.caretWidth = 3;
        customQuantityInput.caretBlinkRate = 0.8f;
        customQuantityInput.selectionColor = Copper;
        customQuantityInput.shouldHideMobileInput = false;
        TMP_Text inputText = Label("Text", inputRect, string.Empty, 30, TextPrimary,
            Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
        TMP_Text inputPlaceholder = Label("Placeholder", inputRect, "请输入数量", 24, TextSecondary,
            Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
        customQuantityInput.textComponent = inputText;
        customQuantityInput.placeholder = inputPlaceholder;
        customQuantityInput.textViewport = inputRect;
        customQuantityInput.text = string.Empty;
        customQuantityInput.onValueChanged.AddListener(value =>
        {
            if (ExpantaNum.TryParse(value, out ExpantaNum parsed) && parsed.IsFinite && parsed >= ExpantaNum.One)
                customBuildingQuantity = parsed.Floor();
        });
        customQuantityInput.onSelect.AddListener(_ => SelectBuildingQuantityMode(BuildingQuantityMode.Custom));
        customQuantityInput.onEndEdit.AddListener(_ => NormalizeCustomQuantityInput());
        UpdateBuildingQuantityButtonColors();
    }

    private void RepairBuildingQuantityControls(RectTransform controls)
    {
        buildingQuantityButtons.Clear();
        buildingPageTitle = controls.parent.Find("BuildingPageTitle")?.GetComponent<TMP_Text>();
        if (buildingPageTitle == null)
        {
            buildingPageTitle = Label("BuildingPageTitle", controls.parent, "建筑", 36, TextPrimary,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -88), new Vector2(170, -28));
        }
        buildingPageTitle.alignment = TextAlignmentOptions.MidlineLeft;
        buildingPageTitle.fontSize = 36;
        buildingPageTitle.text = "\u5efa\u7b51";
        buildingPageTitle.enabled = true;

        AddOrRepairBuildingQuantityButton(controls, BuildingQuantityMode.One, "x1");
        AddOrRepairBuildingQuantityButton(controls, BuildingQuantityMode.Ten, "x10");
        AddOrRepairBuildingQuantityButton(controls, BuildingQuantityMode.Max, "xMax");
        AddOrRepairBuildingQuantityButton(controls, BuildingQuantityMode.Custom, "Custom");

        customQuantityInput = controls.Find("CustomQuantityInput")?.GetComponent<TMP_InputField>();
        if (customQuantityInput != null)
            customQuantityInput.textViewport = customQuantityInput.transform as RectTransform;
        UpdateBuildingQuantityButtonColors();
    }

    private void AddOrRepairBuildingQuantityButton(RectTransform parent, BuildingQuantityMode mode, string label)
    {
        Button button = parent.Find("Quantity_" + mode)?.GetComponent<Button>();
        if (button == null)
        {
            AddBuildingQuantityButton(parent, mode, label, .10f, .28f);
            button = parent.Find("Quantity_" + mode)?.GetComponent<Button>();
        }

        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonText == null)
            buttonText = Label("Text", button.transform, label, 32, TextPrimary,
                Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        buttonText.text = label;
        buttonText.enabled = true;
        buttonText.gameObject.SetActive(true);
        buttonText.fontSize = 32;
        buttonText.color = TextPrimary;
        buttonText.alignment = TextAlignmentOptions.MidlineLeft;
        buttonText.enableWordWrapping = false;
        buttonText.overflowMode = TextOverflowModes.Overflow;
        buttonText.raycastTarget = false;
        if (sharedFontAsset != null)
            buttonText.font = sharedFontAsset;
        buttonText.transform.SetAsLastSibling();
        button.transform.SetAsLastSibling();
        buildingQuantityButtons[mode] = button;
    }

    private void NormalizeCustomQuantityInput()
    {
        if (customQuantityInput == null)
            return;
        if (!ExpantaNum.TryParse(customQuantityInput.text, out ExpantaNum parsed) ||
            !parsed.IsFinite || parsed < ExpantaNum.One)
            parsed = ExpantaNum.One;
        customBuildingQuantity = parsed.Floor();
        if (customBuildingQuantity < ExpantaNum.One)
            customBuildingQuantity = ExpantaNum.One;
        customQuantityInput.text = customBuildingQuantity.ToString();
    }

    private void AddBuildingQuantityButton(RectTransform parent, BuildingQuantityMode mode, string label, float minX, float maxX)
    {
        Button button = Button("Quantity_" + mode, parent, label, PanelRaised,
            new Vector2(minX, .10f), new Vector2(maxX, .90f), Vector2.zero, Vector2.zero,
            () => SelectBuildingQuantityMode(mode));
        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonText == null)
            buttonText = Label("Text", button.transform, label, 32, TextPrimary,
                Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        if (buttonText != null)
        {
            buttonText.enabled = true;
            buttonText.gameObject.SetActive(true);
            buttonText.text = label;
            buttonText.fontSize = 32;
            buttonText.color = TextPrimary;
            buttonText.alignment = TextAlignmentOptions.MidlineLeft;
            buttonText.enableWordWrapping = false;
            buttonText.overflowMode = TextOverflowModes.Overflow;
            buttonText.raycastTarget = false;
            if (sharedFontAsset != null)
                buttonText.font = sharedFontAsset;
            buttonText.transform.SetAsLastSibling();
        }
        buildingQuantityButtons[mode] = button;
    }

    private void SelectBuildingQuantityMode(BuildingQuantityMode mode)
    {
        buildingQuantityMode = mode;
        UpdateBuildingQuantityButtonColors();
    }

    private void UpdateBuildingQuantityButtonColors()
    {
        foreach (KeyValuePair<BuildingQuantityMode, Button> pair in buildingQuantityButtons)
        {
            Image image = pair.Value.targetGraphic as Image;
            if (image != null)
                image.color = pair.Key == buildingQuantityMode ? Copper : PanelRaised;
        }
    }

    private ExpantaNum GetSelectedBuildingQuantity(Building building, bool upgrade, bool deconstruct)
    {
        switch (buildingQuantityMode)
        {
            case BuildingQuantityMode.Ten:
                return new ExpantaNum(10);
            case BuildingQuantityMode.Max:
                if (deconstruct)
                {
                    if (BuildingManager.Instance.States.TryGetValue(building, out BuildingState maxState))
                        return maxState.Amount;
                    return ExpantaNum.Zero;
                }
                return upgrade
                    ? BuildingManager.Instance.GetMaxUpgradeable(building, ExpantaNum.PositiveInfinity)
                    : BuildingManager.Instance.GetMaxBuildable(building, ExpantaNum.PositiveInfinity);
            case BuildingQuantityMode.Custom:
                ExpantaNum maximum;
                if (deconstruct)
                {
                    maximum = BuildingManager.Instance.States.TryGetValue(building, out BuildingState customState)
                        ? customState.Amount
                        : ExpantaNum.Zero;
                }
                else
                {
                    maximum = upgrade
                        ? BuildingManager.Instance.GetMaxUpgradeable(building, ExpantaNum.PositiveInfinity)
                        : BuildingManager.Instance.GetMaxBuildable(building, ExpantaNum.PositiveInfinity);
                }
                return customBuildingQuantity >= maximum ? maximum : customBuildingQuantity;
            default:
                return ExpantaNum.One;
        }
    }
}
