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
    [SerializeField] private LayoutElement illustrationLayout;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text toggleLabel;
    [SerializeField] private Button navigationButton;

    private StoryChapter chapter;
    private bool completed;
    private Action toggleRequested;
    private Action navigationRequested;

    public bool IllustrationVisible => illustrationLayout.gameObject.activeSelf;
    public Sprite Illustration => illustration.sprite;
    public RectTransform Rect => (RectTransform)transform;

    private void Awake()
    {
        toggleButton.onClick.AddListener(RequestToggle);
        navigationButton.onClick.AddListener(RequestNavigation);
    }

    private void OnDestroy()
    {
        toggleButton.onClick.RemoveListener(RequestToggle);
        navigationButton.onClick.RemoveListener(RequestNavigation);
    }

    public void Bind(StoryChapter value, string heading, bool isCompleted,
        bool expanded, bool showNavigation, Color color, TMP_FontAsset font,
        Action onToggle, Action onNavigation)
    {
        chapter = value;
        completed = isCompleted;
        title.text = heading;
        background.color = color;
        toggleRequested = onToggle;
        navigationRequested = onNavigation;
        toggleButton.gameObject.SetActive(completed && !showNavigation);
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
        expanded &= completed;
        body.text = !completed ? "这段文明记忆尚未完成。"
            : expanded ? chapter.Summary + "\n\n" + chapter.Body : chapter.Summary;
        bool showImage = expanded && chapter.Illustration != null;
        // A locked card holds no sprite reference, even while its frame is hidden.
        illustration.sprite = showImage ? chapter.Illustration : null;
        illustration.preserveAspect = true;
        illustrationLayout.gameObject.SetActive(showImage);
        toggleLabel.text = expanded ? "收起" : "展开";
        LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
    }

    private void RequestToggle() => toggleRequested?.Invoke();
    private void RequestNavigation() => navigationRequested?.Invoke();
}
