using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public enum SectorAccessStatus
{
    Locked,
    Available,
    Unlocked,
    Occupied
}

public sealed class SectorMapViewer : MonoBehaviour, IGameUIRefreshable
{
    [SerializeField] private RectTransform content;
    [SerializeField] private GameObject sectorNodePrefab;
    [SerializeField] private TMP_Text detailText;

    private readonly Dictionary<SectorDefinition, SectorState> states = new();
    private readonly Dictionary<SectorDefinition, SectorNodeView> displayers = new();
    private readonly List<SectorDefinition> orderedDefinitions = new();
    private SectorDefinition selectedSector;
    private int selectedVersion = -1;
    private bool warnedMissingPrefab;

    public IReadOnlyDictionary<SectorDefinition, SectorState> States => states;
    public IReadOnlyList<SectorDefinition> OrderedDefinitions => orderedDefinitions;
    public SectorDefinition SelectedSector => selectedSector;

    private void OnEnable()
    {
        BindDefinitions();
        GameUIRefreshManager.Instance?.Register(this);
        RefreshUI();
    }

    private void OnDisable() => GameUIRefreshManager.Instance?.Unregister(this);

    public void RefreshUI()
    {
        for (int i = 0; i < orderedDefinitions.Count; i++)
        {
            SectorDefinition definition = orderedDefinitions[i];
            if (displayers.TryGetValue(definition, out SectorNodeView node))
                node.Refresh();
        }

        RefreshDetails();
    }

    public void SelectSector(SectorDefinition definition)
    {
        if (definition == null || !states.ContainsKey(definition))
            return;

        selectedSector = definition;
        selectedVersion = -1;
        RefreshDetails();
    }

    public SectorState GetState(SectorDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        return states[definition];
    }

    public SectorAccessStatus GetAccessStatus(SectorDefinition definition)
    {
        if (definition == null || !states.TryGetValue(definition, out SectorState state))
            return SectorAccessStatus.Locked;
        return GetAccessStatus(state, states);
    }

    public static SectorAccessStatus GetAccessStatus(
        SectorState state,
        IReadOnlyDictionary<SectorDefinition, SectorState> knownStates)
    {
        if (state == null)
            return SectorAccessStatus.Locked;
        if (state.Occupied)
            return SectorAccessStatus.Occupied;
        if (state.Unlocked)
            return SectorAccessStatus.Unlocked;

        IReadOnlyList<SectorDefinition> prerequisites = state.Definition.PrerequisiteSectors;
        if (prerequisites == null || prerequisites.Count == 0)
            return SectorAccessStatus.Available;

        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (knownStates == null ||
                !knownStates.TryGetValue(prerequisites[i], out SectorState prerequisite) ||
                !prerequisite.Occupied)
                return SectorAccessStatus.Locked;
        }

        return SectorAccessStatus.Available;
    }

    private void BindDefinitions()
    {
        IReadOnlyList<SectorDefinition> definitions = DataBase<SectorDefinition>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            SectorDefinition definition = definitions[i];
            if (definition == null || states.ContainsKey(definition))
                continue;

            states.Add(definition, new SectorState(definition));
            orderedDefinitions.Add(definition);
        }

        orderedDefinitions.Sort(CompareDefinitions);
        CreateMissingNodes();
        if (selectedSector == null && orderedDefinitions.Count > 0)
            selectedSector = orderedDefinitions[0];
    }

    private void CreateMissingNodes()
    {
        if (content == null || sectorNodePrefab == null)
        {
            if (!warnedMissingPrefab && sectorNodePrefab == null)
            {
                Debug.LogWarning("SectorMapViewer has no sector node prefab; data binding remains active.");
                warnedMissingPrefab = true;
            }
            return;
        }

        for (int i = 0; i < orderedDefinitions.Count; i++)
        {
            SectorDefinition definition = orderedDefinitions[i];
            if (displayers.ContainsKey(definition))
                continue;

            SectorNodeView node = Instantiate(sectorNodePrefab, content, false)
                .GetComponent<SectorNodeView>();
            if (node == null)
            {
                Debug.LogError("Sector node prefab is missing SectorNodeView.");
                continue;
            }

            node.Bind(states[definition], SelectSector);
            RectTransform rect = node.transform as RectTransform;
            if (rect != null)
                rect.anchoredPosition = new Vector2(definition.MapX, definition.MapY);
            displayers.Add(definition, node);
        }
    }

    private void RefreshDetails()
    {
        if (detailText == null || selectedSector == null || !states.TryGetValue(selectedSector, out SectorState state))
            return;
        if (selectedVersion == state.Version)
            return;

        selectedVersion = state.Version;
        StringBuilder builder = new();
        builder.AppendLine(string.IsNullOrWhiteSpace(selectedSector.Label)
            ? selectedSector.Id
            : selectedSector.Label);
        builder.AppendLine(selectedSector.Description ?? string.Empty);
        builder.AppendLine($"状态：{GetAccessStatus(state, states)}");
        builder.AppendLine($"敌对力量：{selectedSector.EnemyPower.ToGameString()}");
        builder.AppendLine($"领土奖励：{selectedSector.TerritoryReward.ToGameString()}");
        detailText.text = builder.ToString();
    }

    private static int CompareDefinitions(SectorDefinition left, SectorDefinition right) =>
        string.Compare(left?.Id, right?.Id, StringComparison.OrdinalIgnoreCase);
}
