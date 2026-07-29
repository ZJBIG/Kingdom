using System.Collections.Generic;
using UnityEngine;

public class ResourceViewer : MonoBehaviour, IGameUIRefreshable
{
    [SerializeField] private RectTransform Content;
    [SerializeField] private GameObject DisplayerPrefab;

    private readonly Dictionary<Resource, ResourceDisplayer> displayers = new();
    private readonly Dictionary<Resource.Set, ResourceDisplayerSet> sets = new();
    private ResourceManager resourceManager;

    private void Awake()
    {

        ResourceDisplayerSet[] foundSets = GetComponentsInChildren<ResourceDisplayerSet>(true);
        for (int i = 0; i < foundSets.Length; i++)
            if (System.Enum.TryParse(foundSets[i].name, out Resource.Set set) &&!sets.ContainsKey(set))
                sets.Add(set, foundSets[i]);
    }

    private void OnEnable()
    {
        TryBindResourceManager();
        GameUIRefreshManager.Instance?.Register(this);
        RefreshAll();
    }

    private void Start()
    {
        // Bootstrap managers may be created after this viewer is enabled.
        TryBindResourceManager();
        GameUIRefreshManager.Instance?.Register(this);
        RefreshAll();
    }

    private void TryBindResourceManager()
    {
        if (resourceManager == null)
            resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
            return;

        resourceManager.ResourceStateAdded -= OnResourceStateAdded;
        resourceManager.ResourceStateChanged -= OnResourceStateChanged;
        resourceManager.ResourceStateAdded += OnResourceStateAdded;
        resourceManager.ResourceStateChanged += OnResourceStateChanged;
        BindExistingStates();
    }

    private void OnDisable()
    {
        if (resourceManager != null)
        {
            resourceManager.ResourceStateAdded -= OnResourceStateAdded;
            resourceManager.ResourceStateChanged -= OnResourceStateChanged;
        }
        GameUIRefreshManager.Instance?.Unregister(this);
        resourceManager = null;
    }

    public void RefreshUI() => RefreshAll();

    private void BindExistingStates()
    {
        foreach (ResourceState state in resourceManager.States.Values)
            OnResourceStateAdded(state);
    }

    private void OnResourceStateAdded(ResourceState state)
    {
        if (state == null || !IsResourceManufacturable(state.Definition))
            return;
        CreateDisplayer(state);
    }

    private void CreateDisplayer(ResourceState state)
    {
        if (state == null || displayers.ContainsKey(state.Definition))
            return;
        if (!sets.TryGetValue(state.Definition.DisplayerSet, out ResourceDisplayerSet set) ||
            set == null ||
            set.ContentTransform == null ||
            DisplayerPrefab == null)
        {
            Debug.LogWarning($"Cannot create resource UI for '{state.Definition.Id}'. Missing ResourceViewer set or prefab.");
            return;
        }

        ResourceDisplayer displayer = Instantiate(
                DisplayerPrefab,
                set.ContentTransform,
                false)
            .GetComponent<ResourceDisplayer>();
        displayer.Bind(state);
        displayers.Add(state.Definition, displayer);
        set.AddDisplayer(state.Definition, displayer);
        set.gameObject.SetActive(true);
        set.RefreshLayout();
    }

    private void OnResourceStateChanged(ResourceState state)
    {
        if (state != null && displayers.TryGetValue(state.Definition, out ResourceDisplayer displayer))
            displayer.Refresh();
    }

    public void RefreshAll()
    {
        if (resourceManager == null)
            TryBindResourceManager();
        if (resourceManager == null)
            return;

        foreach (ResourceState state in resourceManager.States.Values)
        {
            bool manufacturable = IsResourceManufacturable(state.Definition);
            if (manufacturable)
                CreateDisplayer(state);

            if (!displayers.TryGetValue(state.Definition, out ResourceDisplayer displayer))
                continue;

            if (sets.TryGetValue(state.Definition.DisplayerSet, out ResourceDisplayerSet set))
                set.SetVisible(state.Definition, manufacturable);
            if (manufacturable)
                displayer.Refresh();
        }

        foreach (ResourceDisplayerSet set in sets.Values)
        {
            bool hasVisibleDisplayer = false;
            foreach (ResourceDisplayer displayer in set.Displayers.Values)
            {
                if (displayer != null && displayer.gameObject.activeSelf)
                {
                    hasVisibleDisplayer = true;
                    break;
                }
            }
            set.gameObject.SetActive(hasVisibleDisplayer);
            set.RefreshLayout();
        }
    }

    private static bool IsResourceManufacturable(Resource resource)
    {
        if (resource == null)
            return false;
        if (ResourceManager.IsStartingResource(resource))
            return true;
        if (GameManager.Instance == null || BuildingManager.Instance == null)
            return false;

        IReadOnlyList<Building> buildings = DataBase<Building>.All;
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building == null)
                continue;

            IReadOnlyList<Pair<Resource, ExpantaNum>> generation =
                building.ResourceGenerationRates;
            for (int j = 0; j < generation.Count; j++)
            {
                Pair<Resource, ExpantaNum> output = generation[j];
                if (output.First != resource || output.Second <= ExpantaNum.Zero)
                    continue;

                if (BuildingManager.Instance.ArePrerequisitesMet(building, out _))
                    return true;
            }
        }

        return false;
    }
}
