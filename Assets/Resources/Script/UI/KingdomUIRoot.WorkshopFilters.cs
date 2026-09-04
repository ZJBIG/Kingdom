using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds the authored Workshop filters under PageTool. Visual hierarchy and
/// labels remain owned by the Prefab.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private RectTransform workshopFilters;
    private Toggle showOwnedToggle;
    private Toggle affordableOnlyToggle;
    private bool showOwned;
    private bool affordableOnly;
    private readonly StringBuilder workshopFilterMembershipBuilder = new(512);
    private string lastWorkshopFilterMembershipSignature;

    private bool BindWorkshopFilters(Transform pageTool)
    {
        workshopFilters = pageTool?.Find("WorkshopFilters") as RectTransform;
        showOwnedToggle = workshopFilters?.Find("ShowOwned")?.GetComponent<Toggle>();
        affordableOnlyToggle = workshopFilters?.Find("AffordableOnly")?.GetComponent<Toggle>();
        if (workshopFilters == null || showOwnedToggle == null || affordableOnlyToggle == null)
        {
            Debug.LogError("[王国界面] Authored WorkshopFilters or its Toggles are missing under PageTool.");
            return false;
        }

        showOwned = false;
        affordableOnly = false;
        showOwnedToggle.SetIsOnWithoutNotify(false);
        affordableOnlyToggle.SetIsOnWithoutNotify(false);
        showOwnedToggle.onValueChanged.RemoveListener(OnShowOwnedChanged);
        showOwnedToggle.onValueChanged.AddListener(OnShowOwnedChanged);
        affordableOnlyToggle.onValueChanged.RemoveListener(OnAffordableOnlyChanged);
        affordableOnlyToggle.onValueChanged.AddListener(OnAffordableOnlyChanged);
        RefreshWorkshopFiltersVisibility();
        return true;
    }

    private void RefreshWorkshopFiltersVisibility()
    {
        if (workshopFilters != null)
            workshopFilters.gameObject.SetActive(populatedPage == "Workshop");
    }

    private void OnShowOwnedChanged(bool value)
    {
        if (showOwned == value)
            return;
        showOwned = value;
        RebuildWorkshopRowsForFilterChange();
    }

    private void OnAffordableOnlyChanged(bool value)
    {
        if (affordableOnly == value)
            return;
        affordableOnly = value;
        RebuildWorkshopRowsForFilterChange();
    }

    private void RebuildWorkshopRowsForFilterChange()
    {
        lastWorkshopFilterMembershipSignature = null;
        workshopRowsUiDirty = true;
        if (populatedPage == "Workshop" && !IsPageScrolling())
            RefreshWorkshopRows();
    }

    private void RefreshWorkshopFilterMembershipIfChanged()
    {
        if (!affordableOnly || populatedPage != "Workshop")
            return;
        string signature = BuildWorkshopFilterMembershipSignature();
        if (signature == lastWorkshopFilterMembershipSignature)
            return;
        lastWorkshopFilterMembershipSignature = signature;
        workshopRowsUiDirty = true;
    }

    private void CaptureWorkshopFilterMembershipSignature()
    {
        lastWorkshopFilterMembershipSignature = affordableOnly
            ? BuildWorkshopFilterMembershipSignature()
            : null;
    }

    private string BuildWorkshopFilterMembershipSignature()
    {
        workshopFilterMembershipBuilder.Clear();
        var definitions = DataBase<WorkshopUpgrade>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            WorkshopUpgrade definition = definitions[i];
            if (definition != null && ShouldRevealWorkshop(definition))
                workshopFilterMembershipBuilder.Append(definition.Id).Append(';');
        }
        return workshopFilterMembershipBuilder.ToString();
    }
}
