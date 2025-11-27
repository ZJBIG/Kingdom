using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ResourceDisplayerSet : MonoBehaviour
{
    [SerializeField] private Transform Content;
    [SerializeField] private Transform InfoBase;
    [SerializeField] private GameObject ResourceDisplayerPrefab;
    private List<ResourceDisplayer> Displayers = new List<ResourceDisplayer>();

    private List<Resource> resources = new();
    public List<Resource> Resources
    {
        get => resources;
        set
        {
            resources = value;
            Refresh();
        }
    }
    void Awake()
    {
        Refresh();
        StartCoroutine(UpdateHeight());
    }
    IEnumerator UpdateHeight()
    {
        while (true)
        {
            float Height = 0;
            if (Content.gameObject.activeSelf)
                foreach (var displayer in Displayers)
                {
                    Image ImageComp = displayer.GetComponent<Image>();
                    Height += ImageComp.rectTransform.rect.height;
                }
            Height += 50;
            if (resources.Count == 0)
            {
                GetComponent<Image>().rectTransform.sizeDelta = new Vector2(GetComponent<Image>().rectTransform.rect.width, 0);
                InfoBase.gameObject.SetActive(false);
            }
            else
            {
                GetComponent<Image>().rectTransform.sizeDelta = new Vector2(GetComponent<Image>().rectTransform.rect.width, Height);
                InfoBase.gameObject.SetActive(true);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
    public void OpenUpResourceSet() => Content.gameObject.SetActive(!Content.gameObject.activeSelf);
    private void Refresh()
    {
        foreach (var displayer in Displayers)
            Destroy(displayer.gameObject);
        Displayers.Clear();
        foreach (Resource r in Resources)
        {
            ResourceDisplayer Displayer = Instantiate(ResourceDisplayerPrefab).GetComponent<ResourceDisplayer>();
            Displayer.Resource = r;
            Displayer.transform.SetParent(Content.transform);
            Displayers.Add(Displayer);
        }
    }
    public void AddResource(Resource resource)
    {
        if (resources.Contains(resource) || ResourceManager.Instance.ResourceAmount.ContainsKey(resource) || ResourceManager.Instance.ResourceGrowthRate.ContainsKey(resource))
            throw new System.Exception($"{resource.name} has already added");
        Resources.Add(resource);
        ResourceManager.Instance.ResourceAmount.Add(resource, 0);
        ResourceManager.Instance.ResourceGrowthRate.Add(resource, 0);
        Refresh();
    }
}
