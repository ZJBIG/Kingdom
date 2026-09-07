using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum UnsafeAreaTickerEdge
{
    None,
    Left,
    Right
}

[DisallowMultipleComponent]
public sealed class UnsafeAreaTicker : MonoBehaviour
{
    private const float LayoutEpsilon = 0.0001f;
    private const float ScrollSpeed = 28f * 1.8f;
    private const float HeadlinePauseSeconds = 5f;

    [SerializeField] private Canvas animationCanvas;
    [SerializeField] private Image background;
    [SerializeField] private RectTransform accentLine;
    [SerializeField] private Image accentLineImage;
    [SerializeField] private RectTransform feedViewport;
    [SerializeField] private RectTransform feedContent;
    [SerializeField] private TMP_Text feedText;

    private RectTransform tickerRect;
    private RectTransform safeArea;
    private Vector2 lastSafeAnchorMin = new(-1f, -1f);
    private Vector2 lastSafeAnchorMax = new(-1f, -1f);
    private float lastCanvasWidth = -1f;
    private float contentHeight;
    private string feedCycle = string.Empty;
    private string pendingFeedCycle = string.Empty;
    private bool hasPendingFeedCycle;
    private string[] headlines = Array.Empty<string>();
    private int headlineIndex;
    private bool feedLayoutDirty;
    private bool textVisible;
    private float headlinePauseRemaining;
    private bool preRenderTextSubscribed;

    public UnsafeAreaTickerEdge CurrentEdge { get; private set; }
    public bool TextVisible => textVisible;
    public bool IsConfigured =>
        animationCanvas != null &&
        background != null &&
        accentLine != null &&
        accentLineImage != null &&
        feedViewport != null &&
        feedContent != null &&
        feedText != null;

    private void Awake()
    {
        tickerRect = transform as RectTransform;
        SubscribeToTextMesh();
    }

    private void OnEnable() => SubscribeToTextMesh();

    private void OnDisable()
    {
        if (!preRenderTextSubscribed || feedText == null)
            return;

        feedText.OnPreRenderText -= RotateHeadlineGlyphs;
        preRenderTextSubscribed = false;
    }

    private void Update()
    {
        RefreshLayoutIfChanged();
        RebuildFeedLayoutIfNeeded();

        if (CurrentEdge == UnsafeAreaTickerEdge.None)
            return;

        if (textVisible)
            AnimateFeed(Time.unscaledDeltaTime);
    }

#if UNITY_EDITOR
    public void AdvanceForEditor(float unscaledDeltaTime)
    {
        AnimateFeed(Mathf.Max(0f, unscaledDeltaTime));
    }
#endif

    public void Initialize(RectTransform safeAreaRoot, TMP_FontAsset font)
    {
        safeArea = safeAreaRoot;
        tickerRect ??= transform as RectTransform;
        SubscribeToTextMesh();
        if (feedText != null && feedText.font == null && font != null)
            feedText.font = font;

        RefreshLayoutIfChanged(force: true);
    }

    public void SetFeed(IReadOnlyList<string> notices, TechLevel era, string date)
    {
        string nextCycle = UnsafeAreaTickerRules.BuildFeedCycle(notices, era, date);
        if (string.Equals(feedCycle, nextCycle, StringComparison.Ordinal))
        {
            pendingFeedCycle = string.Empty;
            hasPendingFeedCycle = false;
            return;
        }

        if (string.IsNullOrEmpty(feedCycle) || headlines.Length == 0 || !textVisible)
        {
            pendingFeedCycle = string.Empty;
            hasPendingFeedCycle = false;
            ApplyFeedCycle(nextCycle);
            RebuildFeedLayoutIfNeeded();
            return;
        }

        // Keep the currently visible headline intact. The new cycle is applied
        // only after the current content has completely crossed the viewport.
        pendingFeedCycle = nextCycle;
        hasPendingFeedCycle = true;
    }

    private void ApplyFeedCycle(string nextCycle)
    {
        feedCycle = nextCycle;
        headlines = string.IsNullOrEmpty(nextCycle)
            ? Array.Empty<string>()
            : nextCycle.Split('\n');
        headlineIndex = 0;
        feedLayoutDirty = true;
    }

    private void RefreshLayoutIfChanged(bool force = false)
    {
        if (tickerRect == null || safeArea == null || tickerRect.parent is not RectTransform canvasRect)
            return;

        Vector2 safeMin = safeArea.anchorMin;
        Vector2 safeMax = safeArea.anchorMax;
        float canvasWidth = canvasRect.rect.width;
        float textHorizontalPadding = feedViewport == null
            ? 0f
            : Mathf.Max(0f, feedViewport.offsetMin.x) +
                Mathf.Max(0f, -feedViewport.offsetMax.x);
        if (!force &&
            Approximately(lastSafeAnchorMin, safeMin) &&
            Approximately(lastSafeAnchorMax, safeMax) &&
            Mathf.Abs(lastCanvasWidth - canvasWidth) <= 0.1f)
            return;

        lastSafeAnchorMin = safeMin;
        lastSafeAnchorMax = safeMax;
        lastCanvasWidth = canvasWidth;

        bool active = UnsafeAreaTickerRules.TryCalculateLayout(
            safeMin,
            safeMax,
            canvasWidth,
            textHorizontalPadding,
            out UnsafeAreaTickerEdge edge,
            out Vector2 anchorMin,
            out Vector2 anchorMax,
            out bool showText);

        CurrentEdge = edge;
        textVisible = active && showText;
        tickerRect.anchorMin = anchorMin;
        tickerRect.anchorMax = anchorMax;
        tickerRect.offsetMin = Vector2.zero;
        tickerRect.offsetMax = Vector2.zero;
        ConfigureAccentEdge(edge);
        SetVisualsEnabled(active, textVisible);
        feedLayoutDirty = active && textVisible;

        float designWidth = active ? Mathf.Max(0f, canvasWidth * (anchorMax.x - anchorMin.x)) : 0f;
        Debug.Log(
            $"[KingdomUI] UnsafeAreaTicker screen={Screen.width}x{Screen.height} " +
            $"safeArea={safeArea.rect.size} edge={edge} designWidth={designWidth:0.0} text={textVisible}");
    }

    private void ConfigureAccentEdge(UnsafeAreaTickerEdge edge)
    {
        if (accentLine == null)
            return;

        float edgeAnchor = edge == UnsafeAreaTickerEdge.Right ? 0f : 1f;
        accentLine.anchorMin = new Vector2(edgeAnchor, 0f);
        accentLine.anchorMax = new Vector2(edgeAnchor, 1f);
        accentLine.pivot = new Vector2(edgeAnchor, 0.5f);
        accentLine.anchoredPosition = Vector2.zero;
    }

    private void SetVisualsEnabled(bool active, bool showText)
    {
        if (background != null)
            background.enabled = active;
        if (accentLineImage != null)
            accentLineImage.enabled = active;
        if (feedText != null)
            feedText.enabled = active && showText;
    }

    private void RebuildFeedLayoutIfNeeded()
    {
        if (!feedLayoutDirty || !textVisible || feedText == null || feedContent == null ||
            feedViewport == null || feedViewport.rect.width <= 1f)
            return;

        if (headlines.Length == 0)
        {
            feedText.text = string.Empty;
            contentHeight = 0f;
            feedContent.anchoredPosition = Vector2.zero;
            feedLayoutDirty = false;
            return;
        }
        headlineIndex = Mathf.Clamp(headlineIndex, 0, headlines.Length - 1);
        ShowCurrentHeadline();
        feedLayoutDirty = false;
    }

    private void ShowCurrentHeadline()
    {
        if (headlines.Length == 0 || feedText == null || feedContent == null ||
            feedViewport == null)
            return;

        feedContent.anchorMin = new Vector2(0f, 1f);
        feedContent.anchorMax = new Vector2(1f, 1f);
        feedContent.pivot = new Vector2(0.5f, 1f);
        feedContent.sizeDelta = new Vector2(0f, feedViewport.rect.height + 1f);
        feedContent.anchoredPosition = Vector2.zero;
        feedText.text = BuildVerticalHeadline(headlines[headlineIndex]);
        feedText.ForceMeshUpdate();
        contentHeight = Mathf.Max(
            feedViewport.rect.height + 1f,
            feedText.GetPreferredValues(feedText.text, feedViewport.rect.width, 0f).y);
        feedContent.sizeDelta = new Vector2(0f, contentHeight);
        feedText.SetLayoutDirty();
        feedText.SetVerticesDirty();
        feedText.ForceMeshUpdate(true, true);
        // Place the content bottom exactly on the viewport top. The first
        // downward frame then reveals the headline instead of showing it pre-cut.
        feedContent.anchoredPosition = new Vector2(0f, contentHeight);
        Debug.Log(
            $"[KingdomUI] UnsafeAreaTicker headline={headlineIndex} " +
            $"characters={headlines[headlineIndex].Length} contentHeight={contentHeight:0.0}");
    }

    private void SubscribeToTextMesh()
    {
        if (preRenderTextSubscribed || feedText == null)
            return;

        feedText.OnPreRenderText += RotateHeadlineGlyphs;
        preRenderTextSubscribed = true;
    }

    private static void RotateHeadlineGlyphs(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];
            if (!character.isVisible)
                continue;

            int materialIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
            if (vertexIndex + 3 >= vertices.Length)
                continue;

            Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, 90f);
            for (int j = 0; j < 4; j++)
                vertices[vertexIndex + j] = center + rotation * (vertices[vertexIndex + j] - center);
        }

    }

    private void AnimateFeed(float unscaledDeltaTime)
    {
        if (feedContent == null || feedViewport == null || contentHeight <= 1f)
            return;

        if (headlinePauseRemaining > 0f)
        {
            headlinePauseRemaining = Mathf.Max(0f, headlinePauseRemaining - unscaledDeltaTime);
            if (headlinePauseRemaining > 0f)
                return;

            if (hasPendingFeedCycle)
            {
                string nextCycle = pendingFeedCycle;
                pendingFeedCycle = string.Empty;
                hasPendingFeedCycle = false;
                ApplyFeedCycle(nextCycle);
                RebuildFeedLayoutIfNeeded();
            }
            else if (headlines.Length > 0)
            {
                headlineIndex = (headlineIndex + 1) % headlines.Length;
                ShowCurrentHeadline();
            }
            return;
        }

        float y = feedContent.anchoredPosition.y - ScrollSpeed * unscaledDeltaTime;
        if (y <= -contentHeight)
        {
            feedContent.anchoredPosition = new Vector2(0f, -contentHeight - 1f);
            Debug.Log(
                $"[KingdomUI] UnsafeAreaTicker headline-complete index={headlineIndex} " +
                $"exitY={y:0.00} contentHeight={contentHeight:0.00}");
            headlinePauseRemaining = HeadlinePauseSeconds;
            return;
        }
        feedContent.anchoredPosition = new Vector2(0f, y);
    }

    private static string BuildVerticalHeadline(string headline)
    {
        if (string.IsNullOrEmpty(headline))
            return string.Empty;

        int colorTagEnd = headline.IndexOf("</color>", StringComparison.Ordinal);
        if (colorTagEnd < 0)
            return InsertLineBreaks(ReverseCharacters(headline));

        string prefix = headline.Substring(0, colorTagEnd + "</color>".Length);
        string body = headline.Substring(colorTagEnd + "</color>".Length).TrimStart();
        return prefix + InsertLineBreaks(ReverseCharacters(body));
    }

    private static string ReverseCharacters(string value)
    {
        char[] characters = value.ToCharArray();
        Array.Reverse(characters);
        return new string(characters);
    }

    private static string InsertLineBreaks(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        char[] characters = new char[value.Length * 2 - 1];
        for (int i = 0; i < value.Length; i++)
        {
            if (i > 0)
                characters[i * 2 - 1] = '\n';
            characters[i * 2] = value[i];
        }
        return new string(characters);
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) <= LayoutEpsilon &&
            Mathf.Abs(a.y - b.y) <= LayoutEpsilon;
    }
}
