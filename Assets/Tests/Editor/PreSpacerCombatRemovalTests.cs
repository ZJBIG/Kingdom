using NUnit.Framework;

public sealed class PreSpacerCombatRemovalTests
{
    [Test]
    public void C811_PreSpacerBuildingsHaveNoCombatAttributes()
    {
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
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
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
        {
            if (research == null || research.TechLevel >= TechLevel.Spacer)
                continue;

            foreach (ResearchEffectDefinition effect in research.Effects)
                Assert.That(effect.Type, Is.Not.EqualTo(ResearchEffectType.MilitaryMultiplier), research.Id);
        }
    }

    [Test]
    public void C811_ConvertedResearchUsesDistinctNonCombatRoles()
    {
        AssertGlobalMultiplier("WatchAndCooperation");
        AssertGlobalMultiplier("OrganizedWatch");
        AssertEffectType("Fortification", ResearchEffectType.GlobalConstructionMultiplier);
        AssertEffectType("PermanentLaborOrganization", ResearchEffectType.PopulationProductivityMultiplier);
        AssertEffectType("GunpowderSupplyStandards", ResearchEffectType.GlobalLogisticsMultiplier);
        AssertBuildingMultiplier("MilitaryIndustry", "MachineFactory");
        Assert.That(
            DataBase<ResearchDef>.Find("MilitaryIndustry").Effects,
            Has.None.Matches<ResearchEffectDefinition>(effect =>
                effect.Type == ResearchEffectType.GlobalBuildingProductionMultiplier));
    }

    [Test]
    public void C811_SpacerFleetBuildingsRemainAvailable()
    {
        Assert.That(DataBase<BuildingDef>.Find("OrbitalStation").FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(DataBase<BuildingDef>.Find("Shipyard").FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
    }

    private static void AssertGlobalMultiplier(string id)
    {
        AssertEffectType(id, ResearchEffectType.GlobalBuildingProductionMultiplier);
    }

    private static void AssertEffectType(string id, ResearchEffectType type)
    {
        ResearchDef research = DataBase<ResearchDef>.Find(id);
        Assert.That(research.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect.Type == type && effect.NumericValue > ExpantaNum.One));
    }

    private static void AssertBuildingMultiplier(string researchId, string buildingId)
    {
        ResearchDef research = DataBase<ResearchDef>.Find(researchId);
        Assert.That(research.Effects, Has.Some.Matches<ResearchEffectDefinition>(effect =>
            effect.Type == ResearchEffectType.BuildingProductionMultiplier &&
            effect.Building != null && effect.Building.Id == buildingId &&
            effect.NumericValue > ExpantaNum.One));
    }
}
