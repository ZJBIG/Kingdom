using System;
using UnityEngine;

internal static class RetiredDefinitionMigration
{
    private static readonly string[] RetiredIds =
    {
        "StoneTool", "MetalTool", "StoneToolWorkshop", "Blacksmith"
    };
    private static bool logged;
    private static bool legacyResourceIdsLogged;

    public static string NormalizeResourceId(string id)
    {
        if (string.Equals(id, "StoneChunk_Marble", StringComparison.Ordinal))
        {
            LogLegacyResourceIdsOnce();
            return "StoneChunk";
        }
        if (string.Equals(id, "StoneBrick_Marble", StringComparison.Ordinal))
        {
            LogLegacyResourceIdsOnce();
            return "StoneBrick";
        }
        return id;
    }

    public static string NormalizeBuildingId(string id)
    {
        if (string.Equals(
                id,
                "StoneCuttingWorkshop_Marble",
                StringComparison.Ordinal))
        {
            LogLegacyResourceIdsOnce();
            return "StoneCuttingWorkshop";
        }
        return id;
    }

    public static bool IsRetired(string id)
    {
        for (int i = 0; i < RetiredIds.Length; i++)
            if (string.Equals(id, RetiredIds[i], StringComparison.Ordinal))
                return true;
        return false;
    }

    public static void LogOnce()
    {
        if (logged)
            return;
        logged = true;
        Debug.LogWarning(
            "Save migration ignored retired definitions: " +
            "StoneTool, MetalTool, StoneToolWorkshop, Blacksmith.");
    }

    private static void LogLegacyResourceIdsOnce()
    {
        if (legacyResourceIdsLogged)
            return;
        legacyResourceIdsLogged = true;
        Debug.LogWarning(
            "Save migration remapped legacy definitions: " +
            "StoneChunk_Marble -> StoneChunk, StoneBrick_Marble -> StoneBrick, " +
            "StoneCuttingWorkshop_Marble -> StoneCuttingWorkshop.");
    }
}
