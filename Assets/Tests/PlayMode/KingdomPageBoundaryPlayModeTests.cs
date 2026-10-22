using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class KingdomPageBoundaryPlayModeTests
{
    private string saveRoot;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "PageBoundary-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("PageBoundaryTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded) yield return SceneManager.UnloadSceneAsync(sample);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }

    [UnityTest]
    public IEnumerator OrdinaryPagesExposeTheirLastTextAtBottomAndIndicateRealOverflow()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        KingdomUIRoot root = null;
        GameBootstrap bootstrap = null;
        for (int frame = 0; frame < 160; frame++)
        {
            foreach (GameObject sceneRoot in SceneManager.GetSceneByName("SampleScene").GetRootGameObjects())
            {
                if (root == null) root = sceneRoot.GetComponentInChildren<KingdomUIRoot>(true);
                if (bootstrap == null) bootstrap = sceneRoot.GetComponentInChildren<GameBootstrap>(true);
            }
            if (root != null && bootstrap != null && bootstrap.Completed) break;
            yield return null;
        }
        Assert.That(root, Is.Not.Null);
        Assert.That(bootstrap != null && bootstrap.Completed, Is.True);
        SimulationManager.Instance.SetRunning(false);
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Ultra);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.IndustrialWorkshop);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1000000));
        foreach (SectorDef sector in DataBase<SectorDef>.All)
            GameManager.Instance.Sectors.GetState(sector).SetOccupiedForEditor(true);
        List<string> chapters = new();
        foreach (StoryChapter chapter in StoryManager.Chapters) chapters.Add(chapter.Id);
        StoryManager.RestoreSaveData(new SaveManager.StorySaveData { CompletedChapterIds = chapters });
        root.StoryPageBuiltForEditor = false;
        root.RefreshNavigationVisibilityForEditor();

        ScrollRect scroll = root.transform.Find("SafeAreaRoot/Content/PageHost").GetComponent<ScrollRect>();
        Assert.That(scroll.verticalScrollbar, Is.Not.Null);
        RectTransform track = (RectTransform)scroll.verticalScrollbar.transform;
        Bounds trackBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, track);
        Assert.That(trackBounds.min.y, Is.GreaterThan(scroll.viewport.rect.yMin));
        Assert.That(trackBounds.max.y, Is.LessThan(scroll.viewport.rect.yMax));
        Assert.That(trackBounds.min.x, Is.GreaterThan(scroll.viewport.rect.xMax));
        Assert.That(root.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/ScrollFrame"), Is.Not.Null);
        int overflowingPages = 0;
        int fittingPages = 0;
        foreach (string page in new[] { "Overview", "Resources", "Buildings", "Era", "Workshop", "Sectors", "Story" })
        {
            root.SetPage(page);
            yield return null;
            if (page == "Story")
                foreach (StoryChapterCard card in scroll.content.GetComponentsInChildren<StoryChapterCard>())
                    card.SetExpanded(false);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            yield return null;
            // Fitters rebuild content before ScrollRect updates AutoHide in LateUpdate.
            yield return null;
            Assert.That(scroll.enabled && scroll.vertical, Is.True, page);
            Assert.That(scroll.viewport.rect.width, Is.GreaterThan(0f), page);
            Assert.That(scroll.viewport.rect.height, Is.GreaterThan(0f), page);
            Assert.That(scroll.content.rect.height, Is.GreaterThan(0f), page);
            bool overflowing = scroll.content.rect.height > scroll.viewport.rect.height + 1f;
            if (overflowing)
            {
                overflowingPages++;
                Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True, page + " has more content below.");
                Assert.That(scroll.verticalScrollbar.size, Is.InRange(0f, .999f), page);
                Assert.That(scroll.verticalScrollbar.targetGraphic.color.a, Is.GreaterThan(0f), page);
            }
            else
            {
                fittingPages++;
                Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.False, page + " fits without scrolling.");
                Vector2 before = scroll.content.anchoredPosition;
                scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = Vector2.down * 10f });
                yield return null;
                Assert.That(Vector2.Distance(scroll.content.anchoredPosition, before), Is.LessThan(.1f),
                    page + ": a short page must not move on wheel input.");
            }
            Assert.That(Mathf.Abs(scroll.content.anchoredPosition.y), Is.LessThan(.1f),
                page + ": first entry starts at the top after layout.");
            RectTransform rows = scroll.content.Find("DataRows") as RectTransform;
            if (rows != null)
                Assert.That(Mathf.Abs(rows.anchoredPosition.y), Is.LessThan(.1f),
                    page + ": the external page title needs no blank header inside the list.");

            scroll.StopMovement();
            Debug.Log("[PageBoundaryBeforeScroll] page=" + page + " normalized=" + scroll.verticalNormalizedPosition + " content=" + scroll.content.rect.size);
            scroll.verticalNormalizedPosition = 0;
            Debug.Log("[PageBoundaryAfterScroll] page=" + page + " normalized=" + scroll.verticalNormalizedPosition + " pos=" + scroll.content.anchoredPosition);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Debug.Log("[PageBoundarySettledScroll] page=" + page + " normalized=" + scroll.verticalNormalizedPosition + " pos=" + scroll.content.anchoredPosition + " content=" + scroll.content.rect.size);
            TMP_Text last = LastActiveText(scroll.content);
            last.ForceMeshUpdate();
            Vector3[] corners = new Vector3[4];
            last.rectTransform.GetWorldCorners(corners);
            Vector3 bottom = scroll.viewport.InverseTransformPoint(corners[0]);
            Vector3 top = scroll.viewport.InverseTransformPoint(corners[1]);
            Assert.That(bottom.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 2f), page + ": " + last.name);
            Assert.That(top.y, Is.GreaterThan(scroll.viewport.rect.yMin), page + ": last text enters the viewport.");
            for (int index = last.textInfo.characterCount - 1; index >= 0; index--)
            {
                TMP_CharacterInfo character = last.textInfo.characterInfo[index];
                if (!character.isVisible) continue;
                Vector3 glyph = scroll.viewport.InverseTransformPoint(last.transform.TransformPoint(character.bottomLeft));
                Assert.That(glyph.y, Is.InRange(scroll.viewport.rect.yMin - 2f, scroll.viewport.rect.yMax + 2f),
                    page + ": the final visible glyph is reachable, rather than clipped below the scroll bounds.");
                break;
            }
            Debug.Log("[PageBoundary] page=" + page + " viewport=" + scroll.viewport.rect.size +
                " content=" + scroll.content.rect.size + " overflow=" + overflowing + " last=" + last.name + " bottom=" + bottom.y);
        }
        Assert.That(overflowingPages, Is.GreaterThan(0), "The fixture exercises real scrolling lists.");
        Assert.That(fittingPages, Is.GreaterThan(0), "The fixture also exercises short pages without overflow.");
    }

    private static TMP_Text LastActiveText(RectTransform content)
    {
        TMP_Text last = null;
        float bottom = float.PositiveInfinity;
        Vector3[] corners = new Vector3[4];
        foreach (TMP_Text text in content.GetComponentsInChildren<TMP_Text>())
        {
            if (!text.enabled || string.IsNullOrWhiteSpace(text.text)) continue;
            text.rectTransform.GetWorldCorners(corners);
            float y = content.InverseTransformPoint(corners[0]).y;
            if (y >= bottom) continue;
            bottom = y;
            last = text;
        }
        return last;
    }
}
