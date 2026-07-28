using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingDisplayer : MonoBehaviour
{
    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Description;
    [SerializeField] private TMP_Text StatusText;
    [SerializeField] private TMP_Text Amount;
    [SerializeField] private Transform Details;
    [SerializeField] private RectTransform Construction;
    [SerializeField] private RectTransform ResourceList;
    [SerializeField] private GameObject BuildResourceReqPrefab;

    private BuildingState state;
    private int renderedVersion = -1;
    private bool requirementsBound;
    private BuildFailure lastFailure;
    public Building Building => state?.Definition;
    public bool IsExpanded => Details != null && Details.gameObject.activeSelf;
    public float PreferredHeight => IsExpanded
        ? 50f + Mathf.Max(1, Mathf.CeilToInt((Building?.ResourceRequirements.Count ?? 0) / 2f)) * 50f + 50f
        : 100f;

    private void Awake()
    {
        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.enabled = false;

        if (ResourceList != null)
        {
            GridLayoutGroup requirementsLayout = ResourceList.GetComponent<GridLayoutGroup>();
            if (requirementsLayout == null)
                requirementsLayout = ResourceList.gameObject.AddComponent<GridLayoutGroup>();
            requirementsLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            requirementsLayout.constraintCount = 2;
            requirementsLayout.cellSize = new Vector2(191.5f, 50f);
        }

        if (Details != null)
        {
            foreach (LayoutGroup detailsLayout in Details.GetComponents<LayoutGroup>())
                detailsLayout.enabled = false;
        }

        if (Description != null)
            Description.gameObject.SetActive(false);

        if (Construction != null)
        {
            foreach (LayoutGroup constructionLayout in Construction.GetComponents<LayoutGroup>())
                constructionLayout.enabled = false;
        }

        SetDetailsVisible(false);
    }

    public void Bind(BuildingState newState)
    {
        state = newState ?? throw new System.ArgumentNullException(nameof(newState));

        Label.text = Building.Label;
        Description.text = Building.Description;
        if (StatusText != null)
            StatusText.text = string.Empty;
        BindRequirements();
        renderedVersion = -1;
        ApplyCardHeight();
        Refresh();
    }

    public void DisplayDetails()
    {
        if (Details == null)
            return;
        SetDetailsVisible(!IsExpanded);
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        GetComponentInParent<BuildingViewer>()?.RefreshLayout();
    }

    public void TryConstruct(string input)
    {
        if (!BuildingTransactionRules.TryNormalizePositiveWhole(input, out ExpantaNum amount))
        {
            ShowFailure(BuildFailure.InvalidAmount);
            return;
        }

        if (BuildingManager.Instance.TryBuild(Building, amount, out BuildFailure failure))
            ClearFailure();
        else
            ShowFailure(failure);
    }

    public void TryDeconstruct(string input)
    {
        if (!BuildingTransactionRules.TryNormalizePositiveWhole(input, out ExpantaNum amount))
        {
            ShowFailure(BuildFailure.InvalidAmount);
            return;
        }

        if (BuildingManager.Instance.TryDeconstruct(Building, amount, out BuildFailure failure))
            ClearFailure();
        else
            ShowFailure(failure);
    }

    public void Clear()
    {
        if (BuildingManager.Instance.TryDeconstruct(Building, state.Amount, out BuildFailure failure))
            ClearFailure();
        else
            ShowFailure(failure);
    }

    public bool Refresh()
    {
        if (state == null)
            return false;

        bool changed = renderedVersion != state.Version;
        if (changed)
        {
            Amount.text = state.Amount.ToGameString();
            RebindRequirements();
            renderedVersion = state.Version;
        }
        if (lastFailure == BuildFailure.None && StatusText != null)
            StatusText.text = BuildPrerequisiteStatus();

        return changed;
    }

    private string BuildPrerequisiteStatus()
    {
        var missing = new List<string>();
        if (GameManager.Instance.State.TechLevel < Building.TechLevel)
            missing.Add($"Era: {Building.TechLevel}");

        ResearchManager researchManager = ResearchManager.Instance;
        for (int i = 0; i < Building.RequiredResearch.Count; i++)
        {
            Research research = Building.RequiredResearch[i];
            if (research == null ||
                !researchManager.States.TryGetValue(research, out ResearchState researchState) ||
                researchState.Status != ResearchStatus.Completed)
                missing.Add($"Research: {research?.Label ?? "Missing definition"}");
        }

        WorkshopManager workshopManager = WorkshopManager.Instance;
        for (int i = 0; i < Building.RequiredWorkshopUpgrades.Count; i++)
        {
            WorkshopUpgradeDefinition upgrade = Building.RequiredWorkshopUpgrades[i];
            if (upgrade == null ||
                !workshopManager.States.TryGetValue(upgrade, out WorkshopUpgradeState upgradeState) ||
                !upgradeState.Purchased)
                missing.Add($"Workshop: {upgrade?.Label ?? "Missing definition"}");
        }

        return missing.Count == 0 ? string.Empty : "Missing — " + string.Join("; ", missing);
    }

    private void ShowFailure(BuildFailure failure)
    {
        lastFailure = failure;
        string message = failure switch
        {
            BuildFailure.InvalidAmount => "请输入有效的整数数量",
            BuildFailure.ResourceInsufficient => "资源不足，无法完成建造或拆除",
            BuildFailure.SpaceInsufficient => "领土不足",
            BuildFailure.ProductivityInsufficient => "生产力不足",
            BuildFailure.DeconstructionUnavailable => "没有可拆除的建筑",
            BuildFailure.TechnologyInsufficient => "Technology level insufficient.",
            BuildFailure.ResearchPrerequisiteIncomplete => "Required research is incomplete.",
            BuildFailure.WorkshopPrerequisiteIncomplete => "Required workshop upgrade is incomplete.",
            _ => string.Empty
        };

        if (StatusText != null)
            StatusText.text = message;
        else if (Description != null && !string.IsNullOrEmpty(message))
            Description.text = Building.Description + "\n" + message;
    }

    private void ClearFailure()
    {
        if (lastFailure == BuildFailure.None)
            return;
        lastFailure = BuildFailure.None;
        if (StatusText != null)
            StatusText.text = string.Empty;
        else if (Description != null)
            Description.text = Building.Description;
    }

    private void BindRequirements()
    {
        if (requirementsBound)
            return;

        RebindRequirements();
        requirementsBound = true;
    }

    private void RebindRequirements()
    {
        if (ResourceList == null || BuildResourceReqPrefab == null || Building == null)
            return;

        ExpantaNum nextAmount = ExpantaNum.One;
        ExpantaNum owned = state?.Amount ?? ExpantaNum.Zero;
        int requirementCount = Building.ResourceRequirements.Count;
        bool reuse = ResourceList.childCount == requirementCount;
        if (!reuse)
        {
            for (int i = ResourceList.childCount - 1; i >= 0; i--)
                Destroy(ResourceList.GetChild(i).gameObject);
        }

        int index = 0;
        foreach (Pair<Resource, ExpantaNum> requirement in Building.ResourceRequirements)
        {
            ExpantaNum cost = requirement.Second.GeometricSeriesCost(
                Building.CostGrowth,
                owned,
                nextAmount);
            GameObject go = reuse
                ? ResourceList.GetChild(index).gameObject
                : Instantiate(BuildResourceReqPrefab, ResourceList, false);
            ResourceRequirementView view = go.GetComponent<ResourceRequirementView>();
            if (view != null)
                view.Bind(requirement.First, cost);
            index++;
        }
    }

    private void ApplyCardHeight()
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        // ResourceDisplayer uses a fixed top-origin card. Keep the card's
        // origin stable while its height changes; a centered pivot makes the
        // detail area move upward when the card expands and causes the parent
        // grid to visually flatten it back into the collapsed row.
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);

        float requirementHeight = Mathf.Max(
            50f,
            Mathf.Max(1, Mathf.CeilToInt((Building?.ResourceRequirements.Count ?? 0) / 2f)) * 50f);
        if (ResourceList != null)
        {
            ResourceList.anchorMin = new Vector2(0.5f, 1f);
            ResourceList.anchorMax = new Vector2(0.5f, 1f);
            ResourceList.pivot = new Vector2(0.5f, 1f);
            ResourceList.anchoredPosition = new Vector2(0f, -25f);
            ResourceList.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                IsExpanded ? requirementHeight : 0f);
        }

        if (Details is RectTransform detailsRect)
        {
            detailsRect.anchorMin = new Vector2(0.5f, 1f);
            detailsRect.anchorMax = new Vector2(0.5f, 1f);
            detailsRect.pivot = new Vector2(0.5f, 1f);
            detailsRect.anchoredPosition = new Vector2(0f, -50f);
            detailsRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                IsExpanded ? requirementHeight + 50f : 0f);
        }

        if (Construction != null)
        {
            Construction.anchorMin = new Vector2(0.5f, 1f);
            Construction.anchorMax = new Vector2(0.5f, 1f);
            Construction.pivot = new Vector2(0.5f, 1f);
            Construction.anchoredPosition = new Vector2(
                0f,
                -(25f + (IsExpanded ? requirementHeight : 0f)));
            Construction.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 383f);
            Construction.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50f);
        }

        rectTransform.sizeDelta = new Vector2(
            rectTransform.sizeDelta.x,
            IsExpanded ? 50f + requirementHeight + 50f : 100f);
    }

    private void SetDetailsVisible(bool visible)
    {
        if (Details != null)
            Details.gameObject.SetActive(visible);

        ApplyCardHeight();
    }

}
