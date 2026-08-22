using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private readonly Dictionary<string, Button> musicTrackButtons = new();
    private readonly Dictionary<string, Sprite> musicIconCache = new();
    private MusicManager musicManagerCache;
    private Button musicGlobalPauseButton;
    private ScrollRect musicListScroll;
    private float lastMusicProgressMaxValue = -1f;
    private float lastMusicVolume = float.NaN;
    private float lastMusicGap = float.NaN;

    private MusicManager FindMusicManager()
    {
        if (musicManagerCache == null)
            musicManagerCache = FindObjectOfType<MusicManager>();
        return musicManagerCache;
    }

    private void BuildMusicPage(RectTransform parent)
    {
        Transform existing = parent.Find("MusicSurface");
        if (existing != null && existing.Find("Controls") != null &&
            existing.Find("TrackListViewport") != null)
        {
            BuildAuthoredMusicPage(existing as RectTransform);
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
        if (result.Count > 0)
            return result;

        // UI-side fallback for the first frame after a domain reload. The
        // manager normally owns this catalog, but the list should not render
        // empty merely because its Awake order has not completed yet.
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Musics");
        System.Array.Sort(clips, (left, right) => string.Compare(left.name, right.name, System.StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null || clip.length <= 0f)
                continue;
            string path = "Musics/" + clip.name;
            result.Add(new MusicManager.MusicTrack(clip.name,
                MusicManager.DisplayNameFor(clip.name), "All Music", path, clip));
        }
        result.Sort((left, right) => string.Compare(left.Label, right.Label, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private void ConfigureMusicTrackIcon(Button button, string iconName)
    {
        if (button == null)
            return;
        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image == null)
            return;
        image.sprite = LoadMusicIcon(iconName);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        button.targetGraphic = image;
    }

    private Sprite LoadMusicIcon(string iconName)
    {
        if (musicIconCache.TryGetValue(iconName, out Sprite cached))
            return cached;
        string path = "UI/Kingdom/MusicIcons/" + iconName;
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
        // Geometry, font size and alignment belong to KingdomUIMusicTrack.prefab.
        // The presenter supplies only the current row data.
        label.raycastTarget = false;
        label.enabled = true;
        label.gameObject.SetActive(true);
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

    private void StopMusicPlayback()
    {
        MusicManager manager = FindMusicManager();
        if (manager != null) manager.Stop();
    }

    private void ToggleGlobalMusicPause()
    {
        MusicManager manager = FindMusicManager();
        if (manager == null)
            return;
        if (manager.IsPermanentlyStopped)
            manager.PlayRandom();
        else
            manager.Stop();
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
                manager.Stop();
            else if (manager.IsPermanentlyStopped)
                manager.PlayTrack(track);
            else
                manager.PlayTrack(track);
            RefreshMusicTrackIcons(manager);
            return;
        }
        manager.PlayTrack(track);
        RefreshMusicTrackIcons(manager);
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
        MusicManager.MusicTrack track = manager.CurrentTrack;
        float current = manager.AudioSource == null ? 0f : manager.AudioSource.time;
        float total = manager.AudioSource == null || manager.AudioSource.clip == null ? 0f : manager.AudioSource.clip.length;
        SetTextIfChanged(musicCurrentLabel, track == null ? "正在播放" : track.Category);
        if (musicTimeLabel != null)
        {
            string trackName = track == null ? "没有曲目" : track.Label;
            SetTextIfChanged(musicTimeLabel,
                trackName + "  " + FormatMusicTime(current) + " / " + FormatMusicTime(total));
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
        float gap = Mathf.Clamp(manager.GapSeconds, 0f, 30f);
        if (manager.GapSeconds > 30f)
            manager.SetGapSeconds(gap);
        if (musicGapSlider != null &&
            !Mathf.Approximately(musicGapSlider.value, gap))
            musicGapSlider.SetValueWithoutNotify(gap);
        if (musicVolumeValueLabel != null &&
            !Mathf.Approximately(lastMusicVolume, manager.Volume))
        {
            SetTextIfChanged(musicVolumeValueLabel, "音量 " + manager.Volume.ToString("0.00"));
            lastMusicVolume = manager.Volume;
        }
        if (musicGapValueLabel != null &&
            !Mathf.Approximately(lastMusicGap, gap))
        {
            SetTextIfChanged(musicGapValueLabel, "音乐间隙 " + gap.ToString("0.00"));
            lastMusicGap = gap;
        }
        RefreshMusicGlobalPauseVisual(manager);
        RefreshMusicTrackIcons(manager, track);
    }

    private void RefreshMusicTrackIcons(MusicManager manager, MusicManager.MusicTrack currentTrack = null)
    {
        if (manager == null)
            return;
        currentTrack ??= manager.CurrentTrack;
        foreach (KeyValuePair<string, Button> pair in musicTrackButtons)
        {
            if (pair.Value == null) continue;
            Image image = pair.Value.transform.parent == null
                ? null
                : pair.Value.transform.parent.GetComponent<Image>();
            if (image != null)
            {
                bool selected = currentTrack != null && pair.Key == currentTrack.Id;
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
                    string iconName = selected && manager.IsPermanentlyStopped
                        ? "stop"
                        : selected && manager.IsPlaying
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
