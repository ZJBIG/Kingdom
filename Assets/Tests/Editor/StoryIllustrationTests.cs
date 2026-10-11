using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class StoryIllustrationTests
{
    [Test]
    public void BackgroundCardAndSharedPreviewHaveAuthoredControlsAndScrollBoundaries()
    {
        var card = Resources.Load<StoryChapterCard>("UI/Kingdom/StoryChapterCard");
        Assert.That(card, Is.Not.Null);
        Assert.That(card.GetComponent<StoryChapterLayout>(), Is.Not.Null);
        var toggle = card.transform.Find("Header/Actions/StoryChapterToggle").GetComponent<LayoutElement>();
        var navigation = card.transform.Find("Header/Actions/StoryChapterNavigation").GetComponent<LayoutElement>();
        Assert.That(toggle.preferredWidth, Is.EqualTo(navigation.preferredWidth).Within(.01f));
        Assert.That(toggle.preferredHeight, Is.EqualTo(navigation.preferredHeight).Within(.01f));
        var styledButton = toggle.GetComponent<Button>();
        Assert.That(styledButton.GetComponent<Image>().sprite, Is.Not.Null);
        Assert.That(styledButton.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
        Assert.That(styledButton.spriteState.highlightedSprite, Is.Not.Null);
        Assert.That(card.transform.Find("Header/Actions/StoryIllustrationPreview").GetComponent<Button>(), Is.Not.Null);
        var scroll = card.transform.Find("BodyViewport").GetComponent<StoryBodyScrollRect>();
        Assert.That(scroll.content, Is.SameAs(card.transform.Find("BodyViewport/Body")));
        Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
        Assert.That(scroll.inertia, Is.False);
        Assert.That(card.transform.Find("IllustrationFrame/Shade").GetComponent<Image>().color.a,
            Is.EqualTo(.55f).Within(.01f));
        var root = Resources.Load<KingdomUIRoot>("UI/Kingdom/KingdomUIRoot");
        var preview = root.transform.Find("SafeAreaRoot/StoryIllustrationPreview");
        Assert.That(preview.GetComponent<StoryIllustrationPreview>(), Is.Not.Null);
        Assert.That(preview.gameObject.activeSelf, Is.False);
        Assert.That(preview.Find("Viewport").GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(preview.Find("Backdrop").GetComponent<Button>(), Is.Not.Null);
    }

    [Test]
    public void AuthoredArchiveHasADistinctIllustrationForEveryChapterAndForwardsThem()
    {
        StoryArchiveDefinition archive = Resources.Load<StoryArchiveDefinition>("Datas/Story/StoryArchive");
        Assert.That(archive, Is.Not.Null);
        Assert.That(archive.Chapters.Count, Is.EqualTo(18), "The archive protocol fixes 18 chapters.");
        var sprites = new HashSet<Sprite>();
        for (int i = 0; i < archive.Chapters.Count; i++)
        {
            StoryChapterDefinition definition = archive.Chapters[i];
            StoryChapter runtime = StoryManager.Chapters[i];
            Assert.That(runtime.Id, Is.EqualTo(definition.Id), "Stable IDs must retain archive order.");
            Assert.That(runtime.Illustration, Is.SameAs(definition.Illustration));
            Assert.That(definition.Illustration, Is.Not.Null, definition.Id);
            Assert.That(sprites.Add(definition.Illustration), Is.True,
                definition.Id + " must have its own illustration.");
            float aspect = definition.Illustration.rect.width / definition.Illustration.rect.height;
            Assert.That(aspect, Is.EqualTo(16f / 9f).Within(.03f));
            TextureImporter importer = AssetImporter.GetAtPath(
                AssetDatabase.GetAssetPath(definition.Illustration)) as TextureImporter;
            Assert.That(AssetDatabase.GetAssetPath(definition.Illustration),
                Is.EqualTo("Assets/Resources/Art/Story/Story_" + definition.name + "_Main.png"),
                "Every chapter must bind its own authored asset path.");
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048), "Runtime texture limit is an authored setting.");
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
        }
        Assert.That(sprites.Count, Is.EqualTo(archive.Chapters.Count),
            "Every archived chapter has its own illustration.");
    }

    [Test]
    public void OptionalIllustrationDoesNotAlterTheChapterActionGates()
    {
        StoryArchiveDefinition archive = Resources.Load<StoryArchiveDefinition>("Datas/Story/StoryArchive");
        foreach (StoryChapterDefinition definition in archive.Chapters)
        {
            StoryChapter runtime = definition.ToRuntime();
            Assert.That(runtime.RequiredEra, Is.EqualTo(definition.RequiredEra));
            Assert.That(runtime.RequiredTutorialStepId, Is.EqualTo(definition.RequiredTutorialStepId ?? string.Empty));
            AssertIds(definition.RequiredResearch, runtime.RequiredResearchIds);
            AssertIds(definition.RequiredBuildings, runtime.RequiredBuildingIds);
            AssertIds(definition.RequiredWorkshops, runtime.RequiredWorkshopIds);
            AssertIds(definition.RequiredUnlockedSectors, runtime.RequiredUnlockedSectorIds);
            AssertIds(definition.RequiredOccupiedSectors, runtime.RequiredOccupiedSectorIds);
        }
    }

    private static void AssertIds<T>(IReadOnlyList<T> definitions,
        IReadOnlyList<string> runtimeIds) where T : GameDefinition
    {
        var expected = new List<string>();
        if (definitions != null)
            foreach (T definition in definitions)
                if (definition != null)
                    expected.Add(definition.Id);
        CollectionAssert.AreEqual(expected, runtimeIds, "Stable gate IDs must pass through unchanged.");
    }
}
