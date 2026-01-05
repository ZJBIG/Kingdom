using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingDisplayer : MonoBehaviour
{
    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Description;
    [SerializeField] private TMP_Text Amount;
    [SerializeField] private Image AutoBuildSpirit;
    [SerializeField] private Transform Details;
    [SerializeField] private RectTransform Construction;
    [SerializeField] private RectTransform ResourceList;
    [SerializeField] private Sprite Enable, Disable;
    [SerializeField] private GameObject BuildResourceReqPrefab;
    [HideInInspector] public bool ShouldDisplay;

    [HideInInspector] public BigNumber BuildingAmount = 0;
    public Building Building;
    public bool AutoBuild;

    void Start()
    {
        Label.text = Building.Label;
        Description.text = Building.Description;
        foreach (var res in Building.BuildCost)
        {
            var go = Instantiate(BuildResourceReqPrefab);
            go.transform.SetParent(ResourceList);
            go.transform.GetChild(0).GetComponent<Image>().sprite = res.first.Sprite;
            go.transform.GetChild(0).GetComponent<Image>().color = res.first.Color;
            go.transform.GetChild(1).GetComponent<TMP_Text>().text = BigNumber.ToString(res.second);
        }
        AutoBuildSpirit.sprite = Disable;
        StartCoroutine(UpdateHeight());
        StartCoroutine(UpdateUI());
    }
    public void DisplayDetails() => Details.gameObject.SetActive(!Details.gameObject.activeSelf);
    public void SwitchAutoBuild()
    {
        AutoBuild = !AutoBuild;
        AutoBuildSpirit.sprite = AutoBuild ? Enable : Disable;
    }
    IEnumerator UpdateHeight()
    {
        while (true)
        {
            Image ImageComp = GetComponent<Image>();
            Image ResourceListImageComp = ResourceList.GetComponent<Image>();
            if (Details.gameObject.activeSelf)
            {
                int resReqAmonut = (ResourceList.childCount + 1) / 2;
                if (AutoBuild)
                    ImageComp.rectTransform.sizeDelta = new Vector2(ImageComp.rectTransform.rect.width, 150 + resReqAmonut * 50);
                else
                    ImageComp.rectTransform.sizeDelta = new Vector2(ImageComp.rectTransform.rect.width, 200 + resReqAmonut * 50);
                ResourceListImageComp.rectTransform.sizeDelta = new Vector2(ResourceListImageComp.rectTransform.rect.width, resReqAmonut * 50);
            }
            else
                ImageComp.rectTransform.sizeDelta = new Vector2(ImageComp.rectTransform.rect.width, 50);
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
    private IEnumerator UpdateUI()
    {
        while (true)
        {
            Amount.text = BuildingAmount.ToString();
            Construction.gameObject.SetActive(!AutoBuild);
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
    public void TryConstruct(string amount)
    {
        foreach (var pair in Building.BuildCost)
            if (ResourceManager.Instance.Displayers[pair.first].ResourceAmount < pair.second * (BigNumber)amount)
                return;
        foreach (var pair in Building.BuildCost)
            ResourceManager.Instance.Displayers[pair.first].ResourceAmount -= pair.second * (BigNumber)amount;
        BuildingAmount += amount;
        BuildingManager.Instance.UpdateResourceGrowthRate();
    }
    public void TryDeconstruct(string amount)
    {
        if (BuildingAmount <= amount)
            amount = BuildingAmount;
        foreach (var pair in Building.BuildCost)
            ResourceManager.Instance.Displayers[pair.first].ResourceAmount += pair.second * (BigNumber)Building.DeconstructReturnPercentage;
        BuildingAmount -= amount;
        BuildingManager.Instance.UpdateResourceGrowthRate();
    }
}
