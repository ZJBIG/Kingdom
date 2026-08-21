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
        // Unity can report the entire editor compile/domain-reload pause as a
        // single frame. Feeding that hitch into the real-time tick loop only
        // creates a backlog warning and then discards the excess work. App
        // suspension is handled by OnApplicationPause; for an editor hitch,
        // reset the accumulator and resume from the next real frame.
        double maximumFrameDelta = tickIntervalSeconds * maximumTicksPerFrame;
        if (maximumFrameDelta > 0d && frameDelta > maximumFrameDelta)
        {
            accumulatedSeconds = 0d;
            backlogWarningLogged = false;
            return;
        }

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
            accumulatedSeconds = maximumBacklog;
            if (!backlogWarningLogged)
            {
            Debug.LogWarning("模拟积压超过每帧上限，已进行限制处理。");
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

#if UNITY_EDITOR
        float tickStart = Time.realtimeSinceStartup;
        long tickAllocatedStart = GC.GetAllocatedBytesForCurrentThread();
        perfTickAllocationCounterAvailable |= tickAllocatedStart > 0L;
#endif
        BuildingManager.Instance.PrepareTickResourceSatisfaction(deltaSeconds);
#if UNITY_EDITOR
        float buildingEnd = Time.realtimeSinceStartup;
#endif
        // PrepareTickResourceSatisfaction already performs the final
        // efficiency convergence and refreshes ResearchPower. Repeating the
        // full building scan here only duplicated work every simulation tick.
        GameManager.Instance.Tick(
            deltaSeconds,
            BuildingManager.Instance.SafePopulationDepartureAllowance);
#if UNITY_EDITOR
        float gameEnd = Time.realtimeSinceStartup;
#endif
        ResourceManager.Instance.Tick(deltaSeconds);
#if UNITY_EDITOR
        float resourceEnd = Time.realtimeSinceStartup;
#endif
        GameManager.Instance.Sectors.TickOccupiedResourceProduction(
            deltaSeconds,
            ResourceManager.Instance);
        GameManager.Instance.Sectors.TickActiveColonization(deltaSeconds, GameManager.Instance.State, ResourceManager.Instance, out _);
        GameManager.Instance.Sectors.TickActiveCampaign(
            deltaSeconds,
            GameManager.Instance.State,
            ResourceManager.Instance,
            out _);
#if UNITY_EDITOR
        float sectorEnd = Time.realtimeSinceStartup;
#endif
        ResearchManager.Instance.Tick(deltaSeconds);
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
                $"activeBuildings={BuildingManager.Instance.LastActiveBuildingCount} " +
                $"efficiencyPasses={BuildingManager.Instance.LastEfficiencyPassCount}/" +
                $"{BuildingManager.Instance.LastActiveBuildingCount + 1} " +
                $"researchPowerRebuilds={BuildingManager.Instance.ResearchPowerRebuildCount} " +
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
                      $"activeBuildings={BuildingManager.Instance.LastActiveBuildingCount}, " +
                      $"efficiencyPasses={BuildingManager.Instance.LastEfficiencyPassCount}");
            KingdomEditorPerfLog.Write($"[KingdomPerf] ManualTick {((tickEnd - tickStart) * 1000f):F1}ms: " +
                                       $"building={buildingMilliseconds:F1}ms, " +
                                       $"game={gameMilliseconds:F1}ms, " +
                                       $"resource={resourceMilliseconds:F1}ms, " +
                                       $"sectors={sectorMilliseconds:F1}ms, " +
                                       $"research={researchMilliseconds:F1}ms, " +
                                       $"activeBuildings={BuildingManager.Instance.LastActiveBuildingCount}, " +
                                       $"efficiencyPasses={BuildingManager.Instance.LastEfficiencyPassCount}");
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
            double effectiveStep = CalculateOfflineEffectiveSeconds(elapsed, step);
            BuildingManager.Instance.PrepareTickResourceSatisfaction(effectiveStep);
            GameManager.Instance.TickOffline(
                step,
                effectiveStep,
                BuildingManager.Instance.SafePopulationDepartureAllowance);
            ResourceManager.Instance.Tick(effectiveStep);
            GameManager.Instance.Sectors.TickOccupiedResourceProduction(
                effectiveStep,
                ResourceManager.Instance);
            GameManager.Instance.Sectors.TickActiveColonization(effectiveStep, GameManager.Instance.State, ResourceManager.Instance, out _);
            GameManager.Instance.Sectors.TickActiveCampaign(
                effectiveStep,
                GameManager.Instance.State,
                ResourceManager.Instance,
                out _);
            ResearchManager.Instance.TickOffline(effectiveStep);
            remaining -= step;
            elapsed += step;
            advanced += step;
        }

        return advanced;
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
