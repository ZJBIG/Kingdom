using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    // 保存文件路径
    private static readonly string SaveFolder = Path.Combine(Application.persistentDataPath, "Saves");
    private static readonly string SaveFilePath = Path.Combine(SaveFolder, "game_data.json");

    // 当前游戏数据
    private static GameData _currentGameData;

    // 初始化
    static SaveSystem()
    {
        Debug.Log($"存档路径: {SaveFolder}");
        Directory.CreateDirectory(SaveFolder);
        Load();
    }

    // 保存数据
    public static void Save()
    {
        if (_currentGameData == null)
            _currentGameData = new GameData();

        _currentGameData.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _currentGameData.gameVersion = Application.version;

        try
        {
            string json = JsonUtility.ToJson(_currentGameData, true);
            File.WriteAllText(SaveFilePath, json);
            Debug.Log("游戏数据已保存");
        }
        catch (Exception e)
        {
            Debug.LogError($"保存失败: {e.Message}");
        }
    }

    // 加载数据
    public static void Load()
    {
        if (File.Exists(SaveFilePath))
        {
            try
            {
                string json = File.ReadAllText(SaveFilePath);
                _currentGameData = JsonUtility.FromJson<GameData>(json);
                Debug.Log("游戏数据已加载");
            }
            catch (Exception e)
            {
                Debug.LogError($"加载失败: {e.Message}");
                _currentGameData = new GameData();
            }
        }
        else
        {
            _currentGameData = new GameData();
            Debug.Log("创建新存档");
        }
    }

    // 获取数据容器
    public static GameData GetGameData() => _currentGameData;

    // 删除存档
    public static void DeleteSave()
    {
        if (File.Exists(SaveFilePath))
        {
            File.Delete(SaveFilePath);
            _currentGameData = new GameData();
            Debug.Log("存档已删除");
        }
    }

    // 备份存档
    public static void Backup()
    {
        if (File.Exists(SaveFilePath))
        {
            string backupPath = Path.Combine(SaveFolder, $"backup_{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Copy(SaveFilePath, backupPath, true);
            Debug.Log($"备份已创建: {backupPath}");
        }
    }
}