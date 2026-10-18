using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ResearchPowerTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
    }

    [Test]
    public void CalculateResearchPower_UsesAmountAndEfficiency()
    {
        Building knowledgeCircle = CreateBuilding("KnowledgeCircle", 1d);
        Building scribeHut = CreateBuilding("ScribeHut", 5d);
        var knowledgeState = new BuildingState(knowledgeCircle);
        var scribeState = new BuildingState(scribeHut);
        knowledgeState.SetAmountForEditor(new ExpantaNum(2d));
        scribeState.SetAmountForEditor(new ExpantaNum(3d));
        scribeState.SetEfficiencyForEditor(new ExpantaNum(0.5d));

        ExpantaNum result = ResearchManager.CalculateResearchPower(
            new List<BuildingState> { knowledgeState, scribeState },
            ExpantaNum.One);

        Assert.That(result.ToDouble(), Is.EqualTo(10.5d).Within(0.000001d));
    }

    [Test]
    public void CalculateResearchPower_DoesNotCreateNegativePower()
    {
        Building invalid = CreateBuilding("InvalidResearchBuilding", -10d);
        var state = new BuildingState(invalid);
        state.SetAmountForEditor(new ExpantaNum(5d));

        ExpantaNum result = ResearchManager.CalculateResearchPower(
            new List<BuildingState> { state },
            ExpantaNum.One);

        Assert.That(result, Is.EqualTo(ExpantaNum.One));
    }

    private Building CreateBuilding(string id, double researchPower)
    {
        Building building = ScriptableObject.CreateInstance<Building>();
        building.name = id;
        building.SetIdForEditor(id);
        building.SetResearchPowerForEditor(new ExpantaNum(researchPower));
        createdObjects.Add(building);
        return building;
    }
}
