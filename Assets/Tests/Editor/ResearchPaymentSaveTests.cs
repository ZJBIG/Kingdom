using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ResearchPaymentSaveTests
{
    private readonly List<GameObject> objects = new();
    private ResearchManager manager;
    private Research target;
    private Resource resource;
    private ExpantaNum required;

    [SetUp]
    public void SetUp()
    {
        foreach (ResearchManager old in UnityEngine.Object.FindObjectsOfType<ResearchManager>()) UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach (ResourceManager old in UnityEngine.Object.FindObjectsOfType<ResourceManager>()) UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach (GameManager old in UnityEngine.Object.FindObjectsOfType<GameManager>()) UnityEngine.Object.DestroyImmediate(old.gameObject);
        ProgressionModifierManager.Rebuild(null);
        CreateManager<GameManager>();
        CreateManager<ResourceManager>().InitializeForEditor();
        manager = CreateManager<ResearchManager>();
        manager.InitializeForEditor();
        target = DataBase<Research>.Find("BronzeAlloyCrafting");
        resource = DataBase<Resource>.Find("BasicMetalIngot");
        required = ExpantaNum.Zero;
        foreach (Pair<Resource, ExpantaNum> cost in target.ResourceRequirements)
            if (cost.First == resource) required += cost.Second;
        Assert.That(required > ExpantaNum.Zero, Is.True);
    }

    private T CreateManager<T>() where T : Component
    {
        var gameObject = new GameObject("ResearchPayment-" + typeof(T).Name);
        objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(objects[i]);
        objects.Clear();
        ProgressionModifierManager.Rebuild(null);
    }

    private SaveManager.ResearchSaveData CreatePaidSave(bool completed)
    {
        var data = new SaveManager.ResearchSaveData
        {
            GlobalEfficiencyFactor = "1",
            States = new List<SaveManager.ResearchStateSaveData>(),
            QueuedResearchIds = new List<string>(),
            SelectedResearchId = string.Empty
        };
        if (completed)
        {
            var included = new HashSet<Research>();
            foreach (Research prerequisite in target.Prerequisites) AddCompleted(prerequisite, data, included);
        }
        var saved = CreateState(target, completed);
        data.States.Add(saved);
        return data;
    }

    private static SaveManager.ResearchStateSaveData CreateState(Research definition, bool completed)
    {
        var saved = new SaveManager.ResearchStateSaveData
        {
            ResearchId = definition.Id,
            Progress = completed ? definition.BaseCost : "0",
            CostPaid = true,
            Completed = completed,
            PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>()
        };
        foreach (Pair<Resource, ExpantaNum> cost in definition.ResourceRequirements)
            if (cost.Second > ExpantaNum.Zero)
                saved.PaidResourceCosts.Add(new SaveManager.ResearchResourceCostSaveData
                {
                    ResourceId = cost.First.Id,
                    Amount = cost.Second.ToString()
                });
        return saved;
    }

    private static void AddCompleted(Research definition, SaveManager.ResearchSaveData data, HashSet<Research> included)
    {
        if (!included.Add(definition)) return;
        foreach (Research prerequisite in definition.Prerequisites) AddCompleted(prerequisite, data, included);
        data.States.Add(CreateState(definition, true));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CurrentSave_PaidLedgerRoundTrips(bool completed)
    {
        manager.RestoreSaveDataForEditor(CreatePaidSave(completed));
        ResearchState state = manager.GetState(target);
        Assert.That(state.CostPaid, Is.True);
        Assert.That(state.GetPaidResourceCost(resource).ToDouble(), Is.EqualTo(required.ToDouble()).Within(1e-7));
        if (completed) Assert.That(state.Status, Is.EqualTo(ResearchStatus.Completed));
        SaveManager.ResearchSaveData saved = manager.CaptureSaveData();
        manager.RestoreSaveDataForEditor(saved);
        Assert.That(manager.GetState(target).GetPaidResourceCost(resource).ToDouble(),
            Is.EqualTo(required.ToDouble()).Within(1e-7));
    }

    [Test]
    public void CurrentSave_NegativeEfficiencyFailsBeforeResearchStateMutation()
    {
        manager.RestoreSaveDataForEditor(CreatePaidSave(false));
        ResearchState before = manager.GetState(target);
        ExpantaNum previousPaid = before.GetPaidResourceCost(resource);
        SaveManager.ResearchSaveData invalid = CreatePaidSave(false);
        invalid.States.Clear();
        invalid.GlobalEfficiencyFactor = "-1";

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.RestoreSaveDataForEditor(invalid));
        Assert.That(before.CostPaid, Is.True);
        Assert.That(before.GetPaidResourceCost(resource).ToDouble(),
            Is.EqualTo(previousPaid.ToDouble()).Within(1e-7));
    }

    [TestCase(null)]
    [TestCase("")]
    public void CurrentSave_MissingLedgerAmountFailsBeforeResearchStateMutation(string missingAmount)
    {
        manager.RestoreSaveDataForEditor(CreatePaidSave(false));
        ResearchState before = manager.GetState(target);
        ExpantaNum previousPaid = before.GetPaidResourceCost(resource);
        SaveManager.ResearchSaveData invalid = CreatePaidSave(false);
        invalid.States[0].CostPaid = false;
        invalid.States[0].PaidResourceCosts[0].Amount = missingAmount;

        Assert.Throws<FormatException>(() => manager.RestoreSaveDataForEditor(invalid));
        Assert.That(before.CostPaid, Is.True);
        Assert.That(before.GetPaidResourceCost(resource).ToDouble(),
            Is.EqualTo(previousPaid.ToDouble()).Within(1e-7));
    }

    [TestCase("duplicate")]
    [TestCase("overpaid")]
    [TestCase("incomplete")]
    [TestCase("unknown")]
    public void CurrentSave_InvalidLedgerFailsBeforeResearchStateMutation(string defect)
    {
        manager.RestoreSaveDataForEditor(CreatePaidSave(false));
        ResearchState before = manager.GetState(target);
        ExpantaNum previousPaid = before.GetPaidResourceCost(resource);
        SaveManager.ResearchSaveData invalid = CreatePaidSave(false);
        List<SaveManager.ResearchResourceCostSaveData> costs = invalid.States[0].PaidResourceCosts;
        SaveManager.ResearchResourceCostSaveData paidCost = costs.Find(cost => cost.ResourceId == resource.Id);
        if (defect == "duplicate") costs.Add(new SaveManager.ResearchResourceCostSaveData { ResourceId = resource.Id, Amount = "0" });
        else if (defect == "overpaid") paidCost.Amount = (required + 1).ToString();
        else if (defect == "unknown") paidCost.ResourceId = "MissingResource";
        else paidCost.Amount = "0";
        Assert.Throws(defect == "unknown" ? typeof(KeyNotFoundException) : typeof(InvalidOperationException),
            () => manager.RestoreSaveDataForEditor(invalid));
        Assert.That(manager.GetState(target), Is.SameAs(before));
        Assert.That(before.CostPaid, Is.True);
        Assert.That(before.GetPaidResourceCost(resource).ToDouble(), Is.EqualTo(previousPaid.ToDouble()).Within(1e-7));
    }
}
