using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One controller owns mobile rendering cadence; simulation keeps its own tick.
[DefaultExecutionOrder(-100)]
public sealed class InteractionFrameRateController : MonoBehaviour
{
    private static InteractionFrameRateController instance;
    private float interactiveUntil;
    private EventSystem eventSystem;
    private int appliedRate;
    private int previousRate;
    private int previousVSync;
    private bool ownsFrameRate;
    private ScrollRect[] scrolls;

    public static int DragThresholdForDpi(float dpi) =>
        Mathf.Clamp(Mathf.RoundToInt(8f * (dpi > 0f && !float.IsNaN(dpi) && !float.IsInfinity(dpi) ? dpi / 160f : 1f)), 8, 48);

    public static int DesiredFrameRate(bool pointerActive, bool scrolling, float now, float interactionUntil) =>
        pointerActive || scrolling || now < interactionUntil ? 60 : 30;

    public static void NotifyInteraction()
    {
        if (instance != null) instance.interactiveUntil = Time.unscaledTime + .4f;
    }

    private void Awake()
    {
        instance = this;
        scrolls = GetComponentsInChildren<ScrollRect>(true);
        ownsFrameRate = Application.isMobilePlatform;
        previousRate = Application.targetFrameRate;
        previousVSync = QualitySettings.vSyncCount;
        if (ownsFrameRate) QualitySettings.vSyncCount = 0;
    }

    private void Update()
    {
        if (eventSystem != EventSystem.current)
        {
            eventSystem = EventSystem.current;
            if (eventSystem != null) eventSystem.pixelDragThreshold = DragThresholdForDpi(Screen.dpi);
        }
        bool pointerActive = Input.touchCount > 0 || Input.GetMouseButton(0) ||
            Input.GetMouseButton(1) || Input.mouseScrollDelta.sqrMagnitude > 0f;
        if (pointerActive) NotifyInteraction();
        if (!ownsFrameRate) return;
        bool scrolling = UIPageScrollDragForwarder.IsRecentlyDragged;
        for (int i = 0; !scrolling && i < scrolls.Length; i++)
            scrolling = scrolls[i] != null && scrolls[i].isActiveAndEnabled &&
                scrolls[i].velocity.sqrMagnitude > 1f;
        int rate = DesiredFrameRate(pointerActive, scrolling,
            Time.unscaledTime, interactiveUntil);
        if (rate == appliedRate) return;
        Application.targetFrameRate = rate;
        appliedRate = rate;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        if (!ownsFrameRate) return;
        Application.targetFrameRate = previousRate;
        QualitySettings.vSyncCount = previousVSync;
    }
}
