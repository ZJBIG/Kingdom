using System;
using TMPro;
using UnityEngine;

public sealed class GameHudViewer : MonoBehaviour, IGameUIRefreshable
{
    private const string ResourceTabLabel = "资源";
    private const string BuildingTabLabel = "建筑";
    private const string ResearchTabLabel = "研究";

    [SerializeField] private TMP_Text Text_Calendar;
    [SerializeField] private TMP_Text Text_TechLevel;
    [SerializeField] private TMP_Text Text_Food;
    [SerializeField] private TMP_Text Text_KingdomName;
    [SerializeField] private TMP_Text Text_Productivity;
    [SerializeField] private TMP_Text Text_KingdomSpace;
    [SerializeField] private TMP_Text Text_TopText;

    private void OnEnable()
    {
        GameUIRefreshManager.Instance?.Register(this);
        RefreshUI();
    }

    private void Start()
    {
        // Retry after the scene-wide refresh manager has completed Awake.
        GameUIRefreshManager.Instance?.Register(this);
        RefreshUI();
    }

    private void OnDisable()
    {
        GameUIRefreshManager.Instance?.Unregister(this);
    }

    public void SetMainTab(MainTab tab)
    {
        if (Text_TopText == null)
            return;

        Text_TopText.text = tab switch
        {
            MainTab.Building => BuildingTabLabel,
            MainTab.Research => ResearchTabLabel,
            MainTab.Workshop => "Workshop",
            _ => ResourceTabLabel
        };
    }

    public void Refresh(GameState state)
    {
        if (state == null)
            return;

        bool calendarKnown = ResearchManager.Instance.IsResearchCompleted("Calendar");

        SetTextIfChanged(Text_Calendar, calendarKnown ? GameManager.CalendarDataToString(state.CalendarDays) : "????/??/??");

        SetTextIfChanged(Text_TechLevel, $"技术等级:{state.TechLevel.GetDescription()}");

        string signedFoodRate = state.FoodNetRate >= ExpantaNum.Zero
            ? "+" + state.FoodNetRate.ToGameString()
            : state.FoodNetRate.ToGameString();

        SetTextIfChanged(Text_Food, $"粮食:{state.FoodAmount.ToGameString()}/{state.FoodCapacity.ToGameString()}   {signedFoodRate}/s");

        SetTextIfChanged(Text_KingdomName, state.KingdomName);

        ResearchManager researchManager = FindObjectOfType<ResearchManager>();
        string researchPower = researchManager == null
            ? ExpantaNum.One.ToGameString()
            : researchManager.ResearchPower.ToGameString();

        string foodEfficiency = state.FoodSatisfaction < ExpantaNum.One
            ? $"   Food Limit: {state.FoodSatisfaction.ToGameString()}"
            : string.Empty;

        GameManager gameManager = GameManager.Instance;
        ExpantaNum populationGrowth = gameManager == null
            ? ExpantaNum.Zero
            : gameManager.CurrentPopulationGrowthRatePerMinute;
        ExpantaNum populationDeparture = gameManager == null
            ? ExpantaNum.Zero
            : gameManager.CurrentPopulationDepartureRatePerMinute;

        string populationChangeRate;
        if (state.Population.Population < state.Population.PopulationCapacity)
            populationChangeRate = $"   人口增长:+{populationGrowth.ToGameString()}/min   ";
        else if (state.Population.Population > state.Population.PopulationCapacity)
            populationChangeRate = $"   人口减少:-{populationDeparture.ToGameString()}/min   ";
        else
            populationChangeRate = "   人口增长:0/min   ";

        SetTextIfChanged(Text_Productivity,
            "人口变化" + populationChangeRate +
            $"人口:{state.Population.Population.ToGameString()}/{state.Population.PopulationCapacity.ToGameString()}   " +
            $"生产力:{BuildingManager.Instance.AvailableProductivity.ToGameString()}/{BuildingManager.Instance.TotalProductivity.ToGameString()}   " +
            $"研究力:{researchPower}/s{foodEfficiency}");

        SetTextIfChanged(Text_KingdomSpace, $"领土:{state.AvailableTerritory.ToGameString()}/{state.TerritoryTotal.ToGameString()}");
    }

    public void RefreshUI() => Refresh(GameManager.Instance.State);

    private static void SetTextIfChanged(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
            target.text = value;
    }
}
