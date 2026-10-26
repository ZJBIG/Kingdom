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
    private Vector2 lastPointerLocalPosition;
    private bool canPanHorizontal;
    private bool canPanVertical;
    private Vector2 measuredViewportSize;
    private Vector2 measuredContentSize;
    private float measuredScale = -1f;
    private int pointerId = int.MinValue;
    private int secondPointerId = int.MinValue;
    private Vector2 pointerScreenPosition;
    private Vector2 secondPointerScreenPosition;
    private PointerEventData pointerEventData;
    private PointerEventData secondPointerEventData;
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
        ResetPointers();
    }

    public void Initialize(RectTransform graphViewport, RectTransform graphContent)
    {
        initialized = false;
        ResetPointers();
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
            // Nodes forward the same EventSystem drag lifecycle as the background.
            // Keep ScrollRect only for bounds; it must not apply a second delta.
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
        measuredViewportSize = Vector2.zero;
        measuredContentSize = Vector2.zero;
        measuredScale = -1f;
        initialized = true;
        Debug.Log($"[王国界面] Research graph implementation=ResearchTreeSK-IntegerGrid-v8, viewport={GetSize(viewport)}, content={GetSize(content)}, dragOwner=UIResearchGraphGesture, horizontalScrollable={canPanHorizontal}, verticalScrollable={canPanVertical}, overflow={GetOverflowState()}");
    }

    public void ResetView(float scale = 1f)
    {
        if (content == null)
            return;

        content.localScale = Vector3.one * Mathf.Clamp(scale, MinScale, MaxScale);
        RefreshLayoutBounds(true);
    }

    public void RestoreView(Vector2 position, float scale)
    {
        if (content == null)
            return;

        content.localScale = Vector3.one * Mathf.Clamp(scale, MinScale, MaxScale);
        RefreshLayoutBounds(false);
        content.anchoredPosition = position;
        ClampContentPosition();
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = true;
        if (pointerId == int.MinValue)
            OnPointerDown(eventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !isActiveAndEnabled || !IsInitialized)
            return;
        if (pointerId == int.MinValue)
        {
            pointerId = eventData.pointerId;
            pointerEventData = eventData;
            pointerScreenPosition = eventData.position;
            pointerPotential = true;
            dragging = false;
            lastPointerLocalPosition = GetLocalPointerPosition(eventData);
        }
        else if (eventData.pointerId != pointerId && secondPointerId == int.MinValue &&
                 eventData.pointerId >= 0 && pointerId >= 0)
        {
            secondPointerId = eventData.pointerId;
            secondPointerEventData = eventData;
            secondPointerScreenPosition = eventData.position;
            previousDistance = Vector2.Distance(pointerScreenPosition, secondPointerScreenPosition);
            dragging = true;
            pointerEventData.eligibleForClick = false;
            secondPointerEventData.eligibleForClick = false;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !isActiveAndEnabled || !IsInitialized)
            return;
        if (pointerId == int.MinValue)
            OnPointerDown(eventData);
        if (eventData.pointerId != pointerId && eventData.pointerId != secondPointerId)
            return;
        if (!extentAdjusted)
            RefreshLayoutBounds(false);
        dragging = true;
        eventData.eligibleForClick = false;
        if (secondPointerId == int.MinValue)
            lastPointerLocalPosition = GetLocalPointerPosition(eventData);
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write($"[KingdomPerf] ResearchDrag begin pointer={eventData.pointerId}");
#endif
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || !IsInitialized || eventData.button != PointerEventData.InputButton.Left)
            return;
        if (pointerId == int.MinValue)
            OnBeginDrag(eventData);
        if (eventData.pointerId != pointerId && eventData.pointerId != secondPointerId)
            return;
        eventData.eligibleForClick = false;
        Vector2 previousCenter = (pointerScreenPosition + secondPointerScreenPosition) * .5f;
        if (eventData.pointerId == pointerId)
            pointerScreenPosition = eventData.position;
        else
            secondPointerScreenPosition = eventData.position;

        if (secondPointerId != int.MinValue)
        {
            float distance = Vector2.Distance(pointerScreenPosition, secondPointerScreenPosition);
            if (previousDistance > .001f)
            {
                float scale = Mathf.Clamp(content.localScale.x * distance / previousDistance, MinScale, MaxScale);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, previousCenter,
                    eventData.pressEventCamera, out Vector2 previousLocal);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,
                    (pointerScreenPosition + secondPointerScreenPosition) * .5f,
                    eventData.pressEventCamera, out Vector2 currentLocal);
                // anchoredPosition is relative to the bottom-left anchor, not the viewport pivot.
                previousLocal -= viewport.rect.min;
                currentLocal -= viewport.rect.min;
                content.anchoredPosition = currentLocal -
                    (previousLocal - content.anchoredPosition) * (scale / content.localScale.x);
                content.localScale = new Vector3(scale, scale, 1f);
                RefreshOverflowState();
                ClampContentPosition();
            }
            previousDistance = distance;
            lastPointerLocalPosition = GetLocalPointerPosition(pointerEventData);
            eventData.Use();
            return;
        }
        if (!pointerPotential)
            return;
        dragging = true;
        Vector2 localPointerPosition = GetLocalPointerPosition(eventData);
        Vector2 movement = localPointerPosition - lastPointerLocalPosition;
        lastPointerLocalPosition = localPointerPosition;
        // anchoredPosition belongs to the viewport, so content zoom does not scale this delta.
        if (!canPanHorizontal)
            movement.x = 0f;
        if (!canPanVertical)
            movement.y = 0f;
        content.anchoredPosition += movement;
        ClampContentPosition();
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData) => OnPointerUp(eventData);

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId && eventData.pointerId != secondPointerId)
            return;
        if (dragging)
        {
            eventData.eligibleForClick = false;
            ClampContentPosition();
        }
        if (secondPointerId != int.MinValue)
        {
            if (eventData.pointerId == pointerId)
            {
                pointerId = secondPointerId;
                pointerEventData = secondPointerEventData;
                pointerScreenPosition = secondPointerScreenPosition;
            }
            secondPointerId = int.MinValue;
            secondPointerEventData = null;
            previousDistance = 0f;
            lastPointerLocalPosition = GetLocalPointerPosition(pointerEventData);
            return;
        }
        ResetPointers();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // EventSystem keeps pointerDrag captured outside the pressed node and viewport.
    }

    private void ResetPointers()
    {
        previousDistance = 0f;
        dragging = false;
        pointerPotential = false;
        pointerId = int.MinValue;
        secondPointerId = int.MinValue;
        pointerEventData = null;
        secondPointerEventData = null;
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
        else
        {
            ClampContentPosition();
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
