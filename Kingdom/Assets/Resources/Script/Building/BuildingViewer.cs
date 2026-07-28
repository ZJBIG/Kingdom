using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildingViewer : MonoBehaviour, IGameUIRefreshable
{
    private const int GridColumns = 2;
    private const float CardWidth = 383f;
    private const float CollapsedCardHeight = 100f;
    private const float GridSpacing = 12f;

    [SerializeField] private RectTransform Content;
    [SerializeField] private GameObject DisplayerPrefab;

    private readonly Dictionary<Building, BuildingDisplayer> displayers = new();
    private BuildingManager buildingManager;

    private void OnEnable()
    {
        buildingManager = BuildingManager.Instance;
        if (buildingManager != null)
        {
            buildingManager.BuildingStateAdded += OnBuildingStateAdded;
            BindExistingStates();
        }
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

    private void BindExistingStates()
    {
        foreach (BuildingState state in buildingManager.States.Values)
            OnBuildingStateAdded(state);
    }

    private void OnBuildingStateAdded(BuildingState state)
    {
        if (state == null || displayers.ContainsKey(state.Definition))
            return;
        if (!buildingManager.ArePrerequisitesMet(state.Definition, out _))
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
        if (buildingManager == null)
            return;

        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Building definition = definitions[i];
            if (buildingManager.ArePrerequisitesMet(definition, out _))
                OnBuildingStateAdded(buildingManager.EnsureBuilding(definition));
        }

        foreach (BuildingDisplayer displayer in displayers.Values)
            displayer.Refresh();
        RefreshGridLayout();
    }

    private void RefreshGridLayout()
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

        List<BuildingDisplayer> ordered = new(displayers.Values);
        int index = 0;
        List<float> rowHeights = new();
        foreach (BuildingDisplayer displayer in ordered)
        {
            RectTransform card = displayer.transform as RectTransform;
            if (card == null)
                continue;

            int column = index % GridColumns;
            int row = index / GridColumns;
            while (rowHeights.Count <= row)
                rowHeights.Add(CollapsedCardHeight);
            rowHeights[row] = Mathf.Max(rowHeights[row], displayer.PreferredHeight);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            float y = 0f;
            for (int previous = 0; previous < row; previous++)
                y += rowHeights[previous] + GridSpacing;
            card.anchoredPosition = new Vector2(column * (CardWidth + GridSpacing), -y);
            index++;
        }

        int rows = (index + GridColumns - 1) / GridColumns;
        float contentHeight = 0f;
        for (int row = 0; row < rows; row++)
            contentHeight += rowHeights[row] + (row == 0 ? 0f : GridSpacing);
        Content.sizeDelta = new Vector2(
            GridColumns * CardWidth + (GridColumns - 1) * GridSpacing,
            Mathf.Max(CollapsedCardHeight, contentHeight));
    }
}
