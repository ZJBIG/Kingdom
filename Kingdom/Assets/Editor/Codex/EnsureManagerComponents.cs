using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kingdom.EditorTools
{
    public static class EnsureManagerComponents
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ManagerObjectName = "Manager";

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject managerObject = GameObject.Find(ManagerObjectName);
            if (managerObject == null)
                throw new MissingReferenceException($"Scene '{ScenePath}' does not contain '{ManagerObjectName}'.");

            bool changed = false;
            changed |= EnsureComponent<GameBootstrap>(managerObject);
            changed |= EnsureComponent<SaveManager>(managerObject);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"Manager component check passed. Changed={changed}.");
        }

        private static bool EnsureComponent<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() != null)
                return false;

            target.AddComponent<T>();
            return true;
        }
    }
}
