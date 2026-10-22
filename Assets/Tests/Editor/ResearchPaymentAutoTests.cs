using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public sealed class ResearchPaymentAutoTests
{
    [Test]
    public void StartingPaidQueuedResearch_RemovesQueueEntryAndRestoresOwnSave()
    {
        GameObject gameObject = new GameObject("ResearchDirectStart-GameManager");
        GameObject resourceObject = new GameObject("ResearchDirectStart-ResourceManager");
        GameObject buildingObject = new GameObject("ResearchDirectStart-BuildingManager");
        GameObject researchObject = new GameObject("ResearchDirectStart-ResearchManager");
        gameObject.AddComponent<GameManager>();
        ResourceManager resources = resourceObject.AddComponent<ResourceManager>();
        buildingObject.AddComponent<BuildingManager>();
        ResearchManager research = researchObject.AddComponent<ResearchManager>();
        research.InitializeForEditor();

        try
        {
            ResearchDef target = DataBase<ResearchDef>.Find("Quarry");
            Assert.That(research.ArePrerequisitesCompleted(target), Is.True);
            Assert.That(research.CanAccessResearch(target), Is.True);
            Assert.That(target.ResourceRequirements.Count, Is.GreaterThan(0));
            foreach (Pair<ResourceDef, ExpantaNum> cost in target.ResourceRequirements)
                resources.SetAmount(cost.First, ExpantaNum.Zero);

            Assert.That(research.HandleResearchAction(target),
                Is.EqualTo(ResearchActionResult.QueuedWaitingResources));
            Assert.That(research.IsQueued(target), Is.True);
            Assert.That(research.GetState(target).CostPaid, Is.False);
            foreach (Pair<ResourceDef, ExpantaNum> cost in target.ResourceRequirements)
            {
                Assert.That(resources.GetAmount(cost.First), Is.EqualTo(ExpantaNum.Zero),
                    "An underfunded queue attempt must leave every resource balance unchanged.");
                resources.SetAmount(cost.First, cost.Second * 2);
            }

            Assert.That(research.PayResearchCost(target), Is.EqualTo(ResearchPaymentResult.Paid));
            Assert.That(research.StartResearch(target), Is.True);
            Assert.That(research.IsQueued(target), Is.False);
            Assert.That(research.ActiveResearch, Is.SameAs(research.GetState(target)));
            foreach (Pair<ResourceDef, ExpantaNum> cost in target.ResourceRequirements)
                Assert.That(resources.GetAmount(cost.First).ToDouble(),
                    Is.EqualTo(cost.Second.ToDouble()).Within(0.000001d),
                    "Starting an already paid research must not charge it again.");

            SaveManager.ResearchSaveData saved = JsonUtility.FromJson<SaveManager.ResearchSaveData>(
                JsonUtility.ToJson(research.CaptureSaveData()));
            Assert.That(saved.QueuedResearchIds, Does.Not.Contain(target.Id));
            Assert.DoesNotThrow(() => research.RestoreSaveDataForEditor(saved));
            Assert.That(research.ActiveResearch, Is.SameAs(research.GetState(target)));
            Assert.That(research.IsQueued(target), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(researchObject);
            Object.DestroyImmediate(buildingObject);
            Object.DestroyImmediate(resourceObject);
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void OfflineTickTransfersCompletionRemainderToNextResearch()
    {
        GameObject gameObject = new GameObject("ResearchOffline-GameManager");
        GameObject resourceObject = new GameObject("ResearchOffline-ResourceManager");
        GameObject buildingObject = new GameObject("ResearchOffline-BuildingManager");
        GameObject researchObject = new GameObject("ResearchOffline-ResearchManager");
        gameObject.AddComponent<GameManager>();
        ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
        buildingObject.AddComponent<BuildingManager>();
        ResearchManager researchManager = researchObject.AddComponent<ResearchManager>();
        researchManager.InitializeForEditor();

        try
        {
            ResearchDef first = DataBase<ResearchDef>.Find("Quarry");
            ResearchDef second = DataBase<ResearchDef>.Find("Agriculture");
            resourceManager.SetAmount(
                DataBase<ResourceDef>.Find("WoodLog"),
                new ExpantaNum(10000d));

            Assert.That(researchManager.PayResearchCost(first),
                Is.EqualTo(ResearchPaymentResult.Paid));
            Assert.That(researchManager.StartResearch(first), Is.True);
            Assert.That(researchManager.PayResearchCost(second),
                Is.EqualTo(ResearchPaymentResult.Paid));
            Assert.That(researchManager.EnqueueResearch(second), Is.True);

            ResearchState firstState = researchManager.GetState(first);
            firstState.SetProgressForEditor(firstState.BaseCost);
            researchManager.TickOfflineForEditor(10d);

            Assert.That(firstState.Status, Is.EqualTo(ResearchStatus.Completed));
            Assert.That(researchManager.GetState(second).Progress,
                Is.GreaterThan(ExpantaNum.Zero));
        }
        finally
        {
            Object.DestroyImmediate(researchObject);
            Object.DestroyImmediate(buildingObject);
            Object.DestroyImmediate(resourceObject);
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ResearchActionAutomaticallyPaysReadyQueueHead()
    {
        GameObject gameObject = new GameObject("ResearchAutoPayment-GameManager");
        GameManager gameManager = gameObject.AddComponent<GameManager>();
        GameObject resourceObject = new GameObject("ResearchAutoPayment-ResourceManager");
        ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
        GameObject researchObject = new GameObject("ResearchAutoPayment-ResearchManager");
        ResearchManager researchManager = researchObject.AddComponent<ResearchManager>();
        researchManager.InitializeForEditor();

        try
        {
            ResourceDef resource = DataBase<ResourceDef>.Find("WoodLog");
            ResearchDef research = DataBase<ResearchDef>.Find("Quarry");
            Assert.That(resource, Is.Not.Null);
            Assert.That(research.ResourceRequirements.Count, Is.GreaterThan(0));

            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                Pair<ResourceDef, ExpantaNum> requirement =
                    research.ResourceRequirements[i];
                resourceManager.SetAmount(requirement.First,
                    requirement.Second - ExpantaNum.One);
            }

            ResearchActionResult result = researchManager.HandleResearchAction(research);
            ResearchState state = researchManager.GetState(research);

            Assert.That(result, Is.EqualTo(ResearchActionResult.QueuedWaitingResources));
            Assert.That(state.CostPaid, Is.False);
            Assert.That(researchManager.ActiveResearch, Is.Null);

            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                Pair<ResourceDef, ExpantaNum> requirement =
                    research.ResourceRequirements[i];
                resourceManager.SetAmount(requirement.First,
                    requirement.Second + ExpantaNum.One);
            }
            var amountsBeforePayment = new Dictionary<ResourceDef, ExpantaNum>();
            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                ResourceDef paymentResource = research.ResourceRequirements[i].First;
                amountsBeforePayment[paymentResource] = resourceManager.GetAmount(paymentResource);
            }

            researchManager.Tick(0d);

            Assert.That(state.CostPaid, Is.True);
            Assert.That(researchManager.ActiveResearch, Is.EqualTo(state));
            Assert.That(state.CostPaid, Is.True);
            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                Pair<ResourceDef, ExpantaNum> requirement =
                    research.ResourceRequirements[i];
                Assert.That(resourceManager.GetAmount(requirement.First),
                    Is.LessThan(amountsBeforePayment[requirement.First]));
            }
        }
        finally
        {
            Object.DestroyImmediate(researchObject);
            Object.DestroyImmediate(resourceObject);
            Object.DestroyImmediate(gameObject);
        }
    }
}
