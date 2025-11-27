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
    [HideInInspector] public bool ShouldDisplay;

    private int Frame;
    ResourceManager Manager;
    void Start()
    {
        Sprite.sprite = Resource.Sprite;
        Label.text = Resource.Label;
        Details.GetChild(1).GetComponent<TMP_Text>().text = Resource.Description;
        Manager = ResourceManager.Instance;
    }
    void Update()
    {
        Frame++;
        if (Frame % 60 == 0)
            UpdateUI();
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
    private void UpdateUI()
    {
        Amount.text = Manager.ResourceAmount[Resource].ToString();
        GrowthRateText.text = Manager.ResourceGrowthRate[Resource].ToString() + " /s";
    }
}
