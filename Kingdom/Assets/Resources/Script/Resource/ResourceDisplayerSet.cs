using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static ResourceDisplayer;

public class ResourceDisplayerSet : MonoBehaviour
{
    public Transform Content;
    [SerializeField] private Transform InfoBase;

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



    [Serializable]
    public class ResourceDisplayerSetData
    {
        public string SetName;
        public List<ResourceDisplayerData> ResourceDisplayerData;
    }
}
