using NUnit.Framework;
using UnityEngine;

public sealed class UnsafeAreaTickerStatusTests
{
    [Test]
    public void HealthyKingdomNeedsNoWarning()
    {
        Assert.That(UnsafeAreaTickerRules.BuildStatusNotices(new GameState(), null), Is.Empty);
    }

    [Test]
    public void CurrentSupplyRisksTakePriorityOverResearch()
    {
        var state = new GameState();
        state.AdjustFoodRatesForEditor(ExpantaNum.Zero, new ExpantaNum(10));
        state.SetPowerSatisfactionForEditor(new ExpantaNum(.5));
        state.SetLogisticsSatisfactionForEditor(new ExpantaNum(.5));
        Research research = ScriptableObject.CreateInstance<Research>();
        try
        {
            research.Label = "不应挤掉风险的研究";
            research.BaseCost = "100";
            var notices = UnsafeAreaTickerRules.BuildStatusNotices(state, new ResearchState(research));
            Assert.That(notices.Count, Is.EqualTo(UnsafeAreaTickerRules.MaximumNoticeCount), "The ticker has three headline slots.");
            StringAssert.Contains("粮食", notices[0]);
            StringAssert.Contains("供电不足", notices[1]);
            StringAssert.Contains("物流", notices[2]);
        }
        finally { Object.DestroyImmediate(research); }
    }

    [Test]
    public void SupplyWarningsFitAHeadlineAndOfferAnAction()
    {
        var state = new GameState();
        state.AdjustFoodRatesForEditor(ExpantaNum.Zero, new ExpantaNum(10));
        state.SetPowerSatisfactionForEditor(new ExpantaNum(.5));
        state.SetLogisticsSatisfactionForEditor(new ExpantaNum(.5));

        var notices = UnsafeAreaTickerRules.BuildStatusNotices(state, null);
        foreach (string notice in notices)
        {
            Assert.That(notice.Length,
                Is.LessThanOrEqualTo(UnsafeAreaTickerRules.MaximumHeadlineCharacters));
            StringAssert.Contains("暂停", notice,
                "Each supply warning must tell the player how to reduce demand.");
        }
    }

    [TestCase(false, "：待支付物资")]
    [TestCase(true, "：研究进行中")]
    public void LongResearchNameKeepsItsStatusSuffix(bool paid, string suffix)
    {
        Research research = ScriptableObject.CreateInstance<Research>();
        Resource material = ScriptableObject.CreateInstance<Resource>();
        try
        {
            research.Label = new string('长', 40);
            research.BaseCost = "100";
            if (!paid)
                research.SetResourceRequirementsForEditor(new System.Collections.Generic.List<Pair<Resource, ExpantaNum>>
                {
                    new Pair<Resource, ExpantaNum>(material, ExpantaNum.One)
                });
            var researchState = new ResearchState(research);
            if (paid) researchState.SetProgressForEditor(new ExpantaNum(50));
            var notices = UnsafeAreaTickerRules.BuildStatusNotices(new GameState(), researchState);
            Assert.That(notices, Has.Count.EqualTo(1));
            Assert.That(notices[0].Length, Is.LessThanOrEqualTo(UnsafeAreaTickerRules.MaximumHeadlineCharacters));
            Assert.That(notices[0], Does.EndWith(suffix));
        }
        finally
        {
            Object.DestroyImmediate(research);
            Object.DestroyImmediate(material);
        }
    }

    [TestCase(TechLevel.Animal)]
    [TestCase(TechLevel.StoneAge)]
    [TestCase(TechLevel.Medieval)]
    [TestCase(TechLevel.Industrial)]
    [TestCase(TechLevel.Spacer)]
    [TestCase(TechLevel.Ultra)]
    [TestCase(TechLevel.Archotech)]
    public void DailyNewsDominatesEveryEraWithOccasionalNotices(TechLevel era)
    {
        string daily = UnsafeAreaTickerRules.BuildFeedCycle(null, era, "5500/1/1");
        string[] headlines = daily.Split('\n');
        Assert.That(headlines.Length, Is.GreaterThanOrEqualTo(30));
        Assert.That(new System.Collections.Generic.HashSet<string>(headlines).Count, Is.EqualTo(headlines.Length));
        foreach (string headline in headlines)
            Assert.That(headline.Length, Is.InRange(1, UnsafeAreaTickerRules.MaximumHeadlineCharacters));
        string[] feed = UnsafeAreaTickerRules.BuildFeedCycle(
            new[] { "过时消息", "通知甲", "通知乙", "通知丙" }, era, "5500/1/1").Split('\n');
        Assert.That(feed.Length, Is.EqualTo(headlines.Length + 3));
        Assert.That(feed, Does.Not.Contain("过时消息"));
        // Three discrete inserts after two, eight and fourteen daily stories.
        Assert.That(feed[2], Is.EqualTo("通知甲"));
        Assert.That(feed[9], Is.EqualTo("通知乙"));
        Assert.That(feed[16], Is.EqualTo("通知丙"));
        Assert.That(UnsafeAreaTickerRules.BuildFeedCycle(null, era, "5500/1/2"), Is.EqualTo(daily));
    }

    [Test]
    public void ResearchProgressDoesNotRestartTheNewsCycle()
    {
        Research research = ScriptableObject.CreateInstance<Research>();
        try
        {
            research.Label = "测试研究";
            research.BaseCost = "100";
            var active = new ResearchState(research);
            active.SetProgressForEditor(new ExpantaNum(10));
            string before = UnsafeAreaTickerRules.BuildFeedCycle(
                UnsafeAreaTickerRules.BuildStatusNotices(new GameState(), active), TechLevel.StoneAge, "5500/1/1");
            active.SetProgressForEditor(new ExpantaNum(50));
            Assert.That(UnsafeAreaTickerRules.BuildFeedCycle(
                UnsafeAreaTickerRules.BuildStatusNotices(new GameState(), active), TechLevel.StoneAge, "5500/1/2"),
                Is.EqualTo(before));
        }
        finally { Object.DestroyImmediate(research); }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void OldEventDoesNotKeepOccupyingTheNews(int sequence)
    {
        var state = new GameState();
        state.Events.RestoreForEditor(new SaveManager.WorldEventSaveData
        {
            Sequence = sequence,
            SecondsUntilNextEvent = WorldEventState.EventIntervalSeconds - WorldEventState.EffectDurationSeconds - 1d
        });
        Assert.That(UnsafeAreaTickerRules.BuildStatusNotices(state, null), Is.Empty);
    }

    [Test]
    public void AuroraIsReportedWithoutAnEconomicPenalty()
    {
        var state = new GameState();
        state.Events.RestoreForEditor(new SaveManager.WorldEventSaveData
        {
            Sequence = 2, SecondsUntilNextEvent = WorldEventState.EventIntervalSeconds
        });
        var notices = UnsafeAreaTickerRules.BuildStatusNotices(state, null);
        Assert.That(notices, Has.Count.EqualTo(1));
        StringAssert.Contains("极光", notices[0]);
        StringAssert.Contains("无数值影响", notices[0]);
        Assert.That(state.Events.ProductionMultiplier, Is.EqualTo(ExpantaNum.One), "Aurora has exactly no multiplier effect.");
    }
}
