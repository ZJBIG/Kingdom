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
        Building building = Resources.Load<Building>("Datas/Building/Industrial/OilDerrick");
        Assert.That(building, Is.Not.Null);
        var state = new BuildingState(building);
        state.SetAmountForEditor(new ExpantaNum(2));
        state.SetEfficiencyForEditor(new ExpantaNum("0.5"));
        WorkshopUpgrade purchased = Upgrade(WorkshopEffectType.BuildingProductionMultiplier, "1.5", building);
        var purchasedState = new WorkshopUpgradeState(purchased);
        purchasedState.SetPurchasedForEditor(true);
        WorkshopUpgrade candidate = Upgrade(WorkshopEffectType.BuildingProductionMultiplier, "1.25", building);
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
        Building building = Resources.Load<Building>("Datas/Building/Industrial/OilDerrick");
        var state = new BuildingState(building);
        state.SetAmountForEditor(new ExpantaNum("1e100"));
        WorkshopUpgrade candidate = Upgrade(WorkshopEffectType.ResourceProductionMultiplier, "1.5",
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
        WorkshopUpgrade candidate = Upgrade(WorkshopEffectType.GlobalResearchMultiplier, "2");
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

    private WorkshopUpgrade Upgrade(WorkshopEffectType type, string value,
        Building building = null, Resource resource = null)
    {
        WorkshopUpgrade upgrade = ScriptableObject.CreateInstance<WorkshopUpgrade>();
        created.Add(upgrade);
        upgrade.SetIdForEditor("preview-" + created.Count);
        upgrade.ConfigureForEditor(null, null, null, new List<WorkshopEffectDefinition>
        {
            new() { Type = type, Value = value, Building = building, Resource = resource }
        });
        return upgrade;
    }
}
