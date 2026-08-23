using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class KingdomOnboardingPlayModeTests
{
    [UnityTest]
    public IEnumerator OverviewShowsPersistentOnboardingGoal()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        Transform primaryCard = GameObject.Find("KingdomUIRoot/SafeAreaRoot/Content/PageHost/Overview/PrimaryCard")?.transform;
        if (primaryCard == null)
        {
            KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
            Assert.That(root, Is.Not.Null);
            primaryCard = root.transform.Find("SafeAreaRoot/Content/PageHost/Overview/PrimaryCard");
        }
        Assert.That(primaryCard, Is.Not.Null);
        TMP_Text text = primaryCard.Find("Text")?.GetComponent<TMP_Text>();
        Assert.That(text, Is.Not.Null);
        Assert.That(text.text, Does.Contain("当前目标"));
        Assert.That(text.text, Does.Contain("下一时代目标"));
        Assert.That(text.text, Does.Contain("推荐行动"));
        Assert.That(text.text, Does.Contain("文明复兴"));
    }

    [UnityTest]
    public IEnumerator EraPageIncludesOnboardingGuidance()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Era" });
        yield return null;

        Transform rows = root.transform.Find("SafeAreaRoot/Content/PageHost/Era/DataRows");
        Assert.That(rows, Is.Not.Null);
        string rendered = string.Empty;
        TMP_Text[] labels = rows.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
            rendered += labels[i].text + "\n";
        Assert.That(rendered, Does.Contain("引导目标"));
        Assert.That(rendered, Does.Contain("引导推荐行动"));
        Assert.That(rendered, Does.Contain("文明复兴阶段"));
    }
}
