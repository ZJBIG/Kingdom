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
    private UIResearchGraphGesture owner;
    private UIResearchGraphGesture Owner =>
        owner != null ? owner : owner = GetComponentInParent<UIResearchGraphGesture>();

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

    public void OnPointerDown(PointerEventData eventData)
    {
        owner = null;
        Owner?.OnPointerDown(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Owner?.OnPointerUp(eventData);
        owner = null;
    }

    public void OnPointerExit(PointerEventData eventData) => Owner?.OnPointerExit(eventData);

    private void OnDisable() => owner = null;
    private void OnTransformParentChanged() => owner = null;
}
