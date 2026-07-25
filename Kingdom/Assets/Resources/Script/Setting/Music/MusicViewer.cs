using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

#pragma warning disable CS0649
public class MusicViewer : MonoBehaviour, IGameUIRefreshable
{
    [SerializeField, FormerlySerializedAs("MusicDisplayerPrefab")] private GameObject musicDisplayerPrefab;
    [SerializeField, FormerlySerializedAs("MusicList")] private Transform musicList;

    [SerializeField, FormerlySerializedAs("CurPlaying_Label")] private TMP_Text curPlayingLabel;
    [SerializeField, FormerlySerializedAs("CurPlaying_Time")] private TMP_Text curPlayingTime;

    [SerializeField, FormerlySerializedAs("PlayingTimeSlider")] private Slider playingTimeSlider;
    public AudioSource AudioSource => MusicManager.Instance.AudioSource;

    private readonly List<MusicDisplayer> displayers = new List<MusicDisplayer>();

    public void InitMusicSetting()
    {
        if (musicList == null || musicDisplayerPrefab == null || displayers.Count != 0)
            return;

        foreach (var (typeName, count) in MusicManager.Instance.MusicTypes)
        {
            for (int i = 0; i < count; i++)
            {
                MusicDisplayer displayer = Instantiate(musicDisplayerPrefab, musicList, false)
                    .GetComponent<MusicDisplayer>();
                AudioClip clip = Resources.Load<AudioClip>($"Musics/{typeName}/{typeName}{i}");
                if (clip == null)
                {
                    Debug.LogWarning($"Missing music clip Musics/{typeName}/{typeName}{i}.");
                    Destroy(displayer.gameObject);
                    continue;
                }

                displayer.Bind(typeName, clip);
                displayers.Add(displayer);
            }
        }

        if (AudioSource != null && AudioSource.clip == null)
            MusicManager.Instance.PlayRandom();
    }

    private void OnEnable()
    {
        GameUIRefreshManager.Instance?.Register(this);
        RefreshUI();
    }

    private void OnDisable()
    {
        GameUIRefreshManager.Instance?.Unregister(this);
    }

    public static (int minute, int second) TimeConvert(int time) => (time / 60, time % 60);

    public void RefreshUI() => RefreshNowPlaying();

    private void RefreshNowPlaying()
    {
        if (AudioSource == null || AudioSource.clip == null)
        {
            if (curPlayingLabel != null)
                curPlayingLabel.text = string.Empty;
            if (curPlayingTime != null)
                curPlayingTime.text = "0:00/0:00";
            if (playingTimeSlider != null)
                playingTimeSlider.value = 0f;
            return;
        }

        var curTime = TimeConvert((int)AudioSource.time);
        var musicLen = TimeConvert((int)AudioSource.clip.length);
        if (curPlayingLabel != null)
            curPlayingLabel.text = AudioSource.clip.name;
        if (curPlayingTime != null)
            curPlayingTime.text = $"{curTime.minute}:{curTime.second:D2}/{musicLen.minute}:{musicLen.second:D2}";
        if (playingTimeSlider != null)
            playingTimeSlider.value = AudioSource.clip.length <= 0f
                ? 0f
                : AudioSource.time / AudioSource.clip.length;
    }
}
#pragma warning restore CS0649
