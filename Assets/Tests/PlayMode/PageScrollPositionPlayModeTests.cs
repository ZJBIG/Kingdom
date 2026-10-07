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
    [SetUp]
    public void SetUp()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        saveRoot = Path.Combine(projectRoot, "Temp", "KingdomPageScrollPlayModeTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [TearDown]
    public void TearDown()
    {
        SaveManager.ClearSaveRootOverrideForTests();
        if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot))
            Directory.Delete(saveRoot, true);
        saveRoot = null;
    }

    [UnityTest]
    public IEnumerator StoryPageRestoresPositionAfterLeavingAndReturning()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.6f);

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        Transform pageHost = root.transform.Find("SafeAreaRoot/Content/PageHost");
        ScrollRect outerScroll = pageHost?.GetComponent<ScrollRect>();
        Button storyButton = root.transform.Find(
            "SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Story")?.GetComponent<Button>();
        Button overviewButton = root.transform.Find(
            "SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Overview")?.GetComponent<Button>();
        Assert.That(outerScroll, Is.Not.Null);
        Assert.That(storyButton, Is.Not.Null);
        Assert.That(overviewButton, Is.Not.Null);

        storyButton.onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();
        outerScroll.verticalNormalizedPosition = 0.42f;
        float expected = outerScroll.verticalNormalizedPosition;

        overviewButton.onClick.Invoke();
        yield return null;
        storyButton.onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();

        Assert.That(outerScroll.content, Is.EqualTo(
            pageHost.Find("Story").GetComponent<RectTransform>()));
        Assert.That(outerScroll.verticalNormalizedPosition,
            Is.EqualTo(expected).Within(0.02f));
    }
}
