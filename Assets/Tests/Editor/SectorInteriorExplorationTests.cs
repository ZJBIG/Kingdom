using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorInteriorExplorationTests
{
    private GameObject root;
    private GameManager game;
    private ResourceManager resources;
    private ResearchManager research;
    private SectorDef sector;
    private SectorInteriorExplorationDefinition original;
    private ResourceDef machinery;
    private ResourceDef electronics;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject(nameof(SectorInteriorExplorationTests));
        game = root.AddComponent<GameManager>();
        resources = root.AddComponent<ResourceManager>();
        research = root.AddComponent<ResearchManager>();
        research.InitializeForEditor();
        sector = DataBase<SectorDef>.Find("AzurePool");
        machinery = DataBase<ResourceDef>.Find("Machinery");
        electronics = DataBase<ResourceDef>.Find("Electronics");
        original = sector.InteriorExploration;
        var project = new SectorInteriorExplorationDefinition();
        project.ConfigureForEditor("fixture", new ExpantaNum(10), new List<Pair<ResourceDef, ExpantaNum>>
        {
            new(machinery, new ExpantaNum(2)), new(electronics, new ExpantaNum(3))
        });
        sector.SetInteriorExplorationForEditor(project);
        resources.SetAmount(machinery, new ExpantaNum(100));
        resources.SetAmount(electronics, new ExpantaNum(100));
    }

    [TearDown]
    public void TearDown()
    {
        sector.SetInteriorExplorationForEditor(original);
        Object.DestroyImmediate(root);
    }

    private SectorState Occupy()
    {
        SectorState state = game.Sectors.GetState(sector);
        state.SetUnlockedForEditor(true);
        state.SetOccupiedForEditor(true);
        return state;
    }

    private void Tick(double seconds) => game.Sectors.TickActiveInteriorExploration(seconds,
        game.State, resources, out _);

    private static void Near(ExpantaNum actual, double expected) =>
        Assert.That((actual - new ExpantaNum(expected)).Abs() < new ExpantaNum(0.000001), Is.True);

    [Test]
    public void OccupationIsRequiredBeforeStarting()
    {
        Assert.That(game.Sectors.TryStartInteriorExploration(sector, out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InteriorExplorationRequiresOccupation));
        Near(resources.GetAmount(machinery), 100);
        Occupy();
        Assert.That(game.Sectors.TryStartInteriorExploration(sector, out failure), Is.True);
    }

    [Test]
    public void PauseBeforeAnyProgressIsExplicitAndRoundTrips()
    {
        SectorState state = Occupy();
        game.Sectors.TryStartInteriorExploration(sector, out _);
        game.Sectors.PauseInteriorExploration(sector);
        Assert.That(state.InteriorExplorationPaused, Is.True);
        Assert.That(game.Sectors.GetInteriorExplorationPreview(sector).Status,
            Is.EqualTo(SectorInteriorExplorationStatus.Paused));
        SaveManager.SectorSaveData save = game.Sectors.CaptureSaveData();
        game.Sectors.ResetForLoad();
        game.Sectors.RestoreSaveData(save);
        Assert.That(state.InteriorExplorationPaused, Is.True);
        game.Sectors.TryStartInteriorExploration(sector, out _);
        Assert.That(state.InteriorExplorationPaused, Is.False);
        Tick(100);
        Assert.That(state.InteriorExplorationPaused, Is.False);
    }

    [TestCase("DawnRing")]
    [TestCase("Terminus")]
    [TestCase("ShardCrown")]
    public void OrdinarySectorsHaveNoInteriorExploration(string sectorId)
    {
        Assert.That(DataBase<SectorDef>.Find(sectorId).InteriorExploration, Is.Null);
    }

    [Test]
    public void EmptyInlineDefinitionIsAbsentButMalformedAuthoredDefinitionRemainsVisible()
    {
        sector.SetInteriorExplorationForEditor(new SectorInteriorExplorationDefinition());
        Assert.That(sector.InteriorExploration, Is.Null);
        var malformed = new SectorInteriorExplorationDefinition();
        malformed.ConfigureForEditor("authored", new ExpantaNum(-1),
            new List<Pair<ResourceDef, ExpantaNum>> { new(machinery, ExpantaNum.One) });
        sector.SetInteriorExplorationForEditor(malformed);
        Assert.That(sector.InteriorExploration, Is.SameAs(malformed));
        Assert.That(game.Sectors.TryStartInteriorExploration(sector, out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InvalidInteriorExploration));
    }

    [Test]
    public void RequiredResearchBlocksStartAndIsReportedInPreview()
    {
        Occupy();
        ResearchDef required = DataBase<ResearchDef>.Find("HomeSystemSurvey");
        sector.InteriorExploration.ConfigureForEditor("fixture", new ExpantaNum(10),
            new List<Pair<ResourceDef, ExpantaNum>> { new(machinery, ExpantaNum.One) }, required);
        Assert.That(game.Sectors.TryStartInteriorExploration(sector, out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InteriorExplorationResearchRequired));
        Assert.That(game.Sectors.GetInteriorExplorationPreview(sector).MissingResearch, Is.SameAs(required));
        research.GetState(required).SetStatusForEditor(ResearchStatus.Completed);
        Assert.That(game.Sectors.TryStartInteriorExploration(sector, out _), Is.True);
    }

    [Test]
    public void MissingOneInputPaysNothingAndAutomaticallyResumesAfterSupply()
    {
        SectorState state = Occupy();
        resources.SetAmount(electronics, ExpantaNum.Zero);
        Assert.That(game.Sectors.TryStartInteriorExploration(sector, out _), Is.True);
        Tick(5);
        Near(state.InteriorExplorationProgress, 0);
        Near(resources.GetAmount(machinery), 100);
        var preview = game.Sectors.GetInteriorExplorationPreview(sector);
        Assert.That(preview.Status, Is.EqualTo(SectorInteriorExplorationStatus.WaitingForSupply));
        Assert.That(preview.MissingResource, Is.SameAs(electronics));
        Assert.That(state.InteriorExplorationActive, Is.True);
        resources.SetAmount(electronics, new ExpantaNum(6));
        Tick(1);
        Near(state.InteriorExplorationProgress, .1);
        Near(resources.GetAmount(machinery), 98);
    }

    [Test]
    public void PartialSupplyPaysOnlyAffordableSecondsAcrossAllInputs()
    {
        SectorState state = Occupy();
        resources.SetAmount(electronics, new ExpantaNum(4.5));
        game.Sectors.TryStartInteriorExploration(sector, out _);
        Tick(8);
        Near(state.InteriorExplorationProgress, .15);
        Near(resources.GetAmount(machinery), 97);
        Near(resources.GetAmount(electronics), 0);
        Assert.That(state.InteriorExplorationActive, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConcurrentExplorationsShareFiniteSupplyInStableIdOrder(bool startInReverseOrder)
    {
        SectorDef other = DataBase<SectorDef>.Find("ThunderGate");
        SectorInteriorExplorationDefinition otherOriginal = other.InteriorExploration;
        try
        {
            var project = new SectorInteriorExplorationDefinition();
            project.ConfigureForEditor("shared supply fixture", new ExpantaNum(10),
                new List<Pair<ResourceDef, ExpantaNum>>
                {
                    new(machinery, new ExpantaNum(2)), new(electronics, new ExpantaNum(3))
                });
            other.SetInteriorExplorationForEditor(project);
            Occupy();
            SectorState otherState = game.Sectors.GetState(other);
            otherState.SetUnlockedForEditor(true);
            otherState.SetOccupiedForEditor(true);
            SectorDef first = string.Compare(sector.Id, other.Id, StringComparison.OrdinalIgnoreCase) < 0
                ? sector : other;
            SectorDef second = first == sector ? other : sector;
            resources.SetAmount(machinery, new ExpantaNum(3));
            Assert.That(game.Sectors.TryStartInteriorExploration(startInReverseOrder ? second : first, out _), Is.True);
            Assert.That(game.Sectors.TryStartInteriorExploration(startInReverseOrder ? first : second, out _), Is.True);

            Tick(8);

            Near(game.Sectors.GetState(first).InteriorExplorationProgress, .15);
            Near(game.Sectors.GetState(second).InteriorExplorationProgress, 0);
            Near(resources.GetAmount(machinery), 0);
            Near(resources.GetAmount(electronics), 95.5);
            Assert.That(game.Sectors.GetState(first).InteriorExplorationActive, Is.True);
            Assert.That(game.Sectors.GetState(second).InteriorExplorationActive, Is.True);
            Tick(8);
            Near(game.Sectors.GetState(first).InteriorExplorationProgress, .15);
            Near(game.Sectors.GetState(second).InteriorExplorationProgress, 0);
            Near(resources.GetAmount(electronics), 95.5);
        }
        finally
        {
            other.SetInteriorExplorationForEditor(otherOriginal);
        }
    }

    [Test]
    public void CompletionClampsTimeAndStopsFuturePayment()
    {
        SectorState state = Occupy();
        game.Sectors.TryStartInteriorExploration(sector, out _);
        Tick(100);
        Assert.That(state.InteriorExplorationCompleted, Is.True);
        Assert.That(state.InteriorExplorationActive, Is.False);
        Near(resources.GetAmount(machinery), 80);
        Near(resources.GetAmount(electronics), 70);
        Tick(100);
        Near(resources.GetAmount(machinery), 80);
        Assert.That(game.Sectors.TryStartInteriorExploration(sector, out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InteriorExplorationCompleted));
    }

    [Test]
    public void ManualPausePreservesProgressAndSupplyUntilContinued()
    {
        SectorState state = Occupy();
        game.Sectors.TryStartInteriorExploration(sector, out _);
        Tick(2);
        game.Sectors.PauseInteriorExploration(sector);
        Tick(20);
        Near(state.InteriorExplorationProgress, .2);
        Near(resources.GetAmount(machinery), 96);
        Assert.That(game.Sectors.GetInteriorExplorationPreview(sector).Status,
            Is.EqualTo(SectorInteriorExplorationStatus.Paused));
        game.Sectors.TryStartInteriorExploration(sector, out _);
        Tick(1);
        Near(state.InteriorExplorationProgress, .3);
    }

    [Test]
    public void OfflineExplorationUsesOrdinaryWindowsAndCommitsFractionalDurationWithoutFoodCadenceRemainder()
    {
        root.AddComponent<BuildingManager>();
        SimulationManager simulation = root.AddComponent<SimulationManager>();
        SectorState state = Occupy();
        Assert.That(game.Sectors.TryStartInteriorExploration(sector, out _), Is.True);
        simulation.AdvanceOffline(2.75d);
        Near(state.InteriorExplorationProgress, .275d);
        Near(resources.GetAmount(machinery), 94.5d);
        Near(resources.GetAmount(electronics), 91.75d);
        Assert.That(simulation.OfflinePendingSimulationSecondsForEditor, Is.EqualTo(0d).Within(1e-9d));
    }

    [TestCase("1.1")]
    [TestCase("NaN")]
    [TestCase(null)]
    public void ExplorationStateRoundTripsAndMalformedRestoreDoesNotMutateStates(string invalidProgress)
    {
        SectorState state = Occupy();
        game.Sectors.TryStartInteriorExploration(sector, out _);
        Tick(2);
        SaveManager.SectorSaveData save = game.Sectors.CaptureSaveData();
        game.Sectors.ResetForLoad();
        game.Sectors.RestoreSaveData(save);
        Near(state.InteriorExplorationProgress, .2);
        Assert.That(state.InteriorExplorationActive, Is.True);
        SaveManager.SectorStateSaveData entry = save.States.Find(value => value.SectorId == sector.Id);
        entry.InteriorExplorationProgress = invalidProgress;
        Assert.Throws<InvalidOperationException>(() => game.Sectors.RestoreSaveData(save));
        Near(state.InteriorExplorationProgress, .2);
        Assert.That(state.InteriorExplorationActive, Is.True);
    }

    [TestCase(true, .2d, true, true)]
    [TestCase(true, 1d, true, false)]
    [TestCase(true, 1d, false, true)]
    [TestCase(false, .2d, false, false)]
    [TestCase(false, 0d, true, false)]
    [TestCase(false, 0d, false, true)]
    public void InvalidExplorationFlagsRejectWholeRestoreWithoutMutatingEarlierSectors(
        bool occupied, double progress, bool active, bool paused)
    {
        SectorState state = Occupy();
        Assert.That(game.Sectors.TryStartInteriorExploration(sector, out _), Is.True);
        Tick(2);
        var versions = new Dictionary<SectorState, int>();
        foreach (SectorState current in game.Sectors.OrderedStates)
            versions.Add(current, current.Version);
        SaveManager.SectorSaveData save = game.Sectors.CaptureSaveData();
        SaveManager.SectorStateSaveData earlier = save.States.Find(value => value.SectorId == "DawnRing");
        earlier.Unlocked = true;
        SaveManager.SectorStateSaveData invalid = save.States.Find(value => value.SectorId == sector.Id);
        invalid.Occupied = occupied;
        invalid.InteriorExplorationProgress = new ExpantaNum(progress).ToString();
        invalid.InteriorExplorationActive = active;
        invalid.InteriorExplorationPaused = paused;
        // Place a valid changed record before the invalid one to detect incremental restore.
        save.States.Remove(earlier);
        save.States.Insert(0, earlier);
        save.States.Remove(invalid);
        save.States.Add(invalid);

        Assert.Throws<InvalidOperationException>(() => game.Sectors.RestoreSaveData(save));

        foreach (KeyValuePair<SectorState, int> previous in versions)
            // Version is a discrete mutation counter; an unsuccessful preflight must not advance it.
            Assert.That(previous.Key.Version, Is.EqualTo(previous.Value), previous.Key.Definition.Id);
        Near(state.InteriorExplorationProgress, .2);
        Assert.That(state.InteriorExplorationActive, Is.True);
        Assert.That(state.InteriorExplorationPaused, Is.False);
        Near(resources.GetAmount(machinery), 96);
        Near(resources.GetAmount(electronics), 94);
    }

    [TestCase(-1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidTimeDoesNotPayOrAdvance(double seconds)
    {
        SectorState state = Occupy();
        game.Sectors.TryStartInteriorExploration(sector, out _);
        Assert.That(game.Sectors.TickActiveInteriorExploration(seconds, game.State, resources,
            out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InvalidDelta));
        Near(state.InteriorExplorationProgress, 0);
        Near(resources.GetAmount(machinery), 100);
    }
}
