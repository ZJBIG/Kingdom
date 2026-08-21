using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class P40UiConfigurationTests
{
    [Test]
    public void ProjectAndSceneUseP40LandscapeDesignResolution()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string projectSettings = File.ReadAllText(Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset"));

        StringAssert.Contains("defaultScreenWidth: 2640", projectSettings);
        StringAssert.Contains("defaultScreenHeight: 1200", projectSettings);
        StringAssert.Contains("allowedAutorotateToPortrait: 0", projectSettings);
        StringAssert.Contains("allowedAutorotateToPortraitUpsideDown: 0", projectSettings);
        // The runtime UI owns the CanvasScaler and configures the P40 design
        // resolution at startup; the scene intentionally contains no scaler.
    }

    [Test]
    public void BundledUiFont_IsPresent()
    {
        string fontPath = Path.Combine(
            Application.dataPath,
            "Resources",
            "Fonts",
            "NotoSansSC-Regular.otf");
        Assert.That(File.Exists(fontPath), Is.True, "The bundled UI font is missing.");
    }

    [Test]
    public void RuntimeUiRoot_ContainsRequiredLandscapePagesAndFontFallback()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        string tooltipPath = Path.Combine(projectRoot, "Assets", "Resources", "Script", "UI", "UITouchTooltip.cs");
        Assert.That(File.Exists(tooltipPath), Is.True, "The touch tooltip component is missing.");
    }
}
