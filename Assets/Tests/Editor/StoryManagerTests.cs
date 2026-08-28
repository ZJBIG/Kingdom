using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public sealed class StoryManagerTests
{
    [Test]
    public void ArchiveHasReadableProgressChapters()
    {
        Assert.That(StoryManager.Chapters.Count, Is.GreaterThanOrEqualTo(8));
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
            Assert.That(chapter.Body.Length, Is.GreaterThanOrEqualTo(70),
                chapter.Id + " should explain the action and its meaning.");
            Assert.That(chapter.Body, Does.Not.Contain("..."),
                chapter.Id + " must not rely on truncated story text.");
        }
    }

    [Test]
    public void TutorialProgressUnlocksActionChaptersInOrder()
    {
        Assert.That(FindChapter("first-fire").RequiredTutorialStepId,
            Is.EqualTo("resources"));
        Assert.That(FindChapter("walls-and-shelter").RequiredTutorialStepId,
            Is.EqualTo("building"));
        Assert.That(FindChapter("the-growing-clan").RequiredTutorialStepId,
            Is.EqualTo("population"));
        Assert.That(FindChapter("remembered-knowledge").RequiredTutorialStepId,
            Is.EqualTo("research"));
        Assert.That(FindChapter("the-first-chain").RequiredTutorialStepId,
            Is.EqualTo("production-chain"));
        Assert.That(FindChapter("the-old-boundary").RequiredEra,
            Is.EqualTo(TechLevel.Archotech));
    }

    [Test]
    public void TutorialCompletionIsIncludedInStoryRefreshSignature()
    {
        TutorialManager manager = TutorialManager.Ensure();
        Invoke(manager, "RestoreSaveData", new SaveManager.TutorialSaveData
        {
            ActiveStepId = "resources",
            CompletedStepIds = new List<string>()
        }, TechLevel.Animal);
        string before = StoryManager.GetProgressSignature(TechLevel.Animal, manager);

        Invoke(manager, "RestoreSaveData", new SaveManager.TutorialSaveData
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
        StoryChapter workshop = FindChapter("workshop-memory");
        StoryChapter frontier = FindChapter("frontier-sectors");
        StoryChapter war = FindChapter("war-between-stars");
        StoryChapter beyond = FindChapter("beyond-the-sky");
        Assert.That(workshop.RequiresWorkshopPurchase, Is.True);
        Assert.That(workshop.RequiredResearchId,
            Is.EqualTo("PrecisionManufacturing"));
        Assert.That(workshop.RequiredWorkshopId, Is.EqualTo("PrecisionTooling"));
        Assert.That(frontier.RequiresSectorAccess, Is.True);
        Assert.That(war.RequiresSectorOccupation, Is.True);
        Assert.That(beyond.RequiresSectorOccupation, Is.True);
    }

    [Test]
    public void IndustrialMemoriesDeclareRealResearchAndBuildingGates()
    {
        StoryChapter power = FindChapter("industrial-power");
        StoryChapter scale = FindChapter("industrial-scale");
        StoryChapter awakening = FindChapter("industrial-awakening");
        StoryChapter homes = FindChapter("industrial-homes");
        StoryChapter network = FindChapter("industrial-network");
        StoryChapter materials = FindChapter("industrial-materials");
        StoryChapter chemistry = FindChapter("industrial-chemistry");
        StoryChapter grid = FindChapter("industrial-grid");
        StoryChapter knowledge = FindChapter("industrial-knowledge");
        StoryChapter frontier = FindChapter("industrial-frontier");

        Assert.That(power, Is.Not.Null);
        Assert.That(awakening, Is.Not.Null);
        Assert.That(awakening.RequiredResearchId, Is.EqualTo("Industrialization"));
        Assert.That(awakening.RequiredBuildingId, Is.Empty);
        Assert.That(scale, Is.Not.Null);
        Assert.That(scale.RequiredResearchId, Is.EqualTo("PrecisionManufacturing"));
        Assert.That(scale.RequiredBuildingId, Is.EqualTo("MachineFactory"));
        Assert.That(power.RequiredResearchId, Is.EqualTo("SteamPower"));
        Assert.That(power.RequiredBuildingId, Is.EqualTo("SteamPlant"));
        Assert.That(homes, Is.Not.Null);
        Assert.That(homes.RequiredResearchId, Is.EqualTo("IndustrialHabitationEngineering"));
        Assert.That(homes.RequiredBuildingId, Is.EqualTo("IndustrialHabitationComplex"));
        Assert.That(network.RequiredResearchId, Is.EqualTo("RailwayEngineering"));
        Assert.That(network.RequiredBuildingId, Is.EqualTo("RailHub"));
        Assert.That(materials, Is.Not.Null);
        Assert.That(materials.RequiredResearchId, Is.EqualTo("IndustrialMetalSmelting"));
        Assert.That(materials.RequiredBuildingId, Is.EqualTo("IndustrialMetalSmelter"));
        Assert.That(chemistry, Is.Not.Null);
        Assert.That(chemistry.RequiredResearchId, Is.EqualTo("IndustrialChemistry"));
        Assert.That(chemistry.RequiredBuildingId, Is.EqualTo("ChemicalPlant"));
        Assert.That(knowledge.RequiredResearchId, Is.EqualTo("ModernUniversity"));
        Assert.That(knowledge.RequiredBuildingId, Is.EqualTo("University"));
        Assert.That(grid, Is.Not.Null);
        Assert.That(grid.RequiredResearchId, Is.EqualTo("PowerGridEngineering"));
        Assert.That(grid.RequiredBuildingId, Is.EqualTo("CentralPowerStation"));
        Assert.That(frontier, Is.Not.Null);
        Assert.That(frontier.RequiredResearchId, Is.EqualTo("TitaniumAlloyEngineering"));
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

            if (!string.IsNullOrEmpty(chapter.RequiredResearchId))
                Assert.That(DataBase<Research>.TryFind(
                    chapter.RequiredResearchId, out Research research) &&
                    research != null, Is.True,
                    chapter.Id + " must reference an existing Research definition.");

            if (!string.IsNullOrEmpty(chapter.RequiredBuildingId))
                Assert.That(DataBase<Building>.TryFind(
                    chapter.RequiredBuildingId, out Building building) &&
                    building != null, Is.True,
                    chapter.Id + " must reference an existing Building definition.");

            if (!string.IsNullOrEmpty(chapter.RequiredWorkshopId))
                Assert.That(DataBase<WorkshopUpgrade>.TryFind(
                    chapter.RequiredWorkshopId, out WorkshopUpgrade workshop) &&
                    workshop != null, Is.True,
                    chapter.Id + " must reference an existing Workshop definition.");
        }
    }

    [Test]
    public void IndustrialScaleMatchesMachineFactoryWorkshopGate()
    {
        StoryChapter scale = FindChapter("industrial-scale");
        Assert.That(scale, Is.Not.Null);
        Assert.That(DataBase<Building>.TryFind(
            scale.RequiredBuildingId, out Building machineFactory), Is.True);
        Assert.That(machineFactory, Is.Not.Null);
        Assert.That(DataBase<Research>.TryFind(
            scale.RequiredResearchId, out Research manufacturing), Is.True);
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

            if (!string.IsNullOrEmpty(chapter.RequiredResearchId))
                Assert.That(DataBase<Research>.TryFind(
                    chapter.RequiredResearchId, out Research research), Is.True,
                    chapter.Id + " must reference a real Research definition.");

            if (!string.IsNullOrEmpty(chapter.RequiredBuildingId))
                Assert.That(DataBase<Building>.TryFind(
                    chapter.RequiredBuildingId, out Building building), Is.True,
                    chapter.Id + " must reference a real Building definition.");

            if (!string.IsNullOrEmpty(chapter.RequiredWorkshopId))
                Assert.That(DataBase<WorkshopUpgrade>.TryFind(
                    chapter.RequiredWorkshopId, out WorkshopUpgrade workshop), Is.True,
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
                string.IsNullOrEmpty(chapter.RequiredResearchId) ||
                string.IsNullOrEmpty(chapter.RequiredBuildingId))
                continue;

            Assert.That(DataBase<Research>.TryFind(
                chapter.RequiredResearchId, out Research research), Is.True,
                chapter.Id + " must resolve its research gate.");
            Assert.That(DataBase<Building>.TryFind(
                chapter.RequiredBuildingId, out Building building), Is.True,
                chapter.Id + " must resolve its building gate.");
            Assert.That(building.RequiredResearch, Does.Contain(research),
                chapter.Id + " must name the real research prerequisite of " +
                chapter.RequiredBuildingId + ".");
        }
    }

    [Test]
    public void WorkshopMemoryUsesTheDeclaredRealUpgrade()
    {
        StoryChapter chapter = FindChapter("workshop-memory");
        Assert.That(chapter, Is.Not.Null);
        Assert.That(chapter.RequiresWorkshopPurchase, Is.True);
        Assert.That(chapter.RequiredResearchId,
            Is.EqualTo("PrecisionManufacturing"));
        Assert.That(DataBase<WorkshopUpgrade>.TryFind(
            chapter.RequiredWorkshopId, out WorkshopUpgrade workshop), Is.True);
        Assert.That(workshop, Is.Not.Null);
        Assert.That(workshop.Id, Is.EqualTo(chapter.RequiredWorkshopId));
        Assert.That(DataBase<Research>.TryFind(
            chapter.RequiredResearchId, out Research requiredResearch), Is.True);
        Assert.That(workshop.RequiredResearch,
            Does.Contain(requiredResearch),
            "The Story chapter must name the Workshop's direct Research prerequisite.");
    }

    [Test]
    public void IndustrialMemoriesFollowTheActionArchiveOrder()
    {
        string[] expected =
        {
            "industrial-awakening",
            "workshop-memory",
            "industrial-scale",
            "industrial-organization",
            "industrial-power",
            "industrial-homes",
            "industrial-grid",
            "industrial-materials",
            "industrial-network",
            "industrial-chemistry",
            "industrial-knowledge",
            "industrial-frontier"
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

        StoryChapter workshop = FindChapter("workshop-memory");
        Assert.That(workshop.RequiredResearchId,
            Is.EqualTo("PrecisionManufacturing"));
        Assert.That(workshop.RequiredWorkshopId, Is.EqualTo("PrecisionTooling"));
        Assert.That(workshop.RequiresWorkshopPurchase, Is.True);

        StoryChapter materials = FindChapter("industrial-materials");
        Assert.That(materials.Body, Does.Contain("IntegratedFurnaces"));

        StoryChapter organization = FindChapter("industrial-organization");
        Assert.That(organization.RequiredResearchId,
            Is.EqualTo("FactoryOrganization"));
    }

    [Test]
    public void IndustrialHintExplainsEraBeforeFutureIndustrialAction()
    {
        StoryChapter power = FindChapter("industrial-power");
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

        StoryChapter scale = FindChapter("industrial-scale");
        string hint = StoryManager.GetChapterProgressHint(
            scale, TechLevel.Industrial);

        Assert.That(hint, Does.Contain("上一段工业记忆"));
        Assert.That(hint, Does.Not.Contain("PrecisionManufacturing"));
    }

    private static StoryChapter FindChapter(string id)
    {
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
            if (StoryManager.Chapters[i].Id == id)
                return StoryManager.Chapters[i];
        return null;
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name + " should exist for this test.");
        return method.Invoke(target, args);
    }

    private static int IndexOf(string id)
    {
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
            if (StoryManager.Chapters[i].Id == id)
                return i;
        return -1;
    }
}
