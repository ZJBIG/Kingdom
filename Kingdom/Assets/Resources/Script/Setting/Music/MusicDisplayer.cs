using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

#pragma warning disable CS0649
public class MusicDisplayer : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("Type")] private TMP_Text typeText;
    [SerializeField, FormerlySerializedAs("Label")] private TMP_Text labelText;

    private AudioClip clip;

    public void Bind(string newTypeName, AudioClip newClip)
    {
        clip = newClip;
        if (typeText != null)
            typeText.text = newTypeName;
        if (labelText != null)
            labelText.text = clip == null ? string.Empty : clip.name;
    }

    public void Play() => MusicManager.Instance.Play(clip);
}
#pragma warning restore CS0649
