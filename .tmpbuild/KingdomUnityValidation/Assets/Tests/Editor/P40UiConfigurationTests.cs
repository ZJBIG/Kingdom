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
        string scene = File.ReadAllText(Path.Combine(projectRoot, "Assets", "Scenes", "SampleScene.unity"));

        StringAssert.Contains("defaultScreenWidth: 2640", projectSettings);
        StringAssert.Contains("defaultScreenHeight: 1200", projectSettings);
        StringAssert.Contains("allowedAutorotateToPortrait: 0", projectSettings);
        StringAssert.Contains("allowedAutorotateToPortraitUpsideDown: 0", projectSettings);
        StringAssert.Contains("m_ReferenceResolution: {x: 2640, y: 1200}", scene);
        StringAssert.Contains("guid: 8a1b1f7f1c2f4e7f9b5d4a1e6c8f0d22", scene);
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
        string rootPath = Path.Combine(projectRoot, "Assets", "Resources", "Script", "UI", "KingdomUIRoot.cs");
        string source = File.ReadAllText(rootPath);

        StringAssert.Contains("BuildOverviewItems", source);
        StringAssert.Contains("ActiveResearch", source);
        StringAssert.Contains("BuildResourceDetailItems", source);
        StringAssert.Contains("BuildBuildingDetailItems", source);
        StringAssert.Contains("CreateBuildingFilterBar", source);
        StringAssert.Contains("BuildResearchGraphItems", source);
        StringAssert.Contains("BuildEraAxisItems", source);
        StringAssert.Contains("UIResearchGraphGesture", source);
        StringAssert.Contains("BuildEraTimelineItems", source);
        StringAssert.Contains("BuildWorkshopItems", source);
        StringAssert.Contains("BuildSectorDetailItems", source);
        StringAssert.Contains("BuildSectorMapItems", source);
        StringAssert.Contains("BuildSettingsCategoryItems", source);
        StringAssert.Contains("ShowModal", source);
        StringAssert.Contains("ModalBackdrop", source);
        StringAssert.Contains("KeyCode.Escape", source);
        StringAssert.Contains("ToggleSimulation", source);
        StringAssert.Contains("SaveGameNow", source);
        StringAssert.Contains("LoadGameNow", source);
        StringAssert.Contains("pageNeedsRebuild", source);
        StringAssert.Contains("NotoSansSC-Regular", source);
        StringAssert.Contains("fallbackFontAssetTable", source);
        StringAssert.Contains("TMP_FontAsset.CreateFontAsset(notoSans)", source);

        string tooltipPath = Path.Combine(projectRoot, "Assets", "Resources", "Script", "UI", "UITouchTooltip.cs");
        Assert.That(File.Exists(tooltipPath), Is.True, "The touch tooltip component is missing.");
        string tooltipSource = File.ReadAllText(tooltipPath);
        StringAssert.Contains("IPointerDownHandler", tooltipSource);
        StringAssert.Contains("WaitForSecondsRealtime", tooltipSource);
    }
}
