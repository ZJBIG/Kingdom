using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class KingdomBackgroundMaterialTests
{
    [Test]
    public void PanelFramesMusicHeaderAndTopStatusAreAuthoredForReadableContent()
    {
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        foreach (string panel in new[] { "Content", "DetailPanel" })
        {
            Transform frame = root.transform.Find("SafeAreaRoot/" + panel + "/Surface/PanelFrame");
            Assert.That(frame, Is.Not.Null, panel);
            Assert.That(frame.childCount, Is.EqualTo(4), "A fixed frame has four sides.");
            foreach (Image image in frame.GetComponentsInChildren<Image>()) Assert.That(image.raycastTarget, Is.False);
        }
        Transform controls = root.transform.Find("SafeAreaRoot/Content/PageHost/Music/DataRows/MusicSurface/Controls");
        RectTransform icon = controls.Find("CurrentCategoryIcon") as RectTransform;
        RectTransform name = controls.Find("CurrentPlayName") as RectTransform;
        Assert.That(icon.anchoredPosition.x + icon.sizeDelta.x, Is.LessThan(name.offsetMin.x));
        foreach (Image image in root.transform.Find("SafeAreaRoot/TopStatusBar/TopInfoShell/InfoGrid").GetComponentsInChildren<Image>())
        {
            Assert.That(image.sprite, Is.Not.Null, image.name);
            Assert.That(image.color.g, Is.GreaterThanOrEqualTo(image.color.r), "Normal status cells must not look like red alarms.");
        }
    }

    [Test]
    public void BackgroundAssetsAndPanelsAreAuthoredWithoutInterceptingInput()
    {
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        Assert.That(root, Is.Not.Null);
        Image environment = root.transform.Find("EnvironmentBackground").GetComponent<Image>();
        Assert.That(environment.sprite, Is.SameAs(Resources.Load<Sprite>("Art/UI/Backgrounds/KingdomEnvironment")));
        Assert.That(environment.raycastTarget, Is.False);
        Assert.That(environment.transform.GetSiblingIndex(), Is.Zero, "Background must precede interactive content.");
        AspectRatioFitter fitter = environment.GetComponent<AspectRatioFitter>();
        Assert.That(fitter.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
        Assert.That(fitter.aspectRatio, Is.EqualTo(environment.sprite.rect.width / environment.sprite.rect.height).Within(.001f));
        Assert.That(fitter.aspectRatio, Is.EqualTo(2640f / 1200f).Within(.001f), "Environment must retain the original phone aspect ratio.");
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(2640, 1200)), "The authored phone reference is an exact design constant.");
        Assert.That(scaler.matchWidthOrHeight, Is.Zero, "Keep the existing Match Width scaling mode.");
        foreach (string panel in new[] { "Content", "DetailPanel", "LeftNavigation", "TopStatusBar", "NotificationBar" })
        {
            Image image = root.transform.Find("SafeAreaRoot/" + panel + "/Surface").GetComponent<Image>();
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), panel);
            Assert.That(image.sprite.border.sqrMagnitude, Is.GreaterThan(0), panel);
            Assert.That(image.raycastTarget, Is.False, panel);
            Assert.That(image.color.a, Is.InRange(.89f, .96f), panel);
            TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(image.sprite)) as TextureImporter;
            Assert.That(importer.mipmapEnabled, Is.False);
        }
        Image previewShade = root.transform.Find("SafeAreaRoot/StoryIllustrationPreview/Backdrop").GetComponent<Image>();
        Assert.That(previewShade.sprite, Is.Null, "Preview retains its plain input-blocking shade.");
        Assert.That(previewShade.raycastTarget, Is.True);
    }

    [Test]
    public void ContentHasOneReadingSurfaceAndPreservesInvisibleInputMasks()
    {
        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        foreach (string name in new[] { "Content", "DetailPanel" })
            Assert.That(root.transform.Find("SafeAreaRoot/" + name + "/Surface").GetComponent<Image>().color.a,
                Is.EqualTo(.96f).Within(.001f), name);
        const string pages = "SafeAreaRoot/Content/PageHost/";
        foreach (string path in new[] { "Overview/PrimaryCard/Surface", "Overview/SecondaryCard/Surface",
            "Music/DataRows/MusicSurface/Surface", "Music/DataRows/MusicSurface/Controls/Surface" })
            Assert.That(root.transform.Find(pages + path).GetComponent<Image>().color.a, Is.LessThan(.01f), path);
        Image graph = root.transform.Find(pages + "Research/DataRows/ResearchGraphViewport").GetComponent<Image>();
        Assert.That(graph.GetComponent<Mask>().showMaskGraphic, Is.False);
        Assert.That(graph.color.a, Is.GreaterThan(.99f), "Stencil geometry must still clip nodes and links.");
        Assert.That(graph.raycastTarget, Is.True);
        Assert.That(graph.sprite, Is.Null);
        foreach (string name in new[] { "BuildingCard", "ResourceCard", "ResearchCard", "TextRow", "MusicTrack", "SectorRow", "WorkshopCard" })
        {
            Image row = Resources.Load<GameObject>("UI/Kingdom/KingdomUI" + name).GetComponent<Image>();
            Assert.That(row.color.a, Is.GreaterThan(0).And.LessThan(.12f), name);
        }
    }

    [Test]
    public void ReapplyingTheAuthorPreservesHierarchyAndExistingSprites()
    {
        GameObject root = Object.Instantiate(Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot"));
        try
        {
            int images = root.GetComponentsInChildren<Image>(true).Length;
            Image background = root.transform.Find("EnvironmentBackground").GetComponent<Image>();
            Sprite sprite = background.sprite;
            KingdomBackgroundAuthoring.Apply(root);
            KingdomBackgroundAuthoring.Apply(root);
            Assert.That(root.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(images), "Reauthoring must not duplicate fixed layers.");
            Assert.That(root.transform.Find("EnvironmentBackground").GetComponent<Image>(), Is.SameAs(background));
            Assert.That(background.sprite, Is.SameAs(sprite));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
