using System;
using System.Collections;
using UnityEngine;

public sealed class GameBootstrap : Singleton<GameBootstrap>
{
    private bool completed;
    private bool started;

    public bool Completed => completed;

    private void Start()
    {
        Bootstrap();
    }

    public void Bootstrap()
    {
        if (started || completed) return;
        started = true;
        SaveManager.Instance.BeginLoading("正在读取存档");
        StartCoroutine(BootstrapRoutine());
    }

    private IEnumerator BootstrapRoutine()
    {
        yield return null;
        bool settling = false;
        try
        {
            bool loadedExistingGame = InitializeGame();
            settling = loadedExistingGame && SaveManager.Instance.BeginStartupOfflineProgress();
        }
        catch (Exception exception)
        {
            SaveManager.Instance.FailLoading(exception);
        }
        if (!string.IsNullOrEmpty(SaveManager.Instance.LoadingError)) yield break;
        while (SaveManager.Instance.IsOfflineProgressRunning) yield return null;
        if (!string.IsNullOrEmpty(SaveManager.Instance.LoadingError)) yield break;
        try
        {
            SaveManager.Instance.FinishLoading();
            SaveManager.Instance.SetReady(true);
            if (settling && !SaveManager.Instance.SaveNow(true))
                throw new System.IO.IOException(SaveManager.Instance.LastSaveError);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProgressionMilestoneRecorder.Attach(gameObject, SaveManager.Instance.LastLoadCreatedNewGame);
#endif
            completed = true;
            SimulationManager.Instance.SetRunning(true);
        }
        catch (Exception exception) { SaveManager.Instance.FailLoading(exception); }
    }

    private static bool InitializeGame()
    {
        ValidateDefinitions();

        _ = GameManager.Instance;
        _ = ResourceManager.Instance;
        _ = BuildingManager.Instance;
        _ = ResearchManager.Instance;
        _ = WorkshopManager.Instance;
        UIButtonSoundManager.EnsureInitialized();
        UIButtonSoundManager.BindResearchManager(ResearchManager.Instance);
        TutorialManager.Ensure();
        GameManager.Instance.Sectors.InitializeDefinitions();

        return SaveManager.Instance.LoadOrCreateGame();
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
