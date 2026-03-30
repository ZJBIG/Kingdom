using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicManager : Singleton<MusicManager>
{
    [HideInInspector]public List<Pair<string, int>> MusicTypes = new List<Pair<string, int>>();
    public AudioSource AudioSource;
    protected override void Initialize()
    {
        AudioSource = GetComponent<AudioSource>();
        MusicTypes.Add(new Pair<string, int>("day", 6));
        MusicTypes.Add(new Pair<string, int>("silence", 12));
        MusicTypes.Add(new Pair<string, int>("village", 8));
    }
    //private void Start()
    //{
    //    StartCoroutine(PlayMusic());
    //}
    //public IEnumerator PlayMusic()
    //{
    //    while (true)
    //    {
    //        int idx = Random.Range(0, MusicTypes.Count);
    //        string type = MusicTypes[idx].first;
    //        int num = Random.Range(0, MusicTypes[idx].second);
    //        AudioClip clip = Resources.Load<AudioClip>($"Musics/{type}/{type}{num}");
    //        AudioSource.clip = clip;
    //        AudioSource.Play();
    //        yield return new WaitForSeconds(clip.length + Random.Range(5, 20));
    //    }
    //}
}
