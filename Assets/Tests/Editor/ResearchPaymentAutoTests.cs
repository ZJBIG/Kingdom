using NUnit.Framework;
using UnityEngine;

public sealed class ResearchPaymentAutoTests
{
    [Test]
    public void ResearchActionAutomaticallyPaysReadyQueueHead()
    {
        GameObject gameObject = new GameObject("ResearchAutoPayment-GameManager");
        GameManager gameManager = gameObject.AddComponent<GameManager>();
        GameObject resourceObject = new GameObject("ResearchAutoPayment-ResourceManager");
        ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
        GameObject researchObject = new GameObject("ResearchAutoPayment-ResearchManager");
        ResearchManager researchManager = researchObject.AddComponent<ResearchManager>();

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

            researchManager.Tick(0d);

            Assert.That(state.CostPaid, Is.True);
            Assert.That(researchManager.ActiveResearch, Is.EqualTo(state));
            Assert.That(state.CostPaid, Is.True);
            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement =
                    research.ResourceRequirements[i];
                Assert.That(resourceManager.GetAmount(requirement.First),
                    Is.EqualTo(ExpantaNum.One));
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
