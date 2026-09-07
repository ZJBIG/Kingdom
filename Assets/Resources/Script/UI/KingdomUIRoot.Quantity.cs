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
        if (buildingControls == null)
            return;
        bool visible = populatedPage == "Buildings";
        TMP_Text buildingPageTitle = pageTitle;
        buildingControls.gameObject.SetActive(visible);
    }

    private void BuildBuildingControls(Transform parent)
    {
        Transform existing = parent.Find("BuildingControls");
        if (existing == null)
        {
            Debug.LogError("[王国界面] Authored BuildingControls is missing; fixed quantity UI will not be generated at runtime.");
            return;
        }
        buildingControls = existing as RectTransform;
        RepairBuildingControls(buildingControls);
    }

    private void RepairBuildingControls(RectTransform controls)
    {
        if (controls == null)
            return;

        buildingQuantityButtons.Clear();
        showBuildingDetailsToggle = controls.Find("ShowDetails")?.GetComponent<Toggle>();
        if (showBuildingDetailsToggle == null)
        {
            Debug.LogError("Authored ShowDetails toggle is missing under BuildingControls.");
            return;
        }
        useCompactBuildingDetails = !showBuildingDetailsToggle.isOn;
        showBuildingDetailsToggle.onValueChanged.RemoveListener(OnShowBuildingDetailsChanged);
        showBuildingDetailsToggle.onValueChanged.AddListener(OnShowBuildingDetailsChanged);
        controls.SetAsLastSibling();
        Transform content = controls.parent?.parent;
        pageTitle = content?.Find("PageTitle")?.GetComponent<TMP_Text>();
        if (pageTitle == null)
        {
            Debug.LogError("[王国界面] 场景外壳缺少已配置的建筑页面标题。");
            return;
        }
        pageTitle.alignment = TextAlignmentOptions.MidlineLeft;
        pageTitle.fontSize = 30;
        pageTitle.text = "\u5efa\u7b51";
        pageTitle.enabled = true;

        AddOrRepairBuildingQuantityButton(controls, BuildingQuantityMode.One, "1个");
        AddOrRepairBuildingQuantityButton(controls, BuildingQuantityMode.Ten, "10个");
        AddOrRepairBuildingQuantityButton(controls, BuildingQuantityMode.Max, "最大");
        AddOrRepairBuildingQuantityButton(controls, BuildingQuantityMode.Custom, "自定义");

        customQuantityInput = controls.Find("CustomQuantityInput")?.GetComponent<TMP_InputField>();
        if (customQuantityInput != null)
        {
            customQuantityInput.textViewport = customQuantityInput.transform as RectTransform;
            customQuantityInput.textComponent = customQuantityInput.transform.Find("Text")?.GetComponent<TMP_Text>();
            customQuantityInput.placeholder = customQuantityInput.transform.Find("Placeholder")?.GetComponent<TMP_Text>();
            customQuantityInput.onValueChanged.RemoveAllListeners();
            customQuantityInput.onValueChanged.AddListener(HandleCustomQuantityInputChanged);
            customQuantityInput.onValidateInput = (text, charIndex, addedChar) =>
                char.IsDigit(addedChar) || addedChar == 'e' || addedChar == 'E' ||
                addedChar == '.'
                    ? addedChar
                    : '\0';
            customQuantityInput.onSelect.RemoveAllListeners();
            customQuantityInput.onSelect.AddListener(_ => SelectBuildingQuantityMode(BuildingQuantityMode.Custom));
            customQuantityInput.onEndEdit.RemoveAllListeners();
            customQuantityInput.onEndEdit.AddListener(_ => NormalizeCustomQuantityInput());
        }
        UpdateBuildingQuantityButtonColors();
    }

    private void OnShowBuildingDetailsChanged(bool value)
    {
        bool useCompact = !value;
        if (useCompactBuildingDetails == useCompact)
            return;
        useCompactBuildingDetails = useCompact;
        if (detailIsBuilding && selectedBuilding != null)
            ShowBuildingDetails(selectedBuilding, true);
    }

    private void HandleCustomQuantityInputChanged(string value)
    {
        if (value.IndexOf('+') >= 0 || value.IndexOf('-') >= 0)
        {
            customQuantityInput.SetTextWithoutNotify(customBuildingQuantity.ToString());
            return;
        }
        if (!ExpantaNum.TryParse(value, out ExpantaNum parsed) ||
            !parsed.IsFinite || parsed < ExpantaNum.One)
            return;
        customBuildingQuantity = parsed.Floor();
        RefreshSelectedBuildingDetails(selectedBuilding);
        RefreshLiveCardValues();
    }

    private void AddOrRepairBuildingQuantityButton(RectTransform parent, BuildingQuantityMode mode, string label)
    {
        Button button = parent.Find("Quantity_" + mode)?.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("[王国界面] Authored quantity button is missing: Quantity_" + mode);
            return;
        }
        button.gameObject.SetActive(true);
        button.interactable = true;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectBuildingQuantityMode(mode));

        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonText == null)
        {
            Debug.LogError("[王国界面] Authored quantity button has no text: Quantity_" + mode);
            return;
        }
        buttonText.text = label;
        buttonText.enabled = true;
        buttonText.gameObject.SetActive(true);
        buttonText.fontSize = 30;
        buttonText.color = TextPrimary;
        buttonText.alignment = TextAlignmentOptions.MidlineLeft;
        buttonText.enableWordWrapping = false;
        buttonText.overflowMode = TextOverflowModes.Overflow;
        buttonText.raycastTarget = false;
        if (button.targetGraphic != null)
            button.targetGraphic.raycastTarget = true;
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
        if (customQuantityInput.text.IndexOf('+') >= 0 ||
            customQuantityInput.text.IndexOf('-') >= 0 ||
            !ExpantaNum.TryParse(customQuantityInput.text, out ExpantaNum parsed) ||
            !parsed.IsFinite || parsed < ExpantaNum.One)
        {
            customQuantityInput.SetTextWithoutNotify(customBuildingQuantity.ToString());
            return;
        }
        customBuildingQuantity = parsed.Floor();
        if (customBuildingQuantity < ExpantaNum.One)
            customBuildingQuantity = ExpantaNum.One;
        customQuantityInput.SetTextWithoutNotify(customBuildingQuantity.ToString());
    }

    private void SelectBuildingQuantityMode(BuildingQuantityMode mode)
    {
        buildingQuantityMode = mode;
        UpdateBuildingQuantityButtonColors();
        RefreshSelectedBuildingDetails(selectedBuilding);
        RefreshLiveCardValues();
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
                return EnsureMinimumBuildQuantity(upgrade
                    ? BuildingManager.Instance.GetMaxUpgradeable(building, ExpantaNum.PositiveInfinity)
                    : BuildingManager.Instance.GetMaxBuildable(building, ExpantaNum.PositiveInfinity));
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
                ExpantaNum selected = customBuildingQuantity >= maximum
                    ? maximum
                    : customBuildingQuantity;
                return deconstruct ? selected : EnsureMinimumBuildQuantity(selected);
            default:
                return ExpantaNum.One;
        }
    }

    private static ExpantaNum EnsureMinimumBuildQuantity(ExpantaNum amount) =>
        amount < ExpantaNum.One ? ExpantaNum.One : amount;
}
