using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NUnit.Framework;

public sealed class EarlyVerticalSlicePacingTests
{
    [Test]
    public void MetalMine_DoesNotRequireItsOwnOutputs()
    {
        Building mine = DataBase<Building>.Find("MetalMine");
        Assert.That(
            mine.ResourceRequirements.Any(pair =>
                pair.First.Id == "BasicMetalOre"),
            Is.False);
        Assert.That(
            mine.ResourceRequirements.Any(pair => pair.First.Id == "BasicMetalIngot"),
            Is.False);
    }

    [Test]
    public void MetalSmelter_DoesNotRequireItsOwnOutput()
    {
        Building foundry = DataBase<Building>.Find("MetalSmelter");
        Assert.That(
            foundry.ResourceRequirements.Select(pair => pair.First.Id),
            Does.Not.Contain("BasicMetalIngot"));
    }

    [Test]
    public void IronworkingAndSteelmaking_FollowsReachableBronzeChain()
    {
        Research iron = DataBase<Research>.Find("IronworkingAndSteelmaking");
        Assert.That(
            iron.Prerequisites.Select(value => value.Id),
            Does.Contain("BronzeAlloyCrafting"));
    }

    [Test, Category("PacingDiagnostic")]
    public void NewGame_FixedMainlineTrace()
    {
        if (Environment.GetEnvironmentVariable("KINGDOM_RUN_PACING_TRACE") != "1")
            Assert.Ignore("Set KINGDOM_RUN_PACING_TRACE=1 to run the fixed-route diagnostic.");
        Assert.That(UnityEngine.Object.FindObjectOfType<GameManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<ResourceManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<BuildingManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<ResearchManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<WorkshopManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<SimulationManager>(), Is.Null);
        var objects = new List<GameObject>();
        T Create<T>() where T : Component
        {
            var host = new GameObject("MainlineTrace-" + typeof(T).Name);
            objects.Add(host);
            return host.AddComponent<T>();
        }
        try
        {
            var game = Create<GameManager>();
            var resources = Create<ResourceManager>();
            resources.InitializeForEditor();
            var buildings = Create<BuildingManager>();
            var researches = Create<ProgressionMilestoneTestResearchManager>();
            researches.EnsureInitialized();
            var simulation = Create<SimulationManager>();
            game.InitializeNewGameForEditor();
            game.Sectors.InitializeDefinitions();
            simulation.ManualTick(0d);

            // One hand-authored route, normal new-game inventory, no grants or strategy selection.
            var commands = new[]
            {
                "B:WoodHouse", "R:Agriculture", "B:Farm", "R:ControlledFire", "R:Quarry", "B:Quarry",
                "B:WoodHouse", "R:KnowledgeSharing", "B:KnowledgeCircle", "R:ClayExtraction", "B:ClayPit",
                "B:WoodHouse", "B:Farm", "R:StoneProcessing", "B:StoneCuttingWorkshop", "R:Woodworking", "B:Lumberyard",
                "R:Mathematics", "R:Calendar", "B:FiberGatheringCamp", "R:AnimalHusbandry", "R:StoneAgeSettlement",
                "B:WoodHouse", "B:Farm", "B:FiberGatheringCamp", "B:FiberGatheringCamp", "R:Mining",
                "B:MetalMine", "B:MetalMine", "B:CoalMine", "B:CoalMine", "R:CharcoalMaking", "R:Measurement",
                "R:MultiMetalSmeltingFundamentals", "B:MetalSmelter", "R:BronzeAlloyCrafting", "R:IronworkingAndSteelmaking",
                "R:TextileCraft", "B:WeavingWorkshop", "R:CeramicFiring", "B:CeramicKiln", "R:WrittenRecords",
                "R:FeudalAdministration", "B:WoodHouse", "B:Farm", "R:Steelmaking", "B:SteelForge",
                "R:MechanicalEngineering", "R:Industrialization"
            };
            int commandIndex = 0;
            bool requested = false;
            bool paymentRecorded = false;
            ExpantaNum previousPopulation = game.State.Population.Population;
            double lastMilestone = 0d;
            double longestMilestoneGap = 0d;
            void Record(double seconds, string action)
            {
                longestMilestoneGap = Math.Max(longestMilestoneGap, seconds - lastMilestone);
                lastMilestone = seconds;
                TestContext.WriteLine($"MAINLINE t={seconds:F1} action={action} wood={resources.GetAmount(DataBase<Resource>.Find("WoodLog"))} " +
                    $"population={game.State.Population.Population}/{game.State.Population.PopulationCapacity} " +
                    $"foodNet={game.State.FoodNetRate} productivity={buildings.AvailableProductivity} researchSpeed={researches.CurrentResearchSpeed}");
            }
            const double step = 0.1d; // Match the normal online cadence.
            const int steps = 3 * 864000;
            double elapsed = 0d;
            Record(0d, "new-game");
            for (int tick = 0; tick <= steps; tick++)
            {
                double seconds = tick * step;
                elapsed = seconds;
                if (game.State.Population.Population != previousPopulation)
                {
                    previousPopulation = game.State.Population.Population;
                    Record(seconds, "population");
                }
                while (commandIndex < commands.Length)
                {
                    string command = commands[commandIndex];
                    string id = command.Substring(2);
                    bool completed;
                    if (command.StartsWith("B:", StringComparison.Ordinal))
                    {
                        var definition = DataBase<Building>.Find(id);
                        completed = buildings.GetMaxBuildable(definition, ExpantaNum.One) >= ExpantaNum.One &&
                            buildings.TryBuild(definition, ExpantaNum.One, out _);
                        if (completed) Record(seconds, "built:" + id);
                    }
                    else
                    {
                        var definition = DataBase<Research>.Find(id);
                        if (!requested)
                        {
                            var result = researches.HandleResearchAction(definition);
                            Assert.That(result, Is.EqualTo(ResearchActionResult.Started).Or.EqualTo(ResearchActionResult.QueuedWaitingResources));
                            requested = true;
                            Record(seconds, "queued:" + id);
                        }
                        var state = researches.GetState(definition);
                        if (state.CostPaid && !paymentRecorded)
                        {
                            paymentRecorded = true;
                            Record(seconds, "paid:" + id);
                        }
                        completed = state.Status == ResearchStatus.Completed;
                        if (completed) Record(seconds, "completed:" + id);
                    }
                    if (!completed) break;
                    commandIndex++;
                    requested = false;
                    paymentRecorded = false;
                }
                if (tick == 6000)
                    Assert.That(commandIndex, Is.GreaterThanOrEqualTo(6), "The fixed opening must establish food and stone production without grants.");
                if (tick % 36000 == 0)
                {
                    Assert.That(resources.States.Values.All(state => state.Amount.IsFinite && state.Amount >= ExpantaNum.Zero), Is.True);
                    TestContext.WriteLine($"MAINLINE sample={seconds:F1} next={(commandIndex < commands.Length ? commands[commandIndex] : "done")} " +
                        $"food={game.State.FoodAmount} happiness={game.State.HappinessMultiplier}");
                }
                if (commandIndex >= commands.Length) break;
                if (tick < steps) simulation.ManualTick(step);
            }
            longestMilestoneGap = Math.Max(longestMilestoneGap, elapsed - lastMilestone);
            TestContext.WriteLine($"MAINLINE finishedCommands={commandIndex}/{commands.Length} longestMilestoneGap={longestMilestoneGap:F1}s");
            Assert.That(game.State.TechLevel, Is.EqualTo(TechLevel.Industrial), "Fixed mainline must reach Industrial through paid commands.");
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            ProgressionModifierManager.Rebuild(null);
        }
    }

}
