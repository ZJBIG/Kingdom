using UnityEngine;

public class SettingViewer : MonoBehaviour
{
    [SerializeField] private MusicViewer musicViewer;
    public enum SettingType
    {
        Music,
    }
    private void OnEnable()
    {
        musicViewer?.InitMusicSetting();
    }

    public void OpenSettingWindow() => gameObject.SetActive(true);
    public void CloseSettingWindow() => gameObject.SetActive(false);
}
