using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private Button detailPinButton;
    private string detailPinKind;
    private string detailPinId;

    private static string FirstDescriptionLine(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return "查看详情了解用途";
        int end = description.IndexOfAny(new[] { '\r', '\n', '。' });
        return end < 0 ? description : description.Substring(0, end);
    }

    private static string GetBuildingRoleSummary(Building building)
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
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = pinned ? "取消" : "置顶";
            label.color = pinned ? Copper : TextSecondary;
        }
        Image surface = button.targetGraphic as Image;
        if (surface != null)
            surface.color = pinned ? new Color(.74f, .57f, .30f, .28f) : new Color(1f, 1f, 1f, .08f);
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

    private static string GetPinnedResearchTitle(Research research)
    {
        string title = string.IsNullOrEmpty(research.Label) ? research.Id : research.Label;
        return UIPinPreferences.IsPinned(UIPinPreferences.Research, research.Id) ? "置顶·" + title : title;
    }

    private void RefreshDetailPin()
    {
        if (detailPinButton == null)
        {
            detailPinButton = detailPanel?.Find("Surface/PinButton")?.GetComponent<Button>();
            detailPinKind = null;
            detailPinId = null;
        }
        if (detailPinButton == null) return;
        Button button = detailPinButton;
        string kind = null;
        string id = null;
        if (selectedResource != null) { kind = UIPinPreferences.Resource; id = selectedResource.Id; }
        else if (selectedBuilding != null) { kind = UIPinPreferences.Building; id = selectedBuilding.Id; }
        else if (selectedWorkshop != null) { kind = UIPinPreferences.Workshop; id = selectedWorkshop.Id; }
        else if (selectedSectorDefinition != null) { kind = UIPinPreferences.Sector; id = selectedSectorDefinition.Id; }
        else if (selectedResearchNode != null) { kind = UIPinPreferences.Research; id = selectedResearchNode.Id; }
        button.gameObject.SetActive(kind != null);
        if (detailPinKind != kind || detailPinId != id)
        {
            detailPinKind = kind;
            detailPinId = id;
            button.onClick.RemoveAllListeners();
            if (kind != null) button.onClick.AddListener(() => TogglePin(kind, id));
        }
        if (kind == null) return;
        SetPinButtonVisual(button, UIPinPreferences.IsPinned(kind, id));
    }
}
