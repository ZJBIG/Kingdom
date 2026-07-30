using System;
using UnityEngine;

public sealed class SimulationManager : Singleton<SimulationManager>
{
    private const double OfflineStepSeconds = 60d;

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

        Advance(Time.unscaledDeltaTime);
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
        ResearchManager.Instance.Tick(deltaSeconds);
    }

    public double AdvanceOffline(double elapsedSeconds)
    {
        if (elapsedSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds <= 0d)
            return 0d;

        double remaining = elapsedSeconds;
        double advanced = 0d;
        while (remaining > 0d)
        {
            double step = Math.Min(OfflineStepSeconds, remaining);
            BuildingManager.Instance.PrepareTickResourceSatisfaction(step);
            BuildingManager.Instance.RefreshEfficiencies();
            GameManager.Instance.Tick(
                step,
                BuildingManager.Instance.SafePopulationDepartureAllowance);
            ResourceManager.Instance.Tick(step);
            ResearchManager.Instance.TickOffline(step);
            remaining -= step;
            advanced += step;
        }

        return advanced;
    }
}
