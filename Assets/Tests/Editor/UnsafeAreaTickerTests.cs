using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class UnsafeAreaTickerTests
{
    [Test]
    public void TryCalculateLayout_UsesLeftInsetAndShowsTextAtReadableWidth()
    {
        bool result = UnsafeAreaTickerRules.TryCalculateLayout(
            new Vector2(0.055f, 0f),
            Vector2.one,
            2640f,
            20f,
            out UnsafeAreaTickerEdge edge,
            out Vector2 anchorMin,
            out Vector2 anchorMax,
            out bool canShowText);

        Assert.That(result, Is.True);
        Assert.That(edge, Is.EqualTo(UnsafeAreaTickerEdge.Left));
        Assert.That(anchorMin.x, Is.InRange(-0.001f, 0.001f));
        Assert.That(anchorMax.x, Is.InRange(0.054f, 0.056f));
        Assert.That(canShowText, Is.True);
    }

    [Test]
    public void TryCalculateLayout_UsesRightInsetAfterLandscapeRotation()
    {
        bool result = UnsafeAreaTickerRules.TryCalculateLayout(
            Vector2.zero,
            new Vector2(0.945f, 1f),
            2640f,
            20f,
            out UnsafeAreaTickerEdge edge,
            out Vector2 anchorMin,
            out Vector2 anchorMax,
            out bool canShowText);

        Assert.That(result, Is.True);
        Assert.That(edge, Is.EqualTo(UnsafeAreaTickerEdge.Right));
        Assert.That(anchorMin.x, Is.InRange(0.944f, 0.946f));
        Assert.That(anchorMax.x, Is.InRange(0.999f, 1.001f));
        Assert.That(canShowText, Is.True);
    }

    [Test]
    public void TryCalculateLayout_HidesWithoutInsetAndFallsBackForNarrowInset()
    {
        bool noInset = UnsafeAreaTickerRules.TryCalculateLayout(
            Vector2.zero,
            Vector2.one,
            2640f,
            20f,
            out UnsafeAreaTickerEdge noInsetEdge,
            out _,
            out _,
            out _);
        bool narrowInset = UnsafeAreaTickerRules.TryCalculateLayout(
            new Vector2(0.02f, 0f),
            Vector2.one,
            2640f,
            20f,
            out UnsafeAreaTickerEdge narrowEdge,
            out _,
            out _,
            out bool narrowCanShowText);

        Assert.That(noInset, Is.False);
        Assert.That(noInsetEdge, Is.EqualTo(UnsafeAreaTickerEdge.None));
        Assert.That(narrowInset, Is.True);
        Assert.That(narrowEdge, Is.EqualTo(UnsafeAreaTickerEdge.Left));
        Assert.That(narrowCanShowText, Is.False);
    }

    [Test]
    public void TryCalculateLayout_SubtractsAuthoredViewportPaddingFromReadableWidth()
    {
        bool belowThreshold = UnsafeAreaTickerRules.TryCalculateLayout(
            new Vector2(91f / 2640f, 0f),
            Vector2.one,
            2640f,
            20f,
            out UnsafeAreaTickerEdge edge,
            out _,
            out _,
            out bool belowCanShowText);
        bool aboveThreshold = UnsafeAreaTickerRules.TryCalculateLayout(
            new Vector2(93f / 2640f, 0f),
            Vector2.one,
            2640f,
            20f,
            out _,
            out _,
            out _,
            out bool aboveCanShowText);

        Assert.That(belowThreshold, Is.True);
        Assert.That(aboveThreshold, Is.True);
        Assert.That(edge, Is.EqualTo(UnsafeAreaTickerEdge.Left));
        Assert.That(belowCanShowText, Is.False,
            "The 72px text threshold applies after the authored 20px horizontal padding.");
        Assert.That(aboveCanShowText, Is.True);
    }

    [Test]
    public void BuildFeedCycle_UsesOnlyLatestThreeAndTruncatesLongHeadline()
    {
        List<string> notices = new()
        {
            "第一条旧消息",
            "第二条消息",
            "第三条消息",
            new string('新', UnsafeAreaTickerRules.MaximumHeadlineCharacters + 8)
        };

        string feed = UnsafeAreaTickerRules.BuildFeedCycle(
            notices, TechLevel.Industrial, "5500/1/1");

        StringAssert.DoesNotContain("第一条旧消息", feed);
        StringAssert.Contains("第二条消息", feed);
        StringAssert.Contains("第三条消息", feed);
        StringAssert.Contains(
            new string('新', UnsafeAreaTickerRules.MaximumHeadlineCharacters - 1) + "…",
            feed);
    }

    [Test]
    public void BuildFeedCycle_AddsOnlyGroundedHeadlinesForTheCurrentEra()
    {
        string feed = UnsafeAreaTickerRules.BuildFeedCycle(
            null, TechLevel.StoneAge, "5500/1/2");

        StringAssert.Contains("河渠今日引水入田", feed);
        string[] lines = feed.Split('\n');
        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(30));
        for (int i = 0; i < lines.Length; i++)
        {
            Assert.That(lines[i].Length, Is.LessThanOrEqualTo(
                UnsafeAreaTickerRules.MaximumHeadlineCharacters));
        }
    }

    [Test]
    public void BuildFeedCycle_SelectsDedicatedHeadlinesForEveryEra()
    {
        TechLevel[] eras =
        {
            TechLevel.Animal,
            TechLevel.StoneAge,
            TechLevel.Medieval,
            TechLevel.Industrial,
            TechLevel.Spacer
        };
        string[] markers =
        {
            "河湾鼠群",
            "河渠今日",
            "铁匠铺",
            "城东铁匠铺",
            "月面温室"
        };

        for (int i = 0; i < eras.Length; i++)
        {
            string feed = UnsafeAreaTickerRules.BuildFeedCycle(
                null, eras[i], "5500/1/1");
            StringAssert.Contains(markers[i], feed, eras[i].ToString());
            Assert.That(feed.Split('\n').Length, Is.GreaterThanOrEqualTo(30),
                eras[i] + " must keep the expanded world-news minimum.");
        }
    }

    [Test]
    public void BuildFeedCycle_DoesNotInventUltraOrArchotechHeadlines()
    {
        Assert.That(
            UnsafeAreaTickerRules.BuildFeedCycle(null, TechLevel.Ultra, "5500/1/1"),
            Is.Empty);
        Assert.That(
            UnsafeAreaTickerRules.BuildFeedCycle(null, TechLevel.Archotech, "5500/1/1"),
            Is.Empty);

        string realEvent = UnsafeAreaTickerRules.BuildFeedCycle(
            new[] { "知识完成：技术奇点" }, TechLevel.Ultra, "5500/1/1");
        StringAssert.Contains("技术奇点", realEvent);
    }
}
