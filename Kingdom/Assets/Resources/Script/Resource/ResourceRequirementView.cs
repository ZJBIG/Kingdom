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

        ResizeTextBoxes();
    }

    private void ResizeTextBoxes()
    {
        RectTransform row = transform as RectTransform;
        if (row == null)
            return;

        float rowHeight = 50f;
        if (Label != null)
            rowHeight = Mathf.Max(rowHeight, Label.GetPreferredValues(
                Label.text, Label.rectTransform.rect.width, 0f).y + 8f);
        if (Amount != null)
            rowHeight = Mathf.Max(rowHeight, Amount.GetPreferredValues(
                Amount.text, Amount.rectTransform.rect.width, 0f).y + 8f);

        row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
        if (Label != null)
            Label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
        if (Amount != null)
            Amount.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
        LayoutRebuilder.MarkLayoutForRebuild(row);
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
