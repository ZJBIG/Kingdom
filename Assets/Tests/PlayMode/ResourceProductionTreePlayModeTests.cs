using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

public sealed class ResourceProductionTreePlayModeTests
{
    private string saveRoot;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;
    [SetUp] public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ResourceTree-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }
    [UnityTearDown] public IEnumerator CleanUp()
    {
        Scene scene = SceneManager.GetSceneByName("SampleScene");
        if (scene.IsValid() && scene.isLoaded)
            foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        yield return null;
        SaveManager.ClearSaveRootOverrideForTests();
        Assert.That(Path.GetDirectoryName(saveRoot), Is.EqualTo(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"))));
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }
    [UnityTest]
    public IEnumerator ProductionTreeUsesExistingScroll_ExpandsAndReturnsToDetails()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        KingdomUIRoot ui = null;
        for (int i = 0; i < 160; i++)
        {
            ui = Object.FindObjectOfType<KingdomUIRoot>();
            if (ui != null && Object.FindObjectOfType<GameBootstrap>()?.Completed == true) break;
            yield return null;
        }
        Assert.That(ui, Is.Not.Null);
        SimulationManager.Instance.SetRunning(false);
        ui.SetPage("Resources");
        ui.ShowResourceDetailsForEditor(DataBase<Resource>.Find("WoodLog"));
        ui.ToggleResourceTreeForEditor();
        yield return null;
        Transform content = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent");
        Transform surface = content.Find("ResourceProductionTree");
        Transform rows = surface.Find("Rows");
        Assert.That(rows.gameObject.activeSelf, Is.True);
        Assert.That(content.Find("Body").gameObject.activeSelf, Is.False);
        Assert.That(surface.GetComponentsInChildren<ScrollRect>(true), Is.Empty, "The existing outer detail scroll owns the tree.");
        Assert.That(rows.childCount, Is.GreaterThan(1), "Wood has actual producer branches.");
        Button expand = rows.GetChild(0).Find("Expand").GetComponent<Button>();
        int count = rows.childCount;
        expand.onClick.Invoke(); yield return null;
        Assert.That(rows.childCount, Is.LessThan(count), "Collapsing the root hides producer branches.");
        expand = rows.GetChild(0).Find("Expand").GetComponent<Button>();
        expand.onClick.Invoke(); yield return null;
        Assert.That(rows.childCount, Is.GreaterThan(1));
        RectTransform viewport = content.parent as RectTransform;
        Assert.That(viewport.rect.width, Is.GreaterThan(0));
        Assert.That(viewport.rect.height, Is.GreaterThan(0));
        foreach (RectTransform row in rows)
            Assert.That(row.rect.width, Is.LessThanOrEqualTo(viewport.rect.width));
        surface.Find("Mode").GetComponent<Button>().onClick.Invoke(); yield return null;
        Assert.That(rows.gameObject.activeSelf, Is.False);
        Assert.That(content.Find("Body").gameObject.activeSelf, Is.True);

        ui.ShowResourceDetailsForEditor(DataBase<Resource>.Find("Steel"));
        ui.ToggleResourceTreeForEditor(); yield return null;
        for (int step = 0; step < 12; step++)
        {
            Button next = null;
            foreach (Transform row in rows)
            {
                Button candidate = row.Find("Expand").GetComponent<Button>();
                if (candidate.interactable && candidate.transform.Find("Label").GetComponent<TMP_Text>().text == "+")
                { next = candidate; break; }
            }
            if (next == null) break;
            next.onClick.Invoke(); yield return null;
        }
        RectTransform contentRect = content as RectTransform;
        float range = contentRect.rect.height - viewport.rect.height;
        Assert.That(range, Is.GreaterThan(0), "Expanded Steel production dependencies must have a real scroll range.");
        UIResourceTreeDetailDrag drag = surface.Find("Mode").GetComponent<UIResourceTreeDetailDrag>();
        PointerEventData pointer = new(EventSystem.current) { button = PointerEventData.InputButton.Left,
            pointerId = 31, delta = new Vector2(0, 160), eligibleForClick = true };
        drag.OnPointerDown(pointer); drag.OnInitializePotentialDrag(pointer);
        drag.OnBeginDrag(pointer); drag.OnDrag(pointer); drag.OnEndDrag(pointer); drag.OnPointerUp(pointer);
        Assert.That(contentRect.anchoredPosition.y, Is.GreaterThan(0));
        Assert.That(contentRect.anchoredPosition.y, Is.LessThanOrEqualTo(range));
        Assert.That(pointer.eligibleForClick, Is.False, "A tree-button drag must not navigate on release.");
        rows.GetChild(0).Find("Label").GetComponent<Button>().onClick.Invoke(); yield return null;
        Assert.That(rows.gameObject.activeSelf, Is.False, "Resource node navigation returns to that resource's detail.");
        Assert.That(content.Find("Body").gameObject.activeSelf, Is.True);
    }
}
