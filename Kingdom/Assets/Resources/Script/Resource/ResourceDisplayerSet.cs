using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceDisplayerSet : MonoBehaviour
{
    [SerializeField] private Transform Content;
    [SerializeField] private GameObject ResourceDisplayerPrefab;
    private List<ResourceDisplayer> Displayers = new List<ResourceDisplayer>();
    public List<Resource> Resources;
    void Start()
    {
        foreach (Resource r in Resources)
        {
            ResourceDisplayer Displayer = Instantiate(ResourceDisplayerPrefab).GetComponent<ResourceDisplayer>();
            Displayer.Resource = r;
            Displayer.transform.SetParent(Content.transform);
            Displayers.Add(Displayer);
        }
        StartCoroutine(UpdateHeight());
    }
    IEnumerator UpdateHeight()
    {
        while (true)
        {
            float Height = 50;
            foreach (var displayer in Displayers)
            {
                Image ImageComp = displayer.GetComponent<Image>();
                Height += ImageComp.rectTransform.rect.height;
            }
            if (Content.gameObject.activeSelf)
                GetComponent<Image>().rectTransform.sizeDelta = new Vector2(GetComponent<Image>().rectTransform.rect.width, Height);
            else
                GetComponent<Image>().rectTransform.sizeDelta = new Vector2(GetComponent<Image>().rectTransform.rect.width, 50);
            yield return new WaitForSeconds(0.1f);
        }
    }
    public void OpenUpResourceSet()
    {
        if (Content.gameObject.activeSelf)
            Content.gameObject.SetActive(false);
        else
            Content.gameObject.SetActive(true);
    }
}
