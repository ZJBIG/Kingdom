using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class EconomySnapshotTraceTests
{
    [Test]
    public void ControlledFixture_ExportsOnlineAndRepeatedOfflineWindows()
    {
        string directory = Environment.GetEnvironmentVariable("KINGDOM_PARITY_TRACE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
            Assert.Ignore("Set KINGDOM_PARITY_TRACE_DIRECTORY to export controlled v5 parity fixtures.");
        Assert.That(UnityEngine.Object.FindObjectOfType<GameManager>(), Is.Null,
            "Run in an isolated EditMode test session, never against a live game.");
        string saveRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "KingdomSnapshotTrace-" + Guid.NewGuid().ToString("N")));
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
        var created = new List<GameObject>();
        try
        {
            GameManager game = Add<GameManager>();
            ResourceManager resources = Add<ResourceManager>();
            BuildingManager buildings = Add<BuildingManager>();
            ResearchManager research = Add<ResearchManager>();
            Add<WorkshopManager>();
            Add<SaveManager>();
            SimulationManager simulation = Add<SimulationManager>();
            BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(house), ExpantaNum.One);
            game.State.RestorePopulationForEditor(ExpantaNum.One);
            simulation.ManualTick(0d);
            foreach (ResourceState state in resources.States.Values)
                resources.SetAmount(state.Definition, ExpantaNum.Zero);
            game.State.RestorePopulationChangeProgressForEditor(new ExpantaNum(.625d));
            research.HandleResearchAction(DataBase<ResearchDef>.Find("ControlledFire"));
            research.HandleResearchAction(DataBase<ResearchDef>.Find("Agriculture"));
            Directory.CreateDirectory(directory);
            var manifest = new TraceManifest();
            Capture("online-10", false, 10d);
            Capture("offline-60", true, 60d);
            Capture("offline-7260", true, 7260d);
            Capture("offline-28860", true, 28860d);
            Capture("offline-repeat-60", true, 60d);
            // Real farm definitions keep Food flow reproducible from v5, unlike an injected rate.
            BuildingDef farm = DataBase<BuildingDef>.Find("Farm");
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(farm), new ExpantaNum(2000d));
            game.State.RestoreCoreForEditor(0, TechLevel.Spacer, new ExpantaNum(500d), 0L);
            simulation.ManualTick(0d);
            SectorDef helios = DataBase<SectorDef>.Find("HeliosCore");
            game.Sectors.GetState(helios).SetUnlockedForEditor(true);
            foreach (var cost in helios.ColonizationResourceRatesPerSecond)
                resources.SetAmount(cost.First, new ExpantaNum(1000000d));
            Assert.That(game.Sectors.TryAdvanceColonization(helios, 0d, game.State, resources, out var failure),
                Is.True, $"Controlled colonization must start: {failure}");
            Capture("colonization-offline-60", true, 60d);
            Capture("colonization-online-10", false, 10d);
            // Use authored projects with controlled occupation/research and supplies, not a pacing grant.
            research.ResetForLoadForEditor();
            var planets = new[] { "AzurePool", "HeliosCore", "ThunderGate" };
            foreach (string id in planets)
            {
                SectorDef planet = DataBase<SectorDef>.Find(id);
                Occupy(planet);
                foreach (ResearchDef prerequisite in planet.InteriorExploration.RequiredResearch)
                    CompleteResearch(prerequisite);
                foreach (var rate in planet.InteriorExploration.ResourceRatesPerSecond)
                    resources.SetAmount(rate.First, new ExpantaNum(1000000d));
            }
            ProgressionModifierState previousModifiers = ProgressionModifierManager.Current;
            ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values));
            buildings.ApplyProgressionModifierChangeForEditor(previousModifiers, ProgressionModifierManager.Current);
            foreach (string id in planets)
                Assert.That(game.Sectors.TryStartInteriorExploration(DataBase<SectorDef>.Find(id), out var startFailure),
                    Is.True, startFailure.ToString());
            Capture("interior-online-10", false, 10d);
            Capture("interior-offline-60", true, 60d);
            SectorDef azure = DataBase<SectorDef>.Find("AzurePool");
            game.Sectors.PauseInteriorExploration(azure);
            ExpantaNum pausedProgress = game.Sectors.GetState(azure).InteriorExplorationProgress;
            Capture("interior-paused-offline-60", true, 60d);
            Assert.That(game.Sectors.GetState(azure).InteriorExplorationProgress, Is.EqualTo(pausedProgress),
                "A manually paused project must not advance.");
            resources.SetAmount(DataBase<ResourceDef>.Find("Electronics"), ExpantaNum.Zero);
            Capture("interior-shortage-offline-60", true, 60d);
            Assert.That(game.Sectors.GetInteriorExplorationPreview(helios).Status,
                Is.EqualTo(SectorInteriorExplorationStatus.WaitingForSupply));
            foreach (string id in planets)
            {
                SectorDef planet = DataBase<SectorDef>.Find(id);
                foreach (var rate in planet.InteriorExploration.ResourceRatesPerSecond)
                    resources.SetAmount(rate.First, new ExpantaNum(1000000d));
                game.Sectors.GetState(planet).SetInteriorExplorationForEditor(
                    ExpantaNum.One - new ExpantaNum(20d) / planet.InteriorExploration.DurationSeconds, true);
            }
            Capture("interior-completion-offline-60", true, 60d);
            foreach (string id in planets)
                Assert.That(game.Sectors.GetState(DataBase<SectorDef>.Find(id)).InteriorExplorationCompleted, Is.True);
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(manifest, true));
            Assert.That(game.State.Population.Population, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));

            void Capture(string id, bool offline, double seconds)
            {
                // v5 omits world events; short online windows and reset inactive state isolate that boundary.
                game.State.Events.RestoreForEditor(new SaveManager.WorldEventSaveData { SecondsUntilNextEvent = 900d });
                EconomyParitySnapshotExporter.ExportCurrent(Path.Combine(directory, id + ".before.json"));
                if (offline) Assert.That(simulation.AdvanceOffline(seconds), Is.EqualTo(seconds).Within(1e-8d));
                else for (int i = 0; i < 100; i++) simulation.ManualTick(.1d);
                EconomyParitySnapshotExporter.ExportCurrent(Path.Combine(directory, id + ".after.json"));
                manifest.Windows.Add(new TraceWindow { Id = id, Offline = offline, Seconds = seconds });
            }

            void CompleteResearch(ResearchDef definition)
            {
                ResearchState state = research.GetState(definition);
                if (state.Status == ResearchStatus.Completed) return;
                foreach (ResearchDef prerequisite in definition.Prerequisites) CompleteResearch(prerequisite);
                foreach (var cost in definition.ResourceRequirements) resources.AddAmount(cost.First, cost.Second);
                Assert.That(ResearchManager.TryPayResearchCost(state), Is.True);
                state.SetProgressForEditor(state.BaseCost);
                state.SetStatusForEditor(ResearchStatus.Completed);
            }

            void Occupy(SectorDef definition)
            {
                foreach (SectorDef prerequisite in definition.PrerequisiteSectors) Occupy(prerequisite);
                SectorState state = game.Sectors.GetState(definition);
                state.SetUnlockedForEditor(true);
                state.SetOccupiedForEditor(true);
                state.SetColonizationActiveForEditor(false);
                state.SetCampaignProgressForEditor(ExpantaNum.One);
            }
        }
        finally
        {
            try
            {
                for (int i = created.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(created[i]);
            }
            finally
            {
                SaveManager.ClearSaveRootOverrideForTests();
                ProgressionModifierManager.Rebuild(null);
                if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
                Assert.That(Directory.Exists(saveRoot), Is.False, "The fixture must remove its own isolated save directory.");
            }
        }

        T Add<T>() where T : Component
        {
            var item = new GameObject("ControlledParity-" + typeof(T).Name);
            created.Add(item);
            T manager = item.AddComponent<T>();
            if (manager is ResourceManager resources && resources.States.Count == 0)
                resources.InitializeForEditor();
            if (manager is ResearchManager research && research.States.Count == 0)
                research.InitializeForEditor();
            return manager;
        }
    }

    [Serializable] private sealed class TraceManifest
    {
        public string Scope = "Controlled housing/population/research, colonization and authored planetary exploration fixtures, not a new-game route. World events inactive; Ultra/relic not covered by v5.";
        public List<TraceWindow> Windows = new();
    }
    [Serializable] private sealed class TraceWindow { public string Id; public bool Offline; public double Seconds; }
}
