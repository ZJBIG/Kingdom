using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Industrial workshop dashboard. The viewer owns presentation and selection only;
/// WorkshopManager remains the authority for prerequisites and purchases.
/// </summary>
public sealed class WorkshopViewer : MonoBehaviour, IGameUIRefreshable
{
    private static readonly Color Steel = new Color(0.055f, 0.075f, 0.10f, 0.98f);
    private static readonly Color SteelPanel = new Color(0.09f, 0.12f, 0.16f, 0.98f);
    private static readonly Color SteelCard = new Color(0.12f, 0.15f, 0.19f, 1f);
    private static readonly Color Copper = new Color(0.86f, 0.47f, 0.20f, 1f);
    private static readonly Color Blue = new Color(0.19f, 0.64f, 0.94f, 1f);
    private static readonly Color Purple = new Color(0.68f, 0.39f, 0.90f, 1f);
    private static readonly Color Orange = new Color(0.95f, 0.56f, 0.20f, 1f);
    private static readonly Color Green = new Color(0.25f, 0.78f, 0.48f, 1f);
    private static readonly Color Muted = new Color(0.57f, 0.63f, 0.70f, 1f);

    [SerializeField] private RectTransform Content;
    [SerializeField] private GameObject CardPrefab;
    [SerializeField] private GameObject DetailPanelPrefab;

    private readonly Dictionary<WorkshopUpgradeDefinition, Row> rows = new();
    private readonly List<WorkshopUpgradeDefinition> definitions = new();
    private readonly List<Button> categoryButtons = new();
    private WorkshopManager manager;
    private WorkshopUpgradeDefinition selected;
    private string selectedCategory = "全部";
    private Transform cardRoot;
    private RectTransform detailRoot;
    private TMP_Text countText;
    private TMP_Text unlockText;
    private TMP_Text detailTitle;
    private TMP_Text detailBody;
    private TMP_Text detailStatus;
    private Button detailPurchase;
    private TMP_Text detailPurchaseLabel;
    private bool confirmPurchase;
    private bool built;

    private void Awake()
    {
        if (Content == null)
            return;
        EnsureDashboardShell();
    }

    private void OnEnable()
    {
        manager = WorkshopManager.Instance;
        if (manager != null)
            manager.UpgradeStateChanged += OnUpgradeChanged;
        GameUIRefreshManager.Instance?.Register(this);
        EnsureDashboardShell();
        BuildRows();
        RefreshUI();
    }

    private void Start()
    {
        // The bootstrap managers can be created after an inactive navigation tab is enabled.
        if (manager == null)
            manager = WorkshopManager.Instance;
        if (manager != null)
        {
            manager.UpgradeStateChanged -= OnUpgradeChanged;
            manager.UpgradeStateChanged += OnUpgradeChanged;
        }
        BuildRows();
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
            manager = WorkshopManager.Instance;
        if (manager == null)
            return;
        BuildRows();
        int purchased = 0;
        foreach (WorkshopUpgradeDefinition definition in definitions)
            if (manager.IsPurchased(definition))
                purchased++;
        if (countText != null)
            countText.text = $"{purchased} / {definitions.Count}";
        if (unlockText != null)
            unlockText.text = manager.IsSystemUnlocked ? "系统已解锁 · 工业时代" : "系统未解锁";
        foreach (KeyValuePair<WorkshopUpgradeDefinition, Row> pair in rows)
            RefreshRow(pair.Key, pair.Value);
        RefreshDetail();
    }

    private void BuildRows()
    {
        if (built || Content == null || manager == null)
            return;
        built = true;
        IReadOnlyList<WorkshopUpgradeDefinition> all = DataBase<WorkshopUpgradeDefinition>.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].TechLevel < TechLevel.Industrial)
                continue;
            definitions.Add(all[i]);
        }
        definitions.Sort((a, b) =>
        {
            int order = a.SortOrder.CompareTo(b.SortOrder);
            return order != 0 ? order : string.Compare(a.Label, b.Label, StringComparison.Ordinal);
        });
        CreateCategoryBar();
        for (int i = 0; i < definitions.Count; i++)
            rows.Add(definitions[i], CreateRow(definitions[i]));
    }

    private void EnsureDashboardShell()
    {
        if (Content == null || cardRoot != null)
            return;
        Content.gameObject.name = "WorkshopContent_IndustrialDashboard";
        Image background = Content.GetComponent<Image>();
        if (background == null)
            background = Content.gameObject.AddComponent<Image>();
        background.color = Steel;
        VerticalLayoutGroup rootLayout = Content.GetComponent<VerticalLayoutGroup>();
        if (rootLayout == null)
            rootLayout = Content.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(28, 28, 34, 110);
        rootLayout.spacing = 18f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        GameObject header = MakePanel(Content, "IndustrialHeader", SteelPanel, 128f);
        HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(26, 26, 18, 18);
        headerLayout.spacing = 18f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        TMP_Text title = MakeText(header.transform, "工业工坊", 34f, Color.white, FontStyles.Bold);
        title.GetComponent<LayoutElement>().flexibleWidth = 1f;
        unlockText = MakeText(header.transform, "系统状态", 18f, Green, FontStyles.Normal);
        unlockText.GetComponent<LayoutElement>().preferredWidth = 240f;
        countText = MakeText(header.transform, "0 / 0", 24f, Copper, FontStyles.Bold);
        countText.alignment = TextAlignmentOptions.Right;
        countText.GetComponent<LayoutElement>().preferredWidth = 130f;

        GameObject listPanel = MakePanel(Content, "WorkshopList", SteelPanel, 0f);
        LayoutElement listElement = listPanel.GetComponent<LayoutElement>();
        listElement.flexibleHeight = 1f;
        VerticalLayoutGroup listLayout = listPanel.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(16, 16, 16, 16);
        listLayout.spacing = 12f;
        GameObject categories = MakePanel(listPanel.transform, "CategoryFilters", Steel, 76f);
        categories.AddComponent<HorizontalLayoutGroup>();
        cardRoot = new GameObject("CardContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).transform;
        cardRoot.SetParent(listPanel.transform, false);
        VerticalLayoutGroup cards = cardRoot.GetComponent<VerticalLayoutGroup>();
        cards.spacing = 10f;
        cards.childControlWidth = true;
        cards.childControlHeight = true;
        cards.childForceExpandWidth = true;
        cards.childForceExpandHeight = false;
        cardRoot.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        detailRoot = CreateDetailPanel(Content);
        detailRoot.gameObject.SetActive(false);
    }

    private void CreateCategoryBar()
    {
        Transform bar = Content.Find("WorkshopList/CategoryFilters");
        if (bar == null || categoryButtons.Count > 0)
            return;
        string[] categories = { "全部", "动力", "机械", "冶金", "化工", "电气", "物流", "农业", "科研" };
        HorizontalLayoutGroup layout = bar.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        for (int i = 0; i < categories.Length; i++)
        {
            string category = categories[i];
            GameObject buttonObject = MakeButton(bar, category, 18f, SteelCard, 88f);
            Button button = buttonObject.GetComponent<Button>();
            button.GetComponent<LayoutElement>().flexibleWidth = 1f;
            button.onClick.AddListener(() => SetCategory(category));
            categoryButtons.Add(button);
        }
    }

    private Row CreateRow(WorkshopUpgradeDefinition definition)
    {
        GameObject root;
        if (CardPrefab != null)
        {
            root = Instantiate(CardPrefab, cardRoot, false);
            root.name = definition.Id;
        }
        else
            root = MakePanel(cardRoot, definition.Id, SteelCard, 152f);
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 16, 16);
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        TMP_Text title = MakeText(root.transform, definition.Label, 24f, Color.white, FontStyles.Bold);
        title.GetComponent<LayoutElement>().preferredWidth = 240f;
        TMP_Text description = MakeText(root.transform, definition.Description, 17f, Muted, FontStyles.Normal);
        description.GetComponent<LayoutElement>().flexibleWidth = 1f;
        description.maxVisibleLines = 2;
        TMP_Text status = MakeText(root.transform, "", 17f, Green, FontStyles.Bold);
        status.GetComponent<LayoutElement>().preferredWidth = 180f;
        Button button = root.GetComponent<Button>();
        if (button == null)
            button = root.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        WorkshopUpgradeDefinition captured = definition;
        button.onClick.AddListener(() => Select(captured));
        return new Row(root, title, description, status);
    }

    private RectTransform CreateDetailPanel(Transform parent)
    {
        GameObject root;
        if (DetailPanelPrefab != null)
            root = Instantiate(DetailPanelPrefab, parent, false);
        else
            root = MakePanel(parent, "WorkshopDetailDrawer", SteelPanel, 560f);
        root.name = "WorkshopDetailDrawer";
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(26, 26, 22, 22);
        layout.spacing = 10f;
        detailTitle = MakeText(root.transform, "选择一个工坊器件", 28f, Copper, FontStyles.Bold);
        detailStatus = MakeText(root.transform, "", 18f, Green, FontStyles.Bold);
        detailBody = MakeText(root.transform, "", 17f, Color.white, FontStyles.Normal);
        detailBody.GetComponent<LayoutElement>().flexibleHeight = 1f;
        detailBody.overflowMode = TextOverflowModes.Ellipsis;
        GameObject purchase = MakeButton(root.transform, "购买", 22f, Copper, 88f);
        detailPurchase = purchase.GetComponent<Button>();
        detailPurchaseLabel = purchase.GetComponentInChildren<TMP_Text>();
        detailPurchase.onClick.AddListener(ConfirmOrPurchase);
        return root.GetComponent<RectTransform>();
    }

    private void SetCategory(string category)
    {
        selectedCategory = category;
        foreach (KeyValuePair<WorkshopUpgradeDefinition, Row> pair in rows)
            pair.Value.Root.SetActive(category == "全部" || pair.Key.Category == category);
        for (int i = 0; i < categoryButtons.Count; i++)
            categoryButtons[i].GetComponent<Image>().color = categoryButtons[i].GetComponentInChildren<TMP_Text>().text == category ? Copper : SteelCard;
    }

    private void Select(WorkshopUpgradeDefinition definition)
    {
        selected = definition;
        confirmPurchase = false;
        if (detailRoot != null)
            detailRoot.gameObject.SetActive(true);
        RefreshDetail();
    }

    private void ConfirmOrPurchase()
    {
        if (selected == null || manager == null)
            return;
        if (manager.IsPurchased(selected))
            return;
        if (!confirmPurchase)
        {
            confirmPurchase = true;
            if (detailPurchaseLabel != null)
                detailPurchaseLabel.text = "再次点击确认购买";
            return;
        }
        if (!manager.TryPurchase(selected, out WorkshopPurchaseFailure failure))
        {
            confirmPurchase = false;
            if (detailStatus != null)
                detailStatus.text = FailureText(failure);
            RefreshDetail();
            return;
        }
        confirmPurchase = false;
        RefreshUI();
    }

    private void RefreshRow(WorkshopUpgradeDefinition definition, Row row)
    {
        bool purchased = manager.IsPurchased(definition);
        bool prerequisites = manager.ArePrerequisitesMet(definition);
        row.Description.text = definition.Description ?? "暂无说明";
        bool systemLocked = !manager.IsSystemUnlocked;
        row.Status.color = purchased ? Copper : systemLocked || !prerequisites ? Muted : Green;
        row.Status.text = purchased ? "✓ 已购买" : systemLocked ? "🔒 系统未解锁" : prerequisites ? "可购买" : "🔒 前置不足";
        row.Root.SetActive(selectedCategory == "全部" || definition.Category == selectedCategory);
        row.Root.GetComponent<Image>().color = purchased ? new Color(0.20f, 0.16f, 0.12f) : SteelCard;
    }

    private void RefreshDetail()
    {
        if (selected == null || detailRoot == null)
            return;
        bool purchased = manager.IsPurchased(selected);
        bool prerequisites = manager.ArePrerequisitesMet(selected);
        detailTitle.text = $"{selected.Label}  ·  {selected.Category}";
        detailStatus.text = purchased ? "✓ 已购买" : !manager.IsSystemUnlocked ? "系统未解锁" : !prerequisites ? "前置条件未满足" : confirmPurchase ? "请再次点击确认" : "可购买";
        detailStatus.color = purchased ? Copper : prerequisites ? Green : Orange;
        detailBody.text = BuildDetailText(selected);
        detailPurchase.interactable = !purchased && manager.IsSystemUnlocked && prerequisites;
        detailPurchaseLabel.text = purchased ? "已购买" : confirmPurchase ? "再次点击确认购买" : "购买";
    }

    private string BuildDetailText(WorkshopUpgradeDefinition definition)
    {
        var text = new StringBuilder();
        text.AppendLine(definition.Description ?? "暂无说明");
        text.AppendLine();
        text.AppendLine("研究前置");
        AppendResearch(text, definition.RequiredResearch);
        text.AppendLine("Workshop 前置");
        AppendWorkshops(text, definition.RequiredUpgrades);
        text.AppendLine("购买资源");
        for (int i = 0; i < definition.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = definition.ResourceRequirements[i];
            text.AppendLine($"• {cost.First.Label}: {cost.Second.ToGameString()}");
        }
        text.AppendLine("实际效果");
        for (int i = 0; i < definition.Effects.Count; i++)
        {
            WorkshopEffectDefinition effect = definition.Effects[i];
            string target = effect.Building != null ? effect.Building.Label : effect.Resource != null ? effect.Resource.Label : "全局";
            text.AppendLine($"• {EffectText(effect.Type)} → {target} × {effect.Value.ToGameString()}");
        }
        string usedBy = UsedBy(definition);
        text.AppendLine("被哪些建筑使用");
        text.AppendLine(usedBy.Length == 0 ? "• 暂无建筑使用" : usedBy);
        return text.ToString();
    }

    private static void AppendResearch(StringBuilder text, IReadOnlyList<Research> list)
    {
        if (list.Count == 0) { text.AppendLine("• 无"); return; }
        for (int i = 0; i < list.Count; i++) text.AppendLine($"• {list[i].Label}");
    }

    private static void AppendWorkshops(StringBuilder text, IReadOnlyList<WorkshopUpgradeDefinition> list)
    {
        if (list.Count == 0) { text.AppendLine("• 无"); return; }
        for (int i = 0; i < list.Count; i++) text.AppendLine($"• {list[i].Label}");
    }

    private static string UsedBy(WorkshopUpgradeDefinition definition)
    {
        var result = new StringBuilder();
        IReadOnlyList<Building> buildings = DataBase<Building>.All;
        for (int i = 0; i < buildings.Count; i++)
        {
            IReadOnlyList<WorkshopUpgradeDefinition> required = buildings[i].RequiredWorkshopUpgrades;
            for (int j = 0; j < required.Count; j++)
                if (required[j] == definition)
                { result.AppendLine($"• {buildings[i].Label}"); break; }
        }
        return result.ToString().TrimEnd();
    }

    private static string EffectText(WorkshopEffectType type) => type switch
    {
        WorkshopEffectType.BuildingProductionMultiplier => "建筑产出",
        WorkshopEffectType.BuildingFoodProductionMultiplier => "建筑食物产出",
        WorkshopEffectType.ResourceProductionMultiplier => "资源产出",
        WorkshopEffectType.GlobalResearchMultiplier => "全局研究速度",
        WorkshopEffectType.GlobalConstructionMultiplier => "全局建造速度",
        WorkshopEffectType.TerritoryGranted => "领土",
        WorkshopEffectType.MilitaryMultiplier => "军事效率",
        WorkshopEffectType.PowerMultiplier => "电力效率",
        WorkshopEffectType.GlobalBuildingProductionMultiplier => "全局建筑产出",
        WorkshopEffectType.BuildingResearchPowerMultiplier => "研究建筑效率",
        WorkshopEffectType.BuildingPowerProductionMultiplier => "电力产出",
        WorkshopEffectType.BuildingLogisticsProductionMultiplier => "物流产出",
        WorkshopEffectType.GlobalLogisticsMultiplier => "全局物流效率",
        _ => "效果"
    };

    private static string FailureText(WorkshopPurchaseFailure failure) => failure switch
    {
        WorkshopPurchaseFailure.SystemLocked => "系统未解锁：先完成工业工坊研究。",
        WorkshopPurchaseFailure.ResearchIncomplete => "购买失败：研究前置尚未完成。",
        WorkshopPurchaseFailure.UpgradeIncomplete => "购买失败：Workshop 前置尚未购买。",
        WorkshopPurchaseFailure.ResourceInsufficient => "购买失败：购买资源不足。",
        WorkshopPurchaseFailure.AlreadyPurchased => "该器件已经购买。",
        _ => "购买失败：定义或运行状态无效。"
    };

    private void OnUpgradeChanged(WorkshopUpgradeState _) => RefreshUI();

    private static GameObject MakePanel(Transform parent, string name, Color color, float height)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        panel.GetComponent<LayoutElement>().preferredHeight = height;
        return panel;
    }

    private static TMP_Text MakeText(Transform parent, string value, float size, Color color, FontStyles style)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.lineSpacing = 4f;
        return text;
    }

    private static GameObject MakeButton(Transform parent, string label, float size, Color color, float height)
    {
        var button = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        button.transform.SetParent(parent, false);
        button.GetComponent<Image>().color = color;
        button.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = MakeText(button.transform, label, size, Color.white, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private sealed class Row
    {
        public readonly GameObject Root;
        public readonly TMP_Text Title;
        public readonly TMP_Text Description;
        public readonly TMP_Text Status;
        public Row(GameObject root, TMP_Text title, TMP_Text description, TMP_Text status)
        { Root = root; Title = title; Description = description; Status = status; }
    }
}
