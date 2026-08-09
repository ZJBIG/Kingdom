using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Forwards research-node drag lifecycle events to the authored graph gesture.
/// A short release is not forwarded as a click, so the Button keeps selection.
/// </summary>
public sealed class UIResearchGraphDragForwarder : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private UIResearchGraphGesture Owner => GetComponentInParent<UIResearchGraphGesture>();
    private ScrollRect ScrollOwner => GetComponentInParent<ScrollRect>();

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        Owner?.OnInitializePotentialDrag(eventData);
        ScrollOwner?.OnInitializePotentialDrag(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Owner?.OnBeginDrag(eventData);
        ScrollOwner?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Owner?.OnDrag(eventData);
        ScrollOwner?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Owner?.OnEndDrag(eventData);
        ScrollOwner?.OnEndDrag(eventData);
    }

    public void OnPointerDown(PointerEventData eventData) => Owner?.OnPointerDown(eventData);
    public void OnPointerUp(PointerEventData eventData) => Owner?.OnPointerUp(eventData);
    public void OnPointerExit(PointerEventData eventData) => Owner?.OnPointerExit(eventData);
}
