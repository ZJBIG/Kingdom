using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class RelicCampaignTests
{
    [Test]
    public void Support_DiscountsActualFoodAndEveryMaterialWithoutChangingCombat()
    {
        CampaignPayment ordinary = RunCampaign(false);
        CampaignPayment supported = RunCampaign(true);
        Near(supported.Food, ordinary.Food * .85d);
        Assert.That(ordinary.Food, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(ordinary.Materials.Count, Is.GreaterThan(1));
        foreach (var entry in ordinary.Materials)
        {
            Assert.That(entry.Value, Is.GreaterThan(ExpantaNum.Zero));
            Near(supported.Materials[entry.Key], entry.Value * .85d);
        }
        Near(supported.Progress, ordinary.Progress);
        Near(supported.CombatRatio, ordinary.CombatRatio);
        Near(supported.Casualties, ordinary.Casualties);
    }

    [Test]
    public void Campaign_MissingLastMaterialPaysNothingAndKeepsAssignedSupport()
    {
        using var f = new Fixture();
        f.PrepareAndAssign();
        var rates = f.Target.CampaignResourceRatesPerSecond;
        f.Resources.SetAmount(rates[rates.Count - 1].First, ExpantaNum.Zero);
        var balances = f.Capture(rates);
        ExpantaNum food = f.Game.State.FoodAmount;
        ExpantaNum progress = f.TargetState.CampaignProgress;
        ExpantaNum casualties = f.Game.State.Campaign.Casualties;
        ExpantaNum combatRatio = f.Game.State.Campaign.CombatRatio;
        int campaignVersion = f.Game.State.Campaign.Version;
        int version = f.Relic.State.Version;
        Assert.That(f.Advance(10d, out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InsufficientCampaignSupply));
        Near(f.Game.State.FoodAmount, food);
        Near(f.TargetState.CampaignProgress, progress);
        Near(f.Game.State.Campaign.Casualties, casualties);
        Near(f.Game.State.Campaign.CombatRatio, combatRatio);
        Assert.That(f.Game.State.Campaign.Version, Is.EqualTo(campaignVersion));
        f.AssertBalances(balances);
        Assert.That(f.Game.State.Campaign.Active, Is.True);
        Assert.That(f.Relic.State.SupportedSectorId, Is.EqualTo(f.Target.Id));
        Assert.That(f.Relic.State.Version, Is.EqualTo(version));
    }

    [Test]
    public void Retreat_ConsumesSupportWithoutRefundAndAllowsAnotherWorkshopPurchase()
    {
        using var f = new Fixture();
        f.PrepareAndAssign();
        Assert.That(f.Advance(10d, out _), Is.True);
        var balances = f.Capture(f.Definition.SupportCraftCosts);
        ExpantaNum food = f.Game.State.FoodAmount;
        Assert.That(f.Game.Sectors.CancelCampaign(f.Target, f.Game.State), Is.True);
        Assert.That(f.Game.State.Campaign.Active, Is.False);
        Assert.That(f.Relic.State.SupportedSectorId, Is.Empty);
        Assert.That(f.Relic.State.SupportReady, Is.False);
        Near(f.Game.State.FoodAmount, food);
        f.AssertBalances(balances);
        Assert.That(f.Workshop.TryCraftRelicSupport(out RelicOperationFailure failure), Is.True);
        Assert.That(failure, Is.EqualTo(RelicOperationFailure.None));
        foreach (var cost in f.Definition.SupportCraftCosts)
            Near(balances[cost.First] - f.Resources.GetAmount(cost.First), cost.Second);
        Assert.That(f.Relic.State.SupportReady, Is.True);
    }

    [Test]
    public void Completion_ClearsServingSupportAndDoesNotRestorePreparedSupport()
    {
        using var f = new Fixture();
        f.PrepareAndAssign();
        f.TargetState.SetCampaignProgressForEditor(new ExpantaNum(.99999d));
        Assert.That(f.Advance(10d, out SectorOperationFailure failure), Is.True);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
        Assert.That(f.TargetState.Occupied, Is.True);
        Assert.That(f.Game.State.Campaign.Active, Is.False);
        Assert.That(f.Relic.State.SupportedSectorId, Is.Empty);
        Assert.That(f.Relic.State.SupportReady, Is.False);
        Assert.That(f.Workshop.TryCraftRelicSupport(out _), Is.True);
    }

    [Test]
    public void ServingSupport_OnlyDiscountsMatchingTargetAndOtherCampaignCannotSpend()
    {
        using var f = new Fixture();
        f.PrepareAndAssign();
        SectorDefinition other = DataBase<SectorDefinition>.Find("AlphaCentauri");
        f.Game.Sectors.GetState(other).SetUnlockedForEditor(true);
        SectorCampaignPreview preview = f.Game.Sectors.GetCampaignPreview(other, f.Game.State, f.Resources);
        Near(preview.FoodCostPerSecond,
            other.CampaignFoodPerSecond * ProgressionModifierManager.Current.CampaignSupplyCostMultiplier);
        foreach (var cost in other.CampaignResourceRatesPerSecond)
        {
            bool found = false;
            foreach (var rate in preview.ResourceCostsPerSecond)
                if (rate.First == cost.First)
                {
                    Near(rate.Second, cost.Second * ProgressionModifierManager.Current.CampaignSupplyCostMultiplier);
                    found = true;
                }
            Assert.That(found, Is.True);
        }
        var balances = f.Capture(other.CampaignResourceRatesPerSecond);
        ExpantaNum food = f.Game.State.FoodAmount;
        Assert.That(f.Game.Sectors.TryAdvanceCampaign(other, 10d, f.Game.State, f.Resources,
            out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.CampaignInProgress));
        Near(f.Game.State.FoodAmount, food);
        f.AssertBalances(balances);
        Assert.That(f.Relic.State.SupportedSectorId, Is.EqualTo(f.Target.Id));
        Assert.That(f.Relic.TryAssignSupport(out _), Is.False);
    }

    [Test]
    public void Workshop_LockedRejectsBeforePayment()
    {
        using var f = new Fixture();
        f.MakeOperational(RelicRoute.Dismantle);
        ProgressionModifierManager.Rebuild(null);
        var balances = f.Capture(f.Definition.SupportCraftCosts);
        int version = f.Relic.State.Version;
        Assert.That(f.Workshop.TryCraftRelicSupport(out RelicOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(RelicOperationFailure.PrerequisiteResearchMissing));
        f.AssertBalances(balances);
        Assert.That(f.Relic.State.Version, Is.EqualTo(version));
    }

    [Test]
    public void Workshop_RepairRouteRejectsWithoutPayment()
    {
        using var f = new Fixture();
        f.MakeOperational(RelicRoute.Repair);
        var balances = f.Capture(f.Definition.SupportCraftCosts);
        Assert.That(f.Workshop.TryCraftRelicSupport(out RelicOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(RelicOperationFailure.InvalidState));
        f.AssertBalances(balances);
        Assert.That(f.Relic.State.SupportReady, Is.False);
    }

    [Test]
    public void Workshop_DismantlePaysEveryIngredientOnceAndRejectsDuplicatePurchase()
    {
        using var f = new Fixture();
        f.MakeOperational(RelicRoute.Dismantle);
        var before = f.Capture(f.Definition.SupportCraftCosts);
        Assert.That(f.Workshop.TryCraftRelicSupport(out RelicOperationFailure failure), Is.True);
        Assert.That(failure, Is.EqualTo(RelicOperationFailure.None));
        foreach (var cost in f.Definition.SupportCraftCosts)
            Near(before[cost.First] - f.Resources.GetAmount(cost.First), cost.Second);
        var after = f.Capture(f.Definition.SupportCraftCosts);
        int version = f.Relic.State.Version;
        Assert.That(f.Workshop.TryCraftRelicSupport(out failure), Is.False);
        Assert.That(failure, Is.EqualTo(RelicOperationFailure.InvalidState));
        f.AssertBalances(after);
        Assert.That(f.Relic.State.Version, Is.EqualTo(version));
        Assert.That(f.Relic.State.SupportReady, Is.True);
    }

    [TestCase(RelicRoute.Repair)]
    [TestCase(RelicRoute.Dismantle)]
    public void ProximaDiscovery_AllowsBothRoutesToSupportTwoExistingLaterCampaigns(RelicRoute route)
    {
        using var f = new Fixture(false);
        Assert.That(f.Definition.Sector.Id, Is.EqualTo("ProximaB")); // Stable sector ID.
        Assert.That(f.TargetState.Occupied, Is.False);
        f.MakeOperational(route);
        foreach (string id in new[] { "TauCetiFoundry", "SiriusResourceBelt" })
        {
            SectorDefinition target = DataBase<SectorDefinition>.Find(id);
            SectorState state = f.Game.Sectors.GetState(target);
            Assert.That(f.Game.Sectors.TryUnlock(target, out SectorOperationFailure unlockFailure), Is.True, unlockFailure.ToString());
            Assert.That(state.Occupied, Is.False);
            if (route == RelicRoute.Repair)
            {
                Assert.That(f.Relic.CanBeginCommission(out _), Is.True);
                Assert.That(f.Relic.TryBeginCommission(out _), Is.True);
                Assert.That(f.Relic.Tick(f.Definition.Commission.DurationSeconds.ToDouble() * 2d), Is.True);
            }
            else Assert.That(f.Relic.TryCraftSupport(out _), Is.True);
            Assert.That(f.Game.Sectors.TryAdvanceCampaign(target, 0d, f.Game.State, f.Resources, out _), Is.True);
            Assert.That(f.Relic.CanAssignSupport(out _), Is.True);
            Assert.That(f.Relic.TryAssignSupport(out _), Is.True);
            Assert.That(f.Relic.GetCampaignSupplyMultiplier(target), Is.LessThan(ExpantaNum.One));
            Assert.That(f.Game.Sectors.TryAdvanceCampaign(target, 10d, f.Game.State, f.Resources, out _), Is.True);
            Assert.That(state.CampaignProgress, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(f.Game.Sectors.TryAdvanceCampaign(target, 100000d, f.Game.State, f.Resources, out _), Is.True);
            Assert.That(state.Occupied, Is.True);
            f.Relic.RefreshCampaignSupport();
        }
    }

    private static CampaignPayment RunCampaign(bool support)
    {
        using var f = new Fixture();
        if (support) f.PrepareAndAssign();
        else f.StartCampaign();
        SectorCampaignPreview preview = f.Game.Sectors.GetCampaignPreview(f.Target, f.Game.State, f.Resources);
        var before = f.Capture(f.Target.CampaignResourceRatesPerSecond);
        ExpantaNum food = f.Game.State.FoodAmount;
        const double seconds = 10d;
        Assert.That(f.Advance(seconds, out SectorOperationFailure failure), Is.True);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
        Assert.That(f.TargetState.Occupied, Is.False);
        var payment = new CampaignPayment
        {
            Food = food - f.Game.State.FoodAmount,
            Progress = f.TargetState.CampaignProgress,
            CombatRatio = f.TargetState.CampaignCombatRatio,
            Casualties = f.TargetState.CampaignCasualties
        };
        Near(payment.Food, preview.FoodCostPerSecond * seconds);
        foreach (var rate in preview.ResourceCostsPerSecond)
        {
            ExpantaNum paid = before[rate.First] - f.Resources.GetAmount(rate.First);
            Near(paid, rate.Second * seconds);
            payment.Materials.Add(rate.First.Id, paid);
        }
        return payment;
    }

    private sealed class CampaignPayment
    {
        public ExpantaNum Food, Progress, CombatRatio, Casualties;
        public readonly Dictionary<string, ExpantaNum> Materials = new();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root;
        public readonly GameManager Game;
        public readonly ResourceManager Resources;
        public readonly WorkshopManager Workshop;
        public RelicManager Relic => Game.Relic;
        public RelicDefinition Definition => Relic.Definition;
        public readonly SectorDefinition Target;
        public SectorState TargetState => Game.Sectors.GetState(Target);

        public Fixture(bool unlockTarget = true)
        {
            ProgressionModifierManager.Rebuild(null);
            root = new GameObject("RelicCampaignTests");
            Game = root.AddComponent<GameManager>();
            Resources = root.AddComponent<ResourceManager>();
            BuildingManager buildings = root.AddComponent<BuildingManager>();
            ResearchManager research = root.AddComponent<ResearchManager>();
            Workshop = root.AddComponent<WorkshopManager>();
            Game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum(1e8d), 0L);
            Game.State.SetSupplySatisfactionForEditor(ExpantaNum.One);
            Game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
            Game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
            Game.State.AdjustAttackPowerForEditor(new ExpantaNum(1e6d));
            Game.State.AdjustFleetPowerForEditor(new ExpantaNum(1e6d));
            Game.State.AdjustMilitaryManpowerForEditor(new ExpantaNum(1e8d));
            Game.State.AdjustDefensePowerForEditor(new ExpantaNum(1e6d));
            research.InitializeForEditor();
            var completed = new List<ResearchState>();
            foreach (var entry in research.States)
            {
                var paid = new Dictionary<Resource, ExpantaNum>();
                foreach (var cost in entry.Key.ResourceRequirements)
                    if (cost.First != null && cost.Second > ExpantaNum.Zero) paid[cost.First] = cost.Second;
                entry.Value.RestoreForEditor(entry.Value.BaseCost, true, true, paid);
                completed.Add(entry.Value);
            }
            ProgressionModifierManager.Rebuild(completed);
            foreach (var resource in DataBase<Resource>.All)
                Resources.SetAmount(resource, new ExpantaNum(1e8d));
            foreach (var building in Definition.RequiredBuildings)
                buildings.EnsureBuilding(building).SetAmountForEditor(ExpantaNum.One);
            buildings.EnsureBuilding(DataBase<Building>.Find("LaunchCenter")).SetAmountForEditor(ExpantaNum.One);
            Game.Sectors.InitializeDefinitions();
            Game.Sectors.GetState(Definition.Sector).SetUnlockedForEditor(true);
            Game.Sectors.GetState(Definition.Sector).SetOccupiedForEditor(true);
            Target = DataBase<SectorDefinition>.Find("TauCetiFoundry");
            TargetState.SetUnlockedForEditor(unlockTarget);
            Game.UltraProject.State.RestoreForEditor(new UltraProjectStateSaveData
            {
                ProjectId = UltraProjectState.ProjectId,
                SaveVersion = UltraProjectState.CurrentSaveVersion,
                Doctrine = UltraProjectDoctrine.Stable,
                Status = UltraProjectStatus.Ready,
                CurrentStage = UltraProjectStage.Stabilization,
                StageProgress = "0",
                CompletedStages = new List<UltraProjectStage> { UltraProjectStage.Prototype },
                StateVersion = 1
            });
        }

        public void MakeOperational(RelicRoute route)
        {
            Assert.That(Relic.TryInvestigate(out _), Is.True);
            Assert.That(Relic.Tick(Definition.Investigation.DurationSeconds.ToDouble() * 2d), Is.True);
            Assert.That(Relic.TryChooseRoute(route, out _), Is.True);
            var work = route == RelicRoute.Repair ? Definition.Repair : Definition.ReverseEngineering;
            Assert.That(Relic.Tick(work.DurationSeconds.ToDouble() * 2d), Is.True);
            Assert.That(Relic.State.Status, Is.EqualTo(RelicStatus.Operational));
        }

        public void StartCampaign() => Assert.That(Advance(0d, out _), Is.True);
        public void PrepareAndAssign()
        {
            MakeOperational(RelicRoute.Dismantle);
            Assert.That(Workshop.TryCraftRelicSupport(out _), Is.True);
            StartCampaign();
            Assert.That(Relic.TryAssignSupport(out _), Is.True);
        }
        public bool Advance(double seconds, out SectorOperationFailure failure) =>
            Game.Sectors.TryAdvanceCampaign(Target, seconds, Game.State, Resources, out failure);
        public Dictionary<Resource, ExpantaNum> Capture(IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
        {
            var balances = new Dictionary<Resource, ExpantaNum>();
            foreach (var cost in costs) balances[cost.First] = Resources.GetAmount(cost.First);
            return balances;
        }
        public void AssertBalances(Dictionary<Resource, ExpantaNum> balances)
        {
            foreach (var entry in balances) Near(Resources.GetAmount(entry.Key), entry.Value);
        }
        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(root);
            ProgressionModifierManager.Rebuild(null);
        }
    }

    private static void Near(ExpantaNum actual, ExpantaNum expected)
    {
        ExpantaNum tolerance = ExpantaNum.Max(new ExpantaNum(1e-6d), expected.Abs() * 1e-8d);
        Assert.That((actual - expected).Abs(), Is.LessThanOrEqualTo(tolerance));
    }
}
