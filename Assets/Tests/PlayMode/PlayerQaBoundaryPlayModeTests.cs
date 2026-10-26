using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Controlled UI geometry probes; these do not establish natural progression.</summary>
public sealed class PlayerQaBoundaryPlayModeTests
{
    private GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [UnityTest]
    public IEnumerator ResearchGraph_ExpandingViewportKeepsCurrentPositionWithinNewBounds()
    {
        root = new GameObject("PlayerQaBoundaryCanvas", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
        viewport.SetParent(root.transform, false);
        viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
        viewport.sizeDelta = new Vector2(1000f, 700f);
        RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = content.anchorMax = content.pivot = Vector2.zero;
        content.sizeDelta = new Vector2(3000f, 2000f);
        UIResearchGraphGesture graph = viewport.gameObject.AddComponent<UIResearchGraphGesture>();
        Canvas.ForceUpdateCanvases();
        graph.Initialize(viewport, content);
        Vector2 initialMinimum = viewport.rect.size - content.rect.size;
        graph.RestoreView(initialMinimum, 1f);
        Assert.That(Vector2.Distance(content.anchoredPosition, initialMinimum), Is.LessThan(.1f),
            "The controlled graph must begin at its actual right and top movement boundary.");
        Debug.Log($"[PlayerQa] ResearchResize before viewport={viewport.rect.size} content={content.rect.size} position={content.anchoredPosition}");

        viewport.sizeDelta = new Vector2(1400f, 900f);
        Canvas.ForceUpdateCanvases();
        // Exercise the real idle LateUpdate path rather than calling a new test-only API.
        yield return null;
        yield return null;

        Vector2 minimum = viewport.rect.size - content.rect.size * content.localScale.x;
        Vector2 position = content.anchoredPosition;
        Debug.Log($"[PlayerQa] ResearchResize after viewport={viewport.rect.size} content={content.rect.size} position={position} minimum={minimum} overflow=({graph.CanPanHorizontal},{graph.CanPanVertical})");
        Assert.That(graph.CanPanHorizontal && graph.CanPanVertical, Is.True,
            "Both axes must still overflow, so this checks clamping rather than non-scrollable reset.");
        Assert.That(position.x, Is.GreaterThanOrEqualTo(minimum.x - .1f),
            "Expanding the viewport must not leave blank space beyond the right content edge.");
        Assert.That(position.y, Is.GreaterThanOrEqualTo(minimum.y - .1f),
            "Expanding the viewport must not leave blank space beyond the top content edge.");
        Assert.That(position.x, Is.LessThanOrEqualTo(.1f));
        Assert.That(position.y, Is.LessThanOrEqualTo(.1f));
    }
}
