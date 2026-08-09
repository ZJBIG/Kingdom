using UnityEngine;

[ExecuteAlways]
public sealed class SafeAreaFitter : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    private Rect lastSafeArea;
    private Vector2 lastScreenSize;

    private void Awake()
    {
        if (target == null)
            target = transform as RectTransform;
        ApplyIfChanged(true);
    }

    private void OnEnable() => ApplyIfChanged(true);

    private void Update() => ApplyIfChanged(false);

    public static bool TryCalculateAnchors(
        Rect safeArea,
        Vector2 screenSize,
        out Vector2 anchorMin,
        out Vector2 anchorMax)
    {
        anchorMin = Vector2.zero;
        anchorMax = Vector2.one;
        if (screenSize.x <= 0f || screenSize.y <= 0f)
            return false;

        float left = Mathf.Clamp01(safeArea.xMin / screenSize.x);
        float bottom = Mathf.Clamp01(safeArea.yMin / screenSize.y);
        float right = Mathf.Clamp01(safeArea.xMax / screenSize.x);
        float top = Mathf.Clamp01(safeArea.yMax / screenSize.y);
        anchorMin = new Vector2(Mathf.Min(left, right), Mathf.Min(bottom, top));
        anchorMax = new Vector2(Mathf.Max(left, right), Mathf.Max(bottom, top));
        return true;
    }

    private void ApplyIfChanged(bool force)
    {
        if (target == null)
            return;

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        Rect safeArea = Screen.safeArea;
        if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
            return;
        if (!TryCalculateAnchors(safeArea, screenSize, out Vector2 min, out Vector2 max))
            return;

        target.anchorMin = min;
        target.anchorMax = max;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
        lastSafeArea = safeArea;
        lastScreenSize = screenSize;
    }
}
