using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static ResearchDisplayer;

public class ResearchManager : Singleton<ResearchManager>
{

    const string SAVE_FILE = "ResearchDatas.json";

    [HideInInspector] public BigNumber GlobalEfficiencyFactor = 1;

    public ResearchViewer ResearchViewer;
    [SerializeField] private GameObject ResearchDisplayerPrefab;
    [SerializeField] private GameObject TransitionLinePrefab;

    public ResearchFinder ResearchFinder;

    [HideInInspector] public Queue<Research> ResearcheQueue = new();

    public Dictionary<Research, ResearchDisplayer> Displayers = new();
    public Dictionary<Pair<Research, Research>, ResearchTransitionLine> Lines = new();

    static readonly Vector3 ZeroPoint = new Vector3(100, -25f);
    readonly Func<float, float, Vector3> PlacePosition = (x, y) => ZeroPoint + new Vector3(x, -y) * 300;
    protected override void Initialize()
    {
        base.Initialize();
    }
    private void Start()
    {
        InitializeResearchDisplayer();
        InitializeResearchLine();
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
            if (ResearcheQueue.TryPeek(out Research research))
            {
                ResearchDisplayer cur = Displayers[research];
                cur.ProgressReal += GlobalEfficiencyFactor;
                if (cur.ProgressReal >= cur.Research.BaseCost)
                {
                    cur.CurState = State.Finished;
                    ResearcheQueue.Dequeue();
                    research.BuildingUnlock.ForEach(b =>
                    {
                        BuildingManager.Instance.AddBuilding(b);
                    });
                }
            }
            yield return new WaitForSecondsRealtime(0.02f);
        }
    }
    public void AddResearch(Research research)
    {
        if (Displayers.ContainsKey(research))
            throw new System.Exception($"{research.name} has already added");

        ResearchDisplayer Displayer = Instantiate(ResearchDisplayerPrefab).GetComponent<ResearchDisplayer>();
        Displayer.Research = research;
        Displayer.ProgressReal = 0;
        Displayer.CurState = State.NotActive;
        Displayer.transform.SetParent(ResearchViewer.Content.transform);

        RectTransform rectTransform = Displayer.GetComponent<RectTransform>();
        rectTransform.localPosition = PlacePosition(research.x, research.y);

        Displayers.Add(research, Displayer);
    }
    public void InitializeResearchDisplayer() => Resources.LoadAll<Research>("Datas/Research").ToList().ForEach(r => AddResearch(r));
    public void InitializeResearchLine()
    {
        foreach (var (r, _) in Displayers)
            if (r.Prequisites.Count != 0)
                foreach (var p in r.Prequisites)
                {
                    ResearchTransitionLine Line = Instantiate(TransitionLinePrefab, ResearchViewer.LineContainer.transform).GetComponent<ResearchTransitionLine>();

                    Vector2 begin, end;
                    ResearchTransitionLine LineComp = Line.GetComponent<ResearchTransitionLine>();
                    Rect LineRect = Line.GetComponent<RectTransform>().rect;

                    Lines.Add(new Pair<Research, Research>(p, r), Line);

                    begin = PlacePosition(p.x, p.y);
                    end = PlacePosition(r.x, r.y);

                    float dx = end.x - begin.x, dy = end.y - begin.y;
                    float Length = Mathf.Sqrt(dx * dx + dy * dy);

                    Line.GetComponent<Image>().rectTransform.sizeDelta = new Vector2(Length, 5f);

                    Line.transform.Rotate(new Vector3(0, 0, Mathf.Rad2Deg * Mathf.Atan2(dy, dx)));
                    Line.GetComponent<RectTransform>().localPosition = begin + new Vector2(dx / 2, dy / 2);
                }
    }





    public override void Save()
    {
        var path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        var SaveData = new SaveData
        {
            ResearchDisplayerData = new List<ResearchDisplayerData>(),
            Queue = new List<string>(),
            GlobalEfficiencyFactor = GlobalEfficiencyFactor,
        };

        foreach (var (_, displayer) in Displayers)
        {
            SaveData.ResearchDisplayerData.Add(new ResearchDisplayerData
            {
                ResearchName = displayer.Research.name,
                CurState = displayer.CurState,
                ProgressReal = displayer.ProgressReal,
            });
        }

        foreach (var research in ResearcheQueue)
            SaveData.Queue.Add(research.name);

        SaveData.CurSelect = ResearchViewer.CurSelect;

        string json = JsonUtility.ToJson(SaveData, true);

        File.WriteAllText(path, json);
        Debug.Log($"Saved research data to: {path}");
    }
    public override void Load()
    {
        var path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        if (!File.Exists(path))
            Save();
        var json = File.ReadAllText(path);
        LoadFromJson(json);
    }
    private void LoadFromJson(string json)
    {
        var SaveData = JsonUtility.FromJson<SaveData>(json);

        foreach (var data in SaveData.ResearchDisplayerData)
        {
            Research research = ResearchFinder.GetFromString(data.ResearchName);
            var displayer = Displayers[research];
            displayer.CurState = data.CurState;
            displayer.ProgressReal = data.ProgressReal;
        }

        ResearcheQueue.Clear();
        foreach (var researchName in SaveData.Queue)
            ResearcheQueue.Enqueue(ResearchFinder.GetFromString(researchName));

        ResearchViewer.CurSelect = SaveData.CurSelect;

        GlobalEfficiencyFactor = SaveData.GlobalEfficiencyFactor;
    }

    [Serializable]
    private class SaveData
    {
        public List<ResearchDisplayerData> ResearchDisplayerData;
        public List<string> Queue;
        public Research CurSelect;
        public BigNumber GlobalEfficiencyFactor;
    }
}