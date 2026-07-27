using System;
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
    [SerializeField] private Slider ProgressPercentage;

    private readonly Dictionary<Research, ResearchDisplayer> displayers = new();
    private readonly Dictionary<Pair<Research, Research>, ResearchLineView> lines = new();
    private readonly List<GameObject> resourceRequirementRows = new();
    private ResearchManager researchManager;
    private Research selectedResearch;
    private Research requirementsForResearch;
    private int selectedVersion = -1;

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
            researchManager.ResearchStateAdded -= OnResearchStateAdded;
        GameUIRefreshManager.Instance?.Unregister(this);
        researchManager = null;
    }

    public void RefreshUI() => RefreshAll();

    private void TryBindResearchManager()
    {
        if (researchManager == null)
            researchManager = ResearchManager.Instance;
        if (researchManager == null)
            return;

        researchManager.ResearchStateAdded -= OnResearchStateAdded;
        researchManager.ResearchStateAdded += OnResearchStateAdded;
        BindExistingStates();
        RestoreSelection();
    }

    public void DoInvest()
    {
        if (selectedResearch == null)
            return;
        ResearchState state = SelectedState;
        if (state == null)
            return;
        if (!ResearchManager.Instance.CanAccessResearch(selectedResearch) ||
            !ResearchManager.Instance.ArePrerequisitesCompleted(selectedResearch))
        {
            RefreshAll();
            return;
        }
        if (!state.CostPaid)
        {
            ResearchManager.TryPayResearchCost(state);
            RefreshAll();
            return;
        }
        if (state.Status == ResearchStatus.Completed ||
            !ResearchManager.Instance.CanAccessResearch(selectedResearch))
        {
            return;
        }

        ResearchManager.Instance.StartResearch(selectedResearch);
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
            if (BaseInfo != null)
                BaseInfo.text = string.Empty;
            RebuildRequirementRows(null);
            return;
        }

        if (!force && selectedVersion == state.Version)
            return;
        selectedVersion = state.Version;

        if (DoInvestButton != null)
            DoInvestButton.text = ButtonText(state);
        if (ProgressPercentage != null)
            ProgressPercentage.value = (float)state.ProgressRatio.ToDouble();
        if (BaseInfo != null)
        {
            string label = selectedResearch.Label ?? "Unknown research";
            string techLevelDesc = selectedResearch.TechLevel.GetDescription() ?? "No tech level";
            double progress = state.ProgressRatio.ToDouble() * 100d;
            string desc = selectedResearch.Description ?? "No description";
            if (BaseInfo != null)
                BaseInfo.text = $"{label}\n{techLevelDesc}\n{progress:F2}%\n{desc}";
        }

        PositionResourceListAfterBaseInfo();
        RebuildRequirementRows(selectedResearch);
    }

    private void PositionResourceListAfterBaseInfo()
    {
        if (BaseInfo == null || ResourceList == null)
            return;

        float preferredHeight = BaseInfo.GetPreferredValues(
            BaseInfo.text,
            BaseInfo.rectTransform.rect.width,
            0f).y;
        ResourceList.anchoredPosition = new Vector2(0f, -preferredHeight - 20f);

        RectTransform detailContent = BaseInfo.rectTransform;
        float requiredHeight = preferredHeight + 20f + ResourceList.rect.height;
        detailContent.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(428f, requiredHeight));
    }

    private string ButtonText(ResearchState state)
    {
        if (!ResearchManager.Instance.CanAccessResearch(selectedResearch))
            return "技术等级过低";
        if (!ResearchManager.Instance.ArePrerequisitesCompleted(selectedResearch))
            return "前置研究未完成";
        if (!state.CostPaid)
            return "支付资源";
        return state.Status switch
        {
            ResearchStatus.Completed => "项目已经完成",
            ResearchStatus.Researching => "正在进行研究",
            ResearchStatus.WaitingResources => "等待研究资源",
            ResearchStatus.Locked => "需要先完成前置研究",
            ResearchStatus.Available => "开始此项研究",
            _ => ""
        };
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

    private static Vector2 PlacePosition(float x, float y) => new Vector2(-750f + 325f * x, -25f - 75f * y);
}
