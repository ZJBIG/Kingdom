using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private Button detailPinButton;
    private static Sprite pinOutlineSprite;
    private static Sprite pinFilledSprite;

    private static string FirstDescriptionLine(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return "查看详情了解用途";
        int end = description.IndexOfAny(new[] { '\r', '\n', '。' });
        return end < 0 ? description : description.Substring(0, end);
    }

    private static string GetBuildingRoleSummary(BuildingDef building)
    {
        if (building.ResourceGenerationRates.Count > 0)
        {
            var output = building.ResourceGenerationRates[0];
            if (output.First != null)
                return "产出 " + output.First.Label + " · 基础 +" + output.Second.ToGameString() + "/s";
        }
        if (building.FoodProductionRate > ExpantaNum.Zero)
            return "食物供给 · 基础 +" + building.FoodProductionRate.ToGameString() + "/s";
        if (building.PowerProductionRate > ExpantaNum.Zero)
            return "电力供给 · 基础 +" + building.PowerProductionRate.ToGameString() + "/s";
        if (building.LogisticsProductionRate > ExpantaNum.Zero)
            return "物流供给 · 基础 +" + building.LogisticsProductionRate.ToGameString() + "/s";
        return FirstDescriptionLine(building.Description);
    }

    private void BindRowPin(GameObject row, string kind, string id)
    {
        Button button = row.transform.Find("PinButton")?.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("[KingdomPins] Missing authored PinButton: " + row.name);
            return;
        }
        SetPinButtonVisual(button, UIPinPreferences.IsPinned(kind, id));
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => TogglePin(kind, id));
    }

    private static void SetPinButtonVisual(Button button, bool pinned)
    {
        Image icon = button.transform.Find("Icon")?.GetComponent<Image>();
        if (icon == null) return;
        if (pinOutlineSprite == null) pinOutlineSprite = Resources.Load<Sprite>("Art/UI/Pins/PinOutline");
        if (pinFilledSprite == null) pinFilledSprite = Resources.Load<Sprite>("Art/UI/Pins/PinFilled");
        icon.sprite = pinned ? pinFilledSprite : pinOutlineSprite;
        icon.color = pinned ? BasicMetalIngot : TextSecondary;
        button.interactable = true;
    }

    private void TogglePin(string kind, string id)
    {
        UIPinPreferences.SetPinned(kind, id, !UIPinPreferences.IsPinned(kind, id));
        UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
        float position = pageScroll == null ? 1f : pageScroll.verticalNormalizedPosition;
        if (kind == UIPinPreferences.Resource)
            resourceRowsBuilt = false;
        else if (kind == UIPinPreferences.Building)
            buildingRowsBuilt = false;
        else if (kind == UIPinPreferences.Workshop)
            workshopRowsBuilt = false;
        if (populatedPage == "Sectors")
        {
            if (kind == UIPinPreferences.Building)
                for (int i = 0; i < sectorRowViews.Count; i++)
                    if (sectorRowViews[i].Menu != null) RebuildSectorMenu(sectorRowViews[i]);
            RefreshSectorRowsAndLayout();
        }
        else if ((kind == UIPinPreferences.Resource && populatedPage == "Resources") ||
                 (kind == UIPinPreferences.Building && populatedPage == "Buildings") ||
                 (kind == UIPinPreferences.Workshop && populatedPage == "Workshop"))
            PopulatePage(populatedPage);
        if (kind == UIPinPreferences.Research)
            RefreshResearchPinButtons();
        Canvas.ForceUpdateCanvases();
        if (pageScroll != null)
            pageScroll.verticalNormalizedPosition = position;
        RefreshDetailPin();
    }

    private void RefreshResearchPinButtons()
    {
        foreach (var pair in researchTreeNodes)
        {
            if (pair.Value == null) continue;
            Button pin = pair.Value.transform.Find("PinButton")?.GetComponent<Button>();
            if (pin != null)
                SetPinButtonVisual(pin, UIPinPreferences.IsPinned(UIPinPreferences.Research, pair.Key.Id));
            if (researchNodeVisualReferences.TryGetValue(pair.Key, out ResearchNodeVisualReferences references) &&
                references.LabelText != null)
                references.LabelText.text = GetPinnedResearchTitle(pair.Key);
        }
    }

    private static string GetPinnedResearchTitle(ResearchDef research)
    {
        string title = string.IsNullOrEmpty(research.Label) ? research.Id : research.Label;
        return UIPinPreferences.IsPinned(UIPinPreferences.Research, research.Id) ? "置顶·" + title : title;
    }

    private void RefreshDetailPin()
    {
        if (detailPinButton == null)
            detailPinButton = detailPanel?.Find("Surface/PinButton")?.GetComponent<Button>();
        if (detailPinButton != null) detailPinButton.gameObject.SetActive(false);
    }
}
