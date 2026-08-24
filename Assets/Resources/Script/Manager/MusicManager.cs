using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Owns the runtime music catalog and playback state. Audio assets are
/// discovered from Resources/Musics so filenames can be descriptive without
/// requiring a code change for every new track.
/// </summary>
public class MusicManager : Singleton<MusicManager>
{
    private const string VolumePreference = "Kingdom.Music.Volume";
    private const string GapPreference = "Kingdom.Music.GapSeconds";
    private const string MusicResourceRoot = "Musics";
    [Serializable]
    public sealed class MusicTrack
    {
        public string Id { get; }
        public string Label { get; }
        public string Category { get; }
        public string ResourcePath { get; }
        public AudioClip Clip { get; }

        public MusicTrack(string id, string label, string category, string resourcePath, AudioClip clip)
        {
            Id = id;
            Label = label;
            Category = category;
            ResourcePath = resourcePath;
            Clip = clip;
        }
    }

    private readonly List<Pair<string, int>> musicTypes = new();
    private readonly List<MusicTrack> tracks = new();
    private readonly WaitForSecondsRealtime autoPlayPollDelay = new(.25f);
    private Coroutine loadingCoroutine;
    private bool manualStop;
    private float volume = 1f;
    private float gapSeconds = 5f;
    private int lastRandomIndex = -1;
    private int loadVersion;
    private int playbackRequestVersion;
    private bool autoAdvanceEligible;

    public IReadOnlyList<Pair<string, int>> MusicTypes => musicTypes;
    public IReadOnlyList<MusicTrack> Tracks => tracks;
    public int CatalogVersion { get; private set; }
    public AudioSource AudioSource { get; private set; }
    public MusicTrack CurrentTrack { get; private set; }
    public float Volume => volume;
    public float GapSeconds => gapSeconds;
    public bool IsPermanentlyStopped => manualStop;
    public bool IsPlaying => AudioSource != null && AudioSource.isPlaying;
    public bool IsPaused => AudioSource != null && AudioSource.clip != null &&
        !AudioSource.isPlaying && AudioSource.time > 0f;
    public float Progress
    {
        get
        {
            if (AudioSource == null || AudioSource.clip == null || AudioSource.clip.length <= 0f)
                return 0f;
            return Mathf.Clamp01(AudioSource.time / AudioSource.clip.length);
        }
    }

    protected override void Initialize()
    {
        AudioSource = GetComponent<AudioSource>();
        if (AudioSource == null)
            AudioSource = gameObject.AddComponent<AudioSource>();
        AudioSource.playOnAwake = false;
        AudioSource.loop = false;
        volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, 1f));
        gapSeconds = Mathf.Clamp(PlayerPrefs.GetFloat(GapPreference, 5f), 0f, 120f);
        AudioSource.volume = volume;
        RebuildCatalog();
    }

    private void Start()
    {
        StartCoroutine(AutoPlayLoop());
    }

    public void RebuildCatalog()
    {
        MusicTrack previous = CurrentTrack;
        tracks.Clear();
        musicTypes.Clear();

        AddAllTracks();

        CurrentTrack = previous == null ? null : FindTrack(previous.Id);
        CatalogVersion++;
        Debug.Log("[MusicManager] Catalog rebuilt: " + tracks.Count + " valid tracks.");
    }

    private void AddAllTracks()
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>(MusicResourceRoot);
        Array.Sort(clips, CompareClips);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null || clip.length <= 0f || float.IsNaN(clip.length) ||
                float.IsInfinity(clip.length))
            {
                Debug.LogWarning("[MusicManager] Ignoring invalid clip in " + MusicResourceRoot + ".");
                continue;
            }
            tracks.Add(new MusicTrack(clip.name, DisplayNameFor(clip.name), "All Music",
                MusicResourceRoot + "/" + clip.name, clip));
        }
        tracks.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
        int count = tracks.Count;
        musicTypes.Add(new Pair<string, int>("all", count));
        if (count == 0)
            Debug.LogWarning("[MusicManager] No valid clips found in Resources/Musics.");
    }

    private static int CompareClips(AudioClip left, AudioClip right)
    {
        return string.Compare(left == null ? string.Empty : left.name,
            right == null ? string.Empty : right.name, StringComparison.OrdinalIgnoreCase);
    }

    public static string DisplayNameFor(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Unnamed Track";
        StringBuilder result = new();
        for (int i = 0; i < name.Length; i++)
        {
            char current = name[i];
            bool startsWord = i > 0 && char.IsUpper(current) &&
                (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])));
            if (startsWord)
                result.Append(' ');
            result.Append(current == '_' ? ' ' : current);
        }
        return result.ToString();
    }

    private MusicTrack FindTrack(string id)
    {
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].Id == id)
                return tracks[i];
        }
        return null;
    }

    public bool Play(string type, string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
            return false;
        // The type parameter remains for source compatibility with the old
        // category-based API; all current music is stored at Resources/Musics.
        return Play(clipName);
    }

    public bool Play(string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
            return false;
        return QueuePlay(MusicResourceRoot + "/" + clipName);
    }

    public bool QueuePlay(string resourcePath)
    {
        if (AudioSource == null || string.IsNullOrEmpty(resourcePath))
            return false;
        manualStop = false;
        playbackRequestVersion++;
        autoAdvanceEligible = true;
        CancelPendingLoad();
        loadingCoroutine = StartCoroutine(LoadAndPlay(resourcePath, ++loadVersion));
        return true;
    }

    private IEnumerator LoadAndPlay(string resourcePath, int requestVersion)
    {
        ResourceRequest request = Resources.LoadAsync<AudioClip>(resourcePath);
        yield return request;
        loadingCoroutine = null;
        if (requestVersion != loadVersion || manualStop)
            yield break;

        AudioClip clip = request.asset as AudioClip;
        if (clip == null || clip.length <= 0f)
        {
            Debug.LogWarning("[MusicManager] Missing or invalid audio clip: " + resourcePath);
            yield break;
        }
        Play(clip);
    }

    public bool PlayTrack(MusicTrack track)
    {
        return track != null && track.Clip != null && Play(track.Clip);
    }

    public bool PlayTrack(int index)
    {
        return index >= 0 && index < tracks.Count && PlayTrack(tracks[index]);
    }

    public bool Play(AudioClip clip)
    {
        if (AudioSource == null || clip == null)
            return false;
        manualStop = false;
        playbackRequestVersion++;
        autoAdvanceEligible = true;
        CancelPendingLoad();
        AudioSource.clip = clip;
        AudioSource.volume = volume;
        CurrentTrack = null;
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].Clip == clip)
            {
                CurrentTrack = tracks[i];
                break;
            }
        }
        AudioSource.Play();
        return true;
    }

    public void Pause()
    {
        if (AudioSource != null && AudioSource.isPlaying)
        {
            autoAdvanceEligible = false;
            AudioSource.Pause();
        }
    }

    public void Resume()
    {
        if (AudioSource != null && AudioSource.clip != null)
        {
            manualStop = false;
            autoAdvanceEligible = true;
            AudioSource.UnPause();
        }
    }

    public void Stop()
    {
        manualStop = true;
        playbackRequestVersion++;
        autoAdvanceEligible = false;
        CancelPendingLoad();
        if (AudioSource != null)
            AudioSource.Stop();
    }

    private void CancelPendingLoad()
    {
        loadVersion++;
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
    }

    public void SeekNormalized(float normalized)
    {
        if (AudioSource == null || AudioSource.clip == null)
            return;
        float length = AudioSource.clip.length;
        if (length <= 0f || float.IsNaN(length) || float.IsInfinity(length) ||
            float.IsNaN(normalized) || float.IsInfinity(normalized))
            return;
        autoAdvanceEligible = false;
        AudioSource.time = Mathf.Clamp01(normalized) * Mathf.Max(0f, length - 0.01f);
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        if (AudioSource != null)
            AudioSource.volume = volume;
        PlayerPrefs.SetFloat(VolumePreference, volume);
    }

    public void SetGapSeconds(float value)
    {
        gapSeconds = Mathf.Clamp(value, 0f, 120f);
        PlayerPrefs.SetFloat(GapPreference, gapSeconds);
    }

    public bool PlayRandom()
    {
        if (tracks.Count == 0)
            return false;
        int index = UnityEngine.Random.Range(0, tracks.Count);
        if (tracks.Count > 1 && index == lastRandomIndex)
            index = (index + 1) % tracks.Count;
        lastRandomIndex = index;
        return PlayTrack(index);
    }

    private IEnumerator AutoPlayLoop()
    {
        while (true)
        {
            if (AudioSource == null || loadingCoroutine != null)
            {
                yield return autoPlayPollDelay;
                continue;
            }

            if (!AudioSource.isPlaying && AudioSource.clip == null)
            {
                if (!manualStop)
                {
                    if (gapSeconds > 0f)
                        yield return new WaitForSecondsRealtime(gapSeconds);
                    PlayRandom();
                }
            }
            else if (!AudioSource.isPlaying && AudioSource.clip != null && !manualStop)
            {
                float length = AudioSource.clip.length;
                bool reachedEnd = length > 0f && AudioSource.time >= length - 0.05f;
                if (autoAdvanceEligible && reachedEnd)
                {
                    autoAdvanceEligible = false;
                    int requestVersion = playbackRequestVersion;
                    if (gapSeconds > 0f)
                        yield return new WaitForSecondsRealtime(gapSeconds);
                    if (requestVersion == playbackRequestVersion &&
                        !manualStop)
                    {
                        PlayRandom();
                    }
                }
            }
            yield return autoPlayPollDelay;
        }
    }
}

/// <summary>
/// Short UI feedback sounds generated at runtime, outside the music catalog.
/// </summary>
public sealed class UIButtonSoundManager : MonoBehaviour
{
    public enum Sound { Detail, Purchase, Sell }

    private const int SampleRate = 44100;
    private const float OutputVolume = .42f;
    private static UIButtonSoundManager cachedManager;
    private AudioSource source;
    private AudioClip detailClip;
    private AudioClip purchaseClip;
    private AudioClip sellClip;

    public static void Play(Sound sound)
    {
        UIButtonSoundManager manager = cachedManager;
        if (manager == null)
        {
            manager = FindObjectOfType<UIButtonSoundManager>();
            if (manager == null)
                manager = new GameObject("UIButtonSoundManager").AddComponent<UIButtonSoundManager>();
            cachedManager = manager;
        }
        manager.PlayInternal(sound);
    }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = OutputVolume;
        detailClip = CreateTone("UI_Detail", 720f, .065f, .42f);
        purchaseClip = CreateTwoTone("UI_Purchase", 430f, 650f, .12f, .46f);
        sellClip = CreateTwoTone("UI_Sell", 560f, 300f, .13f, .44f);
        DontDestroyOnLoad(gameObject);
    }

    private void PlayInternal(Sound sound)
    {
        if (source == null)
            return;
        AudioClip clip = sound switch
        {
            Sound.Purchase => purchaseClip,
            Sound.Sell => sellClip,
            _ => detailClip
        };
        if (clip != null)
            source.PlayOneShot(clip);
    }

    private static AudioClip CreateTone(string name, float frequency, float duration, float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = Mathf.Min(1f, i / (SampleRate * .008f)) *
                Mathf.Min(1f, (sampleCount - i) / (SampleRate * .018f));
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude * envelope;
        }
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateTwoTone(string name, float startFrequency, float endFrequency,
        float duration, float amplitude)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)Mathf.Max(1, sampleCount - 1);
            float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
            float t = i / (float)SampleRate;
            float envelope = Mathf.Min(1f, i / (SampleRate * .008f)) *
                Mathf.Min(1f, (sampleCount - i) / (SampleRate * .022f));
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude * envelope;
        }
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
