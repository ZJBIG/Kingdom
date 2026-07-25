using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ResearchEffectTests
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
    public void Rebuild_AppliesCompletedEffectsAndIgnoresIncompleteResearch()
    {
        Research completed = CreateResearch("research-completed");
        Building building = CreateDefinition<Building>("building-unlocked");
        completed.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.GlobalResearchMultiplier,
                Value = new ExpantaNum(2d)
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.ProductivityGranted,
                Value = new ExpantaNum(3d)
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.TerritoryGranted,
                Value = new ExpantaNum(4d)
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.UnlockBuilding,
                Building = building
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.UnlockSystem,
                SystemId = "military"
            }
        });

        Research incomplete = CreateResearch("research-incomplete");
        incomplete.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.GlobalResearchMultiplier,
                Value = new ExpantaNum(99d)
            }
        });

        ResearchState completedState = CreateState(completed, true);
        ResearchState incompleteState = CreateState(incomplete, false);
        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            completedState,
            incompleteState
        });

        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        Assert.That(modifiers.GlobalResearchMultiplier.ToDouble(), Is.EqualTo(2d).Within(0.000001d));
        Assert.That(modifiers.ProductivityGranted.ToDouble(), Is.EqualTo(3d).Within(0.000001d));
        Assert.That(modifiers.TerritoryGranted.ToDouble(), Is.EqualTo(4d).Within(0.000001d));
        Assert.That(modifiers.UnlockedBuildings, Does.Contain(building));
        Assert.That(modifiers.UnlockedSystems, Does.Contain("military"));
    }

    [Test]
    public void Rebuild_MultipliesRepeatedGlobalEffects()
    {
        Research first = CreateResearch("research-first");
        first.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.GlobalResearchMultiplier,
                Value = new ExpantaNum(2d)
            }
        });
        Research second = CreateResearch("research-second");
        second.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.GlobalResearchMultiplier,
                Value = new ExpantaNum(1.5d)
            }
        });

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(first, true),
            CreateState(second, true)
        });

        Assert.That(
            ProgressionModifierManager.Current.GlobalResearchMultiplier.ToDouble(),
            Is.EqualTo(3d).Within(0.000001d));
    }

    private Research CreateResearch(string id)
    {
        Research research = CreateDefinition<Research>(id);
        research.BaseCost = "100";
        return research;
    }

    private ResearchState CreateState(Research research, bool completed)
    {
        ResearchState state = new ResearchState(research);
        MethodInfo restore = typeof(ResearchState).GetMethod(
            "Restore",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(ExpantaNum), typeof(bool), typeof(bool) },
            null);
        restore.Invoke(state, new object[] { ExpantaNum.Zero, false, completed });
        return state;
    }

    private T CreateDefinition<T>(string id) where T : ScriptableObject
    {
        T definition = ScriptableObject.CreateInstance<T>();
        createdObjects.Add(definition);
        definition.name = id;
        if (definition is GameDefinition gameDefinition)
            gameDefinition.SetIdForEditor(id);
        return definition;
    }
}
