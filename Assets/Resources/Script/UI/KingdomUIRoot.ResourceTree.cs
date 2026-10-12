using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private RectTransform resourceTreeSurface;
    private RectTransform resourceTreeRows;
    private Button resourceTreeModeButton;
    private TMP_Text resourceTreeModeLabel;
    private GameObject resourceTreeRowPrefab;
    private ResourceProductionTree resourceTree;
    private bool resourceTreeVisible;

    private void BindResourceTree()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceTree");
        resourceTreeRowPrefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceTreeRow");
        if (prefab == null || resourceTreeRowPrefab == null)
        {
            Debug.LogError("Missing authored resource production tree prefabs.");
            return;
        }
        resourceTreeSurface = Instantiate(prefab, detailScrollContent, false).GetComponent<RectTransform>();
        resourceTreeSurface.name = "ResourceProductionTree";
        resourceTreeRows = resourceTreeSurface.Find("Rows") as RectTransform;
        resourceTreeModeButton = resourceTreeSurface.Find("Mode").GetComponent<Button>();
        resourceTreeModeLabel = resourceTreeModeButton.transform.Find("Label").GetComponent<TMP_Text>();
        resourceTreeModeButton.onClick.AddListener(ToggleResourceTree);
        resourceTreeModeButton.gameObject.AddComponent<UIResourceTreeDetailDrag>().Bind(requirementGesture);
        resourceTreeSurface.gameObject.SetActive(false);
    }

    private void ToggleResourceTree()
    {
        if (selectedResource == null) return;
        resourceTreeVisible = !resourceTreeVisible;
        if (resourceTreeVisible)
        {
            if (resourceTree == null || resourceTree.Root.Resource != selectedResource)
                resourceTree = new ResourceProductionTree(selectedResource, DataBase<Building>.All, DataBase<SectorDefinition>.All);
            RefreshResourceTreeVisibility();
            RebuildResourceTree();
        }
        else RefreshResourceDetails(selectedResource);
        RefreshResourceTreeVisibility();
        requirementGesture?.SetNormalizedPosition(1f);
    }

    private void RefreshResourceTreeVisibility()
    {
        if (resourceTreeSurface == null) return;
        bool hasResource = selectedResource != null;
        if (!hasResource)
        {
            resourceTreeVisible = false;
            resourceTree = null;
        }
        else if (resourceTree != null && resourceTree.Root.Resource != selectedResource)
        {
            resourceTreeVisible = false;
            resourceTree = null;
        }
        resourceTreeSurface.gameObject.SetActive(hasResource);
        resourceTreeRows.gameObject.SetActive(hasResource && resourceTreeVisible);
        detailBody.gameObject.SetActive(!hasResource || !resourceTreeVisible);
        resourceTreeModeLabel.text = resourceTreeVisible ? "返回资源详情" : "查看递归生产链";
    }

    private void RebuildResourceTree()
    {
        for (int i = resourceTreeRows.childCount - 1; i >= 0; i--)
        {
            GameObject old = resourceTreeRows.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }
        AppendResourceTreeNode(resourceTree.Root, 0);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(resourceTreeRows);
        float height = resourceTreeRows.rect.height + 80f;
        resourceTreeSurface.sizeDelta = new Vector2(0, height);
        detailScrollContent.sizeDelta = new Vector2(0, Mathf.Max(detailScrollViewport.rect.height, height + 24f));
    }

    private void AppendResourceTreeNode(ResourceProductionTree.Node node, int depth)
    {
        GameObject row = Instantiate(resourceTreeRowPrefab, resourceTreeRows, false);
        row.name = "ProductionNode";
        TechLevel era = node.Resource != null ? node.Resource.TechLevel : node.Building != null ? node.Building.TechLevel :
            node.Sector.IsHomeSystem ? TechLevel.Spacer : TechLevel.Ultra;
        ApplyEraRowStyle(row, era, resourceTreeRows.childCount - 1);
        IReadOnlyList<ResourceProductionTree.Node> children = resourceTree.GetChildren(node);
        TMP_Text label = row.transform.Find("Label").GetComponent<TMP_Text>();
        TMP_Text status = row.transform.Find("Status").GetComponent<TMP_Text>();
        Button expand = row.transform.Find("Expand").GetComponent<Button>();
        Button navigate = label.GetComponent<Button>();
        Image icon = row.transform.Find("Icon").GetComponent<Image>();
        icon.sprite = node.Resource == null ? null : node.Resource.Sprite;
        icon.enabled = icon.sprite != null;
        label.text = node.Resource != null ? node.Resource.Label :
            node.Building != null ? node.Building.Label : node.Sector.Label;
        // Keep long chains inside the narrow panel; the depth remains explicit after the indent stops growing.
        label.rectTransform.offsetMin = new Vector2(96f + Mathf.Min(depth, 4) * 18f, 48f);
        status.text = ResourceTreeNodeStatus(node, depth, children.Count);
        expand.interactable = children.Count > 0;
        expand.transform.Find("Label").GetComponent<TMP_Text>().text = children.Count == 0 ? "·" : node.Expanded ? "-" : "+";
        expand.onClick.AddListener(() =>
        {
            float position = requirementGesture.GetNormalizedPosition();
            node.Expanded = !node.Expanded;
            RebuildResourceTree();
            requirementGesture.SetNormalizedPosition(position);
        });
        navigate.onClick.AddListener(() =>
        {
            if (node.Resource != null)
            {
                resourceTreeVisible = false;
                ShowResourceDetails(node.Resource);
            }
            else if (node.Building != null) NavigateToBuildingRow(node.Building);
            else { SetPage("Sectors"); RefreshSectorDetails(node.Sector); }
        });
        expand.gameObject.AddComponent<UIResourceTreeDetailDrag>().Bind(requirementGesture);
        navigate.gameObject.AddComponent<UIResourceTreeDetailDrag>().Bind(requirementGesture);
        if (!node.Expanded) return;
        for (int i = 0; i < children.Count; i++) AppendResourceTreeNode(children[i], depth + 1);
    }

    private static string ResourceTreeNodeStatus(ResourceProductionTree.Node node, int depth, int children)
    {
        string prefix = depth > 4 ? "层级 " + depth + " · " : string.Empty;
        if (node.IsCycle) return prefix + "循环依赖 · 此路径停止展开";
        if (node.Resource != null)
            return prefix + (node.Rate > ExpantaNum.Zero ? "基础消耗 " + node.Rate.ToGameString() + "/s · " : string.Empty) +
                (children > 0 ? children + " 个来源 · 点 + 展开" : "无常规建筑 / 星区来源");
        if (node.Building != null)
        {
            BuildingManager manager = BuildingManager.Instance;
            string availability = manager != null && manager.States.TryGetValue(node.Building, out BuildingState state) && state.Amount > ExpantaNum.Zero
                ? "已建 " + state.Amount.ToGameString() : manager != null && manager.ShouldDisplay(node.Building) ? "已解锁" : "待解锁";
            return prefix + availability + " · " + node.Building.TechLevel.GetDescription() + " · 基础产出 " + node.Rate.ToGameString() + "/s" +
                (children == 0 ? " · 无库存原料" : " · " + children + " 种原料");
        }
        return prefix + (node.OneTimeReward ? "占领奖励 " + node.Rate.ToGameString() : "占领后持续产出 " + node.Rate.ToGameString() + "/s");
    }

#if UNITY_EDITOR
    public void ToggleResourceTreeForEditor() => ToggleResourceTree();
#endif
}
