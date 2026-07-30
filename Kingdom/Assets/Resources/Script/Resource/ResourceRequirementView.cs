using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResourceRequirementView : MonoBehaviour
{
    private static readonly Color RefundColor = new Color32(35, 166, 150, 255);
    [SerializeField] private Image Icon;
    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Amount;
    private Color normalLabelColor;
    private Color normalAmountColor;
    private bool colorsCaptured;

    public void Bind(Resource resource, ExpantaNum amount)
    {
        Bind(resource, amount, false);
    }

    public void BindUpgradeDelta(Resource resource, ExpantaNum signedAmount)
    {
        bool isRefund = signedAmount < ExpantaNum.Zero;
        Bind(resource, isRefund ? -signedAmount : signedAmount, isRefund);
    }

    private void Bind(Resource resource, ExpantaNum amount, bool isRefund)
    {
        if (resource == null)
            return;

        CaptureColors();
        if (Icon != null)
        {
            Icon.sprite = resource.Sprite != null
                ? resource.Sprite
                : ResourceIconFallback.Get(resource);
            Icon.color = resource.Color;
        }
        
        if (Label != null)
        {
            Label.text = isRefund ? resource.Label + "（返还）" : resource.Label;
            Label.color = isRefund ? RefundColor : normalLabelColor;
        }

        if (Amount != null)
        {
            Amount.text = Label != null
                ? amount.ToGameString()
                : $"{resource.Label}{(isRefund ? "（返还）" : string.Empty)}  {amount.ToGameString()}";
            Amount.color = isRefund ? RefundColor : normalAmountColor;
        }
    }

    private void CaptureColors()
    {
        if (colorsCaptured)
            return;
        normalLabelColor = Label != null ? Label.color : Color.white;
        normalAmountColor = Amount != null ? Amount.color : Color.white;
        colorsCaptured = true;
    }
}
