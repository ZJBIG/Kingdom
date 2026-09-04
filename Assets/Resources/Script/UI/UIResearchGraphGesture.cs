using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ResearchTreeSK-compatible graph interaction.
/// This is the Unity UI equivalent of ResearchTreeSK's
/// ButtonInvisibleDraggable: it owns graph panning and two-finger zoom.
/// </summary>
public sealed class UIResearchGraphGesture : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private const float MinScale = 0.75f;
    private const float MaxScale = 4.0f;
    private RectTransform viewport;
    private RectTransform content;
    private ScrollRect scrollRect;
    private float previousDistance;
    private bool extentAdjusted;
    private Vector2 lastLoggedPosition;
    private bool hasLoggedMotion;
    private bool dragging;
    private bool pointerPotential;
    private Vector2 pointerDownScreenPosition;
    private Vector2 lastPointerLocalPosition;
    private bool canPanHorizontal;
    private bool canPanVertical;
    private bool hasLoggedPotentialDrag;
    private bool hasLoggedBeginDrag;
    private Vector2 measuredViewportSize;
    private Vector2 measuredContentSize;
    private float measuredScale = -1f;
    private bool manualPointerHeld;
    private int manualPointerId = int.MinValue;
    private Vector2 manualLastScreenPosition;
    private Vector2 manualLastLocalPosition;
    private float manualDistance;
    private PointerEventData manualEventData;
    private bool initialized;

    public bool IsDragging => dragging;
    public bool IsInitialized => initialized && viewport != null && content != null;
    public bool CanPanHorizontal => canPanHorizontal;
    public bool CanPanVertical => canPanVertical;

    private void OnEnable()
    {
        initialized = initialized && viewport != null && content != null;
    }

    private void OnDisable()
    {
        previousDistance = 0f;
        dragging = false;
        pointerPotential = false;
        manualPointerHeld = false;
        manualPointerId = int.MinValue;
        manualEventData = null;
        manualDistance = 0f;
    }

    public void Initialize(RectTransform graphViewport, RectTransform graphContent)
    {
        initialized = false;
        viewport = graphViewport;
        content = graphContent;
        if (viewport == null || content == null)
            return;

        Canvas graphCanvas = content.GetComponent<Canvas>();
        if (graphCanvas != null)
        {
            graphCanvas.overrideSorting = false;
            graphCanvas.pixelPerfect = false;
        }
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] CanvasIsolation owner=ResearchGraphContent " +
            $"canvas={(graphCanvas != null)} " +
            "raycaster=False reason=parent-viewport-raycaster");
#endif
        scrollRect = viewport.GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            // Keep ScrollRect as the measured bounds component. Single-pointer
            // movement is driven by the pointer tracker below because a child
            // research Button can terminate Unity's ExecuteHierarchy drag
            // dispatch after BeginDrag.
            scrollRect.enabled = false;
            scrollRect.content = content;
            scrollRect.viewport = viewport;
            scrollRect.horizontal = true;
            scrollRect.vertical = true;
        }
        Canvas.ForceUpdateCanvases();
        RefreshLayoutBounds(true);
        lastLoggedPosition = content == null ? Vector2.zero : content.anchoredPosition;
        hasLoggedMotion = false;
        hasLoggedPotentialDrag = false;
        hasLoggedBeginDrag = false;
        measuredViewportSize = Vector2.zero;
        measuredContentSize = Vector2.zero;
        measuredScale = -1f;
        initialized = true;
        Debug.Log($"[王国界面] Research graph implementation=ResearchTreeSK-IntegerGrid-v8, viewport={GetSize(viewport)}, content={GetSize(content)}, dragOwner=UIResearchGraphGesture, horizontalScrollable={canPanHorizontal}, verticalScrollable={canPanVertical}, overflow={GetOverflowState()}");
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        // Match ResearchTreeSK's ButtonInvisibleDraggable: the graph owns
        // the gesture even when the pointer starts on a research Button, but
        // a short release remains a click. Unity applies the configured
        // pixelDragThreshold before BeginDrag, so this component does not add
        // another movement threshold of its own.
        eventData.useDragThreshold = true;
        pointerPotential = true;
        pointerDownScreenPosition = eventData.position;
        if (!hasLoggedPotentialDrag)
        {
            hasLoggedPotentialDrag = true;
            Debug.Log($"[王国界面] Research graph potential drag: viewport={GetSize(viewport)}, content={GetScaledContentSize()}, horizontalScrollable={canPanHorizontal}, verticalScrollable={canPanVertical}, overflow={GetOverflowState()}");
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (manualPointerHeld)
            return;
        if (eventData.button != PointerEventData.InputButton.Left ||
            viewport == null || content == null ||
            (!canPanHorizontal && !canPanVertical))
            return;
        if (!pointerPotential)
        {
            pointerPotential = true;
            pointerDownScreenPosition = eventData.position;
        }
        // EventSystem has already applied its pixelDragThreshold before
        // IBeginDragHandler is invoked. A second distance gate here creates
        // a dead zone and makes dragging feel intermittent.
        dragging = true;
        lastPointerLocalPosition = GetLocalPointerPosition(eventData);
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchDrag begin pointer={eventData.pointerId} touchCount={Input.touchCount}");
#endif
        if (!hasLoggedBeginDrag)
        {
            hasLoggedBeginDrag = true;
            Debug.Log($"[王国界面] Research graph begin drag: pointer={eventData.position}, local={lastPointerLocalPosition}, range=({Mathf.Min(0f, viewport.rect.width - GetScaledContentSize().x)},{Mathf.Min(0f, viewport.rect.height - GetScaledContentSize().y)})");
        }
        // When ScrollRect is active, do not consume BeginDrag: it must receive
        // the same lifecycle event and perform the continuous movement.
        if (scrollRect == null || !scrollRect.enabled)
            eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (viewport == null || content == null)
            return;

        if (manualPointerHeld)
        {
            ApplyManualScreenPosition(eventData.position);
            return;
        }

        // ScrollRect provides the frame-by-frame single-pointer movement.
        // Keeping a second manual movement path here causes double deltas or
        // event-order-dependent stalls on research node Buttons.
        if (scrollRect != null && scrollRect.enabled)
            return;

        if (!dragging)
        {
            if (!pointerPotential)
                return;
            dragging = true;
            lastPointerLocalPosition = GetLocalPointerPosition(eventData);
        }

        Vector2 localPointerPosition = GetLocalPointerPosition(eventData);
        Vector2 delta = localPointerPosition - lastPointerLocalPosition;
        lastPointerLocalPosition = localPointerPosition;
        float scale = Mathf.Max(.001f, Mathf.Abs(content.localScale.x));
        Vector2 movement = delta / scale;
        if (!canPanHorizontal)
            movement.x = 0f;
        if (!canPanVertical)
            movement.y = 0f;
        content.anchoredPosition += movement;
        ClampContentPosition();
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (manualPointerHeld)
            return;
        if (!dragging)
        {
            pointerPotential = false;
            return;
        }
        dragging = false;
        pointerPotential = false;
        ClampContentPosition();
        if (scrollRect == null || !scrollRect.enabled)
            eventData.Use();
    }

    private void Update()
    {
        UpdateManualPointerDrag();
        if (content == null || Input.touchCount < 2)
        {
            previousDistance = 0f;
            return;
        }

        Touch first = Input.GetTouch(0);
        Touch second = Input.GetTouch(1);
        float distance = Vector2.Distance(first.position, second.position);
        if (previousDistance <= 0f)
        {
            previousDistance = distance;
            return;
        }

        float previousScale = content.localScale.x;
        float scale = Mathf.Clamp(previousScale * (distance / previousDistance), MinScale, MaxScale);
        if (Mathf.Abs(scale - previousScale) > 0.001f)
        {
            content.localScale = new Vector3(scale, scale, 1f);
            if (scrollRect != null)
                scrollRect.StopMovement();
            RefreshOverflowState();
            ClampContentPosition();
        }
        previousDistance = distance;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            viewport == null || content == null)
            return;
        manualPointerHeld = true;
        manualPointerId = eventData.pointerId;
        manualLastScreenPosition = eventData.position;
        manualLastLocalPosition = GetLocalPointerPosition(eventData);
        manualDistance = 0f;
        manualEventData = eventData;
        pointerPotential = true;
        pointerDownScreenPosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!manualPointerHeld || eventData.pointerId != manualPointerId)
            return;
        FinishManualPointerDrag(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Do not end a touch when it leaves a node; the viewport may still be
        // receiving the same pointer and should continue to pan the graph.
        if (eventData.pointerId != manualPointerId || eventData.pointerId >= 0)
            return;
        if (!Input.GetMouseButton(0))
            FinishManualPointerDrag(eventData);
    }

    private void UpdateManualPointerDrag()
    {
        if (!manualPointerHeld || viewport == null || content == null)
            return;
        if (!TryGetManualScreenPosition(out Vector2 screenPosition))
        {
            manualPointerHeld = false;
            manualPointerId = int.MinValue;
            manualEventData = null;
            return;
        }
        ApplyManualScreenPosition(screenPosition);
    }

    private void ApplyManualScreenPosition(Vector2 screenPosition)
    {
        Vector2 screenDelta = screenPosition - manualLastScreenPosition;
        if (screenDelta.sqrMagnitude <= 0.001f)
            return;
        manualLastScreenPosition = screenPosition;
        manualDistance += screenDelta.magnitude;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, screenPosition, null, out Vector2 localPosition);
        Vector2 localDelta = localPosition - manualLastLocalPosition;
        manualLastLocalPosition = localPosition;
        float threshold = EventSystem.current == null ? 3f :
            Mathf.Max(1f, EventSystem.current.pixelDragThreshold);
        if (!dragging && manualDistance >= threshold)
        {
            dragging = true;
            pointerPotential = false;
            if (manualEventData != null)
            {
                manualEventData.dragging = true;
                manualEventData.eligibleForClick = false;
                manualEventData.Use();
            }
            Debug.Log($"[王国界面] Research graph manual drag started: pointer={manualPointerId}, threshold={threshold}");
#if UNITY_EDITOR
            KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchDrag manualBegin pointer={manualPointerId} touchCount={Input.touchCount}");
#endif
        }
        if (!dragging)
            return;

        float scale = Mathf.Max(.001f, Mathf.Abs(content.localScale.x));
        Vector2 movement = localDelta / scale;
        if (!canPanHorizontal)
            movement.x = 0f;
        if (!canPanVertical)
            movement.y = 0f;
        content.anchoredPosition += movement;
        ClampContentPosition();
        if (!hasLoggedMotion && Vector2.Distance(content.anchoredPosition, lastLoggedPosition) > 0.5f)
        {
            hasLoggedMotion = true;
            Debug.Log($"[王国界面] Research graph moved by manual pointer: position={content.anchoredPosition}, bounds={GetScaledContentSize()} vs {viewport.rect.size}");
        }
    }

    private bool TryGetManualScreenPosition(out Vector2 position)
    {
        if (manualPointerId < 0)
        {
            position = Input.mousePosition;
            return Input.GetMouseButton(0);
        }
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == manualPointerId)
            {
                position = touch.position;
                return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            }
        }
        // Unity Device Simulator can report a synthetic touch pointer while
        // driving it with the editor mouse. Keep editor/runtime diagnostics
        // faithful without changing real-device touch handling.
        if (Input.GetMouseButton(0))
        {
            position = Input.mousePosition;
            return true;
        }
        position = Vector2.zero;
        return false;
    }

    private void FinishManualPointerDrag(PointerEventData eventData)
    {
        if (dragging)
            ClampContentPosition();
        if (eventData != null && dragging)
            eventData.eligibleForClick = false;
        dragging = false;
        pointerPotential = false;
        manualPointerHeld = false;
        manualPointerId = int.MinValue;
        manualEventData = null;
        manualDistance = 0f;
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchDrag end touchCount={Input.touchCount}");
#endif
    }

    private void LateUpdate()
    {
        // A runtime-created page can receive its first Canvas rebuild after
        // Initialize. Retry exactly until the real viewport/content sizes are
        // available; ScrollRect cannot calculate a drag range from a zero
        // rect and will otherwise remain immovable for the rest of the page.
        // Bounds are stable while the pointer is dragging. Avoid running a
        // layout measurement/recalculation in the same frame as movement.
        if (!dragging && (!extentAdjusted || HasMeasuredBoundsChanged()))
            RefreshLayoutBounds(false);
        else if (!dragging)
            // Keep the exposed gesture state synchronized with the final
            // Canvas layout even when RectTransform size changes are below
            // the full-bounds refresh threshold.
            RefreshOverflowState();
        if (!hasLoggedMotion && content != null &&
            Vector2.Distance(content.anchoredPosition, lastLoggedPosition) > 0.5f)
        {
            hasLoggedMotion = true;
            Debug.Log($"[王国界面] Research graph moved by UIResearchGraphGesture: position={content.anchoredPosition}, bounds={GetScaledContentSize()} vs {viewport.rect.size}");
        }
    }

    public void RefreshLayoutBounds(bool resetToTop)
    {
        if (viewport == null || content == null)
            return;
        Canvas.ForceUpdateCanvases();
        Vector2 viewportSize = viewport.rect.size;
        Vector2 contentSize = content.rect.size;
        if (viewportSize.x <= 1f || viewportSize.y <= 1f || contentSize.x <= 1f || contentSize.y <= 1f)
            return;
        // Observe the measured bounds only. A short tree remains non-scrollable;
        // a tall tree gets its real range from its measured content height.
        extentAdjusted = true;
        RefreshOverflowState();
        measuredViewportSize = viewportSize;
        measuredContentSize = contentSize;
        measuredScale = content.localScale.x;
        Debug.Log($"[王国界面] Research graph bounds: viewport={viewportSize}, content={contentSize}, scale={content.localScale.x}, dragOwner=UIResearchGraphGesture, scrollRectPresent={scrollRect != null}");
        if (resetToTop)
        {
            SetTopLeftPosition();
        }
    }

    private void SetTopLeftPosition()
    {
        if (viewport == null || content == null)
            return;
        // Content uses a bottom-left RectTransform origin while the graph
        // geometry is converted from the reference tree's top-left grid.
        // The visible top-left therefore means: left edge at zero and the
        // content top aligned with the viewport top.  The previous version
        // used viewport-content on both axes, which incorrectly started at
        // the bottom-right edge; zero on both axes instead starts a tall
        // graph at its bottom.
        Vector2 viewportSize = viewport.rect.size;
        Vector2 scaledContentSize = GetScaledContentSize();
        content.anchoredPosition = new Vector2(
            0f,
            Mathf.Min(0f, viewportSize.y - scaledContentSize.y));
        ClampContentPosition();
    }

    private void ClampContentPosition()
    {
        if (viewport == null || content == null)
            return;
        Vector2 viewportSize = viewport.rect.size;
        Vector2 scaledContentSize = GetScaledContentSize();
        float minX = Mathf.Min(0f, viewportSize.x - scaledContentSize.x);
        float minY = Mathf.Min(0f, viewportSize.y - scaledContentSize.y);
        Vector2 position = content.anchoredPosition;
        Vector2 clamped = new Vector2(
            Mathf.Clamp(position.x, minX, 0f),
            Mathf.Clamp(position.y, minY, 0f));
        // Avoid dirtying the RectTransform every frame with a no-op write.
        // ScrollRect also calls this while a drag settles; repeated identical
        // assignments force a Canvas layout pass each frame.
        if (position != clamped)
            content.anchoredPosition = clamped;
    }

    private void RefreshOverflowState()
    {
        if (viewport == null || content == null)
            return;
        Vector2 viewportSize = viewport.rect.size;
        Vector2 scaledContentSize = GetScaledContentSize();
        bool previousHorizontal = canPanHorizontal;
        bool previousVertical = canPanVertical;
        canPanHorizontal = scaledContentSize.x > viewportSize.x + 0.5f;
        canPanVertical = scaledContentSize.y > viewportSize.y + 0.5f;
        if (previousHorizontal != canPanHorizontal || previousVertical != canPanVertical)
            Debug.Log($"[王国界面] Research graph overflow state: viewport={viewportSize}, content={scaledContentSize}, horizontal={canPanHorizontal}, vertical={canPanVertical}");
        if (scrollRect != null)
        {
            scrollRect.horizontal = canPanHorizontal;
            scrollRect.vertical = canPanVertical;
        }
        if (!canPanHorizontal || !canPanVertical)
        {
            Vector2 position = content.anchoredPosition;
            if (!canPanHorizontal)
                position.x = 0f;
            if (!canPanVertical)
                position.y = 0f;
            content.anchoredPosition = position;
        }
    }

    private Vector2 GetScaledContentSize()
    {
        if (content == null)
            return Vector2.zero;
        Vector3 scale = content.localScale;
        return new Vector2(
            content.rect.width * Mathf.Abs(scale.x),
            content.rect.height * Mathf.Abs(scale.y));
    }

    private bool HasMeasuredBoundsChanged()
    {
        if (viewport == null || content == null)
            return false;
        return Vector2.Distance(viewport.rect.size, measuredViewportSize) > 0.5f ||
            Vector2.Distance(content.rect.size, measuredContentSize) > 0.5f ||
            Mathf.Abs(content.localScale.x - measuredScale) > 0.001f;
    }

    private Vector2 GetLocalPointerPosition(PointerEventData eventData)
    {
        if (viewport == null)
            return eventData.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, eventData.position, eventData.pressEventCamera, out Vector2 localPosition);
        return localPosition;
    }

    private static Vector2 GetSize(RectTransform rect)
    {
        return rect == null ? Vector2.zero : rect.rect.size;
    }

    private string GetOverflowState()
    {
        if (viewport == null || content == null)
            return "none";
        Vector2 viewportSize = viewport.rect.size;
        Vector2 scaledContentSize = GetScaledContentSize();
        return $"x={scaledContentSize.x > viewportSize.x + 0.5f},y={scaledContentSize.y > viewportSize.y + 0.5f}";
    }
}
