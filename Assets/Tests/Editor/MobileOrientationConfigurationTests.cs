using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class MobileOrientationConfigurationTests
{
    [Test]
    public void ProjectSettings_LockOrientationToLandscape()
    {
        string projectSettingsPath = Path.Combine(
            Application.dataPath,
            "..",
            "ProjectSettings",
            "ProjectSettings.asset");
        string projectSettings = File.ReadAllText(projectSettingsPath);

        StringAssert.Contains("defaultScreenOrientation: 4", projectSettings);
        StringAssert.Contains("defaultScreenWidth: 2640", projectSettings);
        StringAssert.Contains("defaultScreenHeight: 1200", projectSettings);
        StringAssert.Contains("allowedAutorotateToPortrait: 0", projectSettings);
        StringAssert.Contains("allowedAutorotateToPortraitUpsideDown: 0", projectSettings);
        StringAssert.Contains("allowedAutorotateToLandscapeRight: 1", projectSettings);
        StringAssert.Contains("allowedAutorotateToLandscapeLeft: 1", projectSettings);
        StringAssert.Contains("useOSAutorotation: 0", projectSettings);
    }

    [Test]
    public void ProjectSettings_UseCurrentAndroidBuildBaseline()
    {
        string projectSettingsPath = Path.Combine(
            Application.dataPath,
            "..",
            "ProjectSettings",
            "ProjectSettings.asset");
        string projectSettings = File.ReadAllText(projectSettingsPath);

        StringAssert.Contains("AndroidMinSdkVersion: 22", projectSettings);
        StringAssert.Contains("AndroidTargetSdkVersion: 35", projectSettings);
        // Unity serializes ARM64 as the Android target architecture value 2.
        StringAssert.Contains("AndroidTargetArchitectures: 2", projectSettings);
        // Unity serializes IL2CPP as the Android scripting backend value 1.
        StringAssert.Contains("scriptingBackend:", projectSettings);
        StringAssert.Contains("    Android: 1", projectSettings);
    }

    [Test]
    public void ProjectSettings_KeepNativeLandscapeResolutionAndSafeAreaSupport()
    {
        string projectSettingsPath = Path.Combine(
            Application.dataPath,
            "..",
            "ProjectSettings",
            "ProjectSettings.asset");
        string projectSettings = File.ReadAllText(projectSettingsPath);

        StringAssert.Contains("defaultScreenWidth: 2640", projectSettings);
        StringAssert.Contains("defaultScreenHeight: 1200", projectSettings);
        StringAssert.Contains("defaultIsNativeResolution: 1", projectSettings);
        StringAssert.Contains("resolutionScalingMode: 0", projectSettings);
        StringAssert.Contains("androidStartInFullscreen: 1", projectSettings);
        // The runtime fits the authored SafeAreaRoot to Screen.safeArea.
        StringAssert.Contains("androidRenderOutsideSafeArea: 1", projectSettings);
    }
}
