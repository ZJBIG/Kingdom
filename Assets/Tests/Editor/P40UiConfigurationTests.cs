using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class P40UiConfigurationTests
{
    [TestCase("")]
    [TestCase("EraCards/Animal/")]
    [TestCase("EraCards/StoneAge/")]
    [TestCase("EraCards/Medieval/")]
    [TestCase("EraCards/Industrial/")]
    [TestCase("EraCards/Spacer/")]
    [TestCase("EraCards/Ultra/")]
    [TestCase("EraCards/Archotech/")]
    public void BuildingCards_AuthorInactiveRecommendationStarsWithoutInterceptingInput(string directory)
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/" + directory + "KingdomUIBuildingCard");
        Image star = prefab.transform.Find("RecommendationStar")?.GetComponent<Image>();
        Assert.That(star, Is.Not.Null);
        Assert.That(star.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Status/BuildingRecommendationStar")));
        Assert.That(star.gameObject.activeSelf, Is.False);
        Assert.That(star.raycastTarget, Is.False);
        Assert.That(star.preserveAspect, Is.True);
    }

    [Test]
    public void OverviewNavigationToolbarPrefabProvidesAuthoredTargetButton()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIOverviewNavigationToolbar");
        AssertAuthoredButton(prefab.transform, "OverviewCurrentTargetButton");
        AssertAuthoredButton(prefab.transform, "OverviewCurrentEraButton");

        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        Transform viewport = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
        Assert.That(viewport, Is.Not.Null, "ResearchGraphViewport must be authored in the UI root.");
        Transform pageTool = root.transform.Find("SafeAreaRoot/Content/PageTool");
        Transform queue = pageTool.Find("ResearchQueueViewport");
        Transform toolbar = pageTool.Find("OverviewNavigationToolbar");
        Assert.That(toolbar.parent, Is.SameAs(queue.parent),
            "OverviewNavigationToolbar and ResearchQueueViewport must be siblings under PageTool.");
        AssertAuthoredButton(toolbar, "OverviewCurrentTargetButton");
        AssertAuthoredButton(toolbar, "OverviewCurrentEraButton");
    }

    [Test]
    public void RuntimeUiRoot_DrawsNonInteractiveBackdropOutsideSafeArea()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");

        Image backdrop = prefab.GetComponent<Image>();
        Assert.That(backdrop.raycastTarget, Is.False,
            "The decorative backdrop must not intercept UI input.");
        Assert.That(backdrop.color.a, Is.GreaterThan(0.99f),
            "The backdrop must be opaque enough to hide the camera clear color.");
    }

    [Test]
    public void RuntimeUiRoot_AuthorsNonInteractiveCivilizationTelegraphOutsideSafeArea()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");

        Transform ticker = prefab.transform.Find("UnsafeAreaTicker");
        Assert.That(ticker.parent, Is.SameAs(prefab.transform));
        UnsafeAreaTicker tickerComponent = ticker.GetComponent<UnsafeAreaTicker>();
        Assert.That(tickerComponent.IsConfigured, Is.True,
            "Every authored ticker reference must be bound in the prefab.");
        Canvas tickerCanvas = ticker.GetComponent<Canvas>();
        Assert.That(tickerCanvas, Is.Not.Null,
            "The animated ticker must use a nested Canvas to isolate its rebuilds.");
        Assert.That(ticker.GetComponent<GraphicRaycaster>(), Is.Null,
            "The decorative child Canvas must not receive input.");
        TMP_Text feed = ticker.Find("FeedViewport/FeedText")?.GetComponent<TMP_Text>();
        Assert.That(ticker.Find("Title"), Is.Null,
            "The narrow ticker reserves its full height for one active headline.");
        Assert.That(feed.font, Is.Not.Null);
        Assert.That(feed.fontSharedMaterial, Is.Not.Null);
        Assert.That(feed.isOrthographic, Is.True);
        Assert.That(feed.maskable, Is.True);
        RectMask2D mask = ticker.Find("FeedViewport")?.GetComponent<RectMask2D>();
        Assert.That(feed.transform.IsChildOf(mask.transform), Is.True);

        Graphic[] graphics = ticker.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Assert.That(graphics[i].raycastTarget, Is.False,
                graphics[i].name + " must remain decorative and must not intercept input.");
        }
    }

    private static void AssertAuthoredButton(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        Assert.That(child.GetComponent<Button>(), Is.Not.Null,
            childName + " must contain a Button component in the prefab.");
        Assert.That(child.GetComponentInChildren<TMP_Text>(true), Is.Not.Null,
            childName + " must contain an authored label in the prefab.");
    }

}
