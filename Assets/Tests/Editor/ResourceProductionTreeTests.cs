using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ResourceProductionTreeTests
{
    private readonly List<Object> created = new();
    private T Make<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>(); created.Add(value); return value;
    }
    [TearDown] public void CleanUp()
    {
        foreach (Object value in created) Object.DestroyImmediate(value);
        created.Clear();
    }
    private Building Producer(Resource output, Resource input = null, Resource constructionCost = null)
    {
        Building building = Make<Building>();
        building.ConfigureEconomyForEditor(ExpantaNum.One, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            constructionCost == null ? new() : new() { new(constructionCost, 10) },
            new() { new(output, 2) }, input == null ? new() : new() { new(input, 1) });
        return building;
    }

    [Test]
    public void MultipleSourcesRemainSeparate_AndOnlyProductionInputsBecomeDependencies()
    {
        Resource output = Make<Resource>(), input = Make<Resource>(), construction = Make<Resource>();
        Building first = Producer(output, input, construction), second = Producer(output);
        ResourceProductionTree tree = new(output, new[] { first, second }, null);
        var sources = tree.GetChildren(tree.Root);
        Assert.That(sources.Count, Is.EqualTo(2), "Each discrete producer remains a separate branch.");
        Assert.That(sources[0].Building, Is.SameAs(first));
        var dependencies = tree.GetChildren(sources[0]);
        Assert.That(dependencies.Count, Is.EqualTo(1), "Construction costs are not production dependencies.");
        Assert.That(dependencies[0].Resource, Is.SameAs(input));
        Assert.That(tree.GetChildren(sources[1]), Is.Empty);
    }

    [Test]
    public void CyclesStopOnlyTheirOwnPath()
    {
        Resource a = Make<Resource>(), b = Make<Resource>();
        ResourceProductionTree tree = new(a, new[] { Producer(a, b), Producer(b, a) }, null);
        var sourceA = tree.GetChildren(tree.Root)[0];
        var inputB = tree.GetChildren(sourceA)[0];
        var sourceB = tree.GetChildren(inputB)[0];
        var inputA = tree.GetChildren(sourceB)[0];
        Assert.That(inputA.IsCycle, Is.True);
        Assert.That(tree.GetChildren(inputA), Is.Empty);
        Assert.That(inputB.IsCycle, Is.False);
    }

    [Test]
    public void SectorRecurringProductionAndOneTimeRewardStayDistinct()
    {
        Resource resource = Make<Resource>(); SectorDefinition sector = Make<SectorDefinition>();
        sector.SetOccupiedResourceRatesForEditor(new() { new(resource, 3) });
        sector.SetRewardsForEditor(0, new() { new(resource, 20) });
        ResourceProductionTree tree = new(resource, null, new[] { sector });
        var sources = tree.GetChildren(tree.Root);
        Assert.That(sources.Count, Is.EqualTo(2), "Recurring source and one-time reward are discrete source types.");
        Assert.That(sources[0].OneTimeReward, Is.False);
        Assert.That(sources[1].OneTimeReward, Is.True);
        Assert.That(tree.GetChildren(sources[0]), Is.Empty);
    }

    [Test]
    public void SharedInputCanExpandUnderBothAlternativeProducers()
    {
        Resource resource = Make<Resource>(), input = Make<Resource>();
        ResourceProductionTree tree = new(resource, new[] { Producer(resource, input), Producer(resource, input), Producer(input) }, null);
        var sources = tree.GetChildren(tree.Root);
        foreach (var source in sources)
        {
            var dependency = tree.GetChildren(source)[0];
            Assert.That(dependency.IsCycle, Is.False);
            Assert.That(tree.GetChildren(dependency).Count, Is.EqualTo(1), "Path cycles do not globally suppress shared inputs.");
        }
    }
}
