using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime presentation refresh for live GameState, resource and building values.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private void Update()
    {
        liveRefreshTimer += Time.unscaledDeltaTime;
        if (liveRefreshTimer < 0.1f)
            return;
        liveRefreshTimer = 0f;
        RefreshUI();
    }

    private void RefreshBuildingDisplayMembership()
    {
        if (populatedPage != "Buildings" || BuildingManager.Instance == null)
            return;

        string signature = BuildBuildingDisplaySignature();
        if (lastBuildingDisplaySignature == null)
        {
            lastBuildingDisplaySignature = signature;
            return;
        }
        if (lastBuildingDisplaySignature == signature)
            return;

        // 升级完成后，旧层级可能变为零数量；只在显示成员变化时重建列表。
        lastBuildingDisplaySignature = signature;
        PopulatePage("Buildings");
    }

    public void RefreshUI()
    {
        CacheRuntimeManagers();
        bool uiScrolling = IsUiScrolling();
        if (!uiScrolling)
        {
            RefreshLiveCardValues();
            RefreshBuildingDisplayMembership();
            RefreshBuildingQuantityHeader();
        }
        bool researchGraphDragging = researchGraphGesture != null && researchGraphGesture.IsDragging;
        if (!uiScrolling && selectedResource != null && ShouldRefreshSelectedResource())
            RefreshResourceDetails(selectedResource);
        else if (!uiScrolling && selectedBuilding != null && ShouldRefreshSelectedBuilding())
            RefreshSelectedBuildingDetails(selectedBuilding);
        else if (!researchGraphDragging && populatedPage == "Research")
            RefreshResearchDynamicUI();
        if (!uiScrolling && populatedPage == "Overview")
            RefreshDevelopmentGuidance();
        // Top status is presentation-only. Refresh it last so malformed or
        // incomplete saved numeric data cannot stop research-tree input and
        // visual updates from running in the same frame.
        if (!uiScrolling)
            RefreshTopStatus();
        if (!uiScrolling && populatedPage == "Music")
            RefreshMusicPage();
    }

    private bool IsUiScrolling()
    {
        if (researchGraphGesture != null && researchGraphGesture.IsDragging)
            return true;
        if (pageScroll != null && pageScroll.velocity.sqrMagnitude > 0.01f)
            return true;
        if (detailScroll != null && detailScroll.velocity.sqrMagnitude > 0.01f)
            return true;
        if (musicListScroll != null && musicListScroll.velocity.sqrMagnitude > 0.01f)
            return true;
        return false;
    }

    private void CacheRuntimeManagers()
    {
        if (gameManagerCache == null) gameManagerCache = FindObjectOfType<GameManager>();
        if (researchManagerCache == null) researchManagerCache = FindObjectOfType<ResearchManager>();
        if (resourceManagerCache == null) resourceManagerCache = FindObjectOfType<ResourceManager>();
        if (buildingManagerCache == null) buildingManagerCache = FindObjectOfType<BuildingManager>();
        if (workshopManagerCache == null) workshopManagerCache = FindObjectOfType<WorkshopManager>();
    }

    private bool ShouldRefreshSelectedResource()
    {
        if (selectedResource == null) return false;
        int version = -1;
        if (resourceManagerCache != null && resourceManagerCache.States.TryGetValue(selectedResource, out ResourceState state))
            version = state.Version;
        if (version == lastSelectedResourceVersion) return false;
        lastSelectedResourceVersion = version;
        return true;
    }

    private bool ShouldRefreshSelectedBuilding()
    {
        if (selectedBuilding == null) return false;
        int version = -1;
        BuildingState state = null;
        if (buildingManagerCache != null && buildingManagerCache.States.TryGetValue(selectedBuilding, out state))
            version = state.Version;
        bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
            buildingManagerCache != null && buildingManagerCache.TryGetUnlockedUpgradeTarget(selectedBuilding, out _);
        int resourceVersion = 0;
        if (resourceManagerCache != null)
            for (int i = 0; i < selectedBuilding.ResourceRequirements.Count; i++)
            {
                Resource resource = selectedBuilding.ResourceRequirements[i].First;
                if (resource != null && resourceManagerCache.States.TryGetValue(resource, out ResourceState resourceState))
                    resourceVersion = unchecked(resourceVersion * 31 + resourceState.Version);
            }
        if (version == lastSelectedBuildingVersion && upgrading == lastSelectedBuildingUpgrade &&
            resourceVersion == lastSelectedBuildingResourceVersion)
            return false;
        lastSelectedBuildingVersion = version;
        lastSelectedBuildingUpgrade = upgrading;
        lastSelectedBuildingResourceVersion = resourceVersion;
        return true;
    }

    private void BuildDevelopmentGuidance(RectTransform page)
    {
        if (page == null)
            return;
        developmentGuidanceText = page.Find("PrimaryCard/Text")?.GetComponent<TMP_Text>();
        if (developmentGuidanceText == null)
        {
            Debug.LogError("[王国界面] Overview PrimaryCard/Text is missing; development guidance cannot render.");
            return;
        }
        developmentGuidanceText.enabled = true;
        developmentGuidanceText.gameObject.SetActive(true);
        developmentGuidanceText.alignment = TextAlignmentOptions.TopLeft;
        developmentGuidanceText.fontSize = 24f;
        developmentGuidanceText.enableWordWrapping = true;
        Canvas.ForceUpdateCanvases();
        Vector2 initialRect = developmentGuidanceText.rectTransform.rect.size;
        if (initialRect.x > 0f && initialRect.y > 0f)
            RefreshDevelopmentGuidance();
        else
            Debug.Log($"[王国界面] Development guidance binding deferred until layout: rect={initialRect}");
    }

    private void RefreshDevelopmentGuidance()
    {
        if (developmentGuidanceText == null)
            return;
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        try
        {
            developmentGuidanceSnapshot = DevelopmentGuidance.Build(
                gameManager,
                researchManagerCache,
                resourceManagerCache,
                buildingManagerCache,
                workshopManagerCache);
        }
        catch (Exception exception)
        {
            // Guidance is presentation-only. A partially restored manager
            // must never stop the Overview text from rendering.
            if (!developmentGuidanceErrorLogged)
            {
                developmentGuidanceErrorLogged = true;
                Debug.LogException(exception);
            }
            developmentGuidanceText.text = "当前发展指引\n\n正在读取王国状态，请稍候。";
            developmentGuidanceText.color = TextPrimary;
            return;
        }
        DevelopmentGuidanceSnapshot snapshot = developmentGuidanceSnapshot;
        StringBuilder body = new StringBuilder();
        if (!string.IsNullOrEmpty(snapshot.EraText))
            body.Append(snapshot.EraText).Append("  |  ");
        body.Append(snapshot.Title ?? string.Empty).Append("\n\n");
        body.Append(snapshot.Body ?? string.Empty);
        IReadOnlyList<string> blockers = snapshot.Blockers ?? Array.Empty<string>();
        for (int i = 0; i < blockers.Count && i < 3; i++)
            body.Append("\n- ").Append(blockers[i]);
        string renderedBody = body.ToString();
        if (developmentGuidanceText.text != renderedBody)
        {
            developmentGuidanceText.text = renderedBody;
            developmentGuidanceText.color = TextPrimary;
        }
        if (!developmentGuidanceRuntimeGeometryLogged)
        {
            Vector2 rect = developmentGuidanceText.rectTransform.rect.size;
            if (rect.x > 0f && rect.y > 0f)
            {
                developmentGuidanceRuntimeGeometryLogged = true;
                Debug.Log($"[王国界面] Development guidance rendered after layout: rect={rect}, textLength={developmentGuidanceText.text.Length}");
            }
        }
    }

    private void RefreshTopStatus()
    {
        if (topKingdomTitle == null || topStatus == null)
            return;
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        GameState state = gameManager == null ? null : gameManager.State;

        topKingdomTitle.text = state.KingdomName;
        ExpantaNum populationChange = ExpantaNum.Zero;
        if (gameManager != null)
        {
            if (state.Population.Population < state.Population.PopulationCapacity)
                populationChange = gameManager.CurrentPopulationGrowthRatePerSecond;
            else if (state.Population.Population > state.Population.PopulationCapacity)
                populationChange = -gameManager.CurrentPopulationDepartureRatePerSecond;
        }

        string signedPopulationChange = populationChange >= ExpantaNum.Zero
            ? "+" + populationChange.ToGameString()
            : populationChange.ToGameString();
        string signedFoodChange;
        try
        {
            ExpantaNum foodNetRate = state.FoodNetRate;
            signedFoodChange = foodNetRate >= ExpantaNum.Zero
                ? "+" + foodNetRate.ToGameString()
                : foodNetRate.ToGameString();
        }
        catch (NullReferenceException exception)
        {
            signedFoodChange = string.Empty;
            if (!topStatusDataErrorLogged)
            {
                topStatusDataErrorLogged = true;
                Debug.LogException(exception);
            }
        }
        ResearchManager researchManager = researchManagerCache;
        ResearchState activeResearch = researchManager == null ? null : researchManager.ActiveResearch;
        Research activeDefinition = activeResearch == null ? null : activeResearch.Definition;

        ResearchState defState = null;
        if (activeDefinition)
            defState = researchManager.GetState(activeDefinition);       
        string currentResearch = activeDefinition == null ? "无" 
            : (activeDefinition.Label+ (defState!= null? $"[{ResearchProgressText(defState, defState.Status)}]":""));

        
        bool calendarKnown = researchManager != null && researchManager.IsResearchCompleted("Calendar");
        string calendar = calendarKnown ? GameManager.CalendarDataToString(state.CalendarDays) : "????/??/??";
        string researchPower = researchManager == null ? "0" : researchManager.ResearchPower.ToGameString();
        topStatus.text =
        "科技水平：" + state.TechLevel.GetDescription() +
        "    当前研究：" + currentResearch +
        "    日期：" + calendar +
        "    研究力：" + researchPower + "/s" +
        "\n食物：" + state.FoodAmount.ToGameString() + "/" + state.FoodCapacity.ToGameString() + "（" + signedFoodChange + "/s）" +
        "    生产力：" + BuildingManager.Instance.AvailableProductivity.ToGameString() + "/" + BuildingManager.Instance.TotalProductivity.ToGameString() +
        "    幸福度：" + state.HappinessScore.ToGameString() + $"({state.HappinessMultiplier.ToGameString()}x）" +
        "\n人口：" + state.Population.Population.ToGameString() + "/" + state.Population.PopulationCapacity.ToGameString() + "（" + signedPopulationChange + "/s）" +
        "    领土：" + state.AvailableTerritory.ToGameString() + "/" + state.TerritoryTotal.ToGameString();
            
    }

    private void RefreshLiveCardValues()
    {
        if (BuildingManager.Instance != null)
            foreach (KeyValuePair<Building, TMP_Text> pair in buildingAmountLabels)
                if (pair.Value != null)
                    if (BuildingManager.Instance.States.TryGetValue(pair.Key, out BuildingState state))
                    {
                        SetTextIfChanged(pair.Value, state.Amount.ToGameString());
                        bool hasBuildingAmount = state.Amount > ExpantaNum.Zero;
                        if (buildingDeconstructButtons.TryGetValue(pair.Key, out Button deconstructButton) && deconstructButton != null)
                        {
                            SetBuildingActionButtonText(
                                deconstructButton,
                                "\u62c6\u9664x" + GetSelectedBuildingQuantity(pair.Key, false, true).ToGameString());
                            SetBuildingActionButtonState(deconstructButton, hasBuildingAmount);
                        }
                        else if (buildingDeconstructSurfaces.TryGetValue(pair.Key, out Image deconstructSurface) && deconstructSurface != null)
                            SetColorIfChanged(deconstructSurface, hasBuildingAmount ? Error : Panel);
                        if (buildingActionButtons.TryGetValue(pair.Key, out Button actionButton) && actionButton != null &&
                            buildingActionUpgradeModes.TryGetValue(pair.Key, out bool upgrade))
                        {
                            SetBuildingActionButtonText(
                                actionButton,
                                (upgrade ? "\u5347\u7ea7x" : "\u5efa\u9020x") +
                                GetSelectedBuildingQuantity(pair.Key, upgrade, false).ToGameString());
                            SetBuildingActionButtonState(actionButton, CanPerformBuildingAction(pair.Key, upgrade));
                        }
                    else
                        SetTextIfChanged(pair.Value, "0");
        }
        if (ResourceManager.Instance == null)
            return;
        foreach (KeyValuePair<Resource, TMP_Text> pair in resourceAmountLabels)
        {
            if (!ResourceManager.Instance.States.TryGetValue(pair.Key, out ResourceState state))
                continue;
            if (pair.Value != null)
                SetTextIfChanged(pair.Value, state.Amount.ToGameString());
            if (resourceChangeLabels.TryGetValue(pair.Key, out TMP_Text changeLabel) && changeLabel != null)
            {
                ExpantaNum net = state.ProductionRate - state.ConsumptionRate;
                SetTextIfChanged(changeLabel, (net >= ExpantaNum.Zero ? "+" : string.Empty) + net.ToGameString() + "/s");
                SetColorIfChanged(changeLabel, net >= ExpantaNum.Zero ? Positive : Error);
            }
        }
    }
}
