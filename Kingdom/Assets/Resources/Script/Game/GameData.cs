using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    // 元数据
    public string saveTime;
    public string gameVersion;
    public int totalPlayTimeInSeconds;

    // 资源数据 - 使用资源名称作为键
    public Dictionary<string, ResourceData> resourceData = new Dictionary<string, ResourceData>();

    // 科技数据 - 使用科技ID作为键
    public Dictionary<string, ResearchData> researchData = new Dictionary<string, ResearchData>();

    public SettingsData settingsData = new SettingsData();

    // 成就数据
    public Dictionary<string, AchievementData> achievementData = new Dictionary<string, AchievementData>();

    // 其他自定义数据...
    public Dictionary<string, string> customStringData = new Dictionary<string, string>();
    public Dictionary<string, int> customIntData = new Dictionary<string, int>();
    public Dictionary<string, float> customFloatData = new Dictionary<string, float>();
    public Dictionary<string, bool> customBoolData = new Dictionary<string, bool>();
}
[System.Serializable]
public class ResourceData
{
    public string resourceId;          // Resource ScriptableObject 的GUID或名称
    public string amount;              // BigNumber 字符串表示
    public string growthRate;          // BigNumber 增长率
    public bool isUnlocked;           // 是否已解锁
    public DateTime lastUpdateTime;   // 最后更新时间
    public Dictionary<string, string> modifiers = new Dictionary<string, string>(); // 各种修改器
}

// 科技数据类
[System.Serializable]
public class ResearchData
{
    public string researchId;         // Research ScriptableObject 的ID
    public bool isResearched;         // 是否已研究
    public bool isAvailable;          // 是否可用
    public DateTime researchStartTime; // 开始研究时间
    public float progress;            // 研究进度 0-1
    public Dictionary<string, int> prerequisites; // 前置要求
}
[Serializable]
public class SettingsData
{
    public float masterVolume = 0.8f;
    public float musicVolume = 0.7f;
    public float sfxVolume = 1.0f;
    public bool fullscreen = true;
    public int resolutionIndex = 0;
    public int qualityLevel = 2;
    public string language = "zh-CN";
    public bool autoSave = true;
    public int autoSaveInterval = 300; // 秒
    public bool showFPS = false;
}
[Serializable]
public class AchievementData
{
    public bool isUnlocked;
    public DateTime unlockTime;
    public float progress; // 用于进度型成就
}