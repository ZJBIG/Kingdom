using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchViewer : MonoBehaviour
{
    public RectTransform Content;
    [SerializeField] private TMP_Text BaseInfo;
    [SerializeField] private RectTransform ResourceList;
    [SerializeField] private TMP_Text DoInvestButton;
    [SerializeField] private Slider ProgressPercentage;
    [HideInInspector] public Research CurSelect;

    ResearchDisplayer SelectDisplayer => ResearchManager.Instance.Displayers[CurSelect];
    bool SelectFinished => SelectDisplayer.CurState == ResearchDisplayer.State.Finished;

    private void Start()
    {
        StartCoroutine(UpdateUI());
    }
    public void DoInvest()
    {
        if (SelectFinished)
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
                DoInvestButton.text = SelectFinished ? "当前研究已完成" : "开始此项研究";
                ProgressPercentage.value = (float)SelectDisplayer.ProgressPercent;
            }
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
    private IEnumerator UpdateLine()
    {
        while (true)
        {

            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
}