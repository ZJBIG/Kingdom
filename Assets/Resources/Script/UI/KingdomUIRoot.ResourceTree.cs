using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private RectTransform resourceTreeSurface;
    private Button resourceTreeModeButton;
    private TMP_Text resourceTreeModeLabel;
    private GameObject resourceTreeRowPrefab;
    private GameObject resourceTreeLinkPrefab;

    private void BindProductionEntry()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceTree");
        resourceTreeRowPrefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceTreeRow");
        resourceTreeLinkPrefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceTreeLink");
        if (prefab == null || resourceTreeRowPrefab == null || resourceTreeLinkPrefab == null)
        {
            Debug.LogError("Missing authored resource production tree prefabs.");
            return;
        }
        resourceTreeSurface = Instantiate(prefab, detailScrollContent, false).GetComponent<RectTransform>();
        resourceTreeSurface.name = "ResourceProductionTree";
        resourceTreeModeButton = resourceTreeSurface.Find("Mode").GetComponent<Button>();
        resourceTreeModeLabel = resourceTreeModeButton.transform.Find("Label").GetComponent<TMP_Text>();
        resourceTreeModeButton.onClick.AddListener(OpenSelectedProductionTree);
        resourceTreeModeButton.gameObject.AddComponent<UIResourceTreeDetailDrag>().Bind(requirementGesture);
        resourceTreeSurface.gameObject.SetActive(false);
    }

    private void RefreshProductionEntry()
    {
        if (resourceTreeSurface == null) return;
        resourceTreeSurface.gameObject.SetActive(selectedResource != null || selectedBuilding != null);
        resourceTreeModeLabel.text = selectedBuilding != null ? "查看建筑树" : "查看生产树";
    }

    private void AddResourceTreeLink(RectTransform parent, Vector2 start, Vector2 end)
    {
        RectTransform link = Instantiate(resourceTreeLinkPrefab, parent, false).GetComponent<RectTransform>();
        link.name = "Connection";
        Vector2 middle = (start + end) * .5f;
        link.anchoredPosition = new Vector2(middle.x, -middle.y);
        link.sizeDelta = new Vector2(Mathf.Max(2f, Mathf.Abs(end.x - start.x)), Mathf.Max(2f, Mathf.Abs(end.y - start.y)));
    }

}
