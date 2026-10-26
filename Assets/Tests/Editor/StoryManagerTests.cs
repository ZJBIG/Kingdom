using System.Collections.Generic;
using NUnit.Framework;

public sealed class StoryManagerTests
{
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
        Assert.That(StoryManager.Chapters.Count, Is.EqualTo(expected.Length));
        string[] forbidden = { "玩家", "页面", "菜单", "按钮", "ID", "数值", "数字", "奖励", "界面" };
        for (int i = 0; i < expected.Length; i++)
        {
            StoryChapter chapter = StoryManager.Chapters[i];
            Assert.That(chapter.Id, Is.EqualTo(expected[i]));
            Assert.That(chapter.Title, Is.Not.Null.And.Not.Empty);
            Assert.That(chapter.EraLabel, Is.Not.Null.And.Not.Empty);
            Assert.That(chapter.Summary, Is.Not.Null.And.Not.Empty);
            Assert.That(chapter.Body.Trim().Length, Is.InRange(200, 300),
                chapter.Id + " should contain a complete 200-300 character story.");
            Assert.That(chapter.Body, Does.Not.Contain("..."),
                chapter.Id + " must not rely on truncated story text.");
            if (i >= 8 && i <= 13)
                Assert.That(chapter.RequiredEra, Is.EqualTo(TechLevel.Industrial));
            string text = chapter.Summary + chapter.Body;
            for (int f = 0; f < forbidden.Length; f++)
                Assert.That(text, Does.Not.Contain(forbidden[f]));
        }
        Assert.That(FindChapter("IndustrialMaterials_11").Body, Does.Not.Contain("IntegratedFurnaces"));
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
            Is.Empty, "Story follows real production, independently of the later production-tree tutorial.");
        Assert.That(FindChapter("TheOldBoundary_17").RequiredEra,
            Is.EqualTo(TechLevel.Ultra));
    }

    [Test]
    public void EarlyChaptersRequireDevelopmentBeyondTutorialClicks()
    {
        Assert.That(FindChapter("WallsAndShelter_02").RequiredBuildingIds,
            Does.Contain("WoodHouse"));
        Assert.That(FindChapter("TheGrowingClan_03").RequiredResearchIds,
            Does.Contain("Agriculture"));
        Assert.That(FindChapter("RememberedKnowledge_04").RequiredResearchIds,
            Does.Contain("KnowledgeSharing"));
        Assert.That(FindChapter("TheFirstChain_05").RequiredBuildingIds,
            Does.Contain("Quarry").And.Contain("StoneCuttingWorkshop"));
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
                Assert.That(DataBase<ResearchDef>.TryFind(
                    chapter.RequiredResearchIds[j], out ResearchDef research) &&
                    research != null, Is.True,
                    chapter.Id + " must reference an existing Research definition.");

            for (int j = 0; j < chapter.RequiredBuildingIds.Count; j++)
                Assert.That(DataBase<BuildingDef>.TryFind(
                    chapter.RequiredBuildingIds[j], out BuildingDef building) &&
                    building != null, Is.True,
                    chapter.Id + " must reference an existing Building definition.");

            for (int j = 0; j < chapter.RequiredWorkshopIds.Count; j++)
                Assert.That(DataBase<WorkshopUpgradeDef>.TryFind(
                    chapter.RequiredWorkshopIds[j], out WorkshopUpgradeDef workshop) &&
                    workshop != null, Is.True,
                    chapter.Id + " must reference an existing Workshop definition.");
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

            Assert.That(DataBase<ResearchDef>.TryFind(
                chapter.RequiredResearchIds[0], out ResearchDef research), Is.True,
                chapter.Id + " must resolve its research gate.");
            Assert.That(DataBase<BuildingDef>.TryFind(
                chapter.RequiredBuildingIds[0], out BuildingDef building), Is.True,
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
        Assert.That(chapter.RequiredWorkshopIds.Count, Is.GreaterThan(0));
        Assert.That(FirstId(chapter.RequiredResearchIds),
            Is.EqualTo("PrecisionManufacturing"));
        Assert.That(DataBase<WorkshopUpgradeDef>.TryFind(
            FirstId(chapter.RequiredWorkshopIds), out WorkshopUpgradeDef workshop), Is.True);
        Assert.That(DataBase<ResearchDef>.TryFind(
            FirstId(chapter.RequiredResearchIds), out ResearchDef requiredResearch), Is.True);
        Assert.That(workshop.RequiredResearch,
            Does.Contain(requiredResearch),
            "The Story chapter must name the Workshop's direct Research prerequisite.");
        BuildingDef machineFactory = DataBase<BuildingDef>.Find("MachineFactory");
        Assert.That(machineFactory.RequiredResearch, Does.Contain(requiredResearch),
            "Story research gate must be a real MachineFactory prerequisite.");
        Assert.That(machineFactory.RequiredWorkshopUpgrades, Does.Contain(workshop),
            "Story must acknowledge the real Workshop gate of MachineFactory.");
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
                var paid = new Dictionary<ResourceDef, ExpantaNum>();
                foreach (var cost in entry.Key.ResourceRequirements)
                    paid[cost.First] = cost.Second;
                entry.Value.RestoreForEditor(entry.Value.BaseCost, true, true, paid);
            }
            BuildingDef original = DataBase<BuildingDef>.Find("Farm");
            Assert.That(original.UpgradeTo, Is.Not.Null);
            Assert.That(StoryManager.HasOwnedBuildingForEditor(original.Id), Is.False);
            buildings.EnsureBuilding(original.UpgradeTo).SetAmountForEditor(ExpantaNum.One);
            Assert.That(buildings.EnsureBuilding(original).Amount, Is.LessThan(ExpantaNum.One));
            Assert.That(StoryManager.HasOwnedBuildingForEditor(original.Id), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [Test]
    public void MainlineMemoriesRequireProgressionWithoutOptionalKnowledgeOrCivicBuildings()
    {
        StoryChapter knowledge = FindChapter("RememberedKnowledge_04");
        Assert.That(knowledge.RequiredResearchIds, Is.EquivalentTo(new[] { "KnowledgeSharing" }));
        Assert.That(knowledge.RequiredBuildingIds, Is.Empty, "Knowledge requires its milestone research, without forcing an optional knowledge building.");
        Assert.That(FindChapter("TheFirstChain_05").RequiredBuildingIds,
            Is.EquivalentTo(new[] { "Quarry", "StoneCuttingWorkshop" }), "The chain memory follows its actual raw-material and processing buildings.");
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

}
