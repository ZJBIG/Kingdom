using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class InputFeedbackPlayModeTests
{
    private GameObject root;
    private GameObject audioObject;
    private EventSystem events;
    private Button button;
    private List<string> sequence;
    private Action<UIButtonSoundManager.Sound> observer;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("InputFeedbackTest", typeof(RectTransform));
        events = root.AddComponent<EventSystem>();
        var target = new GameObject("Action", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        target.transform.SetParent(root.transform);
        button = target.GetComponent<Button>();
        target.AddComponent<UIButtonClickFeedback>();
        sequence = new List<string>();
        button.onClick.AddListener(() => sequence.Add("command"));
        UIButtonSoundManager existing = Object.FindObjectOfType<UIButtonSoundManager>();
        if (existing == null)
        {
            audioObject = new GameObject("InputFeedbackAudio");
            audioObject.AddComponent<UIButtonSoundManager>();
        }
        observer = sound => { if (sound == UIButtonSoundManager.Sound.Click) sequence.Add("click"); };
        UIButtonSoundManager.PlayRequested += observer;
    }

    [TearDown]
    public void TearDown()
    {
        UIButtonSoundManager.PlayRequested -= observer;
        Object.DestroyImmediate(root);
        if (audioObject != null) Object.DestroyImmediate(audioObject);
    }

    private PointerEventData Press()
    {
        var data = new PointerEventData(events)
        {
            button = PointerEventData.InputButton.Left,
            pointerId = 0,
            eligibleForClick = true,
            pointerCurrentRaycast = new RaycastResult { gameObject = button.gameObject }
        };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
        return data;
    }

    [Test]
    public void ShortReleaseRequestsSoundBeforeCommand()
    {
        PointerEventData data = Press();
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        Assert.That(sequence, Is.EqualTo(new[] { "click", "command" }));
    }

    [Test]
    public void DragAndReleaseElsewhereDoNotRequestClickSound()
    {
        PointerEventData data = Press();
        data.dragging = true;
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        Assert.That(sequence, Is.Empty);
        data = Press();
        data.pointerCurrentRaycast = default;
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        Assert.That(sequence, Is.Empty);
        button.interactable = false;
        data = Press();
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        Assert.That(sequence, Is.Empty);
    }

    [Test]
    public void FeedbackLeavesAncestorScrollRectAsDragOwner()
    {
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        GameObject dragOwner = ExecuteEvents.GetEventHandler<IDragHandler>(button.gameObject);
        GameObject beginOwner = ExecuteEvents.GetEventHandler<IBeginDragHandler>(button.gameObject);
        Assert.That(dragOwner, Is.SameAs(scroll.gameObject));
        Assert.That(beginOwner, Is.SameAs(scroll.gameObject));
        PointerEventData data = Press();
        Assert.That(ExecuteEvents.Execute(dragOwner, data, ExecuteEvents.beginDragHandler), Is.True,
            "A drag that starts on an ordinary button must reach its ancestor ScrollRect.");
        data.dragging = true;
        data.eligibleForClick = false;
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        Assert.That(sequence, Is.Empty);
    }

    [UnityTest]
    public IEnumerator LongPressDoesNotRequestShortClickFeedback()
    {
        PointerEventData data = Press();
        yield return new WaitForSecondsRealtime(.6f);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        Assert.That(sequence, Is.Empty);
    }
}
