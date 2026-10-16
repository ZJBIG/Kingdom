using System.Collections.Generic;
using UnityEngine;

/// <summary>Top-down tree packed by visible contours; parents center above their children.</summary>
public sealed class ResourceProductionTreeLayout
{
    public readonly struct Placement
    {
        public readonly ResourceProductionTree.Node Node;
        public readonly Vector2 Position;
        public readonly int ParentIndex;
        public Placement(ResourceProductionTree.Node node, Vector2 position, int parentIndex)
        { Node = node; Position = position; ParentIndex = parentIndex; }
    }

    public List<Placement> Nodes { get; } = new();
    public Vector2 Size { get; private set; }
    private readonly ResourceProductionTree tree;
    private readonly Vector2 nodeSize;
    private readonly Vector2 gap;
    private sealed class Branch
    {
        public readonly List<Placement> Nodes = new();
        public readonly List<float> Left = new();
        public readonly List<float> Right = new();
    }

    public ResourceProductionTreeLayout(ResourceProductionTree tree, Vector2 nodeSize, Vector2 gap)
    {
        this.tree = tree; this.nodeSize = nodeSize; this.gap = gap;
        Branch branch = Pack(tree.Root);
        float left = Mathf.Min(branch.Left.ToArray());
        float right = Mathf.Max(branch.Right.ToArray());
        foreach (Placement placement in branch.Nodes)
            Nodes.Add(new Placement(placement.Node, placement.Position - new Vector2(left, 0), placement.ParentIndex));
        Size = new Vector2(right - left, branch.Left.Count * nodeSize.y + (branch.Left.Count - 1) * gap.y);
    }

    private Branch Pack(ResourceProductionTree.Node node)
    {
        Branch result = new();
        result.Nodes.Add(new Placement(node, Vector2.zero, -1));
        result.Left.Add(0); result.Right.Add(nodeSize.x);
        if (!node.Expanded) return result;
        IReadOnlyList<ResourceProductionTree.Node> children = tree.GetChildren(node);
        float firstRoot = 0, lastRoot = 0;
        for (int i = 0; i < children.Count; i++)
        {
            Branch child = Pack(children[i]);
            float shift = 0;
            if (i > 0)
                for (int depth = 0; depth < child.Left.Count && depth + 1 < result.Right.Count; depth++)
                    shift = Mathf.Max(shift, result.Right[depth + 1] + gap.x - child.Left[depth]);
            int start = result.Nodes.Count;
            foreach (Placement placement in child.Nodes)
                result.Nodes.Add(new Placement(placement.Node, placement.Position + new Vector2(shift, nodeSize.y + gap.y),
                    placement.ParentIndex < 0 ? 0 : start + placement.ParentIndex));
            for (int depth = 0; depth < child.Left.Count; depth++)
            {
                float left = child.Left[depth] + shift, right = child.Right[depth] + shift;
                if (depth + 1 == result.Left.Count)
                { result.Left.Add(left); result.Right.Add(right); }
                else
                {
                    result.Left[depth + 1] = Mathf.Min(result.Left[depth + 1], left);
                    result.Right[depth + 1] = Mathf.Max(result.Right[depth + 1], right);
                }
            }
            if (i == 0) firstRoot = child.Nodes[0].Position.x + shift;
            lastRoot = child.Nodes[0].Position.x + shift;
        }
        float rootX = (firstRoot + lastRoot) * .5f;
        result.Nodes[0] = new Placement(node, new Vector2(rootX, 0), -1);
        result.Left[0] = rootX; result.Right[0] = rootX + nodeSize.x;
        return result;
    }
}
