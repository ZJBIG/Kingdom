using System;
using System.Collections.Generic;

/// <summary>Read-only production dependencies. Children are created only when opened.</summary>
public sealed class ResourceProductionTree
{
    public enum Relation { Root, ProductionSource, OngoingInput, Upgrade }

    public sealed class Node
    {
        public Resource Resource { get; internal set; }
        public Building Building { get; internal set; }
        public SectorDefinition Sector { get; internal set; }
        public ExpantaNum Rate { get; internal set; }
        public bool OneTimeReward { get; internal set; }
        public bool IsCycle { get; internal set; }
        public bool Expanded { get; set; }
        public Relation Relationship { get; internal set; }
        internal List<Resource> Ancestors;
        internal List<Building> BuildingAncestors;
        internal List<Node> Children;
        internal List<Node> VisibleChildren;
    }

    private readonly IReadOnlyList<Building> buildings;
    private readonly IReadOnlyList<SectorDefinition> sectors;
    private readonly Func<Resource, bool> resourceVisible;
    private readonly Func<Building, bool> buildingVisible;
    private readonly Func<SectorDefinition, bool> sectorVisible;
    public Node Root { get; }

    public ResourceProductionTree(Resource resource, IReadOnlyList<Building> buildings,
        IReadOnlyList<SectorDefinition> sectors, Func<Resource, bool> resourceVisible = null,
        Func<Building, bool> buildingVisible = null, Func<SectorDefinition, bool> sectorVisible = null)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        this.buildings = buildings ?? Array.Empty<Building>();
        this.sectors = sectors ?? Array.Empty<SectorDefinition>();
        this.resourceVisible = resourceVisible;
        this.buildingVisible = buildingVisible;
        this.sectorVisible = sectorVisible;
        Root = new Node { Resource = resource, Ancestors = new List<Resource>(), BuildingAncestors = new List<Building>(), Expanded = true };
    }

    /// <summary>A building root traces ongoing inputs and upgrades, never construction costs.</summary>
    public ResourceProductionTree(Building building, IReadOnlyList<Building> buildings,
        IReadOnlyList<SectorDefinition> sectors, Func<Resource, bool> resourceVisible = null,
        Func<Building, bool> buildingVisible = null, Func<SectorDefinition, bool> sectorVisible = null)
    {
        if (building == null) throw new ArgumentNullException(nameof(building));
        this.buildings = buildings ?? Array.Empty<Building>();
        this.sectors = sectors ?? Array.Empty<SectorDefinition>();
        this.resourceVisible = resourceVisible;
        this.buildingVisible = buildingVisible;
        this.sectorVisible = sectorVisible;
        Root = new Node { Building = building, Ancestors = new List<Resource>(), BuildingAncestors = new List<Building>(), Expanded = true };
    }

    public IReadOnlyList<Node> GetChildren(Node node)
    {
        if (node.Children != null) return node.VisibleChildren;
        node.Children = new List<Node>();
        node.VisibleChildren = node.Children;
        if (node.IsCycle) return node.VisibleChildren;
        if (node.Resource != null)
        {
            List<Resource> path = new(node.Ancestors) { node.Resource };
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building == null) continue;
                ExpantaNum output = FindRate(building.ResourceGenerationRates, node.Resource);
                if (output > ExpantaNum.Zero)
                    node.Children.Add(new Node { Building = building, Rate = output, Ancestors = path,
                        BuildingAncestors = node.BuildingAncestors, Relationship = Relation.ProductionSource,
                        IsCycle = node.BuildingAncestors.Contains(building) });
            }
            for (int i = 0; i < sectors.Count; i++)
            {
                SectorDefinition sector = sectors[i];
                if (sector == null) continue;
                ExpantaNum output = FindRate(sector.OccupiedResourceRatesPerSecond, node.Resource);
                if (output > ExpantaNum.Zero)
                    node.Children.Add(new Node { Sector = sector, Rate = output, Ancestors = path,
                        BuildingAncestors = node.BuildingAncestors, Relationship = Relation.ProductionSource });
                ExpantaNum reward = FindRate(sector.ResourceRewards, node.Resource);
                if (reward > ExpantaNum.Zero)
                    node.Children.Add(new Node { Sector = sector, Rate = reward, OneTimeReward = true, Ancestors = path,
                        BuildingAncestors = node.BuildingAncestors, Relationship = Relation.ProductionSource });
            }
        }
        else if (node.Building != null)
        {
            List<Building> buildingPath = new(node.BuildingAncestors) { node.Building };
            IReadOnlyList<Pair<Resource, ExpantaNum>> inputs = node.Building.ResourceConsumptionRates;
            for (int i = 0; i < inputs.Count; i++)
            {
                Pair<Resource, ExpantaNum> input = inputs[i];
                if (input.First == null || input.Second <= ExpantaNum.Zero) continue;
                node.Children.Add(new Node { Resource = input.First, Rate = input.Second,
                    Ancestors = node.Ancestors, BuildingAncestors = buildingPath,
                    Relationship = Relation.OngoingInput, IsCycle = node.Ancestors.Contains(input.First) });
            }
            Building upgrade = node.Building.UpgradeTo;
            if (upgrade != null)
                node.Children.Add(new Node { Building = upgrade, Ancestors = node.Ancestors,
                    BuildingAncestors = buildingPath, Relationship = Relation.Upgrade,
                    IsCycle = buildingPath.Contains(upgrade) });
        }
        RefreshVisibleChildren(node);
        return node.VisibleChildren;
    }

    /// <summary>Rechecks cached branches after unlocks without losing expansion state.</summary>
    public bool RefreshVisibility() => RefreshVisibility(Root);

    private bool RefreshVisibility(Node node)
    {
        if (node.Children == null) return false;
        bool changed = RefreshVisibleChildren(node);
        for (int i = 0; i < node.Children.Count; i++)
            changed |= RefreshVisibility(node.Children[i]);
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

    private static ExpantaNum FindRate(IReadOnlyList<Pair<Resource, ExpantaNum>> rates, Resource resource)
    {
        ExpantaNum result = ExpantaNum.Zero;
        for (int i = 0; i < rates.Count; i++)
            if (rates[i].First == resource && rates[i].Second > ExpantaNum.Zero)
                result += rates[i].Second;
        return result;
    }
}
