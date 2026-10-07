using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class UnsafeAreaTickerPlayModeTests
{
    private string saveRoot;
    [SetUp]
    public void SetUp()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        saveRoot = Path.Combine(projectRoot, "Temp", "KingdomUnsafeAreaTickerPlayModeTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [TearDown]
    public void TearDown()
    {
        SaveManager.ClearSaveRootOverrideForTests();
        if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot))
            Directory.Delete(saveRoot, true);
        saveRoot = null;
    }

    [UnityTest]
    public IEnumerator CivilizationTelegraph_UsesInsetWithoutInterceptingInput()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        Assert.That(prefab, Is.Not.Null, "KingdomUIRoot prefab must be loadable.");

        GameObject layoutRoot = new(
            "UnsafeAreaTickerPlayModeRoot",
            typeof(RectTransform),
            typeof(Canvas));
        RectTransform layoutRect = layoutRoot.GetComponent<RectTransform>();
        layoutRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        layoutRect.sizeDelta = new Vector2(2640f, 1200f);

        GameObject safeAreaObject = new("SafeAreaRoot", typeof(RectTransform));
        RectTransform safeArea = safeAreaObject.GetComponent<RectTransform>();
        safeArea.SetParent(layoutRect, false);
        safeArea.anchorMin = new Vector2(0.055f, 0f);
        safeArea.anchorMax = Vector2.one;
        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;

        GameObject tickerTemplate = prefab.transform.Find("UnsafeAreaTicker")?.gameObject;
        Assert.That(tickerTemplate, Is.Not.Null);
        GameObject tickerObject = Object.Instantiate(tickerTemplate, layoutRect, false);
        UnsafeAreaTicker ticker = tickerObject.GetComponent<UnsafeAreaTicker>();
        Assert.That(ticker, Is.Not.Null);
        ticker.Initialize(safeArea, null);
        TMP_Text feedText = tickerObject.transform.Find(
            "FeedViewport/FeedText")?.GetComponent<TMP_Text>();
        Assert.That(feedText, Is.Not.Null);
        float authoredAlpha = feedText.alpha;
        ticker.SetFeed(new[] { "建设完成：农场" }, TechLevel.StoneAge, "5500/1/1");
        yield return null;

        Assert.That(safeArea.gameObject.activeInHierarchy, Is.True);
        Assert.That(ticker.CurrentEdge, Is.EqualTo(UnsafeAreaTickerEdge.Left));
        Assert.That(ticker.TextVisible, Is.True);
        StringAssert.Contains("\n", feedText.text,
            "The active headline must be arranged as a vertical character column.");
        StringAssert.Contains("成\n完\n设\n建", feedText.text);
        Assert.That(feedText.alpha, Is.EqualTo(authoredAlpha).Within(0.001f),
            "News scrolling must not animate opacity.");
        float firstHorizontalPosition = feedText.rectTransform.anchoredPosition.x;
        float firstVerticalPosition = feedText.rectTransform.anchoredPosition.y;
        Assert.That(firstHorizontalPosition, Is.InRange(-0.001f, 0.001f));
        Assert.That(firstVerticalPosition, Is.GreaterThan(0f),
            "The headline starts above the viewport and enters progressively.");
        yield return null;
        Assert.That(feedText.rectTransform.anchoredPosition.x,
            Is.EqualTo(firstHorizontalPosition).Within(0.001f));
        Assert.That(feedText.rectTransform.anchoredPosition.y,
            Is.LessThan(firstVerticalPosition));
        Assert.That(feedText.rectTransform.localEulerAngles.z,
            Is.InRange(-0.001f, 0.001f));
        Assert.That(feedText.alpha, Is.EqualTo(authoredAlpha).Within(0.001f));

        string[] pendingCycle = UnsafeAreaTickerRules.BuildFeedCycle(
            new[] { "新时代事件" }, TechLevel.Industrial, "5500/1/2").Split('\n');
        ticker.SetFeed(new[] { "新时代事件" }, TechLevel.Industrial, "5500/1/2");
        string headlineBeforeExit = feedText.text;
        ticker.AdvanceForEditor(10000f);
        Assert.That(feedText.text, Is.EqualTo(headlineBeforeExit),
            "The current headline remains during the blank pause.");
        ticker.AdvanceForEditor(5.1f);
        Assert.That(feedText.text, Is.EqualTo(ToVerticalDisplay(pendingCycle[0])),
            "A pending era cycle must begin at headline zero.");
        ticker.AdvanceForEditor(10000f);
        ticker.AdvanceForEditor(5.1f);
        Assert.That(feedText.text, Is.EqualTo(ToVerticalDisplay(pendingCycle[1])),
            "A completed headline advances exactly once after its pause.");
        RectMask2D feedMask = tickerObject.transform.Find(
            "FeedViewport")?.GetComponent<RectMask2D>();
        Assert.That(feedMask, Is.Not.Null);
        Assert.That(feedText.maskable, Is.True);
        Assert.That(feedText.transform.IsChildOf(feedMask.transform), Is.True);
        Assert.That(feedMask.rectTransform.rect.width, Is.GreaterThan(1f));
        Assert.That(feedMask.rectTransform.rect.height, Is.GreaterThan(1f));

        Graphic[] graphics = tickerObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            Assert.That(graphics[i].raycastTarget, Is.False);

        safeArea.anchorMin = Vector2.zero;
        safeArea.anchorMax = new Vector2(0.945f, 1f);
        yield return null;

        Assert.That(ticker.CurrentEdge, Is.EqualTo(UnsafeAreaTickerEdge.Right));
        RectTransform tickerRect = tickerObject.GetComponent<RectTransform>();
        Assert.That(tickerRect.anchorMin.x, Is.InRange(0.944f, 0.946f));
        Assert.That(tickerRect.anchorMax.x, Is.InRange(0.999f, 1.001f));
        RectTransform accentLine = tickerObject.transform.Find("AccentLine") as RectTransform;
        Assert.That(accentLine, Is.Not.Null);
        Assert.That(accentLine.anchorMin.x, Is.InRange(-0.001f, 0.001f));
        Assert.That(accentLine.pivot.x, Is.InRange(-0.001f, 0.001f));

        Object.Destroy(layoutRoot);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CivilizationTelegraph_InitializesThroughCompleteKingdomUiRoot()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        Transform tickerTransform = root.transform.Find("UnsafeAreaTicker");
        Transform safeArea = root.transform.Find("SafeAreaRoot");
        Assert.That(tickerTransform, Is.Not.Null);
        Assert.That(safeArea, Is.Not.Null);
        Assert.That(tickerTransform.gameObject.activeInHierarchy, Is.True);
        Assert.That(safeArea.gameObject.activeInHierarchy, Is.True);
        Assert.That(tickerTransform.GetSiblingIndex(), Is.LessThan(safeArea.GetSiblingIndex()),
            "The ticker must render behind the SafeArea UI.");

        UnsafeAreaTicker ticker = tickerTransform.GetComponent<UnsafeAreaTicker>();
        Assert.That(ticker, Is.Not.Null);
        Assert.That(ticker.IsConfigured, Is.True);
        Canvas tickerCanvas = tickerTransform.GetComponent<Canvas>();
        Assert.That(tickerCanvas, Is.Not.Null);
        Assert.That(tickerCanvas, Is.Not.SameAs(root.GetComponent<Canvas>()));
        Assert.That(tickerCanvas.rootCanvas, Is.SameAs(root.GetComponent<Canvas>()));
        Assert.That(tickerCanvas.overrideSorting, Is.False);
        Assert.That(tickerTransform.GetComponent<GraphicRaycaster>(), Is.Null);

        TMP_Text feed = tickerTransform.Find("FeedViewport/FeedText")?.GetComponent<TMP_Text>();
        Assert.That(feed?.font, Is.Not.Null);
        Assert.That(feed.isOrthographic, Is.True);
        Assert.That(feed.maskable, Is.True);
        Assert.That(tickerTransform.Find("FeedViewport")?.GetComponent<RectMask2D>(), Is.Not.Null);

        Image rootBackdrop = root.GetComponent<Image>();
        Assert.That(rootBackdrop, Is.Not.Null);
        Assert.That(rootBackdrop.raycastTarget, Is.False);
    }

    private static string ToVerticalDisplay(string value)
    {
        char[] characters = value.ToCharArray();
        System.Array.Reverse(characters);
        return string.Join("\n", characters);
    }

}
