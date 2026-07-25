using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceDisplayer : MonoBehaviour
{
    [SerializeField] private Image Sprite;
    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Amount;
    [SerializeField] private TMP_Text GrowthRateText;
    [SerializeField] private Transform Details;
    [SerializeField] private TMP_Text Description;

    private ResourceState state;

    public Resource Resource => state?.Definition;

    public void Bind(ResourceState newState)
    {
        state = newState ?? throw new System.ArgumentNullException(nameof(newState));

        Sprite.sprite = Resource.Sprite;
        Sprite.color = Resource.Color;
        Label.text = Resource.Label;
        if (Description != null)
            Description.text = Resource.Description;
        else if (Details != null && Details.childCount > 1)
        {
            TMP_Text detailsText = Details.GetChild(1).GetComponent<TMP_Text>();
            if (detailsText != null)
                detailsText.text = Resource.Description;
        }

        ApplyCardHeight();

        Refresh();
    }

    public void DisplayDetails()
    {
        if (Details == null)
            return;
        Details.gameObject.SetActive(!Details.gameObject.activeSelf);
        ApplyCardHeight();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    private void ApplyCardHeight()
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        rectTransform.sizeDelta = new Vector2(
            rectTransform.sizeDelta.x,
            Details != null && Details.gameObject.activeSelf ? 230f : 100f);
    }

    public bool Refresh()
    {
        if (state == null)
            return false;

        Amount.text = state.Amount.ToGameString();
        ExpantaNum netRate = state.ProductionRate - state.ConsumptionRate;
        GrowthRateText.text = (netRate >= ExpantaNum.Zero
            ? "+" + netRate.ToGameString()
            : netRate.ToGameString()) + " /s";
        return true;
    }
}
