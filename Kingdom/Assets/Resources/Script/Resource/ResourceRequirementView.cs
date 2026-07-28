using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResourceRequirementView : MonoBehaviour
{
    [SerializeField] private Image Icon;
    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Amount;

    public void Bind(Resource resource, ExpantaNum amount)
    {
        if (resource == null)
            return;

        if (Icon != null)
        {
            Icon.sprite = resource.Sprite != null
                ? resource.Sprite
                : ResourceIconFallback.Get(resource);
            Icon.color = resource.Color;
        }
        
        if (Label != null)
            Label.text = resource.Label;

        if (Amount != null)
            Amount.text = Label != null
                ? amount.ToGameString()
                : $"{resource.Label}  {amount.ToGameString()}";
    }
}
