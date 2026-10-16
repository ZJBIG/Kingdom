using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class KingdomPageBoundaryTests
{
    [Test]
    public void ScrollViewportsHaveVisibleFramesWithoutInterceptingInput()
    {
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        foreach (string path in new[] { "SafeAreaRoot/Content/PageHost",
            "SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport",
            "SafeAreaRoot/Content/PageHost/Production/GraphViewport",
            "SafeAreaRoot/Content/PageTool/ResearchQueueViewport",
            "SafeAreaRoot/Content/PageHost/Music/DataRows/MusicSurface/TrackListViewport" })
        {
            Transform frame = root.transform.Find(path + "/ScrollFrame");
            Assert.That(frame, Is.Not.Null, path);
            Assert.That(frame.GetComponent<LayoutElement>().ignoreLayout, Is.True);
            foreach (string side in new[] { "Top", "Bottom", "Left", "Right" })
            {
                Image edge = frame.Find(side).GetComponent<Image>();
                Assert.That(edge.color.a, Is.GreaterThan(.5f), path);
                Assert.That(edge.raycastTarget, Is.False, path);
            }
        }
        Assert.That(Resources.Load<GameObject>("UI/Kingdom/KingdomUIScrollFrame"), Is.Not.Null,
            "The compatibility detail shell instantiates an authored frame.");
    }

    [Test]
    public void PageScrollbarCanRebuildBeforeRuntimePageBinding()
    {
        GameObject root = Object.Instantiate(Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot"));
        try
        {
            ScrollRect scroll = root.transform.Find("SafeAreaRoot/Content/PageHost").GetComponent<ScrollRect>();
            Assert.That(scroll.content, Is.SameAs(scroll.viewport.Find("Overview")),
                "The authored shell needs valid content before its first Canvas rebuild.");
            Assert.DoesNotThrow(() => scroll.verticalScrollbar.Rebuild(CanvasUpdate.Prelayout));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void PageClippingStopsInsideTheVisibleFrame()
    {
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        RectTransform viewport = root.transform.Find("SafeAreaRoot/Content/PageHost") as RectTransform;
        Vector4 padding = viewport.GetComponent<RectMask2D>().padding;
        RectTransform frame = viewport.Find("ScrollFrame") as RectTransform;
        Image left = frame.Find("Left").GetComponent<Image>();
        Image bottom = frame.Find("Bottom").GetComponent<Image>();
        Assert.That(padding.x, Is.GreaterThanOrEqualTo(frame.offsetMin.x + left.rectTransform.rect.width));
        Assert.That(padding.y, Is.GreaterThanOrEqualTo(frame.offsetMin.y + bottom.rectTransform.rect.height));
        Assert.That(padding.z, Is.GreaterThanOrEqualTo(-frame.offsetMax.x + left.rectTransform.rect.width));
        Assert.That(padding.w, Is.GreaterThanOrEqualTo(-frame.offsetMax.y + bottom.rectTransform.rect.height));
        foreach (Image edge in frame.GetComponentsInChildren<Image>())
            Assert.That(edge.maskable, Is.False, "The border remains visible outside the content clip.");
    }

    [Test]
    public void PageAndDecorativeShellsDoNotClipTheirOwnContent()
    {
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        foreach (string path in new[] { "SafeAreaRoot/Content", "SafeAreaRoot/DetailPanel",
            "SafeAreaRoot/Content/PageHost/Overview/PrimaryCard", "SafeAreaRoot/Content/PageHost/Overview/SecondaryCard" })
            AssertNoMask(root.transform.Find(path), path);
        Transform host = root.transform.Find("SafeAreaRoot/Content/PageHost");
        foreach (Transform page in host)
        {
            AssertNoMask(page, page.name);
            Transform rows = page.Find("DataRows");
            if (rows != null) AssertNoMask(rows, page.name + "/DataRows");
        }
        Assert.That(host.GetComponent<RectMask2D>(), Is.Not.Null,
            "The actual outer viewport prevents scrolling content from spilling into navigation.");
    }

    [Test]
    public void ReauthoringRemovesStaleShellMasksAndPreservesRealClippingAndInput()
    {
        GameObject root = Object.Instantiate(Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot"));
        try
        {
            Transform page = root.transform.Find("SafeAreaRoot/Content/PageHost/Overview");
            Transform shell = root.transform.Find("SafeAreaRoot/DetailPanel");
            page.gameObject.AddComponent<RectMask2D>();
            shell.gameObject.AddComponent<Mask>();
            Transform graph = root.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
            Mask stencil = graph.GetComponent<Mask>();
            RectMask2D rectangle = graph.GetComponent<RectMask2D>();
            Image input = graph.GetComponent<Image>();
            Color inputColor = input.color;
            RectTransform viewport = root.transform.Find("SafeAreaRoot/Content/PageHost") as RectTransform;
            Vector2 beforeMin = viewport.offsetMin, beforeMax = viewport.offsetMax;
            Assert.That(KingdomPageBoundaryAuthoring.Apply(root), Is.EqualTo(2), "Two stale shell clips are removed.");
            AssertNoMask(page, "Overview");
            AssertNoMask(shell, "DetailPanel");
            Assert.That(graph.GetComponent<Mask>(), Is.SameAs(stencil));
            Assert.That(graph.GetComponent<RectMask2D>(), Is.SameAs(rectangle));
            Assert.That(stencil.showMaskGraphic, Is.False);
            Assert.That(input.raycastTarget, Is.True);
            Assert.That(input.color, Is.EqualTo(inputColor), "Opaque stencil input geometry remains intact.");
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            Assert.That(scroll.viewport, Is.SameAs(viewport));
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.verticalScrollbar.transform.parent, Is.SameAs(viewport.parent));
            Assert.That(scroll.verticalScrollbarVisibility, Is.EqualTo(ScrollRect.ScrollbarVisibility.AutoHide));
            Assert.That(Vector2.Distance(viewport.offsetMin, beforeMin), Is.LessThan(.01f));
            Assert.That(Vector2.Distance(viewport.offsetMax, beforeMax), Is.LessThan(.01f), "The indicator does not resize the reading area.");
            Assert.That(KingdomPageBoundaryAuthoring.Apply(root), Is.Zero, "The author is idempotent.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void AssertNoMask(Transform shell, string path)
    {
        Assert.That(shell, Is.Not.Null, path);
        Assert.That(shell.GetComponent<Mask>(), Is.Null, path);
        Assert.That(shell.GetComponent<RectMask2D>(), Is.Null, path);
    }

    [Test]
    public void BackgroundReauthoringPreservesContentSizedOverviewAndContextAction()
    {
        GameObject root = Object.Instantiate(Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot"));
        try
        {
            KingdomBackgroundAuthoring.Apply(root);
            Transform overview = root.transform.Find("SafeAreaRoot/Content/PageHost/Overview");
            Transform primary = overview.Find("PrimaryCard");
            Assert.That(primary.GetComponent<VerticalLayoutGroup>().childForceExpandWidth, Is.False,
                "Reauthoring must retain the text-owned width and compact context action.");
            Assert.That(primary.Find("Text").GetComponent<LayoutElement>().flexibleWidth, Is.GreaterThan(0));
            LayoutElement primarySize = primary.GetComponent<LayoutElement>();
            Assert.That(primarySize == null || primarySize.preferredHeight < 0, Is.True,
                "The card height must follow actual content rather than a fixed page height.");
            Assert.That(overview.Find("SecondaryCard").gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("SafeAreaRoot/Content/PageTool/OverviewNavigationToolbar").gameObject.activeSelf, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
