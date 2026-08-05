using System;
using System.Collections.Generic;
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

    public void RefreshUI()
    {
        EnsureRuntimeCanvasGeometry();
        RefreshLiveCardValues();
        RefreshBuildingQuantityHeader();
        bool researchGraphDragging = researchGraphGesture != null && researchGraphGesture.IsDragging;
        if (!researchGraphDragging)
            RefreshResearchTreeVisuals();
        if (selectedResource != null)
            RefreshResourceDetails(selectedResource);
        else if (selectedBuilding != null)
            RefreshSelectedBuildingDetails(selectedBuilding);
        else if (!researchGraphDragging && selectedResearchNode != null && populatedPage == "Research")
            RefreshSelectedResearchDetails(selectedResearchNode);
        // Top status is presentation-only. Refresh it last so malformed or
        // incomplete saved numeric data cannot stop research-tree input and
        // visual updates from running in the same frame.
        RefreshTopStatus();
        if (populatedPage == "Music")
            RefreshMusicPage();
    }

    private void RefreshTopStatus()
    {
        if (topKingdomTitle == null || topStatus == null)
            return;
        GameManager gameManager = FindObjectOfType<GameManager>();
        GameState state = gameManager == null ? null : gameManager.State;
        if (state == null)
        {
            topKingdomTitle.text = "王国";
            topStatus.text = "食物：0/0（0/s）    人口：0/0（0/min）    领土：0/0\n科技水平：未知    当前研究：无    日历：????/??/??";
            return;
        }

        topKingdomTitle.text = "王国 / " + state.KingdomName;
        ExpantaNum populationChange = ExpantaNum.Zero;
        if (gameManager != null)
        {
            if (state.Population.Population < state.Population.PopulationCapacity)
                populationChange = gameManager.CurrentPopulationGrowthRatePerMinute;
            else if (state.Population.Population > state.Population.PopulationCapacity)
                populationChange = -gameManager.CurrentPopulationDepartureRatePerMinute;
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
        ResearchManager researchManager = FindObjectOfType<ResearchManager>();
        ResearchState activeResearch = researchManager == null ? null : researchManager.ActiveResearch;
        Research activeDefinition = activeResearch == null ? null : activeResearch.Definition;
        string currentResearch = activeDefinition == null ? "无" : activeDefinition.Label;
        bool calendarKnown = researchManager != null && researchManager.IsResearchCompleted("Calendar");
        string calendar = calendarKnown ? GameManager.CalendarDataToString(state.CalendarDays) : "????/??/??";
        topStatus.text = "食物：" + state.FoodAmount.ToGameString() + "/" + state.FoodCapacity.ToGameString() + "（" + signedFoodChange + "/s）" +
            "    人口：" + state.Population.Population.ToGameString() + "/" + state.Population.PopulationCapacity.ToGameString() + "（" + signedPopulationChange + "/min）" +
            "    领土：" + state.AvailableTerritory.ToGameString() + "/" + state.TerritoryTotal.ToGameString() +
            "\n科技水平：" + state.TechLevel.GetDescription() + "    当前研究：" + currentResearch + "    日历：" + calendar;
    }

    private void RefreshLiveCardValues()
    {
        if (BuildingManager.Instance != null)
        {
            foreach (KeyValuePair<Building, TMP_Text> pair in buildingAmountLabels)
            {
                if (pair.Value != null)
                {
                    if (BuildingManager.Instance.States.TryGetValue(pair.Key, out BuildingState state))
                    {
                        pair.Value.text = state.Amount.ToGameString();
                        if (buildingDeconstructSurfaces.TryGetValue(pair.Key, out Image deconstructSurface) && deconstructSurface != null)
                            deconstructSurface.color = state.Amount > ExpantaNum.Zero ? Error : Panel;
                        if (buildingActionButtons.TryGetValue(pair.Key, out Button actionButton) && actionButton != null &&
                            buildingActionUpgradeModes.TryGetValue(pair.Key, out bool upgrade))
                        {
                            SetBuildingActionButtonText(actionButton, upgrade ? "\u5347\u7ea7" : "\u5efa\u9020");
                            SetBuildingActionButtonState(actionButton, CanPerformBuildingAction(pair.Key, upgrade));
                        }
                    }
                    else
                    {
                        pair.Value.text = "0";
                    }
                }
            }
        }
        if (ResourceManager.Instance == null)
            return;
        foreach (KeyValuePair<Resource, TMP_Text> pair in resourceAmountLabels)
        {
            if (!ResourceManager.Instance.States.TryGetValue(pair.Key, out ResourceState state))
                continue;
            if (pair.Value != null)
                pair.Value.text = state.Amount.ToGameString();
            if (resourceChangeLabels.TryGetValue(pair.Key, out TMP_Text changeLabel) && changeLabel != null)
            {
                ExpantaNum net = state.ProductionRate - state.ConsumptionRate;
                changeLabel.text = (net >= ExpantaNum.Zero ? "+" : string.Empty) + net.ToGameString() + "/s";
                changeLabel.color = net >= ExpantaNum.Zero ? Positive : Error;
            }
        }
    }
}
