using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private void BindArtworkIcon(Image icon, Sprite sprite, string heading, bool graphDrag = false)
    {
        if (icon == null) return;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        Transform frame = icon.transform.Find("ArtworkFrame");
        if (frame != null) frame.gameObject.SetActive(sprite != null);
        Button preview = icon.GetComponent<Button>();
        if (preview == null) return;
        preview.interactable = sprite != null;
        icon.raycastTarget = sprite != null;
        preview.onClick.RemoveAllListeners();
        preview.onClick.AddListener(() =>
        {
            if (storyIllustrationPreview == null)
                storyIllustrationPreview = GetComponentInChildren<StoryIllustrationPreview>(true);
            if (storyIllustrationPreview != null) storyIllustrationPreview.Show(sprite, heading);
        });
        if (graphDrag) icon.gameObject.AddComponent<UIResearchGraphDragForwarder>();
    }
}
