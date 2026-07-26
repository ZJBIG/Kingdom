// Template only. Adapt namespaces/asmdef before placing under Assets/Tests/Editor.
using System.Collections.Generic;
using NUnit.Framework;

public sealed class ContentProgressionValidatorTests
{
    [Test]
    public void MainProgression_ReachesMedievalWithoutInjectedResources()
    {
        ProgressionAuditResult result = ContentProgressionAudit.Run(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            startingResourceIds: new[] { "WoodLog" },
            startingTechLevel: TechLevel.Animal);

        Assert.That(result.HighestTechLevel, Is.GreaterThanOrEqualTo(TechLevel.Medieval),
            result.FormatFailureReport());
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

// Implement ContentProgressionAudit as an Editor/test utility, not runtime gameplay code.
// It should iterate until no new research/building/resource becomes reachable and report
// the first missing prerequisite/resource for each unreachable main-line research.
