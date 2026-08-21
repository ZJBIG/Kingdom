using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class SectorDefinitionTests
{
    [Test]
    public void C701_InitialSectorChainContainsStableIdsAndPrerequisites()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");
        SectorDefinition alpha = DataBase<SectorDefinition>.Find("AlphaCentauri");

        Assert.That(lowOrbit.Id, Is.EqualTo("LowOrbit"));
        Assert.That(moon.Id, Is.EqualTo("Moon"));
        Assert.That(mars.Id, Is.EqualTo("Mars"));
        Assert.That(alpha.Domain, Is.EqualTo(SectorDefinition.SectorDomain.Interstellar));
        Assert.That(alpha.StarSystemId, Is.EqualTo("AlphaCentauri"));
        Assert.That(lowOrbit.PrerequisiteSectors, Is.Empty);
        Assert.That(moon.PrerequisiteSectors, Has.Count.EqualTo(1));
        Assert.That(moon.PrerequisiteSectors[0].Id, Is.EqualTo("LowOrbit"));
        Assert.That(mars.PrerequisiteSectors, Has.Count.EqualTo(1));
        Assert.That(mars.PrerequisiteSectors[0].Id, Is.EqualTo("Moon"));
    }

    [Test]
    public void C701_InterstellarBranchesKeepTheProximaBPrerequisite()
    {
        SectorDefinition alpha = DataBase<SectorDefinition>.Find("AlphaCentauri");
        SectorDefinition proxima = DataBase<SectorDefinition>.Find("ProximaB");
        SectorDefinition tau = DataBase<SectorDefinition>.Find("TauCetiFoundry");
        SectorDefinition sirius = DataBase<SectorDefinition>.Find("SiriusResourceBelt");

        Assert.That(proxima.PrerequisiteSectors, Has.Count.EqualTo(1));
        Assert.That(proxima.PrerequisiteSectors[0].Id, Is.EqualTo(alpha.Id));
        Assert.That(tau.PrerequisiteSectors, Has.Count.EqualTo(1));
        Assert.That(tau.PrerequisiteSectors[0].Id, Is.EqualTo(proxima.Id));
        Assert.That(sirius.PrerequisiteSectors, Has.Count.EqualTo(1));
        Assert.That(sirius.PrerequisiteSectors[0].Id, Is.EqualTo(tau.Id));
        Assert.That(tau.EnemyPower, Is.GreaterThan(proxima.EnemyPower));
        Assert.That(tau.EnemyPower, Is.LessThan(sirius.EnemyPower));
    }

    [Test]
    public void C701_SectorsExposeRewardsEnemyPowerAndMapCoordinates()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");

        Assert.That(lowOrbit.EnemyPower, Is.EqualTo(new ExpantaNum(40)));
        Assert.That(lowOrbit.CampaignFoodPerSecond, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(lowOrbit.CampaignResourceRatesPerSecond, Is.Empty);
        Assert.That(lowOrbit.TerritoryReward, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(lowOrbit.ResourceRewards, Has.Count.EqualTo(2));
        Assert.That(moon.EnemyPower, Is.EqualTo(new ExpantaNum(80)));
        Assert.That(moon.TerritoryReward, Is.EqualTo(new ExpantaNum(12000)));
        Assert.That(mars.EnemyPower, Is.EqualTo(new ExpantaNum(160)));
        Assert.That(moon.CampaignFoodPerSecond, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(mars.CampaignFoodPerSecond, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(mars.TerritoryReward, Is.EqualTo(new ExpantaNum(100000)));
        Assert.That(mars.MapX, Is.GreaterThan(moon.MapX));
        Assert.That(DataBase<SectorDefinition>.Find("AlphaCentauri").EnemyPower, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void InterstellarCampaignsProvideMeaningfulTerritoryScale()
    {
        foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
        {
            if (sector == null || sector.IsHomeSystem)
                continue;

            Assert.That(
                sector.TerritoryReward,
                Is.GreaterThanOrEqualTo(new ExpantaNum(100000)),
                $"星际战役星区“{sector.Id}”的领土奖励不能停留在早期探索规模。");
            Assert.That(
                sector.CampaignFoodPerSecond,
                Is.GreaterThan(ExpantaNum.Zero),
                $"星际战役星区“{sector.Id}”必须持续消耗 Food。");
            Assert.That(
                sector.CampaignResourceRatesPerSecond,
                Has.Count.GreaterThanOrEqualTo(5),
                $"星际战役星区“{sector.Id}”必须有多种持续后勤消耗。");
        }
    }

    [Test]
    public void InterstellarCampaignRatesArePositivePerSecondValues()
    {
        foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
        {
            if (sector == null || sector.IsHomeSystem)
                continue;

            Assert.That(
                sector.CampaignProgressMultiplier,
                Is.GreaterThan(ExpantaNum.Zero),
                $"星际战役星区“{sector.Id}”必须声明正的推进倍率。");
            foreach (Pair<Resource, ExpantaNum> pair in sector.CampaignResourceRatesPerSecond)
            {
                Assert.That(pair, Is.Not.Null, sector.Id);
                Assert.That(pair.First, Is.Not.Null, sector.Id);
                Assert.That(
                    pair.Second,
                    Is.GreaterThan(ExpantaNum.Zero),
                    $"星际战役星区“{sector.Id}”包含非正的每秒资源消耗。");
            }
        }
    }

    [Test]
    public void C701_SectorSupplyRatesAreExposedPerSecond()
    {
        SectorDefinition alpha = DataBase<SectorDefinition>.Find("AlphaCentauri");

        Assert.That(alpha.CampaignFoodPerSecond, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(alpha.CampaignResourceRatesPerSecond, Has.Count.GreaterThan(0));
        Assert.That(alpha.CampaignResourceRatesPerSecond[0].Second.ToDouble(), Is.EqualTo(0.8d).Within(0.000001d));
    }

    [Test]
    public void 占领星区必须提供持续资源产出而不是只有一次性奖励()
    {
        foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
        {
            if (sector == null)
                continue;
            Assert.That(sector.OccupiedResourceRatesPerSecond, Is.Not.Null, sector.Id);
            if (!sector.IsHomeSystem)
            {
                Assert.That(sector.OccupiedResourceRatesPerSecond, Is.Not.Empty, sector.Id);
                Assert.That(
                    sector.OccupiedResourceRatesPerSecond.Any(pair =>
                        pair.First != null && pair.Second > ExpantaNum.Zero &&
                        (pair.First.Id == "TitaniumAlloy" ||
                         pair.First.Id == "Composite" ||
                         pair.First.Id == "PhantomAlloy" ||
                         pair.First.Id == "PhantomWeave" ||
                         pair.First.Id == "PhaseMaterial")),
                    Is.True,
                    sector.Id);
            }
        }
    }

    [Test]
    public void 星区开拓持续时间统一使用秒()
    {
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");

        Assert.That(moon.ColonizationDurationSeconds, Is.EqualTo(new ExpantaNum(3600)));
        Assert.That(mars.ColonizationDurationSeconds, Is.EqualTo(new ExpantaNum(7200)));
    }

    [Test]
    public void 本星系中后期开拓必须持续消耗食物与工业资源()
    {
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");

        foreach (SectorDefinition sector in new[] { moon, mars })
        {
            Assert.That(sector.ColonizationFoodPerSecond, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            Assert.That(sector.ColonizationResourceRatesPerSecond, Has.Count.GreaterThanOrEqualTo(3), sector.Id);
            Assert.That(
                sector.ColonizationResourceRatesPerSecond.Any(pair =>
                    pair.First != null && pair.First.Id == "RocketFuel" && pair.Second > ExpantaNum.Zero),
                Is.True,
                sector.Id);
            Assert.That(
                sector.ColonizationResourceRatesPerSecond.Any(pair =>
                    pair.First != null && pair.First.Id == "Composite" && pair.Second > ExpantaNum.Zero),
                Is.True,
                sector.Id);
        }
    }

    [Test]
    public void 火星开拓成本必须高于月球开拓成本()
    {
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");

        Assert.That(mars.ColonizationDurationSeconds, Is.GreaterThan(moon.ColonizationDurationSeconds));
        Assert.That(mars.ColonizationFoodPerSecond, Is.GreaterThan(moon.ColonizationFoodPerSecond));
        Assert.That(FindColonizationRate(mars, "RocketFuel"), Is.GreaterThan(FindColonizationRate(moon, "RocketFuel")));
        Assert.That(FindColonizationRate(mars, "Composite"), Is.GreaterThan(FindColonizationRate(moon, "Composite")));
        Assert.That(FindColonizationRate(mars, "Electronics"), Is.GreaterThan(FindColonizationRate(moon, "Electronics")));
    }

    [Test]
    public void C701_AllCurrentSectorDefinitionsPassRuntimeValidation()
    {
        Assert.That(
            SectorValidator.ValidateDefinitions(DataBase<SectorDefinition>.All, out string error),
            Is.True,
            error);
    }

    [Test]
    public void C701_SectorDefinitionsHaveNoDependencyCycles()
    {
        Assert.That(
            SectorValidator.ValidateNoCycles(DataBase<SectorDefinition>.All, out string error),
            Is.True,
            error);
    }

    [Test]
    public void C701_SectorCycleValidationReportsAllIndependentCycles()
    {
        SectorDefinition a = UnityEngine.ScriptableObject.CreateInstance<SectorDefinition>();
        SectorDefinition b = UnityEngine.ScriptableObject.CreateInstance<SectorDefinition>();
        SectorDefinition c = UnityEngine.ScriptableObject.CreateInstance<SectorDefinition>();
        SectorDefinition d = UnityEngine.ScriptableObject.CreateInstance<SectorDefinition>();
        SectorDefinition e = UnityEngine.ScriptableObject.CreateInstance<SectorDefinition>();
        try
        {
            a.SetIdForEditor("A");
            b.SetIdForEditor("B");
            c.SetIdForEditor("C");
            d.SetIdForEditor("D");
            e.SetIdForEditor("E");
            a.SetPrerequisitesForEditor(new List<SectorDefinition> { b });
            b.SetPrerequisitesForEditor(new List<SectorDefinition> { c });
            c.SetPrerequisitesForEditor(new List<SectorDefinition> { a });
            d.SetPrerequisitesForEditor(new List<SectorDefinition> { e });
            e.SetPrerequisitesForEditor(new List<SectorDefinition> { d });

            Assert.That(
                SectorValidator.ValidateNoCycles(new[] { a, b, c, d, e }, out string error),
                Is.False);
            Assert.That(error, Does.Contain("A -> B -> C -> A"));
            Assert.That(error, Does.Contain("D -> E -> D"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
            UnityEngine.Object.DestroyImmediate(c);
            UnityEngine.Object.DestroyImmediate(d);
            UnityEngine.Object.DestroyImmediate(e);
        }
    }

    private static ExpantaNum FindColonizationRate(SectorDefinition sector, string resourceId)
    {
        for (int i = 0; i < sector.ColonizationResourceRatesPerSecond.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = sector.ColonizationResourceRatesPerSecond[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return pair.Second;
        }
        return ExpantaNum.Zero;
    }

    [Test]
    public void C701_AllInterstellarSectorsHaveAHomeSystemEntryRoute()
    {
        Assert.That(
            SectorValidator.ValidateProgressionReachability(
                DataBase<SectorDefinition>.All,
                out string error),
            Is.True,
            error);
    }

    [Test]
    public void 星际战役必须有大规模领土回报和高级资源持续消耗()
    {
        foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
        {
            if (sector == null || sector.IsHomeSystem)
                continue;

            Assert.That(sector.TerritoryReward, Is.GreaterThanOrEqualTo(new ExpantaNum(100000d)), sector.Id);
            Assert.That(
                sector.CampaignResourceRatesPerSecond.Any(pair =>
                    pair.First != null && pair.Second > ExpantaNum.Zero &&
                    (pair.First.Id == "TitaniumAlloy" ||
                     pair.First.Id == "Composite" ||
                     pair.First.Id == "PhantomAlloy" ||
                     pair.First.Id == "PhantomWeave" ||
                     pair.First.Id == "PhaseMaterial")),
                Is.True,
                sector.Id);
        }
    }

    [Test]
    public void 星际战役必须同时承受燃料高级材料与食物的持续后勤压力()
    {
        foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
        {
            if (sector == null || sector.IsHomeSystem)
                continue;

            Assert.That(sector.CampaignFoodPerSecond, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            Assert.That(sector.CampaignResourceRatesPerSecond, Has.Count.GreaterThanOrEqualTo(5), sector.Id);
            Assert.That(FindCampaignRate(sector, "RocketFuel"), Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            Assert.That(FindCampaignRate(sector, "TitaniumAlloy"), Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            Assert.That(FindCampaignRate(sector, "PhantomWeave"), Is.GreaterThan(ExpantaNum.Zero), sector.Id);
        }
    }

    [Test]
    public void InterstellarCampaignsKeepLayeredContinuousLogistics()
    {
        foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
        {
            if (sector == null || sector.IsHomeSystem)
                continue;

            var resourceIds = sector.CampaignResourceRatesPerSecond
                .Where(pair => pair.First != null && pair.Second > ExpantaNum.Zero)
                .Select(pair => pair.First.Id)
                .Distinct()
                .ToList();

            Assert.That(resourceIds, Has.Count.GreaterThanOrEqualTo(8), sector.Id);
            Assert.That(resourceIds, Does.Contain("RocketFuel"), sector.Id);
            Assert.That(resourceIds, Does.Contain("TitaniumAlloy"), sector.Id);
            Assert.That(resourceIds, Does.Contain("PhantomWeave"), sector.Id);
            Assert.That(
                resourceIds.Any(id => id == "Composite" || id == "PhantomAlloy" || id == "PhaseMaterial"),
                Is.True,
                sector.Id);
        }
    }

    [Test]
    public void InterstellarCampaignsRequireLongSustainedAdvance()
    {
        foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
        {
            if (sector == null || sector.IsHomeSystem || sector.CampaignProgressMultiplier <= ExpantaNum.Zero)
                continue;

            ExpantaNum idealProgressRate = CampaignManager.CalculateProgressRate(new ExpantaNum(2d));
            ExpantaNum idealCompletionSeconds =
                ExpantaNum.One / (idealProgressRate * sector.CampaignProgressMultiplier);

            Assert.That(idealCompletionSeconds, Is.GreaterThanOrEqualTo(new ExpantaNum(3600d)), sector.Id);
        }
    }

    [Test]
    public void 星际战役按星区层级递进使用高级材料()
    {
        SectorDefinition alpha = DataBase<SectorDefinition>.Find("AlphaCentauri");
        SectorDefinition proxima = DataBase<SectorDefinition>.Find("ProximaB");
        SectorDefinition tau = DataBase<SectorDefinition>.Find("TauCetiFoundry");
        SectorDefinition sirius = DataBase<SectorDefinition>.Find("SiriusResourceBelt");

        Assert.That(CountAdvancedMaterials(alpha.CampaignResourceRatesPerSecond), Is.GreaterThanOrEqualTo(2));
        foreach (SectorDefinition sector in new[] { proxima, tau, sirius })
        {
            Assert.That(CountAdvancedMaterials(sector.OccupiedResourceRatesPerSecond), Is.GreaterThanOrEqualTo(2), sector.Id);
            Assert.That(CountAdvancedMaterials(sector.CampaignResourceRatesPerSecond), Is.GreaterThanOrEqualTo(3), sector.Id);
            Assert.That(FindCampaignRate(sector, "PhaseMaterial"), Is.GreaterThan(ExpantaNum.Zero), sector.Id);
        }

        Assert.That(proxima.CampaignProgressMultiplier, Is.GreaterThan(tau.CampaignProgressMultiplier));
        Assert.That(tau.CampaignProgressMultiplier, Is.GreaterThan(sirius.CampaignProgressMultiplier));
        Assert.That(alpha.TerritoryReward, Is.LessThan(proxima.TerritoryReward));
        Assert.That(proxima.TerritoryReward, Is.LessThan(sirius.TerritoryReward));
    }

    private static int CountAdvancedMaterials(IReadOnlyList<Pair<Resource, ExpantaNum>> rates)
    {
        if (rates == null)
            return 0;
        int count = 0;
        for (int i = 0; i < rates.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = rates[i];
            if (pair != null && pair.First != null && pair.Second > ExpantaNum.Zero &&
                (pair.First.Id == "TitaniumAlloy" ||
                 pair.First.Id == "Composite" ||
                 pair.First.Id == "PhantomAlloy" ||
                 pair.First.Id == "PhantomWeave" ||
                 pair.First.Id == "PhaseMaterial"))
                count++;
        }
        return count;
    }

    private static ExpantaNum FindCampaignRate(SectorDefinition sector, string resourceId)
    {
        for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = sector.CampaignResourceRatesPerSecond[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return pair.Second;
        }
        return ExpantaNum.Zero;
    }

    [Test]
    public void C701_InterstellarNavigationResearchOwnsTheStarRouteUnlock()
    {
        Research navigation = DataBase<Research>.Find("InterstellarNavigation");
        Assert.That(navigation, Is.Not.Null);
        Assert.That(navigation.Effects, Has.Some.Matches<ResearchEffectDefinition>(
            effect => effect != null &&
                effect.Type == ResearchEffectType.UnlockInterstellarNavigation));
    }

    [Test]
    public void C701_SectorStateStartsEmptyAndTracksProgress()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        var state = new SectorState(lowOrbit);

        Assert.That(state.Unlocked, Is.False);
        Assert.That(state.Occupied, Is.False);
        Assert.That(state.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.VisitCount, Is.EqualTo(0));

        state.SetUnlockedForEditor(true);
        state.SetCampaignProgressForEditor(new ExpantaNum(0.5d));
        state.SetOccupiedForEditor(true);

        Assert.That(state.Unlocked, Is.True);
        Assert.That(state.Occupied, Is.True);
        Assert.That(state.CampaignProgress.ToDouble(), Is.EqualTo(0.5d).Within(0.000001d));
        Assert.That(state.Version, Is.GreaterThan(0));
    }
}
