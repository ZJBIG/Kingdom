using System;
using NUnit.Framework;

public sealed class C6IndustrialClosureAuditTests
{
    [Test]
    public void C604_PowerAndLogisticsRatesRemainDerivedOutsideSaveDtos()
    {
        foreach (Type fieldType in new[]
        {
            typeof(SaveManager.GameSaveData),
            typeof(SaveManager.KingdomSaveData)
        })
        {
            foreach (var field in fieldType.GetFields())
            {
                Assert.That(field.Name, Is.Not.EqualTo("PowerProductionRate"));
                Assert.That(field.Name, Is.Not.EqualTo("PowerConsumptionRate"));
                Assert.That(field.Name, Is.Not.EqualTo("LogisticsProductionRate"));
                Assert.That(field.Name, Is.Not.EqualTo("LogisticsConsumptionRate"));
            }
        }
    }
}
