# 剧情数据维护规范

剧情档案由两层组成：`StoryArchive.asset` 只维护章节顺序，`StoryChapterDefinition` 资产维护单章内容和解锁条件。运行时只按档案列表顺序读取，不按文件名排序。

## 新增章节

1. 在 `Assets/Resources/Datas/Story` 根目录创建“数据/剧情章节”。
2. 用 PascalCase 的资产名命名，例如 `FirstFire_01`；章节资产首次保存时会把资产名填入隐藏的稳定 ID。
3. 填写标题、摘要和正文。正文必须为 200–300 字，推荐约 210–260 字；摘要只用于列表预览。
4. 研究、建筑和工坊条件直接从 Project 拖拽对应资产；教程和星区条件按真实编号/开关填写。多个条件同时满足才解锁。
5. 将章节资产按叙事顺序加入 `StoryArchive.asset` 的 `Chapters`。

`RequiredResearch`、`RequiredBuildings`、`RequiredWorkshops`、`RequiredUnlockedSectors` 和 `RequiredOccupiedSectors` 都是全量 AND 条件列表。建筑条件表示已建成并拥有，不表示仅满足前置即可建造。

章节 ID 是存档和外部引用使用的稳定标识，不能因为调整顺序而改写。资产文件名只是编辑便利；若确实改 ID，必须同步测试和存档策略。

不要把剧情正文写回 `StoryManager.cs`。`StoryManager` 负责运行时读取、顺序完成判定和永久历史查询；完成状态存放在 `GameState.StoryProgress`。

## 科技与探索顺序

章节顺序必须同时遵循科技进步和探索距离，不能只用时代枚举提前解锁。

- 工业时代依次为：工业化规模生产、精密制造、蒸汽与集中能源、铁路冶炼与物流、化工治理、钛合金与发射准备。
- 太空时代依次为：月球与近地轨道、偏远行星、太阳系深处与太阳资源、系外探索与旧文明边界。
- 涉及建立基地、长期驻留、远征成功或资源带回时，必须使用 `RequiredOccupiedSectors`；仅经过航线或发现目标不能替代占领条件。
- 月球阶段不得引用系外星区；偏远行星阶段必须继承月球阶段；太阳系阶段必须晚于近地阶段；系外星区只能出现在最后阶段。
- `StoryArchiveDefinition` 的编辑器校验会检查上述太空阶段的核心星区条件；新增章节也必须保持研究、建筑、工坊和星区条件可达。

当前基线固定为 18 章。`StoryManager.RefreshProgress` 将真实条件按档案顺序写入 `GameState.StoryProgress`；完成记录永久保留，不发放经济奖励。存档格式 v9 必须包含 `Story.CompletedChapterIds`，且只能是按档案顺序排列的连续前缀；未知、重复、跳章、乱序或高于当前时代的记录会使整个主档失效并开始新游戏。
