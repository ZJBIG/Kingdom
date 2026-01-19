using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class ResearchManager : Singleton<ResearchManager>
{

    public ResearchViewer ResearchViewer;
    [SerializeField] private GameObject ResearchDisplayerPrefab;
    [SerializeField] private GameObject TransitionLinePrefab;

    [HideInInspector] public Queue<Research> ResearcheQueue = new();
    [HideInInspector] public Collection<Pair<Research, Research>> ResearcheCollection = new();

    public Dictionary<Research, ResearchDisplayer> Displayers = new();

    Vector2 ZeroPoint = new Vector2(100, -25f);
    Func<float, float, Vector3> PlacePosition = (x, y) => new Vector3(x, -y) * 200;
    protected override void Initialize()
    {
        base.Initialize();
    }
    private void Start()
    {
        InitializeResearchDisplayer();
        StartCoroutine(DoInvest());
    }
    public void RefreshResearchQueue(Research research)
    {
        foreach (var (_, displayer) in Displayers)
            if (displayer.CurState != ResearchDisplayer.State.Finished)
                displayer.CurState = ResearchDisplayer.State.NotActive;

        ResearcheQueue.Clear();
        EnqueuePrequisites(research);
        Displayers[ResearcheQueue.Peek()].CurState = ResearchDisplayer.State.Now;
    }
    void EnqueuePrequisites(Research research)
    {
        if (Displayers[research].CurState == ResearchDisplayer.State.Finished)
            return;
        foreach (var r in research.Prequisites)
            EnqueuePrequisites(r);
        if (Displayers[research].CurState != ResearchDisplayer.State.InQueue)
        {
            ResearcheQueue.Enqueue(research);
            Displayers[research].CurState = ResearchDisplayer.State.InQueue;
        }
    }
    IEnumerator DoInvest()
    {
        while (true)
        {
            Debug.Log(ResearcheQueue.Count);
            if (ResearcheQueue.TryPeek(out Research research))
            {
                ResearchDisplayer cur = Displayers[research];


                cur.ProgressReal += 1f;


                if (cur.ProgressReal >= cur.Research.BaseCost)
                {
                    cur.CurState = ResearchDisplayer.State.Finished;
                    ResearcheQueue.Dequeue();
                }

            }
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
    public void AddResearch(Research research)
    {
        if (Displayers.ContainsKey(research))
            throw new System.Exception($"{research.name} has already added");

        ResearchDisplayer Displayer = Instantiate(ResearchDisplayerPrefab).GetComponent<ResearchDisplayer>();
        Displayer.Research = research;
        Displayer.ProgressReal = 0;
        Displayer.CurState = ResearchDisplayer.State.NotActive;
        Displayer.transform.SetParent(ResearchViewer.Content.transform);

        RectTransform rectTransform = Displayer.GetComponent<RectTransform>();
        rectTransform.localPosition = PlacePosition(research.x, research.y);

        Displayers.Add(research, Displayer);
    }
    public void InitializeResearchDisplayer() => Resources.LoadAll<Research>("Datas/Research").ToList().ForEach(r => AddResearch(r));
    public void InitializeResearchLine()
    {
        Resources.LoadAll<Research>("Datas/Research").ToList().ForEach(r =>
        {
            GameObject Line = Instantiate(TransitionLinePrefab);
            Vector2 begin, end = PlacePosition(r.x, r.y);
            ResearchTransitionLine LineComp =Line .GetComponent<ResearchTransitionLine>();
            Rect LineRect = Line.GetComponent<RectTransform>().rect;
            r.Prequisites.ForEach(p =>
            {
                LineComp.From = p;
                LineComp.To = r;
                begin = PlacePosition(p.x, p.y);
                float Length = Vector2.Distance(begin, end);
                LineRect.height = Length;
                Line.transform.Rotate(new Vector3(0, 0, Mathf.Atan2(end.y - begin.y, end.x - begin.x)));
            });
        });
    }
}