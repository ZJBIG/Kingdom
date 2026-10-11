using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class StrategicPreviewRegressionTests
{
    [Test]
    public void CampaignPostureComparison_PreservesCurrentPlanAndShowsTotalSupplyTradeoff()
    {
        var host = new GameObject("CampaignPosturePreview");
        try
        {
            ProgressionModifierManager.Rebuild(null);
            ResourceManager resources = host.AddComponent<ResourceManager>();
            GameManager game = host.AddComponent<GameManager>();
            host.AddComponent<BuildingManager>();
            host.AddComponent<ResearchManager>().InitializeForEditor();
            game.UltraProject.RestoreSaveDataForEditor(new UltraProjectStateSaveData
            {
                ProjectId = UltraProjectState.ProjectId, SaveVersion = UltraProjectState.CurrentSaveVersion,
                Doctrine = UltraProjectDoctrine.Stable, Status = UltraProjectStatus.Committed,
                CurrentStage = UltraProjectStage.Completed, StageProgress = "1",
                CompletedStages = new List<UltraProjectStage> { UltraProjectStage.Prototype, UltraProjectStage.Stabilization, UltraProjectStage.Expansion },
                LaunchFeePaid = true, StateVersion = 1
            });
            game.State.AdjustAttackPowerForEditor(new ExpantaNum(1e12));
            game.State.AdjustFleetPowerForEditor(new ExpantaNum(1e12));
            game.State.AdjustMilitaryManpowerForEditor(new ExpantaNum(2e12));
            game.State.SetSupplySatisfactionForEditor(ExpantaNum.One);
            game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
            game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
            SectorDefinition sector = DataBase<SectorDefinition>.Find("SiriusResourceBelt");
            int version = game.State.Campaign.Version;
            SectorCampaignPreview stable = game.Sectors.GetCampaignPreview(sector, game.State, resources, CampaignDoctrine.Stable);
            SectorCampaignPreview surge = game.Sectors.GetCampaignPreview(sector, game.State, resources, CampaignDoctrine.Surge);
            Assert.That(game.State.Campaign.Version, Is.EqualTo(version)); // Discrete mutation version.
            Assert.That(game.State.Campaign.Doctrine, Is.EqualTo(CampaignDoctrine.Stable));
            Assert.That(surge.ProgressPerSecond, Is.GreaterThan(stable.ProgressPerSecond));
            Assert.That(surge.FoodCostPerSecond, Is.GreaterThan(stable.FoodCostPerSecond));
            Assert.That(stable.FoodCostPerSecond / stable.ProgressPerSecond,
                Is.GreaterThan(surge.FoodCostPerSecond / surge.ProgressPerSecond));
        }
        finally { Object.DestroyImmediate(host); ProgressionModifierManager.Rebuild(null); }
    }

    [Test]
    public void PartialFleetRepairPreview_IsAffordableAndMatchesTransaction()
    {
        var host = new GameObject("PartialRepairPreview");
        try
        {
            ProgressionModifierManager.Rebuild(null);
            ResourceManager resources = host.AddComponent<ResourceManager>();
            GameManager game = host.AddComponent<GameManager>();
            SectorDefinition sector = DataBase<SectorDefinition>.Find("AlphaCentauri");
            game.Sectors.InitializeDefinitions();
            game.State.RestoreCampaignForEditor(true, sector.Id, new ExpantaNum(10), ExpantaNum.One);
            game.Sectors.GetState(sector).SetCampaignCasualtiesForEditor(new ExpantaNum(10));
            var costs = SectorManager.GetFleetRepairCosts(ExpantaNum.One);
            for (int i = 0; i < costs.Count; i++) resources.SetAmount(costs[i].First, costs[i].Second * new ExpantaNum(4));
            resources.SetAmount(costs[0].First, costs[0].Second * new ExpantaNum(2));
            ExpantaNum amount = game.Sectors.GetMaxAffordableFleetRepair(sector, game.State, resources);
            Assert.That(amount.ToDouble(), Is.EqualTo(2d).Within(1e-8));
            Assert.That(game.State.Campaign.Casualties.ToDouble(), Is.EqualTo(10d).Within(1e-8));
            Assert.That(game.Sectors.TryRepairFleet(sector, game.State, resources, amount,
                out ExpantaNum repaired, out SectorOperationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(repaired.ToDouble(), Is.EqualTo(amount.ToDouble()).Within(1e-8));
            Assert.That(game.State.Campaign.Active, Is.True);
            Assert.That(resources.GetAmount(costs[0].First).ToDouble(), Is.EqualTo(0d).Within(1e-8));
        }
        finally { Object.DestroyImmediate(host); ProgressionModifierManager.Rebuild(null); }
    }

    [Test]
    public void EngineeringPosturePreview_DoesNotMutateStateAndShowsLoadTradeoff()
    {
        var host = new GameObject("PosturePreview");
        try
        {
            ProgressionModifierManager.Rebuild(null);
            ResourceManager resources = host.AddComponent<ResourceManager>();
            GameManager game = host.AddComponent<GameManager>();
            host.AddComponent<BuildingManager>();
            ResearchManager research = host.AddComponent<ResearchManager>();
            research.InitializeForEditor();
            game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum(1e9), 0);
            game.UltraProject.RestoreSaveDataForEditor(new UltraProjectStateSaveData
            {
                ProjectId = UltraProjectState.ProjectId, SaveVersion = UltraProjectState.CurrentSaveVersion,
                Doctrine = UltraProjectDoctrine.Stable, Status = UltraProjectStatus.Ready,
                CurrentStage = UltraProjectStage.Stabilization, StageProgress = "0",
                CompletedStages = new List<UltraProjectStage> { UltraProjectStage.Prototype },
                LaunchFeePaid = false, StateVersion = 1
            });
            UltraProjectStageDefinition stage = game.UltraProject.Definition.Stages[1];
            foreach (var cost in stage.ContinuousResourceCosts) resources.SetAmount(cost.First, new ExpantaNum(1e9));
            game.State.SetFoodAvailabilityForEditor(ExpantaNum.One);
            Assert.That(game.State.HappinessRewardMultiplier, Is.GreaterThan(ExpantaNum.One));
            game.State.AdjustPowerRatesForEditor(stage.PowerConsumptionRate / game.State.HappinessRewardMultiplier, ExpantaNum.Zero);
            game.State.AdjustLogisticsRatesForEditor(stage.LogisticsConsumptionRate / game.State.HappinessRewardMultiplier, ExpantaNum.Zero);
            int version = game.UltraProject.State.Version;
            UltraProjectPreview stable = game.UltraProject.GetPreview(UltraProjectDoctrine.Stable);
            UltraProjectPreview surge = game.UltraProject.GetPreview(UltraProjectDoctrine.Surge);
            Assert.That(game.UltraProject.State.Version, Is.EqualTo(version)); // Discrete mutation version.
            Assert.That(game.UltraProject.State.Doctrine, Is.EqualTo(UltraProjectDoctrine.Stable));
            Assert.That(surge.Doctrine, Is.EqualTo(UltraProjectDoctrine.Surge));
            Assert.That(surge.PowerPerSecond, Is.GreaterThan(stable.PowerPerSecond));
            Assert.That(surge.SupplySatisfaction, Is.LessThan(stable.SupplySatisfaction));
            Assert.That(stable.ProgressPerSecond, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(stable.SupplySatisfaction.ToDouble(), Is.EqualTo(1d).Within(1e-8));
            Assert.That(game.State.FoodAmount.ToDouble(), Is.EqualTo(1e9).Within(1));
        }
        finally { Object.DestroyImmediate(host); ProgressionModifierManager.Rebuild(null); }
    }
}
