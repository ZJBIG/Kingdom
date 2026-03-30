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
    public static class TransitionLineColor
    {
        public static readonly Color UnSel = new Color(1, 1, 1, 0.1882353f);
        public static readonly Color Prior = new Color(0, 0.6784314f, 1, 0.5058824f);
        public static readonly Color Next = new Color(0.05882353f, 0.9490196f, 0.1882353f, 0.3764706f);
    }
    public static class OutlineColor
    {
        public static readonly Color Sel = new Color(0f, 0.6901961f, 0.7647059f, 1f);
        public static readonly Color UnSel = new Color(0f, 0f, 0f, 0f);
    }

    [SerializeField] private TMP_Text Label;
    [SerializeField] private TMP_Text Point;
    [SerializeField] private Image Outline;
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
        if (ResearchManager.Instance.ResearchViewer.CurSelect)
            ResearchManager.Instance.Displayers[ResearchManager.Instance.ResearchViewer.CurSelect].Outline.color = OutlineColor.UnSel;
        ResearchManager.Instance.ResearchViewer.CurSelect = Research;

        Outline.color = OutlineColor.Sel;
        foreach (var ((from, to), line) in ResearchManager.Instance.Lines)
        {
            line.GetComponent<Image>().color = TransitionLineColor.UnSel;
            if (from == Research)
                ResearchManager.Instance.Lines[new Pair<Research, Research>(from, to)].GetComponent<Image>().color = TransitionLineColor.Next;
        }
        foreach (var r in Research.Prequisites)
            ResearchManager.Instance.Lines[new Pair<Research, Research>(r, Research)].GetComponent<Image>().color = TransitionLineColor.Prior;
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

