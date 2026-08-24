using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Keeps a page ScrollRect responsive when a drag starts on a row Button or
/// one of its action buttons. Explicit forwarding removes timing-dependent
/// drag behaviour from selectable page content.
/// </summary>
public sealed class UIPageScrollDragForwarder : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static int activeDragCount;
    private static float lastDragEndTime = float.NegativeInfinity;
    private bool dragActive;
    private ScrollRect owner;

    public static bool IsAnyDragActive => activeDragCount > 0;

    // EndDrag is delivered before the next layout/update pass. Keep the
    // page in its scrolling state briefly so a throttled structural refresh
    // cannot rebuild rows on the same frame that releases the finger.
    public static bool IsRecentlyDragged =>
        activeDragCount > 0 || Time.unscaledTime - lastDragEndTime < 0.25f;

    private ScrollRect Owner
    {
        get
        {
            if (owner == null)
            {
                ScrollRect scroll = GetComponentInParent<ScrollRect>();
                owner = scroll != null && scroll.gameObject != gameObject ? scroll : null;
            }
            return owner;
        }
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        owner = null;
        Owner?.OnInitializePotentialDrag(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!dragActive)
        {
            dragActive = true;
            activeDragCount++;
#if UNITY_EDITOR
            KingdomEditorPerfLog.Write($"[KingdomPerf] PageDrag begin pointer={eventData.pointerId} active={activeDragCount}");
#endif
        }
        Owner?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Owner?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Owner?.OnEndDrag(eventData);
        ClearDragState();
        owner = null;
    }

    private void OnDisable()
    {
        ClearDragState();
        owner = null;
    }

    private void OnTransformParentChanged() => owner = null;

    private void ClearDragState()
    {
        if (dragActive)
        {
            dragActive = false;
            activeDragCount = Mathf.Max(0, activeDragCount - 1);
            lastDragEndTime = Time.unscaledTime;
#if UNITY_EDITOR
            KingdomEditorPerfLog.Write($"[KingdomPerf] PageDrag end active={activeDragCount}");
#endif
        }
    }
}
