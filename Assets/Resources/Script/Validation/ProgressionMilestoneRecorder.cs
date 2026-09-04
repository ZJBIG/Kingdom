#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public sealed class ProgressionMilestoneRecorder : MonoBehaviour
{
    private static ProgressionMilestoneRecorder instance;
    private readonly bool[] recorded = new bool[10];
    private readonly float[] milestoneElapsedSeconds = new float[10];
    private readonly double[,] bottleneckSeconds = new double[7, 5];
    private float sessionStartTime;
    private float lastSampleTime;
    private float nextBottleneckFlushTime;
    private bool newGame;
    private bool pacingSummaryLogged;
    private int sampledEra = -1;

    private const int ResourceWait = 0;
    private const int ResearchWait = 1;
    private const int ProductivityBlocked = 2;
    private const int PopulationBlocked = 3;
    private const int ActionAvailable = 4;

    public static void Attach(GameObject host, bool startedNewGame)
    {
        if (host == null)
            return;
        ProgressionMilestoneRecorder recorder = host.GetComponent<ProgressionMilestoneRecorder>();
        if (recorder == null)
            recorder = host.AddComponent<ProgressionMilestoneRecorder>();
        recorder.Initialize(startedNewGame);
    }

    private void Initialize(bool startedNewGame)
    {
        instance = this;
        sessionStartTime = Time.realtimeSinceStartup;
        lastSampleTime = sessionStartTime;
        nextBottleneckFlushTime = sessionStartTime + 60f;
        newGame = startedNewGame;
        pacingSummaryLogged = false;
        for (int i = 0; i < milestoneElapsedSeconds.Length; i++)
        {
            recorded[i] = false;
            milestoneElapsedSeconds[i] = -1f;
        }
        for (int era = 0; era < bottleneckSeconds.GetLength(0); era++)
            for (int category = 0; category < bottleneckSeconds.GetLength(1); category++)
                bottleneckSeconds[era, category] = 0d;
        sampledEra = -1;
        if (newGame)
            Record("NewGameStarted");
    }

    public static void NotifyResourceDetailViewed()
    {
        if (instance != null && instance.newGame)
            instance.Record("FirstResourceDetailViewed");
    }

    private void Update()
    {
        // The recorder is a first-ten-minute new-game diagnostic. An existing
        // save may already contain every milestone, so sampling it would
        // produce misleading "first" timings.
        if (!newGame)
            return;

        GameManager game = GameManager.Instance;
        ResourceManager resources = ResourceManager.Instance;
        BuildingManager buildings = BuildingManager.Instance;
        ResearchManager research = ResearchManager.Instance;
        if (game == null || resources == null || buildings == null || research == null ||
            game.State == null || game.State.Population == null)
            return;

        if (HasBuilding(buildings))
            Record("FirstBuildingBuilt");
        if (game.State.Population.Population > ExpantaNum.Zero)
            Record("FirstPopulation");
        if (research.ActiveResearch != null || research.ResearchQueue.Count > 0)
            Record("FirstResearchStarted");
        if (research.TotalFinishedResearchCount > 0)
            Record("FirstResearchCompleted");
        if (TutorialManager.HasOwnedProductionChain(buildings.States.Values))
            Record("FirstProductionChain");
        if (game.State.TechLevel >= TechLevel.StoneAge)
            Record("StoneAgeReached");
        if (game.State.TechLevel >= TechLevel.Medieval)
            Record("MedievalReached");
        if (game.State.TechLevel >= TechLevel.Industrial)
            Record("IndustrialReached");

        SampleBottleneck(game, resources, buildings, research);
        if (!pacingSummaryLogged && Time.realtimeSinceStartup - sessionStartTime >= 600f)
        {
            pacingSummaryLogged = true;
            LogPacingSummary();
        }
    }

    private void OnDestroy()
    {
        FlushBottleneckSample();
        if (instance == this)
            instance = null;
    }

    private static bool HasBuilding(BuildingManager buildings)
    {
        foreach (BuildingState state in buildings.States.Values)
            if (state != null && state.Amount > ExpantaNum.Zero)
                return true;
        return false;
    }

    private void Record(string milestone)
    {
        int index = milestone switch
        {
            "NewGameStarted" => 0,
            "FirstResourceDetailViewed" => 1,
            "FirstBuildingBuilt" => 2,
            "FirstPopulation" => 3,
            "FirstResearchStarted" => 4,
            "FirstResearchCompleted" => 5,
            "FirstProductionChain" => 6,
            "StoneAgeReached" => 7,
            "MedievalReached" => 8,
            "IndustrialReached" => 9,
            _ => -1
        };
        if (index < 0 || recorded[index])
            return;
        recorded[index] = true;
        milestoneElapsedSeconds[index] = Time.realtimeSinceStartup - sessionStartTime;
        GameManager game = GameManager.Instance;
        ResourceManager resources = ResourceManager.Instance;
        BuildingManager buildings = BuildingManager.Instance;
        ResearchManager research = ResearchManager.Instance;
        Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
        ResourceState woodState = wood == null ? null : resources.GetState(wood);
        string woodAmount = woodState == null ? "0" : woodState.Amount.ToGameString();
        string woodNet = woodState == null
            ? "0"
            : (ResourceManager.ApplyCurrentProductionReward(woodState.ProductionRate) -
               woodState.ConsumptionRate).ToGameString();
        Debug.Log(
            $"[KingdomMilestone] {milestone} elapsed={Time.realtimeSinceStartup - sessionStartTime:0.00}s " +
            $"days={game.State.CalendarDays} population={game.State.Population.Population.ToGameString()} " +
            $"food={game.State.FoodAmount.ToGameString()} foodNet={game.State.FoodNetRate.ToGameString()} " +
            $"wood={woodAmount} woodNet={woodNet} " +
            $"productivity={buildings.UsedProductivity.ToGameString()}/{buildings.TotalProductivity.ToGameString()} " +
            $"researchPower={research.ResearchPower.ToGameString()} era={game.State.TechLevel}");
    }

    private void LogPacingSummary()
    {
        string[] names =
        {
            "FirstResourceDetailViewed", "FirstBuildingBuilt", "FirstPopulation",
            "FirstResearchStarted", "FirstResearchCompleted", "FirstProductionChain"
        };
        float[] targets = { 20f, 30f, 120f, 150f, 240f, 600f };
        Debug.Log("[KingdomPacing] 10-minute target summary begins.");
        for (int i = 0; i < names.Length; i++)
        {
            int milestoneIndex = i + 1;
            float elapsed = milestoneElapsedSeconds[milestoneIndex];
            string result = elapsed < 0f
                ? "missing"
                : (elapsed <= targets[i] ? "ok " : "late ") +
                  elapsed.ToString("0.00") + "s (target <= " + targets[i].ToString("0") + "s)";
            Debug.Log("[KingdomPacing] " + names[i] + "=" + result);
        }
    }

    private void SampleBottleneck(
        GameManager game,
        ResourceManager resources,
        BuildingManager buildings,
        ResearchManager research)
    {
        float now = Time.realtimeSinceStartup;
        double elapsed = now - lastSampleTime;
        if (elapsed <= 0f)
            return;
        lastSampleTime = now;

        int era = Mathf.Clamp((int)game.State.TechLevel, 0, bottleneckSeconds.GetLength(0) - 1);
        if (sampledEra >= 0 && sampledEra != era)
            FlushBottleneckSample();
        sampledEra = era;
        bottleneckSeconds[era, ClassifyBottleneck(game, resources, buildings, research)] += elapsed;

        if (now >= nextBottleneckFlushTime)
        {
            FlushBottleneckSample();
            do
                nextBottleneckFlushTime += 60f;
            while (nextBottleneckFlushTime <= now);
        }
    }

    private static int ClassifyBottleneck(
        GameManager game,
        ResourceManager resources,
        BuildingManager buildings,
        ResearchManager research)
    {
        if (game.State.FoodNetRate < ExpantaNum.Zero || HasResourceShortage(resources))
            return ResourceWait;
        if (research.ActiveResearch != null &&
            (!research.ActiveResearch.CostPaid ||
             research.ActiveResearch.Status == ResearchStatus.WaitingResources ||
             research.ResearchPower <= ExpantaNum.Zero))
            return ResearchWait;
        if (buildings.AvailableProductivity <= ExpantaNum.Zero)
            return ProductivityBlocked;
        if (game.State.Population.PopulationCapacity > ExpantaNum.Zero &&
            game.State.Population.Population >= game.State.Population.PopulationCapacity)
            return PopulationBlocked;
        return ActionAvailable;
    }

    private static bool HasResourceShortage(ResourceManager resources)
    {
        if (resources == null)
            return false;

        foreach (ResourceState state in resources.States.Values)
        {
            if (state == null || state.Definition == null ||
                state.Amount > ExpantaNum.Zero || state.ConsumptionRate <= ExpantaNum.Zero)
                continue;

            ExpantaNum production = ResourceManager.ApplyCurrentProductionReward(
                state.ProductionRate);
            if (production < state.ConsumptionRate)
                return true;
        }

        return false;
    }

    private void FlushBottleneckSample()
    {
        if (sampledEra < 0)
            return;
        double total = 0d;
        for (int i = 0; i < bottleneckSeconds.GetLength(1); i++)
            total += bottleneckSeconds[sampledEra, i];
        if (total <= 0d)
            return;

        Debug.Log(
            $"[KingdomPacing] era={(TechLevel)sampledEra} samples={total:0.0}s " +
            $"resourceWait={bottleneckSeconds[sampledEra, ResourceWait] / total:P1} " +
            $"researchWait={bottleneckSeconds[sampledEra, ResearchWait] / total:P1} " +
            $"productivityBlocked={bottleneckSeconds[sampledEra, ProductivityBlocked] / total:P1} " +
            $"populationBlocked={bottleneckSeconds[sampledEra, PopulationBlocked] / total:P1} " +
            $"actionAvailable={bottleneckSeconds[sampledEra, ActionAvailable] / total:P1}");
        for (int i = 0; i < bottleneckSeconds.GetLength(1); i++)
            bottleneckSeconds[sampledEra, i] = 0d;
    }
}
#endif
