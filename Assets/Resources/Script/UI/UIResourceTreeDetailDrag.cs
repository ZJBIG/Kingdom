using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Buttons forward detail drags to the one existing scroll owner.</summary>
public sealed class UIResourceTreeDetailDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private UIDetailRequirementScrollGesture owner;
    public void Bind(UIDetailRequirementScrollGesture value) => owner = value;
    public void OnPointerDown(PointerEventData data) => owner?.OnPointerDown(data);
    public void OnPointerUp(PointerEventData data) => owner?.OnPointerUp(data);
    public void OnInitializePotentialDrag(PointerEventData data) => owner?.OnInitializePotentialDrag(data);
    public void OnBeginDrag(PointerEventData data) => owner?.OnBeginDrag(data);
    public void OnDrag(PointerEventData data) => owner?.OnDrag(data);
    public void OnEndDrag(PointerEventData data) => owner?.OnEndDrag(data);
}
