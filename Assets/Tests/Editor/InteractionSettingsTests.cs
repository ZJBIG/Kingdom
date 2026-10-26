using NUnit.Framework;

public sealed class InteractionSettingsTests
{
    [Test]
    public void DragThresholdScalesWithDensityAndBoundsInvalidDpi()
    {
        int baseline = InteractionFrameRateController.DragThresholdForDpi(160f);
        Assert.That(InteractionFrameRateController.DragThresholdForDpi(480f), Is.GreaterThan(baseline));
        Assert.That(InteractionFrameRateController.DragThresholdForDpi(0f), Is.EqualTo(baseline));
        Assert.That(InteractionFrameRateController.DragThresholdForDpi(float.NaN), Is.EqualTo(baseline));
        Assert.That(InteractionFrameRateController.DragThresholdForDpi(float.PositiveInfinity), Is.EqualTo(baseline));
        Assert.That(InteractionFrameRateController.DragThresholdForDpi(10000f), Is.InRange(baseline, 48));
    }

    [Test]
    public void MobileRenderCadenceKeepsInteractionAndInertiaResponsive()
    {
        // 30/60 are the explicitly selected render modes, not computed economy values.
        Assert.That(InteractionFrameRateController.DesiredFrameRate(false, false, 2f, 1f), Is.EqualTo(30));
        Assert.That(InteractionFrameRateController.DesiredFrameRate(true, false, 2f, 1f), Is.EqualTo(60));
        Assert.That(InteractionFrameRateController.DesiredFrameRate(false, true, 2f, 1f), Is.EqualTo(60));
        Assert.That(InteractionFrameRateController.DesiredFrameRate(false, false, .9f, 1f), Is.EqualTo(60));
    }
}
