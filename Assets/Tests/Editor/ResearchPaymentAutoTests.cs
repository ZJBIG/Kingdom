using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed class ResearchPaymentAutoTests
{
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
        typeof(ResearchManager).GetMethod(
                "Initialize",
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(researchManager, null);

        try
        {
            Research first = DataBase<Research>.Find("Quarry");
            Research second = DataBase<Research>.Find("Agriculture");
            resourceManager.SetAmount(
                DataBase<Resource>.Find("WoodLog"),
                new ExpantaNum(10000d));

            Assert.That(researchManager.PayResearchCost(first),
                Is.EqualTo(ResearchPaymentResult.Paid));
            Assert.That(researchManager.StartResearch(first), Is.True);
            Assert.That(researchManager.PayResearchCost(second),
                Is.EqualTo(ResearchPaymentResult.Paid));
            Assert.That(researchManager.EnqueueResearch(second), Is.True);

            ResearchState firstState = researchManager.GetState(first);
            typeof(ResearchState).GetMethod(
                    "SetProgress",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(firstState, new object[] { firstState.BaseCost });
            typeof(ResearchManager).GetMethod(
                    "TickOffline",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(researchManager, new object[] { 10d });

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
        typeof(ResearchManager).GetMethod(
                "Initialize",
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(researchManager, null);

        try
        {
            Resource resource = DataBase<Resource>.Find("WoodLog");
            Research research = DataBase<Research>.Find("Quarry");
            Assert.That(resource, Is.Not.Null);
            Assert.That(research, Is.Not.Null);
            Assert.That(research.ResourceRequirements.Count, Is.GreaterThan(0));

            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement =
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
                Pair<Resource, ExpantaNum> requirement =
                    research.ResourceRequirements[i];
                resourceManager.SetAmount(requirement.First,
                    requirement.Second + ExpantaNum.One);
            }
            var amountsBeforePayment = new Dictionary<Resource, ExpantaNum>();
            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                Resource paymentResource = research.ResourceRequirements[i].First;
                amountsBeforePayment[paymentResource] = resourceManager.GetAmount(paymentResource);
            }

            researchManager.Tick(0d);

            Assert.That(state.CostPaid, Is.True);
            Assert.That(researchManager.ActiveResearch, Is.EqualTo(state));
            Assert.That(state.CostPaid, Is.True);
            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement =
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
