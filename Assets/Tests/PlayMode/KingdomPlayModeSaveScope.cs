using System;
using System.IO;

public static class KingdomPlayModeSaveScope
{
    private static string root;

    public static void Begin()
    {
        Clear();
        root = Path.Combine(Path.GetTempPath(), "KingdomPlayMode-" + Guid.NewGuid().ToString("N"));
        SaveManager.SetSaveRootOverrideForTests(root);
    }

    public static void Clear()
    {
        SaveManager.ClearSaveRootOverrideForTests();
        if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            Directory.Delete(root, true);
        root = null;
    }
}
