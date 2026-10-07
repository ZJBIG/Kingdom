using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SaveArchivePressureTests
{
    private static readonly TechLevel[] AllTechLevels =
    {
        TechLevel.Animal,
        TechLevel.StoneAge,
        TechLevel.Medieval,
        TechLevel.Industrial,
        TechLevel.Spacer,
        TechLevel.Ultra,
        TechLevel.Archotech
    };

    [Test]
    public void NewGameArchiveHasAllSectionsAndSupportsEveryEra()
    {
        for (int i = 0; i < AllTechLevels.Length; i++)
        {
            SaveManager.KingdomSaveData save = CreateBaseSave(AllTechLevels[i]);
            string json = JsonUtility.ToJson(save);
            SaveManager.KingdomSaveData restored =
                JsonUtility.FromJson<SaveManager.KingdomSaveData>(json);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Version, Is.EqualTo(SaveFormat.CurrentVersion));
            Assert.That(restored.General, Is.Not.Null);
            Assert.That(restored.General.TechLevel, Is.EqualTo(AllTechLevels[i]));
            Assert.That(restored.Resources, Is.Not.Null);
            Assert.That(restored.Buildings, Is.Not.Null);
            Assert.That(restored.Researches, Is.Not.Null);
            Assert.That(restored.Workshop, Is.Not.Null);
            Assert.That(restored.Sectors, Is.Not.Null);
            Assert.That(restored.UltraProject, Is.Not.Null);
            Assert.That(restored.UltraProject.Status, Is.EqualTo(UltraProjectStatus.Locked));
            Assert.That(restored.Tutorial, Is.Not.Null);
            Assert.That(restored.Story, Is.Not.Null);
            Assert.That(restored.Story.CompletedChapterIds, Is.Not.Null);
        }
    }

    [Test]
    public void ProgressionArchivePreservesResearchIntermediatesAndDeepWorkshopPrerequisites()
    {
        SaveManager.KingdomSaveData save = CreateBaseSave(TechLevel.Industrial);
        save.Researches.ActiveResearchId = "DeepSpaceFleet";
        save.Researches.SelectedResearchId = "InterstellarNavigation";
        save.Researches.QueuedResearchIds = new List<string>
        {
            "IndustrialWorkshop",
            "InterstellarNavigation"
        };
        save.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = "IndustrialWorkshop",
                Progress = "17.5",
                CostPaid = false,
                Completed = false
            },
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = "DeepSpaceFleet",
                Progress = "3.25",
                CostPaid = true,
                Completed = false,
                PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>
                {
                    new SaveManager.ResearchResourceCostSaveData
                    {
                        ResourceId = "Steel",
                        Amount = "12.5"
                    },
                    new SaveManager.ResearchResourceCostSaveData
                    {
                        ResourceId = "Electronics",
                        Amount = "4.75"
                    }
                }
            },
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = "InterstellarNavigation",
                Progress = "0",
                CostPaid = false,
                Completed = false
            }
        };
        save.Workshop.PurchasedUpgradeIds = new List<string>
        {
            "DraftingTables",
            "DeepSpaceSystemsControl",
            "AutomatedFleetResupplyModules",
            "AdaptiveArmorRepairSystems",
            "InterstellarCombatSupplySystems"
        };

        SaveManager.KingdomSaveData restored = RoundTrip(save);

        Assert.That(restored.Researches.ActiveResearchId, Is.EqualTo("DeepSpaceFleet"));
        Assert.That(restored.Researches.SelectedResearchId, Is.EqualTo("InterstellarNavigation"));
        Assert.That(restored.Researches.QueuedResearchIds,
            Is.EqualTo(new[] { "IndustrialWorkshop", "InterstellarNavigation" }));
        Assert.That(restored.Researches.States, Has.Count.EqualTo(3));
        Assert.That(restored.Researches.States[0].CostPaid, Is.False);
        Assert.That(restored.Researches.States[0].Completed, Is.False);
        Assert.That(restored.Researches.States[1].CostPaid, Is.True);
        Assert.That(restored.Researches.States[1].Completed, Is.False);
        Assert.That(restored.Researches.States[1].Progress, Is.Not.Empty);
        Assert.That(restored.Researches.States[1].PaidResourceCosts, Has.Count.EqualTo(2));
        Assert.That(restored.Workshop.PurchasedUpgradeIds,
            Does.Contain("AdaptiveArmorRepairSystems"));
        Assert.That(restored.Workshop.PurchasedUpgradeIds,
            Does.Contain("InterstellarCombatSupplySystems"));
    }

    [Test]
    public void SpaceCombatAndTutorialArchivePreservesSectorModesFleetRepairAndActiveStep()
    {
        SaveManager.KingdomSaveData save = CreateBaseSave(TechLevel.Spacer);
        save.General.FleetPower = "240";
        save.General.MilitaryManpower = "80";
        save.General.CampaignActive = true;
        save.General.CampaignTargetSectorId = "SiriusResourceBelt";
        save.General.CampaignCasualties = "12.5";
        save.General.CampaignCombatRatio = "0.82";
        save.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = "DeepSpaceFleet",
                Progress = "40",
                CostPaid = true,
                Completed = true
            }
        };
        save.Workshop.PurchasedUpgradeIds = new List<string>
        {
            "AdaptiveArmorRepairSystems",
            "AutomatedFleetResupplyModules"
        };
        save.Sectors.States = new List<SaveManager.SectorStateSaveData>
        {
            new SaveManager.SectorStateSaveData
            {
                SectorId = "AzurePool",
                Unlocked = true,
                Occupied = true,
                CampaignActive = false,
                CampaignProgress = "0.75",
                VisitCount = 4
            },
            new SaveManager.SectorStateSaveData
            {
                SectorId = "SiriusResourceBelt",
                Unlocked = true,
                Occupied = false,
                CampaignActive = true,
                CampaignProgress = "0.30",
                CampaignCasualties = "6.5",
                CampaignCombatRatio = "0.9",
                VisitCount = 2
            }
        };
        save.Tutorial.ActiveStepId = "era-goal";
        save.Tutorial.CompletedStepIds = new List<string> { "orientation", "resources" };

        SaveManager.KingdomSaveData restored = RoundTrip(save);

        Assert.That(restored.General.FleetPower, Is.Not.Empty);
        Assert.That(restored.General.MilitaryManpower, Is.Not.Empty);
        Assert.That(restored.General.CampaignActive, Is.True);
        Assert.That(restored.General.CampaignTargetSectorId, Is.EqualTo("SiriusResourceBelt"));
        Assert.That(restored.Sectors.States, Has.Count.EqualTo(2));
        Assert.That(restored.Sectors.States[0].Occupied, Is.True);
        Assert.That(restored.Sectors.States[1].CampaignActive, Is.True);
        Assert.That(restored.Sectors.States[1].CampaignProgress, Is.Not.Empty);
        Assert.That(restored.Workshop.PurchasedUpgradeIds,
            Does.Contain("AdaptiveArmorRepairSystems"));
        Assert.That(restored.Tutorial.ActiveStepId, Is.EqualTo("era-goal"));
        Assert.That(restored.Tutorial.CompletedStepIds,
            Does.Contain("resources"));
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
                TerritoryTotal = "1"
            },
            Resources = new SaveManager.ResourceSaveData
            {
                Resources = new List<SaveManager.ResourceStateSaveData>()
            },
            Buildings = new SaveManager.BuildingSaveData
            {
                Buildings = new List<SaveManager.BuildingStateSaveData>()
            },
            Researches = new SaveManager.ResearchSaveData
            {
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
                SaveVersion = UltraProjectState.CurrentSaveVersion,
                Doctrine = UltraProjectDoctrine.None,
                Status = UltraProjectStatus.Locked,
                CurrentStage = UltraProjectStage.None,
                StageProgress = "0",
                CompletedStages = new List<UltraProjectStage>(),
                LaunchFeePaid = false,
                StateVersion = 1
            },
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
