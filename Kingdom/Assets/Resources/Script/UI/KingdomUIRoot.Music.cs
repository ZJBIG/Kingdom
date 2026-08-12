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
            musicCurrentLabel.text = track == null ? "正在播放" : track.Category;
        if (musicTimeLabel != null)
        {
            string trackName = track == null ? "没有曲目" : track.Label;
            musicTimeLabel.text = trackName + "  " + FormatMusicTime(current) + " / " + FormatMusicTime(total);
        }
        if (musicProgressSlider != null)
        {
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
                        : new Color(.10f, .13f, .13f, 1f);
            }
        }
    }

    private void SetMusicPauseButtonVisual(MusicManager manager)
    {
        if (musicPlayPauseButton == null || manager == null)
            return;
        TMP_Text label = musicPlayPauseButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = manager.IsPlaying ? "暂停" : "播放";
        Image image = musicPlayPauseButton.targetGraphic as Image;
        if (image == null)
            return;
        image.color = manager.IsPaused
            ? new Color(Positive.r * .45f, Positive.g * .45f, Positive.b * .45f, 1f)
            : manager.IsPlaying ? Positive : PanelRaised;
    }

    private static string MusicCategoryLabel(string category) => category switch
    {
        "day" => "日间",
        "silence" => "静默",
        "village" => "村落",
        _ => category
    };

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
