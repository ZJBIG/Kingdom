using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceDisplayerSet : MonoBehaviour
{
    public Transform Content;
    public Transform Hide;
    [SerializeField] private Transform InfoBase;
    public bool Closed = true;

    private readonly Dictionary<Resource, ResourceDisplayer> displayers =
        new Dictionary<Resource, ResourceDisplayer>();

    public Transform ContentTransform => Content;
    public IReadOnlyDictionary<Resource, ResourceDisplayer> Displayers => displayers;

    private void Awake()
    {
        ConfigureContentLayout();
    }

    public void OpenUpResourceSet()
    {
        Closed = !Closed;
        RefreshLayout();
    }

    public void RefreshLayout()
    {
        Transform parent = Closed ? Hide : Content;
        if (parent == null)
            return;

        RectTransform contentRect = Content as RectTransform;
        float contentHeight = 0f;

        if (contentRect != null)
        {
            // The resource prefab already owns its internal layout. Stack the
            // cards here with explicit positions so no layout group can move
            // their anchored children or stretch their height.
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
            VerticalLayoutGroup layout = contentRect.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.enabled = false;
        }

        foreach (ResourceDisplayer displayer in displayers.Values)
        {
            bool visible = displayer != null && displayer.gameObject.activeSelf;
            RectTransform row = displayer.transform as RectTransform;
            displayer.transform.SetParent(visible ? parent : Hide, false);

            if (!Closed && visible && row != null)
            {
                float rowHeight = Mathf.Max(100f, row.sizeDelta.y);
                row.anchorMin = new Vector2(0.5f, 1f);
                row.anchorMax = new Vector2(0.5f, 1f);
                row.pivot = new Vector2(0.5f, 0.5f);
                row.anchoredPosition = new Vector2(0f, -contentHeight - rowHeight * 0.5f);
                contentHeight += rowHeight;
            }
        }

        if (contentRect != null)
        {
            contentRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                contentHeight);

            Image image = GetComponent<Image>();
            if (image != null)
            {
                image.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    50f + contentHeight);
            }

            Canvas.ForceUpdateCanvases();
        }
    }

    public void AddDisplayer(Resource resource, ResourceDisplayer displayer)
    {
        if (resource == null || displayer == null || displayers.ContainsKey(resource))
            return;

        displayers.Add(resource, displayer);
    }

    public bool SetVisible(Resource resource, bool visible)
    {
        if (resource == null || !displayers.TryGetValue(resource, out ResourceDisplayer displayer) ||
            displayer == null)
            return false;

        if (displayer.gameObject.activeSelf == visible)
            return false;

        displayer.gameObject.SetActive(visible);
        return true;
    }

    private void ConfigureContentLayout()
    {
        RectTransform contentRect = Content as RectTransform;
        if (contentRect == null)
            return;

        VerticalLayoutGroup layout = contentRect.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;
    }
}
