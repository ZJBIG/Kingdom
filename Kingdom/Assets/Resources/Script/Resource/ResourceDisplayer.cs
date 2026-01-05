using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceDisplayer : MonoBehaviour
{
    [SerializeField] private Image Sprite;
    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Amount;
    [SerializeField] private TMP_Text GrowthRateText;
    [SerializeField] private Transform Details;
    [HideInInspector] public Resource Resource;
    [HideInInspector] public BigNumber ResourceGrowthRate;
    [HideInInspector] public BigNumber ResourceAmount;
    [HideInInspector] public bool ShouldDisplay;

    void Start()
    {
        Sprite.sprite = Resource.Sprite;
        Sprite.color = Resource.Color;
        Label.text = Resource.Label;
        Details.GetChild(1).GetComponent<TMP_Text>().text = Resource.Description;

        StartCoroutine(ResourceUpdate());
        StartCoroutine(UpdateUI());
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
            ImageComp.rectTransform.sizeDelta = new Vector2(ImageComp.rectTransform.rect.width, 230);
        }
    }
    private IEnumerator UpdateUI()
    {
        while (true)
        {
            Amount.text = ResourceAmount.ToString();
            GrowthRateText.text = ResourceGrowthRate.ToString() + " /s";
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
    private IEnumerator ResourceUpdate()
    {
        while (true)
        {
            ResourceAmount += ResourceGrowthRate / 10;
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
}
