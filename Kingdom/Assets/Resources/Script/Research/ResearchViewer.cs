using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchViewer : MonoBehaviour, IGameUIRefreshable
{
    private const float ResearchNodeSize = 100f;
    private const float ResearchContentPadding = 50f;

    public RectTransform Content;
    public RectTransform LineContainer;
    [SerializeField] private GameObject ResourceReqPrefab;
    [SerializeField] private GameObject DisplayerPrefab;
    [SerializeField] private GameObject TransitionLinePrefab;
    [SerializeField] private TMP_Text BaseInfo;
    [SerializeField] private RectTransform ResourceList;
    [SerializeField] private TMP_Text DoInvestButton;
    [SerializeField] private GameObject AffordResource;
    [SerializeField] private Slider ProgressPercentage;

    private readonly Dictionary<Research, ResearchDisplayer> displayers = new();
    private readonly Dictionary<Pair<Research, Research>, ResearchLineView> lines = new();
    private readonly List<GameObject> resourceRequirementRows = new();
    private ResearchManager researchManager;
    private Research selectedResearch;
    private Research requirementsForResearch;
    private int selectedVersion = -1;
    private string actionMessage = string.Empty;

    public Research CurSelect
    {
        get => selectedResearch;
        set => SelectResearch(value);
    }

    private ResearchState SelectedState
    {
        get
        {
            if (selectedResearch == null || ResearchManager.Instance == null)
                return null;

            return ResearchManager.Instance.States.TryGetValue(
                selectedResearch,
                out ResearchState state)
                ? state
                : null;
        }
    }

    private void Awake()
    {
        if (ResourceReqPrefab == null)
            ResourceReqPrefab = Resources.Load<GameObject>("UI/Research/ResearchResourceReq");
        if (DisplayerPrefab == null)
            DisplayerPrefab = Resources.Load<GameObject>("UI/Research/ResearchDisplayer");
        if (TransitionLinePrefab == null)
            TransitionLinePrefab = Resources.Load<GameObject>("UI/Research/ResearchTransitionLine");
    }

    private void OnEnable()
    {
        TryBindResearchManager();
        GameUIRefreshManager.Instance?.Register(this);
        RefreshAll();
    }

    private void Start()
    {
        // Bootstrap managers may be created after this inactive tab is enabled.
        TryBindResearchManager();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (researchManager != null)
        {
            researchManager.ResearchStateAdded -= OnResearchStateAdded;
            researchManager.ResearchQueueChanged -= OnResearchQueueChanged;
        }
        GameUIRefreshManager.Instance?.Unregister(this);
        researchManager = null;
    }

    public void RefreshUI() => RefreshAll();

    private void OnResearchQueueChanged()
    {
        selectedVersion = -1;
        RefreshAll();
    }

    private void TryBindResearchManager()
    {
        if (researchManager == null)
            researchManager = ResearchManager.Instance;
        if (researchManager == null)
            return;

        researchManager.ResearchStateAdded -= OnResearchStateAdded;
        researchManager.ResearchStateAdded += OnResearchStateAdded;
        researchManager.ResearchQueueChanged -= OnResearchQueueChanged;
        researchManager.ResearchQueueChanged += OnResearchQueueChanged;
        BindExistingStates();
        RestoreSelection();
    }

    public void DoInvest()
    {
        if (selectedResearch == null)
            return;
        ResearchActionResult result =
            ResearchManager.Instance.HandleResearchAction(selectedResearch);
        actionMessage = result switch
        {
            ResearchActionResult.PaidOnly => "资源已支付。",
            ResearchActionResult.Started => "研究已开始。",
            ResearchActionResult.Queued => "已加入研究队列。",
            ResearchActionResult.QueuedWaitingResources => "已加入队列，等待资源。",
            ResearchActionResult.Cancelled => "已取消排队。",
            ResearchActionResult.AlreadyQueued => "研究已经在队列中。",
            ResearchActionResult.InsufficientResources => "研究资源不足。",
            ResearchActionResult.AlreadyActive => "研究正在进行中。",
            ResearchActionResult.Completed => "研究已经完成。",
            ResearchActionResult.Blocked => "当前无法进行此研究。",
            ResearchActionResult.Invalid => "研究项目无效。",
            _ => string.Empty
        };
        RefreshAll();
    }

    public void AffordResourceAction()
    {
        if (selectedResearch == null || researchManager == null)
            return;

        ResearchPaymentResult result = researchManager.PayResearchCost(selectedResearch);
        actionMessage = result switch
        {
            ResearchPaymentResult.Paid => "资源已支付。",
            ResearchPaymentResult.AlreadyPaid => "资源已经支付。",
            ResearchPaymentResult.InsufficientResources => "研究资源不足。",
            ResearchPaymentResult.Completed => "研究已经完成。",
            _ => "研究项目无效。"
        };
        selectedVersion = -1;
        RefreshAll();
    }

    public void SelectResearch(Research research)
    {
        if (selectedResearch == research)
            return;

        if (selectedResearch != null &&
            displayers.TryGetValue(selectedResearch, out ResearchDisplayer previous))
        {
            previous.SetSelectedVisual(false);
        }

        selectedResearch = research;
        selectedVersion = -1;
        requirementsForResearch = null;
        ResearchManager.Instance.SetSelectedResearch(research);

        if (selectedResearch != null &&
            displayers.TryGetValue(selectedResearch, out ResearchDisplayer current))
        {
            current.SetSelectedVisual(true);
        }

        RefreshLines();
        RefreshSelectedDetails(force: true);
    }

    private void BindExistingStates()
    {
        foreach (ResearchState state in researchManager.States.Values)
            OnResearchStateAdded(state);
        CreateAllLines();
    }

    private void OnResearchStateAdded(ResearchState state)
    {
        if (state == null || displayers.ContainsKey(state.Definition))
            return;
        if (Content == null || DisplayerPrefab == null)
        {
            Debug.LogWarning($"Cannot create research UI for '{state.Definition.Id}'. Missing ResearchViewer content or prefab.");
            return;
        }

        ResearchDisplayer displayer = Instantiate(DisplayerPrefab, Content, false)
            .GetComponent<ResearchDisplayer>();
        displayer.Bind(state);
        RectTransform rectTransform = displayer.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = PlacePosition(state.Definition.x, state.Definition.y);
        displayers.Add(state.Definition, displayer);
        RefreshContentBounds();
    }

    private void CreateAllLines()
    {
        if (LineContainer == null || TransitionLinePrefab == null)
            return;

        foreach (ResearchState state in researchManager.States.Values)
        {
            Research research = state.Definition;
            IReadOnlyList<Research> prerequisites = research.Prerequisites;
            if (prerequisites == null)
                continue;

            for (int i = 0; i < prerequisites.Count; i++)
            {
                var key = new Pair<Research, Research>(prerequisites[i], research);
                if (lines.ContainsKey(key))
                    continue;

                ResearchLineView line = Instantiate(TransitionLinePrefab, LineContainer, false)
                    .GetComponent<ResearchLineView>();
                if (line == null)
                {
                    Debug.LogError("ResearchTransitionLine prefab is missing ResearchLineView.");
                    continue;
                }

                if (!displayers.TryGetValue(prerequisites[i], out ResearchDisplayer prerequisiteDisplayer) ||
                    !displayers.TryGetValue(research, out ResearchDisplayer researchDisplayer))
                {
                    Destroy(line.gameObject);
                    Debug.LogWarning(
                        $"Cannot connect research line '{prerequisites[i].Id}' -> '{research.Id}': " +
                        "one of the research nodes has not been created.");
                    continue;
                }

                line.Bind(
                    prerequisites[i],
                    research,
                    prerequisiteDisplayer.GetComponent<RectTransform>(),
                    researchDisplayer.GetComponent<RectTransform>());
                lines.Add(key, line);
            }
        }

        RefreshContentBounds();
        RefreshLines();
    }

    private void RefreshAll()
    {
        if (researchManager == null)
            TryBindResearchManager();
        if (researchManager == null)
            return;

        RestoreSelection();
        foreach (ResearchDisplayer displayer in displayers.Values)
            displayer.Refresh();
        RefreshContentBounds();
        RefreshLines();
        RefreshSelectedDetails(false);
    }

    private void RefreshSelectedDetails(bool force)
    {
        ResearchState state = SelectedState;
        if (state == null)
        {
            if (DoInvestButton != null)
                DoInvestButton.text = string.Empty;
            if (ProgressPercentage != null)
                ProgressPercentage.value = 0f;
            if (AffordResource != null)
                AffordResource.SetActive(false);
            if (BaseInfo != null)
                BaseInfo.text = string.Empty;
            RebuildRequirementRows(null);
            PositionResourceListAfterBaseInfo();
            return;
        }

        if (!force && selectedVersion == state.Version)
            return;
        selectedVersion = state.Version;

        if (DoInvestButton != null)
            DoInvestButton.text = ButtonText(state);
        if (AffordResource != null)
            AffordResource.SetActive(
                state.Status != ResearchStatus.Completed && !state.CostPaid);
        if (ProgressPercentage != null)
            ProgressPercentage.value = (float)state.ProgressRatio.ToDouble();
        if (BaseInfo != null)
        {
            string label = selectedResearch.Label ?? "未知研究";
            string techLevelDesc = selectedResearch.TechLevel.GetDescription() ?? "未知时代";
            double progress = state.ProgressRatio.ToDouble() * 100d;
            string desc = selectedResearch.Description ?? "暂无研究说明";
            if (BaseInfo != null)
            {
                BaseInfo.text = $"{label}\n{techLevelDesc}\n{progress:F2}%\n{desc}" +
                    (string.IsNullOrWhiteSpace(actionMessage)
                        ? string.Empty
                        : $"\n{actionMessage}") +
                    $"\n\n{QueueSummary()}";
                actionMessage = string.Empty;
            }
        }

        RebuildRequirementRows(selectedResearch);
        PositionResourceListAfterBaseInfo();
    }

    private void PositionResourceListAfterBaseInfo()
    {
        if (BaseInfo == null)
            return;

        RectTransform detailContent = BaseInfo.rectTransform;
        float contentWidth = detailContent.rect.width;
        float preferredHeight = BaseInfo.GetPreferredValues(
            BaseInfo.text,
            contentWidth,
            0f).y;

        float resourceListHeight = 0f;
        if (ResourceList != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(ResourceList);
            resourceListHeight = LayoutUtility.GetPreferredHeight(ResourceList);
            if (resourceListHeight <= 0f)
                resourceListHeight = ResourceList.rect.height;

            ResourceList.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                resourceListHeight);
            ResourceList.anchoredPosition = new Vector2(0f, -preferredHeight - 20f);
        }

        float requiredHeight = preferredHeight +
            (ResourceList == null ? 0f : 20f + resourceListHeight);
        detailContent.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(428f, requiredHeight));
    }

    private string QueueSummary()
    {
        if (researchManager == null)
            return "研究队列不可用";
        List<string> lines = new();
        if (researchManager.ActiveResearch != null)
            lines.Add($"当前研究：{researchManager.ActiveResearch.Definition.Label}");
        IReadOnlyList<ResearchState> queue = researchManager.ResearchQueue;
        for (int i = 0; i < queue.Count; i++)
        {
            ResearchState state = queue[i];
            string status = state.Status == ResearchStatus.WaitingResources
                ? "等待资源"
                : "排队";
            lines.Add($"[{i + 1}]{state.Definition.Label}-{status}");
        }
        return lines.Count == 0
            ? "研究队列：空"
            : "研究队列：\n" + string.Join("\n", lines);
    }

    private string ButtonText(ResearchState state)
    {
        if (state.Status == ResearchStatus.Completed)
            return "研究已完成";
        if (state.Status == ResearchStatus.Researching)
            return "正在研究";
        if (ResearchManager.Instance.IsQueued(selectedResearch))
            return "取消排队";
        if (!ResearchManager.Instance.CanAccessResearch(selectedResearch))
            return "技术等级不足";
        return "加入研究队列";
    }

    private void RebuildRequirementRows(Research research)
    {
        if (research == null || ResourceList == null || ResourceReqPrefab == null)
        {
            if (research == null)
            {
                for (int i = 0; i < resourceRequirementRows.Count; i++)
                    Destroy(resourceRequirementRows[i]);
                resourceRequirementRows.Clear();
                requirementsForResearch = null;
            }
            return;
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = research.ResourceRequirements;
        bool canRefreshExisting = requirementsForResearch == research &&
            resourceRequirementRows.Count == requirements.Count;
        if (!canRefreshExisting)
        {
            for (int i = 0; i < resourceRequirementRows.Count; i++)
                Destroy(resourceRequirementRows[i]);
            resourceRequirementRows.Clear();
            requirementsForResearch = research;
        }

        ResearchState state = SelectedState;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            ResourceRequirementView view;
            if (canRefreshExisting)
                view = resourceRequirementRows[i].GetComponent<ResourceRequirementView>();
            else
            {
                GameObject row = Instantiate(ResourceReqPrefab, ResourceList, false);
                view = row.GetComponent<ResourceRequirementView>();
                resourceRequirementRows.Add(row);
            }

            ExpantaNum paid = state == null
                ? ExpantaNum.Zero
                : state.GetPaidResourceCost(requirement.First);
            ExpantaNum remaining = ExpantaNum.Max(ExpantaNum.Zero, requirement.Second - paid);
            if (view != null)
                view.Bind(requirement.First, remaining);
        }
    }

    private void RefreshLines()
    {
        foreach (ResearchLineView line in lines.Values)
        {
            line.RefreshGeometry();
            line.SetSelectedResearch(selectedResearch);
        }
    }

    private void RefreshContentBounds()
    {
        if (Content == null || displayers.Count == 0)
            return;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        foreach (ResearchDisplayer displayer in displayers.Values)
        {
            RectTransform node = displayer.GetComponent<RectTransform>();
            Vector2 position = PlacePosition(displayer.Research.x, displayer.Research.y);
            float halfWidth = Mathf.Max(ResearchNodeSize, node.rect.width) * 0.5f;
            float halfHeight = Mathf.Max(ResearchNodeSize, node.rect.height) * 0.5f;
            minX = Mathf.Min(minX, position.x - halfWidth);
            maxX = Mathf.Max(maxX, position.x + halfWidth);
            minY = Mathf.Min(minY, position.y - halfHeight);
            maxY = Mathf.Max(maxY, position.y + halfHeight);
        }

        Vector2 offset = new Vector2(
            ResearchContentPadding - minX,
            -ResearchContentPadding - maxY);
        foreach (ResearchDisplayer displayer in displayers.Values)
        {
            RectTransform node = displayer.GetComponent<RectTransform>();
            node.anchoredPosition = PlacePosition(
                displayer.Research.x,
                displayer.Research.y) + offset;
        }

        Content.sizeDelta = new Vector2(
            Mathf.Max(1f, maxX - minX + ResearchContentPadding * 2f),
            Mathf.Max(1f, maxY - minY + ResearchContentPadding * 2f));
    }

    private void RestoreSelection()
    {
        if (selectedResearch != null)
            return;
        if (DataBase<Research>.TryFind(researchManager.SelectedResearchId, out Research restored) &&
            researchManager.States.ContainsKey(restored))
            SelectResearch(restored);
    }

    private static Vector2 PlacePosition(float x, float y) => new Vector2(325f * x,- 75f * y);
}
