using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class RelicSaveTests
{
    private static string SaveJson(string relic, TechLevel era = TechLevel.Ultra)
    {
        return "{\"Version\":" + SaveFormat.CurrentVersion + ",\"General\":{\"TechLevel\":" + (int)era +
            "},\"Resources\":{\"Resources\":[]},\"Buildings\":{\"Buildings\":[]}," +
            "\"Researches\":{\"States\":[],\"QueuedResearchIds\":[]}," +
            "\"Workshop\":{\"PurchasedUpgradeIds\":[]}," +
            "\"Sectors\":{\"States\":[]},\"Tutorial\":{\"CompletedStepIds\":[]}," +
            "\"Story\":{\"CompletedChapterIds\":[]}," +
            "\"UltraProject\":" + JsonUtility.ToJson(new UltraProjectState().CaptureSaveData()) +
            (relic == null ? "" : ",\"Relic\":" + relic) + "}";
    }

    [Test]
    public void MissingRelicSectionIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => SaveManager.ParseSaveDataForEditor(SaveJson(null)));
    }

    [TestCase("null")]
    [TestCase("{}")]
    public void PresentInvalidRelicSectionIsRejected(string section)
    {
        Assert.Catch<Exception>(() => SaveManager.ParseSaveDataForEditor(SaveJson(section)));
    }

    [Test]
    public void FreshRelicRoundTripsAtEarlierEra()
    {
        RelicState state = new RelicState();
        string section = JsonUtility.ToJson(state.CaptureSaveDataForEditor());
        SaveManager.KingdomSaveData parsed = SaveManager.ParseSaveDataForEditor(
            SaveJson(section, TechLevel.Animal));
        Assert.That(parsed.Relic.Status, Is.EqualTo(RelicStatus.Discovered));
        Assert.That(parsed.Version, Is.EqualTo(SaveFormat.CurrentVersion)); // protocol version
    }

    [Test]
    public void UnlockedRelicIsRejectedBeforeUltra()
    {
        RelicState state = new RelicState();
        state.StartInvestigationForEditor();
        string section = JsonUtility.ToJson(state.CaptureSaveDataForEditor());
        Assert.Throws<InvalidDataException>(() => SaveManager.ParseSaveDataForEditor(
            SaveJson(section, TechLevel.Spacer)));
    }
}
