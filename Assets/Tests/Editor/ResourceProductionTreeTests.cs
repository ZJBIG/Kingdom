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
    public void VisibilityFiltersLockedSources_Inputs_Upgrades_AndSectors()
    {
        Resource output = Make<Resource>(), input = Make<Resource>(), hiddenInput = Make<Resource>();
        Building source = Producer(output, input), lockedSource = Producer(output);
        Building lockedUpgrade = Producer(output, hiddenInput);
        source.SetUpgradeToForEditor(lockedUpgrade);
        SectorDefinition visibleSector = Make<SectorDefinition>(), hiddenSector = Make<SectorDefinition>();
        visibleSector.SetOccupiedResourceRatesForEditor(new() { new(output, 3) });
        hiddenSector.SetOccupiedResourceRatesForEditor(new() { new(output, 3) });
        HashSet<Resource> resources = new() { output, input };
        HashSet<Building> buildings = new() { source };
        ResourceProductionTree tree = new(output, new[] { source, lockedSource },
            new[] { visibleSector, hiddenSector }, resources.Contains, buildings.Contains,
            sector => sector == visibleSector);

        var sources = tree.GetChildren(tree.Root);
        Assert.That(sources.Count, Is.EqualTo(2), "Only the unlocked building and accessible sector remain.");
        Assert.That(sources[0].Building, Is.SameAs(source));
        Assert.That(sources[1].Sector, Is.SameAs(visibleSector));
        Assert.That(tree.GetChildren(sources[0]).Count, Is.EqualTo(1), "The locked upgrade stays hidden.");
        resources.Remove(input);
        Assert.That(tree.RefreshVisibility(), Is.True);
        Assert.That(tree.GetChildren(sources[0]), Is.Empty, "An inaccessible ongoing resource stays hidden.");
    }

    [Test]
    public void UnlockRefreshRevealsCachedBranches_AndPreservesExistingNodesAndExpansion()
    {
        Resource output = Make<Resource>(), input = Make<Resource>();
        Building source = Producer(output, input), lockedSource = Producer(output), upgrade = Producer(output);
        source.SetUpgradeToForEditor(upgrade);
        HashSet<Building> unlocked = new() { source };
        ResourceProductionTree tree = new(output, new[] { source, lockedSource }, null,
            buildingVisible: unlocked.Contains);
        var originalSource = tree.GetChildren(tree.Root)[0];
        originalSource.Expanded = true;
        var originalInput = tree.GetChildren(originalSource)[0];
        originalInput.Expanded = true;
        Assert.That(tree.RefreshVisibility(), Is.False, "An unchanged filter avoids rebuilding the graph.");

        unlocked.Add(lockedSource);
        unlocked.Add(upgrade);
        Assert.That(tree.RefreshVisibility(), Is.True);
        var sources = tree.GetChildren(tree.Root);
        Assert.That(sources.Count, Is.EqualTo(2), "The newly unlocked producer becomes visible.");
        Assert.That(sources[0], Is.SameAs(originalSource));
        Assert.That(sources[0].Expanded, Is.True);
        var children = tree.GetChildren(originalSource);
        Assert.That(children[0], Is.SameAs(originalInput));
        Assert.That(children[0].Expanded, Is.True);
        Assert.That(children[1].Building, Is.SameAs(upgrade));
        Assert.That(children[1].Relationship, Is.EqualTo(ResourceProductionTree.Relation.Upgrade));
        Assert.That(tree.RefreshVisibility(), Is.False);
    }

    [Test]
    public void BuildingRootUsesOngoingInputs_AndDoesNotIncludeConstructionMaterials()
    {
        Resource output = Make<Resource>(), input = Make<Resource>(), construction = Make<Resource>();
        Building root = Producer(output, input, construction);
        Building supplier = Producer(input);
        ResourceProductionTree tree = new(root, new[] { root, supplier }, null);
        Assert.That(tree.Root.Building, Is.SameAs(root));
        Assert.That(tree.Root.Expanded, Is.True);
        var inputs = tree.GetChildren(tree.Root);
        Assert.That(inputs.Count, Is.EqualTo(1), "One discrete ongoing input excludes construction costs.");
        Assert.That(inputs[0].Resource, Is.SameAs(input));
        Assert.That(inputs[0].IsCycle, Is.False);
        Assert.That(tree.GetChildren(inputs[0])[0].Building, Is.SameAs(supplier));
        ResourceProductionTree noInputs = new(supplier, new[] { supplier }, null);
        Assert.That(noInputs.GetChildren(noInputs.Root), Is.Empty);
    }

    [Test]
    public void BuildingUpgradesAndInputProducersRemainDistinct_AndUpgradeCyclesStop()
    {
        Resource output = Make<Resource>(), input = Make<Resource>();
        Building root = Producer(output, input), upgrade = Producer(output), supplier = Producer(input);
        root.SetUpgradeToForEditor(upgrade);
        upgrade.SetUpgradeToForEditor(root);
        ResourceProductionTree tree = new(root, new[] { root, upgrade, supplier }, null);
        var children = tree.GetChildren(tree.Root);
        Assert.That(children.Count, Is.EqualTo(2), "One input and one upgrade are discrete relationships.");
        Assert.That(children[0].Relationship, Is.EqualTo(ResourceProductionTree.Relation.OngoingInput));
        var producer = tree.GetChildren(children[0])[0];
        Assert.That(producer.Building, Is.SameAs(supplier));
        Assert.That(producer.Relationship, Is.EqualTo(ResourceProductionTree.Relation.ProductionSource));
        Assert.That(children[1].Building, Is.SameAs(upgrade));
        Assert.That(children[1].Relationship, Is.EqualTo(ResourceProductionTree.Relation.Upgrade));
        Assert.That(children[1].IsCycle, Is.False);
        var repeated = tree.GetChildren(children[1])[0];
        Assert.That(repeated.Building, Is.SameAs(root));
        Assert.That(repeated.IsCycle, Is.True);
        Assert.That(tree.GetChildren(repeated), Is.Empty);
    }

    [Test]
    public void MultipleSourcesRemainSeparate_AndOnlyProductionInputsBecomeDependencies()
    {
        Resource output = Make<Resource>(), input = Make<Resource>(), construction = Make<Resource>();
        Building first = Producer(output, input, construction), second = Producer(output);
        ResourceProductionTree tree = new(output, new[] { first, second }, null);
        Assert.That(tree.Root.Resource, Is.SameAs(output));
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
