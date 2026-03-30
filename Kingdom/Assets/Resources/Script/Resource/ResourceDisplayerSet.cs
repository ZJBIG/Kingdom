using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static ResourceDisplayer;

public class ResourceDisplayerSet : MonoBehaviour
{
    public Transform Content;
    public Transform Hide;
    [SerializeField] private Transform InfoBase;
    public bool Closed = true;

    public Dictionary<Resource, ResourceDisplayer> Displayers = new Dictionary<Resource, ResourceDisplayer>();

    private void Start()
    {
        StartCoroutine(UpdateHeight());
    }

    IEnumerator UpdateHeight()
    {
        while (true)
        {
            Image ImageComp = GetComponent<Image>();
            float Height = 50;
            if (Closed)
                ImageComp.rectTransform.sizeDelta = new Vector2(ImageComp.rectTransform.rect.width, Height);
            else
            {
                foreach (var (_, displayer) in Displayers)
                    Height += displayer.GetComponent<Image>().rectTransform.rect.height;
                ImageComp.rectTransform.sizeDelta = new Vector2(ImageComp.rectTransform.rect.width, Height);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
    public void OpenUpResourceSet()
    {
        Closed = !Closed;
        foreach (var (_, displayer) in Displayers)
            displayer.transform.SetParent(Closed ? Hide : Content);
    }



    [Serializable]
    public class ResourceDisplayerSetData
    {
        public string SetName;
        public bool Closed;
        public List<ResourceDisplayerData> ResourceDisplayerData;
    }
}
