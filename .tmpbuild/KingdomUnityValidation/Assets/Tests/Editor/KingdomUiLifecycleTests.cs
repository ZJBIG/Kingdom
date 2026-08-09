using NUnit.Framework;

public sealed class KingdomUiLifecycleTests
{
    [Test]
    public void NewUiLifecycleTestAssemblyRemainsAvailable()
    {
        Assert.That(typeof(KingdomUIRoot), Is.Not.Null);
    }

    [Test]
    public void DevelopmentGuidanceWithoutManagersReturnsSafeCompleteState()
    {
        DevelopmentGuidanceSnapshot snapshot = DevelopmentGuidance.Build(null, null, null, null, null);

        Assert.That(snapshot, Is.Not.Null);
        Assert.That(snapshot.Status, Is.EqualTo(DevelopmentGuidanceStatus.Complete));
        Assert.That(snapshot.Blockers, Is.Empty);
    }

    [Test]
    public void DevelopmentGuidanceEmptyStateHasReadableSummary()
    {
        DevelopmentGuidanceSnapshot snapshot = DevelopmentGuidance.Build(null, null, null, null, null);

        Assert.That(snapshot.Body, Does.Contain("研究树"));
    }
}
