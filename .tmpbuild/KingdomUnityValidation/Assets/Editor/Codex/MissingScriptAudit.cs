using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class MissingScriptAudit
{
    private const string SessionKey = "Kingdom.MissingScriptAudit.Ran";

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        int total = 0;
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid())
        {
            foreach (GameObject root in active.GetRootGameObjects())
                total += ScanHierarchy(root, "scene:" + active.path);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/UI/Kingdom" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                total += ScanHierarchy(prefab, "prefab:" + path);
        }

        if (total == 0)
            Debug.Log("[KingdomUI] Missing script audit: PASS (scene and Kingdom prefabs contain no missing MonoBehaviour)");
        else
            Debug.LogError($"[KingdomUI] Missing script audit: FAIL count={total}");
    }

    private static int ScanHierarchy(GameObject root, string source)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        if (count > 0)
            Debug.LogError($"[KingdomUI] Missing script: {source}/{GetPath(root.transform)}, count={count}");

        foreach (Transform child in root.transform)
            count += ScanHierarchy(child.gameObject, source);
        return count;
    }

    private static string GetPath(Transform value)
    {
        string path = value.name;
        while (value.parent != null)
        {
            value = value.parent;
            path = value.name + "/" + path;
        }
        return path;
    }
}
