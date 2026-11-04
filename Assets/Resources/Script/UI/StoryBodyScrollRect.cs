using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class StoryBodyScrollRect : ScrollRect
{
    private static int activeDragCount;
    private static float lastDragEndTime = float.NegativeInfinity;
    private ScrollRect outer;
    private PointerEventData dragPointer;
    private bool draggingOuter;

    public bool IsDragging => dragPointer != null;
    public static bool IsRecentlyDragged =>
        activeDragCount > 0 || Time.unscaledTime - lastDragEndTime < 0.25f;
    public bool IsDraggingOuter => IsDragging && draggingOuter;
    public bool IsOverflowing => content != null && viewport != null &&
        RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, content).size.y > viewport.rect.height + 1f;

    private ScrollRect Outer
    {
        get
        {
            if (outer == null && transform.parent != null)
                outer = transform.parent.GetComponentInParent<ScrollRect>();
            return outer != null && outer.IsActive() ? outer : null;
        }
    }

    public override void OnInitializePotentialDrag(PointerEventData eventData)
    {
        outer = null;
        base.OnInitializePotentialDrag(eventData);
        Outer?.OnInitializePotentialDrag(eventData);
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || IsDragging)
            return;

        dragPointer = eventData;
        activeDragCount++;
        draggingOuter = !IsOverflowing && Outer != null;
        if (draggingOuter)
            Outer.OnBeginDrag(eventData);
        else
            base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (dragPointer == null || dragPointer.pointerId != eventData.pointerId)
            return;

        if (!draggingOuter && Outer != null && (!IsOverflowing || AtBoundary(DragDelta(eventData))))
        {
            base.OnEndDrag(eventData);
            StopMovement();
            draggingOuter = true;
            // Restart at the previous pointer position so handoff neither jumps nor loses this delta.
            Vector2 position = eventData.position;
            eventData.position -= eventData.delta;
            Outer.OnInitializePotentialDrag(eventData);
            Outer.OnBeginDrag(eventData);
            eventData.position = position;
        }

        if (draggingOuter)
            Outer?.OnDrag(eventData);
        else
            base.OnDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (dragPointer == null || dragPointer.pointerId != eventData.pointerId)
            return;

        if (draggingOuter)
            Outer?.OnEndDrag(eventData);
        else
            base.OnEndDrag(eventData);
        ClearDragState();
    }

    public override void OnScroll(PointerEventData eventData)
    {
        if (!IsActive())
            return;

        float direction = -eventData.scrollDelta.y;
        if (Mathf.Abs(eventData.scrollDelta.x) > Mathf.Abs(direction))
            direction = eventData.scrollDelta.x;
        if (Outer != null && (!IsOverflowing || AtBoundary(direction)))
        {
            StopMovement();
            Outer.OnScroll(eventData);
        }
        else
            base.OnScroll(eventData);
    }

    protected override void OnDisable()
    {
        if (dragPointer != null && draggingOuter && outer != null)
        {
            outer.OnEndDrag(dragPointer);
            outer.StopMovement();
        }
        ClearDragState();
        outer = null;
        base.OnDisable();
    }

    private void ClearDragState()
    {
        if (dragPointer != null)
        {
            activeDragCount = Mathf.Max(0, activeDragCount - 1);
            lastDragEndTime = Time.unscaledTime;
        }
        dragPointer = null;
        draggingOuter = false;
    }

    private bool AtBoundary(float direction) =>
        direction > 0f && verticalNormalizedPosition <= 0.001f ||
        direction < 0f && verticalNormalizedPosition >= 0.999f;

    private float DragDelta(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
            eventData.pressEventCamera, out Vector2 current);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position - eventData.delta,
            eventData.pressEventCamera, out Vector2 previous);
        return current.y - previous.y;
    }
}
