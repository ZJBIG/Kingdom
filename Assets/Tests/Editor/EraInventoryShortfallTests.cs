using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class EraInventoryShortfallTests
{
    [TestCase(0d, 0d, 1d)]
    [TestCase(0.8d, 0d, 0.2d)]
    [TestCase(1.2d, 0d, 0d)]
    [TestCase(0.2d, 0.5d, 0.3d)]
    [TestCase(0d, 1d, 0d)]
    public void InventoryShortfall_DeductsExistingStockAndPaidLedger(
        double stockRatio, double paidRatio, double expectedMissingRatio)
    {
        var resourceObject = new GameObject("EraShortfall-Resources");
        var researchObject = new GameObject("EraShortfall-Research");
        var gameObject = new GameObject("EraShortfall-Game");
        try
        {
            gameObject.AddComponent<GameManager>();
            ResourceManager resources = resourceObject.AddComponent<ResourceManager>();
            ResearchManager researches = researchObject.AddComponent<ResearchManager>();
            researches.InitializeForEditor();
            Research transition = EraGoalEvaluator.FindTransition(TechLevel.StoneAge);
            Resource wood = DataBase<Resource>.Find("WoodLog");
            ExpantaNum required = ExpantaNum.Zero;
            for (int i = 0; i < transition.ResourceRequirements.Count; i++)
                if (transition.ResourceRequirements[i].First == wood)
                    required += transition.ResourceRequirements[i].Second;
            Assert.That(required, Is.GreaterThan(ExpantaNum.Zero));
            resources.SetAmount(wood, required * new ExpantaNum(stockRatio));
            researches.GetState(transition).RestoreForEditor(ExpantaNum.Zero, false, false,
                new Dictionary<Resource, ExpantaNum> { [wood] = required * new ExpantaNum(paidRatio) });
            EraGoalEvaluation goal = EraGoalEvaluator.Evaluate(TechLevel.Animal, researches, resources);
            EraGoalConditionEvaluation condition = null;
            for (int i = 0; i < goal.Conditions.Count; i++)
                if (goal.Conditions[i].Resource == wood) condition = goal.Conditions[i];
            Assert.That(condition, Is.Not.Null);
            Assert.That((condition.MissingAmount / required).ToDouble(),
                Is.EqualTo(expectedMissingRatio).Within(1e-8));
            Assert.That((condition.RemainingAmount / required).ToDouble(),
                Is.EqualTo(1d - paidRatio).Within(1e-8), "Unpaid cost retains its payment meaning.");
            Assert.That(condition.Met, Is.EqualTo(expectedMissingRatio <= 0d));
        }
        finally
        {
            Object.DestroyImmediate(researchObject);
            Object.DestroyImmediate(resourceObject);
            Object.DestroyImmediate(gameObject);
        }
    }
}
