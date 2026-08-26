using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private const float DetailFooterHeight = 82f;

    /// <summary>
    /// Builds the replacement detail surface. The old authored children are
    /// deliberately isolated before this tree is created so stale masks,
    /// nested ScrollRects and missing scripts cannot participate in input.
    /// </summary>
    private bool BuildDetailUI()
    {
        if (detailPanel == null)
            return false;

        for (int i = detailPanel.childCount - 1; i >= 0; i--)
        {
            Transform legacy = detailPanel.GetChild(i);
            legacy.gameObject.SetActive(false);
            Destroy(legacy.gameObject);
        }

        RectTransform shell = CreateRect("DetailUI", detailPanel);
        Image shellImage = shell.gameObject.AddComponent<Image>();
        shellImage.color = new Color(.075f, .095f, .105f, .98f);
        shellImage.raycastTarget = false;

        RectTransform header = CreateRect("Header", shell);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.offsetMin = new Vector2(22f, -68f);
        header.offsetMax = new Vector2(-22f, -14f);
        Image headerImage = header.gameObject.AddComponent<Image>();
        headerImage.color = new Color(.13f, .16f, .17f, 1f);
        headerImage.raycastTarget = false;
        TMP_Text headerText = CreateText("HeaderLabel", header, "详细信息",
            TextSecondary, TextAlignmentOptions.MidlineLeft);
        headerText.rectTransform.offsetMin = new Vector2(18f, 0f);
        headerText.rectTransform.offsetMax = new Vector2(-18f, 0f);

        detailScrollViewport = CreateRect("DetailScrollViewport", shell);
        detailScrollViewport.anchorMin = Vector2.zero;
        detailScrollViewport.anchorMax = Vector2.one;
        detailScrollViewport.offsetMin = new Vector2(20f, DetailFooterHeight + 12f);
        detailScrollViewport.offsetMax = new Vector2(-20f, -76f);
        Image viewportImage = detailScrollViewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;
        detailScrollViewport.gameObject.AddComponent<RectMask2D>();
        EnsureNestedCanvas(detailScrollViewport);
        detailScroll = detailScrollViewport.gameObject.AddComponent<ScrollRect>();
        detailScroll.horizontal = false;
        detailScroll.vertical = true;
        detailScroll.movementType = ScrollRect.MovementType.Clamped;
        detailScroll.inertia = false;
        detailScroll.enabled = false;

        detailScrollContent = CreateRect("DetailScrollContent", detailScrollViewport);
        detailScrollContent.anchorMin = new Vector2(0f, 1f);
        detailScrollContent.anchorMax = new Vector2(1f, 1f);
        detailScrollContent.pivot = new Vector2(.5f, 1f);
        detailScrollContent.anchoredPosition = Vector2.zero;
        detailScrollContent.sizeDelta = new Vector2(0f, 1f);
        detailScroll.content = detailScrollContent;
        detailScroll.viewport = detailScrollViewport;

        detailBody = CreateText("Body", detailScrollContent, string.Empty,
            TextPrimary, TextAlignmentOptions.TopLeft);
        detailBody.richText = true;
        detailBody.enableWordWrapping = true;
        detailBody.rectTransform.anchorMin = new Vector2(0f, 1f);
        detailBody.rectTransform.anchorMax = new Vector2(1f, 1f);
        detailBody.rectTransform.pivot = new Vector2(.5f, 1f);
        detailBody.rectTransform.offsetMin = new Vector2(24f, -300f);
        detailBody.rectTransform.offsetMax = new Vector2(-24f, -16f);

        flowHost = CreateRect("BuildingOutput", detailScrollContent);
        flowHost.gameObject.SetActive(false);
        flowContent = CreateRect("FlowContent", flowHost);
        flowContent.anchorMin = new Vector2(0f, 1f);
        flowContent.anchorMax = new Vector2(1f, 1f);
        flowContent.pivot = new Vector2(.5f, 1f);
        flowContent.sizeDelta = new Vector2(0f, 86f);
        TMP_Text flowHeading = CreateText("Heading", flowContent, "产出 / 消耗", TextSecondary,
            TextAlignmentOptions.TopLeft);
        RectTransform flowHeadingRect = flowHeading.rectTransform;
        flowHeadingRect.anchorMin = new Vector2(0f, 1f);
        flowHeadingRect.anchorMax = new Vector2(1f, 1f);
        flowHeadingRect.pivot = new Vector2(.5f, 1f);
        flowHeadingRect.sizeDelta = new Vector2(0f, 38f);
        flowHeadingRect.offsetMin = new Vector2(4f, 0f);
        flowHeadingRect.offsetMax = new Vector2(-4f, 0f);
        flowHeadingRect.anchoredPosition = new Vector2(0f, -4f);
        requirementHost = CreateRect("BuildingRequirements", detailScrollContent);
        requirementHost.gameObject.SetActive(false);
        requirementContent = CreateRect("RequirementContent", requirementHost);
        requirementContent.anchorMin = new Vector2(0f, 1f);
        requirementContent.anchorMax = new Vector2(1f, 1f);
        requirementContent.pivot = new Vector2(.5f, 1f);
        requirementContent.sizeDelta = new Vector2(0f, 90f);
        TMP_Text requirementHeading = CreateText("Heading", requirementContent, string.Empty,
            TextSecondary, TextAlignmentOptions.TopLeft);
        RectTransform headingRect = requirementHeading.rectTransform;
        headingRect.anchorMin = new Vector2(0f, 1f);
        headingRect.anchorMax = new Vector2(1f, 1f);
        headingRect.pivot = new Vector2(.5f, 1f);
        headingRect.sizeDelta = new Vector2(0f, 38f);
        headingRect.offsetMin = new Vector2(4f, 0f);
        headingRect.offsetMax = new Vector2(-4f, 0f);
        headingRect.anchoredPosition = new Vector2(0f, -4f);
        TMP_Text none = CreateText("None", requirementContent, "无需额外资源",
            TextSecondary, TextAlignmentOptions.TopLeft);
        RectTransform noneRect = none.rectTransform;
        noneRect.anchorMin = new Vector2(0f, 1f);
        noneRect.anchorMax = new Vector2(1f, 1f);
        noneRect.pivot = new Vector2(.5f, 1f);
        noneRect.sizeDelta = new Vector2(0f, 42f);
        noneRect.offsetMin = new Vector2(4f, 0f);
        noneRect.offsetMax = new Vector2(-4f, 0f);
        noneRect.anchoredPosition = new Vector2(0f, -46f);
        none.gameObject.SetActive(false);

        RectTransform footer = CreateRect("Footer", shell);
        footer.anchorMin = Vector2.zero;
        footer.anchorMax = new Vector2(1f, 0f);
        footer.offsetMin = new Vector2(20f, 12f);
        footer.offsetMax = new Vector2(-20f, 12f + DetailFooterHeight);

        detailPaymentButtonText = null;
        detailActionButtonText = null;
        detailPaymentButton = CreateButton("Payment", footer, "支付资源", new Color(.28f, .22f, .12f, 1f));
        detailPaymentButton.gameObject.SetActive(false);
        detailActionButton = CreateButton("Action", footer, "执行", new Color(.18f, .31f, .27f, 1f));
        detailActionButton.gameObject.SetActive(false);
        RectTransform paymentRect = detailPaymentButton.transform as RectTransform;
        paymentRect.anchorMin = Vector2.zero;
        paymentRect.anchorMax = new Vector2(.48f, 1f);
        paymentRect.offsetMin = Vector2.zero;
        paymentRect.offsetMax = Vector2.zero;
        RectTransform actionRect = detailActionButton.transform as RectTransform;
        actionRect.anchorMin = new Vector2(.52f, 0f);
        actionRect.anchorMax = Vector2.one;
        actionRect.offsetMin = Vector2.zero;
        actionRect.offsetMax = Vector2.zero;

        requirementGesture = detailScrollViewport.gameObject.AddComponent<UIDetailRequirementScrollGesture>();
        requirementGesture.Initialize(detailScrollViewport, detailScrollContent);
        requirementGesture.enabled = true;

        Canvas.ForceUpdateCanvases();
        Debug.Log($"[王国界面] Detail UI rebuilt: legacyChildren=discarded, viewport={detailScrollViewport.rect.size}, content={detailScrollContent.rect.size}, footer={DetailFooterHeight:0}");
        return true;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private TMP_Text CreateText(string name, Transform parent, string value,
        Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        // TMP defaults to a small centered rect when created by script. That
        // makes headings wrap one glyph per line and can produce invalid
        // measurements when the parent is resized. Text created here is a
        // content element by default; callers can override this afterwards.
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = sharedFontAsset;
        text.text = value;
        text.fontSize = 30f;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);
        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        text.font = sharedFontAsset;
        text.text = label;
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextPrimary;
        text.raycastTarget = false;
        return button;
    }

    private GameObject CreateRuntimeDetailRow(string name, RectTransform parent, float top)
    {
        GameObject row = new GameObject(name, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 72f);
        rect.anchoredPosition = new Vector2(0f, top);
        Image background = row.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, .035f);
        background.raycastTarget = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(row.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, .5f);
        iconRect.anchorMax = new Vector2(0f, .5f);
        iconRect.pivot = new Vector2(.5f, .5f);
        iconRect.sizeDelta = new Vector2(48f, 48f);
        iconRect.anchoredPosition = new Vector2(30f, 0f);
        iconObject.GetComponent<Image>().raycastTarget = false;

        TMP_Text labelText = CreateText("Label", row.transform, string.Empty,
            TextPrimary, TextAlignmentOptions.MidlineLeft);
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = new Vector2(.58f, 1f);
        labelText.rectTransform.offsetMin = new Vector2(66f, 0f);
        labelText.rectTransform.offsetMax = Vector2.zero;

        TMP_Text amountText = CreateText("Amount", row.transform, string.Empty,
            TextSecondary, TextAlignmentOptions.MidlineRight);
        amountText.rectTransform.anchorMin = new Vector2(.58f, 0f);
        amountText.rectTransform.anchorMax = Vector2.one;
        amountText.rectTransform.offsetMin = Vector2.zero;
        amountText.rectTransform.offsetMax = new Vector2(-18f, 0f);
        return row;
    }
}
