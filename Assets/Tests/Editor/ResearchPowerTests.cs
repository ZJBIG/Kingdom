using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ResearchPowerTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        ProgressionModifierManager.Rebuild(null);
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
    }

    [Test]
    public void EffectiveResearchPower_IncludesGlobalEfficiencyResearchAndHappiness()
    {
        GameManager game = CreateManager<GameManager>("EffectivePower-Game");
        ResourceManager resources = CreateManager<ResourceManager>("EffectivePower-Resources");
        CreateManager<BuildingManager>("EffectivePower-Buildings");
        ResearchManager manager = CreateManager<ResearchManager>("EffectivePower-Research");
        manager.InitializeForEditor();
        ResearchDef bonus = ScriptableObject.CreateInstance<ResearchDef>();
        createdObjects.Add(bonus);
        bonus.SetIdForEditor("effective-power-bonus");
        bonus.BaseCost = "1";
        bonus.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.GlobalResearchMultiplier,
                Value = new ExpantaNum(2d)
            }
        });
        var completed = new ResearchState(bonus);
        completed.SetStatusForEditor(ResearchStatus.Completed);
        ProgressionModifierManager.Rebuild(new[] { completed });
        manager.GlobalEfficiencyFactor = new ExpantaNum(1.5d);

        ExpantaNum expected = manager.ResearchPower * 3d * game.State.HappinessMultiplier;
        Assert.That(manager.EffectiveResearchPower.ToDouble(),
            Is.EqualTo(expected.ToDouble()).Within(0.000001d));

        ResearchDef target = DataBase<ResearchDef>.Find("Quarry");
        foreach (Pair<ResourceDef, ExpantaNum> cost in target.ResourceRequirements)
            resources.SetAmount(cost.First, cost.Second * 2d);
        Assert.That(manager.PayResearchCost(target), Is.EqualTo(ResearchPaymentResult.Paid));
        Assert.That(manager.StartResearch(target), Is.True);
        Assert.That(manager.CurrentResearchSpeed.ToDouble(),
            Is.EqualTo(manager.EffectiveResearchPower.ToDouble() *
                ResearchManager.ResearchSpeedEffect(game.State.TechLevel, target.TechLevel))
                .Within(0.000001d));
    }

    private T CreateManager<T>(string name) where T : Component
    {
        var owner = new GameObject(name);
        createdObjects.Add(owner);
        return owner.AddComponent<T>();
    }

    [Test]
    public void CalculateResearchPower_UsesAmountAndEfficiency()
    {
        BuildingDef knowledgeCircle = CreateBuilding("KnowledgeCircle", 1d);
        BuildingDef scribeHut = CreateBuilding("ScribeHut", 5d);
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
        BuildingDef invalid = CreateBuilding("InvalidResearchBuilding", -10d);
        var state = new BuildingState(invalid);
        state.SetAmountForEditor(new ExpantaNum(5d));

        ExpantaNum result = ResearchManager.CalculateResearchPower(
            new List<BuildingState> { state },
            ExpantaNum.One);

        Assert.That(result, Is.EqualTo(ExpantaNum.One));
    }

    private BuildingDef CreateBuilding(string id, double researchPower)
    {
        BuildingDef building = ScriptableObject.CreateInstance<BuildingDef>();
        building.name = id;
        building.SetIdForEditor(id);
        building.SetResearchPowerForEditor(new ExpantaNum(researchPower));
        createdObjects.Add(building);
        return building;
    }
}
