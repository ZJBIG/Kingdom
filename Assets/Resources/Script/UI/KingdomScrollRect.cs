using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KingdomScrollRect : ScrollRect
{
    public const float DragSpeedMultiplier = 1.5f;
    private Vector2 dragOrigin;

    public override void OnBeginDrag(PointerEventData eventData)
    {
        dragOrigin = eventData.position;
        base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        Vector2 position = eventData.position;
        // Scale only the scroll owner's input; forwarded handlers still receive the real pointer.
        eventData.position = dragOrigin + (position - dragOrigin) * DragSpeedMultiplier;
        try
        {
            base.OnDrag(eventData);
        }
        finally
        {
            eventData.position = position;
        }
    }
}
