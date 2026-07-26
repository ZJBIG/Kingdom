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
    private AudioClip lastDisplayedClip;
    private int lastDisplayedSecond = -1;
    private int lastDisplayedLength = -1;
    private bool lastDisplayWasEmpty;

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
                string clipName = typeName + i;
                string resourcePath = $"Musics/{typeName}/{clipName}";
                displayer.Bind(typeName, resourcePath, clipName);
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
            if (!lastDisplayWasEmpty)
            {
                if (curPlayingLabel != null)
                    curPlayingLabel.text = string.Empty;
                if (curPlayingTime != null)
                    curPlayingTime.text = "0:00/0:00";
                if (playingTimeSlider != null)
                    playingTimeSlider.value = 0f;
            }

            lastDisplayedClip = null;
            lastDisplayedSecond = -1;
            lastDisplayedLength = -1;
            lastDisplayWasEmpty = true;
            return;
        }

        lastDisplayWasEmpty = false;
        AudioClip clip = AudioSource.clip;
        int elapsedSecond = (int)AudioSource.time;
        int lengthSecond = (int)clip.length;
        if (clip != lastDisplayedClip)
        {
            if (curPlayingLabel != null)
                curPlayingLabel.text = clip.name;
            lastDisplayedClip = clip;
        }

        var curTime = TimeConvert((int)AudioSource.time);
        var musicLen = TimeConvert(lengthSecond);
        if (elapsedSecond != lastDisplayedSecond || lengthSecond != lastDisplayedLength)
        {
            if (curPlayingTime != null)
                curPlayingTime.text = $"{curTime.minute}:{curTime.second:D2}/{musicLen.minute}:{musicLen.second:D2}";
            lastDisplayedSecond = elapsedSecond;
            lastDisplayedLength = lengthSecond;
        }

        if (playingTimeSlider != null)
            playingTimeSlider.value = clip.length <= 0f ? 0f : AudioSource.time / clip.length;
    }
}
#pragma warning restore CS0649
