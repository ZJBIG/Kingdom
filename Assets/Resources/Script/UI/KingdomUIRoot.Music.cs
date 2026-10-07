using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private readonly Dictionary<string, Button> musicTrackButtons = new();
    private readonly Dictionary<string, Sprite> musicIconCache = new();
    private readonly HashSet<Slider> musicPointerStateSliders = new();
    private MusicManager musicManagerCache;
    private Button musicGlobalPauseButton;
    private TMP_Text musicCurrentPlayName;
    private Image musicCurrentCategoryIcon;
    private ScrollRect musicListScroll;
    private float lastMusicProgressMaxValue = -1f;
    private int lastMusicTimeSecond = -1;
    private int lastMusicDurationSecond = -1;
    private string lastMusicTimeTrackName;
    private int lastMusicCatalogVersion = -1;
    private string lastMusicTrackVisualId;
    private bool lastMusicTrackVisualPlaying;
    private bool lastMusicTrackVisualStopped;
    private bool musicTrackVisualStateValid;

    private MusicManager FindMusicManager()
    {
        if (musicManagerCache == null)
            musicManagerCache = FindObjectOfType<MusicManager>();
        return musicManagerCache;
    }

    private void BuildMusicPage(RectTransform parent)
    {
        MusicManager manager = FindMusicManager();
        if (musicPageBuilt &&
            lastMusicCatalogVersion == (manager == null ? -1 : manager.CatalogVersion))
            return;
        Transform existing = parent.Find("MusicSurface");
        if (existing != null && existing.Find("Controls") != null &&
            existing.Find("TrackListViewport") != null)
        {
            musicTrackVisualStateValid = false;
            lastMusicTimeSecond = -1;
            BuildAuthoredMusicPage(existing as RectTransform);
            if (musicPageBuilt)
                lastMusicCatalogVersion = manager == null ? -1 : manager.CatalogVersion;
            return;
        }
        musicPageBuilt = false;
        Debug.LogError("[王国界面] Authored MusicSurface is missing Controls or TrackListViewport; fixed music UI will not be generated at runtime.");
    }

    private static List<MusicManager.MusicTrack> BuildMusicDisplayTracks(MusicManager manager)
    {
        List<MusicManager.MusicTrack> result = new();
        if (manager != null)
        {
            for (int i = 0; i < manager.Tracks.Count; i++)
                result.Add(manager.Tracks[i]);
        }
        result.Sort((left, right) =>
            string.Compare(left.Label, right.Label, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private void ConfigureMusicIcon(Button button, string iconName, bool simpleImage)
    {
        if (button == null)
            return;
        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image == null)
            return;
        image.sprite = LoadMusicIcon(iconName);
        if (simpleImage)
            image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        button.targetGraphic = image;
    }

    private void ConfigureMusicTrackIcon(Button button, string iconName)
    {
        ConfigureMusicIcon(button, iconName, true);
    }

    private Sprite LoadMusicIcon(string iconName)
    {
        if (musicIconCache.TryGetValue(iconName, out Sprite cached))
            return cached;
        string path = "Texture/MusicIcons/" + iconName;
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture != null)
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f), 100f);
        }
        musicIconCache[iconName] = sprite;
        if (sprite == null)
            Debug.LogError("[王国界面] Music icon could not be loaded: " + path);
        return sprite;
    }

    private static TMP_Text ConfigureMusicTrackColumn(GameObject rowObject, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        TextAlignmentOptions alignment)
    {
        Transform child = rowObject.transform.Find(name);
        TMP_Text label = child == null ? null : child.GetComponent<TMP_Text>();
        if (label == null)
        {
            Debug.LogError("[王国界面] MusicTrack prefab is missing its authored column: " + name);
            return null;
        }
        label.text = text ?? string.Empty;
        RectTransform rect = label.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
        label.alignment = alignment;
        label.raycastTarget = false;
        label.enabled = true;
        label.gameObject.SetActive(true);
        return label;
    }

    private void ConfigureMusicTrackCategoryIcon(GameObject rowObject, string category)
    {
        Transform existing = rowObject.transform.Find("Category");
        GameObject categoryObject = existing == null
            ? new GameObject("Category", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : existing.gameObject;
        categoryObject.transform.SetParent(rowObject.transform, false);
        TMP_Text oldText = categoryObject.GetComponent<TMP_Text>();
        if (oldText != null)
            oldText.enabled = false;
        Image image = categoryObject.GetComponent<Image>() ?? categoryObject.AddComponent<Image>();
        RectTransform rect = categoryObject.transform as RectTransform;
        if (rect == null)
            return;
        rect.anchorMin = new Vector2(.82f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(8f, 8f);
        rect.offsetMax = new Vector2(-8f, -8f);
        image.sprite = LoadMusicIcon((category ?? string.Empty).ToLowerInvariant());
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        categoryObject.SetActive(true);
        categoryObject.transform.SetAsLastSibling();
    }

    private void AddPointerState(Slider slider, UnityEngine.Events.UnityAction down,
        UnityEngine.Events.UnityAction up)
    {
        if (slider == null) return;
        EventTrigger trigger = slider.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = slider.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry pointerDown = new() { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener(_ => { SetMusicPageScrollEnabled(false); if (down != null) down(); });
        trigger.triggers.Add(pointerDown);
        EventTrigger.Entry pointerUp = new() { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener(_ => { if (up != null) up(); SetMusicPageScrollEnabled(true); });
        trigger.triggers.Add(pointerUp);
    }

    private void SetMusicPageScrollEnabled(bool enabled)
    {
        // Music owns the page viewport while its track list scrolls inside it;
        // never re-enable the outer page ScrollRect during a Slider gesture.
        if (pageScroll != null)
            pageScroll.enabled = enabled && populatedPage != "Music";
    }

    private void ToggleGlobalMusicPause()
    {
        MusicManager manager = FindMusicManager();
        if (manager == null)
            return;
        if (manager.IsPlaying)
            manager.Pause();
        else if (manager.IsPaused)
            manager.Resume();
        else if (!manager.PlayCurrent())
            manager.PlayRandom();
        RefreshMusicTrackIcons(manager);
        RefreshMusicGlobalPauseVisual(manager);
    }

    private void ToggleMusicTrack(MusicManager.MusicTrack track)
    {
        MusicManager manager = FindMusicManager();
        if (manager == null || track == null)
            return;
        if (manager.CurrentTrack != null && manager.CurrentTrack.Id == track.Id)
        {
            if (manager.IsPlaying)
                manager.Pause();
            else if (manager.IsPaused)
                manager.Resume();
            else
                manager.PlayTrack(track);
            RefreshMusicTrackIcons(manager);
            RefreshMusicGlobalPauseVisual(manager);
            return;
        }
        manager.PlayTrack(track);
        RefreshMusicTrackIcons(manager);
        RefreshMusicGlobalPauseVisual(manager);
    }

    private void PlayRelativeMusicTrack(int direction)
    {
        MusicManager manager = FindMusicManager();
        if (manager == null || manager.Tracks.Count == 0)
            return;
        if (direction < 0)
            manager.PreviousTrack();
        else
            manager.NextTrack();
        RefreshMusicTrackIcons(manager);
        RefreshMusicGlobalPauseVisual(manager);
    }

    private void RefreshMusicPage()
    {
        MusicManager manager = FindMusicManager();
        if (manager == null || !musicPageBuilt) return;
        if (!IsPageScrolling() && manager.CatalogVersion != lastMusicCatalogVersion &&
            musicListScroll != null)
        {
            float previousListPosition = musicListScroll.verticalNormalizedPosition;
            BuildAuthoredMusicRows(musicListScroll.GetComponent<RectTransform>(), manager);
            lastMusicCatalogVersion = manager.CatalogVersion;
            musicListScroll.verticalNormalizedPosition = previousListPosition;
        }
        bool loading = manager.State == MusicManager.PlaybackState.Loading;
        MusicManager.MusicTrack track = loading ? null : manager.CurrentTrack;
        float current = loading || manager.AudioSource == null ? 0f : manager.AudioSource.time;
        float total = loading || manager.AudioSource == null || manager.AudioSource.clip == null ? 0f : manager.AudioSource.clip.length;
        RefreshCurrentMusicCategoryIcon(track);
        SetTextIfChanged(musicCurrentPlayName, track == null ? string.Empty : track.Label);
        if (musicTimeLabel != null)
        {
            int currentSecond = Mathf.Max(0, Mathf.FloorToInt(current));
            int durationSecond = Mathf.Max(0, Mathf.FloorToInt(total));
            if (lastMusicTimeSecond != currentSecond ||
                lastMusicDurationSecond != durationSecond ||
                lastMusicTimeTrackName != (track == null ? string.Empty : track.Id))
            {
                SetTextIfChanged(musicTimeLabel,
                    FormatMusicTime(current) + " / " + FormatMusicTime(total));
                lastMusicTimeSecond = currentSecond;
                lastMusicDurationSecond = durationSecond;
                lastMusicTimeTrackName = track == null ? string.Empty : track.Id;
            }
        }
        if (musicProgressSlider != null)
        {
            // The listener is bound once when the authored music page is
            // created. Rebinding it every 100 ms allocates a new delegate and
            // UnityEvent bookkeeping entry, which creates avoidable GC spikes
            // during a long session.
            if (!Mathf.Approximately(lastMusicProgressMaxValue, total))
            {
                musicProgressSlider.maxValue = total;
                lastMusicProgressMaxValue = total;
            }
            if (!musicProgressDragging)
            {
                float value = Mathf.Clamp(current, 0f, total);
                if (!Mathf.Approximately(musicProgressSlider.value, value))
                    musicProgressSlider.SetValueWithoutNotify(value);
            }
        }
        if (musicVolumeSlider != null &&
            !Mathf.Approximately(musicVolumeSlider.value, manager.Volume))
            musicVolumeSlider.SetValueWithoutNotify(manager.Volume);
        SetTextIfChanged(musicVolumeValueLabel,
            "音量：" + Mathf.RoundToInt(manager.Volume * 100f) + "%");
        float gap = Mathf.Clamp(manager.GapSeconds, 0f, MusicManager.MaxGapSeconds);
        if (musicGapSlider != null && !Mathf.Approximately(musicGapSlider.maxValue, MusicManager.MaxGapSeconds))
            musicGapSlider.maxValue = MusicManager.MaxGapSeconds;
        if (musicGapSlider != null &&
            !Mathf.Approximately(musicGapSlider.value, gap))
            musicGapSlider.SetValueWithoutNotify(gap);
        SetTextIfChanged(musicGapValueLabel, "曲目间隔：" + gap.ToString("0.00") + "s");
        if (sfxVolumeSlider != null && !Mathf.Approximately(sfxVolumeSlider.value, UIButtonSoundManager.SfxVolume))
            sfxVolumeSlider.SetValueWithoutNotify(UIButtonSoundManager.SfxVolume);
        SetTextIfChanged(sfxVolumeValueLabel,
            "音效音量：" + Mathf.RoundToInt(UIButtonSoundManager.SfxVolume * 100f) + "%" +
            (UIButtonSoundManager.SfxMuted ? "（静音）" : string.Empty));
        RefreshMusicGlobalPauseVisual(manager);
        RefreshMusicTrackIcons(manager, track);
    }

    private void RefreshCurrentMusicCategoryIcon(MusicManager.MusicTrack track)
    {
        if (musicCurrentCategoryIcon == null)
            return;
        Image image = musicCurrentCategoryIcon;
        string category = track == null ? string.Empty : track.Category;
        image.sprite = string.IsNullOrEmpty(category)
            ? null
            : LoadMusicIcon(category.ToLowerInvariant());
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        image.enabled = track != null && image.sprite != null;
        musicCurrentCategoryIcon.transform.SetAsLastSibling();
    }

    private void RefreshMusicTrackIcons(MusicManager manager, MusicManager.MusicTrack currentTrack = null)
    {
        if (manager == null)
            return;
        currentTrack ??= manager.CurrentTrack;
        string currentTrackId = currentTrack == null ? null : currentTrack.Id;
        bool isPlaying = manager.IsPlaying;
        bool isStopped = manager.IsPermanentlyStopped;
        if (musicTrackVisualStateValid &&
            lastMusicTrackVisualId == currentTrackId &&
            lastMusicTrackVisualPlaying == isPlaying &&
            lastMusicTrackVisualStopped == isStopped)
            return;
        lastMusicTrackVisualId = currentTrackId;
        lastMusicTrackVisualPlaying = isPlaying;
        lastMusicTrackVisualStopped = isStopped;
        musicTrackVisualStateValid = true;
        foreach (KeyValuePair<string, Button> pair in musicTrackButtons)
        {
            if (pair.Value == null) continue;
            Image image = pair.Value.transform.parent == null
                ? null
                : pair.Value.transform.parent.GetComponent<Image>();
            if (image != null)
            {
                bool selected = currentTrackId != null && pair.Key == currentTrackId;
                Transform rowTransform = pair.Value.transform.parent;
                int rowIndex = rowTransform == null ? 0 : rowTransform.GetSiblingIndex();
                Color color = selected
                    ? Copper
                    : rowIndex % 2 == 0
                        ? new Color(.18f, .22f, .22f, 1f)
                        : new Color(.10f, .13f, .13f, 1f);
                if (image.color != color)
                    image.color = color;
                Image playPauseImage = pair.Value.targetGraphic as Image;
                if (playPauseImage != null)
                {
                    // A permanently stopped track remains the selected
                    // restart target; keep the play affordance visible rather
                    // than treating it as an ordinary unselected row.
                    string iconName = selected && manager.IsPermanentlyStopped
                            ? "play"
                            : selected && isPlaying
                                ? "pause"
                                : "play";
                    Sprite icon = LoadMusicIcon(iconName);
                    if (playPauseImage.sprite != icon)
                        playPauseImage.sprite = icon;
                }
            }
        }
    }

    private void RefreshMusicGlobalPauseVisual(MusicManager manager)
    {
        if (musicGlobalPauseButton == null || manager == null)
            return;
        Image image = musicGlobalPauseButton.targetGraphic as Image;
        if (image == null)
            return;
        Color color = manager.IsPermanentlyStopped ? Error : Color.white;
        if (image.color != color)
            image.color = color;
        Sprite icon = LoadMusicIcon(manager.IsPlaying ? "pause" : "play");
        if (image.sprite != icon)
            image.sprite = icon;
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
