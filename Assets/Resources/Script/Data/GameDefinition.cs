using UnityEngine;

/// <summary>
/// 所有可通过 DataBase 查询的只读游戏定义基类。
/// Id 是代码与存档使用的语义标识；资产文件名和 Unity 资产名与 Id 一致，Label 使用对应的显示名称。
/// </summary>
public abstract class GameDefinition : ScriptableObject
{
    [SerializeField, HideInInspector]
    private string id;

    public string Id => id;

#if UNITY_EDITOR
    public void SetIdForEditor(string value)=> id = value == null ? string.Empty : value.Trim();
#endif
}
