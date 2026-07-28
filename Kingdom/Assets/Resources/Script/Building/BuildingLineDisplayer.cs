using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Building-line card based on BuildingDisplayer. It renders the current tier and
/// next definition without introducing a second runtime authority or upgrade state.
/// </summary>
public sealed class BuildingLineDisplayer : MonoBehaviour
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

    public Building Building => state?.Definition;

    public void Bind(BuildingState newState)
    {
        state = newState ?? throw new ArgumentNullException(nameof(newState));
        renderedVersion = -1;
        Refresh();
    }

    public bool Refresh()
    {
        if (state == null)
            return false;

        bool changed = renderedVersion != state.Version;
        if (changed)
        {
            if (Label != null)
                Label.text = state.Definition.Label;
            if (Amount != null)
                Amount.text = state.Amount.ToGameString();
            renderedVersion = state.Version;
        }

        if (Description != null)
        {
            Building next = state.Definition.UpgradeTo;
            string nextText = next == null ? "已是当前谱系最高层级" : "下一层级：" + next.Label;
            Description.text = state.Definition.Description
                + "\n当前层级：" + state.Definition.Label
                + "\n" + nextText;
        }

        if (StatusText != null)
            StatusText.text = string.Empty;
        return changed;
    }

    public void DisplayDetails()
    {
        if (Details == null)
            return;
        Details.gameObject.SetActive(!Details.gameObject.activeSelf);
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    public void TryConstruct(string input)
    {
        if (state == null || !BuildingTransactionRules.TryNormalizePositiveWhole(input, out ExpantaNum amount))
            return;
        BuildingManager.Instance.TryBuild(Building, amount, out _);
    }

    public void TryDeconstruct(string input)
    {
        if (state == null || !BuildingTransactionRules.TryNormalizePositiveWhole(input, out ExpantaNum amount))
            return;
        BuildingManager.Instance.TryDeconstruct(Building, amount, out _);
    }
}
