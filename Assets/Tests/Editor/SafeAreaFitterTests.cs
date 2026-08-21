using NUnit.Framework;
using UnityEngine;

public sealed class SafeAreaFitterTests
{
    [Test]
    public void TryCalculateAnchors_ConvertsInsetSafeAreaToNormalizedAnchors()
    {
        bool calculated = SafeAreaFitter.TryCalculateAnchors(
            new Rect(100f, 50f, 1820f, 970f),
            new Vector2(1920f, 1080f),
            out Vector2 min,
            out Vector2 max);

        Assert.That(calculated, Is.True);
        Assert.That(min.x, Is.EqualTo(100f / 1920f).Within(0.000001f));
        Assert.That(min.y, Is.EqualTo(50f / 1080f).Within(0.000001f));
        Assert.That(max.x, Is.EqualTo(1920f / 1920f).Within(0.000001f));
        Assert.That(max.y, Is.EqualTo(1020f / 1080f).Within(0.000001f));
    }

    [Test]
    public void TryCalculateAnchors_RejectsInvalidScreenSize()
    {
        bool calculated = SafeAreaFitter.TryCalculateAnchors(
            new Rect(0f, 0f, 100f, 100f),
            Vector2.zero,
            out _,
            out _);

        Assert.That(calculated, Is.False);
    }
}
