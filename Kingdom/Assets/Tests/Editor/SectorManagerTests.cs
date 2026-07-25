using NUnit.Framework;

public sealed class SectorManagerTests
{
    [Test]
    public void C704_ManagerUsesStableDefinitionOrderAndPrerequisites()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();

        Assert.That(manager.OrderedStates, Has.Count.EqualTo(3));
        Assert.That(manager.OrderedStates[0].Definition.Id, Is.EqualTo("LowOrbit"));
        Assert.That(manager.OrderedStates[1].Definition.Id, Is.EqualTo("Mars"));
        Assert.That(manager.OrderedStates[2].Definition.Id, Is.EqualTo("Moon"));
        Assert.That(manager.CanAccess(DataBase<SectorDefinition>.Find("LowOrbit")), Is.True);
        Assert.That(manager.CanAccess(DataBase<SectorDefinition>.Find("Moon")), Is.False);
    }

    [Test]
    public void C704_SectorStateRoundTripsByStableId()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorState state = manager.GetState(lowOrbit);
        state.SetUnlockedForEditor(true);
        state.SetOccupiedForEditor(true);
        state.SetCampaignProgressForEditor(new ExpantaNum(0.75d));
        state.SetVisitCountForEditor(3);

        SaveManager.SectorSaveData saved = manager.CaptureSaveData();
        manager.InitializeNew();
        Assert.That(manager.GetState(lowOrbit).Occupied, Is.False);

        manager.RestoreSaveData(saved);
        SectorState restored = manager.GetState(lowOrbit);
        Assert.That(restored.Unlocked, Is.True);
        Assert.That(restored.Occupied, Is.True);
        Assert.That(restored.CampaignProgress.ToDouble(), Is.EqualTo(0.75d).Within(0.000001d));
        Assert.That(restored.VisitCount, Is.EqualTo(3));
    }

    [Test]
    public void C704_NonRepeatableSectorCannotBeOccupiedTwice()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorState state = manager.GetState(DataBase<SectorDefinition>.Find("LowOrbit"));
        state.SetUnlockedForEditor(true);

        Assert.That(manager.TryOccupy(state.Definition, out SectorOperationFailure first), Is.True);
        Assert.That(manager.TryOccupy(state.Definition, out SectorOperationFailure second), Is.False);
        Assert.That(second, Is.EqualTo(SectorOperationFailure.AlreadyOccupied));
        Assert.That(state.VisitCount, Is.EqualTo(1));
    }

    [Test]
    public void C705_SectorRewardsUseExistingStrategicResources()
    {
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");
        Resource composite = DataBase<Resource>.Find("Composite");
        Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");

        Assert.That(moon.ResourceRewards, Has.Count.EqualTo(1));
        Assert.That(moon.ResourceRewards[0].First, Is.EqualTo(composite));
        Assert.That(moon.ResourceRewards[0].Second, Is.EqualTo(new ExpantaNum(75)));
        Assert.That(mars.ResourceRewards, Has.Count.EqualTo(1));
        Assert.That(mars.ResourceRewards[0].First, Is.EqualTo(rocketFuel));
        Assert.That(mars.ResourceRewards[0].Second, Is.EqualTo(new ExpantaNum(100)));
    }
}
