using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ProgressionMilestoneRecorderTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private ResourceManager resources;
    private ProgressionMilestoneTestResearchManager research;
    private ProgressionMilestoneRecorder recorder;
    private Research definition;
    private GameObject host;

    [SetUp]
    public void SetUp()
    {
        // Do not destroy or use a user's scene or instantiate a SaveManager.
        Assert.That(Object.FindObjectOfType<GameManager>(), Is.Null);
        Assert.That(Object.FindObjectOfType<ResourceManager>(), Is.Null);
        Assert.That(Object.FindObjectOfType<BuildingManager>(), Is.Null);
        Assert.That(Object.FindObjectOfType<ResearchManager>(), Is.Null);
        Create<GameManager>();
        resources = Create<ResourceManager>();
        resources.EnsureStartingResource();
        Create<BuildingManager>();
        research = Create<ProgressionMilestoneTestResearchManager>();
        research.EnsureInitialized();
        definition = DataBase<Research>.Find("Quarry");
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.HasPositiveResourceRequirement, Is.True);
        foreach (Pair<Resource, ExpantaNum> cost in definition.ResourceRequirements)
            resources.SetAmount(cost.First, ExpantaNum.Zero);
        host = new GameObject("MilestoneRecorderTest");
        objects.Add(host);
        ProgressionMilestoneRecorder.Attach(host, true);
        recorder = host.GetComponent<ProgressionMilestoneRecorder>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null)
                Object.DestroyImmediate(objects[i]);
        objects.Clear();
        ProgressionModifierManager.Rebuild(null);
    }

    [Test]
    public void UnselectedResearch_DoesNotRecordPaymentOrProgress()
    {
        recorder.SampleNow();
        AssertMissingResearchMilestones();
    }

    [Test]
    public void WaitingQueue_Payment_AndPositiveTick_RecordSeparateMilestones()
    {
        Assert.That(research.HandleResearchAction(definition),
            Is.EqualTo(ResearchActionResult.QueuedWaitingResources));
        recorder.SampleNow();
        Assert.That(recorder.FirstResearchQueuedElapsedSeconds, Is.GreaterThanOrEqualTo(0f));
        Assert.That(recorder.FirstResearchPaidElapsedSeconds, Is.LessThan(0f));
        Assert.That(recorder.FirstResearchProgressedElapsedSeconds, Is.LessThan(0f));

        SupplyExactCost();
        research.Tick(0d);
        Assert.That(research.GetState(definition).CostPaid, Is.True);
        recorder.SampleNow();
        Assert.That(recorder.FirstResearchPaidElapsedSeconds,
            Is.GreaterThanOrEqualTo(recorder.FirstResearchQueuedElapsedSeconds));
        Assert.That(recorder.FirstResearchProgressedElapsedSeconds, Is.LessThan(0f));

        research.Tick(0.1d);
        Assert.That(research.GetState(definition).Progress, Is.GreaterThan(ExpantaNum.Zero));
        recorder.SampleNow();
        Assert.That(recorder.FirstResearchProgressedElapsedSeconds,
            Is.GreaterThanOrEqualTo(recorder.FirstResearchPaidElapsedSeconds));
    }

    [Test]
    public void CompletedBetweenSamples_StillRecordsActualProgress()
    {
        SupplyExactCost();
        Assert.That(research.HandleResearchAction(definition),
            Is.EqualTo(ResearchActionResult.Started));
        research.Tick(1000000d);
        Assert.That(research.GetState(definition).Status, Is.EqualTo(ResearchStatus.Completed));
        Assert.That(research.ActiveResearch, Is.Null);
        recorder.SampleNow();
        Assert.That(recorder.FirstResearchQueuedElapsedSeconds, Is.GreaterThanOrEqualTo(0f));
        Assert.That(recorder.FirstResearchPaidElapsedSeconds, Is.GreaterThanOrEqualTo(0f));
        Assert.That(recorder.FirstResearchProgressedElapsedSeconds, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void RepeatedSampling_IsReadOnlyAndDoesNotReplaceFirstObservation()
    {
        SupplyExactCost();
        research.HandleResearchAction(definition);
        research.Tick(0.1d);

        // Sample once first, THEN capture the baseline. Capturing it before the
        // sample would bake in any mutation the sample itself caused and make
        // the "read-only" assertions below vacuous.
        recorder.SampleNow();
        float firstObservation = recorder.FirstResearchProgressedElapsedSeconds;
        Assert.That(firstObservation, Is.GreaterThanOrEqualTo(0f),
            "The first sample must record the already-progressed research.");

        int versionBeforeSecondSample = research.GetState(definition).Version;
        string researchBeforeSecondSample = JsonUtility.ToJson(research.CaptureSaveData());
        string resourcesBeforeSecondSample = JsonUtility.ToJson(resources.CaptureSaveData());

        recorder.SampleNow();

        // Exact comparisons assert immutable snapshots and the same stored
        // timestamp, not a hard-coded economy value or wall-clock duration.
        Assert.That(recorder.FirstResearchProgressedElapsedSeconds, Is.EqualTo(firstObservation),
            "A later sample must not replace the first observation.");
        Assert.That(research.GetState(definition).Version, Is.EqualTo(versionBeforeSecondSample),
            "Sampling must not mutate research state versions.");
        Assert.That(JsonUtility.ToJson(research.CaptureSaveData()), Is.EqualTo(researchBeforeSecondSample));
        Assert.That(JsonUtility.ToJson(resources.CaptureSaveData()), Is.EqualTo(resourcesBeforeSecondSample));
    }

    [Test]
    public void ExistingSaveSession_DoesNotReportNewGameResearchTimings()
    {
        ProgressionMilestoneRecorder.Attach(host, false);
        SupplyExactCost();
        research.HandleResearchAction(definition);
        research.Tick(0.1d);
        recorder.SampleNow();
        AssertMissingResearchMilestones();
    }

    [Test]
    public void ReinitializedSession_ClearsPreviousResearchObservations()
    {
        SupplyExactCost();
        research.HandleResearchAction(definition);
        research.Tick(0.1d);
        recorder.SampleNow();
        research.ResetForPerformanceTest();
        ProgressionMilestoneRecorder.Attach(host, true);
        recorder.SampleNow();
        AssertMissingResearchMilestones();
    }

    private void AssertMissingResearchMilestones()
    {
        Assert.That(recorder.FirstResearchQueuedElapsedSeconds, Is.LessThan(0f));
        Assert.That(recorder.FirstResearchPaidElapsedSeconds, Is.LessThan(0f));
        Assert.That(recorder.FirstResearchProgressedElapsedSeconds, Is.LessThan(0f));
    }

    private void SupplyExactCost()
    {
        // Controlled payment fixture, not a no-cheat pacing/playthrough claim.
        foreach (Pair<Resource, ExpantaNum> cost in definition.ResourceRequirements)
            resources.SetAmount(cost.First, cost.Second);
    }

    private T Create<T>() where T : Component
    {
        GameObject go = new GameObject(typeof(T).Name + "-MilestoneTest");
        objects.Add(go);
        return go.AddComponent<T>();
    }
}

public sealed class ProgressionMilestoneTestResearchManager : ResearchManager
{
    public void EnsureInitialized()
    {
        // EditMode does not guarantee Awake; use the protected typed initializer.
        if (TotalResearchCount == 0)
            base.Initialize();
    }
}
