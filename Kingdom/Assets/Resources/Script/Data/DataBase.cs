using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// 从 Resources/Datas 一次性加载并索引某一种游戏定义。
/// 常用入口：DataBase&lt;Resource&gt;.Find("Gold")。
/// </summary>
public static class DataBase<T> where T : GameDefinition
{
    private const string ResourcesPath = "Datas";

    private static Dictionary<string, T> definitionsById;
    private static ReadOnlyCollection<T> allDefinitions;

    public static IReadOnlyList<T> All
    {
        get
        {
            EnsureInitialized();
            return allDefinitions;
        }
    }

    public static int Count
    {
        get
        {
            EnsureInitialized();
            return definitionsById.Count;
        }
    }

    public static T Find(string id)
    {
        if (TryFind(id, out T definition))
            return definition;

        throw new KeyNotFoundException(
            $"{typeof(T).Name} definition with Id '{id ?? "<null>"}' was not found in Resources/{ResourcesPath}.");
    }

    public static bool TryFind(string id, out T definition)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null;
            return false;
        }

        return definitionsById.TryGetValue(id.Trim(), out definition);
    }

    public static bool Contains(string id) => TryFind(id, out _);

    private static void EnsureInitialized()
    {
        if (definitionsById != null)
            return;

        T[] loaded = Resources.LoadAll<T>(ResourcesPath);
        Array.Sort(loaded, CompareById);

        var index = new Dictionary<string, T>(loaded.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < loaded.Length; i++)
        {
            T definition = loaded[i];
            if (definition == null)
                continue;

            string id = definition.Id == null ? string.Empty : definition.Id.Trim();
            if (id.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} asset '{definition.name}' has an empty stable Id.");
            }

            if (index.TryGetValue(id, out T existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate {typeof(T).Name} Id '{id}' on assets '{existing.name}' and '{definition.name}'.");
            }

            index.Add(id, definition);
        }

        definitionsById = index;
        allDefinitions = Array.AsReadOnly(loaded);
    }

    private static int CompareById(T left, T right)
    {
        string leftId = left == null ? string.Empty : left.Id;
        string rightId = right == null ? string.Empty : right.Id;
        return string.Compare(leftId, rightId, StringComparison.OrdinalIgnoreCase);
    }
}
