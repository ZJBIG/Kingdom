using UnityEngine;
using UnityEngine.EventSystems;

// One controller owns mobile rendering cadence; simulation keeps its own tick.
[DefaultExecutionOrder(-100)]
public sealed class InteractionFrameRateController : MonoBehaviour
{
    private static InteractionFrameRateController instance;
    private EventSystem eventSystem;
    private int previousRate;
    private int previousVSync;
    private bool ownsFrameRate;

    public static int DragThresholdForDpi(float dpi) =>
        Mathf.Clamp(Mathf.RoundToInt(8f * (dpi > 0f && !float.IsNaN(dpi) && !float.IsInfinity(dpi) ? dpi / 160f : 1f)), 8, 48);

    private void Awake()
    {
        instance = this;
        ownsFrameRate = Application.isMobilePlatform;
        previousRate = Application.targetFrameRate;
        previousVSync = QualitySettings.vSyncCount;
        if (ownsFrameRate)
        {
            QualitySettings.vSyncCount = 0;
            double refreshRate = Screen.currentResolution.refreshRateRatio.value;
            foreach (Resolution resolution in Screen.resolutions)
                refreshRate = System.Math.Max(refreshRate, resolution.refreshRateRatio.value);
            Application.targetFrameRate = Mathf.Max(60, Mathf.RoundToInt((float)refreshRate));
        }
    }

    private void Update()
    {
        if (eventSystem != EventSystem.current)
        {
            eventSystem = EventSystem.current;
            if (eventSystem != null) eventSystem.pixelDragThreshold = DragThresholdForDpi(Screen.dpi);
        }
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
