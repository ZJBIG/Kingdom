using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class KingdomBuild
{
    private const string AndroidOutputPath = "Builds/Android/Kingdom.apk";

    public static void BuildAndroid()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputPath = Path.Combine(projectRoot, AndroidOutputPath);
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDirectory))
            throw new InvalidOperationException("Android output directory is invalid.");
        Directory.CreateDirectory(outputDirectory);

        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        PlayerSettings.SetScriptingBackend(
            BuildTargetGroup.Android,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled Unity scenes are configured.");

        BuildReport report = BuildPipeline.BuildPlayer(
            scenes,
            outputPath,
            BuildTarget.Android,
            BuildOptions.StrictMode);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException(
                $"Android build failed: {report.summary.result}, errors={report.summary.totalErrors}.");

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            throw new InvalidOperationException("Android build reported success but APK is missing.");

        Debug.Log(
            $"[KingdomBuild] Android APK created: {outputPath} " +
            $"size={new FileInfo(outputPath).Length} bytes, " +
            $"warnings={report.summary.totalWarnings}, errors={report.summary.totalErrors}");
    }
}
