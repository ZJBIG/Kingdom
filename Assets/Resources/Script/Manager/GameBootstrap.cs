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
        UIButtonSoundManager.EnsureInitialized();
        TutorialManager.Ensure();
        GameManager.Instance.Sectors.InitializeDefinitions();

        bool loadedExistingGame = SaveManager.Instance.LoadOrCreateGame();
        SaveManager.Instance.SetReady(true);
        if (loadedExistingGame && SaveManager.Instance.ApplyOfflineProgress())
            SaveManager.Instance.SaveNow(true);
        SimulationManager.Instance.SetRunning(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ProgressionMilestoneRecorder.Attach(gameObject, SaveManager.Instance.LastLoadCreatedNewGame);
#endif
        completed = true;
    }

    private static void ValidateDefinitions()
    {
        ValidateDefinitions<ResourceDef>();
        ValidateDefinitions<BuildingDef>();
        BuildingManager.ValidateBuildingChains(DataBase<BuildingDef>.All);
        ValidateDefinitions<ResearchDef>();
        ValidateDefinitions<WorkshopUpgradeDef>();
        ValidateDefinitions<SectorDef>();
        if (!SectorValidator.ValidateDefinitions(
            DataBase<SectorDef>.All,
                out string sectorError))
        {
            Debug.LogWarning(sectorError);
        }
        if (!EconomyDependencyValidator.Validate(
                DataBase<ResourceDef>.All,
                DataBase<BuildingDef>.All,
                DataBase<ResearchDef>.All,
                DataBase<WorkshopUpgradeDef>.All,
                out string error))
        {
            Debug.LogWarning(error);
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
