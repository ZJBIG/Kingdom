using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class ContentProgressionValidatorTests
{
    [Test]
    public void IndustrialProduction_DoesNotGrantOutputsWithoutAllContinuousInputs()
    {
        Building chemicalPlant = UnityEngine.Object.Instantiate(DataBase<Building>.Find("ChemicalPlant"));
        try
        {
            chemicalPlant.SetRequiredResearchForEditor(new List<Research>());
            chemicalPlant.SetRequiredWorkshopUpgradesForEditor(new List<WorkshopUpgrade>());
            var startingResources = new List<string> { "Coke" };
            foreach (Pair<Resource, ExpantaNum> cost in chemicalPlant.ResourceRequirements)
                startingResources.Add(cost.First.Id);
            ProgressionAuditResult result = ContentProgressionAudit.Run(
                DataBase<Resource>.All, new[] { chemicalPlant }, Array.Empty<Research>(),
                startingResources, TechLevel.Industrial);

            Assert.That(result.UnreachableBuildings, Does.Contain(chemicalPlant.Id),
                "A constructed chemical plant cannot produce its first explosives without crude oil.");
            Assert.That(result.ResourcesWithoutSource, Does.Contain("Explosives"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(chemicalPlant);
        }
    }

    [Test]
    public void MainProgression_ReachesUltraWithContinuousInputSources()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All, DataBase<Building>.All, DataBase<Research>.All,
            new[] { "WoodLog" }, TechLevel.Animal);
        Assert.That(result.HighestTechLevel, Is.GreaterThanOrEqualTo(TechLevel.Ultra),
            result.FormatFailureReport());
        Assert.That(result.UnreachableResearch, Is.Empty, result.FormatFailureReport());
        Assert.That(result.UnreachableBuildings, Is.Empty, result.FormatFailureReport());
    }

    [Test]
    public void MainProgression_ReachesMedievalWithoutInjectedResources()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        Assert.That(result.HighestTechLevel, Is.GreaterThanOrEqualTo(TechLevel.Medieval),
            result.FormatFailureReport());
    }

    [Test]
    public void ResourcesWithoutProducersOrConsumers_AreReportedIndividually()
    {
        Resource basicOre = DataBase<Resource>.Find("BasicMetalOre");
        Resource advancedOre = DataBase<Resource>.Find("AdvancedMetalOre");
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            new[] { basicOre, advancedOre }, Array.Empty<Building>(), Array.Empty<Research>(),
            Array.Empty<string>(), TechLevel.Animal);

        Assert.That(result.ResourcesWithoutSource, Is.EquivalentTo(new[] { basicOre.Id, advancedOre.Id }));
        Assert.That(result.ResourcesWithoutSink, Is.EquivalentTo(new[] { basicOre.Id, advancedOre.Id }));
    }

    [Test]
    public void EveryReleasedResource_HasSourceAndSink()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        Assert.That(result.ResourcesWithoutSource, Is.Empty,
            string.Join("\n", result.ResourcesWithoutSource));
        Assert.That(result.ResourcesWithoutSink, Is.Empty,
            string.Join("\n", result.ResourcesWithoutSink));
    }
}

public sealed class ProgressionAuditResult
{
    public TechLevel HighestTechLevel;
    public readonly List<string> ResourcesWithoutSource = new List<string>();
    public readonly List<string> ResourcesWithoutSink = new List<string>();
    public readonly List<string> UnreachableResearch = new List<string>();
    public readonly List<string> UnreachableBuildings = new List<string>();

    public string FormatFailureReport()
    {
        return string.Format(
            "HighestTechLevel={0}; UnreachableResearch=[{1}]; UnreachableBuildings=[{2}]; ResourcesWithoutSource=[{3}]",
            HighestTechLevel,
            string.Join(", ", UnreachableResearch),
            string.Join(", ", UnreachableBuildings),
            string.Join(", ", ResourcesWithoutSource));
    }
}

public static class ContentProgressionAudit
{
    public static ProgressionAuditResult Run(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Building> buildings,
        IReadOnlyList<Research> researches,
        IReadOnlyList<string> startingResourceIds,
        TechLevel startingTechLevel,
        IReadOnlyCollection<string> releasedResourceIds = null)
    {
        var result = new ProgressionAuditResult { HighestTechLevel = startingTechLevel };
        HashSet<string> releasedResources = releasedResourceIds == null
            ? null
            : new HashSet<string>(releasedResourceIds, StringComparer.OrdinalIgnoreCase);
        var reachableResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reachableBuildings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completedResearch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sinkResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < startingResourceIds.Count; i++)
        {
            string id = startingResourceIds[i];
            if (!string.IsNullOrWhiteSpace(id))
            {
                reachableResources.Add(id);
                sourceResources.Add(id);
            }
        }

        bool changed;
        do
        {
            changed = false;

            for (int i = 0; i < researches.Count; i++)
            {
                Research research = researches[i];
                if (research == null || completedResearch.Contains(research.Id))
                    continue;
                if (!IsResearchEraAccessible(research, result.HighestTechLevel) ||
                    !PrerequisitesComplete(research, completedResearch))
                    continue;
                if (!RequirementsAvailable(research.ResourceRequirements, reachableResources))
                    continue;

                completedResearch.Add(research.Id);
                AddPairs(research.ResourceRequirements, reachableResources, sinkResources, false);
                if (research.AdvancesTechLevel && research.TechLevel > result.HighestTechLevel)
                    result.HighestTechLevel = research.TechLevel;
                changed = true;
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building == null || building.TechLevel > result.HighestTechLevel)
                    continue;
                if (!BuildingResearchComplete(building, completedResearch))
                    continue;
                if (!RequirementsAvailable(building.ResourceRequirements, reachableResources) ||
                    !RequirementsAvailable(building.ResourceConsumptionRates, reachableResources))
                    continue;

                if (reachableBuildings.Add(building.Id))
                    changed = true;
                AddPairs(building.ResourceGenerationRates, reachableResources, sourceResources);
                AddPairs(building.ResourceRequirements, reachableResources, sinkResources, false);
                AddPairs(building.ResourceConsumptionRates, reachableResources, sinkResources, false);
            }
        }
        while (changed);

        for (int i = 0; i < resources.Count; i++)
        {
            Resource resource = resources[i];
            if (resource == null)
                continue;
            if (releasedResources != null && !releasedResources.Contains(resource.Id))
                continue;
            if (!sourceResources.Contains(resource.Id))
                result.ResourcesWithoutSource.Add(resource.Id);
            if (!sinkResources.Contains(resource.Id))
                result.ResourcesWithoutSink.Add(resource.Id);
        }

        for (int i = 0; i < researches.Count; i++)
        {
            Research research = researches[i];
            if (research != null && !completedResearch.Contains(research.Id))
                result.UnreachableResearch.Add(research.Id);
        }

        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building != null && !reachableBuildings.Contains(building.Id))
                result.UnreachableBuildings.Add(building.Id);
        }

        return result;
    }

    private static bool PrerequisitesComplete(Research research, HashSet<string> completedResearch)
    {
        for (int i = 0; i < research.Prerequisites.Count; i++)
        {
            Research prerequisite = research.Prerequisites[i];
            if (prerequisite == null || !completedResearch.Contains(prerequisite.Id))
                return false;
        }
        return true;
    }

    private static bool IsResearchEraAccessible(Research research, TechLevel highestTechLevel)
    {
        if (research.TechLevel <= highestTechLevel)
            return true;

        return research.AdvancesTechLevel &&
               (int)research.TechLevel == (int)highestTechLevel + 1;
    }

    private static bool RequirementsAvailable(
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements,
        HashSet<string> reachableResources)
    {
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = requirements[i];
            if (pair.First == null || !reachableResources.Contains(pair.First.Id))
                return false;
        }
        return true;
    }

    private static bool BuildingResearchComplete(
        Building building,
        HashSet<string> completedResearch)
    {
        for (int i = 0; i < building.RequiredResearch.Count; i++)
        {
            Research research = building.RequiredResearch[i];
            if (research == null || !completedResearch.Contains(research.Id))
                return false;
        }
        return true;
    }

    private static void AddPairs(
        IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        HashSet<string> reachableResources,
        HashSet<string> affectedResources,
        bool addReachable = true)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            Resource resource = pairs[i].First;
            if (resource == null)
                continue;
            affectedResources.Add(resource.Id);
            if (addReachable)
                reachableResources.Add(resource.Id);
        }
    }
}
