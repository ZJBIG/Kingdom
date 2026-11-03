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
}
