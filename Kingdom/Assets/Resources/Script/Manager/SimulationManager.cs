using System;
using UnityEngine;

public sealed class SimulationManager : Singleton<SimulationManager>
{
    [SerializeField] private float tickIntervalSeconds = 0.02f;
    [SerializeField] private int maximumTicksPerFrame = 1000;

    private double accumulatedSeconds;
    private bool running;
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
        GameManager.Instance.Tick(deltaSeconds);
        ResourceManager.Instance.Tick(deltaSeconds);
        ResearchManager.Instance.Tick(deltaSeconds);
        BuildingManager.Instance.AdvanceAutoBuild(deltaSeconds);
    }
}
