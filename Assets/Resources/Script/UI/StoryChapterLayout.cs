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
    private bool measurementsDirty = true;
    private float measuredWidth;
    private Vector2 measuredViewportSize;
    private string measuredTitle;
    private string measuredBody;
    private int measuredActions;
    private TMP_FontAsset measuredTitleFont;
    private TMP_FontAsset measuredBodyFont;
    private float measuredTitleFontSize;
    private float measuredBodyFontSize;
    private float preferredHeight;

#if UNITY_EDITOR
    public int MeasurementCountForEditor { get; private set; }
#endif

    public void SetIllustrated(bool value)
    {
        illustrated = value;
        measurementsDirty = true;
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
        EnsureMeasurements();
        SetLayoutInputForAxis(preferredHeight, preferredHeight, 0, 1);
    }

    private void EnsureMeasurements()
    {
        FitWidthToViewport();
        float width = Mathf.Max(1, rectTransform.rect.width - padding.horizontal);
        Vector2 viewportSize = pageScroll != null && pageScroll.viewport != null
            ? pageScroll.viewport.rect.size : Vector2.zero;
        int visibleActions = 0;
        for (int i = 0; i < actions.childCount; i++)
            visibleActions = unchecked(visibleActions * 31 + (actions.GetChild(i).gameObject.activeSelf ? 1 : 0));
        if (!measurementsDirty && Mathf.Abs(width - measuredWidth) <= .1f &&
            viewportSize == measuredViewportSize && title.text == measuredTitle && body.text == measuredBody &&
            visibleActions == measuredActions && title.font == measuredTitleFont && body.font == measuredBodyFont &&
            title.fontSize == measuredTitleFontSize && body.fontSize == measuredBodyFontSize)
            return;
        measurementsDirty = false;
        measuredWidth = width;
        measuredViewportSize = viewportSize;
        measuredTitle = title.text;
        measuredBody = body.text;
        measuredActions = visibleActions;
        measuredTitleFont = title.font;
        measuredBodyFont = body.font;
        measuredTitleFontSize = title.fontSize;
        measuredBodyFontSize = body.fontSize;
#if UNITY_EDITOR
        MeasurementCountForEditor++;
#endif
        // Header has no layout controller, so Unity stops traversal before Actions.
        LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
        actionWidth = LayoutUtility.GetPreferredWidth(actions);
        actionHeight = LayoutUtility.GetPreferredHeight(actions);
        stacked = width - actionWidth - spacing < minimumTitleWidth && actionWidth > 0;
        float titleWidth = stacked ? width : Mathf.Max(1, width - actionWidth - spacing);
        titleHeight = title.GetPreferredValues(title.text, titleWidth, Mathf.Infinity).y;
        headerHeight = stacked ? titleHeight + spacing + actionHeight + actionTopInset
            : Mathf.Max(titleHeight, actionHeight + actionTopInset);
        preferredHeight = illustrated ? rectTransform.rect.width / illustrationAspect
            : padding.vertical + headerHeight + spacing + body.GetPreferredValues(body.text, width, Mathf.Infinity).y;
    }

    public override void SetLayoutHorizontal()
    {
        EnsureMeasurements();
        float width = Mathf.Max(1, rectTransform.rect.width - padding.horizontal);
        SetChildAlongAxis(header, 0, padding.left, width);
        Place((RectTransform)title.transform, stacked ? width : Mathf.Max(1, width - actionWidth - spacing),
            titleHeight, 0, 0);
        bool actionSizeChanged = Vector2.Distance(actions.rect.size, new Vector2(actionWidth, actionHeight)) > .1f;
        Place(actions, actionWidth, actionHeight, stacked ? 0 : width - actionWidth,
            (stacked ? titleHeight + spacing : 0) + actionTopInset);
        if (actionSizeChanged) LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
        SetChildAlongAxis(bodyViewport, 0, padding.left, width);
    }

    public override void SetLayoutVertical()
    {
        EnsureMeasurements();
        SetChildAlongAxis(header, 1, padding.top, headerHeight);
        SetChildAlongAxis(bodyViewport, 1, padding.top + headerHeight + spacing,
            Mathf.Max(0, rectTransform.rect.height - padding.vertical - headerHeight - spacing));
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        measurementsDirty = true;
        base.OnValidate();
    }
#endif

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
