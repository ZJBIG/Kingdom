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

    private readonly Dictionary<SectorDefinition, SectorNodeView> displayers = new();
    private SectorDefinition selectedSector;
    private int selectedVersion = -1;
    private bool warnedMissingPrefab;

    private SectorManager SectorManager => GameManager.Instance.Sectors;
    public IReadOnlyDictionary<SectorDefinition, SectorState> States => SectorManager.States;
    public IReadOnlyList<SectorState> OrderedStates => SectorManager.OrderedStates;
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
        for (int i = 0; i < OrderedStates.Count; i++)
        {
            SectorDefinition definition = OrderedStates[i].Definition;
            if (displayers.TryGetValue(definition, out SectorNodeView node))
                node.Refresh();
        }

        RefreshDetails();
    }

    public void SelectSector(SectorDefinition definition)
    {
        if (definition == null || !States.ContainsKey(definition))
            return;

        selectedSector = definition;
        selectedVersion = -1;
        RefreshDetails();
    }

    public SectorState GetState(SectorDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        return States[definition];
    }

    public SectorAccessStatus GetAccessStatus(SectorDefinition definition)
    {
        if (definition == null || !States.TryGetValue(definition, out SectorState state))
            return SectorAccessStatus.Locked;
        return GetAccessStatus(state, States);
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
        SectorManager.InitializeDefinitions();
        CreateMissingNodes();
        if (selectedSector == null && OrderedStates.Count > 0)
            selectedSector = OrderedStates[0].Definition;
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

        for (int i = 0; i < OrderedStates.Count; i++)
        {
            SectorDefinition definition = OrderedStates[i].Definition;
            if (displayers.ContainsKey(definition))
                continue;

            SectorNodeView node = Instantiate(sectorNodePrefab, content, false)
                .GetComponent<SectorNodeView>();
            if (node == null)
            {
                Debug.LogError("Sector node prefab is missing SectorNodeView.");
                continue;
            }

            node.Bind(States[definition], SelectSector);
            RectTransform rect = node.transform as RectTransform;
            if (rect != null)
                rect.anchoredPosition = new Vector2(definition.MapX, definition.MapY);
            displayers.Add(definition, node);
        }
    }

    private void RefreshDetails()
    {
        if (detailText == null || selectedSector == null || !States.TryGetValue(selectedSector, out SectorState state))
            return;
        if (selectedVersion == state.Version)
            return;

        selectedVersion = state.Version;
        StringBuilder builder = new();
        builder.AppendLine(string.IsNullOrWhiteSpace(selectedSector.Label)
            ? selectedSector.Id
            : selectedSector.Label);
        builder.AppendLine(selectedSector.Description ?? string.Empty);
        builder.AppendLine($"状态：{GetAccessStatus(state, States)}");
        builder.AppendLine($"敌对力量：{selectedSector.EnemyPower.ToGameString()}");
        builder.AppendLine($"领土奖励：{selectedSector.TerritoryReward.ToGameString()}");
        SectorCampaignPreview preview = SectorManager.GetCampaignPreview(
            selectedSector,
            GameManager.Instance.State,
            ResourceManager.Instance);
        if (preview.IsValid && selectedSector.EnemyPower > ExpantaNum.Zero)
        {
            builder.AppendLine($"Campaign ratio: {preview.CombatRatio.ToGameString()}");
            builder.AppendLine($"Progress/min: {preview.ProgressPerMinute.ToGameString()}");
            builder.AppendLine($"Casualties/min: {preview.CasualtiesPerMinute.ToGameString()}");
            builder.AppendLine($"Food/min: {preview.FoodCostPerMinute.ToGameString()}");
            for (int i = 0; i < preview.ResourceCostsPerMinute.Count; i++)
            {
                Pair<Resource, ExpantaNum> cost = preview.ResourceCostsPerMinute[i];
                builder.AppendLine($"{cost.First.Id}/min: {cost.Second.ToGameString()}");
            }
            builder.AppendLine(preview.HasSupply ? "Supply: Ready" : "Supply: Insufficient");
        }
        detailText.text = builder.ToString();
    }

}
