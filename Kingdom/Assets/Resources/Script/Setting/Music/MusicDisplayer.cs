using TMPro;
using UnityEngine;

public class MusicDisplayer : MonoBehaviour
{
    public TMP_Text Type;
    public TMP_Text Label;
    public void Play()
    {
        AudioClip clip = Resources.Load<AudioClip>($"Musics/{Type.text}/{Label.text}");
        AudioSource AudioSource = MusicManager.Instance.AudioSource;
        AudioSource.clip = clip;
        AudioSource.Play();
    }
}
