using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicManager : Singleton<MusicManager>
{
    private readonly List<Pair<string, int>> musicTypes = new List<Pair<string, int>>();

    public IReadOnlyList<Pair<string, int>> MusicTypes => musicTypes;
    public AudioSource AudioSource { get; private set; }

    protected override void Initialize()
    {
        AudioSource = GetComponent<AudioSource>();
        musicTypes.Clear();
        musicTypes.Add(new Pair<string, int>("day", 6));
        musicTypes.Add(new Pair<string, int>("silence", 12));
        musicTypes.Add(new Pair<string, int>("village", 8));
    }

    private void Start()
    {
        StartCoroutine(AutoPlayLoop());
    }

    public bool Play(string type, string clipName)
    {
        if (AudioSource == null || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(clipName))
            return false;

        AudioClip clip = Resources.Load<AudioClip>($"Musics/{type}/{clipName}");
        if (clip == null)
        {
            Debug.LogWarning($"Missing music clip Musics/{type}/{clipName}.");
            return false;
        }

        return Play(clip);
    }

    public bool Play(AudioClip clip)
    {
        if (AudioSource == null || clip == null)
            return false;

        AudioSource.clip = clip;
        AudioSource.Play();
        return true;
    }

    public bool PlayRandom()
    {
        if (musicTypes.Count == 0)
            return false;

        int typeIndex = Random.Range(0, musicTypes.Count);
        Pair<string, int> type = musicTypes[typeIndex];
        if (type.Second <= 0)
            return false;

        string clipName = type.First + Random.Range(0, type.Second);
        return Play(type.First, clipName);
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

            if (!AudioSource.isPlaying)
            {
                yield return new WaitForSecondsRealtime(Random.Range(5f, 30f));
                PlayRandom();
            }

            yield return new WaitForSecondsRealtime(1f);
        }
    }
}
