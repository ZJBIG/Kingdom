using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Displays an authored illustration overlay without changing story or simulation state.</summary>
public sealed class StoryIllustrationPreview : MonoBehaviour,
    IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler
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
    private int dragPointerId = int.MinValue;
    private bool pinching;
    private bool movedSincePress;

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
        dragPointerId = int.MinValue;
        pinching = false;
        movedSincePress = false;
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
        RectTransform frame = image.transform.Find("ArtworkFrame") as RectTransform;
        Vector2 contentSize = viewportSize;
        if (frame != null) contentSize -= Vector2.Max(Vector2.zero, frame.sizeDelta);
        fittedSize = spriteSize * Mathf.Min(contentSize.x / spriteSize.x, contentSize.y / spriteSize.y);
        Zoom = 1f;
        image.rectTransform.sizeDelta = fittedSize;
        image.rectTransform.anchoredPosition = Vector2.zero;
        image.preserveAspect = true;
        previousPinchDistance = 0f;
        dragPointerId = int.MinValue;
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

    public void OnPointerDown(PointerEventData eventData) => movedSincePress = false;

    public void OnBeginDrag(PointerEventData eventData)
    {
        movedSincePress = true;
        dragPointerId = Input.touchCount < 2 ? eventData.pointerId : int.MinValue;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
            eventData.pressEventCamera, out previousDragPosition);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (pinching || Input.touchCount >= 2)
            return;
        if (dragPointerId != eventData.pointerId)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position - eventData.delta,
                    eventData.pressEventCamera, out previousDragPosition))
                return;
            dragPointerId = eventData.pointerId;
        }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position,
                eventData.pressEventCamera, out Vector2 position))
        {
            Pan((position - previousDragPosition));
            previousDragPosition = position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == dragPointerId)
            dragPointerId = int.MinValue;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !IsOpen)
            return;
        bool moved = movedSincePress;
        movedSincePress = false;
        if (!moved && !pinching && eventData.eligibleForClick &&
            !RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform,
                eventData.position, eventData.pressEventCamera))
            Hide();
    }

    private void Update()
    {
        if (!IsOpen || OnboardingInputGate.BlocksNavigation)
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
                dragPointerId = int.MinValue;
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
            movedSincePress = true;
            dragPointerId = int.MinValue;
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
