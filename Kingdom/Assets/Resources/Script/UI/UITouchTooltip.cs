using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Mobile-safe long-press tooltip. It deliberately has no mouse-only path;
/// touch and pointer devices can both use the same press gesture.
/// </summary>
public sealed class UITouchTooltip : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private const float HoldSeconds = 0.55f;
    private string tooltip;
    private Coroutine holdRoutine;

    public void SetTooltip(string value) => tooltip = value ?? string.Empty;

    public void OnPointerDown(PointerEventData eventData)
    {
        StopHold();
        holdRoutine = StartCoroutine(ShowAfterHold());
    }

    public void OnPointerUp(PointerEventData eventData) => StopHold();

    public void OnPointerExit(PointerEventData eventData) => StopHold();

    private IEnumerator ShowAfterHold()
    {
        yield return new WaitForSecondsRealtime(HoldSeconds);
        if (!string.IsNullOrWhiteSpace(tooltip))
            GetComponentInParent<KingdomUIRoot>()?.ShowTooltip(tooltip);
        holdRoutine = null;
    }

    private void StopHold()
    {
        if (holdRoutine == null)
            return;
        StopCoroutine(holdRoutine);
        holdRoutine = null;
    }
}
