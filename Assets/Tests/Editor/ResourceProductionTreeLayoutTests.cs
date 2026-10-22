using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ResourceProductionTreeLayoutTests
{
    private readonly List<Object> created = new();
    private static readonly Vector2 NodeSize = new(205, 104);
    private static readonly Vector2 Gap = new(16, 24);
    private T Make<T>() where T : ScriptableObject
    { T value = ScriptableObject.CreateInstance<T>(); created.Add(value); return value; }
    private BuildingDef Producer(ResourceDef output, ResourceDef input = null)
    {
        BuildingDef value = Make<BuildingDef>();
        value.ConfigureEconomyForEditor(ExpantaNum.One, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new(), new() { new(output, 2) }, input == null ? new() : new() { new(input, 1) });
        return value;
    }
    [TearDown] public void CleanUp()
    { foreach (Object value in created) Object.DestroyImmediate(value); created.Clear(); }

    [Test]
    public void IndependentProductionPageHasAuthoredNavigationViewportAndTools()
    {
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        Transform page = root.transform.Find("SafeAreaRoot/Content/PageHost/Production");
        Transform viewport = page.Find("GraphViewport");
        Assert.That(viewport.GetComponent<UIResearchGraphGesture>(), Is.Not.Null);
        Assert.That(viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
        Assert.That(viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic, Is.False);
        Assert.That(viewport.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null);
        RectTransform content = viewport.Find("GraphContent") as RectTransform;
        Assert.That(Vector2.Distance(content.pivot, Vector2.zero), Is.LessThan(.001f));
        Assert.That(Vector2.Distance(content.anchorMin, Vector2.zero), Is.LessThan(.001f));
        Assert.That(Vector2.Distance(content.anchorMax, Vector2.zero), Is.LessThan(.001f));
        Assert.That(root.transform.Find("SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Production").GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);
        Transform tools = root.transform.Find("SafeAreaRoot/Content/PageTool/ProductionTools");
        Assert.That(tools.Find("Heading").GetComponent<TMPro.TMP_Text>(), Is.Not.Null);
        Assert.That(tools.Find("Reset").GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);
    }

    [Test]
    public void AuthoredNodesAndConnectionsProvideFixedGeometry()
    {
        GameObject node = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceTreeRow");
        RectTransform rect = node.GetComponent<RectTransform>();
        Assert.That(rect.sizeDelta.x, Is.GreaterThan(0));
        Assert.That(rect.sizeDelta.y, Is.GreaterThan(0));
        Assert.That(Vector2.Distance(rect.anchorMin, rect.anchorMax), Is.LessThan(.001f));
        Assert.That(Vector2.Distance(rect.pivot, new Vector2(0, 1)), Is.LessThan(.001f));
        Assert.That(node.GetComponent<UnityEngine.UI.LayoutElement>(), Is.Null);
        GameObject link = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceTreeLink");
        Assert.That(link.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.False);
    }

    [Test]
    public void AlternativeSourcesSitSideBySide_ParentCenteredAboveThem()
    {
        ResourceDef resource = Make<ResourceDef>();
        ResourceProductionTree tree = new(resource, new[] { Producer(resource), Producer(resource), Producer(resource) }, null);
        ResourceProductionTreeLayout layout = new(tree, NodeSize, Gap);
        Assert.That(layout.Nodes.Count, Is.EqualTo(4), "Root and three alternative sources are discrete nodes.");
        Vector2 first = layout.Nodes[1].Position, last = layout.Nodes[3].Position;
        Assert.That(first.y, Is.GreaterThan(layout.Nodes[0].Position.y));
        Assert.That(last.y, Is.EqualTo(first.y).Within(.001f));
        Assert.That(last.x, Is.GreaterThan(first.x));
        float sourceMidpoint = (first.x + last.x + NodeSize.x) * .5f;
        Assert.That(layout.Nodes[0].Position.x + NodeSize.x * .5f, Is.EqualTo(sourceMidpoint).Within(.001f));
    }

    [Test]
    public void CollapsedSiblingUsesOnlyItsVisibleContour_AndRootCollapseUsesOneNode()
    {
        ResourceDef output = Make<ResourceDef>(), a = Make<ResourceDef>(), b = Make<ResourceDef>(), c = Make<ResourceDef>();
        BuildingDef broad = Producer(output);
        broad.ConfigureEconomyForEditor(ExpantaNum.One, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new(), new() { new(output, 2) }, new() { new(a, 1), new(b, 1), new(c, 1) });
        ResourceProductionTree tree = new(output, new[] { broad, Producer(output) }, null);
        tree.GetChildren(tree.Root)[0].Expanded = true;
        ResourceProductionTreeLayout layout = new(tree, NodeSize, Gap);
        float reservedSubtreeWidth = NodeSize.x * 4 + Gap.x * 3;
        Assert.That(layout.Size.x, Is.LessThan(reservedSubtreeWidth - Gap.x),
            "A collapsed sibling can use space above the wider dependency layer.");
        foreach (var left in layout.Nodes)
            foreach (var right in layout.Nodes)
                if (left.Node != right.Node)
                    Assert.That(new Rect(left.Position, NodeSize).Overlaps(new Rect(right.Position, NodeSize)), Is.False);
        tree.Root.Expanded = false;
        layout = new ResourceProductionTreeLayout(tree, NodeSize, Gap);
        Assert.That(layout.Nodes.Count, Is.EqualTo(1), "A collapsed root has one visible node.");
        Assert.That(Vector2.Distance(layout.Size, NodeSize), Is.LessThan(.001f));
    }

    [Test]
    public void ExpandedDependenciesHaveForwardEdgesAndDoNotOverlap_CollapsingRemovesDescendants()
    {
        ResourceDef output = Make<ResourceDef>(), a = Make<ResourceDef>(), b = Make<ResourceDef>();
        ResourceProductionTree tree = new(output, new[] { Producer(output, a), Producer(output, b), Producer(a, b), Producer(b) }, null);
        var sources = tree.GetChildren(tree.Root);
        sources[0].Expanded = sources[1].Expanded = true;
        var input = tree.GetChildren(sources[0])[0]; input.Expanded = true;
        tree.GetChildren(input)[0].Expanded = true;
        ResourceProductionTreeLayout layout = new(tree, NodeSize, Gap);
        for (int i = 0; i < layout.Nodes.Count; i++)
        {
            var node = layout.Nodes[i];
            if (node.ParentIndex >= 0)
                Assert.That(node.Position.y, Is.GreaterThan(layout.Nodes[node.ParentIndex].Position.y + NodeSize.y));
            for (int j = i + 1; j < layout.Nodes.Count; j++)
                Assert.That(new Rect(node.Position, NodeSize).Overlaps(new Rect(layout.Nodes[j].Position, NodeSize)), Is.False);
        }
        int expandedCount = layout.Nodes.Count;
        sources[0].Expanded = false;
        layout = new ResourceProductionTreeLayout(tree, NodeSize, Gap);
        Assert.That(layout.Nodes.Count, Is.LessThan(expandedCount));
        Assert.That(layout.Size.x, Is.GreaterThan(0)); Assert.That(layout.Size.y, Is.GreaterThan(0));
    }
}
