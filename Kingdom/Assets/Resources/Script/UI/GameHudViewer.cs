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
        if (Text_Calendar != null)
            SetTextIfChanged(Text_Calendar, calendarKnown ? GameManager.CalendarDataToString(state.CalendarDays) : "????/??/??");
        if (Text_TechLevel != null)
            SetTextIfChanged(Text_TechLevel, $"技术等级:{state.TechLevel.GetDescription()}");

        ExpantaNum populationFoodConsumption = state.Population.Population * state.Population.FoodPerPerson;
        ExpantaNum netFoodRate = state.FoodProductionRate - state.FoodConsumptionRate - populationFoodConsumption;
        string signedFoodRate = netFoodRate >= ExpantaNum.Zero
            ? "+" + netFoodRate.ToGameString()
            : netFoodRate.ToGameString();
        if (Text_Food != null)
            SetTextIfChanged(Text_Food,$"粮食:{state.FoodAmount.ToGameString()}/{state.FoodCapacity.ToGameString()}   {signedFoodRate}/s");
        if (Text_KingdomName != null)
            SetTextIfChanged(Text_KingdomName, state.KingdomName);
        if (Text_Productivity != null)
            SetTextIfChanged(
                Text_Productivity,
                $"劳动力:{BuildingManager.Instance.AvailableWorkforce.ToGameString()}");
        if (Text_Productivity != null)
        {
            ResearchManager researchManager = FindObjectOfType<ResearchManager>();
            string researchPower = researchManager == null
                ? ExpantaNum.One.ToGameString()
                : researchManager.ResearchPower.ToGameString();
            string foodEfficiency = state.FoodSatisfaction < ExpantaNum.One
                ? $"   Food Limit: {state.FoodSatisfaction.ToGameString()}"
                : string.Empty;
            SetTextIfChanged(
                Text_Productivity,
                $"劳动力:{BuildingManager.Instance.AvailableWorkforce.ToGameString()}   研究力:{researchPower}/s{foodEfficiency}");
        }
        if (Text_Productivity != null)
            SetTextIfChanged(
                Text_Productivity,
                $"Population: {state.Population.Population.ToGameString()}/{state.Population.PopulationCapacity.ToGameString()}   " +
                $"Workforce: {BuildingManager.Instance.AvailableWorkforce.ToGameString()}");
        if (Text_KingdomSpace != null)
            SetTextIfChanged(
                Text_KingdomSpace,
                $"领土:{state.AvailableTerritory.ToGameString()}/{state.TerritoryTotal.ToGameString()}");
    }

    public void RefreshUI() => Refresh(GameManager.Instance.State);

    private static void SetTextIfChanged(TMP_Text target, string value)
    {
        if (target.text != value)
            target.text = value;
    }
}
