using System;
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

    [SerializeField] private float tickIntervalSeconds = 0.1f;
    [SerializeField] private int maximumTicksPerFrame = 20;

    private double accumulatedSeconds;
    private double offlineCampaignPendingCalendarSeconds;
    private double offlineCampaignPendingSimulationSeconds;
    private bool running;
    private bool resumeAfterApplicationPause;
    private bool backlogWarningLogged;
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
        running = value;
        if (!running)
            accumulatedSeconds = 0d;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
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

#if UNITY_EDITOR
        float tickStart = Time.realtimeSinceStartup;
        long tickAllocatedStart = GC.GetAllocatedBytesForCurrentThread();
        perfTickAllocationCounterAvailable |= tickAllocatedStart > 0L;
#endif
        BuildingManager buildingManager = BuildingManager.Instance;
        buildingManager.PrepareTickResourceSatisfaction(deltaSeconds);
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
        resourceManager.Tick(deltaSeconds);
#if UNITY_EDITOR
        float resourceEnd = Time.realtimeSinceStartup;
#endif
        gameManager.UltraProject.Tick(deltaSeconds);
        GameState gameState = gameManager.State;
        TickSectorOperations(sectors, deltaSeconds, gameState, resourceManager);
#if UNITY_EDITOR
        float sectorEnd = Time.realtimeSinceStartup;
#endif
        ResearchManager.Instance.Tick(deltaSeconds);
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

    public double AdvanceOffline(double elapsedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds <= 0d)
            return 0d;

        double remaining = elapsedSeconds;
        double elapsed = 0d;
        double advanced = 0d;
        while (remaining > 0d)
        {
            double step = Math.Min(OfflineStepSeconds, remaining);
            GameManager gameManager = GameManager.Instance;
            if (gameManager.State.Campaign.Active)
            {
                AdvanceOfflineCampaignWindow(
                    step,
                    elapsed,
                    gameManager,
                    ref offlineCampaignPendingCalendarSeconds,
                    ref offlineCampaignPendingSimulationSeconds);
            }
            else
            {
                if (offlineCampaignPendingSimulationSeconds > 0d)
                {
                    TickOfflineSimulation(
                        offlineCampaignPendingCalendarSeconds,
                        offlineCampaignPendingSimulationSeconds,
                        gameManager,
                        useRealtimeResearchCadence: true);
                    offlineCampaignPendingCalendarSeconds = 0d;
                    offlineCampaignPendingSimulationSeconds = 0d;
                }
                TickOfflineSimulation(
                    step,
                    CalculateOfflineEffectiveSeconds(elapsed, step),
                    gameManager);
            }
            remaining -= step;
            elapsed += step;
            advanced += step;
        }

        StoryManager.RefreshProgress(GameManager.Instance.State.TechLevel,
            TutorialManager.Current);

        return advanced;
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
            GameManager.Instance,
            useRealtimeResearchCadence: true);
        offlineCampaignPendingCalendarSeconds = 0d;
        offlineCampaignPendingSimulationSeconds = 0d;
    }

    private void AdvanceOfflineCampaignWindow(
        double calendarSeconds,
        double elapsedSeconds,
        GameManager gameManager,
        ref double pendingCalendarSeconds,
        ref double pendingSimulationSeconds)
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
                effectiveTickSeconds - pendingSimulationSeconds;
            double calendarUntilNextTick = simulationUntilNextTick / rate;
            double calendarDelta = Math.Min(
                remainingCalendar,
                Math.Min(secondsToRateBoundary, calendarUntilNextTick));
            bool completesTick = calendarDelta >= calendarUntilNextTick;
            double simulationDelta = completesTick
                ? simulationUntilNextTick
                : calendarDelta * rate;
            pendingCalendarSeconds += calendarDelta;
            pendingSimulationSeconds += simulationDelta;
            currentElapsed += calendarDelta;
            remainingCalendar -= calendarDelta;

            if (completesTick)
            {
                TickOfflineSimulation(
                    pendingCalendarSeconds,
                    effectiveTickSeconds,
                    gameManager,
                    useRealtimeResearchCadence: true);
                pendingCalendarSeconds = 0d;
                pendingSimulationSeconds = 0d;
            }
        }
    }

    private static void TickOfflineSimulation(
        double calendarSeconds,
        double simulationSeconds,
        GameManager gameManager,
        bool useRealtimeResearchCadence = false)
    {
        BuildingManager buildingManager = BuildingManager.Instance;
        buildingManager.PrepareTickResourceSatisfaction(simulationSeconds);
        gameManager.TickOffline(
            calendarSeconds,
            simulationSeconds,
            buildingManager.SafePopulationDepartureAllowance);
        ResourceManager resourceManager = ResourceManager.Instance;
        SectorManager sectors = gameManager.Sectors;
        sectors.TickOccupiedResourceProduction(simulationSeconds, resourceManager);
        resourceManager.Tick(simulationSeconds);
        gameManager.UltraProject.Tick(simulationSeconds);
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
            if (useRealtimeResearchCadence)
                researchManager.Tick(simulationSeconds);
            else
                researchManager.TickOffline(simulationSeconds);
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
