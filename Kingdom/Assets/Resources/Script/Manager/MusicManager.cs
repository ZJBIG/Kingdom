using System.Collections;
using UnityEngine;

public class MusicManager : Singleton<MusicManager>
{
    AudioSource AudioSource;
    protected override void Initialize()
    {
        base.Initialize();
        AudioSource = GetComponent<AudioSource>();
    }
    private void Start()
    {
        StartCoroutine(PlayMusic());
    }
    public IEnumerator PlayMusic()
    {
        while (true)
        {
            string type = char.ToString((char)(Random.Range(0, 2) + 'A'));
            AudioClip clip = Resources.Load<AudioClip>("Sounds/BGSong" + type);
            AudioSource.clip = clip;
            AudioSource.Play();
            yield return new WaitForSeconds(clip.length + Random.Range(5, 20));
        }
    }
}
