using NUnit.Framework;

public sealed class PreSpacerCombatRemovalTests
{
    [Test]
    public void C811_PreSpacerBuildingsHaveNoCombatAttributes()
    {
        foreach (Building building in DataBase<Building>.All)
        {
            if (building == null || building.TechLevel >= TechLevel.Spacer)
                continue;

            Assert.That(building.AttackPowerGranted, Is.EqualTo(ExpantaNum.Zero), building.Id);
            Assert.That(building.DefensePowerGranted, Is.EqualTo(ExpantaNum.Zero), building.Id);
            Assert.That(building.FleetPowerGranted, Is.EqualTo(ExpantaNum.Zero), building.Id);
            Assert.That(building.MilitaryManpowerGranted, Is.EqualTo(ExpantaNum.Zero), building.Id);
        }
    }

    [Test]
    public void C811_PreSpacerResearchHasNoMilitaryEffects()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            if (research == null || research.TechLevel >= TechLevel.Spacer)
                continue;

            foreach (ResearchEffectDefinition effect in research.Effects)
                Assert.That(effect.Type, Is.Not.EqualTo(ResearchEffectType.MilitaryMultiplier), research.Id);
        }
    }

    [Test]
    public void C811_ConvertedResearchUsesGlobalProductionMultipliers()
    {
        AssertGlobalMultiplier("OrganizedDefense", 1.03d);
        AssertGlobalMultiplier("OrganizedWatch", 1.04d);
        AssertGlobalMultiplier("Fortification", 1.05d);
        AssertGlobalMultiplier("StandingArmy", 1.06d);
        AssertGlobalMultiplier("Gunpowder", 1.07d);
        AssertGlobalMultiplier("MilitaryIndustry", 1.10d);
        AssertBuildingMultiplier("MilitaryIndustry", "MachineFactory", 1.20d);
    }

    [Test]
    public void C811_SpacerFleetBuildingsRemainAvailable()
    {
        Assert.That(DataBase<Building>.Find("OrbitalStation").FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<Building>.Find("Shipyard").FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
    }

    private static void AssertGlobalMultiplier(string id, double expected)
    {
        Research research = DataBase<Research>.Find(id);
        Assert.That(research, Is.Not.Null, id);
        Assert.That(research.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect.Type == ResearchEffectType.GlobalBuildingProductionMultiplier &&
            effect.Value.ToDouble() == expected));
    }

    private static void AssertBuildingMultiplier(string researchId, string buildingId, double expected)
    {
        Research research = DataBase<Research>.Find(researchId);
        Assert.That(research, Is.Not.Null, researchId);
        Assert.That(research.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
            effect.Building != null && effect.Building.Id == buildingId &&
            effect.Value.ToDouble() == expected));
    }
}
