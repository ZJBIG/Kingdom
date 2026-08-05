using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime music catalog and playback service.
/// Audio clips remain ordinary Resources assets; this manager owns only
/// playback state and presentation-facing catalog metadata.
/// </summary>
public class MusicManager : Singleton<MusicManager>
{
    private const string VolumePreference = "Kingdom.Music.Volume";
    private const string GapPreference = "Kingdom.Music.GapSeconds";

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
    private Coroutine loadingCoroutine;
    private Coroutine autoPlayCoroutine;
    private bool manualStop;
    private float volume = 1f;
    private float gapSeconds = 5f;
    private int lastRandomIndex = -1;

    public IReadOnlyList<Pair<string, int>> MusicTypes => musicTypes;
    public IReadOnlyList<MusicTrack> Tracks => tracks;
    public AudioSource AudioSource { get; private set; }
    public MusicTrack CurrentTrack { get; private set; }
    public float Volume => volume;
    public float GapSeconds => gapSeconds;
    public bool IsPlaying => AudioSource != null && AudioSource.isPlaying;
    public bool IsPaused => AudioSource != null && AudioSource.clip != null && !AudioSource.isPlaying && AudioSource.time > 0f;
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
        autoPlayCoroutine = StartCoroutine(AutoPlayLoop());
    }

    public void RebuildCatalog()
    {
        tracks.Clear();
        musicTypes.Clear();
        AddCategory("day", "日间");
        AddCategory("silence", "静默");
        AddCategory("village", "村落");
    }

    private void AddCategory(string category, string label)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Musics/" + category);
        Array.Sort(clips, (left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        int count = 0;
        foreach (AudioClip clip in clips)
        {
            if (clip == null)
                continue;
            string path = "Musics/" + category + "/" + clip.name;
            tracks.Add(new MusicTrack(category + ":" + clip.name, clip.name, label, path, clip));
            count++;
        }
        musicTypes.Add(new Pair<string, int>(category, count));
    }

    // Compatibility with the previous manager API.
    public bool Play(string type, string clipName)
    {
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(clipName))
            return false;
        return QueuePlay("Musics/" + type + "/" + clipName);
    }

    public bool QueuePlay(string resourcePath)
    {
        if (AudioSource == null || string.IsNullOrEmpty(resourcePath))
            return false;
        manualStop = false;
        if (loadingCoroutine != null)
            StopCoroutine(loadingCoroutine);
        loadingCoroutine = StartCoroutine(LoadAndPlay(resourcePath));
        return true;
    }

    private IEnumerator LoadAndPlay(string resourcePath)
    {
        ResourceRequest request = Resources.LoadAsync<AudioClip>(resourcePath);
        yield return request;
        AudioClip clip = request.asset as AudioClip;
        loadingCoroutine = null;
        if (clip == null)
        {
            Debug.LogWarning("Missing music clip " + resourcePath + ".");
            yield break;
        }
        Play(clip);
    }

    public bool PlayTrack(MusicTrack track)
    {
        if (track == null || track.Clip == null)
            return false;
        CurrentTrack = track;
        return Play(track.Clip);
    }

    public bool PlayTrack(int index)
    {
        if (index < 0 || index >= tracks.Count)
            return false;
        return PlayTrack(tracks[index]);
    }

    public bool Play(AudioClip clip)
    {
        if (AudioSource == null || clip == null)
            return false;
        manualStop = false;
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
        AudioSource.clip = clip;
        AudioSource.volume = volume;
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
            AudioSource.Pause();
    }

    public void Resume()
    {
        if (AudioSource != null && AudioSource.clip != null)
        {
            manualStop = false;
            AudioSource.UnPause();
        }
    }

    public void Stop()
    {
        manualStop = true;
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
        if (AudioSource != null)
            AudioSource.Stop();
    }

    public void SeekNormalized(float normalized)
    {
        if (AudioSource == null || AudioSource.clip == null)
            return;
        float length = AudioSource.clip.length;
        if (length <= 0f || float.IsNaN(length) || float.IsInfinity(length) ||
            float.IsNaN(normalized) || float.IsInfinity(normalized))
            return;
        // Unity's native audio backend rejects the exact clip end for some
        // compressed formats. Keep the target strictly inside the clip.
        float safeEnd = Mathf.Max(0f, length - 0.01f);
        AudioSource.time = Mathf.Clamp01(normalized) * safeEnd;
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
            if (AudioSource == null)
            {
                yield return new WaitForSecondsRealtime(1f);
                continue;
            }
            if (!AudioSource.isPlaying && AudioSource.clip == null)
            {
                if (manualStop)
                {
                    yield return new WaitForSecondsRealtime(.25f);
                    continue;
                }
                if (gapSeconds > 0f)
                    yield return new WaitForSecondsRealtime(gapSeconds);
                PlayRandom();
            }
            else if (!AudioSource.isPlaying && AudioSource.clip != null && !manualStop)
            {
                AudioSource.clip = null;
                if (gapSeconds > 0f)
                    yield return new WaitForSecondsRealtime(gapSeconds);
                PlayRandom();
            }
            yield return new WaitForSecondsRealtime(.25f);
        }
    }
}
