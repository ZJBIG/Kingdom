using System;
using System.Collections.Generic;

/// <summary>Read-only production dependencies. Children are created only when opened.</summary>
public sealed class ResourceProductionTree
{
    public sealed class Node
    {
        public Resource Resource { get; internal set; }
        public Building Building { get; internal set; }
        public SectorDefinition Sector { get; internal set; }
        public ExpantaNum Rate { get; internal set; }
        public bool OneTimeReward { get; internal set; }
        public bool IsCycle { get; internal set; }
        public bool Expanded { get; set; }
        internal List<Resource> Ancestors;
        internal List<Node> Children;
    }

    private readonly IReadOnlyList<Building> buildings;
    private readonly IReadOnlyList<SectorDefinition> sectors;
    public Node Root { get; }

    public ResourceProductionTree(Resource resource, IReadOnlyList<Building> buildings,
        IReadOnlyList<SectorDefinition> sectors)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        this.buildings = buildings ?? Array.Empty<Building>();
        this.sectors = sectors ?? Array.Empty<SectorDefinition>();
        Root = new Node { Resource = resource, Ancestors = new List<Resource>(), Expanded = true };
    }

    public IReadOnlyList<Node> GetChildren(Node node)
    {
        if (node.Children != null) return node.Children;
        node.Children = new List<Node>();
        if (node.IsCycle) return node.Children;
        if (node.Resource != null)
        {
            List<Resource> path = new(node.Ancestors) { node.Resource };
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building == null) continue;
                ExpantaNum output = FindRate(building.ResourceGenerationRates, node.Resource);
                if (output > ExpantaNum.Zero)
                    node.Children.Add(new Node { Building = building, Rate = output, Ancestors = path });
            }
            for (int i = 0; i < sectors.Count; i++)
            {
                SectorDefinition sector = sectors[i];
                if (sector == null) continue;
                ExpantaNum output = FindRate(sector.OccupiedResourceRatesPerSecond, node.Resource);
                if (output > ExpantaNum.Zero)
                    node.Children.Add(new Node { Sector = sector, Rate = output, Ancestors = path });
                ExpantaNum reward = FindRate(sector.ResourceRewards, node.Resource);
                if (reward > ExpantaNum.Zero)
                    node.Children.Add(new Node { Sector = sector, Rate = reward, OneTimeReward = true, Ancestors = path });
            }
        }
        else if (node.Building != null)
        {
            IReadOnlyList<Pair<Resource, ExpantaNum>> inputs = node.Building.ResourceConsumptionRates;
            for (int i = 0; i < inputs.Count; i++)
            {
                Pair<Resource, ExpantaNum> input = inputs[i];
                if (input.First == null || input.Second <= ExpantaNum.Zero) continue;
                node.Children.Add(new Node { Resource = input.First, Rate = input.Second,
                    Ancestors = node.Ancestors, IsCycle = node.Ancestors.Contains(input.First) });
            }
        }
        return node.Children;
    }

    private static ExpantaNum FindRate(IReadOnlyList<Pair<Resource, ExpantaNum>> rates, Resource resource)
    {
        ExpantaNum result = ExpantaNum.Zero;
        for (int i = 0; i < rates.Count; i++)
            if (rates[i].First == resource && rates[i].Second > ExpantaNum.Zero)
                result += rates[i].Second;
        return result;
    }
}
