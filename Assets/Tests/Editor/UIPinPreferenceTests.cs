using System;
using NUnit.Framework;

public sealed class UIPinPreferenceTests
{
    private string id;
    private string otherId;

    [SetUp]
    public void SetUp()
    {
        id = "pin-test-" + Guid.NewGuid().ToString("N");
        otherId = id + "-other";
    }

    [TearDown]
    public void TearDown()
    {
        UIPinPreferences.SetPinned(UIPinPreferences.Resource, id, false);
        UIPinPreferences.SetPinned(UIPinPreferences.Resource, otherId, false);
        UIPinPreferences.SetPinned(UIPinPreferences.Building, id, false);
    }

    [Test]
    public void PinPreferencePersistsByCategoryAndStableIdAndCanBeRemoved()
    {
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Resource, id), Is.False);
        UIPinPreferences.SetPinned(UIPinPreferences.Resource, id, true);
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Resource, id), Is.True);
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Building, id), Is.False);
        UIPinPreferences.SetPinned(UIPinPreferences.Resource, id, false);
        Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Resource, id), Is.False);
    }

    [Test]
    public void PinnedEntriesSortBeforeUnpinnedAndTiesPreserveExistingComparison()
    {
        UIPinPreferences.SetPinned(UIPinPreferences.Resource, id, true);
        Assert.That(UIPinPreferences.Compare(UIPinPreferences.Resource, id, otherId), Is.LessThan(0));
        Assert.That(UIPinPreferences.Compare(UIPinPreferences.Resource, otherId, id), Is.GreaterThan(0));
        UIPinPreferences.SetPinned(UIPinPreferences.Resource, otherId, true);
        Assert.That(UIPinPreferences.Compare(UIPinPreferences.Resource, id, otherId), Is.Zero,
            "A tie delegates to the existing era and ID ordering.");
    }

    [Test]
    public void MissingStableIdentityIsRejected()
    {
        Assert.Throws<ArgumentException>(() => UIPinPreferences.SetPinned(UIPinPreferences.Resource, "", true));
        Assert.Throws<ArgumentException>(() => UIPinPreferences.IsPinned("", id));
    }
}
