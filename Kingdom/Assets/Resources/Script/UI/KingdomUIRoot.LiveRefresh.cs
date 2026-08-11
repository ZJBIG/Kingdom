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

    public void RefreshUI()
    {
        EnsureRuntimeCanvasGeometry();
        RefreshLiveCardValues();
        RefreshResearchQueueToolbar();
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
        RefreshDevelopmentGuidance();
        // Top status is presentation-only. Refresh it last so malformed or
        // incomplete saved numeric data cannot stop research-tree input and
        // visual updates from running in the same frame.
        RefreshTopStatus();
        if (populatedPage == "Music")
            RefreshMusicPage();
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
        GameManager gameManager = FindObjectOfType<GameManager>();
        try
        {
            developmentGuidanceSnapshot = DevelopmentGuidance.Build(
                gameManager,
                FindObjectOfType<ResearchManager>(),
                FindObjectOfType<ResourceManager>(),
                FindObjectOfType<BuildingManager>(),
                FindObjectOfType<WorkshopManager>());
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
        developmentGuidanceText.text = body.ToString();
        developmentGuidanceText.color = TextPrimary;
        Canvas.ForceUpdateCanvases();
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
        GameManager gameManager = FindObjectOfType<GameManager>();
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
        ResearchManager researchManager = FindObjectOfType<ResearchManager>();
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
                        pair.Value.text = state.Amount.ToGameString();
                        bool hasBuildingAmount = state.Amount > ExpantaNum.Zero;
                        if (buildingDeconstructButtons.TryGetValue(pair.Key, out Button deconstructButton) && deconstructButton != null)
                            SetBuildingActionButtonState(deconstructButton, hasBuildingAmount);
                        else if (buildingDeconstructSurfaces.TryGetValue(pair.Key, out Image deconstructSurface) && deconstructSurface != null)
                            deconstructSurface.color = hasBuildingAmount ? Error : Panel;
                        if (buildingActionButtons.TryGetValue(pair.Key, out Button actionButton) && actionButton != null &&
                            buildingActionUpgradeModes.TryGetValue(pair.Key, out bool upgrade))
                        {
                            SetBuildingActionButtonText(actionButton, upgrade ? "\u5347\u7ea7" : "\u5efa\u9020");
                            SetBuildingActionButtonState(actionButton, CanPerformBuildingAction(pair.Key, upgrade));
                        }
                    else
                        pair.Value.text = "0";
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
