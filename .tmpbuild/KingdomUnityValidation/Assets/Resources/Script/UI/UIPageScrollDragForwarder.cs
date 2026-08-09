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
    private ScrollRect Owner
    {
        get
        {
            ScrollRect scroll = GetComponentInParent<ScrollRect>();
            return scroll != null && scroll.gameObject != gameObject ? scroll : null;
        }
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        Owner?.OnInitializePotentialDrag(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Owner?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Owner?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Owner?.OnEndDrag(eventData);
    }
}
