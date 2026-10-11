using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Layout owns card geometry; authored padding and typography remain in the prefab.
public sealed class StoryChapterLayout : LayoutGroup, ILayoutSelfController
{
    [SerializeField] private RectTransform header;
    [SerializeField] private TMP_Text title;
    [SerializeField] private RectTransform actions;
    [SerializeField] private RectTransform bodyViewport;
    [SerializeField] private TMP_Text body;
    [SerializeField] private float spacing = 16;
    [SerializeField] private float minimumTitleWidth = 400;
    [SerializeField] private float illustrationAspect = 16f / 9f;
    [SerializeField] private float viewportMargin = 24;
    [SerializeField] private float actionTopInset = 14;
    private ScrollRect pageScroll;
    private bool illustrated;
    private float headerHeight;
    private float titleHeight;
    private float actionWidth;
    private float actionHeight;
    private bool stacked;

    public void SetIllustrated(bool value)
    {
        illustrated = value;
        SetDirty();
    }

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        FitWidthToViewport();
        SetLayoutInputForAxis(0, 0, 1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        float width = Mathf.Max(1, rectTransform.rect.width - padding.horizontal);
        // Header has no layout controller, so Unity stops traversal before Actions.
        LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
        actionWidth = LayoutUtility.GetPreferredWidth(actions);
        actionHeight = LayoutUtility.GetPreferredHeight(actions);
        stacked = width - actionWidth - spacing < minimumTitleWidth && actionWidth > 0;
        float titleWidth = stacked ? width : Mathf.Max(1, width - actionWidth - spacing);
        titleHeight = title.GetPreferredValues(title.text, titleWidth, Mathf.Infinity).y;
        headerHeight = stacked ? titleHeight + spacing + actionHeight + actionTopInset
            : Mathf.Max(titleHeight, actionHeight + actionTopInset);
        float height = illustrated ? rectTransform.rect.width / illustrationAspect
            : padding.vertical + headerHeight + spacing + body.GetPreferredValues(body.text, width, Mathf.Infinity).y;
        SetLayoutInputForAxis(height, height, 0, 1);
    }

    public override void SetLayoutHorizontal()
    {
        FitWidthToViewport();
        CalculateLayoutInputVertical();
        float width = Mathf.Max(1, rectTransform.rect.width - padding.horizontal);
        SetChildAlongAxis(header, 0, padding.left, width);
        Place((RectTransform)title.transform, stacked ? width : Mathf.Max(1, width - actionWidth - spacing),
            titleHeight, 0, 0);
        Place(actions, actionWidth, actionHeight, stacked ? 0 : width - actionWidth,
            (stacked ? titleHeight + spacing : 0) + actionTopInset);
        LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
        SetChildAlongAxis(bodyViewport, 0, padding.left, width);
    }

    public override void SetLayoutVertical()
    {
        CalculateLayoutInputVertical();
        SetChildAlongAxis(header, 1, padding.top, headerHeight);
        SetChildAlongAxis(bodyViewport, 1, padding.top + headerHeight + spacing,
            Mathf.Max(0, rectTransform.rect.height - padding.vertical - headerHeight - spacing));
    }

    private void FitWidthToViewport()
    {
        if (!(rectTransform.parent is RectTransform parent)) return;
        if (pageScroll == null) pageScroll = GetComponentInParent<ScrollRect>();
        float width = parent.rect.width;
        if (illustrated && pageScroll != null && pageScroll.viewport != null && pageScroll.viewport.rect.height > viewportMargin)
            width = Mathf.Min(width, (pageScroll.viewport.rect.height - viewportMargin) * illustrationAspect);
        if (width > 0 && Mathf.Abs(rectTransform.rect.width - width) > .1f)
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private static void Place(RectTransform rect, float width, float height, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }
}
