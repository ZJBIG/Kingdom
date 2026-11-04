using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private RectTransform productionViewport;
    private RectTransform productionContent;
    private Transform productionTools;
    private TMP_Text productionHeading;
    private UIResearchGraphGesture productionGesture;
    private ResourceProductionTree productionTree;
    private Vector2 productionSavedPosition;
    private float productionSavedScale = 1f;
    private bool productionViewSaved;
    private Vector2 productionGraphSize;
    private readonly Dictionary<ResourceProductionTree.Node, TMP_Text> productionStatuses = new();
    private readonly Dictionary<ResourceProductionTree.Node, Image> productionSurfaces = new();
    private float nextProductionVisibilityRefresh;
    private readonly HashSet<ResourceDef> productionResources = new();
    private readonly HashSet<BuildingDef> productionBuildings = new();
    private readonly HashSet<SectorDef> productionSectors = new();

#if UNITY_EDITOR
    public void RefreshProductionVisibilityForEditor() => RefreshProductionVisibilityIfNeeded(true);
#endif

    private bool BindProductionPage(Transform content)
    {
        productionViewport = pages["Production"].Find("GraphViewport") as RectTransform;
        productionContent = productionViewport?.Find("GraphContent") as RectTransform;
        productionGesture = productionViewport?.GetComponent<UIResearchGraphGesture>();
        productionTools = content.Find("PageTool/ProductionTools");
        productionHeading = productionTools?.Find("Heading")?.GetComponent<TMP_Text>();
        Button reset = productionTools?.Find("Reset")?.GetComponent<Button>();
        if (productionContent == null || productionGesture == null || productionHeading == null || reset == null)
            return false;
        reset.onClick.RemoveAllListeners();
        reset.onClick.AddListener(CenterProductionRoot);
        productionTools.gameObject.SetActive(false);
        return true;
    }

    private void OpenSelectedProductionTree()
    {
        if (!IsProgressionPageUnlocked("Production")) return;
        if (selectedResource != null) ShowProductionTree(selectedResource);
        else if (selectedBuilding != null) ShowProductionTree(selectedBuilding);
        else if (selectedSectorDefinition != null) ShowProductionTree(selectedSectorDefinition);
    }

    public void ShowProductionTree(ResourceDef resource)
    {
        if (resource == null || !IsProgressionPageUnlocked("Production")) return;
        RefreshProductionVisibility();
        if (!productionResources.Contains(resource)) return;
        ShowResourceDetails(resource);
        SetPage("Production");
        if (productionTree?.Root.Resource == resource) return;
        productionTree = new ResourceProductionTree(resource, DataBase<BuildingDef>.All, DataBase<SectorDef>.All,
            productionResources.Contains, productionBuildings.Contains, productionSectors.Contains);
        RebuildProductionGraph();
        CenterProductionRoot();
    }

    public void ShowProductionTree(BuildingDef building)
    {
        if (building == null || !IsProgressionPageUnlocked("Production")) return;
        RefreshProductionVisibility();
        if (!productionBuildings.Contains(building)) return;
        ShowBuildingDetails(building);
        SetPage("Production");
        if (productionTree?.Root.Building == building) return;
        productionTree = new ResourceProductionTree(building, DataBase<BuildingDef>.All, DataBase<SectorDef>.All,
            productionResources.Contains, productionBuildings.Contains, productionSectors.Contains);
        RebuildProductionGraph();
        CenterProductionRoot();
    }

    public void ShowProductionTree(SectorDef sector)
    {
        if (sector == null || !IsProgressionPageUnlocked("Production")) return;
        RefreshProductionVisibility();
        if (!productionSectors.Contains(sector)) return;
        RefreshSectorDetails(sector);
        SetPage("Production");
        if (productionTree?.Root.Sector == sector) return;
        productionTree = new ResourceProductionTree(sector, DataBase<BuildingDef>.All, DataBase<SectorDef>.All,
            productionResources.Contains, productionBuildings.Contains, productionSectors.Contains);
        RebuildProductionGraph();
        CenterProductionRoot();
    }

    private void PopulateProductionPage()
    {
        RefreshProductionVisibility();
        if (productionTree == null)
        {
            if (selectedBuilding != null && productionBuildings.Contains(selectedBuilding))
                productionTree = new ResourceProductionTree(selectedBuilding, DataBase<BuildingDef>.All, DataBase<SectorDef>.All,
                    productionResources.Contains, productionBuildings.Contains, productionSectors.Contains);
            else if (selectedSectorDefinition != null && productionSectors.Contains(selectedSectorDefinition))
                productionTree = new ResourceProductionTree(selectedSectorDefinition, DataBase<BuildingDef>.All, DataBase<SectorDef>.All,
                    productionResources.Contains, productionBuildings.Contains, productionSectors.Contains);
            else
            {
                ResourceDef root = selectedResource;
                if (!productionResources.Contains(root)) root = null;
                if (root == null)
                    foreach (ResourceDef resource in DataBase<ResourceDef>.All)
                        if (productionResources.Contains(resource)) { root = resource; break; }
                if (root == null)
                {
                    productionHeading.text = "从资源、建筑或星区详情打开生产树";
                    return;
                }
                productionTree = new ResourceProductionTree(root, DataBase<BuildingDef>.All, DataBase<SectorDef>.All,
                    productionResources.Contains, productionBuildings.Contains, productionSectors.Contains);
            }
            RebuildProductionGraph();
            CenterProductionRoot();
        }
        RefreshProductionVisibilityIfNeeded(true);
    }

    private void RebuildProductionGraph()
    {
        // Resolve pending Canvas size changes before sizing the graph and measuring its anchor.
        Canvas.ForceUpdateCanvases();
        ClearProductionGraph();
        Vector2 nodeSize = resourceTreeRowPrefab.GetComponent<RectTransform>().sizeDelta;
        ResourceProductionTreeLayout layout = new(productionTree, nodeSize, new Vector2(16f, 24f));
        productionGraphSize = layout.Size;
        productionContent.sizeDelta = new Vector2(Mathf.Max(productionViewport.rect.width, layout.Size.x + 48f),
            Mathf.Max(productionViewport.rect.height, layout.Size.y + 48f));
        Vector2 margin = new((productionContent.rect.width - layout.Size.x) * .5f, 24f);
        List<RectTransform> nodes = new(layout.Nodes.Count);
        for (int i = 0; i < layout.Nodes.Count; i++)
        {
            ResourceProductionTreeLayout.Placement placement = layout.Nodes[i];
            RectTransform row = CreateProductionNode(placement.Node);
            Vector2 position = placement.Position + margin;
            row.anchoredPosition = new Vector2(position.x, -position.y);
            nodes.Add(row);
            if (placement.ParentIndex < 0) continue;
            Vector2 parent = layout.Nodes[placement.ParentIndex].Position;
            Vector2 child = placement.Position;
            float targetX = child.x - parent.x + nodeSize.x * .5f;
            float middleY = nodeSize.y + 12f;
            AddResourceTreeLink(nodes[placement.ParentIndex], new Vector2(nodeSize.x * .5f, nodeSize.y), new Vector2(nodeSize.x * .5f, middleY));
            AddResourceTreeLink(nodes[placement.ParentIndex], new Vector2(nodeSize.x * .5f, middleY), new Vector2(targetX, middleY));
            AddResourceTreeLink(nodes[placement.ParentIndex], new Vector2(targetX, middleY), new Vector2(targetX, child.y - parent.y));
        }
        productionHeading.text = productionTree.Root.Building != null
            ? productionTree.Root.Building.Label
            : productionTree.Root.Resource != null ? productionTree.Root.Resource.Label : productionTree.Root.Sector.Label;
        productionGesture.Initialize(productionViewport, productionContent, true);
    }

    private RectTransform CreateProductionNode(ResourceProductionTree.Node node)
    {
        GameObject row = KingdomUIPrefabLibrary.Instantiate(resourceTreeRowPrefab, productionContent);
        row.name = "ProductionNode";
        TMP_Text label = row.transform.Find("Label").GetComponent<TMP_Text>();
        TMP_Text status = row.transform.Find("Status").GetComponent<TMP_Text>();
        Image icon = row.transform.Find("Icon").GetComponent<Image>();
        icon.sprite = node.Resource != null ? node.Resource.Sprite : node.Building != null ? node.Building.Icon : node.Sector.Icon;
        icon.enabled = icon.sprite != null;
        if (node.Resource == null)
            BindArtworkIcon(icon, icon.sprite, node.Building != null ? node.Building.Label : node.Sector.Label, true);
        else
        {
            Transform frame = icon.transform.Find("ArtworkFrame");
            if (frame != null) frame.gameObject.SetActive(false);
            Button preview = icon.GetComponent<Button>();
            if (preview != null) preview.enabled = false;
            icon.raycastTarget = false;
        }
        label.text = node.Resource != null ? node.Resource.Label : node.Building != null ? node.Building.Label : node.Sector.Label;
        productionStatuses.Add(node, status);
        Image surface = row.GetComponent<Image>();
        productionSurfaces.Add(node, surface);
        RefreshProductionNode(node, status, surface, resourceTreeRowPrefab.GetComponent<Image>().color);
        Button expand = row.transform.Find("Expand").GetComponent<Button>();
        expand.interactable = node.ReferenceTarget != null || productionTree.GetChildren(node).Count > 0;
        expand.transform.Find("Label").GetComponent<TMP_Text>().text = node.ReferenceTarget != null ? "↩" : !expand.interactable ? "·" : node.Expanded ? "-" : "+";
        expand.onClick.AddListener(() =>
        {
            if (node.ReferenceTarget != null)
            {
                FocusProductionReference(node.ReferenceTarget);
                return;
            }
            Vector3 oldPosition = productionViewport.InverseTransformPoint(row.transform.position);
            float scale = productionContent.localScale.x;
            node.Expanded = !node.Expanded;
            RebuildProductionGraph();
            Vector3 newPosition = productionViewport.InverseTransformPoint(productionStatuses[node].transform.parent.position);
            productionGesture.RestoreView(productionContent.anchoredPosition + (Vector2)(oldPosition - newPosition), scale);
            SaveProductionView();
        });
        label.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (node.ReferenceTarget != null)
            {
                FocusProductionReference(node.ReferenceTarget);
                return;
            }
            if (node.Resource != null) ShowResourceDetails(node.Resource);
            else if (node.Building != null) ShowBuildingDetails(node.Building);
            else RefreshSectorDetails(node.Sector);
        });
        expand.gameObject.AddComponent<UIResearchGraphDragForwarder>();
        label.gameObject.AddComponent<UIResearchGraphDragForwarder>();
        return row.GetComponent<RectTransform>();
    }

    private void FocusProductionReference(ResourceProductionTree.Node target)
    {
        if (!productionStatuses.TryGetValue(target, out TMP_Text status) || status == null)
            return;
        Vector2 position = productionViewport.InverseTransformPoint(status.transform.parent.position);
        Vector2 center = productionViewport.rect.center;
        productionGesture.RestoreView(productionContent.anchoredPosition + center - position,
            productionContent.localScale.x);
        SaveProductionView();
    }

    private static void RefreshProductionNode(ResourceProductionTree.Node node, TMP_Text status, Image surface, Color normalColor)
    {
        BuildingManager buildings = BuildingManager.Instance;
        bool superseded = node.Building != null && buildings != null &&
            buildings.ArePrerequisitesMet(node.Building, out _) && !buildings.CanConstructNew(node.Building);
        surface.color = superseded ? new Color(.32f, .32f, .32f, normalColor.a) : normalColor;
        status.text = superseded ? "已被替代 · 不可新建" : ProductionNodeStatus(node);
    }

    private static string ProductionNodeStatus(ResourceProductionTree.Node node)
    {
        if (node.ReferenceTarget != null)
            return "参见本链「" + (node.Resource != null ? node.Resource.Label : node.Building.Label) + "」";
        if (node.OneTimeReward) return "一次性占领奖励";
        return node.Relationship switch
        {
            ResourceProductionTree.Relation.Upgrade => "升级建筑",
            ResourceProductionTree.Relation.ProductionSource => "生产来源",
            ResourceProductionTree.Relation.OngoingInput => "持续投入",
            ResourceProductionTree.Relation.SectorFacility => "星区建筑",
            ResourceProductionTree.Relation.SectorOutput => "占领持续产出",
            _ => node.Resource != null ? "资源" : node.Building != null ? "建筑" : "星区"
        };
    }

    private void RefreshProductionVisibilityIfNeeded(bool force = false)
    {
        if (productionTree == null || productionGesture != null && productionGesture.IsDragging) return;
        if (!force && Time.unscaledTime < nextProductionVisibilityRefresh) return;
        nextProductionVisibilityRefresh = Time.unscaledTime + .5f;
        RefreshProductionVisibility();
        RefreshProductionEntry();
        if (productionTree.Root.Resource != null ? !productionResources.Contains(productionTree.Root.Resource)
            : productionTree.Root.Building != null ? !productionBuildings.Contains(productionTree.Root.Building)
            : !productionSectors.Contains(productionTree.Root.Sector))
        {
            productionTree = null;
            productionViewSaved = false;
            ClearProductionGraph();
            PopulateProductionPage();
            return;
        }
        if (productionTree.RefreshVisibility())
        {
            Vector3 before = productionViewport.InverseTransformPoint(productionStatuses[productionTree.Root].transform.parent.position);
            float scale = productionContent.localScale.x;
            RebuildProductionGraph();
            Vector3 after = productionViewport.InverseTransformPoint(productionStatuses[productionTree.Root].transform.parent.position);
            productionGesture.RestoreView(productionContent.anchoredPosition + (Vector2)(before - after), scale);
            SaveProductionView();
        }
        Color normalColor = resourceTreeRowPrefab.GetComponent<Image>().color;
        foreach (var entry in productionStatuses)
            RefreshProductionNode(entry.Key, entry.Value, productionSurfaces[entry.Key], normalColor);
    }

    private void RefreshProductionVisibility()
    {
        productionResources.Clear();
        productionBuildings.Clear();
        productionSectors.Clear();
        ResourceManager resources = ResourceManager.Instance;
        if (resources != null)
            foreach (var entry in resources.States)
                if (entry.Value.Amount > ExpantaNum.Zero || entry.Value.ProductionRate > ExpantaNum.Zero ||
                    entry.Value.ConsumptionRate > ExpantaNum.Zero)
                    productionResources.Add(entry.Key);
        BuildingManager buildings = BuildingManager.Instance;
        if (buildings != null && GameManager.Instance != null)
            foreach (BuildingDef building in DataBase<BuildingDef>.All)
            {
                if (building == null) continue;
                bool built = buildings.States.TryGetValue(building, out BuildingState state) && state.Amount > ExpantaNum.Zero;
                if (!built && !buildings.ArePrerequisitesMet(building, out _)) continue;
                productionBuildings.Add(building);
                AddProductionResources(building.ResourceConsumptionRates);
                AddProductionResources(building.ResourceGenerationRates);
            }
        SectorManager sectors = GameManager.Instance?.Sectors;
        if (sectors != null)
            foreach (var entry in sectors.States)
            {
                if (!entry.Value.Unlocked && !entry.Value.Occupied) continue;
                productionSectors.Add(entry.Key);
                AddProductionResources(entry.Key.OccupiedResourceRatesPerSecond);
                AddProductionResources(entry.Key.ResourceRewards);
            }
    }

    private void AddProductionResources(IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates)
    {
        for (int i = 0; i < rates.Count; i++)
            if (rates[i].First != null && rates[i].Second > ExpantaNum.Zero)
                productionResources.Add(rates[i].First);
    }

    private void ClearProductionGraph()
    {
        productionStatuses.Clear();
        productionSurfaces.Clear();
        for (int i = productionContent.childCount - 1; i >= 0; i--)
        {
            GameObject old = productionContent.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }
    }

    private void CenterProductionRoot()
    {
        if (productionTree == null || !productionStatuses.TryGetValue(productionTree.Root, out TMP_Text rootStatus)) return;
        RectTransform root = (RectTransform)rootStatus.transform.parent;
        float scale = Mathf.Clamp(Mathf.Min(productionViewport.rect.width / (productionGraphSize.x + 48f),
            productionViewport.rect.height / (productionGraphSize.y + 48f)), 1f, 2f);
        productionGesture.RestoreView(new Vector2(productionViewport.rect.width * .5f - (root.anchoredPosition.x + root.rect.width * .5f) * scale,
            productionViewport.rect.height - productionContent.rect.height * scale), scale);
        SaveProductionView();
    }

    private void SaveProductionView()
    {
        if (productionContent == null || productionTree == null) return;
        productionSavedPosition = productionContent.anchoredPosition;
        productionSavedScale = productionContent.localScale.x;
        productionViewSaved = true;
    }

    private void RestoreProductionView()
    {
        if (productionViewSaved) productionGesture?.RestoreView(productionSavedPosition, productionSavedScale);
    }
}
