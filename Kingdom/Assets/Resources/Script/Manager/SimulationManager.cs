using System;
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

    public bool IsRunning => running;

    private void Update()
    {
        if (!running)
            return;

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
        if (elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        double tickInterval = tickIntervalSeconds;
        if (tickInterval <= 0d)
            throw new InvalidOperationException("Simulation tick interval must be greater than zero.");
        if (maximumTicksPerFrame < 1)
            throw new InvalidOperationException("Maximum simulation ticks per frame must be at least one.");

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
                Debug.LogWarning("Simulation backlog exceeded the per-frame limit and was clamped.");
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
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        BuildingManager.Instance.PrepareTickResourceSatisfaction(deltaSeconds);
        BuildingManager.Instance.RefreshEfficiencies();
        GameManager.Instance.Tick(
            deltaSeconds,
            BuildingManager.Instance.SafePopulationDepartureAllowance);
        ResourceManager.Instance.Tick(deltaSeconds);
        GameManager.Instance.Sectors.TickActiveColonization(deltaSeconds, GameManager.Instance.State, ResourceManager.Instance, out _);
        GameManager.Instance.Sectors.TickActiveCampaign(
            deltaSeconds,
            GameManager.Instance.State,
            ResourceManager.Instance,
            out _);
        ResearchManager.Instance.Tick(deltaSeconds);
    }

    public double AdvanceOffline(double elapsedSeconds)
    {
        if (elapsedSeconds < 0d)
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
            BuildingManager.Instance.RefreshEfficiencies();
            GameManager.Instance.TickOffline(
                step,
                effectiveStep,
                BuildingManager.Instance.SafePopulationDepartureAllowance);
            ResourceManager.Instance.Tick(effectiveStep);
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
        if (elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (requestedSeconds < 0d)
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
