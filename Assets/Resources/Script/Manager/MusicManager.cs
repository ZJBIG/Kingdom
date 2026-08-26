using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Owns the runtime music catalog and playback state. Audio assets are
/// discovered from Resources/Musics/PMusic so filenames can be descriptive without
/// requiring a code change for every new track.
/// </summary>
public class MusicManager : Singleton<MusicManager>
{
    private const string VolumePreference = "Kingdom.Music.Volume";
    private const string GapPreference = "Kingdom.Music.GapSeconds";
    private const string MusicResourceRoot = "Musics/PMusic";
    private static readonly string[] MusicCategories = { "Tense", "Day", "Night", "AllTime" };

    public enum PlaybackState
    {
        Stopped,
        Loading,
        Playing,
        Paused
    }
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
    private readonly List<MusicTrack> history = new();
    private readonly WaitForSecondsRealtime autoPlayPollDelay = new(.25f);
    private Coroutine loadingCoroutine;
    private bool manualStop;
    private float volume = 1f;
    private float gapSeconds = 5f;
    private int loadVersion;
    private int playbackRequestVersion;
    private int historyCursor = -1;
    private bool waitingForNext;

    public IReadOnlyList<Pair<string, int>> MusicTypes => musicTypes;
    public IReadOnlyList<MusicTrack> Tracks => tracks;
    public IReadOnlyList<MusicTrack> History => history;
    public int CatalogVersion { get; private set; }
    public AudioSource AudioSource { get; private set; }
    public MusicTrack CurrentTrack { get; private set; }
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;
    public float Volume => volume;
    public float GapSeconds => gapSeconds;
    public bool IsPermanentlyStopped => State == PlaybackState.Stopped && manualStop;
    public bool IsPlaying => State == PlaybackState.Playing && AudioSource != null && AudioSource.isPlaying;
    public bool IsPaused => State == PlaybackState.Paused;
    public float Progress
    {
        get
        {
            if (AudioSource == null || AudioSource.clip == null || AudioSource.clip.length <= 0f ||
                State == PlaybackState.Stopped || State == PlaybackState.Loading)
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
        PlayRandom();
        StartCoroutine(AutoPlayLoop());
    }

    public void RebuildCatalog()
    {
        MusicTrack previous = CurrentTrack;
        tracks.Clear();
        musicTypes.Clear();

        AddAllTracks();

        for (int i = history.Count - 1; i >= 0; i--)
        {
            MusicTrack replacement = FindTrack(history[i].Id);
            if (replacement == null)
            {
                history.RemoveAt(i);
                if (historyCursor >= i)
                    historyCursor--;
            }
            else
            {
                history[i] = replacement;
            }
        }
        historyCursor = Mathf.Clamp(historyCursor, -1, history.Count - 1);

        CurrentTrack = previous == null ? null : FindTrack(previous.Id);
        if (previous != null && CurrentTrack == null)
        {
            AudioSource?.Stop();
            if (AudioSource != null)
                AudioSource.clip = null;
            State = PlaybackState.Stopped;
        }
        CatalogVersion++;
        Debug.Log("[MusicManager] Catalog rebuilt: " + tracks.Count + " valid tracks.");
    }

    private void AddAllTracks()
    {
        int totalCount = 0;
        for (int categoryIndex = 0; categoryIndex < MusicCategories.Length; categoryIndex++)
        {
            string category = MusicCategories[categoryIndex];
            string resourcePath = MusicResourceRoot + "/" + category;
            AudioClip[] clips = Resources.LoadAll<AudioClip>(resourcePath);
            Array.Sort(clips, CompareClips);
            int categoryCount = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null || clip.length <= 0f || float.IsNaN(clip.length) ||
                    float.IsInfinity(clip.length))
                {
                    Debug.LogWarning("[MusicManager] Ignoring invalid clip in " + resourcePath + ".");
                    continue;
                }
                tracks.Add(new MusicTrack(clip.name, DisplayNameFor(clip.name), category,
                    resourcePath + "/" + clip.name, clip));
                categoryCount++;
            }
            musicTypes.Add(new Pair<string, int>(category, categoryCount));
            totalCount += categoryCount;
        }
        tracks.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
        musicTypes.Insert(0, new Pair<string, int>("All", totalCount));
        if (totalCount == 0)
            Debug.LogWarning("[MusicManager] No valid clips found in Resources/Musics/PMusic.");
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
        MusicTrack track = FindTrack(clipName);
        if (track == null || (!string.IsNullOrEmpty(type) &&
            !string.Equals(type, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(type, "All Music", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(track.Category, type, StringComparison.OrdinalIgnoreCase)))
            return false;
        return PlayTrack(track);
    }

    public bool Play(string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
            return false;
        MusicTrack track = FindTrack(clipName);
        return PlayTrack(track);
    }

    public bool QueuePlay(string resourcePath)
    {
        if (AudioSource == null || string.IsNullOrEmpty(resourcePath))
            return false;
        manualStop = false;
        CancelPendingLoad();
        State = PlaybackState.Loading;
        int requestVersion = ++playbackRequestVersion;
        loadingCoroutine = StartCoroutine(LoadAndPlay(resourcePath, ++loadVersion, requestVersion));
        return true;
    }

    private IEnumerator LoadAndPlay(string resourcePath, int requestVersion, int playbackVersion)
    {
        ResourceRequest request = Resources.LoadAsync<AudioClip>(resourcePath);
        yield return request;
        loadingCoroutine = null;
        if (requestVersion != loadVersion || playbackVersion != playbackRequestVersion ||
            manualStop || State != PlaybackState.Loading)
            yield break;

        AudioClip clip = request.asset as AudioClip;
        if (clip == null || clip.length <= 0f)
        {
            Debug.LogWarning("[MusicManager] Missing or invalid audio clip: " + resourcePath);
            State = PlaybackState.Stopped;
            yield break;
        }
        if (!Play(clip))
            State = PlaybackState.Stopped;
    }

    public bool PlayTrack(MusicTrack track)
    {
        return track != null && track.Clip != null && StartTrack(track, true);
    }

    public bool PlayTrack(int index) =>
index >= 0 && index < tracks.Count && PlayTrack(tracks[index]);

    public bool Play(AudioClip clip)
    {
        if (AudioSource == null || clip == null)
            return false;
        MusicTrack track = null;
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].Clip == clip)
            {
                track = tracks[i];
                break;
            }
        }
        return track != null && StartTrack(track, true);
    }

    private bool StartTrack(MusicTrack track, bool addToHistory)
    {
        if (AudioSource == null || track == null || track.Clip == null)
            return false;
        manualStop = false;
        waitingForNext = false;
        playbackRequestVersion++;
        CancelPendingLoad();
        AudioSource.clip = track.Clip;
        AudioSource.volume = volume;
        CurrentTrack = track;
        if (addToHistory)
            AddToHistory(track);
        State = PlaybackState.Playing;
        AudioSource.Play();
        return true;
    }

    public void Pause()
    {
        if (State == PlaybackState.Playing && AudioSource != null && AudioSource.isPlaying)
        {
            AudioSource.Pause();
            State = PlaybackState.Paused;
        }
    }

    public void Resume()
    {
        if (State == PlaybackState.Paused && AudioSource != null && AudioSource.clip != null)
        {
            manualStop = false;
            AudioSource.UnPause();
            State = PlaybackState.Playing;
        }
    }

    public void Stop()
    {
        manualStop = true;
        waitingForNext = false;
        playbackRequestVersion++;
        CancelPendingLoad();
        if (AudioSource != null)
        {
            AudioSource.Stop();
            AudioSource.clip = null;
        }
        State = PlaybackState.Stopped;
    }

    public bool PlayCurrent()
    {
        if (waitingForNext)
            return NextTrack();
        return CurrentTrack != null && StartTrack(CurrentTrack, false);
    }

    public bool PreviousTrack()
    {
        if (historyCursor <= 0 || historyCursor >= history.Count)
            return false;
        int targetCursor = historyCursor - 1;
        if (!StartTrack(history[targetCursor], false))
            return false;
        historyCursor = targetCursor;
        return true;
    }

    public bool NextTrack()
    {
        return PlayRandom();
    }

    private void AddToHistory(MusicTrack track)
    {
        if (track == null)
            return;
        if (historyCursor >= 0 && historyCursor < history.Count &&
            history[historyCursor].Id == track.Id)
            return;
        if (historyCursor < history.Count - 1)
            history.RemoveRange(historyCursor + 1, history.Count - historyCursor - 1);
        history.Add(track);
        historyCursor = history.Count - 1;
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
        AudioSource.time = Mathf.Clamp01(normalized) * Mathf.Max(0f, length - 0.01f);
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        if (AudioSource != null)
            AudioSource.volume = volume;
        PlayerPrefs.SetFloat(VolumePreference, volume);
        PlayerPrefs.Save();
    }

    public void SetGapSeconds(float value)
    {
        gapSeconds = Mathf.Clamp(value, 0f, 120f);
        PlayerPrefs.SetFloat(GapPreference, gapSeconds);
        PlayerPrefs.Save();
    }

    public bool PlayRandom()
    {
        if (tracks.Count == 0)
            return false;
        int candidateCount = 0;
        for (int i = 0; i < tracks.Count; i++)
        {
            if (CurrentTrack == null || tracks[i].Id != CurrentTrack.Id)
                candidateCount++;
        }
        if (candidateCount == 0)
            return false;
        int candidate = UnityEngine.Random.Range(0, candidateCount);
        for (int i = 0; i < tracks.Count; i++)
        {
            if (CurrentTrack != null && tracks[i].Id == CurrentTrack.Id)
                continue;
            if (candidate-- == 0)
                return PlayTrack(i);
        }
        return false;
    }

    private IEnumerator AutoPlayLoop()
    {
        while (true)
        {
            if (AudioSource == null || loadingCoroutine != null || State == PlaybackState.Paused)
            {
                yield return autoPlayPollDelay;
                continue;
            }

            if (State == PlaybackState.Playing && !AudioSource.isPlaying && AudioSource.clip != null)
            {
                float length = AudioSource.clip.length;
                int endTolerance = Mathf.Max(1, AudioSource.clip.frequency / 10);
                bool reachedEnd = length > 0f &&
                    (AudioSource.time >= length - 0.25f ||
                     (AudioSource.clip.samples > 0 &&
                      AudioSource.timeSamples >= AudioSource.clip.samples - endTolerance));
                if (reachedEnd)
                {
                    int requestVersion = playbackRequestVersion;
                    State = PlaybackState.Stopped;
                    waitingForNext = true;
                    if (gapSeconds > 0f)
                        yield return new WaitForSecondsRealtime(gapSeconds);
                    if (requestVersion == playbackRequestVersion &&
                        !manualStop)
                    {
                        NextTrack();
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
