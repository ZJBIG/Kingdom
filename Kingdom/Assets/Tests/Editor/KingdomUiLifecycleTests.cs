using NUnit.Framework;

public sealed class KingdomUiLifecycleTests
{
    [Test]
    public void NewUiLifecycleTestAssemblyRemainsAvailable()
    {
        Assert.That(typeof(KingdomUIRoot), Is.Not.Null);
    }
}
