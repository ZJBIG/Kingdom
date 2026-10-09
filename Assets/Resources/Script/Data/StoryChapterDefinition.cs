using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "创建剧情章节", menuName = "数据/剧情章节", order = 1)]
public sealed class StoryChapterDefinition : GameDefinition
{
    [Header("身份")]
    [Tooltip("稳定编号首次按资产名生成；运行时不依赖文件名。")]
    public string Title;
    public TechLevel RequiredEra;

    [Header("叙事内容")]
    [TextArea(2, 4)] public string Summary;
    [TextArea(8, 30)] public string Body;
    [SerializeField] private Sprite illustration;
    public Sprite Illustration => illustration;

    [Header("解锁条件")]
    [Tooltip("完成指定教程动作后解锁。")]
    public string RequiredTutorialStepId;
    [Tooltip("列表中的研究必须全部完成。")]
    public List<Research> RequiredResearch = new();
    [Tooltip("列表中的建筑必须全部拥有。")]
    public List<Building> RequiredBuildings = new();
    [Tooltip("列表中的工坊升级必须全部购买。")]
    public List<WorkshopUpgrade> RequiredWorkshops = new();
    [Tooltip("列表中的星区必须全部解锁。")]
    public List<SectorDefinition> RequiredUnlockedSectors = new();
    [Tooltip("列表中的星区必须全部占领。")]
    public List<SectorDefinition> RequiredOccupiedSectors = new();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            SetIdForEditor(name);
        else
            SetIdForEditor(Id);

        int bodyLength = string.IsNullOrWhiteSpace(Body) ? 0 : Body.Trim().Length;
        if (bodyLength < 200 || bodyLength > 300)
            Debug.LogWarning("剧情章节正文建议控制在 200–300 字：" + Id +
                "，当前约 " + bodyLength + " 字。", this);
        string[] forbidden = { "玩家", "页面", "菜单", "按钮", "ID", "数值", "数字", "奖励", "界面" };
        for (int i = 0; i < forbidden.Length; i++)
            if ((Title ?? string.Empty).Contains(forbidden[i]) ||
                (Summary ?? string.Empty).Contains(forbidden[i]) ||
                (Body ?? string.Empty).Contains(forbidden[i]))
                Debug.LogError("剧情章节包含禁用元叙事词“" + forbidden[i] + "”：" + Id, this);
    }

#endif

    public StoryChapter ToRuntime()
    {
        return new StoryChapter(
            Id, Title, RequiredEra.GetDescription(), Summary, Body, RequiredEra,
            RequiredTutorialStepId, RequiredResearchIds(), RequiredBuildingIds(),
            RequiredWorkshopIds(), RequiredUnlockedSectorIds(),
            RequiredOccupiedSectorIds(), illustration);
    }

    private List<string> RequiredResearchIds() => ToIds(RequiredResearch);
    private List<string> RequiredBuildingIds() => ToIds(RequiredBuildings);
    private List<string> RequiredWorkshopIds() => ToIds(RequiredWorkshops);
    private List<string> RequiredUnlockedSectorIds() => ToIds(RequiredUnlockedSectors);
    private List<string> RequiredOccupiedSectorIds() => ToIds(RequiredOccupiedSectors);

    private static List<string> ToIds<T>(List<T> definitions) where T : GameDefinition
    {
        var ids = new List<string>();
        if (definitions == null)
            return ids;
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null)
                ids.Add(definitions[i].Id);
        return ids;
    }
}
