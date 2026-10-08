using System;
using NUnit.Framework;
using UnityEngine;

public sealed class RelicStateTests
{
    [TestCase(RelicRoute.Repair)]
    [TestCase(RelicRoute.Dismantle)]
    public void Routes_ArePermanentAndBothReachOperational(RelicRoute route)
    {
        RelicState state = Investigated();
        state.ChooseRouteForEditor(route);
        Assert.Throws<InvalidOperationException>(() => state.ChooseRouteForEditor(
            route == RelicRoute.Repair ? RelicRoute.Dismantle : RelicRoute.Repair));
        state.AdvanceForEditor(new ExpantaNum(2d));
        Assert.That(state.Status, Is.EqualTo(RelicStatus.Operational));
        Assert.That(state.Route, Is.EqualTo(route));
        Assert.That(state.Progress, Is.LessThan(new ExpantaNum(1e-8d)));
        Assert.Throws<InvalidOperationException>(() => state.AdvanceForEditor(ExpantaNum.One));
        Assert.That(state.SupportReady, Is.False);
    }

    [Test]
    public void SuspendedInvestigation_RoundTripsWithoutLosingProgress()
    {
        RelicState original = new();
        original.StartInvestigationForEditor();
        original.AdvanceForEditor(new ExpantaNum(.35d));
        original.SuspendForEditor(RelicPauseReason.Manual);
        RelicState restored = RoundTrip(original);
        Assert.That(restored.Suspended, Is.True);
        Assert.That(restored.PauseReason, Is.EqualTo(RelicPauseReason.Manual));
        Assert.That(restored.Progress.ToDouble(), Is.EqualTo(original.Progress.ToDouble()).Within(1e-8d));
        Assert.Throws<InvalidOperationException>(() => restored.AdvanceForEditor(ExpantaNum.One));
        restored.ResumeForEditor();
        restored.AdvanceForEditor(ExpantaNum.One);
        Assert.That(restored.Status, Is.EqualTo(RelicStatus.AwaitingChoice));
    }

    [Test]
    public void RepairCommission_GrantsOneSupportAndCannotRepeatCompletion()
    {
        RelicState state = Operational(RelicRoute.Repair);
        state.BeginCommissionForEditor();
        state.AdvanceForEditor(new ExpantaNum(.4d));
        state.SuspendForEditor(RelicPauseReason.InsufficientSupply);
        state = RoundTrip(state);
        Assert.That(state.CommissionActive, Is.True);
        state.ResumeForEditor();
        state.AdvanceForEditor(ExpantaNum.One);
        Assert.That(state.CommissionActive, Is.False);
        Assert.That(state.SupportReady, Is.True);
        // CompletedCommissions and Version are discrete counters, compared exactly.
        Assert.That(state.CompletedCommissions, Is.EqualTo(1));
        int version = state.Version;
        Assert.Throws<InvalidOperationException>(() => state.AdvanceForEditor(ExpantaNum.One));
        Assert.Throws<InvalidOperationException>(() => state.BeginCommissionForEditor());
        Assert.That(state.Version, Is.EqualTo(version));
        Assert.That(state.CompletedCommissions, Is.EqualTo(1));
        state.ConsumeSupportForEditor("TauCetiFoundry");
        state = RoundTrip(state);
        Assert.That(state.SupportReady, Is.False);
        Assert.That(state.SupportedSectorId, Is.EqualTo("TauCetiFoundry"));
        Assert.Throws<InvalidOperationException>(() => state.ConsumeSupportForEditor("ProximaB"));
        Assert.Throws<InvalidOperationException>(() => state.BeginCommissionForEditor());
        state.ClearSupportedCampaignForEditor();
        state.BeginCommissionForEditor();
        Assert.That(state.CommissionActive, Is.True);
    }

    [Test]
    public void DismantleBlueprint_PreparesOnlyOneSupportAndNeverRunsRepairCommission()
    {
        RelicState state = Operational(RelicRoute.Dismantle);
        Assert.Throws<InvalidOperationException>(() => state.BeginCommissionForEditor());
        state.PrepareSupportForEditor();
        Assert.Throws<InvalidOperationException>(() => state.PrepareSupportForEditor());
        Assert.That(state.CompletedCommissions, Is.EqualTo(0));
        Assert.Throws<ArgumentException>(() => state.ConsumeSupportForEditor(" "));
        Assert.That(state.SupportReady, Is.True);
        state.ConsumeSupportForEditor("ProximaB");
        Assert.Throws<InvalidOperationException>(() => state.PrepareSupportForEditor());
        state.ClearSupportedCampaignForEditor();
        state.PrepareSupportForEditor();
        Assert.That(state.SupportReady, Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(11)]
    [TestCase(12)]
    [TestCase(13)]
    public void InvalidSave_IsRejectedBeforeChangingState(int invalidCase)
    {
        RelicState target = Operational(RelicRoute.Repair);
        RelicStateSaveData data = target.CaptureSaveDataForEditor();
        switch (invalidCase)
        {
            case 0: data.SaveVersion++; break;
            case 1: data.RelicId = "OtherRelic"; break;
            case 2: data.Progress = "NaN"; break;
            case 3: data.Progress = "1"; break;
            case 4: data.Status = (RelicStatus)999; break;
            case 5: data.Route = RelicRoute.None; break;
            case 6: data.PauseReason = RelicPauseReason.Manual; break;
            case 7: data.SupportReady = true; data.SupportedSectorId = "ProximaB"; break;
            case 8: data.CompletedCommissions = -1; break;
            case 9: data.CommissionActive = true; data.Route = RelicRoute.Dismantle; break;
            case 10: data.Progress = ".2"; break;
            case 11: data.StateVersion = 0; break;
            case 12: data.Status = RelicStatus.Repairing; data.Route = RelicRoute.Dismantle; break;
            case 13: data.Suspended = true; data.PauseReason = RelicPauseReason.InsufficientSupply; break;
        }
        int previousVersion = target.Version;
        Assert.That(() => target.RestoreForEditor(data), Throws.Exception);
        Assert.That(target.Status, Is.EqualTo(RelicStatus.Operational));
        Assert.That(target.Route, Is.EqualTo(RelicRoute.Repair));
        Assert.That(target.Version, Is.EqualTo(previousVersion));
    }

    [Test]
    public void InvalidTransitions_LeaveDiscoveryIntactAndManualSealCanResume()
    {
        RelicState state = new();
        Assert.Throws<InvalidOperationException>(() => state.ChooseRouteForEditor(RelicRoute.Repair));
        Assert.Throws<InvalidOperationException>(() => state.AdvanceForEditor(ExpantaNum.One));
        Assert.Throws<InvalidOperationException>(() => state.SuspendForEditor(RelicPauseReason.InsufficientSupply));
        Assert.Throws<InvalidOperationException>(() => state.PrepareSupportForEditor());
        state.SuspendForEditor(RelicPauseReason.Manual);
        Assert.Throws<InvalidOperationException>(() => state.StartInvestigationForEditor());
        state.ResumeForEditor();
        state.StartInvestigationForEditor();
        Assert.Throws<ArgumentOutOfRangeException>(() => state.AdvanceForEditor(new ExpantaNum(-.1d)));
        Assert.That(state.Status, Is.EqualTo(RelicStatus.Investigating));
    }

    private static RelicState Investigated()
    {
        RelicState state = new();
        state.StartInvestigationForEditor();
        state.AdvanceForEditor(ExpantaNum.One);
        return state;
    }

    private static RelicState Operational(RelicRoute route)
    {
        RelicState state = Investigated();
        state.ChooseRouteForEditor(route);
        state.AdvanceForEditor(ExpantaNum.One);
        return state;
    }

    private static RelicState RoundTrip(RelicState original)
    {
        RelicState restored = new();
        restored.RestoreForEditor(JsonUtility.FromJson<RelicStateSaveData>(
            JsonUtility.ToJson(original.CaptureSaveDataForEditor())));
        return restored;
    }
}
