using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public enum TechLevel
{
    [Description("原始时代")] Primitive,
    [Description("中世纪")] Medieval,
    [Description("工业时代")] Industrial,
    [Description("太空时代")] Spacer,
    [Description("极致时代")] Ultra,
    [Description("远古科技时代")] Archotech,
    [Description("超凡时代")] Ascend
}
public class GameManager : Singleton<GameManager>
{
    const string SAVE_FILE = "GameDatas.json";

    public float AutoSaveDuration = 30;

    [HideInInspector] public string Calendar;
    [HideInInspector] public string KingdomName;
    [HideInInspector] public TechLevel TechLevel;
    [HideInInspector] public BigNumber CurrentFood;
    [HideInInspector] public BigNumber FoodGenerateRate,FoodConsumeRate;
    [HideInInspector] public BigNumber KingdomSpace;
    [HideInInspector] public BigNumber Productivity;

    [SerializeField] private Transform Top;

    [SerializeField] private TMP_Text Text_Calendar;
    [SerializeField] private TMP_Text Text_TechLevel;
    [SerializeField] private TMP_Text Text_Food;
    [SerializeField] private TMP_Text Text_KingdomName;
    [SerializeField] private TMP_Text Text_Productivity;
    [SerializeField] private TMP_Text Text_KingdomSpace;
    [SerializeField] private TMP_Text Text_TopText;

    [Header("Viewer")]
    [SerializeField] private RectTransform ResourceViewer;
    [SerializeField] private RectTransform BuildingViewer;
    [SerializeField] private RectTransform ResearchViewer;
    private readonly Vector2 ResourceViewerLocalPos = new Vector2(-400, 895);
    private readonly Vector2 BuildingViewerLocalPos = new Vector2(-400, 895);
    private readonly Vector2 ResearchViewerLocalPos = new Vector2(0, 895);
    private readonly Vector2 OutsideTheWindows = new Vector2(-10000, -10000);
    private void Start()
    {

        Application.runInBackground = true;

        StartCoroutine(LoadTheGame());
        StartCoroutine(AutoSave(AutoSaveDuration));
        StartCoroutine(UpdateUI());

        UpdateViewerPosition();
    }
    private void InitializeGame()
    {
        Calendar = CalendarToString(5500, 3, 12);
        KingdomName = "鼠托邦";
        TechLevel = TechLevel.Primitive;
        KingdomSpace = 100000;
        CurrentFood = 10000;
        Productivity = 100;


        ResourceManager.Instance.AddResource(ResourceManager.Instance.ResourceFinder.WoodLog);
        ResourceManager.Instance.DisplayerSets["WoodSet"].Displayers[ResourceManager.Instance.ResourceFinder.WoodLog].GenerateRate = 10;
    }
    private string CalendarToString(int year, int month, int day)
    {
        return $"{year} {month} {day}";
    }
    IEnumerator AutoSave(float duration)
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(duration);
            SaveTheGame();
        }
    }
    IEnumerator UpdateUI()
    {
        while (true)
        {
            Text_Calendar.text = Calendar;
            Text_TechLevel.text = $"技术等级:{TechLevel.GetDescription()}";
            Text_Food.text = $"粮食:{CurrentFood.ToString()}   {(FoodGenerateRate-FoodGenerateRate).ToStringWithPositiveSign()}/s";
            Text_KingdomName.text = $"国名:{KingdomName}";
            Text_Productivity.text = $"生产力:{Productivity}";
            Text_KingdomSpace.text = $"剩余领土:{KingdomSpace}";
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
    public void SwitchTopUI()
    {
        Text_TopText.text = Text_TopText.text switch
        {
            "资源" => "建筑",
            "建筑" => "研究",
            _ => "资源"
        };
        UpdateViewerPosition();
    }
    private void UpdateViewerPosition()
    {
        ResourceViewer.localPosition = Text_TopText.text == "资源" ? ResourceViewerLocalPos : OutsideTheWindows;
        BuildingViewer.localPosition = Text_TopText.text == "建筑" ? BuildingViewerLocalPos : OutsideTheWindows;
        ResearchViewer.localPosition = Text_TopText.text == "研究" ? ResearchViewerLocalPos : OutsideTheWindows;
    }
    public override void Save()
    {
        var path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        var SaveData = new SaveData
        {
            Calendar = Calendar,
            KingdomName = KingdomName,
            TechLevel = TechLevel,
            CurrentFood = CurrentFood,
            FoodGenerateRate = FoodGenerateRate,
            FoodConsumeRate = FoodConsumeRate,
            KingdomSpace = KingdomSpace,
            Productivity = Productivity,
        };

        string json = JsonUtility.ToJson(SaveData, true);

        File.WriteAllText(path, json);
        Debug.Log($"Saved game data to: {path}");
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

        Calendar = SaveData.Calendar;
        KingdomName = SaveData.KingdomName;
        TechLevel = SaveData.TechLevel;
        CurrentFood = SaveData.CurrentFood;
        FoodGenerateRate = SaveData.FoodGenerateRate;
        FoodConsumeRate = SaveData.FoodConsumeRate;
        KingdomSpace = SaveData.KingdomSpace;
        Productivity = SaveData.Productivity;
    }
     void SaveTheGame()
    {
        ResourceManager.Instance.Save();
        BuildingManager.Instance.Save();
        ResearchManager.Instance.Save();
        Save();
    }
     IEnumerator LoadTheGame()
    {
        var path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        yield return new WaitForSecondsRealtime(0.1f);
        if (!File.Exists(path))
        {
            InitializeGame();
            yield break;
        }
        ResourceManager.Instance.Load();
        BuildingManager.Instance.Load();
        ResearchManager.Instance.Load();
        Load();
    }
    [Serializable]
    public class SaveData
    {
        public string Calendar;
        public string KingdomName;
        public TechLevel TechLevel;
        public BigNumber CurrentFood;
        public BigNumber FoodGenerateRate, FoodConsumeRate;
        public BigNumber KingdomSpace;
        public BigNumber Productivity;
    }
}
