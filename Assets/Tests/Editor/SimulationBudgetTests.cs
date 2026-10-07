using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public sealed class SimulationBudgetTests
{
    private GameObject managerObject;

    [TearDown]
    public void TearDown()
    {
        if (managerObject != null)
            Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void Advance_PreservesBacklogWhileRespectingMaximumTicksPerFrame()
    {
        managerObject = new GameObject("Simulation-Budget-Test");
        managerObject.AddComponent<GameManager>();
        managerObject.AddComponent<ResourceManager>();
        managerObject.AddComponent<BuildingManager>();
        managerObject.AddComponent<ResearchManager>();
        SimulationManager simulationManager = managerObject.AddComponent<SimulationManager>();

        double frameBudget =
            simulationManager.TickIntervalSecondsForEditor *
            simulationManager.MaximumTicksPerFrameForEditor;
        simulationManager.Advance(frameBudget * 5d);
        Assert.That(
            simulationManager.AccumulatedSecondsForEditor,
            Is.EqualTo(frameBudget * 4d).Within(0.000001d));

        for (int remainingFrameBudgets = 3; remainingFrameBudgets >= 0; remainingFrameBudgets--)
        {
            simulationManager.Advance(0d);
            Assert.That(
                simulationManager.AccumulatedSecondsForEditor,
                Is.EqualTo(frameBudget * remainingFrameBudgets).Within(0.000001d));
        }
    }

    [Test]
    public void UltraContinuousMaterialDemandParticipatesInSameTickSatisfaction()
    {
        ProgressionModifierManager.Rebuild(null);
        GameObject gameObject = new GameObject("Simulation-Ultra-Supply-GameManager");
        GameObject resourceObject = new GameObject("Simulation-Ultra-Supply-ResourceManager");
        GameObject buildingObject = new GameObject("Simulation-Ultra-Supply-BuildingManager");
        GameObject researchObject = new GameObject("Simulation-Ultra-Supply-ResearchManager");
        Building testBuilding = ScriptableObject.CreateInstance<Building>();
        try
        {
            GameManager gameManager = gameObject.AddComponent<GameManager>();
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            BuildingManager buildingManager = buildingObject.AddComponent<BuildingManager>();
            researchObject.AddComponent<ResearchManager>();
            gameManager.UltraProject.RestoreSaveDataForEditor(
                new UltraProjectStateSaveData
                {
                    ProjectId = UltraProjectState.ProjectId,
                    SaveVersion = UltraProjectState.CurrentSaveVersion,
                    Doctrine = UltraProjectDoctrine.Stable,
                    Status = UltraProjectStatus.Running,
                    CurrentStage = UltraProjectStage.Prototype,
                    StageProgress = "0",
                    CompletedStages = new List<UltraProjectStage>(),
                    LaunchFeePaid = true,
                    StateVersion = 1
                });
            gameManager.State.RestoreCoreForEditor(
                0,
                TechLevel.Animal,
                ExpantaNum.Zero,
                0L);

            UltraProjectPreview preview = gameManager.UltraProject.GetPreview();
            Pair<Resource, ExpantaNum> continuousCost = preview.ContinuousCosts[0];
            resourceManager.SetAmount(continuousCost.First, continuousCost.Second);
            testBuilding.SetIdForEditor("SimulationUltraSupplyConsumer");
            testBuilding.ConfigureEconomyForEditor(
                new ExpantaNum(1.15d),
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                new List<Pair<Resource, ExpantaNum>>(),
                new List<Pair<Resource, ExpantaNum>>(),
                new List<Pair<Resource, ExpantaNum>>
                {
                    new Pair<Resource, ExpantaNum>(continuousCost.First, continuousCost.Second)
                });
            BuildingState state = buildingManager.EnsureBuilding(testBuilding);
            buildingManager.SetAmountAndRatesForEditor(state, ExpantaNum.One);

            buildingManager.PrepareTickResourceSatisfactionForEditor(1d);

            Assert.That(state.Efficiency, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(
                state.Efficiency,
                Is.LessThan(ExpantaNum.One),
                "The Ultra project's live material demand must reduce same-tick building satisfaction.");
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(testBuilding);
            Object.DestroyImmediate(researchObject);
            Object.DestroyImmediate(buildingObject);
            Object.DestroyImmediate(resourceObject);
            Object.DestroyImmediate(gameObject);
        }
    }

    [TestCase(CampaignDoctrine.Stable)]
    [TestCase(CampaignDoctrine.Surge)]
    public void OfflineCampaignMatchesRealtimeForTheSameEffectiveTickSlices(
        CampaignDoctrine doctrine)
    {
        float tickInterval = 0.1f;
        for (int supplyCase = 0; supplyCase < 3; supplyCase++)
        {
            bool supplyLimited = supplyCase == 1;
            bool productionLimited = supplyCase == 2;
            ExpantaNum[] realtime = RunCampaignTicks(
                false, tickInterval, 10, doctrine, supplyLimited, productionLimited);
            ExpantaNum[] offline = RunCampaignTicks(
                true, tickInterval, 10, doctrine, supplyLimited, productionLimited);
            ExpantaNum[] segmentedOffline = RunCampaignTicks(
                true, tickInterval, 10, doctrine, supplyLimited, productionLimited, 7);

            Assert.That(offline.Length, Is.EqualTo(realtime.Length));
            for (int i = 0; i < realtime.Length; i++)
            {
                Assert.That(
                    offline[i].ToDouble(),
                    Is.EqualTo(realtime[i].ToDouble()).Within(1e-8d),
                    $"Campaign result index {i}, doctrine {doctrine}, " +
                    $"limited={supplyLimited}, production={productionLimited}");
                Assert.That(
                    segmentedOffline[i].ToDouble(),
                    Is.EqualTo(realtime[i].ToDouble()).Within(1e-8d),
                    $"Segmented campaign result index {i}, doctrine {doctrine}, " +
                    $"limited={supplyLimited}, production={productionLimited}");
            }
            if (supplyLimited || productionLimited)
                Assert.That(realtime[0], Is.GreaterThan(ExpantaNum.Zero));
        }
    }

    [TestCase(CampaignDoctrine.Stable)]
    [TestCase(CampaignDoctrine.Surge)]
    public void OfflineCampaignRemainderIsAppliedBeforeRealtimeResumes(
        CampaignDoctrine doctrine)
    {
        const float tickSeconds = 0.1f;
        ExpantaNum[] realtime = RunCampaignTicks(
            false, tickSeconds, 1, doctrine, false, false, leadWithHalfTick: true);
        ExpantaNum[] resumed = RunCampaignTicks(
            true, tickSeconds, 1, doctrine, false, false, leadWithHalfTick: true);

        Assert.That(resumed.Length, Is.EqualTo(realtime.Length));
        for (int i = 0; i < realtime.Length; i++)
        {
            Assert.That(
                resumed[i].ToDouble(),
                Is.EqualTo(realtime[i].ToDouble()).Within(1e-8d),
                $"Offline remainder result index {i}, doctrine {doctrine}");
        }
    }

    [Test]
    public void CampaignOperationalDoctrinesChangeProgressLossesAndSupplyUse()
    {
        ExpantaNum[] stable = RunCampaignTicks(
            false, 0.1d, 10, CampaignDoctrine.Stable, false, false);
        ExpantaNum[] surge = RunCampaignTicks(
            false, 0.1d, 10, CampaignDoctrine.Surge, false, false);

        Assert.That(surge[0], Is.GreaterThan(stable[0]), "Surge should advance faster.");
        Assert.That(surge[1], Is.GreaterThan(stable[1]), "Surge should incur more sector losses.");
        Assert.That(surge[2], Is.GreaterThan(stable[2]), "Surge should incur more fleet losses.");
        Assert.That(stable[4], Is.GreaterThan(surge[4]), "Stable should spend less Food.");
        for (int i = 5; i < stable.Length; i++)
            Assert.That(stable[i], Is.GreaterThan(surge[i]), $"Stable should spend less campaign material at index {i}.");
    }

    private static ExpantaNum[] RunCampaignTicks(
        bool offline,
        double tickSeconds,
        int tickCount,
        CampaignDoctrine doctrine,
        bool supplyLimited,
        bool productionLimited,
        int offlineSegmentCount = 1,
        bool leadWithHalfTick = false)
    {
        ProgressionModifierManager.Rebuild(null);
        GameObject gameObject = new GameObject("Simulation-Campaign-Slicing");
        SectorDefinition producerSector = null;
        List<Pair<Resource, ExpantaNum>> originalProducerRates = null;
        try
        {
            GameManager gameManager = gameObject.AddComponent<GameManager>();
            ResourceManager resourceManager = gameObject.AddComponent<ResourceManager>();
            gameObject.AddComponent<BuildingManager>();
            gameObject.AddComponent<ResearchManager>();
            SimulationManager simulationManager = gameObject.AddComponent<SimulationManager>();
            gameManager.UltraProject.RestoreSaveDataForEditor(
                new UltraProjectStateSaveData
                {
                    ProjectId = UltraProjectState.ProjectId,
                    SaveVersion = UltraProjectState.CurrentSaveVersion,
                    Doctrine = UltraProjectDoctrine.Stable,
                    Status = UltraProjectStatus.Committed,
                    CurrentStage = UltraProjectStage.Completed,
                    StageProgress = "1",
                    CompletedStages = new List<UltraProjectStage>
                    {
                        UltraProjectStage.Prototype,
                        UltraProjectStage.Stabilization,
                        UltraProjectStage.Expansion
                    },
                    LaunchFeePaid = true,
                    StateVersion = 10
                });
            SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
            SectorState sectorState = gameManager.Sectors.GetState(sector);
            sectorState.SetUnlockedForEditor(true);
            sectorState.SetCampaignActiveForEditor(true);
            gameManager.State.BeginCampaignForEditor(sector.Id);
            gameManager.State.RestoreCoreForEditor(
                0,
                TechLevel.Spacer,
                new ExpantaNum(1000000d),
                0L);
            gameManager.State.AdjustFoodCapacityForEditor(new ExpantaNum(1000000d));
            gameManager.AdjustAttackPower(new ExpantaNum(1400d));
            gameManager.AdjustDefensePower(new ExpantaNum(3000d));
            gameManager.AdjustFleetPower(new ExpantaNum(1000d));
            gameManager.AdjustMilitaryManpower(new ExpantaNum(2400d));
            gameManager.SetSupplySatisfaction(ExpantaNum.One);
            gameManager.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
            gameManager.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
            gameManager.State.SetCampaignDoctrineForEditor(doctrine);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(
                ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(
                ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(
                ResearchSystem.DeepSpaceFleet);
            SectorCampaignPreview preview = gameManager.Sectors.GetCampaignPreview(
                sector,
                gameManager.State,
                resourceManager);
            if (productionLimited)
            {
                producerSector = DataBase<SectorDefinition>.Find("DawnRing");
                originalProducerRates = new List<Pair<Resource, ExpantaNum>>(
                    producerSector.OccupiedResourceRatesPerSecond);
                producerSector.SetOccupiedResourceRatesForEditor(
                    new List<Pair<Resource, ExpantaNum>>
                    {
                        new Pair<Resource, ExpantaNum>(
                            sector.CampaignResourceRatesPerSecond[0].First,
                            preview.ResourceCostsPerSecond[0].Second * 0.5d)
                    });
                gameManager.Sectors.GetState(producerSector).SetOccupiedForEditor(true);
            }
            if (supplyLimited)
            {
                ExpantaNum availableCampaignSeconds = new ExpantaNum(
                    tickSeconds * (tickCount - 0.5d));
                gameManager.State.RestoreCoreForEditor(
                    0,
                    TechLevel.Spacer,
                    preview.FoodCostPerSecond * availableCampaignSeconds,
                    0L);
                gameManager.State.AdjustFoodCapacityForEditor(new ExpantaNum(1000000d));
            }
            for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
            {
                Pair<Resource, ExpantaNum> rate = sector.CampaignResourceRatesPerSecond[i];
                ExpantaNum amount = supplyLimited
                    ? preview.ResourceCostsPerSecond[i].Second *
                        new ExpantaNum(tickSeconds * (tickCount - 0.5d))
                    : productionLimited && i == 0
                        ? ExpantaNum.Zero
                        : new ExpantaNum(1000000000d);
                resourceManager.SetAmount(rate.First, amount);
            }

            if (leadWithHalfTick)
            {
                if (offline)
                    simulationManager.AdvanceOffline(tickSeconds * 0.5d);
                else
                    simulationManager.ManualTick(tickSeconds * 0.5d);
                simulationManager.ManualTick(tickSeconds);
            }
            else if (offline)
            {
                double remainingSeconds = tickSeconds * tickCount;
                for (int segment = 0; segment < offlineSegmentCount; segment++)
                {
                    double segmentSeconds = segment == offlineSegmentCount - 1
                        ? remainingSeconds
                        : tickSeconds * tickCount / offlineSegmentCount;
                    simulationManager.AdvanceOffline(segmentSeconds);
                    remainingSeconds -= segmentSeconds;
                }
            }
            else
            {
                for (int i = 0; i < tickCount; i++)
                    simulationManager.ManualTick(tickSeconds);
            }

            // Advancing to real-time mode settles any final sub-tick offline
            // remainder before comparing the same effective simulation time.
            simulationManager.ManualTick(1e-9d);

            var result = new ExpantaNum[5 + sector.CampaignResourceRatesPerSecond.Count];
            result[0] = sectorState.CampaignProgress;
            result[1] = sectorState.CampaignCasualties;
            result[2] = gameManager.State.Campaign.Casualties;
            result[3] = CampaignManager.CalculateFleetReadiness(
                gameManager.State.FleetPower,
                gameManager.State.Campaign.Casualties);
            result[4] = gameManager.State.FoodAmount;
            for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
            {
                Resource resource = sector.CampaignResourceRatesPerSecond[i].First;
                result[5 + i] = resourceManager.GetAmount(resource);
            }
            return result;
        }
        finally
        {
            if (producerSector != null)
                producerSector.SetOccupiedResourceRatesForEditor(originalProducerRates);
            Object.DestroyImmediate(gameObject);
            ProgressionModifierManager.Rebuild(null);
        }
    }
}
