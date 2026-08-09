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
}
