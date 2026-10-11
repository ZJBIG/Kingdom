using System.Collections.Generic;
using NUnit.Framework;

public sealed class StoryManagerTests
{
    [Test]
    public void ArchiveHasReadableProgressChapters()
    {
        Assert.That(StoryManager.Chapters.Count, Is.EqualTo(18));
        var ids = new HashSet<string>();
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
        {
            StoryChapter chapter = StoryManager.Chapters[i];
            Assert.That(chapter, Is.Not.Null);
            Assert.That(ids.Add(chapter.Id), Is.True);
            Assert.That(chapter.Id, Is.Not.Null.And.Not.Empty);
            Assert.That(chapter.Title, Is.Not.Null.And.Not.Empty);
            Assert.That(chapter.EraLabel, Is.Not.Null.And.Not.Empty);
            Assert.That(chapter.Summary, Is.Not.Null.And.Not.Empty);
            Assert.That(chapter.Body.Trim().Length, Is.InRange(200, 300),
                chapter.Id + " should contain a complete 200-300 character story.");
            Assert.That(chapter.Body, Does.Not.Contain("..."),
                chapter.Id + " must not rely on truncated story text.");
        }
    }

    [Test]
    public void StoryArchiveHasFixedOrderAndNoMetaNarrativeTerms()
    {
        string[] expected =
        {
            "PrologueAshes_00", "FirstFire_01", "WallsAndShelter_02",
            "TheGrowingClan_03", "RememberedKnowledge_04", "TheFirstChain_05",
            "StoneAgeReturn_06", "MedievalOrder_07", "IndustrialAwakening_08",
            "WorkshopMemory_09", "IndustrialPower_10", "IndustrialMaterials_11",
            "IndustrialChemistry_12", "IndustrialFrontier_13", "FrontierSectors_14",
            "WarBetweenStars_15", "BeyondTheSky_16", "TheOldBoundary_17"
        };
        string[] forbidden = { "玩家", "页面", "菜单", "按钮", "ID", "数值", "数字", "奖励", "界面" };
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.That(StoryManager.Chapters[i].Id, Is.EqualTo(expected[i]));
            string text = StoryManager.Chapters[i].Summary + StoryManager.Chapters[i].Body;
            for (int f = 0; f < forbidden.Length; f++)
                Assert.That(text, Does.Not.Contain(forbidden[f]));
        }
    }


    [Test]
    public void TutorialProgressUnlocksActionChaptersInOrder()
    {
        Assert.That(FindChapter("FirstFire_01").RequiredTutorialStepId,
            Is.EqualTo("resources"));
        Assert.That(FindChapter("WallsAndShelter_02").RequiredTutorialStepId,
            Is.EqualTo("building"));
        Assert.That(FindChapter("TheGrowingClan_03").RequiredTutorialStepId,
            Is.EqualTo("population"));
        Assert.That(FindChapter("RememberedKnowledge_04").RequiredTutorialStepId,
            Is.EqualTo("research"));
        Assert.That(FindChapter("TheFirstChain_05").RequiredTutorialStepId,
            Is.EqualTo("production-chain"));
        Assert.That(FindChapter("TheOldBoundary_17").RequiredEra,
            Is.EqualTo(TechLevel.Ultra));
    }

    [Test]
    public void TutorialCompletionIsIncludedInStoryRefreshSignature()
    {
        TutorialManager manager = TutorialManager.Ensure();
        manager.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            ActiveStepId = "resources",
            CompletedStepIds = new List<string>()
        }, TechLevel.Animal);
        string before = StoryManager.GetProgressSignature(TechLevel.Animal, manager);

        manager.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            ActiveStepId = "building",
            CompletedStepIds = new List<string> { "orientation", "resources" }
        }, TechLevel.Animal);
        string after = StoryManager.GetProgressSignature(TechLevel.Animal, manager);

        Assert.That(before, Does.Contain("|t:resources=0"));
        Assert.That(after, Does.Contain("|t:resources=1"));
        Assert.That(after, Is.Not.EqualTo(before));
    }

    [Test]
    public void LaterMemoriesDeclareTheirRealActionGates()
    {
        StoryChapter workshop = FindChapter("WorkshopMemory_09");
        StoryChapter frontier = FindChapter("FrontierSectors_14");
        StoryChapter war = FindChapter("WarBetweenStars_15");
        StoryChapter beyond = FindChapter("BeyondTheSky_16");
        Assert.That(workshop.RequiredWorkshopIds.Count, Is.GreaterThan(0));
        Assert.That(FirstId(workshop.RequiredResearchIds),
            Is.EqualTo("PrecisionManufacturing"));
        Assert.That(FirstId(workshop.RequiredWorkshopIds), Is.EqualTo("PrecisionTooling"));
        Assert.That(frontier.RequiredOccupiedSectorIds.Count, Is.GreaterThan(0));
        Assert.That(war.RequiredOccupiedSectorIds.Count, Is.GreaterThan(0));
        Assert.That(beyond.RequiredOccupiedSectorIds.Count, Is.GreaterThan(0));
        Assert.That(FirstId(frontier.RequiredResearchIds),
            Is.EqualTo("HomeSystemSurvey"));
    }

    [Test]
    public void IndustrialMemoriesDeclareRealResearchAndBuildingGates()
    {
        StoryChapter power = FindChapter("IndustrialPower_10");
        StoryChapter awakening = FindChapter("IndustrialAwakening_08");
        StoryChapter materials = FindChapter("IndustrialMaterials_11");
        StoryChapter chemistry = FindChapter("IndustrialChemistry_12");
        StoryChapter frontier = FindChapter("IndustrialFrontier_13");

        Assert.That(power, Is.Not.Null);
        Assert.That(awakening, Is.Not.Null);
        Assert.That(FirstId(awakening.RequiredResearchIds), Is.EqualTo("Industrialization"));
        Assert.That(awakening.RequiredBuildingIds, Has.Count.GreaterThan(0));
        Assert.That(FirstId(power.RequiredResearchIds), Is.EqualTo("SteamPower"));
        Assert.That(FirstId(power.RequiredBuildingIds), Is.EqualTo("SteamPlant"));
        Assert.That(materials, Is.Not.Null);
        Assert.That(FirstId(materials.RequiredResearchIds), Is.EqualTo("IndustrialMetalSmelting"));
        Assert.That(FirstId(materials.RequiredBuildingIds), Is.EqualTo("IndustrialMetalSmelter"));
        Assert.That(chemistry, Is.Not.Null);
        Assert.That(FirstId(chemistry.RequiredResearchIds), Is.EqualTo("IndustrialChemistry"));
        Assert.That(FirstId(chemistry.RequiredBuildingIds), Is.EqualTo("ChemicalPlant"));
        Assert.That(frontier, Is.Not.Null);
        Assert.That(FirstId(frontier.RequiredResearchIds), Is.EqualTo("TitaniumAlloyEngineering"));
        Assert.That(frontier.RequiredResearchIds, Does.Not.Contain("OrbitalEngineering"));
        Assert.That(frontier.RequiredBuildingIds, Does.Not.Contain("LaunchCenter"));
        Assert.That(frontier.RequiredWorkshopIds, Does.Not.Contain("ReusableLaunchStages"));
    }

    [Test]
    public void StoryTutorialGatesResolveToActualTutorialSteps()
    {
        TutorialManager tutorial = TutorialManager.Ensure();
        Assert.That(tutorial, Is.Not.Null);

        var stepIds = new HashSet<string>();
        for (int i = 0; i < tutorial.Steps.Count; i++)
            if (tutorial.Steps[i] != null)
                stepIds.Add(tutorial.Steps[i].Id);

        for (int i = 0; i < StoryManager.Chapters.Count; i++)
        {
            StoryChapter chapter = StoryManager.Chapters[i];
            if (chapter == null || string.IsNullOrEmpty(chapter.RequiredTutorialStepId))
                continue;
            Assert.That(stepIds.Contains(chapter.RequiredTutorialStepId), Is.True,
                chapter.Id + " must reference an existing TutorialStep, not a Research ID.");
        }
    }

    [Test]
    public void StoryActionGatesResolveToRealDefinitions()
    {
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
        {
            StoryChapter chapter = StoryManager.Chapters[i];
            if (chapter == null)
                continue;

            for (int j = 0; j < chapter.RequiredResearchIds.Count; j++)
                Assert.That(DataBase<Research>.TryFind(
                    chapter.RequiredResearchIds[j], out Research research) &&
                    research != null, Is.True,
                    chapter.Id + " must reference an existing Research definition.");

            for (int j = 0; j < chapter.RequiredBuildingIds.Count; j++)
                Assert.That(DataBase<Building>.TryFind(
                    chapter.RequiredBuildingIds[j], out Building building) &&
                    building != null, Is.True,
                    chapter.Id + " must reference an existing Building definition.");

            for (int j = 0; j < chapter.RequiredWorkshopIds.Count; j++)
                Assert.That(DataBase<WorkshopUpgrade>.TryFind(
                    chapter.RequiredWorkshopIds[j], out WorkshopUpgrade workshop) &&
                    workshop != null, Is.True,
                    chapter.Id + " must reference an existing Workshop definition.");
        }
    }

    [Test]
    public void MachineFactoryRetainsItsWorkshopGate()
    {
        Assert.That(DataBase<Building>.TryFind(
            "MachineFactory", out Building machineFactory), Is.True);
        Assert.That(machineFactory, Is.Not.Null);
        Assert.That(DataBase<Research>.TryFind(
            "PrecisionManufacturing", out Research manufacturing), Is.True);
        Assert.That(machineFactory.RequiredResearch,
            Does.Contain(manufacturing),
            "Story research gate must be a real MachineFactory prerequisite.");
        Assert.That(machineFactory.RequiredWorkshopUpgrades.Count,
            Is.GreaterThan(0),
            "Story must acknowledge the real Workshop gate of MachineFactory.");
        bool hasPrecisionTooling = false;
        for (int i = 0; i < machineFactory.RequiredWorkshopUpgrades.Count; i++)
            if (machineFactory.RequiredWorkshopUpgrades[i] != null &&
                machineFactory.RequiredWorkshopUpgrades[i].Id == "PrecisionTooling")
                hasPrecisionTooling = true;
        Assert.That(hasPrecisionTooling, Is.True,
            "MachineFactory must retain its real PrecisionTooling Workshop gate.");
    }

    [Test]
    public void IndustrialMemoryGatesResolveToRealDefinitions()
    {
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
        {
            StoryChapter chapter = StoryManager.Chapters[i];
            if (chapter.RequiredEra != TechLevel.Industrial)
                continue;

            for (int j = 0; j < chapter.RequiredResearchIds.Count; j++)
                Assert.That(DataBase<Research>.TryFind(
                    chapter.RequiredResearchIds[j], out Research research), Is.True,
                    chapter.Id + " must reference a real Research definition.");

            for (int j = 0; j < chapter.RequiredBuildingIds.Count; j++)
                Assert.That(DataBase<Building>.TryFind(
                    chapter.RequiredBuildingIds[j], out Building building), Is.True,
                    chapter.Id + " must reference a real Building definition.");

            for (int j = 0; j < chapter.RequiredWorkshopIds.Count; j++)
                Assert.That(DataBase<WorkshopUpgrade>.TryFind(
                    chapter.RequiredWorkshopIds[j], out WorkshopUpgrade workshop), Is.True,
                    chapter.Id + " must reference a real Workshop definition.");
        }
    }

    [Test]
    public void IndustrialStoryResearchMatchesItsBuildingPrerequisite()
    {
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
        {
            StoryChapter chapter = StoryManager.Chapters[i];
            if (chapter == null || chapter.RequiredEra != TechLevel.Industrial ||
                chapter.RequiredResearchIds.Count == 0 ||
                chapter.RequiredBuildingIds.Count == 0)
                continue;

            Assert.That(DataBase<Research>.TryFind(
                chapter.RequiredResearchIds[0], out Research research), Is.True,
                chapter.Id + " must resolve its research gate.");
            Assert.That(DataBase<Building>.TryFind(
                chapter.RequiredBuildingIds[0], out Building building), Is.True,
                chapter.Id + " must resolve its building gate.");
            Assert.That(building.RequiredResearch, Does.Contain(research),
                chapter.Id + " must name the real research prerequisite of " +
                chapter.RequiredBuildingIds[0] + ".");
        }
    }

    [Test]
    public void WorkshopMemoryUsesTheDeclaredRealUpgrade()
    {
        StoryChapter chapter = FindChapter("WorkshopMemory_09");
        Assert.That(chapter, Is.Not.Null);
        Assert.That(chapter.RequiredWorkshopIds.Count, Is.GreaterThan(0));
        Assert.That(FirstId(chapter.RequiredResearchIds),
            Is.EqualTo("PrecisionManufacturing"));
        Assert.That(DataBase<WorkshopUpgrade>.TryFind(
            FirstId(chapter.RequiredWorkshopIds), out WorkshopUpgrade workshop), Is.True);
        Assert.That(workshop, Is.Not.Null);
        Assert.That(workshop.Id, Is.EqualTo(FirstId(chapter.RequiredWorkshopIds)));
        Assert.That(DataBase<Research>.TryFind(
            FirstId(chapter.RequiredResearchIds), out Research requiredResearch), Is.True);
        Assert.That(workshop.RequiredResearch,
            Does.Contain(requiredResearch),
            "The Story chapter must name the Workshop's direct Research prerequisite.");
    }

    [Test]
    public void IndustrialMemoriesFollowTheActionArchiveOrder()
    {
        string[] expected =
        {
            "IndustrialAwakening_08",
            "WorkshopMemory_09",
            "IndustrialPower_10",
            "IndustrialMaterials_11",
            "IndustrialChemistry_12",
            "IndustrialFrontier_13"
        };
        int previousIndex = -1;
        for (int i = 0; i < expected.Length; i++)
        {
            int currentIndex = IndexOf(expected[i]);
            Assert.That(currentIndex, Is.GreaterThan(previousIndex));
            Assert.That(StoryManager.Chapters[currentIndex].RequiredEra,
                Is.EqualTo(TechLevel.Industrial));
            previousIndex = currentIndex;
        }

        StoryChapter workshop = FindChapter("WorkshopMemory_09");
        Assert.That(FirstId(workshop.RequiredResearchIds),
            Is.EqualTo("PrecisionManufacturing"));
        Assert.That(FirstId(workshop.RequiredWorkshopIds), Is.EqualTo("PrecisionTooling"));
        Assert.That(workshop.RequiredWorkshopIds.Count, Is.GreaterThan(0));

        StoryChapter materials = FindChapter("IndustrialMaterials_11");
        Assert.That(materials.Body, Does.Not.Contain("IntegratedFurnaces"));
    }

    [Test]
    public void IndustrialHintExplainsEraBeforeFutureIndustrialAction()
    {
        StoryChapter power = FindChapter("IndustrialPower_10");
        string hint = StoryManager.GetUnlockHint(power, TechLevel.Medieval);
        Assert.That(hint, Does.Contain("工业"));
        Assert.That(hint, Does.Not.Contain("SteamPower"));
    }

    [Test]
    public void IndustrialHintsPrioritizeThePreviousMemoryBlocker()
    {
        TutorialManager tutorial = TutorialManager.Ensure();
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            ActiveStepId = "orientation",
            CompletedStepIds = new List<string>()
        }, TechLevel.Industrial);

        StoryChapter scale = FindChapter("IndustrialPower_10");
        string hint = StoryManager.GetChapterProgressHint(
            scale, TechLevel.Industrial);

        Assert.That(hint, Does.Contain("上一段工业记忆"));
        Assert.That(hint, Does.Not.Contain("PrecisionManufacturing"));
    }

    [Test]
    public void UpgradedBuildingContinuesToSatisfyItsOriginalStoryRequirement()
    {
        var host = new UnityEngine.GameObject("Story-UpgradeRequirement");
        try
        {
            var game = host.AddComponent<GameManager>();
            game.State.RestoreCoreForEditor(0, TechLevel.StoneAge, new ExpantaNum(300), 0L);
            host.AddComponent<ResourceManager>();
            var buildings = host.AddComponent<BuildingManager>();
            var research = host.AddComponent<ResearchManager>();
            research.InitializeForEditor();
            foreach (var entry in research.States)
            {
                var paid = new Dictionary<Resource, ExpantaNum>();
                foreach (var cost in entry.Key.ResourceRequirements)
                    paid[cost.First] = cost.Second;
                entry.Value.RestoreForEditor(entry.Value.BaseCost, true, true, paid);
            }
            Building original = DataBase<Building>.Find("Farm");
            Assert.That(original.UpgradeTo, Is.Not.Null);
            Assert.That(StoryManager.HasOwnedBuildingForEditor(original.Id), Is.False);
            buildings.EnsureBuilding(original.UpgradeTo).SetAmountForEditor(ExpantaNum.One);
            Assert.That(buildings.EnsureBuilding(original).Amount, Is.LessThan(ExpantaNum.One));
            Assert.That(StoryManager.HasOwnedBuildingForEditor(original.Id), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [Test]
    public void MainlineMemoriesDoNotRequireOptionalKnowledgeOrCivicBuildings()
    {
        Assert.That(FindChapter("RememberedKnowledge_04").RequiredResearchIds, Is.Empty);
        Assert.That(FindChapter("TheFirstChain_05").RequiredBuildingIds, Is.Empty);
        Assert.That(FindChapter("MedievalOrder_07").RequiredBuildingIds, Is.Empty);
    }

    private static StoryChapter FindChapter(string id)
    {
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
            if (StoryManager.Chapters[i].Id == id)
                return StoryManager.Chapters[i];
        return null;
    }

    private static string FirstId(IReadOnlyList<string> ids)
    {
        return ids != null && ids.Count > 0 ? ids[0] : string.Empty;
    }

    private static int IndexOf(string id)
    {
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
            if (StoryManager.Chapters[i].Id == id)
                return i;
        return -1;
    }
}
