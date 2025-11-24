using System;
using UnityEngine;
using UnityEngine.UI;

public class ResourceDisplayer : MonoBehaviour
{
    [SerializeField] private Image Sprite;
    [SerializeField] private Text Label;
    [SerializeField] private Text Amount;
    [SerializeField] private Transform Details;
    [HideInInspector] public Resource Resource;
    [HideInInspector] public BigNumber GrowthRate;
    private BigNumber PreAmout;
    private BigNumber CurAmout;
    ResourceManager Manager => ResourceManager.Instance;
    void Start()
    {
        Sprite.sprite = Resource.Sprite;
        Label.text = Resource.Label;
    }
    void Update()
    {
        PreAmout = CurAmout;
        Manager.ResourceAmount.TryGetValue(Resource, out CurAmout);
        GrowthRate = (CurAmout - PreAmout) / Time.deltaTime;
        Amount.text = CurAmout.ToString();
    }
    public void DisplayDetails()
    {
        Image ImageComp = GetComponent<Image>();
        if (Details.gameObject.activeSelf)
        {
            Details.gameObject.SetActive(false);
            ImageComp.rectTransform.sizeDelta = new Vector2(ImageComp.rectTransform.rect.width, 100);
        }
        else
        {
            Details.gameObject.SetActive(true);
            ImageComp.rectTransform.sizeDelta = new Vector2(ImageComp.rectTransform.rect.width, 200);
        }
    }
}
