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

public sealed class StoryIllustrationPlayModeTests
{
    private string saveRoot;
    private KingdomUIRoot root;
    private const string SurfacePath = "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage";

    [SetUp]
    public void SetUp()
    {
        saveRoot = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            "Temp", "KingdomStoryIllustrationTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Destroy the scene's writers before releasing the isolated save root.
        Scene empty = SceneManager.CreateScene("StoryIllustrationTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            yield return SceneManager.UnloadSceneAsync(sample);
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot))
            Directory.Delete(saveRoot, true);
        root = null;
    }

    [UnityTest]
    public IEnumerator LatestPrologueShowsItsImageAndLockedChaptersCannotLeakTheirImages()
    {
        yield return LoadStory(1, TechLevel.Animal);
        RectTransform first = Card("PrologueAshes_00");
        AssertVisibleImage(first, StoryManager.Chapters[0].Illustration);
        Assert.That(first.Find("Header/StoryChapterToggle").gameObject.activeSelf, Is.False);
        for (int i = 1; i < StoryManager.Chapters.Count; i++)
        {
            string id = StoryManager.Chapters[i].Id;
            RectTransform locked = Card(id);
            Image image = locked.Find("IllustrationFrame/Illustration").GetComponent<Image>();
            Assert.That(image.transform.parent.gameObject.activeSelf, Is.False, id + " is still locked.");
            Assert.That(image.sprite, Is.Null, "Locked chapter must not bind unrevealed art.");
        }
        Button navigation = first.Find("Header/StoryChapterNavigation").GetComponent<Button>();
        Assert.That(navigation.gameObject.activeInHierarchy && navigation.interactable, Is.True);
        Assert.That(navigation.GetComponent<UIPageScrollDragForwarder>(), Is.Not.Null,
            "The authored navigation button must forward page drags.");
        navigation.onClick.Invoke();
        yield return null;
        ScrollRect scroll = OuterScroll();
        Assert.That(scroll.content.name, Is.EqualTo("Overview"));
    }

    [UnityTest]
    public IEnumerator ToggleReclaimsIllustrationSpaceAndFollowingChapterRemainsReadable()
    {
        yield return LoadStory(2, TechLevel.Animal);
        RectTransform first = Card("PrologueAshes_00");
        RectTransform next = Card("FirstFire_01");
        Image image = first.Find("IllustrationFrame/Illustration").GetComponent<Image>();
        Button toggle = first.Find("Header/StoryChapterToggle").GetComponent<Button>();
        Assert.That(toggle.GetComponent<UIPageScrollDragForwarder>(), Is.Not.Null,
            "The authored toggle button must forward page drags.");
        Assert.That(image.transform.parent.gameObject.activeSelf, Is.False);
        float collapsedHeight = first.rect.height;
        float collapsedNextY = next.anchoredPosition.y;
        toggle.onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();
        AssertVisibleImage(first, StoryManager.Chapters[0].Illustration);
        float expandedHeight = first.rect.height;
        Assert.That(expandedHeight, Is.GreaterThan(collapsedHeight + image.rectTransform.rect.height * .8f));
        Assert.That(next.anchoredPosition.y, Is.LessThan(collapsedNextY));
        AssertNoOverlap(first, next);
        toggle.onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(image.transform.parent.gameObject.activeSelf, Is.False);
        Assert.That(first.rect.height, Is.EqualTo(collapsedHeight).Within(2f));
        Assert.That(next.anchoredPosition.y, Is.EqualTo(collapsedNextY).Within(2f));
        AssertVisibleImage(next, StoryManager.Chapters[1].Illustration);
        TMP_Text body = next.Find("Body").GetComponent<TMP_Text>();
        body.ForceMeshUpdate(true, true);
        Assert.That(body.textInfo.characterCount, Is.GreaterThan(0));
        Assert.That(body.rectTransform.rect.height, Is.GreaterThanOrEqualTo(body.preferredHeight - 2f));

        ScrollRect scroll = OuterScroll();
        scroll.verticalNormalizedPosition = .5f;
        Vector2 beforeDrag = scroll.content.anchoredPosition;
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, toggle.transform.position)
        };
        ExecuteEvents.Execute(toggle.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(toggle.gameObject, pointer, ExecuteEvents.beginDragHandler);
        pointer.position += Vector2.up * 120f;
        ExecuteEvents.Execute(toggle.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(toggle.gameObject, pointer, ExecuteEvents.endDragHandler);
        Assert.That(Vector2.Distance(scroll.content.anchoredPosition, beforeDrag), Is.GreaterThan(1f),
            "Dragging from the authored toggle must move the Story page.");
        Debug.Log("[StoryIllustrations] Button drag moved content from " + beforeDrag +
            " to " + scroll.content.anchoredPosition);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = .42f;
        float expected = scroll.verticalNormalizedPosition;
        root.SetPage("Overview");
        yield return null;
        root.SetPage("Story");
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(expected).Within(.02f));
    }

    [UnityTest]
    public IEnumerator CompletedWorkshopAndMoonChaptersDisplayTheirOwnArt()
    {
        yield return LoadStory(15, TechLevel.Spacer);
        RectTransform moon = Card("FrontierSectors_14");
        AssertVisibleImage(moon, StoryManager.Chapters[14].Illustration);
        RectTransform workshop = Card("WorkshopMemory_09");
        Assert.That(workshop.Find("IllustrationFrame").gameObject.activeSelf, Is.False);
        workshop.Find("Header/StoryChapterToggle").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();
        AssertVisibleImage(workshop, StoryManager.Chapters[9].Illustration);
        AssertNoOverlap(workshop, Card("IndustrialPower_10"));
        Assert.That(moon.Find("Header/StoryChapterNavigation").GetComponent<Button>().interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator EveryCompletedChapterCanDisplayItsArtWithoutOverlappingFollowingCards()
    {
        yield return LoadStory(StoryManager.Chapters.Count, TechLevel.Ultra);
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
        {
            StoryChapter chapter = StoryManager.Chapters[i];
            RectTransform card = Card(chapter.Id);
            if (i < StoryManager.Chapters.Count - 1)
            {
                card.Find("Header/StoryChapterToggle").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertNoOverlap(card, Card(StoryManager.Chapters[i + 1].Id));
            }
            AssertVisibleImage(card, chapter.Illustration);
            TMP_Text title = card.Find("Header/Title").GetComponent<TMP_Text>();
            Assert.That(title.rectTransform.rect.height,
                Is.GreaterThanOrEqualTo(title.preferredHeight - 2f),
                "The chapter heading must retain all authored lines.");
        }
        ScrollRect scroll = OuterScroll();
        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
        Vector2 initial = scroll.content.anchoredPosition;
        scroll.verticalNormalizedPosition = 0f;
        yield return null;
        Assert.That(Vector2.Distance(scroll.content.anchoredPosition, initial), Is.GreaterThan(1f),
            "The expanded illustrated archive must remain scrollable.");
    }

    private IEnumerator LoadStory(int completedCount, TechLevel era)
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;
        root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        GameManager.Instance.AdvanceTechLevelForEditor(era);
        var completed = new List<string>();
        for (int i = 0; i < completedCount; i++)
            completed.Add(StoryManager.Chapters[i].Id);
        StoryManager.RestoreSaveData(new SaveManager.StorySaveData { CompletedChapterIds = completed });
        root.StoryPageBuiltForEditor = false;
        root.SetPage("Story");
        yield return null;
        Canvas.ForceUpdateCanvases();
    }

    private RectTransform Card(string id)
    {
        Transform surface = root.transform.Find(SurfacePath);
        Assert.That(surface, Is.Not.Null);
        RectTransform card = surface.Find("StoryChapter_" + id) as RectTransform;
        Assert.That(card, Is.Not.Null, id);
        return card;
    }

    private ScrollRect OuterScroll() => root.transform.Find(
        "SafeAreaRoot/Content/PageHost").GetComponent<ScrollRect>();

    private static void AssertVisibleImage(RectTransform card, Sprite expected)
    {
        Assert.That(expected, Is.Not.Null, card.name);
        Image image = card.Find("IllustrationFrame/Illustration").GetComponent<Image>();
        Assert.That(image.gameObject.activeInHierarchy, Is.True, card.name);
        Assert.That(image.sprite, Is.SameAs(expected));
        Assert.That(image.preserveAspect, Is.True);
        Assert.That(image.rectTransform.rect.width, Is.GreaterThan(0f));
        Assert.That(image.rectTransform.rect.height, Is.GreaterThan(0f));
        Debug.Log("[StoryIllustrations] " + card.name + " sprite=" + image.sprite.name +
            " card=" + card.rect.size + " image=" + image.rectTransform.rect.size);
        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        Assert.That(aspect, Is.EqualTo(16f / 9f).Within(.03f));
        var imageCorners = new Vector3[4];
        var cardCorners = new Vector3[4];
        var bodyCorners = new Vector3[4];
        image.rectTransform.GetWorldCorners(imageCorners);
        card.GetWorldCorners(cardCorners);
        ((RectTransform)card.Find("Body")).GetWorldCorners(bodyCorners);
        Assert.That(imageCorners[0].x, Is.GreaterThanOrEqualTo(cardCorners[0].x - 1f));
        Assert.That(imageCorners[2].x, Is.LessThanOrEqualTo(cardCorners[2].x + 1f));
        Assert.That(imageCorners[0].y, Is.GreaterThanOrEqualTo(bodyCorners[1].y - 1f),
            "Illustration and body text must not overlap.");
    }

    private static void AssertNoOverlap(RectTransform earlier, RectTransform later)
    {
        var earlierCorners = new Vector3[4];
        var laterCorners = new Vector3[4];
        earlier.GetWorldCorners(earlierCorners);
        later.GetWorldCorners(laterCorners);
        Assert.That(earlierCorners[0].y, Is.GreaterThanOrEqualTo(laterCorners[1].y - 1f));
    }
}
