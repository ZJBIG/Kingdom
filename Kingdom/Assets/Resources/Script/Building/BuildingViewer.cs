using System.Collections.Generic;
using UnityEngine;

public class BuildingViewer : MonoBehaviour, IGameUIRefreshable
{
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
        if (Content == null || DisplayerPrefab == null)
        {
            Debug.LogWarning($"Cannot create building UI for '{state.Definition.Id}'. Missing BuildingViewer content or prefab.");
            return;
        }

        BuildingDisplayer displayer = Instantiate(DisplayerPrefab, Content, false)
            .GetComponent<BuildingDisplayer>();
        displayer.Bind(state);
        displayers.Add(state.Definition, displayer);
    }

    public void RefreshAll()
    {
        foreach (BuildingDisplayer displayer in displayers.Values)
            displayer.Refresh();
    }
}
