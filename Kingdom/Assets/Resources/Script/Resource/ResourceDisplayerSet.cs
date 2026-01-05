using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceDisplayerSet : MonoBehaviour
{
    public Transform Content;
    [SerializeField] private Transform InfoBase;
    [SerializeField] private GameObject ResourceDisplayerPrefab;
    //private List<ResourceDisplayer> Displayers = new List<ResourceDisplayer>();

    public Dictionary<Resource, ResourceDisplayer> Displayers = new Dictionary<Resource, ResourceDisplayer>();

    void Awake()
    {
        StartCoroutine(UpdateHeight());
    }
    IEnumerator UpdateHeight()
    {
        while (true)
        {
            float Height = 0;
            if (Content.gameObject.activeSelf)
                foreach (var (_, displayer) in Displayers)
                {
                    Image ImageComp = displayer.GetComponent<Image>();
                    Height += ImageComp.rectTransform.rect.height;
                }
            Height += 50;
            if (Displayers.Count == 0)
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
    public void AddResource(Resource resource)
    {
        if (Displayers.ContainsKey(resource))
            throw new System.Exception($"{resource.name} has already added");

        ResourceDisplayer Displayer = Instantiate(ResourceDisplayerPrefab).GetComponent<ResourceDisplayer>();
        Displayer.Resource = resource;
        Displayer.ResourceAmount = 0;
        Displayer.ResourceGrowthRate = 0;
        Displayer.transform.SetParent(Content.transform);
        Displayers.Add(resource, Displayer);

        ResourceManager.Instance.Displayers.Add(resource,Displayer);
    }
}
