using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;

public sealed class SimulationManager : Singleton<SimulationManager>
{
    private const double OfflineStepSeconds = 60d;
    private const double OfflineFullRateSeconds = 2d * 60d * 60d;
    private const double OfflineReducedRateEndSeconds = 8d * 60d * 60d;
    private const double OfflineMiddleRate = 0.60d;
    private const double OfflineLateRate = 0.25d;
    // Leave room for the population step epsilon and ExpantaNum quantization.
    private static readonly ExpantaNum PopulationBatchStepLimit = new ExpantaNum(.999999d);

    [SerializeField] private float tickIntervalSeconds = 0.1f;
    [SerializeField] private int maximumTicksPerFrame = 20;

    private double accumulatedSeconds;
    private double accumulatedAccelerationSeconds;
    private double offlineCampaignPendingCalendarSeconds;
    private double offlineCampaignPendingSimulationSeconds;
    private bool running;
    private bool resumeAfterApplicationPause;
    private bool backlogWarningLogged;
    private OfflineProgressSession offlineSession;
    private bool applicationPaused;
#if UNITY_EDITOR
    private float slowTickLogCooldown;
    private float memoryLogCooldown;
    private float perfStatsLogCooldown = 5f;
    private int perfTickSampleCount;
    private float perfTickTotalMilliseconds;
    private float perfTickMaximumMilliseconds;
    private float perfBuildingTotalMilliseconds;
    private float perfBuildingMaximumMilliseconds;
    private float perfGameTotalMilliseconds;
    private float perfGameMaximumMilliseconds;
    private float perfResourceTotalMilliseconds;
    private float perfResourceMaximumMilliseconds;
    private float perfSectorTotalMilliseconds;
    private float perfSectorMaximumMilliseconds;
    private float perfResearchTotalMilliseconds;
    private float perfResearchMaximumMilliseconds;
    private long perfTickAllocatedBytes;
    private bool perfTickAllocationCounterAvailable;
    private ProfilerRecorder gcAllocRecorder;
#endif

    public bool IsRunning => running;
    public int AccelerationMultiplier { get; private set; } = 1;

    public void SetAccelerationMultiplier(int multiplier)
    {
        GetOfflineConsumptionMultiplier(multiplier);
        AccelerationMultiplier = GameManager.Instance.State.RemainingOfflineSeconds > 0d ? multiplier : 1;
        accumulatedAccelerationSeconds = 0d;
    }

    public static int GetOfflineConsumptionMultiplier(int multiplier)
    {
        switch (multiplier)
        {
            case 1: return 0;
            case 5: return 5;
            case 20: return 25;
            case 1000: return 500;
            default: throw new ArgumentOutOfRangeException(nameof(multiplier));
        }
    }

    private void AdvanceAcceleration(double elapsedSeconds)
    {
        if (AccelerationMultiplier == 1) return;
        GameState state = GameManager.Instance.State;
        double costPerSecond = (double)GetOfflineConsumptionMultiplier(AccelerationMultiplier) / (AccelerationMultiplier - 1);
        accumulatedAccelerationSeconds = Math.Min(
            accumulatedAccelerationSeconds + elapsedSeconds * (AccelerationMultiplier - 1),
            state.RemainingOfflineSeconds / costPerSecond);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        while (accumulatedAccelerationSeconds > 0d)
        {
            double step = Math.Min(tickIntervalSeconds, accumulatedAccelerationSeconds);
            ManualTick(step);
            state.RestoreOfflineTime(Math.Max(0d, state.RemainingOfflineSeconds - step * costPerSecond));
            accumulatedAccelerationSeconds = Math.Max(0d, accumulatedAccelerationSeconds - step);
            if (state.RemainingOfflineSeconds <= 1e-9d)
            {
                state.RestoreOfflineTime(0d);
                SetAccelerationMultiplier(1);
                return;
            }
            if ((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d /
                System.Diagnostics.Stopwatch.Frequency >= 4d) return;
        }
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (!gcAllocRecorder.Valid)
            gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
    }

    private void OnDisable()
    {
        if (gcAllocRecorder.Valid)
            gcAllocRecorder.Dispose();
    }

    public double AccumulatedSecondsForEditor => accumulatedSeconds;
    public double AccumulatedAccelerationSecondsForEditor => accumulatedAccelerationSeconds;
    public long PreparedSegmentCountForEditor { get; private set; }
    public double OfflinePendingCalendarSecondsForEditor => offlineCampaignPendingCalendarSeconds;
    public double OfflinePendingSimulationSecondsForEditor => offlineCampaignPendingSimulationSeconds;
    public float TickIntervalSecondsForEditor => tickIntervalSeconds;
    public int MaximumTicksPerFrameForEditor => maximumTicksPerFrame;
    public void OnApplicationPauseForEditor(bool pauseStatus) => OnApplicationPause(pauseStatus);
#endif

    private void Update()
    {
        if (!running)
            return;

#if UNITY_EDITOR
        slowTickLogCooldown = Mathf.Max(0f, slowTickLogCooldown - Time.unscaledDeltaTime);
        memoryLogCooldown = Mathf.Max(0f, memoryLogCooldown - Time.unscaledDeltaTime);
        perfStatsLogCooldown = Mathf.Max(0f, perfStatsLogCooldown - Time.unscaledDeltaTime);
        if (memoryLogCooldown <= 0f)
        {
            memoryLogCooldown = 5f;
            long allocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            long reservedBytes = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
            long monoBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            long gcAllocBytes = gcAllocRecorder.Valid ? gcAllocRecorder.LastValue : -1L;
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] Memory {allocatedBytes / (1024f * 1024f):F1}MB " +
                $"reserved={reservedBytes / (1024f * 1024f):F1}MB " +
                $"mono={monoBytes / (1024f * 1024f):F1}MB " +
                $"gcAllocFrameKB={(gcAllocBytes < 0L ? "NA" : (gcAllocBytes / 1024L).ToString())}");
        }
#endif

        double frameDelta = Time.unscaledDeltaTime;
#if UNITY_EDITOR
        // Unity can report the entire editor compile/domain-reload pause as a
        // single frame. App suspension is handled by OnApplicationPause; for
        // an editor hitch, reset the accumulator and resume from the next real
        // frame. Player hitches must flow through Advance so their backlog is
        // processed across later frames without exceeding the per-frame budget.
        double maximumFrameDelta = tickIntervalSeconds * maximumTicksPerFrame;
        if (maximumFrameDelta > 0d && frameDelta > maximumFrameDelta)
        {
            accumulatedSeconds = 0d;
            backlogWarningLogged = false;
            return;
        }
#endif

        Advance(frameDelta);
    }

    public void SetRunning(bool value)
    {
        if (value && applicationPaused) resumeAfterApplicationPause = true;
        running = value && !applicationPaused && offlineSession == null &&
            (!SaveManager.TryGetInstance(out SaveManager saves) || !saves.IsLoading);
        if (!running)
        {
            accumulatedSeconds = 0d;
            accumulatedAccelerationSeconds = 0d;
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (applicationPaused == pauseStatus) return;
        applicationPaused = pauseStatus;
        if (pauseStatus)
        {
            resumeAfterApplicationPause = running;
            SetRunning(false);
            return;
        }

        if (resumeAfterApplicationPause)
        {
            resumeAfterApplicationPause = false;
            SetRunning(true);
        }
    }

    public void Advance(double elapsedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        double tickInterval = tickIntervalSeconds;
        if (tickInterval <= 0d)
            throw new InvalidOperationException("模拟 tick 间隔必须大于零。");
        if (maximumTicksPerFrame < 1)
            throw new InvalidOperationException("每帧最大模拟 tick 数必须至少为一。");

        int tickCount = 0;
        accumulatedSeconds += elapsedSeconds;
        while (accumulatedSeconds >= tickInterval && tickCount < maximumTicksPerFrame)
        {
            ManualTick(tickInterval);
            accumulatedSeconds -= tickInterval;
            tickCount++;
        }

        AdvanceAcceleration(elapsedSeconds);

        double maximumBacklog = tickInterval * maximumTicksPerFrame;
        if (accumulatedSeconds > maximumBacklog)
        {
            if (!backlogWarningLogged)
            {
                Debug.LogWarning("模拟积压超过单帧处理预算，将在后续帧继续结算。");
                backlogWarningLogged = true;
            }
        }
        else if (accumulatedSeconds < maximumBacklog)
        {
            backlogWarningLogged = false;
        }
    }

    public void ManualTick(double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (deltaSeconds > 0d)
            FlushOfflineCampaignRemainder();
        double remaining = deltaSeconds;
        do
        {
            GameManager game = GameManager.Instance;
            double step = Math.Min(GetIntegrationStep(remaining, game), game.State.Events.NextBoundary(true));
            ManualTickCore(step);
            GameManager.Instance.AdvanceWorldEvents(step, true);
            remaining -= step;
        } while (remaining > 0d);
    }

    private void ManualTickCore(double deltaSeconds)
    {
        double remaining = deltaSeconds;
        // A fed segment, an unfed segment, and at most one rounding-residue
        // segment. Each segment commits forward before the next is prepared.
        for (int segment = 0; segment < 3; segment++)
        {
#if UNITY_EDITOR
            float tickStart = Time.realtimeSinceStartup;
            long tickAllocatedStart = GC.GetAllocatedBytesForCurrentThread();
#endif
            BuildingManager.Instance.PrepareTickResourceSatisfaction(remaining);
            double step = segment == 2 ? remaining :
                GameManager.Instance.GetFoodSegmentSeconds(remaining);
            ManualTickPreparedSegment(step
#if UNITY_EDITOR
                , tickStart, tickAllocatedStart
#endif
                );
            remaining = Math.Max(0d, remaining - step);
            if (remaining <= 0d)
                break;
        }
    }

    private void ManualTickPreparedSegment(double deltaSeconds
#if UNITY_EDITOR
        , float tickStart, long tickAllocatedStart
#endif
        )
    {
#if UNITY_EDITOR
        PreparedSegmentCountForEditor++;
        perfTickAllocationCounterAvailable |= tickAllocatedStart > 0L;
#endif
        BuildingManager buildingManager = BuildingManager.Instance;
        ExpantaNum resourceReward = GameManager.Instance.State.HappinessRewardMultiplier;
#if UNITY_EDITOR
        float buildingEnd = Time.realtimeSinceStartup;
#endif
        // PrepareTickResourceSatisfaction already performs the final
        // efficiency convergence and refreshes ResearchPower. Repeating the
        // full building scan here only duplicated work every simulation tick.
        GameManager gameManager = GameManager.Instance;
        gameManager.Tick(
            deltaSeconds,
            buildingManager.SafePopulationDepartureAllowance);
#if UNITY_EDITOR
        float gameEnd = Time.realtimeSinceStartup;
#endif
        ResourceManager resourceManager = ResourceManager.Instance;
        SectorManager sectors = gameManager.Sectors;
        sectors.TickOccupiedResourceProduction(
            deltaSeconds,
            resourceManager);
        resourceManager.Tick(deltaSeconds, resourceReward);
#if UNITY_EDITOR
        float resourceEnd = Time.realtimeSinceStartup;
#endif
        gameManager.UltraProject.Tick(deltaSeconds);
        gameManager.Relic.Tick(deltaSeconds);
        GameState gameState = gameManager.State;
        TickSectorOperations(sectors, deltaSeconds, gameState, resourceManager);
#if UNITY_EDITOR
        float sectorEnd = Time.realtimeSinceStartup;
#endif
        ResearchManager.Instance.Tick(deltaSeconds);
        if (deltaSeconds > 0d) TutorialManager.Current?.RecordFirstSessionProductionTick();
        StoryManager.RefreshProgress(gameManager.State.TechLevel,
            TutorialManager.Current);
#if UNITY_EDITOR
        float tickEnd = Time.realtimeSinceStartup;
        perfTickAllocatedBytes += Math.Max(0L,
            GC.GetAllocatedBytesForCurrentThread() - tickAllocatedStart);
        perfTickAllocationCounterAvailable |= GC.GetAllocatedBytesForCurrentThread() > 0L;
        float tickMilliseconds = (tickEnd - tickStart) * 1000f;
        perfTickSampleCount++;
        perfTickTotalMilliseconds += tickMilliseconds;
        perfTickMaximumMilliseconds = Mathf.Max(perfTickMaximumMilliseconds, tickMilliseconds);
        float buildingMilliseconds = (buildingEnd - tickStart) * 1000f;
        float gameMilliseconds = (gameEnd - buildingEnd) * 1000f;
        float resourceMilliseconds = (resourceEnd - gameEnd) * 1000f;
        float sectorMilliseconds = (sectorEnd - resourceEnd) * 1000f;
        float researchMilliseconds = (tickEnd - sectorEnd) * 1000f;
        perfBuildingTotalMilliseconds += buildingMilliseconds;
        perfBuildingMaximumMilliseconds = Mathf.Max(perfBuildingMaximumMilliseconds, buildingMilliseconds);
        perfGameTotalMilliseconds += gameMilliseconds;
        perfGameMaximumMilliseconds = Mathf.Max(perfGameMaximumMilliseconds, gameMilliseconds);
        perfResourceTotalMilliseconds += resourceMilliseconds;
        perfResourceMaximumMilliseconds = Mathf.Max(perfResourceMaximumMilliseconds, resourceMilliseconds);
        perfSectorTotalMilliseconds += sectorMilliseconds;
        perfSectorMaximumMilliseconds = Mathf.Max(perfSectorMaximumMilliseconds, sectorMilliseconds);
        perfResearchTotalMilliseconds += researchMilliseconds;
        perfResearchMaximumMilliseconds = Mathf.Max(perfResearchMaximumMilliseconds, researchMilliseconds);
        if (perfStatsLogCooldown <= 0f)
        {
            perfStatsLogCooldown = 5f;
            float averageMilliseconds = perfTickSampleCount == 0
                ? 0f
                : perfTickTotalMilliseconds / perfTickSampleCount;
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] ManualTickStats samples={perfTickSampleCount} " +
                $"avg={averageMilliseconds:F2}ms max={perfTickMaximumMilliseconds:F2}ms " +
                $"buildingAvg={perfBuildingTotalMilliseconds / perfTickSampleCount:F2}ms buildingMax={perfBuildingMaximumMilliseconds:F2}ms " +
                $"gameAvg={perfGameTotalMilliseconds / perfTickSampleCount:F2}ms gameMax={perfGameMaximumMilliseconds:F2}ms " +
                $"resourceAvg={perfResourceTotalMilliseconds / perfTickSampleCount:F2}ms resourceMax={perfResourceMaximumMilliseconds:F2}ms " +
                $"sectorsAvg={perfSectorTotalMilliseconds / perfTickSampleCount:F2}ms sectorsMax={perfSectorMaximumMilliseconds:F2}ms " +
                $"researchAvg={perfResearchTotalMilliseconds / perfTickSampleCount:F2}ms researchMax={perfResearchMaximumMilliseconds:F2}ms " +
                $"activeBuildings={buildingManager.LastActiveBuildingCount} " +
                $"efficiencyPasses={buildingManager.LastEfficiencyPassCount}/" +
                $"{buildingManager.LastActiveBuildingCount + 1} " +
                $"researchPowerRebuilds={buildingManager.ResearchPowerRebuildCount} " +
                $"allocKB={(perfTickAllocationCounterAvailable ? (perfTickAllocatedBytes / 1024L).ToString() : "NA")}");
            perfTickSampleCount = 0;
            perfTickTotalMilliseconds = 0f;
            perfTickMaximumMilliseconds = 0f;
            perfBuildingTotalMilliseconds = 0f;
            perfBuildingMaximumMilliseconds = 0f;
            perfGameTotalMilliseconds = 0f;
            perfGameMaximumMilliseconds = 0f;
            perfResourceTotalMilliseconds = 0f;
            perfResourceMaximumMilliseconds = 0f;
            perfSectorTotalMilliseconds = 0f;
            perfSectorMaximumMilliseconds = 0f;
            perfResearchTotalMilliseconds = 0f;
            perfResearchMaximumMilliseconds = 0f;
            perfTickAllocatedBytes = 0L;
        }
        if (slowTickLogCooldown <= 0f && tickEnd - tickStart >= 0.02f)
        {
            slowTickLogCooldown = 1f;
            Debug.Log($"[KingdomPerf] ManualTick {((tickEnd - tickStart) * 1000f):F1}ms: " +
                      $"building={buildingMilliseconds:F1}ms, " +
                      $"game={gameMilliseconds:F1}ms, " +
                      $"resource={resourceMilliseconds:F1}ms, " +
                      $"sectors={sectorMilliseconds:F1}ms, " +
                      $"research={researchMilliseconds:F1}ms, " +
                      $"activeBuildings={buildingManager.LastActiveBuildingCount}, " +
                      $"efficiencyPasses={buildingManager.LastEfficiencyPassCount}");
            KingdomEditorPerfLog.Write($"[KingdomPerf] ManualTick {((tickEnd - tickStart) * 1000f):F1}ms: " +
                                       $"building={buildingMilliseconds:F1}ms, " +
                                       $"game={gameMilliseconds:F1}ms, " +
                                       $"resource={resourceMilliseconds:F1}ms, " +
                                       $"sectors={sectorMilliseconds:F1}ms, " +
                                       $"research={researchMilliseconds:F1}ms, " +
                                       $"activeBuildings={buildingManager.LastActiveBuildingCount}, " +
                                       $"efficiencyPasses={buildingManager.LastEfficiencyPassCount}");
        }
#endif
    }

    private double GetIntegrationStep(double remaining, GameManager game)
    {
        double cadence = tickIntervalSeconds;
        if (cadence <= 0d || double.IsNaN(cadence) || double.IsInfinity(cadence))
            throw new InvalidOperationException("模拟 tick 间隔必须大于零。");
        double window = Math.Min(OfflineStepSeconds, remaining);
        if (window <= cadence)
            return window;

        // Only merge an equilibrium window. Dynamic population, research and
        // continuous transactions must observe the same cadence online/offline.
        GameState state = game.State;
        bool starving = state.FoodAmount <= ExpantaNum.Zero && state.FoodNetRate < ExpantaNum.Zero;
        if ((!starving && state.Population.Population != state.Population.PopulationCapacity) ||
            state.Campaign.Active || ResearchManager.Instance.ResearchQueue.Count > 0 ||
            ResearchManager.Instance.ActiveResearch != null ||
            game.UltraProject.State.Status == UltraProjectStatus.Running ||
            game.Relic.CanSuspend)
            return cadence;
        var sectors = game.Sectors.OrderedStates;
        for (int i = 0; i < sectors.Count; i++)
            if (sectors[i].ColonizationActive || sectors[i].InteriorExplorationActive)
                return cadence;

        BuildingManager.Instance.PrepareTickResourceSatisfaction(cadence);
        starving = state.FoodAmount <= ExpantaNum.Zero && state.FoodNetRate < ExpantaNum.Zero;
        if ((!starving && state.Population.Population != state.Population.PopulationCapacity) ||
            (starving && state.Population.PopulationChangeProgress +
                game.CurrentPopulationDepartureRatePerSecond * window >= PopulationBatchStepLimit) ||
            (!starving && (state.FoodAvailability < ExpantaNum.One ||
                state.FoodAmount + state.FoodNetRate * window < ExpantaNum.Zero)) ||
            state.PowerSatisfaction < ExpantaNum.One ||
            state.LogisticsSatisfaction < ExpantaNum.One)
            return cadence;
        foreach (ResourceState resource in ResourceManager.Instance.States.Values)
            if (resource.Amount < resource.TickPotentialConsumptionRate * window)
                return cadence;
        return window;
    }

    public double AdvanceOffline(double elapsedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds == 0d) return 0d;
        bool wasRunning = running;
        double previousAccumulatedSeconds = accumulatedSeconds;
        OfflineProgressSession session = BeginOfflineProgress(elapsedSeconds);
        while (!session.Completed)
            session.Advance(double.PositiveInfinity);
        SetRunning(wasRunning);
        accumulatedSeconds = previousAccumulatedSeconds;
        return elapsedSeconds;
    }

    public OfflineProgressSession BeginOfflineProgress(double elapsedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (offlineSession != null)
            throw new InvalidOperationException("离线进度正在结算。");
        offlineSession = new OfflineProgressSession(this, elapsedSeconds);
        SetRunning(false);
        return offlineSession;
    }

    public sealed class OfflineProgressSession
    {
        private readonly SimulationManager owner;
        private readonly IEnumerator<double> steps;
        private double totalSeconds;
        private double advancedSeconds;
        public bool Completed { get; private set; }
        public double TotalSeconds => totalSeconds;
        public float Progress => totalSeconds <= 0d ? 1f : (float)Math.Min(1d, advancedSeconds / totalSeconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private double maximumStepMilliseconds;
        private double maximumSliceMilliseconds;
        private int sliceCount;
#endif

        internal OfflineProgressSession(SimulationManager owner, double seconds)
        {
            this.owner = owner;
            totalSeconds = seconds;
            steps = owner.AdvanceOfflineSteps(this);
        }

        public void Extend(double additionalSeconds)
        {
            if (double.IsNaN(additionalSeconds) || double.IsInfinity(additionalSeconds) || additionalSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(additionalSeconds));
            if (Completed) throw new InvalidOperationException("离线进度已经结算完成。");
            if (double.IsInfinity(totalSeconds + additionalSeconds))
                throw new ArgumentOutOfRangeException(nameof(additionalSeconds));
            totalSeconds += additionalSeconds;
        }

        public void Advance(double budgetMilliseconds = 4d)
        {
            if (double.IsNaN(budgetMilliseconds) || budgetMilliseconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds));
            if (Completed) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            sliceCount++;
#endif
            try
            {
                do
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    long stepStarted = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                    bool hasStep = steps.MoveNext();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    maximumStepMilliseconds = Math.Max(maximumStepMilliseconds,
                        (System.Diagnostics.Stopwatch.GetTimestamp() - stepStarted) * 1000d / System.Diagnostics.Stopwatch.Frequency);
#endif
                    if (!hasStep)
                    {
                        Completed = true;
                        advancedSeconds = totalSeconds;
                        steps.Dispose();
                        owner.offlineSession = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        maximumSliceMilliseconds = Math.Max(maximumSliceMilliseconds,
                            (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency);
                        Debug.Log($"[KingdomOffline] seconds={totalSeconds:0.###} slices={sliceCount} maxStepMs={maximumStepMilliseconds:0.###} maxSliceMs={maximumSliceMilliseconds:0.###}");
#endif
                        return;
                    }
                    advancedSeconds += steps.Current;
                } while ((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d /
                    System.Diagnostics.Stopwatch.Frequency < budgetMilliseconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                maximumSliceMilliseconds = Math.Max(maximumSliceMilliseconds,
                    (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency);
#endif
            }
            catch
            {
                steps.Dispose();
                owner.offlineSession = null;
                throw;
            }
        }
    }

    private IEnumerator<double> AdvanceOfflineSteps(OfflineProgressSession session)
    {
        double elapsed = 0d;
        while (elapsed < session.TotalSeconds)
        {
            double step = Math.Min(OfflineStepSeconds, session.TotalSeconds - elapsed);
            GameManager gameManager = GameManager.Instance;
            if (gameManager.State.Campaign.Active || HasActiveColonization(gameManager.Sectors))
            {
                using IEnumerator<double> campaignSteps = AdvanceOfflineCampaignWindow(
                    step,
                    elapsed,
                    gameManager);
                while (campaignSteps.MoveNext()) yield return campaignSteps.Current;
            }
            else
            {
                FlushOfflineCampaignRemainder();
                using IEnumerator<double> simulationSteps = TickOfflineSimulationSteps(
                    step,
                    CalculateOfflineEffectiveSeconds(elapsed, step),
                    gameManager);
                while (simulationSteps.MoveNext()) yield return simulationSteps.Current;
            }
            elapsed += step;
        }

        StoryManager.RefreshProgress(GameManager.Instance.State.TechLevel,
            TutorialManager.Current);

    }

    private static bool HasActiveColonization(SectorManager sectors)
    {
        // Colonization also pays from capped Food after production each tick.
        // A 60-second lump can exceed capacity even when the live flow is sufficient.
        var states = sectors.OrderedStates;
        for (int i = 0; i < states.Count; i++)
            if (states[i].ColonizationActive)
                return true;
        return false;
    }

    private static void TickSectorOperations(
        SectorManager sectors,
        double simulationSeconds,
        GameState gameState,
        ResourceManager resourceManager)
    {
        sectors.TickActiveColonization(
            simulationSeconds,
            gameState,
            resourceManager,
            out _);
        sectors.TickActiveInteriorExploration(simulationSeconds, gameState, resourceManager, out _);
        sectors.TickActiveCampaign(
            simulationSeconds,
            gameState,
            resourceManager,
            out _);
    }

    private void FlushOfflineCampaignRemainder()
    {
        if (offlineCampaignPendingSimulationSeconds <= 0d)
            return;

        TickOfflineSimulation(
            offlineCampaignPendingCalendarSeconds,
            offlineCampaignPendingSimulationSeconds,
            GameManager.Instance);
        offlineCampaignPendingCalendarSeconds = 0d;
        offlineCampaignPendingSimulationSeconds = 0d;
    }

    private IEnumerator<double> AdvanceOfflineCampaignWindow(
        double calendarSeconds,
        double elapsedSeconds,
        GameManager gameManager)
    {
        double effectiveTickSeconds = tickIntervalSeconds;
        if (double.IsNaN(effectiveTickSeconds) ||
            double.IsInfinity(effectiveTickSeconds) ||
            effectiveTickSeconds <= 0d)
            throw new InvalidOperationException("模拟 tick 间隔必须大于零。");

        double remainingCalendar = calendarSeconds;
        double currentElapsed = elapsedSeconds;
        while (remainingCalendar > 0d)
        {
            double rate;
            double secondsToRateBoundary;
            if (currentElapsed < OfflineFullRateSeconds)
            {
                rate = 1d;
                secondsToRateBoundary = OfflineFullRateSeconds - currentElapsed;
            }
            else if (currentElapsed < OfflineReducedRateEndSeconds)
            {
                rate = OfflineMiddleRate;
                secondsToRateBoundary = OfflineReducedRateEndSeconds - currentElapsed;
            }
            else
            {
                rate = OfflineLateRate;
                secondsToRateBoundary = double.PositiveInfinity;
            }

            double simulationUntilNextTick =
                effectiveTickSeconds - offlineCampaignPendingSimulationSeconds;
            double calendarUntilNextTick = simulationUntilNextTick / rate;
            double calendarDelta = Math.Min(
                remainingCalendar,
                Math.Min(secondsToRateBoundary, calendarUntilNextTick));
            bool completesTick = calendarDelta >= calendarUntilNextTick;
            double simulationDelta = completesTick
                ? simulationUntilNextTick
                : calendarDelta * rate;
            offlineCampaignPendingCalendarSeconds += calendarDelta;
            offlineCampaignPendingSimulationSeconds += simulationDelta;
            currentElapsed += calendarDelta;
            remainingCalendar -= calendarDelta;

            if (completesTick)
            {
                using IEnumerator<double> simulationSteps = TickOfflineSimulationSteps(
                    offlineCampaignPendingCalendarSeconds,
                    effectiveTickSeconds,
                    gameManager);
                while (simulationSteps.MoveNext()) yield return 0d;
                offlineCampaignPendingCalendarSeconds = 0d;
                offlineCampaignPendingSimulationSeconds = 0d;
            }
            yield return calendarDelta;
        }
    }

    private void TickOfflineSimulation(
        double calendarSeconds,
        double simulationSeconds,
        GameManager gameManager)
    {
        using IEnumerator<double> steps = TickOfflineSimulationSteps(calendarSeconds, simulationSeconds, gameManager);
        while (steps.MoveNext()) { }
    }

    private IEnumerator<double> TickOfflineSimulationSteps(
        double calendarSeconds,
        double simulationSeconds,
        GameManager gameManager)
    {
        double remaining = simulationSeconds;
        do
        {
            double step = Math.Min(GetIntegrationStep(remaining, gameManager), gameManager.State.Events.NextBoundary(false));
            double calendarStep = simulationSeconds > 0d ? calendarSeconds * step / simulationSeconds : calendarSeconds;
            TickOfflineSimulationCore(calendarStep, step, gameManager);
            gameManager.AdvanceWorldEvents(step, false);
            remaining -= step;
            yield return calendarStep;
        } while (remaining > 0d);
    }

    private void TickOfflineSimulationCore(
        double calendarSeconds,
        double simulationSeconds,
        GameManager gameManager)
    {
        double remaining = simulationSeconds;
        double remainingCalendar = calendarSeconds;
        for (int segment = 0; segment < 3; segment++)
        {
            BuildingManager.Instance.PrepareTickResourceSatisfaction(remaining);
            double step = segment == 2 ? remaining : gameManager.GetFoodSegmentSeconds(remaining);
            double calendarStep = remaining > 0d ? remainingCalendar * step / remaining : remainingCalendar;
            TickOfflinePreparedSegment(calendarStep, step, gameManager);
            remaining = Math.Max(0d, remaining - step);
            remainingCalendar = Math.Max(0d, remainingCalendar - calendarStep);
            if (remaining <= 0d)
                break;
        }
    }

    private void TickOfflinePreparedSegment(
        double calendarSeconds,
        double simulationSeconds,
        GameManager gameManager)
    {
#if UNITY_EDITOR
        PreparedSegmentCountForEditor++;
#endif
        BuildingManager buildingManager = BuildingManager.Instance;
        ExpantaNum resourceReward = gameManager.State.HappinessRewardMultiplier;
        gameManager.TickOffline(
            calendarSeconds,
            simulationSeconds,
            buildingManager.SafePopulationDepartureAllowance);
        ResourceManager resourceManager = ResourceManager.Instance;
        SectorManager sectors = gameManager.Sectors;
        sectors.TickOccupiedResourceProduction(simulationSeconds, resourceManager);
        resourceManager.Tick(simulationSeconds, resourceReward);
        gameManager.UltraProject.Tick(simulationSeconds);
        gameManager.Relic.Tick(simulationSeconds);
        TickSectorOperations(
            sectors,
            simulationSeconds,
            gameManager.State,
            resourceManager);
        ResearchManager researchManager = ResearchManager.Instance;
        bool previousSuppression = researchManager.SuppressCompletionAudio;
        researchManager.SuppressCompletionAudio = true;
        try
        {
            researchManager.Tick(simulationSeconds);
            if (simulationSeconds > 0d) TutorialManager.Current?.RecordFirstSessionProductionTick();
        }
        finally
        {
            researchManager.SuppressCompletionAudio = previousSuppression;
        }
    }

    public static double CalculateOfflineEffectiveSeconds(
        double elapsedSeconds,
        double requestedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (double.IsNaN(requestedSeconds) || double.IsInfinity(requestedSeconds) || requestedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(requestedSeconds));
        if (requestedSeconds <= 0d)
            return 0d;

        double fullRate = Math.Max(0d, OfflineFullRateSeconds - elapsedSeconds);
        double fullSeconds = Math.Min(requestedSeconds, fullRate);
        double remaining = requestedSeconds - fullSeconds;
        double middleRateSeconds = Math.Max(0d, OfflineReducedRateEndSeconds -
            Math.Max(elapsedSeconds, OfflineFullRateSeconds));
        double middleSeconds = Math.Min(remaining, middleRateSeconds);
        double lateSeconds = remaining - middleSeconds;
        return fullSeconds + middleSeconds * OfflineMiddleRate + lateSeconds * OfflineLateRate;
    }
}

#if UNITY_EDITOR
public static class KingdomEditorPerfLog
{
    private static string path;
    private static readonly System.Text.StringBuilder pending = new();
    private static double nextFlushTime;
    private static bool flushRequested;

    static KingdomEditorPerfLog()
    {
        UnityEditor.EditorApplication.update += FlushIfDue;
        UnityEditor.EditorApplication.quitting += Flush;
    }

    public static void Write(string message)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                path = Path.Combine(projectRoot, "Temp", "KingdomPerf.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            pending.Append(DateTime.Now.ToString("O"))
                .Append(' ')
                .Append(message)
                .Append(Environment.NewLine);
            flushRequested |= pending.Length >= 8192;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[KingdomPerf] Could not write project log: {exception.Message}");
        }
    }

    private static void FlushIfDue()
    {
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (!flushRequested && now < nextFlushTime)
            return;
        nextFlushTime = now + 0.5d;
        Flush();
    }

    private static void Flush()
    {
        if (pending.Length == 0 || string.IsNullOrEmpty(path))
            return;
        try
        {
            File.AppendAllText(path, pending.ToString());
            pending.Clear();
            flushRequested = false;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[KingdomPerf] Could not flush project log: {exception.Message}");
        }
    }
}
#endif
