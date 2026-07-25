using UnityEngine;

/// <summary>
/// 所有可通过 DataBase 查询的只读游戏定义基类。
/// Id 是代码与存档使用的稳定标识；显示名称和 Unity 资产名称可以独立修改。
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
