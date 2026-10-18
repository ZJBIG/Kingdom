using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SaveArchivePressureTests
{
    [TestCase("General")]
    [TestCase("Resources")]
    [TestCase("Buildings")]
    [TestCase("Researches")]
    [TestCase("Workshop")]
    [TestCase("Sectors")]
    [TestCase("UltraProject")]
    [TestCase("Relic")]
    [TestCase("Tutorial")]
    [TestCase("Story")]
    public void CurrentArchiveRequiresEverySection(string section)
    {
        string json = JsonUtility.ToJson(CreateBaseSave(TechLevel.Animal));
        string missing = json.Replace("\"" + section + "\":", "\"Removed" + section + "\":");
        Assert.Throws<InvalidDataException>(() => SaveManager.ParseSaveDataForEditor(missing));
    }

    [Test]
    public void CurrentArchiveIgnoresRetiredSaveFileName()
    {
        string root = Path.Combine(Path.GetTempPath(), "KingdomSaveGeneration-" + Guid.NewGuid().ToString("N"));
        GameObject owner = null;
        try
        {
            Directory.CreateDirectory(root);
            SaveManager.SetSaveRootOverrideForTests(root);
            string json = JsonUtility.ToJson(CreateBaseSave(TechLevel.Animal));
            File.WriteAllText(Path.Combine(root, "KingdomSave.json"), json);
            owner = new GameObject("SaveGeneration-Test");
            SaveManager manager = owner.AddComponent<SaveManager>();
            Assert.That(manager.HasSave, Is.False, "The retired filename must never be considered a current save.");
            File.WriteAllText(Path.Combine(root, "KingdomSave-v1.json"), json);
            Assert.That(manager.HasSave, Is.True);
        }
        finally
        {
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            SaveManager.ClearSaveRootOverrideForTests();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public void OfflineSettlementIsCappedAtConfiguredTwentyFourHourWindow()
    {
        const double maximumSeconds = 24d * 60d * 60d;
        double elapsed = SaveManager.CalculateOfflineElapsedSeconds(
            100L,
            100L + (long)(maximumSeconds * 2d),
            maximumSeconds);

        Assert.That(elapsed, Is.GreaterThan(0d));
        Assert.That(elapsed, Is.LessThanOrEqualTo(maximumSeconds));
    }

    [Test]
    public void StorySaveDataRoundTripPreservesCompletedPrefix()
    {
        SaveManager.KingdomSaveData save = CreateBaseSave(TechLevel.Industrial);
        save.Story.CompletedChapterIds = new List<string>
        {
            "PrologueAshes_00", "FirstFire_01", "WallsAndShelter_02"
        };
        SaveManager.KingdomSaveData restored = RoundTrip(save);
        Assert.That(restored.Story.CompletedChapterIds,
            Is.EqualTo(save.Story.CompletedChapterIds));
        SaveManager.ValidateStorySaveData(restored);
    }

    [Test]
    public void StorySaveDataRejectsUnknownDuplicateAndSkippedIds()
    {
        SaveManager.KingdomSaveData save = CreateBaseSave(TechLevel.Industrial);
        save.Story.CompletedChapterIds = new List<string>
        {
            "PrologueAshes_00", "IndustrialAwakening_08"
        };
        Assert.Throws<System.IO.InvalidDataException>(() =>
            SaveManager.ValidateStorySaveData(save));

        save.Story.CompletedChapterIds = new List<string>
        {
            "PrologueAshes_00", "PrologueAshes_00"
        };
        Assert.Throws<System.IO.InvalidDataException>(() =>
            SaveManager.ValidateStorySaveData(save));

        save.Story.CompletedChapterIds = new List<string> { "Unknown_99" };
        Assert.Throws<System.IO.InvalidDataException>(() =>
            SaveManager.ValidateStorySaveData(save));
    }

    private static SaveManager.KingdomSaveData CreateBaseSave(TechLevel techLevel)
    {
        return new SaveManager.KingdomSaveData
        {
            Version = SaveFormat.CurrentVersion,
            General = new SaveManager.GameSaveData
            {
                TechLevel = techLevel,
                FoodAmount = "10",
                Population = "2",
                TerritoryTotal = "1",
                PopulationChangeProgress = "0",
                AttackPower = "0",
                DefensePower = "0",
                FleetPower = "0",
                MilitaryManpower = "0",
                SupplySatisfaction = "1",
                PowerSatisfaction = "1",
                LogisticsSatisfaction = "1",
                CampaignCasualties = "0",
                CampaignCombatRatio = "0"
            },
            Resources = new SaveManager.ResourceSaveData
            {
                GlobalEfficiencyFactor = "1",
                Resources = new List<SaveManager.ResourceStateSaveData>()
            },
            Buildings = new SaveManager.BuildingSaveData
            {
                GlobalEfficiencyFactor = "1",
                Buildings = new List<SaveManager.BuildingStateSaveData>()
            },
            Researches = new SaveManager.ResearchSaveData
            {
                GlobalEfficiencyFactor = "1",
                States = new List<SaveManager.ResearchStateSaveData>(),
                QueuedResearchIds = new List<string>()
            },
            Workshop = new SaveManager.WorkshopSaveData
            {
                PurchasedUpgradeIds = new List<string>()
            },
            Sectors = new SaveManager.SectorSaveData
            {
                States = new List<SaveManager.SectorStateSaveData>()
            },
            UltraProject = new UltraProjectStateSaveData
            {
                ProjectId = UltraProjectState.ProjectId,
                Doctrine = UltraProjectDoctrine.None,
                Status = UltraProjectStatus.Locked,
                CurrentStage = UltraProjectStage.None,
                StageProgress = "0",
                CompletedStages = new List<UltraProjectStage>(),
                LaunchFeePaid = false,
                StateVersion = 1
            },
            Relic = new RelicState().CaptureSaveDataForEditor(),
            Tutorial = new SaveManager.TutorialSaveData
            {
                CompletedStepIds = new List<string>()
            },
            Story = new SaveManager.StorySaveData
            {
                CompletedChapterIds = new List<string>()
            }
        };
    }

    private static SaveManager.KingdomSaveData RoundTrip(SaveManager.KingdomSaveData save)
    {
        string json = JsonUtility.ToJson(save);
        Assert.That(json, Is.Not.Empty);
        return JsonUtility.FromJson<SaveManager.KingdomSaveData>(json);
    }
}
