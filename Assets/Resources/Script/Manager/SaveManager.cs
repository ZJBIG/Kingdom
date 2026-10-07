using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class SaveManager : Singleton<SaveManager>
{
    private const string SaveFileName = "KingdomSave.json";
    private const string TempExtension = ".tmp";

    [SerializeField] private float autoSaveIntervalSeconds = 30f;
    [SerializeField] private float maximumOfflineHours = 24f;

    private bool ready;
    private bool dirty = true;
    private long lastSavedStateSignature;
    private long applicationPausedAtUnixSeconds;

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
    private static string saveRootOverride;
#endif

    private string SaveRoot
    {
        get
        {
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
            if (!string.IsNullOrEmpty(saveRootOverride))
                return saveRootOverride;
#endif
            return Application.persistentDataPath;
        }
    }

    private string SavePath => Path.Combine(SaveRoot, SaveFileName);
    private string TempPath => SavePath + TempExtension;

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
    public static void SetSaveRootOverrideForTests(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Save root cannot be empty.", nameof(root));
        saveRootOverride = Path.GetFullPath(root);
    }

    public static void ClearSaveRootOverrideForTests()
    {
        saveRootOverride = null;
    }
#endif

    private void Start()
    {
        StartCoroutine(AutoSaveLoop());
    }

    public bool HasSave => File.Exists(SavePath);
    public bool LastLoadCreatedNewGame { get; private set; }

    public static void ValidateStorySaveData(KingdomSaveData data)
    {
        ValidateStorySection(data);
    }

    public double LastOfflineProgressSeconds { get; private set; }

    public void SetReady(bool value)
    {
        ready = value;
        StoryManager.RefreshProgress();
        lastSavedStateSignature = CalculateStateSignature();
        dirty = value && !HasSave;
    }

    public void MarkDirty() => dirty = true;

    public bool ApplyOfflineProgress()
    {
        long savedAt = GameManager.Instance.State.LastSaveUnixSeconds;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return ApplyOfflineProgress(savedAt, now);
    }

    private bool ApplyOfflineProgress(long savedAt, long now)
    {
        LastOfflineProgressSeconds = 0d;
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
        LastLoadCreatedNewGame = false;
        if (TryLoadSave(SavePath))
        {
            ready = true;
            dirty = false;
            lastSavedStateSignature = CalculateStateSignature();
            Debug.Log($"已载入 Kingdom 存档：{SavePath}");
            return true;
        }

        ResetRuntimeStateForLoad();
        GameManager.Instance.InitializeNewGame();
        BuildingManager.Instance.InitializeStartingBuildings();
        TutorialManager.Ensure().ResetForNewGame();
        StoryManager.ResetForNewGame();
        StoryManager.RefreshProgress();
        LastLoadCreatedNewGame = true;
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
            long saveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Stamp the serialized payload before writing, but commit the
            // runtime timestamp only after the file replacement succeeds.
            // This prevents active play time from being counted as offline
            // time on resume, while a failed save remains retryable.
            StampSaveTimestamp(data, saveTimestamp);
            string json = JsonUtility.ToJson(data, true);
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(TempPath, json);
            CommitTempSave();
            // Commit the runtime baseline only after the primary is durable.
            GameManager.Instance.MarkSaveTimestamp(saveTimestamp);

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
        HandleApplicationPause(paused, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    internal void HandleApplicationPause(bool paused, long unixSeconds)
    {
        if (unixSeconds < 0L)
            throw new ArgumentOutOfRangeException(nameof(unixSeconds));

        if (paused)
        {
            applicationPausedAtUnixSeconds = unixSeconds;
            SaveNow(true);
            return;
        }

        long pausedAt = applicationPausedAtUnixSeconds;
        applicationPausedAtUnixSeconds = 0L;
        LastOfflineProgressSeconds = 0d;
        if (ready && pausedAt > 0L && ApplyOfflineProgress(pausedAt, unixSeconds))
            SaveNow(true);
    }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
    public void HandleApplicationPauseForTests(bool paused, long unixSeconds)
    {
        HandleApplicationPause(paused, unixSeconds);
    }
#endif

    private void OnApplicationQuit()
    {
        SaveNow(true);
    }

    private void CommitTempSave()
    {
        if (File.Exists(SavePath))
            File.Replace(TempPath, SavePath, null);
        else
            File.Move(TempPath, SavePath);
    }

    public KingdomSaveData CaptureSaveData()
    {
        StoryManager.RefreshProgress();
        return new KingdomSaveData
        {
            Version = SaveFormat.CurrentVersion,
            General = GameManager.Instance.CaptureSaveData(),
            Resources = ResourceManager.Instance.CaptureSaveData(),
            Buildings = BuildingManager.Instance.CaptureSaveData(),
            Researches = ResearchManager.Instance.CaptureSaveData(),
            Workshop = WorkshopManager.Instance.CaptureSaveData(),
            Sectors = GameManager.Instance.Sectors.CaptureSaveData(),
            UltraProject = GameManager.Instance.UltraProject.CaptureSaveData(),
            Tutorial = TutorialManager.Ensure().CaptureSaveData(),
            Story = StoryManager.CaptureSaveData()
        };
    }

    private void ApplySaveData(KingdomSaveData data)
    {
        if (data == null)
            throw new InvalidDataException("存档 JSON 为空或无效。");
        if (!IsSupportedVersion(data.Version))
            throw new InvalidDataException("存档结构不是当前版本。");
        ValidateRequiredSections(data);

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
        BuildingManager.Instance.ValidateSectorBuildingState(GameManager.Instance.Sectors);
        if (data.UltraProject == null)
            GameManager.Instance.UltraProject.InitializeNew();
        else
            GameManager.Instance.UltraProject.RestoreSaveData(data.UltraProject);
        TutorialManager.Ensure().RestoreSaveData(data.Tutorial, GameManager.Instance.State.TechLevel);
        StoryManager.RestoreSaveData(data.Story);
        StoryManager.RefreshProgress();
    }

    internal static void StampSaveTimestamp(KingdomSaveData data, long unixSeconds)
    {
        if (data == null || data.General == null)
            throw new ArgumentNullException(nameof(data));
        if (unixSeconds < 0L)
            throw new ArgumentOutOfRangeException(nameof(unixSeconds));
        data.General.LastSaveUnixSeconds = unixSeconds;
    }

#if UNITY_EDITOR
    public void ApplySaveDataForEditor(KingdomSaveData data) => ApplySaveData(data);
    public static void StampSaveTimestampForEditor(KingdomSaveData data, long unixSeconds) =>
        StampSaveTimestamp(data, unixSeconds);
    public static KingdomSaveData ParseSaveDataForEditor(string json) => ParseSaveData(json);
#endif

    private bool TryLoadSave(string path)
    {
        if (!TryReadPath(path, out KingdomSaveData candidate))
            return false;

        try
        {
            ApplySaveData(candidate);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"应用 Kingdom 存档“{path}”失败，将开始新游戏。" +
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
            data = ParseSaveData(File.ReadAllText(path));
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
        StoryManager.RefreshProgress();
        long signature = CalculateStateSignature();
        if (signature != lastSavedStateSignature)
            dirty = true;
    }

    private static void ValidateRequiredSections(KingdomSaveData data)
    {
        if (data.General == null || data.Resources == null ||
            data.Buildings == null || data.Researches == null ||
            data.Workshop == null || data.Sectors == null ||
            data.Tutorial == null || data.Story == null)
        {
            throw new InvalidDataException("存档缺少当前版本的必要数据段。");
        }

        if (data.UltraProject != null)
        {
            UltraProjectManager.ValidateSaveDataForArchive(data.UltraProject);
            if (data.UltraProject.Status != UltraProjectStatus.Locked &&
                data.General.TechLevel < TechLevel.Ultra)
            {
                throw new InvalidDataException(
                    "非锁定 Ultra 工程状态不能出现在 Ultra 时代之前的存档中。");
            }
        }
        ValidateStorySection(data);
    }

    private static void ValidateRequiredSectionKeys(string json)
    {
        string[] requiredKeys =
        {
            "General", "Resources", "Buildings", "Researches",
                "Workshop", "Sectors", "Tutorial", "Story"
        };

        for (int i = 0; i < requiredKeys.Length; i++)
        {
            if (!HasTopLevelJsonMember(json, requiredKeys[i]))
                throw new InvalidDataException("存档缺少当前版本的必要数据段。");
        }
    }

    private static KingdomSaveData ParseSaveData(string json)
    {
        bool hasUltraProjectSection = HasTopLevelJsonMember(json, "UltraProject");
        bool hasExplicitNullUltraProject = IsTopLevelJsonMemberNull(json, "UltraProject");
        ValidateRequiredSectionKeys(json);
        KingdomSaveData data = JsonUtility.FromJson<KingdomSaveData>(json);
        if (data == null)
            throw new InvalidDataException("JSON 未生成存档对象。");

        // JsonUtility materializes a missing serializable object field as its
        // default instance, so preserve the wire-level distinction.
        if (!hasUltraProjectSection)
            data.UltraProject = null;
        else if (data.UltraProject == null)
            throw new InvalidDataException(
                "存档的 UltraProject 数据段存在但不是有效对象。");

        if (!IsSupportedVersion(data.Version))
            throw new InvalidDataException(
                $"存档版本“{data.Version}”不受支持，当前应为“{SaveFormat.CurrentVersion}”。");
        if (hasExplicitNullUltraProject)
            throw new InvalidDataException("存档的 UltraProject 数据段显式为空。");
        ValidateRequiredSections(data);
        return data;
    }

    private static bool HasTopLevelJsonMember(string json, string key)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = 0; i < json.Length; i++)
        {
            char current = json[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '"')
                    inString = false;
                continue;
            }

            if (current == '"')
            {
                int keyStart = i + 1;
                int keyEnd = keyStart;
                bool keyEscaped = false;
                for (; keyEnd < json.Length; keyEnd++)
                {
                    char keyCharacter = json[keyEnd];
                    if (keyEscaped)
                    {
                        keyEscaped = false;
                        continue;
                    }

                    if (keyCharacter == '\\')
                    {
                        keyEscaped = true;
                        continue;
                    }

                    if (keyCharacter == '"')
                        break;
                }

                if (keyEnd >= json.Length)
                    return false;

                int separator = keyEnd + 1;
                while (separator < json.Length && char.IsWhiteSpace(json[separator]))
                    separator++;

                if (depth == 1 && separator < json.Length && json[separator] == ':' &&
                    JsonStringEquals(json, keyStart, keyEnd - keyStart, key))
                    return true;

                i = keyEnd;
                continue;
            }

            if (current == '{')
                depth++;
            else if (current == '}')
                depth--;
        }

        return false;
    }

    private static bool IsTopLevelJsonMemberNull(string json, string key)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return false;

        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = 0; i < json.Length; i++)
        {
            char current = json[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (current == '"')
                    inString = false;
                continue;
            }

            if (current == '"')
            {
                int keyStart = i + 1;
                int keyEnd = keyStart;
                bool keyEscaped = false;
                for (; keyEnd < json.Length; keyEnd++)
                {
                    char keyCharacter = json[keyEnd];
                    if (keyEscaped)
                    {
                        keyEscaped = false;
                        continue;
                    }
                    if (keyCharacter == '\\')
                    {
                        keyEscaped = true;
                        continue;
                    }
                    if (keyCharacter == '"')
                        break;
                }
                if (keyEnd >= json.Length)
                    return false;

                int valueStart = keyEnd + 1;
                while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                    valueStart++;
                if (depth == 1 && valueStart < json.Length && json[valueStart] == ':')
                {
                    valueStart++;
                    while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                        valueStart++;
                    if (JsonStringEquals(json, keyStart, keyEnd - keyStart, key))
                    {
                        return valueStart + 4 <= json.Length &&
                            string.Equals(
                                json.Substring(valueStart, 4),
                                "null",
                                StringComparison.Ordinal);
                    }
                }

                i = keyEnd;
                continue;
            }

            if (current == '{')
                depth++;
            else if (current == '}')
                depth--;
        }

        return false;
    }

    private static bool JsonStringEquals(string json, int start, int length, string expected)
    {
        StringBuilder decoded = new StringBuilder(length);
        int end = start + length;
        for (int i = start; i < end; i++)
        {
            char current = json[i];
            if (current != '\\')
            {
                decoded.Append(current);
                continue;
            }

            if (++i >= end)
                return false;
            switch (json[i])
            {
                case '"': decoded.Append('"'); break;
                case '\\': decoded.Append('\\'); break;
                case '/': decoded.Append('/'); break;
                case 'b': decoded.Append('\b'); break;
                case 'f': decoded.Append('\f'); break;
                case 'n': decoded.Append('\n'); break;
                case 'r': decoded.Append('\r'); break;
                case 't': decoded.Append('\t'); break;
                case 'u':
                    if (i + 4 >= end || !ushort.TryParse(
                            json.Substring(i + 1, 4),
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture,
                            out ushort codeUnit))
                        return false;
                    decoded.Append((char)codeUnit);
                    i += 4;
                    break;
                default:
                    return false;
            }
        }

        return string.Equals(decoded.ToString(), expected, StringComparison.Ordinal);
    }

    private static void ValidateStorySection(KingdomSaveData data)
    {
        if (data == null)
            throw new InvalidDataException("存档对象为空。");
        if (data.Story == null || data.Story.CompletedChapterIds == null)
            throw new InvalidDataException("存档缺少剧情完成字段。");

        TechLevel era = data.General == null
            ? TechLevel.Animal : data.General.TechLevel;
        StoryManager.ValidateCompletedChapterIds(
            data.Story.CompletedChapterIds, era);
    }

    private static long CalculateStateSignature()
    {
            unchecked
            {
                long hash = 17;
                Append(ref hash, GameManager.Instance.State.Version);
                Append(ref hash, StoryManager.ProgressVersion);

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

            Append(ref hash, GameManager.Instance.UltraProject.State.Version);

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
        public UltraProjectStateSaveData UltraProject;
        public TutorialSaveData Tutorial;
        public StorySaveData Story;
    }

    [Serializable]
    public sealed class StorySaveData
    {
        public List<string> CompletedChapterIds;
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public int CalendarDays;
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
        public CampaignDoctrine CampaignDoctrine;
        // Preserve the sub-day calendar accumulator so save/load does not
        // silently discard up to one simulation day of calendar progress.
        public double CalendarElapsedSeconds;
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

    private static bool IsSupportedVersion(int version)
    {
        return version == SaveFormat.CurrentVersion;
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
