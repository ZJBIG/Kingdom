using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResourceRequirementView : MonoBehaviour
{
    [SerializeField] private Image Icon;
    [SerializeField] private TMP_Text Amount;

    public void Bind(Resource resource, ExpantaNum amount)
    {
        if (resource == null)
            return;

        if (Icon != null)
        {
            Icon.sprite = resource.Sprite;
            Icon.color = resource.Color;
        }

        if (Amount != null)
            Amount.text = amount.ToGameString();
    }
}
