using UnityEngine;

public class SettingViewer : MonoBehaviour
{
    public MusicViewer musicViewer;
    public enum SettingType
    {
        Music,
    }
    private void Start()
    {
        musicViewer.InitMusicSetting();
    }
    public void OpenSettingWindow() => GetComponent<RectTransform>().localPosition = GameManager.SettingViewerLocalPos;
    public void CloseSettingWindow() => GetComponent<RectTransform>().localPosition = GameManager.OutsideTheWindows;
}