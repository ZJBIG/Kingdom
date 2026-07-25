using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingDisplayer : MonoBehaviour
{
    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Description;
    [SerializeField] private TMP_Text StatusText;
    [SerializeField] private TMP_Text Amount;
    [SerializeField] private Image AutoBuildSpirit;
    [SerializeField] private Transform Details;
    [SerializeField] private RectTransform Construction;
    [SerializeField] private RectTransform ResourceList;
    [SerializeField] private Sprite Enable, Disable;
    [SerializeField] private GameObject BuildResourceReqPrefab;

    private BuildingState state;
    private int renderedVersion = -1;
    private bool requirementsBound;
    private BuildFailure lastFailure;
    public Building Building => state?.Definition;

    private void Awake()
    {
        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (ResourceList != null)
        {
            GridLayoutGroup requirementsLayout = ResourceList.GetComponent<GridLayoutGroup>();
            if (requirementsLayout == null)
                requirementsLayout = ResourceList.gameObject.AddComponent<GridLayoutGroup>();
            requirementsLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            requirementsLayout.constraintCount = 2;
            requirementsLayout.cellSize = new Vector2(191.5f, 50f);
        }
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
        Refresh();
    }

    public void DisplayDetails()
    {
        if (Details == null)
            return;
        Details.gameObject.SetActive(!Details.gameObject.activeSelf);
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    public void SwitchAutoBuild() =>
        BuildingManager.Instance.SetAutoBuild(Building, !state.AutoBuild);

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
            Construction.gameObject.SetActive(!state.AutoBuild);
            AutoBuildSpirit.sprite = state.AutoBuild ? Enable : Disable;
            renderedVersion = state.Version;
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

        foreach (Pair<Resource, ExpantaNum> requirement in Building.ResourceRequirements)
        {
            GameObject go = Instantiate(BuildResourceReqPrefab, ResourceList, false);
            ResourceRequirementView view = go.GetComponent<ResourceRequirementView>();
            if (view != null)
                view.Bind(requirement.First, requirement.Second);
        }

        requirementsBound = true;
    }

}
