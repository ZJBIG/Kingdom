using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MusicViewer : MonoBehaviour
{
    public GameObject MusicDisplayerPrefab;
    public Transform MusicList;

    public TMP_Text CurPlaying_Label;
    public TMP_Text CurPlaying_Time;

    public Slider PlayingTimeSlider;
    public AudioSource AudioSource => MusicManager.Instance.AudioSource;
    public void InitMusicSetting()
    {
        foreach (var (typeName, idx) in MusicManager.Instance.MusicTypes)
            for (int i = 0; i < idx; i++)
            {
                var Displayer = Instantiate(MusicDisplayerPrefab, MusicList).GetComponent<MusicDisplayer>();
                Displayer.Type.text = typeName;
                Displayer.Label.text = typeName + i;
            }
        if (!AudioSource.clip)
        {
            int idx = Random.Range(0, MusicList.childCount);
            MusicList.GetChild(idx).GetComponent<MusicDisplayer>().Play();
        }
    }

    private void Start()
    {
        StartCoroutine(PlayMusic());
        StartCoroutine(UpdateUI());
    }
    public static (int minute, int second) TimeConvert(int time) => (time / 60, time % 60);
    IEnumerator PlayMusic()
    {
        while (true)
        {
            if (!AudioSource.isPlaying)
            {
                yield return new WaitForSecondsRealtime(Random.Range(5f, 30f));
                int idx = Random.Range(0, MusicList.childCount);
                MusicList.GetChild(idx).GetComponent<MusicDisplayer>().Play();
            }
            yield return new WaitForSecondsRealtime(1f);
        }
    }
    IEnumerator UpdateUI()
    {
        while (true)
        {
            var curTime = TimeConvert((int)AudioSource.time);
            var musicLen = TimeConvert((int)AudioSource.clip.length);
            CurPlaying_Label.text = AudioSource.clip.name;
            CurPlaying_Time.text = $"{curTime.minute}:{curTime.second:D2}/{musicLen.minute}:{musicLen.second:D2}";
            PlayingTimeSlider.value = AudioSource.time / AudioSource.clip.length;
            yield return new WaitForSecondsRealtime(1f);
        }
    }
}
