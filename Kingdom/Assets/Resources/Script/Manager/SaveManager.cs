// DataManager.cs - 管理所有游戏数据的加载和保存
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SaveManager : Singleton<SaveManager>
{

    [Header("保存设置")]
    [SerializeField] private float autoSaveInterval = 60f;
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private bool saveOnApplicationPause = true;

    private float _autoSaveTimer;
    private bool _isInitialized;

    // 当前游戏数据（只读）
    public GameData CurrentData => SaveSystem.GetGameData();


    void Start()
    {
        InitializeData();
        ApplyLoadedData();

        // 开始自动保存协程
        if (enableAutoSave)
            StartCoroutine(AutoSaveRoutine());
    }

    void Update()
    {
        // 实时更新游戏时间
        if (CurrentData != null)
        {
            CurrentData.totalPlayTimeInSeconds = (int)Time.realtimeSinceStartup;
        }

        // 定时自动保存
        if (enableAutoSave)
        {
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= autoSaveInterval)
            {
                SaveAllData();
                _autoSaveTimer = 0f;
            }
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && saveOnApplicationPause)
        {
            SaveAllData();
            Debug.Log("应用暂停，自动保存");
        }
    }

    void OnApplicationQuit()
    {
        SaveAllData();
        Debug.Log("应用退出，自动保存");
    }

    // 初始化数据
    private void InitializeData()
    {
        if (_isInitialized) return;

        // 初始化默认数据（如果没有）
        if (CurrentData.resourceData.Count == 0)
        {
            //InitializeDefaultResourceData();
        }

        if (CurrentData.researchData.Count == 0)
        {
            InitializeDefaultResearchData();
        }



        _isInitialized = true;
    }

    // 应用加载的数据
    private void ApplyLoadedData()
    {
        // 1. 应用资源数据
        //ApplyResourceData();

        // 2. 应用科技数据
        ApplyResearchData();

        // 3. 应用设置数据
        ApplySettingsData();

        Debug.Log("已应用所有加载的数据");
    }

    // ============ 资源数据操作 ============
    //private void InitializeDefaultResourceData()
    //{
    //    // 从ResourceManager获取所有资源
    //    if (ResearchManager.Instance.Displayers != null)
    //    {
    //        foreach (var kvp in ResearchManager.Instance.Displayers)
    //        {
    //            if (kvp.Key != null)
    //            {
    //                string resourceId = GetResourceId(kvp.Key);

    //                if (!CurrentData.resourceData.ContainsKey(resourceId))
    //                {
    //                    CurrentData.resourceData[resourceId] = new ResourceData
    //                    {
    //                        resourceId = resourceId,
    //                        amount = "0",
    //                        growthRate = "0",
    //                        isUnlocked = false
    //                    };
    //                }
    //            }
    //        }
    //    }
    //}

    //private void ApplyResourceData()
    //{
    //    if (resourceManager == null || resourceManager.Displayers == null) return;

    //    foreach (var kvp in resourceManager.Displayers)
    //    {
    //        var resource = kvp.Key;
    //        var displayer = kvp.Value;

    //        if (resource != null && displayer != null)
    //        {
    //            string resourceId = GetResourceId(resource);

    //            if (CurrentData.resourceData.TryGetValue(resourceId, out var savedData))
    //            {
    //                // 应用保存的数据到显示器
    //                ApplyResourceToDisplayer(displayer, savedData);
    //            }
    //        }
    //    }
    //}

    //private void ApplyResourceToDisplayer(ResourceDisplayer displayer, ResourceData data)
    //{
    //    try
    //    {
    //        // 假设 BigNumber 有 Parse 方法
    //        // displayer.ResourceAmount = BigNumber.Parse(data.amount);
    //        // displayer.ResourceGrowthRate = BigNumber.Parse(data.growthRate);

    //        // 临时：使用字符串赋值，然后由 ResourceDisplayer 自行解析
    //        Debug.Log($"应用资源数据: {data.resourceId}, 数量: {data.amount}");

    //        // 如果 ResourceDisplayer 有对应方法
    //        // displayer.LoadFromData(data);
    //    }
    //    catch (Exception e)
    //    {
    //        Debug.LogError($"应用资源数据失败: {e.Message}");
    //    }
    //}

    //public void UpdateResourceData(Resource resource, ResourceDisplayer displayer)
    //{
    //    if (resource == null || displayer == null) return;

    //    string resourceId = GetResourceId(resource);

    //    var resourceData = new ResourceData
    //    {
    //        resourceId = resourceId,
    //        amount = displayer.ResourceAmount?.ToString() ?? "0",
    //        growthRate = displayer.ResourceGrowthRate?.ToString() ?? "0",
    //        isUnlocked = displayer.ShouldDisplay,
    //        lastUpdateTime = DateTime.Now
    //    };

    //    CurrentData.resourceData[resourceId] = resourceData;

    //    // 标记需要保存
    //    if (enableAutoSave)
    //        _autoSaveTimer = autoSaveInterval - 5f; // 5秒后自动保存
    //}

    // ============ 科技数据操作 ============
    private void InitializeDefaultResearchData()
    {
        // 加载所有科技
        var researches = Resources.LoadAll<Research>("Datas/Research");

        foreach (var research in researches)
        {
            if (research != null)
            {
                string researchId = GetResearchId(research);

                if (!CurrentData.researchData.ContainsKey(researchId))
                {
                    CurrentData.researchData[researchId] = new ResearchData
                    {
                        researchId = researchId,
                        isResearched = false,
                        isAvailable = false,
                        progress = 0f
                    };
                }
            }
        }
    }

    private void ApplyResearchData()
    {
        if (ResearchManager.Instance == null) return;

        // 这里需要根据你的 ResearchManager 结构进行调整
        // 例如：researchManager.SetResearchProgress(researchId, progress);
    }

    public void UpdateResearchData(Research research, bool isResearched, float progress = 0f)
    {
        string researchId = GetResearchId(research);

        if (CurrentData.researchData.TryGetValue(researchId, out var data))
        {
            data.isResearched = isResearched;
            data.progress = progress;
        }
        else
        {
            CurrentData.researchData[researchId] = new ResearchData
            {
                researchId = researchId,
                isResearched = isResearched,
                progress = progress
            };
        }
    }

    // ============ 设置数据操作 ============
    private void ApplySettingsData()
    {
        var settings = CurrentData.settingsData;

        // 应用音频设置
        AudioListener.volume = settings.masterVolume;

        // 应用画面设置
        QualitySettings.SetQualityLevel(settings.qualityLevel);
        Screen.fullScreen = settings.fullscreen;

        Debug.Log("应用游戏设置");
    }

    // ============ 通用数据操作 ============
    public void SetCustomString(string key, string value)
    {
        CurrentData.customStringData[key] = value;
    }

    public string GetCustomString(string key, string defaultValue = "")
    {
        return CurrentData.customStringData.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public void SetCustomInt(string key, int value)
    {
        CurrentData.customIntData[key] = value;
    }

    public int GetCustomInt(string key, int defaultValue = 0)
    {
        return CurrentData.customIntData.TryGetValue(key, out var value) ? value : defaultValue;
    }

    // ============ 保存操作 ============
    public void SaveAllData()
    {
        // 更新所有资源数据
        //UpdateAllResourceData();

        // 保存到文件
        SaveSystem.Save();

        Debug.Log("所有数据已保存");
    }

    //private void UpdateAllResourceData()
    //{
    //    if (resourceManager != null && resourceManager.Displayers != null)
    //    {
    //        foreach (var kvp in resourceManager.Displayers)
    //        {
    //            UpdateResourceData(kvp.Key, kvp.Value);
    //        }
    //    }
    //}

    private System.Collections.IEnumerator AutoSaveRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(autoSaveInterval);
            SaveAllData();
        }
    }

    // ============ 工具方法 ============

    private string GetResearchId(Research research)=>$"{research.name}_{research.GetInstanceID()}";

    // ============ 公共接口 ============
    public void QuickSave()
    {
        SaveAllData();
        Debug.Log("快速保存完成");
    }

    public void QuickLoad()
    {
        SaveSystem.Load();
        ApplyLoadedData();
        Debug.Log("快速加载完成");
    }

    public string GetSaveInfo()
    {
        var data = CurrentData;
        return $"存档时间: {data.saveTime}\n" +
               $"游戏版本: {data.gameVersion}\n" +
               $"资源数量: {data.resourceData.Count}\n" +
               $"科技数量: {data.researchData.Count}\n" +
               $"游戏时间: {FormatTime(data.totalPlayTimeInSeconds)}";
    }

    private string FormatTime(int seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        return $"{time.Days}d {time.Hours}h {time.Minutes}m {time.Seconds}s";
    }
}