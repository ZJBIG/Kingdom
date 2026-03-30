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
    [HideInInspector] public BigNumber Efficiency;
    [HideInInspector] public bool AutoBuild;


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
            Refresh(GetEfficiency(), BuildingAmount);
            yield return new WaitForSecondsRealtime(10f);
        }
    }
    public void TryConstruct(string amount)
    {
        BigNumber SpaceReq = amount * (BigNumber)Building.SpaceOccupy;
        BigNumber ProductivityReq = amount * (BigNumber)Building.ProductivityRequirement;
        foreach (var (r, req) in Building.ResourceRequirement)
        {
            if (FindResourceDisplayer(r).ResourceAmount < (BigNumber)req * amount
                || GameManager.Instance.KingdomSpace < SpaceReq
                || GameManager.Instance.Productivity < ProductivityReq)
                return;
        }
        foreach (var (r, num) in Building.ResourceRequirement)
            FindResourceDisplayer(r).ResourceAmount -= (BigNumber)num * Building.DeconstructReturnPercentage;

        GameManager.Instance.KingdomSpace -= SpaceReq;
        GameManager.Instance.Productivity -= ProductivityReq;
        BigNumber NewBuildingAmount = BuildingAmount + amount;
        Refresh(GetEfficiency(), NewBuildingAmount);
    }
    public void TryDeconstruct(string amount)
    {
        if (BuildingAmount <= 0.01)
            return;
        BigNumber SpaceReq = (BigNumber)amount * Building.SpaceOccupy;
        BigNumber ProductivityReq = (BigNumber)amount * Building.ProductivityRequirement;
        if (BuildingAmount <= amount)
            amount = BuildingAmount.ToString();
        foreach (var (r, num) in Building.ResourceRequirement)
            FindResourceDisplayer(r).ResourceAmount += (BigNumber)num * Building.DeconstructReturnPercentage;

        GameManager.Instance.KingdomSpace += SpaceReq;
        GameManager.Instance.Productivity += ProductivityReq;
        BigNumber NewBuildingAmount = BuildingAmount - amount;
        Refresh(GetEfficiency(), NewBuildingAmount);
    }
    public void Clear() => TryDeconstruct(BuildingAmount.ToString());
    private void Refresh(BigNumber NewEfficiency, BigNumber NewBuildingAmount)
    {
        foreach (var (r, num) in Building.ResourceGenerateRate)
        {
            FindResourceDisplayer(r).GenerateRate -= BuildingAmount * Efficiency * num;
            FindResourceDisplayer(r).GenerateRate += NewBuildingAmount * NewEfficiency * num;
        }
        foreach (var (r, num) in Building.ResourceConsumeRate)
        {
            FindResourceDisplayer(r).ConsumeRate -= BuildingAmount * Efficiency * num;
            FindResourceDisplayer(r).ConsumeRate += NewBuildingAmount * NewEfficiency * num;
        }
        //if ((BigNumber)Building.FoodConsumeRate >= 0)
        //{
        //    GameManager.Instance.FoodConsumeRate -= BuildingAmount * Efficiency * Building.FoodConsumeRate;
        //    GameManager.Instance.FoodConsumeRate += NewBuildingAmount * NewEfficiency * Building.FoodConsumeRate;
        //}
        //else
        //{
        //    GameManager.Instance.FoodGenerateRate -= BuildingAmount * Efficiency * Building.FoodConsumeRate;
        //    GameManager.Instance.FoodGenerateRate += NewBuildingAmount * NewEfficiency * Building.FoodConsumeRate;
        //}
        Efficiency = NewEfficiency;
        BuildingAmount = NewBuildingAmount;
    }
    private BigNumber GetEfficiency()
    {
        double result = 1;
        foreach (var (r, _) in Building.ResourceConsumeRate)
        {
            var displayer = ResourceManager.Instance.FindResourceDisplayer(r);
            if (displayer.ResourceAmount == 0)
                result *= Math.Clamp((displayer.ConsumeRate / displayer.GenerateRate).ToDouble(), 0, 1);
        }
        return result * BuildingManager.Instance.GlobalEfficiencyFactor;
    }


    [Serializable]
    public class BuildingDisplayerData
    {
        public string BuildingName;
        public bool AutoBuild;
        public BigNumber BuildingAmount;
        public BigNumber Efficiency;
    }
}
