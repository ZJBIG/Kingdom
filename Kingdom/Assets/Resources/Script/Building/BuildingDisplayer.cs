using System;
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

    [HideInInspector] public BigNumber BuildingAmount;
    [HideInInspector] public Building Building;
    [HideInInspector] public bool AutoBuild;
    [HideInInspector] public double Efficiency = 1;

    void Start()
    {
        Label.text = Building.Label;
        Description.text = Building.Description;
        foreach (var res in Building.ResourceRequirement)
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
        StartCoroutine(UpdateResourceRate());
    }
    public void DisplayDetails() => Details.gameObject.SetActive(!Details.gameObject.activeSelf);
    public void SwitchAutoBuild() => AutoBuild = !AutoBuild;
    public ResourceDisplayer FindResourceDisplayer(Resource r) => ResourceManager.Instance.FindResourceDisplayer(r);
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
    IEnumerator UpdateUI()
    {
        while (true)
        {
            Amount.text = BuildingAmount.ToString();
            Construction.gameObject.SetActive(!AutoBuild);
            AutoBuildSpirit.sprite = AutoBuild ? Enable : Disable;
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
    IEnumerator UpdateResourceRate()
    {
        while (true)
        {
            RefreshResourceRate(GetEfficiency(), BuildingAmount);
            yield return new WaitForSecondsRealtime(10f);
        }
    }
    public void TryConstruct(string amount)
    {
        BigNumber SpaceReq = amount * (BigNumber)Building.SpaceOccupy;
        BigNumber ProductivityReq = amount * (BigNumber)Building.ProductivityRequirement;
        foreach (var pair in Building.ResourceRequirement)
        {
            var r = pair.first;
            var req = pair.second;
            if (FindResourceDisplayer(r).ResourceAmount < (BigNumber)req * amount
                || GameManager.Instance.KingdomSpace < SpaceReq
                || GameManager.Instance.Productivity < ProductivityReq)
                return;
        }
        foreach (var pair in Building.ResourceRequirement)
        {
            var r = pair.first;
            BigNumber num = pair.second;
            FindResourceDisplayer(r).ResourceAmount -= num * Building.DeconstructReturnPercentage;
        }
        GameManager.Instance.KingdomSpace -= SpaceReq;
        GameManager.Instance.Productivity -= ProductivityReq;
        BigNumber NewBuildingAmount = BuildingAmount + amount;
        RefreshResourceRate(GetEfficiency(), NewBuildingAmount);
    }
    public void TryDeconstruct(string amount)
    {
        if (BuildingAmount <= 0.01)
            return;
        BigNumber SpaceReq = (BigNumber)amount * Building.SpaceOccupy;
        BigNumber ProductivityReq = (BigNumber)amount * Building.ProductivityRequirement;
        if (BuildingAmount <= amount)
            amount = BuildingAmount.ToString();
        foreach (var pair in Building.ResourceRequirement)
        {
            var r = pair.first;
            BigNumber num = pair.second;
            FindResourceDisplayer(r).ResourceAmount += num * Building.DeconstructReturnPercentage;
        }
        GameManager.Instance.KingdomSpace += SpaceReq;
        GameManager.Instance.Productivity += ProductivityReq;
        BigNumber NewBuildingAmount = BuildingAmount - amount;
        RefreshResourceRate(GetEfficiency(), NewBuildingAmount);
    }
    private void RefreshResourceRate(double NewEfficiency, BigNumber NewBuildingAmount)
    {
        foreach (var pair in Building.ResourceGenerateRate)
        {
            var r = pair.first;
            BigNumber num = pair.second;

            FindResourceDisplayer(r).GenerateRate -= BuildingAmount * Efficiency * num;
            FindResourceDisplayer(r).GenerateRate += NewBuildingAmount * NewEfficiency * num;
        }
        foreach (var pair in Building.ResourceConsumeRate)
        {
            var r = pair.first;
            BigNumber num = pair.second;

            FindResourceDisplayer(r).GenerateRate -= BuildingAmount * Efficiency * num;
            FindResourceDisplayer(r).GenerateRate += NewBuildingAmount * NewEfficiency * num;
        }
        GameManager.Instance.FoodGrowthRate += BuildingAmount * Efficiency * Building.FoodConsumeRate;
        GameManager.Instance.FoodGrowthRate -= NewBuildingAmount * NewEfficiency * Building.FoodConsumeRate;
        Efficiency = NewEfficiency;
        BuildingAmount = NewBuildingAmount;
    }
    private double GetEfficiency()
    {
        double result = 1;
        Building.ResourceConsumeRate.ForEach(pair =>
        {
            var r = pair.first;
            var displayer = ResourceManager.Instance.FindResourceDisplayer(r);
            if (displayer.ResourceAmount == 0)
                result *= Math.Clamp((displayer.ConsumeRate / displayer.GenerateRate).ToDouble(), 0, 1);
        });
        return result;
    }


    [Serializable]
    public class BuildingDisplayerData
    {
        public string BuildingName;
        public bool AutoBuild;
        public BigNumber BuildingAmount;
        public double Efficiency;
    }
}
