using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceViewer : MonoBehaviour
{
    [SerializeField] private RectTransform Content;
    void Start()
    {
        StartCoroutine(UpdateHeight());
    }
    IEnumerator UpdateHeight()
    {
        while (true)
        {
            float Height = 0;
            for (int i = 0; i < Content.childCount; i++)
            {
                Image ImageComp = Content.GetChild(i).GetComponent<Image>();
                Height += ImageComp.rectTransform.rect.height;
            }
            Height += 500;
            Content.sizeDelta = new Vector2(Content.rect.width, Height);
            yield return new WaitForSeconds(1f);
        }
    }
}
