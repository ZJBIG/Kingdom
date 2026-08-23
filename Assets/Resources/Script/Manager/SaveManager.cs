using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class SaveManager : Singleton<SaveManager>
{
    private const string SaveFileName = "KingdomSave.json";
    private const string TempExtension = ".tmp";
    private const string BackupExtension = ".bak";

    [SerializeField] private float autoSaveIntervalSeconds = 30f;
    [SerializeField] private float maximumOfflineHours = 24f;

    private bool ready;
    private bool dirty = true;
    private long lastSavedStateSignature;

    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    private string TempPath => SavePath + TempExtension;
    private string BackupPath => SavePath + BackupExtension;

    private void Start()
    {
        StartCoroutine(AutoSaveLoop());
    }

    public bool HasSave => File.Exists(SavePath) || File.Exists(BackupPath);

    public double LastOfflineProgressSeconds { get; private set; }

    public void SetReady(bool value)
    {
        ready = value;
        lastSavedStateSignature = CalculateStateSignature();
        dirty = value && !HasSave;
    }

    public void MarkDirty() => dirty = true;

    public bool ApplyOfflineProgress()
    {
        LastOfflineProgressSeconds = 0d;
        long savedAt = GameManager.Instance.State.LastSaveUnixSeconds;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        double maximumSeconds = Math.Max(0d, maximumOfflineHours) * 3600d;
        double elapsedSeconds = CalculateOfflineElapsedSeconds(
            savedAt,
            now,
            maximumSeconds);
        if (elapsedSeconds <= 0d)
            return false;

        LastOfflineProgressSeconds = SimulationManager.Instance.AdvanceOffline(elapsedSeconds);
        GameManager.Instance.MarkSaveTimestamp(now);
        dirty = true;
        Debug.Log($"已应用离线进度：{LastOfflineProgressSeconds:0.##} 秒。");
        return LastOfflineProgressSeconds > 0d;
    }

    public static double CalculateOfflineElapsedSeconds(
        long savedAt,
        long currentTime,
        double maximumSeconds)
    {
        if (double.IsNaN(maximumSeconds) || double.IsInfinity(maximumSeconds) || maximumSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(maximumSeconds));
        if (savedAt <= 0L || currentTime <= savedAt)
            return 0d;
        return Math.Min(currentTime - savedAt, maximumSeconds);
    }

    public bool LoadOrCreateGame()
    {
        if (TryLoadCandidate(SavePath, out KingdomSaveData saveData))
        {
            ready = true;
            dirty = false;
            lastSavedStateSignature = CalculateStateSignature();
            Debug.Log($"已载入 Kingdom 存档：{SavePath}");
            return true;
        }

        if (TryLoadCandidate(BackupPath, out saveData))
        {
            ready = true;
            dirty = false;
            lastSavedStateSignature = CalculateStateSignature();
            Debug.Log($"主存档保存失败，已从备份载入 Kingdom 存档：{BackupPath}");
            return true;
        }

        ResetRuntimeStateForLoad();
        GameManager.Instance.InitializeNewGame();
        BuildingManager.Instance.InitializeStartingBuildings();
        TutorialManager.Ensure().ResetForNewGame();
        ready = true;
        dirty = true;
        lastSavedStateSignature = CalculateStateSignature();
        Debug.Log("没有可应用的有效 Kingdom 存档，已开始新游戏。");
        return false;
    }

    public bool SaveNow(bool force = false)
    {
        if (!ready)
            return false;

        UpdateDirtyFromStateVersions();
        if (!force && !dirty)
            return true;

        try
        {
            KingdomSaveData data = CaptureSaveData();
            string json = JsonUtility.ToJson(data, true);
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(TempPath, json);
            if (File.Exists(SavePath))
                File.Copy(SavePath, BackupPath, true);
            File.Copy(TempPath, SavePath, true);
            File.Delete(TempPath);

            dirty = false;
            lastSavedStateSignature = CalculateStateSignature();
            Debug.Log($"Kingdom 数据已保存至：{SavePath}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"保存 Kingdom 数据失败：{exception}");
            return false;
        }
    }

    private IEnumerator AutoSaveLoop()
    {
        while (true)
        {
            float waitSeconds = Mathf.Clamp(autoSaveIntervalSeconds, 30f, 60f);
            yield return new WaitForSecondsRealtime(waitSeconds);
            SaveNow(false);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveNow(true);
            return;
        }

        if (ready && ApplyOfflineProgress())
            SaveNow(true);
    }

    private void OnApplicationQuit()
    {
        SaveNow(true);
    }

    private KingdomSaveData CaptureSaveData()
    {
        return new KingdomSaveData
        {
            Version = SaveFormat.CurrentVersion,
            General = GameManager.Instance.CaptureSaveData(),
            Resources = ResourceManager.Instance.CaptureSaveData(),
            Buildings = BuildingManager.Instance.CaptureSaveData(),
            Researches = ResearchManager.Instance.CaptureSaveData(),
            Workshop = WorkshopManager.Instance.CaptureSaveData(),
            Sectors = GameManager.Instance.Sectors.CaptureSaveData(),
            Tutorial = TutorialManager.Ensure().CaptureSaveData()
        };
    }

    private void ApplySaveData(KingdomSaveData data)
    {
        if (data == null)
            throw new InvalidDataException("存档 JSON 为空或无效。");
        if (data.Version != SaveFormat.CurrentVersion && data.Version != 5)
            throw new InvalidDataException("存档结构不是当前版本。");

        ResetRuntimeStateForLoad();
        GameManager.Instance.InitializeNewGame();
        BuildingManager.Instance.InitializeStartingBuildings();
        GameManager.Instance.RestoreSaveData(data.General);
        GameManager.Instance.ResetDerivedEconomy();
        ResourceManager.Instance.ResetDerivedRates();
        GameManager.Instance.InitializeStartingResources();
        ResourceManager.Instance.RestoreSaveData(data.Resources);
        BuildingManager.Instance.RestoreSaveData(data.Buildings);
        BuildingManager.Instance.RecalculateDerivedStateFromBuildings();
        GameManager.Instance.RestorePopulationChangeProgress(data.General);
        BuildingManager.Instance.RefreshEfficiencies();
        GameManager.Instance.RestoreMilitarySaveData(data.General);
        ResearchManager.Instance.RestoreSaveData(data.Researches);
        WorkshopManager.Instance.RestoreSaveData(data.Workshop);
        BuildingManager.Instance.RefreshBuildingChainAvailability();
        GameManager.Instance.Sectors.RestoreSaveData(data.Sectors);
        GameManager.Instance.Sectors.ValidateCampaignState(GameManager.Instance.State);
        TutorialManager.Ensure().RestoreSaveData(data.Tutorial, GameManager.Instance.State.TechLevel);
    }

    private bool TryLoadCandidate(string path, out KingdomSaveData data)
    {
        data = null;
        if (!TryReadPath(path, out KingdomSaveData candidate))
            return false;

        try
        {
            ApplySaveData(candidate);
            data = candidate;
            return true;
        }
        catch (Exception exception)
        {
            ResetRuntimeStateForLoad();
            GameManager.Instance.InitializeNewGame();
            BuildingManager.Instance.InitializeStartingBuildings();
            TutorialManager.Ensure().ResetForNewGame();
            Debug.LogError(
                $"应用 Kingdom 存档“{path}”失败。已在尝试下一个候选存档前重置运行时状态。" +
                $"详细信息：{exception.Message}");
            return false;
        }
    }

    private static void ResetRuntimeStateForLoad()
    {
        ResourceManager.Instance.ResetForLoad();
        BuildingManager.Instance.ResetForLoad();
        // Research reset rebuilds the shared progression modifiers. Clear
        // Workshop purchases first so a failed/corrupt load cannot leave
        // stale Workshop effects active in the subsequent new game.
        WorkshopManager.Instance.ResetForLoad();
        ResearchManager.Instance.ResetForLoad();
        GameManager.Instance.Sectors.ResetForLoad();
    }

    private static bool TryReadPath(string path, out KingdomSaveData data)
    {
        data = null;
        if (!File.Exists(path))
            return false;

        try
        {
            data = JsonUtility.FromJson<KingdomSaveData>(File.ReadAllText(path));
            if (data == null)
            {
                Debug.LogError($"Kingdom 存档“{path}”无效：JSON 未生成存档对象。");
                return false;
            }

            if (data.Version != SaveFormat.CurrentVersion && data.Version != 5)
            {
                Debug.LogError(
                    $"Kingdom 存档“{path}”无效：不支持版本“{data.Version}”，" +
                    $"当前应为“{SaveFormat.CurrentVersion}”。");
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"读取 Kingdom 存档“{path}”失败：{exception.Message}");
            data = null;
            return false;
        }
    }

    private void UpdateDirtyFromStateVersions()
    {
        long signature = CalculateStateSignature();
        if (signature != lastSavedStateSignature)
            dirty = true;
    }

    private static long CalculateStateSignature()
    {
        unchecked
        {
            long hash = 17;
            Append(ref hash, GameManager.Instance.State.Version);

            IReadOnlyDictionary<Resource, ResourceState> resources = ResourceManager.Instance.States;
            IReadOnlyList<Resource> resourceDefinitions = DataBase<Resource>.All;
            for (int i = 0; i < resourceDefinitions.Count; i++)
                if (resources.TryGetValue(resourceDefinitions[i], out ResourceState state))
                    Append(ref hash, state.Version);

            IReadOnlyDictionary<Building, BuildingState> buildings = BuildingManager.Instance.States;
            IReadOnlyList<Building> buildingDefinitions = DataBase<Building>.All;
            for (int i = 0; i < buildingDefinitions.Count; i++)
                if (buildings.TryGetValue(buildingDefinitions[i], out BuildingState state))
                    Append(ref hash, state.Version);

            IReadOnlyDictionary<Research, ResearchState> researches = ResearchManager.Instance.States;
            IReadOnlyList<Research> researchDefinitions = DataBase<Research>.All;
            for (int i = 0; i < researchDefinitions.Count; i++)
                if (researches.TryGetValue(researchDefinitions[i], out ResearchState state))
                    Append(ref hash, state.Version);

            IReadOnlyDictionary<WorkshopUpgrade, WorkshopUpgradeState> upgrades =
                WorkshopManager.Instance.States;
            IReadOnlyList<WorkshopUpgrade> upgradeDefinitions =
                DataBase<WorkshopUpgrade>.All;
            for (int i = 0; i < upgradeDefinitions.Count; i++)
                if (upgrades.TryGetValue(upgradeDefinitions[i], out WorkshopUpgradeState state))
                    Append(ref hash, state.Version);

            GameManager.Instance.Sectors.AppendStateSignature(ref hash);

            TutorialManager tutorial = TutorialManager.Current;
            if (tutorial != null)
                Append(ref hash, tutorial.Version);

            return hash;
        }
    }

    private static void Append(ref long hash, int value)
    {
        hash = hash * 31 + value;
    }

    [Serializable]
    public sealed class KingdomSaveData
    {
        public int Version;
        public GameSaveData General;
        public ResourceSaveData Resources;
        public BuildingSaveData Buildings;
        public ResearchSaveData Researches;
        public WorkshopSaveData Workshop;
        public SectorSaveData Sectors;
        public TutorialSaveData Tutorial;
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public int CalendarDays;
        public string KingdomName;
        public TechLevel TechLevel;
        public string FoodAmount;
        public string Population;
        public string PopulationChangeProgress;
        public string TerritoryTotal;
        public string AttackPower;
        public string DefensePower;
        public string FleetPower;
        public string MilitaryManpower;
        public string SupplySatisfaction;
        public string PowerSatisfaction;
        public string LogisticsSatisfaction;
        public bool CampaignActive;
        public string CampaignTargetSectorId;
        public string CampaignCasualties;
        public string CampaignCombatRatio;
        public long LastSaveUnixSeconds;
    }

    [Serializable]
    public sealed class ResourceSaveData
    {
        public string GlobalEfficiencyFactor;
        public List<ResourceStateSaveData> Resources;
    }

    [Serializable]
    public sealed class ResourceStateSaveData
    {
        public string ResourceId;
        public string Amount;
    }

    [Serializable]
    public sealed class BuildingSaveData
    {
        public string GlobalEfficiencyFactor;
        public List<BuildingStateSaveData> Buildings;
    }

    [Serializable]
    public sealed class BuildingStateSaveData
    {
        public string BuildingId;
        public string Amount;
    }

    [Serializable]
    public sealed class ResearchSaveData
    {
        public string GlobalEfficiencyFactor;
        public List<ResearchStateSaveData> States;
        public string ActiveResearchId;
        public string SelectedResearchId;
        public List<string> QueuedResearchIds;
    }

    [Serializable]
    public sealed class ResearchStateSaveData
    {
        public string ResearchId;
        public string Progress;
        public bool CostPaid;
        public bool Completed;
        public List<ResearchResourceCostSaveData> PaidResourceCosts;
    }

    [Serializable]
    public sealed class ResearchResourceCostSaveData
    {
        public string ResourceId;
        public string Amount;
    }

    [Serializable]
    public sealed class WorkshopSaveData
    {
        public List<string> PurchasedUpgradeIds;
    }

    [Serializable]
    public sealed class SectorSaveData
    {
        public List<SectorStateSaveData> States;
    }

    [Serializable]
    public sealed class TutorialSaveData
    {
        public string ActiveStepId;
        public List<string> CompletedStepIds;
    }

    [Serializable]
    public sealed class SectorStateSaveData
    {
        public string SectorId;
        public bool Unlocked;
        public bool Occupied;
        public bool ColonizationActive;
        public bool CampaignActive;
        public string CampaignProgress;
        public string CampaignCasualties;
        public string CampaignCombatRatio;
        public int VisitCount;
    }
}
