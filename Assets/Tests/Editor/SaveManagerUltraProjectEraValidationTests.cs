using System.IO;
using NUnit.Framework;

public sealed class SaveManagerUltraProjectEraValidationTests
{
    [Test]
    public void RunningUltraProjectIsRejectedBeforeUltraTechLevel()
    {
        string json = BuildSaveJson(
            TechLevel.Spacer,
            UltraProjectStatus.Running);

        Assert.Throws<InvalidDataException>(
            () => SaveManager.ParseSaveDataForEditor(json));
    }

    [Test]
    public void CommittedUltraProjectIsRejectedBeforeUltraTechLevel()
    {
        string json = BuildSaveJson(
            TechLevel.Spacer,
            UltraProjectStatus.Committed);

        Assert.Throws<InvalidDataException>(
            () => SaveManager.ParseSaveDataForEditor(json));
    }

    [TestCase(UltraProjectStatus.Paused)]
    [TestCase(UltraProjectStatus.ReadyToCommit)]
    public void OtherUnlockedUltraProjectStatesAreRejectedBeforeUltraTechLevel(
        UltraProjectStatus status)
    {
        Assert.Throws<InvalidDataException>(
            () => SaveManager.ParseSaveDataForEditor(
                BuildSaveJson(TechLevel.Spacer, status)));
    }

    [Test]
    public void RunningAndCommittedUltraProjectsAreAcceptedAtUltraTechLevel()
    {
        SaveManager.KingdomSaveData running =
            SaveManager.ParseSaveDataForEditor(
                BuildSaveJson(TechLevel.Ultra, UltraProjectStatus.Running));
        SaveManager.KingdomSaveData committed =
            SaveManager.ParseSaveDataForEditor(
                BuildSaveJson(TechLevel.Ultra, UltraProjectStatus.Committed));

        Assert.That(running.UltraProject.Status, Is.EqualTo(UltraProjectStatus.Running));
        Assert.That(committed.UltraProject.Status, Is.EqualTo(UltraProjectStatus.Committed));
    }

    private static string BuildSaveJson(
        TechLevel techLevel,
        UltraProjectStatus status)
    {
        bool committed = status == UltraProjectStatus.Committed;
        bool readyToCommit = status == UltraProjectStatus.ReadyToCommit;
        bool paused = status == UltraProjectStatus.Paused;
        string currentStage = committed ? "4" : "1";
        string progress = committed || readyToCommit ? "1" : paused ? "0.25" : "0.25";
        string completedStages = committed ? "[1,2,3]" : readyToCommit ? "[1]" : "[]";
        string launchFeePaid = status == UltraProjectStatus.Locked ? "false" : "true";
        string pauseReason = paused
            ? ((int)UltraProjectPauseReason.InsufficientSupply).ToString()
            : "0";
        int stateVersion = committed ? 10 : readyToCommit ? 5 : paused ? 3 : 2;

        return "{" +
            "\"Version\":9," +
            "\"General\":{\"TechLevel\":" + (int)techLevel + "}," +
            "\"Resources\":{}," +
            "\"Buildings\":{}," +
            "\"Researches\":{}," +
            "\"Workshop\":{}," +
            "\"Sectors\":{}," +
            "\"UltraProject\":{" +
                "\"ProjectId\":\"UltraCivilizationEngineering\"," +
                "\"SaveVersion\":1," +
                "\"Doctrine\":1," +
                "\"Status\":" + (int)status + "," +
                "\"CurrentStage\":" + currentStage + "," +
                "\"StageProgress\":\"" + progress + "\"," +
                "\"CompletedStages\":" + completedStages + "," +
                "\"LaunchFeePaid\":" + launchFeePaid + "," +
                "\"PauseReason\":" + pauseReason + "," +
                "\"StateVersion\":" + stateVersion +
            "}," +
            "\"Tutorial\":{\"CompletedStepIds\":[]}," +
            "\"Story\":{\"CompletedChapterIds\":[]}" +
            "}";
    }
}
