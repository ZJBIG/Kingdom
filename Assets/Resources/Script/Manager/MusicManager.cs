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
    private bool playbackObserved;
    private bool applicationPaused;
    private double expectedPlaybackEnd;

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
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<MusicCatalogEntry> entries = catalogHandle.Result.Entries;
        for (int categoryIndex = 0; categoryIndex < MusicCategories.Length; categoryIndex++)
        {
            string category = MusicCategories[categoryIndex];
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
            }
        }

        tracks.Sort(CompareTracks);
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
        StartLoadedTrack(track, clipHandle.Result);
    }

    private void StartLoadedTrack(MusicTrack track, AudioClip clip)
    {
        if (AudioSource == null || track == null || clip == null)
            return;
        manualStop = false;
        waitingForNext = false;
        AudioSource.clip = clip;
        AudioSource.volume = volume;
        CurrentTrack = track;
        AddToHistory(track);
        State = PlaybackState.Playing;
        AudioSource.Play();
        playbackObserved = AudioSource.isPlaying;
        expectedPlaybackEnd = AudioSettings.dspTime + clip.length / Mathf.Max(.01f, AudioSource.pitch);
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
            expectedPlaybackEnd = AudioSettings.dspTime +
                (AudioSource.clip.length - AudioSource.time) / Mathf.Max(.01f, AudioSource.pitch);
        }
    }

    public void Stop()
    {
        manualStop = true;
        waitingForNext = false;
        playbackObserved = false;
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
        expectedPlaybackEnd = AudioSettings.dspTime +
            (length - AudioSource.time) / Mathf.Max(.01f, AudioSource.pitch);
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
            if (AudioSource == null || loadingCoroutine != null || State == PlaybackState.Paused ||
                applicationPaused || AudioListener.pause)
            {
                yield return autoPlayPollDelay;
                continue;
            }
            if (State == PlaybackState.Playing && AudioSource.clip != null)
            {
                playbackObserved |= AudioSource.isPlaying;
                // Unity may reset time/timeSamples after the clip ends. Observe
                // the playing-to-stopped transition instead of that reset cursor.
                if (!AudioSource.isPlaying &&
                    (playbackObserved || AudioSettings.dspTime >= expectedPlaybackEnd))
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

    private void OnApplicationPause(bool paused) => applicationPaused = paused;
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
        StrategicSupplyPause,
        Click
    }

    private const int SampleRate = 22050;
    private const float OutputVolume = .42f;
    private const string VolumePreference = "Kingdom.Sfx.Volume";
    private const string MutePreference = "Kingdom.Sfx.Mute";
    private static UIButtonSoundManager cachedManager;
    private AudioSource source;
    private AudioSource actionSource;
    private AudioSource milestoneSource;
    private AudioClip[][] clips;
    private int[] nextVariant;
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
            ConfigureSource(source, 160);
            actionSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(actionSource, 128);
            milestoneSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(milestoneSource, 64);
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, 1f));
            muted = PlayerPrefs.GetInt(MutePreference, 0) != 0;
            ApplyVolume();
            int soundCount = (int)Sound.Click + 1;
            clips = new AudioClip[soundCount][];
            nextVariant = new int[soundCount];
            for (int i = 0; i < soundCount; i++)
            {
                clips[i] = new AudioClip[3];
                for (int variant = 0; variant < 3; variant++)
                    clips[i][variant] = CreateSound((Sound)i, variant);
            }
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        SubscribeResearch(FindObjectOfType<ResearchManager>());
    }

    public static void BindResearchManager(ResearchManager manager) => EnsureManager().SubscribeResearch(manager);

    private void SubscribeResearch(ResearchManager manager)
    {
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
        if (clips != null)
            foreach (AudioClip[] variants in clips)
                foreach (AudioClip clip in variants)
                    ReleaseTone(clip);
    }

    private static void ReleaseTone(AudioClip clip)
    {
        if (clip == null) return;
        if (Application.isPlaying) Destroy(clip);
        else DestroyImmediate(clip);
    }

    private void ApplyVolume()
    {
        if (source != null)
            source.volume = muted ? 0f : volume * OutputVolume;
        if (actionSource != null)
            actionSource.volume = muted ? 0f : volume * OutputVolume;
        if (milestoneSource != null)
            milestoneSource.volume = muted ? 0f : volume * OutputVolume;
    }

    private void OnResearchCompleted(ResearchState state)
    {
        if (state == null || state.Definition == null)
            return;
        Play(state.Definition.AdvancesTechLevel ? Sound.EraBreakthrough : Sound.ResearchComplete);
    }

    private static void ConfigureSource(AudioSource output, int priority)
    {
        output.playOnAwake = false;
        output.loop = false;
        output.spatialBlend = 0f;
        output.priority = priority;
    }

    private void PlayInternal(Sound sound)
    {
        int index = (int)sound;
        if (source == null || index < 0 || index >= clips.Length)
            return;
        PlayRequested?.Invoke(sound);
        if (muted || volume <= 0f)
            return;

        bool milestone = sound == Sound.ResearchComplete || sound == Sound.EraBreakthrough ||
            sound == Sound.StrategicCommit;
        AudioSource output = milestone ? milestoneSource :
            sound == Sound.Click || sound == Sound.Detail ? source : actionSource;
        // A batch of completed research must not cut off the era celebration.
        if (sound == Sound.ResearchComplete && output.isPlaying && output.clip != null &&
            output.clip.name.StartsWith("SFX_EraBreakthrough", StringComparison.Ordinal))
            return;
        AudioClip clip = clips[index][nextVariant[index]];
        nextVariant[index] = (nextVariant[index] + 1) % clips[index].Length;
        // Three independent voices keep rapid input bounded without delaying feedback.
        output.clip = clip;
        output.Play();
    }

    private static AudioClip CreateSound(Sound sound, int variant)
    {
        float duration = sound == Sound.EraBreakthrough ? 1.05f :
            sound == Sound.ResearchComplete || sound == Sound.StrategicCommit ? .65f :
            sound == Sound.Click ? .045f : .32f;
        float[] samples = new float[Mathf.RoundToInt(SampleRate * duration)];
        float tuning = 1f + (variant - 1) * .025f;
        uint noise = (uint)(179 + (int)sound * 977 + variant * 131);
        switch (sound)
        {
            case Sound.Click:
                AddNote(samples, 0f, .04f, 1050f, .24f, 0, tuning, ref noise);
                break;
            case Sound.Detail:
                AddNote(samples, 0f, .11f, 660f, .26f, 0, tuning, ref noise);
                AddNote(samples, .045f, .13f, 880f, .18f, 1, tuning, ref noise);
                break;
            case Sound.Build:
                AddNote(samples, 0f, .09f, 330f, .48f, 0, tuning, ref noise);
                AddNote(samples, .085f, .12f, 440f, .40f, 0, tuning, ref noise);
                break;
            case Sound.Upgrade:
                AddNote(samples, 0f, .09f, 330f, .36f, 0, tuning, ref noise);
                AddNote(samples, .07f, .18f, 880f, .34f, 2, tuning, ref noise);
                AddNote(samples, .14f, .17f, 1320f, .24f, 1, tuning, ref noise);
                break;
            case Sound.Deconstruct:
                AddNote(samples, 0f, .10f, 410f, .37f, 0, tuning, ref noise);
                AddNote(samples, .065f, .13f, 270f, .32f, 0, tuning, ref noise);
                AddNote(samples, .14f, .15f, 160f, .26f, 0, tuning, ref noise);
                break;
            case Sound.WorkshopPurchase:
                AddNote(samples, 0f, .10f, 260f, .32f, 0, tuning, ref noise);
                AddNote(samples, .055f, .23f, 1174.66f, .34f, 2, tuning, ref noise);
                break;
            case Sound.Purchase:
            case Sound.Sell:
                bool buying = sound == Sound.Purchase;
                AddNote(samples, 0f, .17f, buying ? 1046.5f : 1318.5f, .33f, 2, tuning, ref noise);
                AddNote(samples, .075f, .22f, buying ? 1568f : 784f, .30f, 2, tuning, ref noise);
                break;
            case Sound.ResearchComplete:
                AddNote(samples, 0f, .32f, 659.25f, .30f, 1, tuning, ref noise);
                AddNote(samples, .12f, .36f, 783.99f, .28f, 1, tuning, ref noise);
                AddNote(samples, .25f, .39f, 1046.5f, .30f, 1, tuning, ref noise);
                break;
            case Sound.EraBreakthrough:
                AddNote(samples, 0f, .28f, 261.63f, .30f, 0, tuning, ref noise);
                AddNote(samples, .12f, .43f, 523.25f, .28f, 1, tuning, ref noise);
                AddNote(samples, .28f, .48f, 659.25f, .27f, 1, tuning, ref noise);
                AddNote(samples, .44f, .60f, 783.99f, .27f, 1, tuning, ref noise);
                AddNote(samples, .44f, .60f, 1046.5f, .24f, 1, tuning, ref noise);
                break;
            case Sound.StrategicStart:
                AddNote(samples, 0f, .12f, 392f, .32f, 0, tuning, ref noise);
                AddNote(samples, .08f, .22f, 587.33f, .30f, 1, tuning, ref noise);
                break;
            case Sound.StrategicStop:
                AddNote(samples, 0f, .14f, 523.25f, .28f, 0, tuning, ref noise);
                AddNote(samples, .10f, .20f, 349.23f, .26f, 0, tuning, ref noise);
                break;
            case Sound.StrategicCommit:
                AddNote(samples, 0f, .30f, 392f, .30f, 1, tuning, ref noise);
                AddNote(samples, .14f, .36f, 587.33f, .28f, 2, tuning, ref noise);
                AddNote(samples, .28f, .36f, 783.99f, .27f, 1, tuning, ref noise);
                break;
            case Sound.StrategicSupplyPause:
                AddNote(samples, 0f, .13f, 440f, .25f, 0, tuning, ref noise);
                AddNote(samples, .15f, .13f, 440f, .20f, 0, tuning, ref noise);
                break;
        }
        AudioClip clip = AudioClip.Create($"SFX_{sound}_{variant}", samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Damped resonances: wood/body, soft bell, and small metal/tool accents.
    private static void AddNote(float[] samples, float start, float duration, float frequency,
        float amplitude, int timbre, float tuning, ref uint noise)
    {
        int offset = Mathf.RoundToInt(start * SampleRate);
        int count = Mathf.Min(Mathf.RoundToInt(duration * SampleRate), samples.Length - offset);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float phase = 2f * Mathf.PI * frequency * tuning * t;
            float progress = i / (float)count;
            float attack = Mathf.Min(1f, t / .002f);
            float release = Mathf.Min(1f, (count - 1 - i) / (SampleRate * .012f));
            float body;
            if (timbre == 0)
            {
                noise ^= noise << 13;
                noise ^= noise >> 17;
                noise ^= noise << 5;
                float grain = (noise & 65535) / 32767.5f - 1f;
                body = .65f * Mathf.Sin(phase) + .20f * Mathf.Sin(phase * 1.58f) +
                    .15f * grain * Mathf.Exp(-t * 120f);
            }
            else
            {
                float partial = timbre == 1 ? 2.01f : 2.76f;
                body = .65f * Mathf.Sin(phase) + .23f * Mathf.Sin(phase * partial) * Mathf.Exp(-t * 12f) +
                    .12f * Mathf.Sin(phase * 4.07f) * Mathf.Exp(-t * 22f);
            }
            samples[offset + i] += body * amplitude * attack * release *
                Mathf.Exp(-progress * (timbre == 0 ? 6f : 4f));
        }
    }
}
