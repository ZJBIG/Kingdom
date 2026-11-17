using System.Collections.Generic;
using NUnit.Framework;

public sealed class C5ContentClosureAuditTests
{
    [Test]
    public void C504_AllResearchAndBuildingStableIdsRemainUnique()
    {
        var researchIds = new HashSet<string>();
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
        {
            if (research == null)
                continue;
            Assert.That(researchIds.Add(research.Id), Is.True,
                $"Duplicate research ID '{research.Id}'.");
        }

        var buildingIds = new HashSet<string>();
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            if (building == null)
                continue;
            Assert.That(buildingIds.Add(building.Id), Is.True,
                $"Duplicate building ID '{building.Id}'.");
        }
    }
}
