#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEditor;
using UnityEngine;

namespace Kingdom.EditorTools
{
    public static class SolutionSync
    {
        [MenuItem("Tools/Kingdom/Project/Sync C# Projects")]
        public static void Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var generator = new ProjectGeneration();
            generator.Sync();
            NormalizeSolution();
            Debug.Log("Kingdom solution and C# projects synchronized.");
        }

        private static void NormalizeSolution()
        {
            string solutionPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Kingdom.sln");
            string solution = File.ReadAllText(solutionPath);

            var playerProjectGuids = Regex.Matches(
                    solution,
                    @"Project\([^\r\n]*\) = [^\r\n]*\.Player\.csproj"", ""\{([^\}]+)\}""\r?\nEndProject\r?\n")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .ToArray();
            solution = Regex.Replace(
                solution,
                @"Project\([^\r\n]*\) = [^\r\n]*\.Player\.csproj"", ""\{[^\}]+\}""\r?\nEndProject\r?\n",
                string.Empty);
            foreach (string guid in playerProjectGuids)
                solution = Regex.Replace(solution, @"^.*" + Regex.Escape(guid) + @".*(?:\r?\n|$)", string.Empty, RegexOptions.Multiline);

            File.WriteAllText(solutionPath, solution);
        }
    }
}
#endif
