using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class ContentProgressionValidatorTests
{
    [Test]
    public void IndustrialProduction_DoesNotGrantOutputsWithoutAllContinuousInputs()
    {
        BuildingDef chemicalPlant = UnityEngine.Object.Instantiate(DataBase<BuildingDef>.Find("ChemicalPlant"));
        try
        {
            chemicalPlant.SetRequiredResearchForEditor(new List<ResearchDef>());
            chemicalPlant.SetRequiredWorkshopUpgradesForEditor(new List<WorkshopUpgradeDef>());
            var startingResources = new List<string> { "Coke" };
            foreach (Pair<ResourceDef, ExpantaNum> cost in chemicalPlant.ResourceRequirements)
                startingResources.Add(cost.First.Id);
            ProgressionAuditResult result = ContentProgressionAudit.Run(
                DataBase<ResourceDef>.All, new[] { chemicalPlant }, Array.Empty<ResearchDef>(),
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
            DataBase<ResourceDef>.All, DataBase<BuildingDef>.All, DataBase<ResearchDef>.All,
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
            DataBase<ResourceDef>.All,
            DataBase<BuildingDef>.All,
            DataBase<ResearchDef>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);

        Assert.That(result.HighestTechLevel, Is.GreaterThanOrEqualTo(TechLevel.Medieval),
            result.FormatFailureReport());
    }

    [Test]
    public void ResourcesWithoutProducersOrConsumers_AreReportedIndividually()
    {
        ResourceDef basicOre = DataBase<ResourceDef>.Find("BasicMetalOre");
        ResourceDef advancedOre = DataBase<ResourceDef>.Find("AdvancedMetalOre");
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            new[] { basicOre, advancedOre }, Array.Empty<BuildingDef>(), Array.Empty<ResearchDef>(),
            Array.Empty<string>(), TechLevel.Animal);

        Assert.That(result.ResourcesWithoutSource, Is.EquivalentTo(new[] { basicOre.Id, advancedOre.Id }));
        Assert.That(result.ResourcesWithoutSink, Is.EquivalentTo(new[] { basicOre.Id, advancedOre.Id }));
    }

    [Test]
    public void EveryReleasedResource_HasSourceAndSink()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<ResourceDef>.All,
            DataBase<BuildingDef>.All,
            DataBase<ResearchDef>.All,
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
        IReadOnlyList<ResourceDef> resources,
        IReadOnlyList<BuildingDef> buildings,
        IReadOnlyList<ResearchDef> researches,
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
                ResearchDef research = researches[i];
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
                BuildingDef building = buildings[i];
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
            ResourceDef resource = resources[i];
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
            ResearchDef research = researches[i];
            if (research != null && !completedResearch.Contains(research.Id))
                result.UnreachableResearch.Add(research.Id);
        }

        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingDef building = buildings[i];
            if (building != null && !reachableBuildings.Contains(building.Id))
                result.UnreachableBuildings.Add(building.Id);
        }

        return result;
    }

    private static bool PrerequisitesComplete(ResearchDef research, HashSet<string> completedResearch)
    {
        for (int i = 0; i < research.Prerequisites.Count; i++)
        {
            ResearchDef prerequisite = research.Prerequisites[i];
            if (prerequisite == null || !completedResearch.Contains(prerequisite.Id))
                return false;
        }
        return true;
    }

    private static bool IsResearchEraAccessible(ResearchDef research, TechLevel highestTechLevel)
    {
        if (research.TechLevel <= highestTechLevel)
            return true;

        return research.AdvancesTechLevel &&
               (int)research.TechLevel == (int)highestTechLevel + 1;
    }

    private static bool RequirementsAvailable(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements,
        HashSet<string> reachableResources)
    {
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = requirements[i];
            if (pair.First == null || !reachableResources.Contains(pair.First.Id))
                return false;
        }
        return true;
    }

    private static bool BuildingResearchComplete(
        BuildingDef building,
        HashSet<string> completedResearch)
    {
        for (int i = 0; i < building.RequiredResearch.Count; i++)
        {
            ResearchDef research = building.RequiredResearch[i];
            if (research == null || !completedResearch.Contains(research.Id))
                return false;
        }
        return true;
    }

    private static void AddPairs(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> pairs,
        HashSet<string> reachableResources,
        HashSet<string> affectedResources,
        bool addReachable = true)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            ResourceDef resource = pairs[i].First;
            if (resource == null)
                continue;
            affectedResources.Add(resource.Id);
            if (addReachable)
                reachableResources.Add(resource.Id);
        }
    }
}
