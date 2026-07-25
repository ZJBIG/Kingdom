using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kingdom.EditorTools
{
    public static class DefinitionIdMigration
    {
        private const string DataRoot = "Assets/Resources/Datas";

        [MenuItem("Tools/Kingdom/Definitions/Audit Stable IDs")]
        public static void AuditFromMenu() => Run(false, false);

        [MenuItem("Tools/Kingdom/Definitions/Assign Missing IDs From Asset Names")]
        public static void ApplyFromMenu() => Run(true, false);

        public static void ApplyFromCommandLine() => Run(true, true);

        public static void ReserializeFromCommandLine()
        {
            try
            {
                var paths = new List<string>();
                AddPaths<Resource>(paths);
                AddPaths<Building>(paths);
                AddPaths<Research>(paths);
                AssetDatabase.ForceReserializeAssets(
                    paths,
                    ForceReserializeAssetsOptions.ReserializeAssets);
                AssetDatabase.SaveAssets();
                Debug.Log($"Definition reserialization passed. Assets={paths.Count}.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Run(bool apply, bool exitEditor)
        {
            try
            {
                int changed = 0;
                int count = 0;
                var errors = new List<string>();

                ProcessType<Resource>(apply, ref count, ref changed, errors);
                ProcessType<Building>(apply, ref count, ref changed, errors);
                ProcessType<Research>(apply, ref count, ref changed, errors);

                if (errors.Count != 0)
                    throw new InvalidOperationException(string.Join("\n", errors));

                if (apply && changed != 0)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                Debug.Log($"Definition ID {(apply ? "migration" : "audit")} passed. Assets={count}, Changed={changed}, Errors=0.");
                if (exitEditor)
                    EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (exitEditor)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }

        private static void ProcessType<T>(
            bool apply,
            ref int count,
            ref int changed,
            List<string> errors)
            where T : GameDefinition
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot });
            var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T definition = AssetDatabase.LoadAssetAtPath<T>(path);
                if (definition == null)
                {
                    errors.Add($"Unable to load {typeof(T).Name} asset at '{path}'.");
                    continue;
                }

                count++;
                string id = definition.Id == null ? string.Empty : definition.Id.Trim();
                if (id.Length == 0 && apply)
                {
                    id = definition.name.Trim();
                    definition.SetIdForEditor(id);
                    EditorUtility.SetDirty(definition);
                    changed++;
                }

                if (id.Length == 0)
                {
                    errors.Add($"{typeof(T).Name} asset '{path}' has an empty Id.");
                    continue;
                }

                if (ids.TryGetValue(id, out string existingPath))
                    errors.Add($"Duplicate {typeof(T).Name} Id '{id}': '{existingPath}' and '{path}'.");
                else
                    ids.Add(id, path);
            }
        }

        private static void AddPaths<T>(List<string> paths) where T : GameDefinition
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot });
            for (int i = 0; i < guids.Length; i++)
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
        }
    }
}
