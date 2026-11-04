using System;
using System.Collections.Generic;

/// <summary>Read-only production dependencies. Children are created only when opened.</summary>
public sealed class ResourceProductionTree
{
    public enum Relation { Root, ProductionSource, OngoingInput, Upgrade, SectorFacility, SectorOutput }

    public sealed class Node
    {
        public ResourceDef Resource { get; internal set; }
        public BuildingDef Building { get; internal set; }
        public SectorDef Sector { get; internal set; }
        public ExpantaNum Rate { get; internal set; }
        public bool OneTimeReward { get; internal set; }
        public Node ReferenceTarget { get; internal set; }
        public bool IsCycle => ReferenceTarget != null;
        public bool Expanded { get; set; }
        public Relation Relationship { get; internal set; }
        internal List<Node> Children;
        internal List<Node> VisibleChildren;
        internal Node Parent;
    }

    private readonly IReadOnlyList<BuildingDef> buildings;
    private readonly IReadOnlyList<SectorDef> sectors;
    private readonly Func<ResourceDef, bool> resourceVisible;
    private readonly Func<BuildingDef, bool> buildingVisible;
    private readonly Func<SectorDef, bool> sectorVisible;
    public Node Root { get; }

    public ResourceProductionTree(ResourceDef resource, IReadOnlyList<BuildingDef> buildings,
        IReadOnlyList<SectorDef> sectors, Func<ResourceDef, bool> resourceVisible = null,
        Func<BuildingDef, bool> buildingVisible = null, Func<SectorDef, bool> sectorVisible = null)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        this.buildings = buildings ?? Array.Empty<BuildingDef>();
        this.sectors = sectors ?? Array.Empty<SectorDef>();
        this.resourceVisible = resourceVisible;
        this.buildingVisible = buildingVisible;
        this.sectorVisible = sectorVisible;
        Root = new Node { Resource = resource, Expanded = true };
    }

    /// <summary>A building root traces ongoing inputs and upgrades, never construction costs.</summary>
    public ResourceProductionTree(BuildingDef building, IReadOnlyList<BuildingDef> buildings,
        IReadOnlyList<SectorDef> sectors, Func<ResourceDef, bool> resourceVisible = null,
        Func<BuildingDef, bool> buildingVisible = null, Func<SectorDef, bool> sectorVisible = null)
    {
        if (building == null) throw new ArgumentNullException(nameof(building));
        this.buildings = buildings ?? Array.Empty<BuildingDef>();
        this.sectors = sectors ?? Array.Empty<SectorDef>();
        this.resourceVisible = resourceVisible;
        this.buildingVisible = buildingVisible;
        this.sectorVisible = sectorVisible;
        Root = new Node { Building = building, Expanded = true };
    }

    /// <summary>A sector root groups its local facilities and direct resource outputs.</summary>
    public ResourceProductionTree(SectorDef sector, IReadOnlyList<BuildingDef> buildings,
        IReadOnlyList<SectorDef> sectors, Func<ResourceDef, bool> resourceVisible = null,
        Func<BuildingDef, bool> buildingVisible = null, Func<SectorDef, bool> sectorVisible = null)
    {
        if (sector == null) throw new ArgumentNullException(nameof(sector));
        this.buildings = buildings ?? Array.Empty<BuildingDef>();
        this.sectors = sectors ?? Array.Empty<SectorDef>();
        this.resourceVisible = resourceVisible;
        this.buildingVisible = buildingVisible;
        this.sectorVisible = sectorVisible;
        Root = new Node { Sector = sector, Expanded = true };
    }

    public IReadOnlyList<Node> GetChildren(Node node)
    {
        if (node.Children != null)
        {
            RefreshVisibleChildren(node);
            return node.VisibleChildren;
        }
        node.Children = new List<Node>();
        node.VisibleChildren = node.Children;
        if (node.IsCycle) return node.VisibleChildren;
        if (node.Resource != null)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingDef building = buildings[i];
                if (building == null) continue;
                ExpantaNum output = FindRate(building.ResourceGenerationRates, node.Resource);
                if (output > ExpantaNum.Zero)
                {
                    int index = node.Children.Count;
                    while (index > 0 && node.Children[index - 1].Building.TechLevel < building.TechLevel) index--;
                    node.Children.Insert(index, new Node { Building = building, Rate = output, Parent = node,
                        Relationship = Relation.ProductionSource,
                        ReferenceTarget = FindAncestor(node, null, building) });
                }
            }
            for (int i = 0; i < sectors.Count; i++)
            {
                SectorDef sector = sectors[i];
                if (sector == null) continue;
                ExpantaNum output = FindRate(sector.OccupiedResourceRatesPerSecond, node.Resource);
                if (output > ExpantaNum.Zero)
                    node.Children.Add(new Node { Sector = sector, Rate = output, Parent = node,
                        Relationship = Relation.ProductionSource });
                ExpantaNum reward = FindRate(sector.ResourceRewards, node.Resource);
                if (reward > ExpantaNum.Zero)
                    node.Children.Add(new Node { Sector = sector, Rate = reward, OneTimeReward = true, Parent = node,
                        Relationship = Relation.ProductionSource });
            }
        }
        else if (node.Building != null)
        {
            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> inputs = node.Building.ResourceConsumptionRates;
            for (int i = 0; i < inputs.Count; i++)
            {
                Pair<ResourceDef, ExpantaNum> input = inputs[i];
                if (input.First == null || input.Second <= ExpantaNum.Zero) continue;
                node.Children.Add(new Node { Resource = input.First, Rate = input.Second, Parent = node,
                    Relationship = Relation.OngoingInput, ReferenceTarget = FindAncestor(node, input.First, null) });
            }
            BuildingDef upgrade = node.Building.UpgradeTo;
            if (upgrade != null)
                node.Children.Add(new Node { Building = upgrade, Parent = node,
                    Relationship = Relation.Upgrade,
                    ReferenceTarget = FindAncestor(node, null, upgrade) });
        }
        else if (node.Sector != null && node.Relationship == Relation.Root)
        {
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i] is SectorBuildingDef local && local.Sector == node.Sector)
                    node.Children.Add(new Node { Building = local, Parent = node,
                        Relationship = Relation.SectorFacility });
            AddSectorOutputs(node, node.Sector.OccupiedResourceRatesPerSecond, false);
            AddSectorOutputs(node, node.Sector.ResourceRewards, true);
        }
        RefreshVisibleChildren(node);
        return node.VisibleChildren;
    }

    private static void AddSectorOutputs(Node node, IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates, bool oneTime)
    {
        for (int i = 0; i < rates.Count; i++)
            if (rates[i].First != null && rates[i].Second > ExpantaNum.Zero)
                node.Children.Add(new Node { Resource = rates[i].First, Rate = rates[i].Second, Parent = node,
                    OneTimeReward = oneTime,
                    Relationship = Relation.SectorOutput });
    }

    private static Node FindAncestor(Node node, ResourceDef resource, BuildingDef building)
    {
        for (Node ancestor = node; ancestor != null; ancestor = ancestor.Parent)
            if (resource != null ? ancestor.Resource == resource : ancestor.Building == building)
                return ancestor;
        return null;
    }

    /// <summary>Rechecks cached branches after unlocks without losing expansion state.</summary>
    public bool RefreshVisibility() => RefreshVisibility(Root);

    private bool RefreshVisibility(Node node)
    {
        if (!node.Expanded || node.Children == null) return false;
        bool changed = RefreshVisibleChildren(node);
        for (int i = 0; i < node.VisibleChildren.Count; i++)
            changed |= RefreshVisibility(node.VisibleChildren[i]);
        return changed;
    }

    private bool RefreshVisibleChildren(Node node)
    {
        int visibleIndex = 0;
        bool changed = false;
        for (int i = 0; i < node.Children.Count; i++)
        {
            Node child = node.Children[i];
            if (!IsVisible(child)) continue;
            if (visibleIndex >= node.VisibleChildren.Count || node.VisibleChildren[visibleIndex] != child)
                changed = true;
            visibleIndex++;
        }
        if (!changed && visibleIndex == node.VisibleChildren.Count) return false;
        List<Node> visible = new(visibleIndex);
        for (int i = 0; i < node.Children.Count; i++)
            if (IsVisible(node.Children[i])) visible.Add(node.Children[i]);
        node.VisibleChildren = visible;
        return true;
    }

    private bool IsVisible(Node node) => node.Resource != null
        ? resourceVisible == null || resourceVisible(node.Resource)
        : node.Building != null
            ? buildingVisible == null || buildingVisible(node.Building)
            : sectorVisible == null || sectorVisible(node.Sector);

    private static ExpantaNum FindRate(IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates, ResourceDef resource)
    {
        ExpantaNum result = ExpantaNum.Zero;
        for (int i = 0; i < rates.Count; i++)
            if (rates[i].First == resource && rates[i].Second > ExpantaNum.Zero)
                result += rates[i].Second;
        return result;
    }
}
