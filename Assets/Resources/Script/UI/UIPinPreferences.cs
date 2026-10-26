using System;
using UnityEngine;

/// <summary>Local presentation preferences, separate from the gameplay save.</summary>
public static class UIPinPreferences
{
    public const string Resource = "resource";
    public const string Building = "building";
    public const string Sector = "sector";
    public const string Research = "research";

    public static bool IsPinned(string kind, string id) =>
        PlayerPrefs.GetInt(Key(kind, id), 0) != 0;

    public static void SetPinned(string kind, string id, bool pinned)
    {
        string key = Key(kind, id);
        if (pinned) PlayerPrefs.SetInt(key, 1);
        else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    public static int Compare(string kind, string leftId, string rightId) =>
        IsPinned(kind, rightId).CompareTo(IsPinned(kind, leftId));

    private static string Key(string kind, string id)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Pin preferences require a category and stable definition ID.");
        return "Kingdom.UI.Pin." + kind.Length + ":" + kind + ":" + id;
    }
}
