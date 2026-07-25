using System.Collections;
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

    private void Start()
    {
        StartCoroutine(UpdateHeight());
    }

    private IEnumerator UpdateHeight()
    {
        while (true)
        {
            Image image = GetComponent<Image>();
            if (image != null)
            {
                float height = 50f;
                if (!Closed)
                {
                    foreach (ResourceDisplayer displayer in displayers.Values)
                        height += displayer.GetComponent<Image>().rectTransform.rect.height;
                }

                image.rectTransform.sizeDelta =
                    new Vector2(image.rectTransform.rect.width, height);
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    public void OpenUpResourceSet()
    {
        Closed = !Closed;
        foreach (ResourceDisplayer displayer in displayers.Values)
        {
            Transform parent = Closed ? Hide : Content;
            if (parent != null)
                displayer.transform.SetParent(parent, false);
            displayer.gameObject.SetActive(true);
        }
    }

    public void RefreshLayout()
    {
        Transform parent = Closed ? Hide : Content;
        if (parent == null)
            return;

        foreach (ResourceDisplayer displayer in displayers.Values)
            displayer.transform.SetParent(parent, false);
    }

    public void AddDisplayer(Resource resource, ResourceDisplayer displayer)
    {
        if (resource == null || displayer == null || displayers.ContainsKey(resource))
            return;

        displayers.Add(resource, displayer);
    }
}
