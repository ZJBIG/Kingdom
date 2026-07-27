using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildingViewer : MonoBehaviour, IGameUIRefreshable
{
    private const int GridColumns = 2;
    private const float CardWidth = 383f;
    private const float CardHeight = 150f;
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

        BuildingDisplayer displayer = Instantiate(DisplayerPrefab, Content, false).GetComponent<BuildingDisplayer>();
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

        int index = 0;
        foreach (BuildingDisplayer displayer in displayers.Values)
        {
            RectTransform card = displayer.transform as RectTransform;
            if (card == null)
                continue;

            int column = index % GridColumns;
            int row = index / GridColumns;
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(
                column * (CardWidth + GridSpacing),
                -row * (CardHeight + GridSpacing));
            index++;
        }

        int rows = (index + GridColumns - 1) / GridColumns;
        Content.sizeDelta = new Vector2(
            GridColumns * CardWidth + (GridColumns - 1) * GridSpacing,
            Mathf.Max(CardHeight, rows * CardHeight + Mathf.Max(0, rows - 1) * GridSpacing));
    }
}
