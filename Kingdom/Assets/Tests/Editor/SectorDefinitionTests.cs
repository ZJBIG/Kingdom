using NUnit.Framework;

public sealed class SectorDefinitionTests
{
    [Test]
    public void C701_InitialSectorChainContainsStableIdsAndPrerequisites()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");
        SectorDefinition alpha = DataBase<SectorDefinition>.Find("AlphaCentauri");

        Assert.That(lowOrbit.Id, Is.EqualTo("LowOrbit"));
        Assert.That(moon.Id, Is.EqualTo("Moon"));
        Assert.That(mars.Id, Is.EqualTo("Mars"));
        Assert.That(alpha.Domain, Is.EqualTo(SectorDefinition.SectorDomain.Interstellar));
        Assert.That(alpha.StarSystemId, Is.EqualTo("AlphaCentauri"));
        Assert.That(lowOrbit.PrerequisiteSectors, Is.Empty);
        Assert.That(moon.PrerequisiteSectors, Has.Count.EqualTo(1));
        Assert.That(moon.PrerequisiteSectors[0].Id, Is.EqualTo("LowOrbit"));
        Assert.That(mars.PrerequisiteSectors, Has.Count.EqualTo(1));
        Assert.That(mars.PrerequisiteSectors[0].Id, Is.EqualTo("Moon"));
    }

    [Test]
    public void C701_SectorsExposeRewardsEnemyPowerAndMapCoordinates()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");

        Assert.That(lowOrbit.EnemyPower, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(lowOrbit.CampaignFoodPerMinute, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(lowOrbit.CampaignResourceCosts, Is.Empty);
        Assert.That(lowOrbit.TerritoryReward, Is.EqualTo(new ExpantaNum(10)));
        Assert.That(moon.EnemyPower, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(moon.TerritoryReward, Is.EqualTo(new ExpantaNum(25)));
        Assert.That(mars.EnemyPower, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(moon.CampaignFoodPerMinute, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(mars.CampaignFoodPerMinute, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(mars.TerritoryReward, Is.EqualTo(new ExpantaNum(50)));
        Assert.That(mars.MapX, Is.GreaterThan(moon.MapX));
        Assert.That(DataBase<SectorDefinition>.Find("AlphaCentauri").EnemyPower, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void C701_SectorDefinitionsHaveNoDependencyCycles()
    {
        Assert.That(
            SectorValidator.ValidateNoCycles(DataBase<SectorDefinition>.All, out string error),
            Is.True,
            error);
    }

    [Test]
    public void C701_SectorStateStartsEmptyAndTracksProgress()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        var state = new SectorState(lowOrbit);

        Assert.That(state.Unlocked, Is.False);
        Assert.That(state.Occupied, Is.False);
        Assert.That(state.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.VisitCount, Is.EqualTo(0));

        state.SetUnlockedForEditor(true);
        state.SetCampaignProgressForEditor(new ExpantaNum(0.5d));
        state.SetOccupiedForEditor(true);

        Assert.That(state.Unlocked, Is.True);
        Assert.That(state.Occupied, Is.True);
        Assert.That(state.CampaignProgress.ToDouble(), Is.EqualTo(0.5d).Within(0.000001d));
        Assert.That(state.Version, Is.GreaterThan(0));
    }
}
