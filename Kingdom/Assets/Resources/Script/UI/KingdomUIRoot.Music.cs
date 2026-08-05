using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private readonly Dictionary<string, Button> musicTrackButtons = new();
    private Button musicPlayPauseButton;
    private ScrollRect musicListScroll;

    private static MusicManager FindMusicManager()
    {
        return FindObjectOfType<MusicManager>();
    }

    private void BuildMusicPage(RectTransform parent)
    {
        Transform existing = parent.Find("MusicSurface");
        if (existing != null && musicPageBuilt)
            return;
        if (existing != null)
            Destroy(existing.gameObject);

        musicTrackButtons.Clear();
        musicPageBuilt = true;
        RectTransform surface = Rect("MusicSurface", parent, Vector2.zero, Vector2.one,
            new Vector2(18, 18), new Vector2(-18, -18));
        PanelRect("Surface", surface, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
        Label("Heading", surface, "MUSIC PLAYER", 32, Copper, new Vector2(0, 1), Vector2.one,
            new Vector2(28, -64), new Vector2(-28, -18));

        // Keep the control panel compact while leaving enough vertical room
        // for the always-visible time slider and the two setting sliders.
        RectTransform controls = Rect("Controls", surface, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(28, -349), new Vector2(-28, -31));
        PanelRect("Surface", controls, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelRaised);
        musicCurrentLabel = Label("Current", controls, "NOW PLAYING", 24, TextPrimary,
            new Vector2(0, 1), new Vector2(.40f, 1), new Vector2(24, -58), new Vector2(-12, -18));
        musicTimeLabel = Label("Time", controls, "00:00 / 00:00", 22, TextSecondary,
            new Vector2(.42f, 1), new Vector2(1, 1), new Vector2(12, -58), new Vector2(-24, -18));
        musicTimeLabel.alignment = TextAlignmentOptions.MidlineRight;

        Label("TimeSeekLabel", controls, "TIME / SEEK", 16, TextSecondary,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -116), new Vector2(260, -92));
        musicProgressSeekHandler = SeekMusicFromSlider;
        musicProgressSlider = CreateMusicSlider("TimeSeek", controls,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24, -152), new Vector2(-24, -130),
            0f, 0f, false, musicProgressSeekHandler);
        AddPointerState(musicProgressSlider, () => musicProgressDragging = true,
            () => musicProgressDragging = false);

        Color musicButton = new(.11f, .15f, .15f, 1f);
        musicPlayPauseButton = Button("PlayPause", controls, "PAUSE", Positive,
            Vector2.zero, Vector2.zero, new Vector2(24, 24), new Vector2(220, 72), ToggleMusicPlayback);
        musicPlayPauseButton.transition = Selectable.Transition.None;
        Button previousButton = Button("Previous", controls, "PREV", musicButton,
            Vector2.zero, Vector2.zero, new Vector2(232, 24), new Vector2(428, 72),
            () => PlayRelativeMusicTrack(-1));
        Button nextButton = Button("Next", controls, "NEXT", musicButton,
            Vector2.zero, Vector2.zero, new Vector2(440, 24), new Vector2(636, 72),
            () => PlayRelativeMusicTrack(1));
        Button stopButton = Button("Stop", controls, "STOP", Error,
            Vector2.zero, Vector2.zero, new Vector2(648, 24), new Vector2(844, 72), StopMusicPlayback);

        musicVolumeValueLabel = Label("VolumeValue", controls, "音量 100%", 18, TextPrimary,
            new Vector2(0f, 0f), new Vector2(.48f, 0f), new Vector2(24, 128), new Vector2(-12, 152));
        musicVolumeSlider = CreateMusicSlider("Volume", controls,
            new Vector2(0f, 0f), new Vector2(.48f, 0f), new Vector2(24, 92), new Vector2(-12, 116),
            0f, 1f, false,
            value => { MusicManager manager = FindMusicManager(); if (manager != null) manager.SetVolume(value); });

        musicGapValueLabel = Label("GapValue", controls, "音乐间隙 5.00", 18, TextPrimary,
            new Vector2(.52f, 0f), new Vector2(1f, 0f), new Vector2(12, 128), new Vector2(-24, 152));
        musicGapSlider = CreateMusicSlider("Gap", controls,
            new Vector2(.52f, 0f), new Vector2(1f, 0f), new Vector2(12, 92), new Vector2(-24, 116),
            0f, 30f, false,
            value => { MusicManager manager = FindMusicManager(); if (manager != null) manager.SetGapSeconds(value); });
        AddPointerState(musicVolumeSlider, null, null);
        AddPointerState(musicGapSlider, null, null);

        ConfigureMusicButtonText(musicPlayPauseButton, "PAUSE");
        ConfigureMusicButtonText(previousButton, "PREV");
        ConfigureMusicButtonText(nextButton, "NEXT");
        ConfigureMusicButtonText(stopButton, "STOP");
        ConfigureMusicText(controls.Find("TimeSeekLabel")?.GetComponent<TMP_Text>(), "TIME / SEEK");
        ConfigureMusicText(musicVolumeValueLabel, "音量 100%");
        ConfigureMusicText(musicGapValueLabel, "音乐间隙 5.00");
        EnsureMusicValueLabelVisible(musicVolumeValueLabel);
        EnsureMusicValueLabelVisible(musicGapValueLabel);
        ConfigureMusicText(musicCurrentLabel, "NOW PLAYING");
        BringMusicTextToFront(musicTimeLabel);
        BringMusicTextToFront(musicVolumeValueLabel);
        BringMusicTextToFront(musicGapValueLabel);
        BringMusicTextToFront(musicCurrentLabel);

        MusicManager manager = FindMusicManager();
        if (manager != null && manager.Tracks.Count == 0)
            manager.RebuildCatalog();
        List<MusicManager.MusicTrack> displayTracks = BuildMusicDisplayTracks(manager);
        Label("ListHeading", surface, "MUSIC LIST", 26, Copper, new Vector2(0, 1), Vector2.one,
            new Vector2(28, -397), new Vector2(-28, -359));
        RectTransform listViewport = Rect("TrackListViewport", surface, new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(28, 24), new Vector2(-28, -431));
        PanelRect("Surface", listViewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Panel);
        listViewport.gameObject.AddComponent<RectMask2D>();
        musicListScroll = listViewport.gameObject.AddComponent<ScrollRect>();
        musicListScroll.viewport = listViewport;
        musicListScroll.horizontal = false;
        musicListScroll.vertical = true;
        musicListScroll.inertia = true;
        musicListScroll.movementType = ScrollRect.MovementType.Clamped;
        musicListScroll.scrollSensitivity = 24f;
        listViewport.gameObject.AddComponent<UIMusicListDragForwarder>();
        RectTransform trackContent = Rect("TrackList", listViewport, new Vector2(0, 1), new Vector2(1, 1),
            Vector2.zero, Vector2.zero);
        trackContent.pivot = new Vector2(.5f, 1f);
        trackContent.sizeDelta = Vector2.zero;
        musicListScroll.content = trackContent;
        musicTrackList = trackContent;
        if (displayTracks.Count == 0)
        {
            Label("Empty", musicTrackList, "NO MUSIC FOUND", 24, TextSecondary,
                new Vector2(0, 1), Vector2.one, new Vector2(18, -64), new Vector2(-18, 0));
            nextRowTop = 0f;
            Canvas.ForceUpdateCanvases();
            Debug.Log($"[KingdomUI] Music list built: tracks=0, viewport={listViewport.rect.size}, content={musicTrackList.rect.size}");
            return;
        }

        float contentHeight = 0f;
        for (int i = 0; i < displayTracks.Count; i++)
        {
            MusicManager.MusicTrack track = displayTracks[i];
            GameObject rowObject = KingdomUIPrefabLibrary.Instantiate(
                KingdomUIPrefabLibrary.MusicTrack, musicTrackList);
            if (rowObject == null)
            {
                Debug.LogWarning("[KingdomUI] MusicTrack prefab unavailable; using runtime row: " + track.Id);
                rowObject = new GameObject("KingdomUIMusicTrack_" + track.Id, typeof(RectTransform));
                rowObject.transform.SetParent(musicTrackList, false);
            }
            rowObject.SetActive(true);
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            // A newly-instantiated prefab can report a stale rect until the
            // first canvas rebuild. The prefab's serialized sizeDelta is the
            // source of truth for the row height in that first frame.
            float rowHeight = rowRect.rect.height;
            if (rowHeight <= 1f)
                rowHeight = rowRect.sizeDelta.y;
            if (rowHeight <= 1f)
            {
                RectTransform template = KingdomUIPrefabLibrary.Load(
                    KingdomUIPrefabLibrary.MusicTrack)?.GetComponent<RectTransform>();
                rowHeight = template == null ? 56f : template.sizeDelta.y;
            }
            rowHeight = Mathf.Max(1f, rowHeight);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);
            rowRect.anchoredPosition = new Vector2(0f, -contentHeight);
            Image rowSurface = rowObject.GetComponent<Image>();
            if (rowSurface == null)
                rowSurface = rowObject.AddComponent<Image>();
            rowSurface.color = i % 2 == 0
                ? new Color(.18f, .22f, .22f, 1f)
                : new Color(.14f, .18f, .18f, 1f);
            Button row = rowObject.GetComponent<Button>();
            if (row == null)
                row = rowObject.AddComponent<Button>();
            row.targetGraphic = rowSurface;
            ApplyButtonColors(row, rowSurface.color);
            row.onClick.AddListener(() => manager?.PlayTrack(track));
            ConfigureMusicTrackColumn(rowObject, "Label", track.Label,
                new Vector2(0f, 0f), new Vector2(.58f, 1f),
                new Vector2(12f, 4f), new Vector2(-8f, -4f),
                TextAlignmentOptions.MidlineLeft);
            float length = track.Clip == null ? 0f : track.Clip.length;
            ConfigureMusicTrackColumn(rowObject, "Length",
                FormatMusicTime(length), new Vector2(.58f, 0f), new Vector2(.78f, 1f),
                new Vector2(0f, 4f), new Vector2(0f, -4f),
                TextAlignmentOptions.Center);
            ConfigureMusicTrackColumn(rowObject, "Type", track.Category,
                new Vector2(.78f, 0f), new Vector2(1f, 1f),
                new Vector2(8f, 4f), new Vector2(-12f, -4f),
                TextAlignmentOptions.MidlineRight);
            row.gameObject.AddComponent<UIPageScrollDragForwarder>();
            musicTrackButtons[track.Id] = row;
            contentHeight += rowHeight;
        }
        trackContent.sizeDelta = new Vector2(0f, Mathf.Max(120f, contentHeight));
        trackContent.anchoredPosition = Vector2.zero;
        trackContent.SetAsLastSibling();
        nextRowTop = 0f;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(trackContent);
        musicListScroll.StopMovement();
        musicListScroll.verticalNormalizedPosition = 1f;
        Debug.Log($"[KingdomUI] Music list built: tracks={displayTracks.Count}, rows={musicTrackButtons.Count}, viewport={listViewport.rect.size}, content={trackContent.rect.size}, verticalRange={Mathf.Max(0f, trackContent.rect.height - listViewport.rect.height):0.00}");
    }

    private static List<MusicManager.MusicTrack> BuildMusicDisplayTracks(MusicManager manager)
    {
        List<MusicManager.MusicTrack> result = new();
        if (manager != null)
        {
            for (int i = 0; i < manager.Tracks.Count; i++)
                result.Add(manager.Tracks[i]);
        }
        if (result.Count > 0)
            return result;

        // UI-side fallback for the first frame after a domain reload. The
        // manager normally owns this catalog, but the list should not render
        // empty merely because its Awake order has not completed yet.
        string[] categories = { "day", "silence", "village" };
        for (int i = 0; i < categories.Length; i++)
        {
            string category = categories[i];
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Musics/" + category);
            System.Array.Sort(clips, (left, right) => string.Compare(left.name, right.name, System.StringComparison.OrdinalIgnoreCase));
            for (int j = 0; j < clips.Length; j++)
            {
                AudioClip clip = clips[j];
                if (clip == null)
                    continue;
                string path = "Musics/" + category + "/" + clip.name;
                result.Add(new MusicManager.MusicTrack(category + ":" + clip.name,
                    clip.name, category, path, clip));
            }
        }
        return result;
    }

    private static void ConfigureMusicButtonText(Button button, string text)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        ConfigureMusicText(label, text);
        label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
    }

    private static TMP_Text ConfigureMusicTrackColumn(GameObject rowObject, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        TextAlignmentOptions alignment)
    {
        Transform child = rowObject.transform.Find(name);
        TMP_Text label = child == null ? null : child.GetComponent<TMP_Text>();
        if (label == null)
            label = Label(name, rowObject.transform, string.Empty, 22, TextPrimary,
                anchorMin, anchorMax, offsetMin, offsetMax);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        label.text = text ?? string.Empty;
        label.color = TextPrimary;
        label.fontSize = 22;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.enabled = true;
        label.gameObject.SetActive(true);
        label.transform.SetAsLastSibling();
        return label;
    }

    private static void ConfigureMusicText(TMP_Text label, string text)
    {
        if (label == null) return;
        label.text = text;
        label.color = TextPrimary;
        label.enabled = true;
        label.gameObject.SetActive(true);
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.enableWordWrapping = false;
        BringMusicTextToFront(label);
    }

    private static void BringMusicTextToFront(TMP_Text label)
    {
        if (label != null)
             label.transform.SetAsLastSibling();
    }

    private static void EnsureMusicValueLabelVisible(TMP_Text label)
    {
        if (label == null)
            return;
        label.enabled = true;
        label.gameObject.SetActive(true);
        label.raycastTarget = false;
        label.color = TextPrimary;
        label.canvasRenderer.SetAlpha(1f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Overflow;
        label.enableWordWrapping = false;
        BringMusicTextToFront(label);
    }

    private Slider CreateMusicSlider(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax,
        float min, float max, bool wholeNumbers, UnityEngine.Events.UnityAction<float> changed)
    {
        return CreateMusicSlider(name, parent, Vector2.zero, Vector2.zero,
            offsetMin, offsetMax, min, max, wholeNumbers, changed);
    }

    private Slider CreateMusicSlider(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, float min, float max, bool wholeNumbers,
        UnityEngine.Events.UnityAction<float> changed)
    {
        RectTransform rect = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(.08f, .10f, .10f, 1f);
        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.interactable = true;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.direction = Slider.Direction.LeftToRight;
        RectTransform fill = Rect("Fill", rect, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = Positive;
        fillImage.raycastTarget = false;
        RectTransform handle = Rect("Handle", rect, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(20, 0));
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = TextPrimary;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.onValueChanged.AddListener(changed);
        return slider;
    }

    private void AddPointerState(Slider slider, UnityEngine.Events.UnityAction down,
        UnityEngine.Events.UnityAction up)
    {
        if (slider == null) return;
        EventTrigger trigger = slider.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry pointerDown = new() { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener(_ => { SetMusicPageScrollEnabled(false); if (down != null) down(); });
        trigger.triggers.Add(pointerDown);
        EventTrigger.Entry pointerUp = new() { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener(_ => { if (up != null) up(); SetMusicPageScrollEnabled(true); });
        trigger.triggers.Add(pointerUp);
        EventTrigger.Entry pointerExit = new() { eventID = EventTriggerType.PointerExit };
        pointerExit.callback.AddListener(_ => { if (up != null) up(); SetMusicPageScrollEnabled(true); });
        trigger.triggers.Add(pointerExit);
    }

    private void SetMusicPageScrollEnabled(bool enabled)
    {
        // Music owns the page viewport while its track list scrolls inside it;
        // never re-enable the outer page ScrollRect during a Slider gesture.
        if (pageScroll != null)
            pageScroll.enabled = enabled && populatedPage != "Music";
    }

    private void ToggleMusicPlayback()
    {
        MusicManager manager = FindMusicManager();
        if (manager == null) return;
        if (manager.IsPlaying) manager.Pause();
        else if (manager.IsPaused) manager.Resume();
        else manager.PlayRandom();
    }

    private void StopMusicPlayback()
    {
        MusicManager manager = FindMusicManager();
        if (manager != null) manager.Stop();
    }

    private void PlayRelativeMusicTrack(int direction)
    {
        MusicManager manager = FindMusicManager();
        if (manager == null || manager.Tracks.Count == 0)
            return;
        int currentIndex = -1;
        if (manager.CurrentTrack != null)
        {
            for (int i = 0; i < manager.Tracks.Count; i++)
            {
                if (manager.Tracks[i].Id == manager.CurrentTrack.Id)
                {
                    currentIndex = i;
                    break;
                }
            }
        }
        int index = currentIndex < 0
            ? direction > 0 ? 0 : manager.Tracks.Count - 1
            : (currentIndex + direction + manager.Tracks.Count) % manager.Tracks.Count;
        manager.PlayTrack(index);
    }

    private void RefreshMusicPage()
    {
        MusicManager manager = FindMusicManager();
        if (manager == null || !musicPageBuilt) return;
        if (musicTrackButtons.Count == 0 && manager.Tracks.Count > 0)
        {
            RectTransform musicSurface = musicTrackList;
            while (musicSurface != null && musicSurface.name != "MusicSurface")
                musicSurface = musicSurface.parent as RectTransform;
            RectTransform pageHost = musicSurface == null ? null : musicSurface.parent as RectTransform;
            if (pageHost != null)
            {
                musicPageBuilt = false;
                BuildMusicPage(pageHost);
                return;
            }
        }
        MusicManager.MusicTrack track = manager.CurrentTrack;
        float current = manager.AudioSource == null ? 0f : manager.AudioSource.time;
        float total = manager.AudioSource == null || manager.AudioSource.clip == null ? 0f : manager.AudioSource.clip.length;
        if (musicCurrentLabel != null)
            musicCurrentLabel.text = track == null ? "NOW PLAYING" : track.Category;
        if (musicTimeLabel != null)
        {
            string trackName = track == null ? "NO TRACK" : track.Label;
            musicTimeLabel.text = trackName + "  " + FormatMusicTime(current) + " / " + FormatMusicTime(total);
        }
        if (musicProgressSlider != null)
        {
            musicProgressRefreshing = true;
            if (musicProgressSeekHandler == null)
                musicProgressSeekHandler = SeekMusicFromSlider;
            // This also clears a listener captured by an older hot-reloaded
            // UI instance, which otherwise survives and seeks during maxValue
            // assignment.
            musicProgressSlider.onValueChanged.RemoveAllListeners();
            try
            {
                musicProgressSlider.maxValue = total;
                if (!musicProgressDragging)
                    musicProgressSlider.SetValueWithoutNotify(Mathf.Clamp(current, 0f, total));
            }
            finally
            {
                musicProgressSlider.onValueChanged.AddListener(musicProgressSeekHandler);
                musicProgressRefreshing = false;
            }
        }
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(manager.Volume);
        float gap = Mathf.Clamp(manager.GapSeconds, 0f, 30f);
        if (manager.GapSeconds > 30f)
            manager.SetGapSeconds(gap);
        if (musicGapSlider != null) musicGapSlider.SetValueWithoutNotify(gap);
        if (musicVolumeValueLabel != null)
        {
            musicVolumeValueLabel.text = "音量 " + manager.Volume.ToString("0.00");
            EnsureMusicValueLabelVisible(musicVolumeValueLabel);
        }
        if (musicGapValueLabel != null)
        {
            musicGapValueLabel.text = "音乐间隙 " + gap.ToString("0.00");
            EnsureMusicValueLabelVisible(musicGapValueLabel);
        }
        SetMusicPauseButtonVisual(manager);
        foreach (KeyValuePair<string, Button> pair in musicTrackButtons)
        {
            if (pair.Value == null) continue;
            Image image = pair.Value.targetGraphic as Image;
            if (image != null)
            {
                bool selected = track != null && pair.Key == track.Id;
                int rowIndex = pair.Value.transform.GetSiblingIndex();
                image.color = selected
                    ? Copper
                    : rowIndex % 2 == 0
                        ? new Color(.18f, .22f, .22f, 1f)
                        : new Color(.14f, .18f, .18f, 1f);
            }
        }
    }

    private void SetMusicPauseButtonVisual(MusicManager manager)
    {
        if (musicPlayPauseButton == null || manager == null)
            return;
        TMP_Text label = musicPlayPauseButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = manager.IsPlaying ? "PAUSE" : "PLAY";
        Image image = musicPlayPauseButton.targetGraphic as Image;
        if (image == null)
            return;
        image.color = manager.IsPaused
            ? new Color(Positive.r * .45f, Positive.g * .45f, Positive.b * .45f, 1f)
            : manager.IsPlaying ? Positive : PanelRaised;
    }

    private static string FormatMusicTime(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
    }

    private void SeekMusicFromSlider(float value)
    {
        MusicManager manager = FindMusicManager();
        if (manager == null || manager.AudioSource == null || manager.AudioSource.clip == null)
            return;
        float length = manager.AudioSource.clip.length;
        if (length > 0f && !float.IsNaN(length) && !float.IsInfinity(length))
            manager.SeekNormalized(Mathf.Clamp01(value / length));
    }
}

/// <summary>
/// Gives the music list's blank viewport area the same drag ownership as its
/// rows. This is needed because a ScrollRect alone does not reliably receive
/// a drag that starts on a child Button or on the viewport Image.
/// </summary>
public sealed class UIMusicListDragForwarder : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ScrollRect Owner => GetComponent<ScrollRect>();

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        Owner?.OnInitializePotentialDrag(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Owner?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Owner?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Owner?.OnEndDrag(eventData);
    }
}
