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
        _ = WorkshopManager.Instance;
        GameManager.Instance.Sectors.InitializeDefinitions();

        bool loadedExistingGame = SaveManager.Instance.LoadOrCreateGame();
        SaveManager.Instance.SetReady(true);
        if (loadedExistingGame && SaveManager.Instance.ApplyOfflineProgress())
            SaveManager.Instance.SaveNow(true);
        SimulationManager.Instance.SetRunning(true);
        completed = true;
    }

    private static void ValidateDefinitions()
    {
        ValidateDefinitions<Resource>();
        ValidateDefinitions<Building>();
        ValidateDefinitions<Research>();
        ValidateDefinitions<WorkshopUpgrade>();
        ValidateDefinitions<SectorDefinition>();
        if (!SectorValidator.ValidateDefinitions(
                DataBase<SectorDefinition>.All,
                out string sectorError))
        {
            throw new InvalidOperationException(sectorError);
        }
        if (!EconomyDependencyValidator.Validate(
                DataBase<Resource>.All,
                DataBase<Building>.All,
                DataBase<Research>.All,
                DataBase<WorkshopUpgrade>.All,
                out string error))
        {
            throw new InvalidOperationException(error);
        }
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
                $"初始化 {typeof(T).Name} 定义失败。", exception);
        }
    }
}
