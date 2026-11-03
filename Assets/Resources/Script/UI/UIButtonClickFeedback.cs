using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// PointerUp precedes Button.onClick, so feedback reaches audio before page work.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class UIButtonClickFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Button button;
    private int pointerId = int.MinValue;
    private float pressedAt;

    private void Awake() => button = GetComponent<Button>();

    public void OnPointerDown(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || !button.IsInteractable() ||
            !OnboardingInputGate.Allows(transform)) return;
        pointerId = data.pointerId;
        pressedAt = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData data)
    {
        bool matching = pointerId == data.pointerId;
        pointerId = int.MinValue;
        GameObject hit = data.pointerCurrentRaycast.gameObject;
        if (!matching || data.dragging || !data.eligibleForClick ||
            Time.unscaledTime - pressedAt >= .55f || !button.IsInteractable() ||
            !OnboardingInputGate.Allows(transform) || hit == null ||
            ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) != gameObject) return;
        UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Click);
    }

    private void OnDisable() => pointerId = int.MinValue;
}
