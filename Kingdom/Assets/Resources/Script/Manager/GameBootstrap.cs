using System;
using UnityEngine;

public sealed class GameBootstrap : Singleton<GameBootstrap>
{
    private bool completed;

    public bool Completed => completed;

    private void Start()
    {
        Bootstrap();
    }

    public void Bootstrap()
    {
        if (completed)
            return;

        SimulationManager.Instance.SetRunning(false);

        ValidateDefinitions();

        _ = GameManager.Instance;
        _ = ResourceManager.Instance;
        _ = BuildingManager.Instance;
        _ = ResearchManager.Instance;
        GameManager.Instance.Sectors.InitializeDefinitions();

        SaveManager.Instance.LoadOrCreateGame();
        SaveManager.Instance.SetReady(true);
        SimulationManager.Instance.SetRunning(true);
        completed = true;
    }

    private static void ValidateDefinitions()
    {
        ValidateDefinitions<Resource>();
        ValidateDefinitions<Building>();
        ValidateDefinitions<Research>();
        ValidateDefinitions<SectorDefinition>();
    }

    private static void ValidateDefinitions<T>() where T : GameDefinition
    {
        try
        {
            _ = DataBase<T>.All;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Failed to initialize {typeof(T).Name} definitions.", exception);
        }
    }
}
