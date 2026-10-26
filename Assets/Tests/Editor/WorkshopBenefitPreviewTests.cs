using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class WorkshopBenefitPreviewTests
{
    private readonly List<Object> created = new();

    [TearDown]
    public void TearDown()
    {
        ProgressionModifierManager.Rebuild(null);
        for (int i = created.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void BuildingMultiplierPreview_UsesAdditiveStackAndCurrentEfficiencyForOutputAndInputs()
    {
        BuildingDef building = Resources.Load<BuildingDef>("Datas/Building/Industrial/CokeOven");
        Assert.That(building, Is.Not.Null);
        var state = new BuildingState(building);
        state.SetAmountForEditor(new ExpantaNum(2));
        state.SetEfficiencyForEditor(new ExpantaNum("0.5"));
        WorkshopUpgradeDef purchased = Upgrade(WorkshopEffectType.BuildingProductionMultiplier, "1.5", building);
        var purchasedState = new WorkshopUpgradeState(purchased);
        purchasedState.SetPurchasedForEditor(true);
        WorkshopUpgradeDef candidate = Upgrade(WorkshopEffectType.BuildingProductionMultiplier, "1.25", building);
        var upgrades = new[] { purchasedState };
        ProgressionModifierManager.Rebuild(null, upgrades);
        ProgressionModifierState current = ProgressionModifierManager.Current;
        ProgressionModifierState preview = ProgressionModifierManager.BuildPreview(null, upgrades, candidate);
        IReadOnlyList<WorkshopBenefitRatePreview> rates = WorkshopManager.CalculateBenefitPreview(
            new[] { state }, current, preview);

        foreach (WorkshopBenefitRatePreview rate in rates)
        {
            // 1 + (.5 + .25) is the live modifier stack; it is not 1.5 * 1.25.
            Assert.That((rate.After / rate.Before).ToDouble(), Is.EqualTo(1.75 / 1.5).Within(1e-6));
            Assert.That(rate.Change, Is.GreaterThan(ExpantaNum.Zero));
            ExpantaNum baseRate = rate.Kind == WorkshopBenefitRateKind.ResourceProduction
                ? building.ResourceGenerationRates[0].Second
                : building.ResourceConsumptionRates[0].Second;
            Assert.That((rate.Before / baseRate).ToDouble(), Is.EqualTo(1.5).Within(1e-6),
                "Two buildings at half efficiency contribute one building, multiplied by the existing stack.");
        }
        Assert.That(rates, Has.Some.Matches<WorkshopBenefitRatePreview>(x =>
            x.Kind == WorkshopBenefitRateKind.ResourceConsumption));
        Assert.That(ProgressionModifierManager.Current, Is.SameAs(current));
        Assert.That(purchasedState.Purchased, Is.True);
        Assert.That(state.Amount.ToDouble(), Is.EqualTo(2).Within(1e-6));
        Assert.That(state.Efficiency.ToDouble(), Is.EqualTo(.5).Within(1e-6));
        IReadOnlyList<WorkshopBenefitRatePreview> rewarded = WorkshopManager.CalculateBenefitPreview(
            new[] { state }, current, preview, new ExpantaNum("1.3"));
        for (int i = 0; i < rates.Count; i++)
            Assert.That((rewarded[i].Change / rates[i].Change).ToDouble(),
                Is.EqualTo(rates[i].Kind == WorkshopBenefitRateKind.ResourceProduction ? 1.3 : 1).Within(1e-6),
                "Happiness increases settled production, never material demand.");
    }

    [Test]
    public void ResourceOnlyPreview_IncreasesOutputWithoutAdditionalInputs_AndZeroEfficiencyHasNoBenefit()
    {
        BuildingDef building = Resources.Load<BuildingDef>("Datas/Building/Industrial/OilDerrick");
        var state = new BuildingState(building);
        state.SetAmountForEditor(new ExpantaNum("1e100"));
        WorkshopUpgradeDef candidate = Upgrade(WorkshopEffectType.ResourceProductionMultiplier, "1.5",
            resource: building.ResourceGenerationRates[0].First);
        ProgressionModifierState current = ProgressionModifierManager.BuildPreview(null);
        ProgressionModifierState preview = ProgressionModifierManager.BuildPreview(null, null, candidate);
        IReadOnlyList<WorkshopBenefitRatePreview> rates = WorkshopManager.CalculateBenefitPreview(
            new[] { state }, current, preview);
        Assert.That(rates.Count, Is.EqualTo(1), "Exactly one resource output channel changes.");
        Assert.That(rates[0].Kind, Is.EqualTo(WorkshopBenefitRateKind.ResourceProduction));
        Assert.That(rates[0].After, Is.GreaterThan(rates[0].Before));
        Assert.That((rates[0].After / rates[0].Before).ToDouble(), Is.EqualTo(1.5).Within(1e-6));
        state.SetEfficiencyForEditor(ExpantaNum.Zero);
        Assert.That(WorkshopManager.CalculateBenefitPreview(new[] { state }, current, preview), Is.Empty);
        Assert.That(WorkshopManager.CalculateBenefitPreview(new BuildingState[0], current, preview), Is.Empty);
    }

    [Test]
    public void PurchasedCandidateIsNotAppliedTwice_AndResearchPreviewIncludesBaseSupply()
    {
        WorkshopUpgradeDef candidate = Upgrade(WorkshopEffectType.GlobalResearchMultiplier, "2");
        ProgressionModifierState current = ProgressionModifierManager.BuildPreview(null);
        ProgressionModifierState preview = ProgressionModifierManager.BuildPreview(null, null, candidate);
        IReadOnlyList<WorkshopBenefitRatePreview> rates = WorkshopManager.CalculateBenefitPreview(null, current, preview);
        Assert.That(rates.Count, Is.EqualTo(1), "Only the base research supply changes without buildings.");
        Assert.That(rates[0].Before.ToDouble(), Is.EqualTo(ResearchManager.BaseResearchPower.ToDouble()).Within(1e-6));
        Assert.That(rates[0].After.ToDouble(), Is.EqualTo((ResearchManager.BaseResearchPower * new ExpantaNum(2)).ToDouble()).Within(1e-6));
        var state = new WorkshopUpgradeState(candidate);
        state.SetPurchasedForEditor(true);
        var upgrades = new[] { state };
        current = ProgressionModifierManager.BuildPreview(null, upgrades);
        preview = ProgressionModifierManager.BuildPreview(null, upgrades, candidate);
        Assert.That(WorkshopManager.CalculateBenefitPreview(null, current, preview), Is.Empty);
    }

    [TestCase(1.15)]
    [TestCase(.8)]
    public void EventPreview_ScalesResourceAndBuildingFoodButNotInputsOrBaseFood(double eventMultiplier)
    {
        BuildingDef oven = Resources.Load<BuildingDef>("Datas/Building/Industrial/CokeOven");
        BuildingDef farm = Resources.Load<BuildingDef>("Datas/Building/Animal/Farm");
        var ovenState = new BuildingState(oven);
        var farmState = new BuildingState(farm);
        ovenState.SetAmountForEditor(ExpantaNum.One);
        farmState.SetAmountForEditor(ExpantaNum.One);
        WorkshopUpgradeDef candidate = Upgrade(WorkshopEffectType.GlobalBuildingProductionMultiplier, "1.5");
        ProgressionModifierState before = ProgressionModifierManager.BuildPreview(null);
        ProgressionModifierState after = ProgressionModifierManager.BuildPreview(null, null, candidate);
        var buildings = new[] { ovenState, farmState };
        IReadOnlyList<WorkshopBenefitRatePreview> baseline = WorkshopManager.CalculateBenefitPreview(
            buildings, before, after);
        IReadOnlyList<WorkshopBenefitRatePreview> actual = WorkshopManager.CalculateBenefitPreview(
            buildings, before, after, ExpantaNum.One, new ExpantaNum(eventMultiplier));
        Assert.That(baseline, Has.Some.Matches<WorkshopBenefitRatePreview>(x =>
            x.Building == farm && x.Kind == WorkshopBenefitRateKind.FoodProduction));
        Assert.That(baseline, Has.Some.Matches<WorkshopBenefitRatePreview>(x =>
            x.Kind == WorkshopBenefitRateKind.ResourceConsumption));
        for (int i = 0; i < baseline.Count; i++)
        {
            double expected = baseline[i].Kind == WorkshopBenefitRateKind.ResourceProduction ||
                baseline[i].Kind == WorkshopBenefitRateKind.FoodProduction ? eventMultiplier : 1d;
            Assert.That((actual[i].Before / baseline[i].Before).ToDouble(), Is.EqualTo(expected).Within(1e-6));
            Assert.That((actual[i].Change / baseline[i].Change).ToDouble(), Is.EqualTo(expected).Within(1e-6));
        }
        candidate = Upgrade(WorkshopEffectType.GlobalFoodProductionMultiplier, "1.5");
        after = ProgressionModifierManager.BuildPreview(null, null, candidate);
        baseline = WorkshopManager.CalculateBenefitPreview(null, before, after);
        actual = WorkshopManager.CalculateBenefitPreview(null, before, after,
            ExpantaNum.One, new ExpantaNum(eventMultiplier));
        Assert.That(actual[0].Change.ToDouble(), Is.EqualTo(baseline[0].Change.ToDouble()).Within(1e-6),
            "World events affect building production, not the base food supply.");
    }

    [TestCase(WorkshopEffectType.GlobalResearchMultiplier, WorkshopBenefitRateKind.ResearchPower)]
    [TestCase(WorkshopEffectType.PowerMultiplier, WorkshopBenefitRateKind.PowerProduction)]
    [TestCase(WorkshopEffectType.GlobalLogisticsMultiplier, WorkshopBenefitRateKind.LogisticsProduction)]
    public void Preview_UsesEffectiveResearchAndFlowRewards(
        WorkshopEffectType effect, WorkshopBenefitRateKind channel)
    {
        BuildingDef building = ScriptableObject.CreateInstance<BuildingDef>();
        created.Add(building);
        building.SetIdForEditor("effective-preview-building");
        building.SetResearchPowerForEditor(new ExpantaNum(3d));
        building.SetPowerContributionForEditor(new ExpantaNum(4d));
        building.SetLogisticsContributionForEditor(new ExpantaNum(5d));
        var state = new BuildingState(building);
        state.SetAmountForEditor(new ExpantaNum(2d));
        state.SetEfficiencyForEditor(new ExpantaNum(.5d));
        WorkshopUpgradeDef candidate = Upgrade(effect, "1.5");
        ProgressionModifierState before = ProgressionModifierManager.BuildPreview(null);
        ProgressionModifierState after = ProgressionModifierManager.BuildPreview(null, null, candidate);
        var buildings = new[] { state };
        IReadOnlyList<WorkshopBenefitRatePreview> baseline = WorkshopManager.CalculateBenefitPreview(
            buildings, before, after);
        IReadOnlyList<WorkshopBenefitRatePreview> effective = WorkshopManager.CalculateBenefitPreview(
            buildings, before, after, new ExpantaNum(1.2d), ExpantaNum.One, new ExpantaNum(1.8d));
        Assert.That(effective.Count, Is.GreaterThan(0));
        for (int i = 0; i < baseline.Count; i++)
        {
            Assert.That(effective[i].Kind, Is.EqualTo(channel));
            double multiplier = channel == WorkshopBenefitRateKind.ResearchPower ? 1.8d : 1.2d;
            Assert.That((effective[i].Before / baseline[i].Before).ToDouble(),
                Is.EqualTo(multiplier).Within(1e-6));
            Assert.That((effective[i].Change / baseline[i].Change).ToDouble(),
                Is.EqualTo(multiplier).Within(1e-6));
        }
    }

    private WorkshopUpgradeDef Upgrade(WorkshopEffectType type, string value,
        BuildingDef building = null, ResourceDef resource = null)
    {
        WorkshopUpgradeDef upgrade = ScriptableObject.CreateInstance<WorkshopUpgradeDef>();
        created.Add(upgrade);
        upgrade.SetIdForEditor("preview-" + created.Count);
        upgrade.ConfigureForEditor(null, null, null, new List<WorkshopEffectDefinition>
        {
            new() { Type = type, Value = value, Building = building, Resource = resource }
        });
        return upgrade;
    }
}
