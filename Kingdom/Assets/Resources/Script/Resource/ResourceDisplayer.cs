using System.Collections.Generic;
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

        Sprite.sprite = Resource.Sprite != null
            ? Resource.Sprite
            : ResourceIconFallback.Get(Resource);
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
        GetComponentInParent<ResourceDisplayerSet>()?.RefreshLayout();
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

internal static class ResourceIconFallback
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(Resource resource)
    {
        if (resource == null)
            return null;
        if (cache.TryGetValue(resource.Id, out Sprite existing))
            return existing;

        const int size = 24;
        Color32[] pixels = new Color32[size * size];
        int seed = Mathf.Abs((resource.Id ?? resource.name ?? string.Empty).GetHashCode());
        int stripe = 3 + seed % 4;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool border = x == 1 || y == 1 || x == size - 2 || y == size - 2;
                bool mark = (x + y + seed) % stripe == 0 &&
                    x > 3 && x < size - 4 && y > 3 && y < size - 4;
                pixels[y * size + x] = border || mark
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ResourceIconFallback_" + resource.Id,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        cache[resource.Id] = sprite;
        return sprite;
    }
}
