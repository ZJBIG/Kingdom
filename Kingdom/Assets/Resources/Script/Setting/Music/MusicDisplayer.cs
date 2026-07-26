using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

#pragma warning disable CS0649
public class MusicDisplayer : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("Type")] private TMP_Text typeText;
    [SerializeField, FormerlySerializedAs("Label")] private TMP_Text labelText;

    private AudioClip clip;
    private string resourcePath;

    public void Bind(string newTypeName, AudioClip newClip)
    {
        clip = newClip;
        resourcePath = string.Empty;
        if (typeText != null)
            typeText.text = newTypeName;
        if (labelText != null)
            labelText.text = clip == null ? string.Empty : clip.name;
    }

    public void Bind(string newTypeName, string newResourcePath, string newLabel)
    {
        clip = null;
        resourcePath = newResourcePath;
        if (typeText != null)
            typeText.text = newTypeName;
        if (labelText != null)
            labelText.text = newLabel ?? string.Empty;
    }

    public void Play()
    {
        if (!string.IsNullOrEmpty(resourcePath))
            MusicManager.Instance.QueuePlay(resourcePath);
        else
            MusicManager.Instance.Play(clip);
    }
}
#pragma warning restore CS0649
