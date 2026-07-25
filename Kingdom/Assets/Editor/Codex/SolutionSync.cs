#if UNITY_EDITOR
using System;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEditor;
using UnityEngine;

namespace Kingdom.EditorTools
{
    public static class SolutionSync
    {
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Type generatorType = typeof(ProjectGeneration).Assembly.GetType(
                "Microsoft.Unity.VisualStudio.Editor.LegacyStyleProjectGeneration",
                throwOnError: true);
            var generator = (ProjectGeneration)Activator.CreateInstance(generatorType);
            generator.Sync();
            Debug.Log("Kingdom solution and C# projects synchronized.");
        }
    }
}
#endif
