using NUnit.Framework;

public sealed class KingdomUiLifecycleTests
{
    [Test]
    public void DevelopmentGuidanceWithoutManagersReturnsSafeCompleteState()
    {
        DevelopmentGuidanceSnapshot snapshot = DevelopmentGuidance.Build(null, null, null, null, null);

        Assert.That(snapshot.Status, Is.EqualTo(DevelopmentGuidanceStatus.Complete));
        Assert.That(snapshot.Blockers, Is.Empty);
        Assert.That(snapshot.Body, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void SituationReportUsesLiveFoodBalanceWithoutMutatingState()
    {
        var state = new GameState();
        state.AdjustFoodRatesForEditor(ExpantaNum.Zero, new ExpantaNum(10));
        int version = state.Version;
        ExpantaNum food = state.FoodAmount;
        string report = DevelopmentGuidance.BuildSituationReport(state, null);
        StringAssert.Contains("余粮约可维持", report);
        StringAssert.DoesNotContain("粮食净余", report);
        Assert.That(state.Version, Is.EqualTo(version), "Rendering cannot mutate the state version.");
        Assert.That(state.FoodAmount, Is.EqualTo(food), "Rendering cannot spend food.");
    }

    [Test]
    public void SituationReportSeparatesBalancedFoodFromSupplyShortage()
    {
        var state = new GameState();
        state.AdjustFoodRatesForEditor(ExpantaNum.Zero, GameState.BaseFoodProductionRate);
        string report = DevelopmentGuidance.BuildSituationReport(state, null);
        StringAssert.Contains("粮食收支持平", report);
        StringAssert.DoesNotContain("电力供给满足", report);
        StringAssert.DoesNotContain("物流供给满足", report);
        StringAssert.DoesNotContain("分钟", report);
    }
}
