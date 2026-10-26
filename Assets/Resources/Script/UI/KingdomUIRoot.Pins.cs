using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private Button detailPinButton;
    private static Sprite pinOutlineSprite;
    private static Sprite pinFilledSprite;

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
        float position = pageScroll == null ? 1f : pageScroll.verticalNormalizedPosition;
        if (kind == UIPinPreferences.Resource)
            resourceRowsBuilt = false;
        else if (kind == UIPinPreferences.Building)
            buildingRowsBuilt = false;
        if (populatedPage == "Sectors")
        {
            if (kind == UIPinPreferences.Building)
                for (int i = 0; i < sectorRowViews.Count; i++)
                    if (sectorRowViews[i].Menu != null) RebuildSectorMenu(sectorRowViews[i]);
            RefreshSectorRowsAndLayout();
        }
        else if ((kind == UIPinPreferences.Resource && populatedPage == "Resources") ||
                 (kind == UIPinPreferences.Building && populatedPage == "Buildings"))
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
