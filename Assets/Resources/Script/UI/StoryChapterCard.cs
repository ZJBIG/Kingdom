using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Binds one authored, repeatable chapter card without owning story progress.</summary>
public sealed class StoryChapterCard : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private Image illustration;
    [SerializeField] private GameObject illustrationFrame;
    [SerializeField] private StoryChapterLayout layout;
    [SerializeField] private StoryBodyScrollRect bodyScroll;
    [SerializeField] private Button previewButton;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleLabel;
    [SerializeField] private Button navigationButton;

    private StoryChapter chapter;
    private bool completed;
    private Action toggleRequested;
    private Action navigationRequested;
    private Action previewRequested;
    public event Action<StoryChapterCard, float> HeightChanged;
    private float measuredHeight;
    private bool rebuilding;

    public bool IllustrationVisible => illustrationFrame.activeSelf;
    public Sprite Illustration => illustration.sprite;
    public RectTransform Rect => (RectTransform)transform;
    public bool IsExpanded { get; private set; }

    private void Awake()
    {
        toggleButton.onClick.AddListener(RequestToggle);
        navigationButton.onClick.AddListener(RequestNavigation);
        previewButton.onClick.AddListener(RequestPreview);
    }

    private void OnDestroy()
    {
        toggleButton.onClick.RemoveListener(RequestToggle);
        navigationButton.onClick.RemoveListener(RequestNavigation);
        previewButton.onClick.RemoveListener(RequestPreview);
    }

    public void Bind(StoryChapter value, string heading, bool isCompleted,
        bool expanded, bool showNavigation, Color color, TMP_FontAsset font,
        Action onToggle, Action onNavigation, Action onPreview)
    {
        chapter = value;
        completed = isCompleted;
        title.text = heading;
        background.color = color;
        toggleRequested = onToggle;
        navigationRequested = onNavigation;
        previewRequested = onPreview;
        toggleButton.gameObject.SetActive(completed);
        navigationButton.gameObject.SetActive(completed && showNavigation);
        if (font != null)
        {
            title.font = font;
            body.font = font;
            toggleLabel.font = font;
            TMP_Text navigationLabel = navigationButton.GetComponentInChildren<TMP_Text>();
            if (navigationLabel != null)
                navigationLabel.font = font;
        }
        SetExpanded(expanded);
    }

    public void SetExpanded(bool expanded)
    {
        float oldHeight = measuredHeight;
        rebuilding = true;
        expanded &= completed;
        IsExpanded = expanded;
        body.text = !completed ? "这段文明记忆尚未完成。"
            : expanded ? chapter.Summary + "\n\n" + chapter.Body : chapter.Summary;
        bool showImage = expanded && chapter.Illustration != null;
        // A locked card holds no sprite reference, even while its frame is hidden.
        illustration.sprite = showImage ? chapter.Illustration : null;
        illustration.preserveAspect = true;
        illustrationFrame.SetActive(showImage);
        previewButton.gameObject.SetActive(showImage);
        layout.SetIllustrated(showImage);
        bodyScroll.StopMovement();
        bodyScroll.verticalNormalizedPosition = 1;
        toggleLabel.text = expanded ? "收起" : "展开";
        LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
        measuredHeight = Rect.rect.height;
        rebuilding = false;
        if (oldHeight > 0 && Mathf.Abs(measuredHeight - oldHeight) > .1f)
            HeightChanged?.Invoke(this, measuredHeight - oldHeight);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (chapter == null || rebuilding || !isActiveAndEnabled) return;
        float height = Rect.rect.height;
        if (measuredHeight > 0 && Mathf.Abs(height - measuredHeight) > .1f)
            HeightChanged?.Invoke(this, height - measuredHeight);
        measuredHeight = height;
    }

    private void RequestToggle() => toggleRequested?.Invoke();
    private void RequestNavigation() => navigationRequested?.Invoke();
    private void RequestPreview()
    {
        if (completed && IllustrationVisible) previewRequested?.Invoke();
    }
}
