using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorkshopViewer : MonoBehaviour, IGameUIRefreshable
{
    [SerializeField] private RectTransform Content;
    private readonly Dictionary<WorkshopUpgradeDefinition, Row> rows = new();
    private WorkshopManager manager;

    private void OnEnable()
    {
        manager = WorkshopManager.Instance;
        manager.UpgradeStateChanged += OnUpgradeChanged;
        BuildRows();
        GameUIRefreshManager.Instance?.Register(this);
        RefreshUI();
    }

    private void OnDisable()
    {
        if (manager != null)
            manager.UpgradeStateChanged -= OnUpgradeChanged;
        GameUIRefreshManager.Instance?.Unregister(this);
        manager = null;
    }

    public void RefreshUI()
    {
        if (manager == null)
            return;

        if (rows.Count == 0)
            BuildRows();

        foreach (KeyValuePair<WorkshopUpgradeDefinition, Row> pair in rows)
            RefreshRow(pair.Key, pair.Value);
    }

    private void BuildRows()
    {
        if (Content == null || rows.Count != 0 || manager == null || !manager.IsSystemUnlocked)
            return;

        VerticalLayoutGroup layout = Content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = Content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 8f;

        ContentSizeFitter fitter = Content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = Content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        IReadOnlyList<WorkshopUpgradeDefinition> definitions =
            DataBase<WorkshopUpgradeDefinition>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i].TechLevel < TechLevel.Industrial)
                continue;
            rows.Add(definitions[i], CreateRow(definitions[i]));
        }
    }

    private Row CreateRow(WorkshopUpgradeDefinition definition)
    {
        var root = new GameObject(definition.Id, typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(LayoutElement));
        root.transform.SetParent(Content, false);
        root.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        root.GetComponent<LayoutElement>().minHeight = 112f;
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 4f;

        TMP_Text title = CreateText(root.transform, "Title", 22f);
        TMP_Text description = CreateText(root.transform, "Description", 16f);
        TMP_Text requirements = CreateText(root.transform, "Requirements", 15f);

        var buttonObject = new GameObject("Purchase", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(root.transform, false);
        buttonObject.GetComponent<Image>().color = new Color(0.24f, 0.45f, 0.25f, 1f);
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 36f;
        Button button = buttonObject.GetComponent<Button>();
        TMP_Text buttonLabel = CreateText(buttonObject.transform, "Label", 16f);
        RectTransform labelRect = buttonLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => manager.TryPurchase(definition, out _));

        return new Row(title, description, requirements, button, buttonLabel);
    }

    private static TMP_Text CreateText(Transform parent, string name, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.fontSize = size;
        text.enableWordWrapping = true;
        text.color = Color.white;
        return text;
    }

    private void OnUpgradeChanged(WorkshopUpgradeState _) => RefreshUI();

    private void RefreshRow(WorkshopUpgradeDefinition definition, Row row)
    {
        WorkshopUpgradeState state = manager.States[definition];
        bool available = manager.IsSystemUnlocked && manager.ArePrerequisitesMet(definition);
        row.Title.text = $"{definition.Category} · {definition.Label}";
        row.Description.text = definition.Description;
        row.Requirements.text = BuildRequirements(definition);
        row.Button.interactable = available && !state.Purchased;
        row.ButtonLabel.text = state.Purchased ? "已购买" : available ? "购买" : "未解锁";
    }

    private static string BuildRequirements(WorkshopUpgradeDefinition definition)
    {
        string result = string.Empty;
        for (int i = 0; i < definition.RequiredResearch.Count; i++)
        {
            if (result.Length > 0)
                result += "  ";
            result += $"Research: {definition.RequiredResearch[i].Label}";
        }
        for (int i = 0; i < definition.RequiredUpgrades.Count; i++)
        {
            if (result.Length > 0)
                result += "  ";
            result += $"Workshop: {definition.RequiredUpgrades[i].Label}";
        }
        for (int i = 0; i < definition.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = definition.ResourceRequirements[i];
            if (result.Length > 0)
                result += "  ";
            result += $"{requirement.First.Label} {requirement.Second.ToGameString()}";
        }

        string usedBy = string.Empty;
        IReadOnlyList<Building> buildings = DataBase<Building>.All;
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            for (int j = 0; j < building.RequiredWorkshopUpgrades.Count; j++)
            {
                if (building.RequiredWorkshopUpgrades[j] != definition)
                    continue;
                if (usedBy.Length > 0)
                    usedBy += ", ";
                usedBy += building.Label;
                break;
            }
        }
        if (usedBy.Length > 0)
            result += $"\nUnlock condition for: {usedBy}";
        return result;
    }

    private sealed class Row
    {
        public readonly TMP_Text Title;
        public readonly TMP_Text Description;
        public readonly TMP_Text Requirements;
        public readonly Button Button;
        public readonly TMP_Text ButtonLabel;

        public Row(TMP_Text title, TMP_Text description, TMP_Text requirements,
            Button button, TMP_Text buttonLabel)
        {
            Title = title;
            Description = description;
            Requirements = requirements;
            Button = button;
            ButtonLabel = buttonLabel;
        }
    }
}
