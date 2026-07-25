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
        resourceManager = ResourceManager.Instance;
        if (resourceManager != null)
        {
            resourceManager.ResourceStateAdded += OnResourceStateAdded;
            resourceManager.ResourceStateChanged += OnResourceStateChanged;
            BindExistingStates();
        }
        GameUIRefreshManager.Instance?.Register(this);
        RefreshAll();
    }

    private void Start()
    {
        // OnEnable can run before the scene-wide refresh manager has awakened.
        // Retry registration after all scene Awake methods have completed.
        GameUIRefreshManager.Instance?.Register(this);
        RefreshAll();
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
        foreach (ResourceDisplayer displayer in displayers.Values)
            displayer.Refresh();

        foreach (ResourceDisplayerSet set in sets.Values)
        {
            set.gameObject.SetActive(set.Displayers.Count != 0);
            set.RefreshLayout();
        }
    }
}
