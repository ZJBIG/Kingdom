using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class UltraProjectStateTests
{
    [Test]
    public void UltraProjectDefinitionUsesAuthoredStrongReferencesAndGiantUpkeep()
    {
        UltraProjectDefinition definition = DataBase<UltraProjectDefinition>.Find(
            "UltraCivilizationEngineering");
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.Stages, Has.Count.EqualTo(UltraProjectState.StageCount));
        for (int i = 0; i < definition.Stages.Count; i++)
        {
            UltraProjectStageDefinition stage = definition.Stages[i];
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.RequiredResearch, Is.Not.Null);
            Assert.That(stage.RequiredBuilding, Is.Not.Null);
            Assert.That(stage.OneTimeResourceCosts, Is.Not.Empty);
            Assert.That(stage.ContinuousResourceCosts, Is.Not.Empty);
            Assert.That(stage.BaseDurationSeconds, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(stage.FoodConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(stage.PowerConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(stage.LogisticsConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        }

        UltraProjectStageDefinition continuity = definition.Stages[2];
        Assert.That(continuity.AdditionalRequiredResearch, Has.Count.EqualTo(1));
        Assert.That(continuity.AdditionalRequiredResearch[0].Id,
            Is.EqualTo("AdaptiveFleetLogistics"));
    }

    [Test]
    public void StateMachineKeepsStageBoundariesAndCommitIdempotent()
    {
        UltraProjectState state = new UltraProjectState();
        state.InitializeNewForEditor(UltraProjectDoctrine.Stable);
        Assert.That(state.Status, Is.EqualTo(UltraProjectStatus.Ready));
        Assert.That(state.CurrentStage, Is.EqualTo(UltraProjectStage.Prototype));

        state.StartForEditor();
        state.AdvanceStageProgressForEditor(ExpantaNum.One);
        Assert.That(state.Status, Is.EqualTo(UltraProjectStatus.ReadyToCommit));
        Assert.That(state.CurrentStage, Is.EqualTo(UltraProjectStage.Prototype));
        Assert.That(state.CompletedStageCount, Is.EqualTo(1));
        Assert.That(state.LaunchFeePaid, Is.True);

        state.CommitForEditor();
        Assert.That(state.Status, Is.EqualTo(UltraProjectStatus.Ready));
        Assert.That(state.CurrentStage, Is.EqualTo(UltraProjectStage.Stabilization));
        Assert.That(state.LaunchFeePaid, Is.False);
        state.StartForEditor();
        state.AdvanceStageProgressForEditor(ExpantaNum.One);
        Assert.That(state.Status, Is.EqualTo(UltraProjectStatus.ReadyToCommit));
        state.CommitForEditor();
        state.StartForEditor();
        state.AdvanceStageProgressForEditor(ExpantaNum.One);
        Assert.That(state.Status, Is.EqualTo(UltraProjectStatus.ReadyToCommit));
        Assert.That(state.CurrentStage, Is.EqualTo(UltraProjectStage.Completed));
        Assert.That(state.CompletedStageCount, Is.EqualTo(UltraProjectState.StageCount));

        state.CommitForEditor();
        int version = state.Version;
        state.CommitForEditor();
        Assert.That(state.Status, Is.EqualTo(UltraProjectStatus.Committed));
        Assert.That(state.Version, Is.EqualTo(version));
    }

    [Test]
    public void AbandonClearsOnlyCurrentProgressAndDoesNotRefundStartupFee()
    {
        UltraProjectState state = new UltraProjectState(UltraProjectDoctrine.Stable);
        state.StartForEditor();
        state.AdvanceStageProgressForEditor(new ExpantaNum(0.4d));
        state.AbandonForEditor();

        Assert.That(state.Status, Is.EqualTo(UltraProjectStatus.Ready));
        Assert.That(state.CurrentStage, Is.EqualTo(UltraProjectStage.Prototype));
        Assert.That(state.StageProgress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.LaunchFeePaid, Is.True);
        Assert.That(state.CompletedStageCount, Is.EqualTo(0));

        UltraProjectState restored = new UltraProjectState();
        Assert.DoesNotThrow(() => restored.RestoreForEditor(state.CaptureSaveData()));
        Assert.That(restored.Status, Is.EqualTo(UltraProjectStatus.Ready));
        Assert.That(restored.LaunchFeePaid, Is.True);
        Assert.That(restored.StageProgress, Is.EqualTo(ExpantaNum.Zero));

        state.StartForEditor();
        Assert.That(state.Status, Is.EqualTo(UltraProjectStatus.Running));
        Assert.That(state.LaunchFeePaid, Is.True);
    }

    [Test]
    public void PauseReasonSurvivesSaveRestoreAndManualPauseIsDistinct()
    {
        UltraProjectState state = new UltraProjectState(UltraProjectDoctrine.Stable);
        state.StartForEditor();
        state.PauseForEditor();

        Assert.That(state.PauseReason, Is.EqualTo(UltraProjectPauseReason.Manual));
        UltraProjectStateSaveData save = state.CaptureSaveData();
        Assert.That(save.PauseReason, Is.EqualTo(UltraProjectPauseReason.Manual));

        UltraProjectState restored = new UltraProjectState();
        restored.RestoreForEditor(save);
        Assert.That(restored.Status, Is.EqualTo(UltraProjectStatus.Paused));
        Assert.That(restored.PauseReason, Is.EqualTo(UltraProjectPauseReason.Manual));
    }

    [Test]
    public void PausedSaveWithoutAReasonIsRejected()
    {
        UltraProjectState state = new UltraProjectState(UltraProjectDoctrine.Stable);
        state.StartForEditor();
        state.PauseForEditor();
        UltraProjectStateSaveData save = state.CaptureSaveData();
        save.PauseReason = UltraProjectPauseReason.None;

        UltraProjectState restored = new UltraProjectState();
        Assert.Throws<System.InvalidOperationException>(
            () => restored.RestoreForEditor(save));
    }

    [Test]
    public void SaveRestoreRejectsInvalidStateAndPreservesValidState()
    {
        UltraProjectState state = new UltraProjectState(UltraProjectDoctrine.Stable);
        state.StartForEditor();
        state.AdvanceStageProgressForEditor(ExpantaNum.One);
        state.CommitForEditor();
        state.ChangeDoctrineForEditor(UltraProjectDoctrine.Surge);
        state.StartForEditor();
        state.AdvanceStageProgressForEditor(new ExpantaNum(0.25d));
        UltraProjectStateSaveData save = state.CaptureSaveData();
        Assert.That(save.CompletedStages, Has.Count.GreaterThanOrEqualTo(1));

        UltraProjectState restored = new UltraProjectState();
        restored.RestoreForEditor(save);
        Assert.That(restored.Status, Is.EqualTo(UltraProjectStatus.Running));
        Assert.That(restored.Doctrine, Is.EqualTo(UltraProjectDoctrine.Surge));
        Assert.That(restored.CompletedStageCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(restored.StageProgress, Is.EqualTo(new ExpantaNum(0.25d)));

        save.StageProgress = "2";
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => restored.RestoreForEditor(save));
        Assert.That(restored.Status, Is.EqualTo(UltraProjectStatus.Running));
        Assert.That(restored.Doctrine, Is.EqualTo(UltraProjectDoctrine.Surge));
        Assert.That(restored.StageProgress, Is.EqualTo(new ExpantaNum(0.25d)));
    }

    [Test]
    public void SaveRestoreRejectsFinalReadyToCommitWithNonCompletedStage()
    {
        UltraProjectState state = new UltraProjectState(UltraProjectDoctrine.Stable);
        state.StartForEditor();
        state.AdvanceStageProgressForEditor(ExpantaNum.One);
        state.CommitForEditor();
        state.StartForEditor();
        state.AdvanceStageProgressForEditor(ExpantaNum.One);
        state.CommitForEditor();
        state.StartForEditor();
        state.AdvanceStageProgressForEditor(ExpantaNum.One);

        UltraProjectStateSaveData save = state.CaptureSaveData();
        save.CurrentStage = UltraProjectStage.Expansion;

        UltraProjectState restored = new UltraProjectState();
        Assert.Throws<System.InvalidOperationException>(
            () => restored.RestoreForEditor(save));
    }

    [Test]
    public void MissingOrMalformedUltraSaveDataIsRejected()
    {
        UltraProjectManager manager = new UltraProjectManager();
        Assert.Throws<System.ArgumentNullException>(
            () => manager.RestoreSaveDataForEditor(null));

        UltraProjectStateSaveData explicitDefault = new UltraProjectStateSaveData();
        Assert.Throws<System.InvalidOperationException>(
            () => manager.RestoreSaveDataForEditor(explicitDefault));
    }

    [Test]
    public void SaveRestoreRejectsAProjectDefinitionMismatch()
    {
        UltraProjectState state = new UltraProjectState(UltraProjectDoctrine.Stable);
        UltraProjectStateSaveData save = state.CaptureSaveData();
        save.ProjectId = "AnotherCivilizationEngineeringProject";

        UltraProjectState restored = new UltraProjectState();
        Assert.Throws<System.InvalidOperationException>(
            () => restored.RestoreForEditor(save));
        Assert.That(restored.Status, Is.EqualTo(UltraProjectStatus.Locked));
    }

    [Test]
    public void SaveRestoreRejectsUninitializedZeroStateVersion()
    {
        UltraProjectState state = new UltraProjectState(UltraProjectDoctrine.Stable);
        UltraProjectStateSaveData save = state.CaptureSaveData();
        save.StateVersion = 0;

        UltraProjectState restored = new UltraProjectState();
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => restored.RestoreForEditor(save));
        Assert.That(restored.Status, Is.EqualTo(UltraProjectStatus.Locked));
    }


    [Test]
    public void SaveRestoreAllowsAbandonedReadyStateWithPaidLaunchFee()
    {
        UltraProjectState source = new UltraProjectState(UltraProjectDoctrine.Stable);
        source.StartForEditor();
        source.AdvanceStageProgressForEditor(new ExpantaNum(0.4d));
        source.AbandonForEditor();
        UltraProjectStateSaveData save = source.CaptureSaveData();

        UltraProjectState restored = new UltraProjectState();
        Assert.DoesNotThrow(() => restored.RestoreForEditor(save));
        Assert.That(restored.Status, Is.EqualTo(UltraProjectStatus.Ready));
        Assert.That(restored.StageProgress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(restored.LaunchFeePaid, Is.True);
    }

    [Test]
    public void SaveParserDistinguishesMissingNullAndMalformedUltraSections()
    {
        const string requiredSections =
            "\"Version\":9,\"General\":{\"TechLevel\":0},\"Resources\":{}," +
            "\"Buildings\":{},\"Researches\":{},\"Workshop\":{},\"Sectors\":{}," +
            "\"Tutorial\":{\"CompletedStepIds\":[]},\"Story\":{\"CompletedChapterIds\":[]}";
        string missing = "{" + requiredSections + "}";

        SaveManager.KingdomSaveData parsedMissing =
            SaveManager.ParseSaveDataForEditor(missing);
        Assert.That(parsedMissing.UltraProject, Is.Null);

        Assert.Throws<System.IO.InvalidDataException>(() =>
            SaveManager.ParseSaveDataForEditor(
                "{" + requiredSections + ",\"UltraProject\":null}"));
        Assert.Throws<System.InvalidOperationException>(() =>
            SaveManager.ParseSaveDataForEditor(
                "{" + requiredSections + ",\"UltraProject\":{}}"));
        Assert.Throws<System.IO.InvalidDataException>(() =>
            SaveManager.ParseSaveDataForEditor(
                "{" + requiredSections + ",\"Ul\\u0074raProject\":null}"));
    }

    [Test]
    public void SaveParserRejectsUnlockedUltraProjectBeforeUltraTechLevel()
    {
        const string requiredSections =
            "\"Version\":9,\"General\":{\"TechLevel\":3},\"Resources\":{}," +
            "\"Buildings\":{},\"Researches\":{},\"Workshop\":{},\"Sectors\":{}," +
            "\"Tutorial\":{\"CompletedStepIds\":[]},\"Story\":{\"CompletedChapterIds\":[]}";
        const string ultraProject =
            "\"ProjectId\":\"UltraCivilizationEngineering\",\"SaveVersion\":1," +
            "\"Doctrine\":1,\"Status\":1,\"CurrentStage\":1,\"StageProgress\":\"0\"," +
            "\"CompletedStages\":[],\"LaunchFeePaid\":false,\"PauseReason\":0," +
            "\"StateVersion\":1";

        Assert.Throws<System.IO.InvalidDataException>(() =>
            SaveManager.ParseSaveDataForEditor(
                "{" + requiredSections + ",\"UltraProject\":{" + ultraProject + "}}"));
    }

    [Test]
    public void ExpeditionSaveDataIsRejectedUntilCivilizationEngineeringIsCommitted()
    {
        UltraProjectManager manager = new UltraProjectManager();
        UltraProjectStateSaveData save = CreateCompletedSave(UltraProjectDoctrine.Expedition);
        save.Status = UltraProjectStatus.Ready;
        save.CurrentStage = UltraProjectStage.Stabilization;
        save.StageProgress = "0";
        save.CompletedStages = new List<UltraProjectStage>
        {
            UltraProjectStage.Prototype
        };
        save.LaunchFeePaid = false;

        Assert.Throws<System.InvalidOperationException>(
            () => manager.RestoreSaveDataForEditor(save));
    }

    [Test]
    public void SurgeSaveDataIsRejectedBeforeFirstStageIsCompleted()
    {
        UltraProjectState state = new UltraProjectState(UltraProjectDoctrine.Stable);
        UltraProjectStateSaveData save = state.CaptureSaveData();
        save.Doctrine = UltraProjectDoctrine.Surge;

        UltraProjectState restored = new UltraProjectState();
        Assert.Throws<System.InvalidOperationException>(
            () => restored.RestoreForEditor(save));
        Assert.That(restored.Status, Is.EqualTo(UltraProjectStatus.Locked));
        Assert.That(restored.Doctrine, Is.EqualTo(UltraProjectDoctrine.None));

        UltraProjectManager manager = new UltraProjectManager();
        Assert.Throws<System.InvalidOperationException>(
            () => manager.RestoreSaveDataForEditor(save));
        Assert.That(manager.State.Status, Is.EqualTo(UltraProjectStatus.Locked));

        state.StartForEditor();
        state.AdvanceStageProgressForEditor(ExpantaNum.One);
        UltraProjectStateSaveData unlocked = state.CaptureSaveData();
        unlocked.Doctrine = UltraProjectDoctrine.Surge;
        Assert.DoesNotThrow(() => restored.RestoreForEditor(unlocked));
        Assert.That(restored.Doctrine, Is.EqualTo(UltraProjectDoctrine.Surge));
        Assert.That(restored.CompletedStageCount, Is.EqualTo(1));
    }

    [Test]
    public void ExpeditionPostureIsReachableAfterCivilizationEngineeringCommit()
    {
        GameManager gameManager;
        GameObject gameObject = null;
        if (!GameManager.TryGetInstance(out gameManager))
        {
            gameObject = new GameObject("UltraProject-Doctrine-GameManager");
            gameManager = gameObject.AddComponent<GameManager>();
        }

        try
        {
            gameManager.State.AdvanceTechLevelForEditor(TechLevel.Ultra);
            UltraProjectManager manager = new UltraProjectManager();
            manager.RestoreSaveDataForEditor(CreateCompletedSave(UltraProjectDoctrine.Stable));

            int version = manager.State.Version;
            Assert.That(
                manager.TrySetOperationalDoctrine(
                    UltraProjectDoctrine.Expedition,
                    out UltraProjectOperationFailure failure),
                Is.True);
            Assert.That(failure, Is.EqualTo(UltraProjectOperationFailure.None));
            Assert.That(manager.State.Doctrine, Is.EqualTo(UltraProjectDoctrine.Expedition));
            Assert.That(manager.State.Version, Is.GreaterThan(version));
        }
        finally
        {
            if (gameObject != null)
                Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ManagerFinalCommitIsIdempotent()
    {
        UltraProjectState completed = new UltraProjectState(UltraProjectDoctrine.Stable);
        completed.StartForEditor();
        completed.AdvanceStageProgressForEditor(ExpantaNum.One);
        completed.CommitForEditor();
        completed.StartForEditor();
        completed.AdvanceStageProgressForEditor(ExpantaNum.One);
        completed.CommitForEditor();
        completed.StartForEditor();
        completed.AdvanceStageProgressForEditor(ExpantaNum.One);
        completed.CommitForEditor();

        UltraProjectManager manager = new UltraProjectManager();
        manager.RestoreSaveDataForEditor(completed.CaptureSaveData());
        int version = manager.State.Version;

        Assert.That(manager.TryCommitCompletedStage(out UltraProjectOperationFailure failure), Is.True);
        Assert.That(failure, Is.EqualTo(UltraProjectOperationFailure.None));
        Assert.That(manager.State.Status, Is.EqualTo(UltraProjectStatus.Committed));
        Assert.That(manager.State.Version, Is.EqualTo(version));
    }

    private static UltraProjectStateSaveData CreateCompletedSave(
        UltraProjectDoctrine doctrine)
    {
        return new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            SaveVersion = UltraProjectState.CurrentSaveVersion,
            Doctrine = doctrine,
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
        };
    }
}
