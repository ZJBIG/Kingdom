using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class UIGestureRegressionPlayModeTests
{
    private GameObject root;
    private GameObject cameraObject;
    private GameObject eventSystemObject;
    private RectTransform viewport;
    private RectTransform content;
    private UIResearchGraphGesture graph;
    private GraphicRaycaster raycaster;
    private Camera eventCamera;

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(eventSystemObject);
    }

    [UnityTest]
    public IEnumerator GraphBackgroundAndNodeDragsSurviveExitAndNextFrame()
    {
        CreateGraph(false);
        GameObject node = new GameObject("Node", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(UIResearchGraphDragForwarder));
        node.transform.SetParent(content, false);
        foreach (GameObject target in new[] { viewport.gameObject, node })
        {
            graph.RestoreView(new Vector2(-250, -250), 1f);
            var pointer = Pointer(-1, new Vector2(0, 0));
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.beginDragHandler);
            Assert.That(graph.IsDragging, Is.True, "BeginDrag must acquire the pointer after PointerDown.");
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerExitHandler);
            yield return null;
            Vector2 before = content.anchoredPosition;
            Move(pointer, new Vector2(30, 25));
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
            Assert.That(content.anchoredPosition.x, Is.GreaterThan(before.x));
            Assert.That(content.anchoredPosition.y, Is.GreaterThan(before.y));
            Assert.That(pointer.eligibleForClick, Is.False);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.endDragHandler);
            Assert.That(graph.IsDragging, Is.False);
        }
    }

    [TestCase(false, 2f)]
    [TestCase(true, 1.5f)]
    public void ZoomedGraphFollowsPointerInViewportCoordinates(bool cameraCanvas, float zoom)
    {
        CreateGraph(cameraCanvas);
        graph.RestoreView(new Vector2(-400, -400), zoom);
        var pointer = Pointer(11, Vector2.zero);
        graph.OnPointerDown(pointer);
        graph.OnInitializePotentialDrag(pointer);
        graph.OnBeginDrag(pointer);
        Vector2 before = content.anchoredPosition;
        Vector2 startScreen = pointer.position;
        Move(pointer, new Vector2(40, 30));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, startScreen,
            eventCamera, out Vector2 startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer.position,
            eventCamera, out Vector2 endLocal);
        graph.OnDrag(pointer);
        Assert.That(Vector2.Distance(content.anchoredPosition - before, (endLocal - startLocal)),
            Is.LessThan(.1f), "Content translation must use the pointer movement regardless of content zoom or render camera.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PinchKeepsBothPointersAndContinuesWithRemainingFinger(bool cameraCanvas)
    {
        CreateGraph(cameraCanvas);
        graph.RestoreView(new Vector2(-300, -300), 1f);
        var first = Pointer(10, new Vector2(-50, 0));
        var second = Pointer(20, new Vector2(50, 0));
        graph.OnPointerDown(first);
        graph.OnPointerDown(second);
        graph.OnBeginDrag(first);
        graph.OnBeginDrag(second);
        Vector2 beforePinch = content.anchoredPosition;
        Vector2 centerBefore = (first.position + second.position) * .5f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, centerBefore,
            eventCamera, out Vector2 localBefore);
        Vector2 contentPoint = (localBefore - viewport.rect.min - beforePinch) / content.localScale.x;
        Move(second, new Vector2(25, 0));
        graph.OnDrag(second);
        Assert.That(content.localScale.x, Is.GreaterThan(1f));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,
            (first.position + second.position) * .5f, eventCamera, out Vector2 localAfter);
        Assert.That(Vector2.Distance(content.anchoredPosition + contentPoint * content.localScale.x,
            localAfter - viewport.rect.min), Is.LessThan(.1f),
            "The point beneath the pinch center must stay beneath the fingers while zooming.");
        Move(second, new Vector2(-25, 0));
        graph.OnDrag(second);
        Assert.That(Vector2.Distance(content.anchoredPosition, beforePinch), Is.LessThan(.1f),
            "Reversing the pinch must return to the original view without drifting.");
        graph.OnPointerUp(first);
        graph.OnEndDrag(first);
        Vector2 beforePan = content.anchoredPosition;
        Move(second, new Vector2(15, 20));
        graph.OnDrag(second);
        Assert.That(content.anchoredPosition.x, Is.GreaterThan(beforePan.x));
        Assert.That(content.anchoredPosition.y, Is.GreaterThan(beforePan.y));
        Assert.That(first.eligibleForClick, Is.False);
        Assert.That(second.eligibleForClick, Is.False);
        graph.OnPointerUp(second);
        graph.OnEndDrag(second);
        Assert.That(graph.IsDragging, Is.False);
    }

    [Test]
    public void ShortGraphPressRemainsClickableAndDisableCancelsDrag()
    {
        CreateGraph(false);
        var pointer = Pointer(10, Vector2.zero);
        graph.OnPointerDown(pointer);
        graph.OnInitializePotentialDrag(pointer);
        graph.OnPointerUp(pointer);
        Assert.That(pointer.eligibleForClick, Is.True);
        graph.OnPointerDown(pointer);
        graph.OnBeginDrag(pointer);
        graph.enabled = false;
        Assert.That(graph.IsDragging, Is.False);
        graph.enabled = true;
        graph.OnPointerDown(pointer);
        graph.OnBeginDrag(pointer);
        Vector2 before = content.anchoredPosition;
        Move(pointer, new Vector2(-20, 20));
        graph.OnDrag(pointer);
        Assert.That(Vector2.Distance(content.anchoredPosition, before), Is.GreaterThan(1f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DetailDragStartedOnChildButtonDoesNotRequireParentPointerDown(bool cameraCanvas)
    {
        CreateGraph(cameraCanvas);
        graph.enabled = false;
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        var detail = viewport.gameObject.AddComponent<UIDetailRequirementScrollGesture>();
        detail.Initialize(viewport, content);
        GameObject button = new GameObject("DetailChild", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(content, false);
        var pointer = Pointer(4, Vector2.zero);
        ExecuteEvents.Execute(button, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(button, pointer, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.ExecuteHierarchy(button, pointer, ExecuteEvents.beginDragHandler);
        Assert.That(detail.IsDragging, Is.True, "Button owns PointerDown; the detail viewport must still accept BeginDrag.");
        Vector2 startScreen = pointer.position;
        Move(pointer, new Vector2(0, 30));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, startScreen,
            eventCamera, out Vector2 startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer.position,
            eventCamera, out Vector2 endLocal);
        ExecuteEvents.ExecuteHierarchy(button, pointer, ExecuteEvents.dragHandler);
        Assert.That(content.anchoredPosition.y, Is.EqualTo((endLocal.y - startLocal.y)).Within(.1f));
        Assert.That(pointer.eligibleForClick, Is.False);
        ExecuteEvents.ExecuteHierarchy(button, pointer, ExecuteEvents.endDragHandler);
        Assert.That(detail.IsDragging, Is.False, "EndDrag must release ownership even when PointerUp went to the Button.");
        detail.OnPointerDown(pointer);
        detail.OnBeginDrag(pointer);
        detail.enabled = false;
        Assert.That(detail.IsDragging, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrdinaryPageDragFollowsPointerOnce(bool fromRow)
    {
        CreateGraph(false);
        graph.enabled = false;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        Canvas.ForceUpdateCanvases();
        GameObject button = new GameObject("PageRow", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(UIPageScrollDragForwarder));
        button.transform.SetParent(content, false);
        var pointer = Pointer(-1, Vector2.zero);
        GameObject target = fromRow ? button : viewport.gameObject;
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.beginDragHandler);
        Move(pointer, new Vector2(0, 30));
        Vector2 realPointerPosition = pointer.position;
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
        Assert.That(content.anchoredPosition.y, Is.EqualTo(30f).Within(.1f));
        Assert.That(Vector2.Distance(pointer.position, realPointerPosition), Is.LessThan(.001f),
            "Forwarding must not leave the pointer multiplied for another handler.");
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.endDragHandler);
    }

    [TestCase("UI/Kingdom/KingdomUIRoot")]
    [TestCase("UI/Kingdom/KingdomUIResearchGraph")]
    [TestCase("UI/Kingdom/KingdomUIReviewControls")]
    [TestCase("UI/Kingdom/StoryChapterCard")]
    public void AuthoredScrollOwnersUseNativeScrolling(string path)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        Assert.That(prefab, Is.Not.Null);
        ScrollRect[] scrolls = prefab.GetComponentsInChildren<ScrollRect>(true);
        Assert.That(scrolls, Is.Not.Empty);
        foreach (ScrollRect scroll in scrolls)
            Assert.That(scroll, Is.InstanceOf<ScrollRect>(), path + ": " + scroll.name);
    }

    private void CreateGraph(bool cameraCanvas)
    {
        eventSystemObject = new GameObject("GestureTestEventSystem", typeof(EventSystem));
        root = new GameObject("GestureTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = cameraCanvas ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
        if (cameraCanvas)
        {
            cameraObject = new GameObject("GestureTestCamera", typeof(Camera));
            eventCamera = cameraObject.GetComponent<Camera>();
            eventCamera.orthographic = true;
            eventCamera.transform.position = new Vector3(0, 0, -10);
            canvas.worldCamera = eventCamera;
            canvas.planeDistance = 5;
        }
        raycaster = root.GetComponent<GraphicRaycaster>();
        viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        viewport.SetParent(root.transform, false);
        viewport.sizeDelta = new Vector2(300, 200);
        content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = content.anchorMax = content.pivot = Vector2.zero;
        content.sizeDelta = new Vector2(1200, 900);
        graph = viewport.gameObject.AddComponent<UIResearchGraphGesture>();
        Canvas.ForceUpdateCanvases();
        graph.Initialize(viewport, content);
    }

    private PointerEventData Pointer(int id, Vector2 offset)
    {
        return new PointerEventData(eventSystemObject.GetComponent<EventSystem>())
        {
            pointerId = id,
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(eventCamera, viewport.position) + offset,
            eligibleForClick = true,
            pointerPressRaycast = new RaycastResult { module = raycaster, gameObject = viewport.gameObject }
        };
    }

    private static void Move(PointerEventData pointer, Vector2 delta)
    {
        pointer.position += delta;
        pointer.delta = delta;
    }
}
