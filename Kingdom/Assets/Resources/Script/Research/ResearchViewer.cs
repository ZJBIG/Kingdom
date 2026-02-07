using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchViewer : MonoBehaviour
{
    public RectTransform Content;
    public RectTransform LineContainer;
    [SerializeField] private GameObject ResearchResourceReqPrefab;
    [SerializeField] private TMP_Text BaseInfo;
    [SerializeField] private RectTransform ResourceList;
    [SerializeField] private TMP_Text DoInvestButton;
    [SerializeField] private Slider ProgressPercentage;
    [HideInInspector] public Research CurSelect;
    [HideInInspector] public Research PreSelect;

    ResearchDisplayer SelectDisplayer => ResearchManager.Instance.Displayers[CurSelect];
    string ButtonText
    {
        get
        {
            return SelectDisplayer.CurState switch
            {
                ResearchDisplayer.State.Finished => "当前研究已完成",
                ResearchDisplayer.State.InQueue or ResearchDisplayer.State.Now => "在队列中",
                ResearchDisplayer.State.NotActive => "开始此项研究",
                _ => ""
            };
        }
    }

    private void Start()
    {
        StartCoroutine(UpdateUI());
    }
    public void DoInvest()
    {
        if (SelectDisplayer.CurState == ResearchDisplayer.State.Finished)
            return;
        ResearchManager.Instance.RefreshResearchQueue(CurSelect);
    }
    IEnumerator UpdateUI()
    {
        while (true)
        {
            if (CurSelect)
            {
                DoInvestButton.text = ButtonText;
                ProgressPercentage.value = (float)SelectDisplayer.ProgressPercent;
                BaseInfo.text = $"{CurSelect.Label}\n{CurSelect.TechLevel.GetDescription()}\n{SelectDisplayer.ProgressPercent * 100f:F2}%\n{CurSelect.Description}";
                if (CurSelect != PreSelect)
                {
                    for (int i = 1; i < ResourceList.childCount; i++)
                        Destroy(ResourceList.GetChild(i).gameObject);
                    foreach (var (r, num) in CurSelect.ResourceRequirement)
                    {
                        GameObject Displayer = Instantiate(ResearchResourceReqPrefab);
                        Image image = Displayer.transform.GetChild(0).GetComponent<Image>();
                        TMP_Text text = Displayer.transform.GetChild(1).GetComponent<TMP_Text>();
                        image.sprite = r.Sprite;
                        image.color = r.Color;
                        text.text = num.ToString();
                        Displayer.transform.SetParent(ResourceList);
                    }
                    PreSelect = CurSelect;
                }
            }
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
}