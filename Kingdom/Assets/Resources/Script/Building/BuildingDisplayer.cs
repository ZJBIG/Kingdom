using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingDisplayer : MonoBehaviour
{
    public const float HeaderHeight = 50f;
    public const float DetailRowHeight = 56f;
    public const float ActionRowHeight = 56f;

    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Description;
    [SerializeField] private TMP_Text Amount;
    [SerializeField] private Transform Details;
    [SerializeField] private RectTransform Construction;
    [SerializeField] private RectTransform ResourceList;
    [SerializeField] private GameObject BuildResourceReqPrefab;
    [SerializeField] private TMP_Text PrimaryActionLabel;

    private BuildingState state;
    private int renderedVersion = -1;
    private int renderedTargetVersion = -1;
    private bool requirementsBound;
    private BuildFailure lastFailure;
    private Building upgradeTarget;
    private readonly List<Pair<Resource, ExpantaNum>> upgradeResourceDeltas = new();
    public Building Building => state?.Definition;
    public bool IsExpanded => Details != null && Details.gameObject.activeSelf;
    public float PreferredHeight => CalculatePreferredHeight(
        IsExpanded,
        DisplayedRequirementCount);
    private bool IsUpgradeMode => upgradeTarget != null && state.Amount > ExpantaNum.Zero;
    private int DisplayedRequirementCount => IsUpgradeMode
        ? upgradeResourceDeltas.Count
        : Building?.ResourceRequirements.Count ?? 0;

    public static float CalculatePreferredHeight(bool expanded, int requirementCount)
    {
        if (!expanded)
            return HeaderHeight;

        int requirementRows = Mathf.Max(
            1,
            Mathf.CeilToInt(Mathf.Max(0, requirementCount) / 2f));
        return HeaderHeight +
            requirementRows * DetailRowHeight +
            ActionRowHeight;
    }

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
            requirementsLayout.cellSize = new Vector2(191.5f, DetailRowHeight);
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
        RefreshActionMode();
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

        bool succeeded = IsUpgradeMode
            ? BuildingManager.Instance.TryUpgrade(Building, amount, out BuildFailure failure)
            : BuildingManager.Instance.TryBuild(Building, amount, out failure);
        if (succeeded)
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

        bool actionChanged = RefreshActionMode();
        int targetVersion = upgradeTarget == null
            ? -1
            : BuildingManager.Instance.GetState(upgradeTarget).Version;
        bool changed =
            actionChanged ||
            renderedVersion != state.Version ||
            renderedTargetVersion != targetVersion;
        if (changed)
        {
            Amount.text = state.Amount.ToGameString();
            RebindRequirements();
            renderedVersion = state.Version;
            renderedTargetVersion = targetVersion;
            ApplyCardHeight();
        }
        return changed;
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
            BuildFailure.BuildingTierSuperseded => "该层级已被更高级建筑取代，只能升级或拆除。",
            BuildFailure.UpgradeUnavailable => "当前没有可用的建筑升级。",
            _ => string.Empty
        };
        if (Description != null && !string.IsNullOrEmpty(message))
            Description.text = Building.Description + "\n" + message;
    }

    private void ClearFailure()
    {
        if (lastFailure == BuildFailure.None)
            return;
        lastFailure = BuildFailure.None;
        if (Description != null)
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
        if (IsUpgradeMode)
            BuildingManager.Instance.GetUpgradeResourceDeltas(
                Building,
                nextAmount,
                upgradeResourceDeltas);
        else
            upgradeResourceDeltas.Clear();
        int requirementCount = DisplayedRequirementCount;
        bool reuse = ResourceList.childCount == requirementCount;
        if (!reuse)
        {
            for (int i = ResourceList.childCount - 1; i >= 0; i--)
                Destroy(ResourceList.GetChild(i).gameObject);
        }

        int index = 0;
        if (IsUpgradeMode)
        {
            for (int i = 0; i < upgradeResourceDeltas.Count; i++)
            {
                GameObject go = reuse
                    ? ResourceList.GetChild(index).gameObject
                    : Instantiate(BuildResourceReqPrefab, ResourceList, false);
                ResourceRequirementView view = go.GetComponent<ResourceRequirementView>();
                if (view != null)
                    view.BindUpgradeDelta(
                        upgradeResourceDeltas[i].First,
                        upgradeResourceDeltas[i].Second);
                index++;
            }
        }
        else
        {
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
    }

    private bool RefreshActionMode()
    {
        Building previousTarget = upgradeTarget;
        upgradeTarget = null;
        if (state != null && state.Amount > ExpantaNum.Zero)
            BuildingManager.Instance.TryGetUnlockedUpgradeTarget(Building, out upgradeTarget);

        if (PrimaryActionLabel != null)
            PrimaryActionLabel.text = upgradeTarget == null
                ? "建造"
                : "升级为" + upgradeTarget.Label;
        if (previousTarget == upgradeTarget)
            return false;

        renderedTargetVersion = -1;
        return true;
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
            DetailRowHeight,
            Mathf.Max(
                1,
                Mathf.CeilToInt(DisplayedRequirementCount / 2f)) *
                DetailRowHeight);
        if (ResourceList != null)
        {
            ResourceList.anchorMin = new Vector2(0.5f, 1f);
            ResourceList.anchorMax = new Vector2(0.5f, 1f);
            ResourceList.pivot = new Vector2(0.5f, 1f);
            ResourceList.anchoredPosition = Vector2.zero;
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
                -(IsExpanded ? requirementHeight : 0f));
            Construction.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 383f);
            Construction.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                ActionRowHeight);
        }

        rectTransform.sizeDelta = new Vector2(
            rectTransform.sizeDelta.x,
            IsExpanded
                ? HeaderHeight + requirementHeight + ActionRowHeight
                : HeaderHeight);
    }

    private void SetDetailsVisible(bool visible)
    {
        if (Details != null)
            Details.gameObject.SetActive(visible);

        ApplyCardHeight();
    }

}
