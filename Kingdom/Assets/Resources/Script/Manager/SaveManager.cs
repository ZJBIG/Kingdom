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

    public void SetReady(bool value)
    {
        ready = value;
        lastSavedStateSignature = CalculateStateSignature();
        dirty = value && !HasSave;
    }

    public void MarkDirty() => dirty = true;

    public bool LoadOrCreateGame()
    {
        if (TryLoadCandidate(SavePath, out KingdomSaveData saveData))
        {
            ready = true;
            dirty = false;
            lastSavedStateSignature = CalculateStateSignature();
            Debug.Log($"Loaded Kingdom save: {SavePath}");
            return true;
        }

        if (TryLoadCandidate(BackupPath, out saveData))
        {
            ready = true;
            dirty = false;
            lastSavedStateSignature = CalculateStateSignature();
            Debug.Log($"Main save failed; loaded Kingdom save from backup: {BackupPath}");
            return true;
        }

        ResetRuntimeStateForLoad();
        GameManager.Instance.InitializeNewGame();
        ready = true;
        dirty = true;
        lastSavedStateSignature = CalculateStateSignature();
        Debug.Log("No valid Kingdom save could be applied. Started a new game.");
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
            Debug.Log($"Saved Kingdom data to: {SavePath}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save Kingdom data: {exception}");
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
            Researches = ResearchManager.Instance.CaptureSaveData()
        };
    }

    private void ApplySaveData(KingdomSaveData data)
    {
        if (data == null)
            throw new InvalidDataException("Save JSON is empty or invalid.");
        if (data.Version != SaveFormat.CurrentVersion)
            throw new InvalidDataException("Save schema is not current.");

        ResetRuntimeStateForLoad();
        GameManager.Instance.InitializeNewGame();
        GameManager.Instance.RestoreSaveData(data.General);
        GameManager.Instance.ResetDerivedEconomy();
        ResourceManager.Instance.ResetDerivedRates();
        GameManager.Instance.InitializeStartingResources();
        ResourceManager.Instance.RestoreSaveData(data.Resources);
        BuildingManager.Instance.RestoreSaveData(data.Buildings);
        BuildingManager.Instance.RecalculateDerivedStateFromBuildings();
        BuildingManager.Instance.RefreshEfficiencies();
        ResearchManager.Instance.RestoreSaveData(data.Researches);
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
            Debug.LogError(
                $"Failed to apply Kingdom save '{path}'. Runtime state was reset before the next candidate. " +
                $"Details: {exception.Message}");
            return false;
        }
    }

    private static void ResetRuntimeStateForLoad()
    {
        ResourceManager.Instance.ResetForLoad();
        BuildingManager.Instance.ResetForLoad();
        ResearchManager.Instance.ResetForLoad();
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
                Debug.LogError($"Invalid Kingdom save '{path}': JSON produced no save object.");
                return false;
            }

            if (data.Version != SaveFormat.CurrentVersion)
            {
                Debug.LogError(
                    $"Invalid Kingdom save '{path}': unsupported Version '{data.Version}', " +
                    $"expected '{SaveFormat.CurrentVersion}'.");
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to read Kingdom save '{path}': {exception.Message}");
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
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public int CalendarDays;
        public string KingdomName;
        public TechLevel TechLevel;
        public string FoodAmount;
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
        public bool AutoBuild;
        public string AutoBuildProgress;
    }

    [Serializable]
    public sealed class ResearchSaveData
    {
        public string GlobalEfficiencyFactor;
        public List<ResearchStateSaveData> States;
        public string ActiveResearchId;
        public string SelectedResearchId;
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
}
