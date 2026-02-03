using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchViewer : MonoBehaviour
{
    public RectTransform Content;
    public RectTransform LineContainer;
    [SerializeField] private TMP_Text BaseInfo;
    [SerializeField] private RectTransform ResourceList;
    [SerializeField] private TMP_Text DoInvestButton;
    [SerializeField] private Slider ProgressPercentage;
    [HideInInspector] public Research CurSelect;

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
                BaseInfo.text = $"{CurSelect.Label}\n{CurSelect.TechLevel}\n{SelectDisplayer.ProgressPercent * 100f:F2}%\n{CurSelect.Description}";
                DoInvestButton.text = ButtonText;
                ProgressPercentage.value = (float)SelectDisplayer.ProgressPercent;
            }
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
}