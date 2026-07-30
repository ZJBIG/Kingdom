using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildingViewer : MonoBehaviour, IGameUIRefreshable
{
    private const float CardWidth = 383f;
    private const float CollapsedCardHeight = BuildingDisplayer.HeaderHeight;
    private const float GridSpacing = 12f;

    [SerializeField] private RectTransform Content;
    [SerializeField] private GameObject DisplayerPrefab;

    private readonly Dictionary<Building, BuildingDisplayer> displayers = new();
    private BuildingManager buildingManager;

    private void Awake()
    {
        // Building cards use the same explicit top-origin positioning model
        // as ResourceDisplayerSet. Automatic layout would overwrite the
        // card positions and preferred heights during every canvas rebuild.


        //DisableAutomaticContentLayout();
    }

    private void OnEnable()
    {
        TryBindBuildingManager();
        GameUIRefreshManager.Instance?.Register(this);
        RefreshAll();
    }

    private void Start()
    {
        // GameBootstrap creates managers in Start, so this viewer may be
        // enabled before BuildingManager exists. Retry after all Start
        // methods have run and make the periodic refresh self-healing.
        TryBindBuildingManager();
        GameUIRefreshManager.Instance?.Register(this);
        RefreshAll();
    }

    private void OnDisable()
    {
        if (buildingManager != null)
            buildingManager.BuildingStateAdded -= OnBuildingStateAdded;
        GameUIRefreshManager.Instance?.Unregister(this);
        buildingManager = null;
    }

    public void RefreshUI() => RefreshAll();
    public void RefreshLayout() => RefreshGridLayout();

    private void TryBindBuildingManager()
    {
        BuildingManager candidate = FindObjectOfType<BuildingManager>();
        if (candidate == null)
            return;

        if (buildingManager == candidate)
            return;

        if (buildingManager != null)
            buildingManager.BuildingStateAdded -= OnBuildingStateAdded;

        buildingManager = candidate;
        buildingManager.BuildingStateAdded += OnBuildingStateAdded;
        BindExistingStates();
    }

    private void BindExistingStates()
    {
        foreach (BuildingState state in buildingManager.States.Values)
            OnBuildingStateAdded(state);
    }

    private void OnBuildingStateAdded(BuildingState state)
    {
        if (state == null || displayers.ContainsKey(state.Definition))
            return;
        if (!ShouldDisplayCard(state.Definition))
            return;
        if (Content == null || DisplayerPrefab == null)
        {
            Debug.LogWarning($"Cannot create building UI for '{state.Definition.Id}'. Missing BuildingViewer content or prefab.");
            return;
        }

        GameObject instance = Instantiate(DisplayerPrefab, Content, false);
        BuildingDisplayer displayer = instance.GetComponent<BuildingDisplayer>();
        if (displayer == null)
        {
            Debug.LogError(
                $"BuildingViewer prefab '{DisplayerPrefab.name}' must have a BuildingDisplayer component on its root.",
                DisplayerPrefab);
            Destroy(instance);
            return;
        }
        displayer.Bind(state);
        displayers.Add(state.Definition, displayer);
        RefreshGridLayout();
    }

    public void RefreshAll()
    {
        TryBindBuildingManager();
        if (buildingManager == null)
            return;

        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Building definition = definitions[i];
            if (ShouldDisplayCard(definition))
                OnBuildingStateAdded(buildingManager.EnsureBuilding(definition));
        }

        foreach (BuildingDisplayer displayer in displayers.Values)
        {
            bool shouldDisplay = ShouldDisplayCard(displayer.Building);
            if (displayer.gameObject.activeSelf != shouldDisplay)
                displayer.gameObject.SetActive(shouldDisplay);
            displayer.Refresh();
        }
        RefreshGridLayout();
    }

    private bool ShouldDisplayCard(Building building)
    {
        if (buildingManager.ShouldDisplay(building))
            return true;

        // BuildingManager determines whether a definition is eligible. The
        // lower-tier hide rule is UI-specific: it must depend on the
        // successor card actually existing and being active, not merely on
        // the successor's prerequisites becoming true.
        if (building == null || building.UpgradeTo == null ||
            !buildingManager.ArePrerequisitesMet(building, out _))
        {
            return false;
        }

        if (buildingManager.States.TryGetValue(building, out BuildingState state) &&
            state.Amount > ExpantaNum.Zero)
        {
            return true;
        }

        return !displayers.TryGetValue(building.UpgradeTo, out BuildingDisplayer successor) ||
            !successor.gameObject.activeSelf;
    }

    private void RefreshGridLayout()
    {
        if (Content == null)
            return;

        List<BuildingDisplayer> layoutCards = new(displayers.Count);
        List<float> rowHeights = new();
        foreach (BuildingDisplayer displayer in displayers.Values)
        {
            if (!displayer.gameObject.activeSelf)
                continue;

            RectTransform card = displayer.transform as RectTransform;
            if (card == null)
                continue;

            rowHeights.Add(Mathf.Max(CollapsedCardHeight, displayer.PreferredHeight));
            layoutCards.Add(displayer);
        }

        float nextRowTop = 0f;
        for (int index = 0; index < layoutCards.Count; index++)
        {
            BuildingDisplayer displayer = layoutCards[index];
            RectTransform card = displayer.transform as RectTransform;
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(0f, -nextRowTop);
            nextRowTop += rowHeights[index] + GridSpacing;
        }

        float contentHeight = layoutCards.Count == 0
            ? CollapsedCardHeight
            : nextRowTop - GridSpacing;
        Content.sizeDelta = new Vector2(
            CardWidth,
            Mathf.Max(CollapsedCardHeight, contentHeight));
    }

    private void DisableAutomaticContentLayout()
    {
        if (Content == null)
            return;

        LayoutGroup layoutGroup = Content.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
            layoutGroup.enabled = false;

        // The cards are positioned explicitly below, just like the resource
        // list. A ContentSizeFitter would calculate the scroll content from
        // the prefab's stale preferred height and collapse expanded cards
        // back into the 50/100 pixel row.
        ContentSizeFitter fitter = Content.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;
    }
}
