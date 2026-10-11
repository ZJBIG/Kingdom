using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Displays an authored illustration overlay without changing story or simulation state.</summary>
public sealed class StoryIllustrationPreview : MonoBehaviour,
    IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text title;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backdropButton;

    private Vector2 fittedSize;
    private Vector2 viewportSize;
    private Vector2 previousDragPosition;
    private float previousPinchDistance;
    private Vector2 previousPinchCenter;
    private Camera eventCamera;
    private bool dragging;
    private bool pinching;

    public bool IsOpen => gameObject.activeInHierarchy && image != null && image.sprite != null;
    public float Zoom { get; private set; } = 1f;
    public Sprite Illustration => image != null ? image.sprite : null;
    public Vector2 ViewPosition => image != null ? image.rectTransform.anchoredPosition : Vector2.zero;

    private void Awake()
    {
        closeButton.onClick.AddListener(Hide);
        resetButton.onClick.AddListener(ResetView);
        backdropButton.onClick.AddListener(Hide);
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;
    }

    private void OnDestroy()
    {
        closeButton.onClick.RemoveListener(Hide);
        resetButton.onClick.RemoveListener(ResetView);
        backdropButton.onClick.RemoveListener(Hide);
    }

    public void Show(Sprite sprite, string heading)
    {
        if (sprite == null)
        {
            Hide();
            return;
        }
        image.sprite = sprite;
        title.text = heading;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        ResetView();
    }

    public void Hide() => gameObject.SetActive(false);

    private void OnDisable()
    {
        if (image != null)
            image.sprite = null;
        if (title != null)
            title.text = string.Empty;
        dragging = false;
        pinching = false;
        previousPinchDistance = 0f;
    }

    public void ResetView()
    {
        if (!IsOpen)
            return;
        viewportSize = viewport.rect.size;
        Vector2 spriteSize = image.sprite.rect.size;
        if (viewportSize.x <= 0f || viewportSize.y <= 0f || spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;
        fittedSize = spriteSize * Mathf.Min(viewportSize.x / spriteSize.x, viewportSize.y / spriteSize.y);
        Zoom = 1f;
        image.rectTransform.sizeDelta = fittedSize;
        image.rectTransform.anchoredPosition = Vector2.zero;
        image.preserveAspect = true;
        previousPinchDistance = 0f;
    }

    public void ZoomAt(float multiplier, Vector2 screenPosition, Camera camera = null)
    {
        if (!IsOpen || multiplier <= 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPosition,
                camera != null ? camera : eventCamera, out Vector2 point))
            return;
        float nextZoom = Mathf.Clamp(Zoom * multiplier, 1f, 4f);
        Vector2 position = image.rectTransform.anchoredPosition;
        image.rectTransform.anchoredPosition = point - (point - position) * (nextZoom / Zoom);
        Zoom = nextZoom;
        image.rectTransform.sizeDelta = fittedSize * Zoom;
        ClampPosition();
    }

    /// <summary>Moves the image in viewport-local UI units.</summary>
    public void Pan(Vector2 delta)
    {
        if (!IsOpen)
            return;
        image.rectTransform.anchoredPosition += delta;
        ClampPosition();
    }

    private void ClampPosition()
    {
        Vector2 limit = Vector2.Max(Vector2.zero, (fittedSize * Zoom - viewport.rect.size) * 0.5f);
        Vector2 position = image.rectTransform.anchoredPosition;
        image.rectTransform.anchoredPosition = new Vector2(
            Mathf.Clamp(position.x, -limit.x, limit.x), Mathf.Clamp(position.y, -limit.y, limit.y));
    }

    public void OnScroll(PointerEventData eventData)
    {
        ZoomAt(Mathf.Pow(1.15f, eventData.scrollDelta.y), eventData.position, eventData.pressEventCamera);
        eventData.Use();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = Input.touchCount < 2;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
            eventData.pressEventCamera, out previousDragPosition);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || pinching || Input.touchCount >= 2)
            return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
                eventData.pressEventCamera, out Vector2 position))
        {
            Pan(position - previousDragPosition);
            previousDragPosition = position;
        }
    }

    public void OnEndDrag(PointerEventData eventData) => dragging = false;

    private void Update()
    {
        if (!IsOpen)
            return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }
        if (viewport.rect.size != viewportSize)
            ResetView();
        if (Input.touchCount < 2)
        {
            if (pinching)
                dragging = false;
            pinching = false;
            previousPinchDistance = 0f;
            return;
        }
        Touch first = Input.GetTouch(0);
        Touch second = Input.GetTouch(1);
        Vector2 center = (first.position + second.position) * 0.5f;
        float distance = Vector2.Distance(first.position, second.position);
        if (!pinching)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, first.position, eventCamera)
                || !RectTransformUtility.RectangleContainsScreenPoint(viewport, second.position, eventCamera))
                return;
            pinching = true;
            dragging = false;
        }
        if (previousPinchDistance > 0f)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, previousPinchCenter,
                eventCamera, out Vector2 previousLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, center,
                eventCamera, out Vector2 currentLocal);
            Pan(currentLocal - previousLocal);
            ZoomAt(distance / previousPinchDistance, center, eventCamera);
        }
        previousPinchDistance = distance;
        previousPinchCenter = center;
    }
}
