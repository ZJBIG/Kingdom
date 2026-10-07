using NUnit.Framework;

public sealed class OfflineSimulationBoundaryTests
{
    [TestCase(0d, 60d, 60d)]
    [TestCase(7199d, 60d, 36.4d)]
    [TestCase(7200d, 60d, 36d)]
    [TestCase(28799d, 60d, 15.35d)]
    [TestCase(28800d, 60d, 15d)]
    [TestCase(0d, 28800d, 20160d)]
    public void OfflineEffectiveTime_UsesExpectedRateAtAndAcrossBoundaries(
        double elapsedSeconds,
        double requestedSeconds,
        double expectedEffectiveSeconds)
    {
        Assert.That(
            SimulationManager.CalculateOfflineEffectiveSeconds(elapsedSeconds, requestedSeconds),
            Is.EqualTo(expectedEffectiveSeconds).Within(0.001d));
    }
}
