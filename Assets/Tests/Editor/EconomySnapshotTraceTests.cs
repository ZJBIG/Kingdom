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
            Building house = DataBase<Building>.Find("WoodHouse");
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(house), ExpantaNum.One);
            game.State.RestorePopulationForEditor(ExpantaNum.One);
            simulation.ManualTick(0d);
            foreach (ResourceState state in resources.States.Values)
                resources.SetAmount(state.Definition, ExpantaNum.Zero);
            game.State.RestorePopulationChangeProgressForEditor(new ExpantaNum(.625d));
            research.HandleResearchAction(DataBase<Research>.Find("ControlledFire"));
            research.HandleResearchAction(DataBase<Research>.Find("Agriculture"));
            Directory.CreateDirectory(directory);
            var manifest = new TraceManifest();
            Capture("online-10", false, 10d);
            Capture("offline-60", true, 60d);
            Capture("offline-7260", true, 7260d);
            Capture("offline-28860", true, 28860d);
            Capture("offline-repeat-60", true, 60d);
            // Real farm definitions keep Food flow reproducible from v5, unlike an injected rate.
            Building farm = DataBase<Building>.Find("Farm");
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(farm), new ExpantaNum(2000d));
            game.State.RestoreCoreForEditor(0, TechLevel.Spacer, new ExpantaNum(500d), 0L);
            simulation.ManualTick(0d);
            SectorDefinition helios = DataBase<SectorDefinition>.Find("HeliosCore");
            game.Sectors.GetState(helios).SetUnlockedForEditor(true);
            foreach (var cost in helios.ColonizationResourceRatesPerSecond)
                resources.SetAmount(cost.First, new ExpantaNum(1000000d));
            Assert.That(game.Sectors.TryAdvanceColonization(helios, 0d, game.State, resources, out var failure),
                Is.True, $"Controlled colonization must start: {failure}");
            Capture("colonization-offline-60", true, 60d);
            Capture("colonization-online-10", false, 10d);
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
        public string Scope = "Controlled housing/population/research fixture, not a new-game route. World events inactive; Ultra/relic not covered by v5.";
        public List<TraceWindow> Windows = new();
    }
    [Serializable] private sealed class TraceWindow { public string Id; public bool Offline; public double Seconds; }
}
