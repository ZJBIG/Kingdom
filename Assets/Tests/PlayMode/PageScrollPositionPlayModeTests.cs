using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using System.Collections;
using Object = UnityEngine.Object;

public sealed class PageScrollPositionPlayModeTests
{
    private string saveRoot;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;
    private UIButtonSoundManager initialSound;
    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        initialSound = Object.FindObjectOfType<UIButtonSoundManager>(true);
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        saveRoot = Path.Combine(projectRoot, "Temp", "KingdomPageScrollPlayModeTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("PageScrollTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            yield return SceneManager.UnloadSceneAsync(sample);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        UIButtonSoundManager sound = Object.FindObjectOfType<UIButtonSoundManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        if (sound != null && sound != initialSound) Object.DestroyImmediate(sound.gameObject);
        Assert.That(Object.FindObjectOfType<SaveManager>(true), Is.Null,
            "Destroy every save writer before releasing the isolated save root.");
        SaveManager.ClearSaveRootOverrideForTests();
        if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot))
        {
            Directory.Delete(saveRoot, true);
            Assert.That(Directory.Exists(saveRoot), Is.False);
        }
        saveRoot = null;
    }

    [UnityTest]
    public IEnumerator StoryPageRestoresPositionAfterResearchWarmupAndNavigation()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.6f);

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Transform pageHost = root.transform.Find("SafeAreaRoot/Content/PageHost");
        ScrollRect outerScroll = pageHost?.GetComponent<ScrollRect>();
        Button storyButton = root.transform.Find(
            "SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Story")?.GetComponent<Button>();
        Button overviewButton = root.transform.Find(
            "SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Overview")?.GetComponent<Button>();
        Assert.That(outerScroll, Is.Not.Null);
        Assert.That(storyButton, Is.Not.Null);
        Assert.That(overviewButton, Is.Not.Null);

        BuildingManager.Instance.SetAmountAndRatesForEditor(BuildingManager.Instance.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);

        root.SetPage("Research");
        yield return null;
        Assert.That(outerScroll.enabled, Is.False, "Research owns its graph scrolling.");
        overviewButton.onClick.Invoke();
        yield return null;
        Assert.That(outerScroll.enabled && outerScroll.vertical, Is.True);
        Assert.That(outerScroll.content, Is.EqualTo(pageHost.Find("Overview")));

        storyButton.onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(outerScroll.enabled && outerScroll.vertical, Is.True);
        Assert.That(outerScroll.content, Is.EqualTo(pageHost.Find("Story")));
        Assert.That(outerScroll.verticalNormalizedPosition, Is.GreaterThan(0.99f),
            "Story must initially enter at the top of its content.");
        outerScroll.verticalNormalizedPosition = 0.42f;
        float expected = outerScroll.verticalNormalizedPosition;

        overviewButton.onClick.Invoke();
        yield return null;
        Assert.That(outerScroll.enabled && outerScroll.vertical, Is.True);
        Assert.That(outerScroll.content, Is.EqualTo(pageHost.Find("Overview")));
        storyButton.onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();

        Assert.That(outerScroll.content, Is.EqualTo(
            pageHost.Find("Story").GetComponent<RectTransform>()));
        Assert.That(outerScroll.verticalNormalizedPosition,
            Is.EqualTo(expected).Within(0.02f));
    }
}
