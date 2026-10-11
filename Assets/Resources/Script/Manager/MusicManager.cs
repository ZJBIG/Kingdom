using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Background-music catalog and playback backed by Addressables.
/// </summary>
public class MusicManager : Singleton<MusicManager>
{
    private const string VolumePreference = "Kingdom.Music.Volume";
    private const string GapPreference = "Kingdom.Music.GapSeconds";
    public const float MaxGapSeconds = 120f;
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
        public string Address { get; }
        public float DurationSeconds { get; }

        public MusicTrack(string id, string label, string category, string address,
            float durationSeconds)
        {
            Id = id;
            Label = label;
            Category = category;
            Address = address;
            DurationSeconds = durationSeconds;
        }
    }

    private readonly List<Pair<string, int>> musicTypes = new();
    private readonly List<MusicTrack> tracks = new();
    private readonly List<MusicTrack> history = new();
    private readonly WaitForSecondsRealtime autoPlayPollDelay = new(.25f);
    private AsyncOperationHandle<MusicCatalog> catalogHandle;
    private AsyncOperationHandle<AudioClip> clipHandle;
    private Coroutine catalogCoroutine;
    private Coroutine loadingCoroutine;
    private bool hasCatalogHandle;
    private bool hasClipHandle;
    private bool manualStop;
    private float volume = 1f;
    private float gapSeconds = 5f;
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
        AudioSource.spatialBlend = 0f;
        volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, 1f));
        gapSeconds = Mathf.Clamp(PlayerPrefs.GetFloat(GapPreference, 5f), 0f, MaxGapSeconds);
        AudioSource.volume = volume;
        RebuildCatalog();
    }

    private void Start()
    {
        StartCoroutine(AutoPlayLoop());
    }

    protected override void OnDestroy()
    {
        CancelCatalogLoad();
        playbackRequestVersion++;
        CancelPendingLoad();
        ClearAudioPlayback(true);
        base.OnDestroy();
    }

    public void RebuildCatalog()
    {
        CancelCatalogLoad();
        catalogCoroutine = StartCoroutine(LoadCatalog());
    }

    private IEnumerator LoadCatalog()
    {
        catalogHandle = Addressables.LoadAssetAsync<MusicCatalog>(MusicCatalog.AddressableAddress);
        hasCatalogHandle = true;
        yield return catalogHandle;
        catalogCoroutine = null;
        if (!hasCatalogHandle || catalogHandle.Status != AsyncOperationStatus.Succeeded ||
            catalogHandle.Result == null)
        {
            Debug.LogError("[MusicManager] Failed to load Addressables music catalog.");
            ReleaseCatalogHandle();
            yield break;
        }

        MusicTrack previous = CurrentTrack;
        tracks.Clear();
        musicTypes.Clear();
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<MusicCatalogEntry> entries = catalogHandle.Result.Entries;
        for (int categoryIndex = 0; categoryIndex < MusicCategories.Length; categoryIndex++)
        {
            string category = MusicCategories[categoryIndex];
            int categoryCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                MusicCatalogEntry entry = entries[i];
                if (entry == null || !string.Equals(entry.Category, category,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrEmpty(entry.Id) || string.IsNullOrEmpty(entry.Address) ||
                    !ids.Add(entry.Id) || entry.DurationSeconds <= 0f ||
                    float.IsNaN(entry.DurationSeconds) || float.IsInfinity(entry.DurationSeconds))
                {
                    Debug.LogWarning("[MusicManager] Ignoring invalid catalog entry in " + category + ".");
                    continue;
                }
                tracks.Add(new MusicTrack(entry.Id, entry.Label, category, entry.Address,
                    entry.DurationSeconds));
                categoryCount++;
            }
            musicTypes.Add(new Pair<string, int>(category, categoryCount));
        }

        tracks.Sort(CompareTracks);
        musicTypes.Insert(0, new Pair<string, int>("All", tracks.Count));
        ReconcileHistory(previous);
        CatalogVersion++;
        Debug.Log("[MusicManager] Addressables catalog contains " + tracks.Count + " tracks.");
        if (tracks.Count > 0 && CurrentTrack == null && !manualStop)
            PlayRandom();
    }

    private void ReconcileHistory(MusicTrack previous)
    {
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
            ClearAudioPlayback(true);
            State = PlaybackState.Stopped;
        }
    }

    private static int CompareTracks(MusicTrack left, MusicTrack right)
    {
        return string.Compare(left == null ? string.Empty : left.Label,
            right == null ? string.Empty : right.Label, StringComparison.OrdinalIgnoreCase);
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
            if (string.Equals(tracks[i].Id, id, StringComparison.OrdinalIgnoreCase))
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
        return PlayTrack(FindTrack(clipName));
    }

    public bool PlayTrack(MusicTrack track)
    {
        if (AudioSource == null || track == null || string.IsNullOrEmpty(track.Address))
            return false;
        manualStop = false;
        playbackRequestVersion++;
        int requestVersion = playbackRequestVersion;
        CancelPendingLoad();
        ClearAudioPlayback(true);
        CurrentTrack = track;
        State = PlaybackState.Loading;
        loadingCoroutine = StartCoroutine(LoadAndPlay(track, requestVersion));
        return true;
    }

    public bool PlayTrack(int index)
    {
        return index >= 0 && index < tracks.Count && PlayTrack(tracks[index]);
    }

    public bool Play(AudioClip clip)
    {
        if (clip == null)
            return false;
        MusicTrack track = FindTrack(clip.name);
        if (track == null)
            return false;
        playbackRequestVersion++;
        CancelPendingLoad();
        ClearAudioPlayback(true);
        return StartLoadedTrack(track, clip, true);
    }

    private IEnumerator LoadAndPlay(MusicTrack track, int requestVersion)
    {
        clipHandle = Addressables.LoadAssetAsync<AudioClip>(track.Address);
        hasClipHandle = true;
        yield return clipHandle;
        loadingCoroutine = null;
        if (requestVersion != playbackRequestVersion || manualStop ||
            State != PlaybackState.Loading || CurrentTrack != track)
        {
            ReleaseClipHandle();
            yield break;
        }
        if (clipHandle.Status != AsyncOperationStatus.Succeeded || clipHandle.Result == null ||
            clipHandle.Result.length <= 0f)
        {
            Debug.LogWarning("[MusicManager] Missing or invalid Addressables clip: " + track.Address);
            ReleaseClipHandle();
            ClearAudioPlayback(true);
            State = PlaybackState.Stopped;
            yield break;
        }
        StartLoadedTrack(track, clipHandle.Result, true);
    }

    private bool StartLoadedTrack(MusicTrack track, AudioClip clip, bool addToHistory)
    {
        if (AudioSource == null || track == null || clip == null)
            return false;
        manualStop = false;
        waitingForNext = false;
        AudioSource.clip = clip;
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
        ClearAudioPlayback(false);
        State = PlaybackState.Stopped;
    }

    public bool PlayCurrent()
    {
        if (waitingForNext)
            return NextTrack();
        return CurrentTrack != null && PlayTrack(CurrentTrack);
    }

    public bool PreviousTrack()
    {
        if (historyCursor <= 0 || historyCursor >= history.Count)
            return false;
        int targetCursor = historyCursor - 1;
        if (!PlayTrack(history[targetCursor]))
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

    private void CancelCatalogLoad()
    {
        if (catalogCoroutine != null)
        {
            StopCoroutine(catalogCoroutine);
            catalogCoroutine = null;
        }
        ReleaseCatalogHandle();
    }

    private void CancelPendingLoad()
    {
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
    }

    private void ClearAudioPlayback(bool clearCurrentTrack)
    {
        if (AudioSource != null)
        {
            AudioSource.Stop();
            AudioSource.clip = null;
        }
        ReleaseClipHandle();
        if (clearCurrentTrack)
            CurrentTrack = null;
        waitingForNext = false;
    }

    private void ReleaseCatalogHandle()
    {
        if (!hasCatalogHandle)
            return;
        Addressables.Release(catalogHandle);
        hasCatalogHandle = false;
    }

    private void ReleaseClipHandle()
    {
        if (!hasClipHandle)
            return;
        Addressables.Release(clipHandle);
        hasClipHandle = false;
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
        gapSeconds = Mathf.Clamp(value, 0f, MaxGapSeconds);
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
            return tracks.Count == 1 && PlayTrack(tracks[0]);
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
                    int requestVersion = ++playbackRequestVersion;
                    State = PlaybackState.Stopped;
                    waitingForNext = true;
                    if (gapSeconds > 0f)
                        yield return new WaitForSecondsRealtime(gapSeconds);
                    if (requestVersion == playbackRequestVersion && !manualStop)
                        NextTrack();
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
    public enum Sound
    {
        Detail,
        Purchase,
        Sell,
        Build,
        Upgrade,
        Deconstruct,
        WorkshopPurchase,
        ResearchComplete,
        EraBreakthrough,
        StrategicStart,
        StrategicStop,
        StrategicCommit,
        StrategicSupplyPause
    }

    private const int SampleRate = 44100;
    private const float OutputVolume = .42f;
    private const string VolumePreference = "Kingdom.Sfx.Volume";
    private const string MutePreference = "Kingdom.Sfx.Mute";
    private static UIButtonSoundManager cachedManager;
    private AudioSource source;
    private AudioClip detailClip;
    private AudioClip purchaseClip;
    private AudioClip sellClip;
    private AudioClip buildClip;
    private AudioClip upgradeClip;
    private AudioClip deconstructClip;
    private AudioClip researchClip;
    private AudioClip eraClip;
    private ResearchManager subscribedResearchManager;
    private float volume = 1f;
    private bool muted;
    private bool initialized;

    /// <summary>
    /// Fired when a typed SFX request reaches the audio output path. Tests and
    /// diagnostics can observe result feedback without inspecting AudioSource
    /// internals or using reflection.
    /// </summary>
    public static event Action<Sound> PlayRequested;

    public static float Volume => cachedManager == null ?
        Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, 1f)) : cachedManager.volume;
    public static bool IsMuted => cachedManager == null ?
        PlayerPrefs.GetInt(MutePreference, 0) != 0 : cachedManager.muted;
    public static float SfxVolume => Volume;
    public static bool SfxMuted => IsMuted;

    public static void Play(Sound sound)
    {
        EnsureManager().PlayInternal(sound);
    }

    public static void EnsureInitialized() => EnsureManager();

    public static void SetVolume(float value)
    {
        UIButtonSoundManager manager = EnsureManager();
        manager.volume = Mathf.Clamp01(value);
        manager.ApplyVolume();
        PlayerPrefs.SetFloat(VolumePreference, manager.volume);
        PlayerPrefs.Save();
    }

    public static void SetMuted(bool value)
    {
        UIButtonSoundManager manager = EnsureManager();
        manager.muted = value;
        manager.ApplyVolume();
        PlayerPrefs.SetInt(MutePreference, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void ToggleMuted() => SetMuted(!IsMuted);

    private static UIButtonSoundManager EnsureManager()
    {
        UIButtonSoundManager manager = cachedManager;
        if (manager != null)
        {
            manager.InitializeAudio();
            return manager;
        }
        manager = FindObjectOfType<UIButtonSoundManager>();
        if (manager == null)
            manager = new GameObject("UIButtonSoundManager").AddComponent<UIButtonSoundManager>();
        manager.InitializeAudio();
        cachedManager = manager;
        return manager;
    }

    private void Awake()
    {
        InitializeAudio();
    }

    private void InitializeAudio()
    {
        if (!initialized)
        {
            initialized = true;
            source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, 1f));
            muted = PlayerPrefs.GetInt(MutePreference, 0) != 0;
            ApplyVolume();
            detailClip = CreateTone("UI_Detail", 720f, .065f, .42f);
            purchaseClip = CreateTwoTone("UI_Purchase", 430f, 650f, .12f, .46f);
            sellClip = CreateTwoTone("UI_Sell", 560f, 300f, .13f, .44f);
            buildClip = CreateTwoTone("UI_Build", 360f, 540f, .11f, .42f);
            upgradeClip = CreateTwoTone("UI_Upgrade", 440f, 760f, .14f, .44f);
            deconstructClip = CreateTwoTone("UI_Deconstruct", 620f, 300f, .13f, .40f);
            researchClip = CreateTwoTone("UI_ResearchComplete", 520f, 880f, .22f, .40f);
            eraClip = CreateTwoTone("UI_EraBreakthrough", 420f, 980f, .30f, .46f);
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        ResearchManager manager = FindObjectOfType<ResearchManager>();
        if (manager != subscribedResearchManager)
        {
            if (subscribedResearchManager != null)
                subscribedResearchManager.ResearchCompleted -= OnResearchCompleted;
            subscribedResearchManager = manager;
            if (subscribedResearchManager != null)
                subscribedResearchManager.ResearchCompleted += OnResearchCompleted;
        }
    }

    private void OnDestroy()
    {
        if (subscribedResearchManager != null)
            subscribedResearchManager.ResearchCompleted -= OnResearchCompleted;
        if (cachedManager == this)
            cachedManager = null;
    }

    private void ApplyVolume()
    {
        if (source != null)
            source.volume = muted ? 0f : volume * OutputVolume;
    }

    private void OnResearchCompleted(ResearchState state)
    {
        if (state == null || state.Definition == null)
            return;
        Play(state.Definition.AdvancesTechLevel ? Sound.EraBreakthrough : Sound.ResearchComplete);
    }

    private void PlayInternal(Sound sound)
    {
        if (source == null)
            return;
        AudioClip clip = sound switch
        {
            Sound.Purchase => purchaseClip,
            Sound.Sell => sellClip,
            Sound.Build => buildClip,
            Sound.Upgrade => upgradeClip,
            Sound.Deconstruct => deconstructClip,
            Sound.WorkshopPurchase => purchaseClip,
            Sound.ResearchComplete => researchClip,
            Sound.EraBreakthrough => eraClip,
            Sound.StrategicStart => buildClip,
            Sound.StrategicStop => deconstructClip,
            Sound.StrategicCommit => eraClip,
            Sound.StrategicSupplyPause => sellClip,
            _ => detailClip
        };
        if (clip != null)
        {
            PlayRequested?.Invoke(sound);
            source.PlayOneShot(clip);
        }
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
