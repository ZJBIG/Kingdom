using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResearchDisplayer : MonoBehaviour
{
    public enum State
    {
        Finished, Now, InQueue, NotActive
    }
    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Point;
    [SerializeField] private Slider Fill;
    [HideInInspector] public State CurState = State.NotActive;
    [HideInInspector] public BigNumber ProgressReal;
    [HideInInspector] public Research Research;
    public double ProgressPercent => BigNumber.Clamp01(ProgressReal / Research.BaseCost).ToDouble();
    void Start()
    {
        Label.text = Research.Label;
        Point.text = ((BigNumber)(Research.BaseCost)).ToString();

        StartCoroutine(UpdateUI());
    }
    public void RefreshResearchQueue() => ResearchManager.Instance.RefreshResearchQueue(Research);
    public void SetSelect()
    {
        ResearchManager.Instance.ResearchViewer.CurSelect = Research;
        foreach (var (_, line) in ResearchManager.Instance.Lines)
            line.GetComponent<Image>().color = ResearchTransitionLine.UnSel;
        foreach (var r in Research.Prequisites)
            ResearchManager.Instance.Lines[new Pair<Research, Research>(r, Research)].GetComponent<Image>().color = ResearchTransitionLine.Sel;
    }
    IEnumerator UpdateUI()
    {
        while (true)
        {
            Fill.value = (float)ProgressPercent;
            if (CurState == State.Finished)
                yield break;
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }


    [Serializable]
    public class ResearchDisplayerData
    {
        public string ResearchName;
        public State CurState;
        public BigNumber ProgressReal;
    }
}

