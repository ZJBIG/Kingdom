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
    private BuildingDef Producer(ResourceDef output, ResourceDef input = null, ResourceDef constructionCost = null)
    {
        BuildingDef building = Make<BuildingDef>();
        building.ConfigureEconomyForEditor(ExpantaNum.One, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            constructionCost == null ? new() : new() { new(constructionCost, 10) },
            new() { new(output, 2) }, input == null ? new() : new() { new(input, 1) });
        return building;
    }

    [Test]
    public void VisibilityFiltersLockedSources_Inputs_Upgrades_AndSectors()
    {
        ResourceDef output = Make<ResourceDef>(), input = Make<ResourceDef>(), hiddenInput = Make<ResourceDef>();
        BuildingDef source = Producer(output, input), lockedSource = Producer(output);
        BuildingDef lockedUpgrade = Producer(output, hiddenInput);
        source.SetUpgradeToForEditor(lockedUpgrade);
        SectorDef visibleSector = Make<SectorDef>(), hiddenSector = Make<SectorDef>();
        visibleSector.SetOccupiedResourceRatesForEditor(new() { new(output, 3) });
        hiddenSector.SetOccupiedResourceRatesForEditor(new() { new(output, 3) });
        HashSet<ResourceDef> resources = new() { output, input };
        HashSet<BuildingDef> buildings = new() { source };
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
        ResourceDef output = Make<ResourceDef>(), input = Make<ResourceDef>();
        BuildingDef source = Producer(output, input), lockedSource = Producer(output), upgrade = Producer(output);
        source.SetUpgradeToForEditor(upgrade);
        HashSet<BuildingDef> unlocked = new() { source };
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
        ResourceDef output = Make<ResourceDef>(), input = Make<ResourceDef>(), construction = Make<ResourceDef>();
        BuildingDef root = Producer(output, input, construction);
        BuildingDef supplier = Producer(input);
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
        ResourceDef output = Make<ResourceDef>(), input = Make<ResourceDef>();
        BuildingDef root = Producer(output, input), upgrade = Producer(output), supplier = Producer(input);
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
        Assert.That(repeated.ReferenceTarget, Is.SameAs(tree.Root),
            "An upgrade loop references the already displayed building instead of recursing.");
        Assert.That(tree.GetChildren(repeated), Is.Empty);
    }

    [Test]
    public void MultipleSourcesRemainSeparate_AndOnlyProductionInputsBecomeDependencies()
    {
        ResourceDef output = Make<ResourceDef>(), input = Make<ResourceDef>(), construction = Make<ResourceDef>();
        BuildingDef first = Producer(output, input, construction), second = Producer(output);
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
        ResourceDef a = Make<ResourceDef>(), b = Make<ResourceDef>();
        ResourceProductionTree tree = new(a, new[] { Producer(a, b), Producer(b, a) }, null);
        var sourceA = tree.GetChildren(tree.Root)[0];
        var inputB = tree.GetChildren(sourceA)[0];
        var sourceB = tree.GetChildren(inputB)[0];
        var inputA = tree.GetChildren(sourceB)[0];
        Assert.That(inputA.IsCycle, Is.True);
        Assert.That(inputA.ReferenceTarget, Is.SameAs(tree.Root));
        Assert.That(tree.GetChildren(inputA), Is.Empty);
        Assert.That(inputB.IsCycle, Is.False);
    }

    [Test]
    public void ActualOilLoop_ReferencesExistingOil_AndKeepsIndependentStarterSource()
    {
        ResourceDef oil = DataBase<ResourceDef>.Find("CrudeOil");
        BuildingDef complex = DataBase<BuildingDef>.Find("IndustrialOilExtractionComplex");
        BuildingDef chemical = DataBase<BuildingDef>.Find("ChemicalPlant");
        BuildingDef starter = DataBase<BuildingDef>.Find("OilDerrick");
        ResourceProductionTree tree = new(oil, new[] { complex, chemical, starter }, null);
        ResourceProductionTree.Node advanced = null, independent = null;
        foreach (ResourceProductionTree.Node source in tree.GetChildren(tree.Root))
        {
            if (source.Building == complex) advanced = source;
            if (source.Building == starter) independent = source;
        }
        Assert.That(advanced, Is.Not.Null);
        Assert.That(independent, Is.Not.Null, "The oil source without ongoing material input remains visible.");
        ResourceProductionTree.Node explosives = null;
        foreach (ResourceProductionTree.Node input in tree.GetChildren(advanced))
            if (input.Resource.Id == "Explosives") explosives = input;
        Assert.That(explosives, Is.Not.Null);
        ResourceProductionTree.Node chemicalSource = tree.GetChildren(explosives)[0];
        ResourceProductionTree.Node repeatedOil = null;
        foreach (ResourceProductionTree.Node input in tree.GetChildren(chemicalSource))
            if (input.Resource == oil) repeatedOil = input;
        Assert.That(repeatedOil, Is.Not.Null);
        Assert.That(repeatedOil.ReferenceTarget, Is.SameAs(tree.Root));
        Assert.That(tree.GetChildren(repeatedOil), Is.Empty,
            "Real reciprocal industrial recipes terminate as a reference, not a second subtree.");
        foreach (Pair<ResourceDef, ExpantaNum> input in starter.ResourceConsumptionRates)
            Assert.That(input.Second, Is.LessThanOrEqualTo(ExpantaNum.Zero),
                "A cycle is not a start-up deadlock when the initial oil source has no ongoing material cost.");
    }

    [Test]
    public void SectorRecurringProductionAndOneTimeRewardStayDistinct()
    {
        ResourceDef resource = Make<ResourceDef>(); SectorDef sector = Make<SectorDef>();
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
        ResourceDef resource = Make<ResourceDef>(), input = Make<ResourceDef>();
        ResourceProductionTree tree = new(resource, new[] { Producer(resource, input), Producer(resource, input), Producer(input) }, null);
        var sources = tree.GetChildren(tree.Root);
        foreach (var source in sources)
        {
            var dependency = tree.GetChildren(source)[0];
            Assert.That(dependency.IsCycle, Is.False);
            Assert.That(tree.GetChildren(dependency).Count, Is.EqualTo(1), "Path cycles do not globally suppress shared inputs.");
        }
    }

    [Test]
    public void SectorRootFiltersLocalFacilities_AndUnlockRefreshPreservesExpandedSupplyBranches()
    {
        SectorDef sector = Make<SectorDef>(), otherSector = Make<SectorDef>();
        ResourceDef output = Make<ResourceDef>(), input = Make<ResourceDef>();
        SectorBuildingDef local = Make<SectorBuildingDef>(), locked = Make<SectorBuildingDef>(), foreign = Make<SectorBuildingDef>();
        local.SetSectorForEditor(sector);
        locked.SetSectorForEditor(sector);
        foreign.SetSectorForEditor(otherSector);
        local.ConfigureEconomyForEditor(ExpantaNum.One, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new(), new() { new(output, 2) }, new() { new(input, 1) });
        BuildingDef supplier = Producer(input), ordinaryProducer = Producer(output);
        HashSet<BuildingDef> unlocked = new() { local, foreign, supplier, ordinaryProducer };
        ResourceProductionTree tree = new(sector, new BuildingDef[] { local, locked, foreign, supplier, ordinaryProducer },
            new[] { sector, otherSector }, buildingVisible: unlocked.Contains);

        Assert.That(tree.Root.Sector, Is.SameAs(sector));
        Assert.That(tree.Root.Expanded, Is.True);
        var facilities = tree.GetChildren(tree.Root);
        Assert.That(facilities.Count, Is.EqualTo(1), "Only the unlocked facility belonging to this sector appears.");
        var originalFacility = facilities[0];
        Assert.That(originalFacility.Building, Is.SameAs(local));
        Assert.That(originalFacility.Relationship, Is.EqualTo(ResourceProductionTree.Relation.SectorFacility));
        originalFacility.Expanded = true;
        var originalInput = tree.GetChildren(originalFacility)[0];
        originalInput.Expanded = true;
        Assert.That(originalInput.Resource, Is.SameAs(input));
        Assert.That(tree.GetChildren(originalInput)[0].Building, Is.SameAs(supplier),
            "A sector facility traces its ongoing input to the ordinary supply building.");

        unlocked.Add(locked);
        Assert.That(tree.RefreshVisibility(), Is.True);
        facilities = tree.GetChildren(tree.Root);
        Assert.That(facilities.Count, Is.EqualTo(2));
        Assert.That(facilities[1].Building, Is.SameAs(locked));
        Assert.That(facilities[0], Is.SameAs(originalFacility));
        Assert.That(facilities[0].Expanded, Is.True);
        Assert.That(tree.GetChildren(facilities[0])[0], Is.SameAs(originalInput));
        Assert.That(originalInput.Expanded, Is.True);
        Assert.That(tree.RefreshVisibility(), Is.False);
    }

    [Test]
    public void SectorRootKeepsRecurringOutputsAndRewardsSeparate_AndSectorSourcesRemainLeaves()
    {
        ResourceDef resource = Make<ResourceDef>(), hidden = Make<ResourceDef>();
        SectorDef sector = Make<SectorDef>();
        sector.SetOccupiedResourceRatesForEditor(new() { new(resource, 3), new(hidden, 1) });
        sector.SetRewardsForEditor(0, new() { new(resource, 20), new(hidden, 1) });
        ResourceProductionTree tree = new(sector, null, new[] { sector }, resourceVisible: value => value == resource);

        var outputs = tree.GetChildren(tree.Root);
        Assert.That(outputs.Count, Is.EqualTo(2), "Hidden resources are excluded from both recurring and reward outputs.");
        Assert.That(outputs[0].Resource, Is.SameAs(resource));
        Assert.That(outputs[1].Resource, Is.SameAs(resource));
        Assert.That(outputs[0].Relationship, Is.EqualTo(ResourceProductionTree.Relation.SectorOutput));
        Assert.That(outputs[1].Relationship, Is.EqualTo(ResourceProductionTree.Relation.SectorOutput));
        Assert.That(outputs[0].OneTimeReward, Is.False);
        Assert.That(outputs[1].OneTimeReward, Is.True);
        Assert.That(outputs[1].Rate > outputs[0].Rate, Is.True, "A reward amount remains distinct from the recurring rate.");
        var sources = tree.GetChildren(outputs[0]);
        Assert.That(sources.Count, Is.EqualTo(2));
        foreach (var source in sources)
        {
            Assert.That(source.Sector, Is.SameAs(sector));
            Assert.That(source.Relationship, Is.EqualTo(ResourceProductionTree.Relation.ProductionSource));
            Assert.That(tree.GetChildren(source), Is.Empty, "A source sector cannot recursively reopen the sector root.");
        }
    }

    [Test]
    public void UnlockedLowerSourcesRemainAlongsideHigherTiers_AndKeepCachedExpansion()
    {
        ResourceDef output = Make<ResourceDef>();
        BuildingDef lower = Producer(output), higher = Producer(output);
        lower.SetUpgradeToForEditor(higher);
        HashSet<BuildingDef> unlocked = new() { lower };
        ResourceProductionTree tree = new(output, new[] { lower, higher }, null,
            buildingVisible: unlocked.Contains);
        var originalLower = tree.GetChildren(tree.Root)[0];
        originalLower.Expanded = true;
        Assert.That(tree.GetChildren(originalLower), Is.Empty, "The upgrade must still satisfy its prerequisites.");

        unlocked.Add(higher);
        Assert.That(tree.RefreshVisibility(), Is.True);
        var sources = tree.GetChildren(tree.Root);
        Assert.That(sources.Count, Is.EqualTo(2), "Unlocked lower sources remain even when a higher tier is available.");
        Assert.That(sources[0], Is.SameAs(originalLower));
        Assert.That(originalLower.Expanded, Is.True);
        var originalUpgrade = tree.GetChildren(originalLower)[0];
        Assert.That(originalUpgrade.Building, Is.SameAs(higher));
        Assert.That(originalUpgrade.Relationship, Is.EqualTo(ResourceProductionTree.Relation.Upgrade));
        Assert.That(tree.RefreshVisibility(), Is.False);
        Assert.That(tree.GetChildren(originalLower)[0], Is.SameAs(originalUpgrade));

        SectorDef sector = Make<SectorDef>();
        SectorBuildingDef facility = Make<SectorBuildingDef>();
        facility.SetSectorForEditor(sector);
        ResourceProductionTree sectorTree = new(sector, new BuildingDef[] { facility }, null,
            buildingVisible: unlocked.Contains);
        Assert.That(sectorTree.GetChildren(sectorTree.Root), Is.Empty);
        unlocked.Add(facility);
        Assert.That(sectorTree.RefreshVisibility(), Is.True);
        Assert.That(sectorTree.GetChildren(sectorTree.Root)[0].Building, Is.SameAs(facility));
    }

    [Test]
    public void ResourceSourcesOrderHigherErasFirst_AndPreserveAuthoredOrderWithinAnEra()
    {
        ResourceDef output = Make<ResourceDef>();
        BuildingDef lower = Producer(output), firstHigher = Producer(output), secondHigher = Producer(output);
        lower.TechLevel = TechLevel.Animal;
        firstHigher.TechLevel = secondHigher.TechLevel = TechLevel.Ultra;
        ResourceProductionTree tree = new(output, new[] { lower, firstHigher, secondHigher }, null);

        var sources = tree.GetChildren(tree.Root);
        Assert.That(sources.Count, Is.EqualTo(3));
        Assert.That(sources[0].Building, Is.SameAs(firstHigher));
        Assert.That(sources[1].Building, Is.SameAs(secondHigher), "Equal-era sources retain definition order.");
        Assert.That(sources[2].Building, Is.SameAs(lower));
    }
}
