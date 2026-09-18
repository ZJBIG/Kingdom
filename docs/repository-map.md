# Kingdom 项目定位地图

此文件是仓库结构/API导航的唯一详细地图，由项目开发技能按需读取。2026-09-13核对；路径不是运行通过证明，修改前重读声明、调用者和相关handoff。以下 `Script/` 代表 `Assets/Resources/Script/`。

## 项目结构

- `ProjectSettings/ProjectVersion.txt`：权威Unity版本；`Packages/manifest.json`：实际依赖。
- `Assets/Scenes/SampleScene.unity`：主场景；构建启用情况看 `ProjectSettings/EditorBuildSettings.asset`。
- `Script/Kingdom.Runtime.asmdef`：运行时程序集；`Assets/Tests/PlayMode/Kingdom.PlayModeTests.asmdef`：PlayMode程序集。
- `Assets/Resources/Datas/`：Resource、Building、Research、Workshop、Sector、Story、Tutorial资产；TechLevel是GameManager中的枚举，不是独立资产目录。
- `Assets/Resources/UI/Kingdom/`：固定UI Prefab，核心KingdomUIRoot；`KingdomUIPrefabLibrary`负责加载。
- `Assets/Tests/`：EditMode/PlayMode；`Assets/Editor/`：迁移、校验、导出、构建工具。
- `docs/`：架构、平衡、内容、测试契约；`tools/codex/`：辅助执行程序；`tools/NewEconomySimulator/`：确定性诊断。
- `.agents/skills/`：项目开发主入口及内容/经济、UI两个领域分支；任务分流只在主技能维护。`.workbuddy-ai/skills/`：轻量客户端入口，不等于自动注册已验证。
- `.codex/handoffs/`：连续交接；`.codex/prompts/`：薄续接入口；`.codex/archive/`：可恢复历史，不作当前证据。
- `data/economy-parity/`：存在时的快照/事件/首次差异，必须核对实际输入与Unity采集来源。
- 根Developer csproj/sln：Build委托仓库脚本并依赖Unity缓存，不是自足的普通.NET测试工程。

## 按症状定位

| 症状 | 代码路径（Script/下） | 入口与检查点 |
|---|---|---|
| 启动/载入/离线重复结算 | `Manager/GameBootstrap.cs`、`Manager/SaveManager.cs` | Bootstrap停表、校验、初始化、LoadOrCreateGame、离线应用、恢复模拟；启动成功不等于所有告警校验通过 |
| 积压/实时与离线差异 | `Manager/SimulationManager.cs` | Advance、ManualTick、AdvanceOffline；Editor卡顿与Player积压路径不同 |
| 产量/饥饿/效率/研究力 | `Manager/BuildingManager.cs`、`Manager/ResourceManager.cs` | PrepareTickResourceSatisfaction、GetAmount、TryApplyAtomicPayment/Changes，追踪State及修饰器 |
| 建造/拆除/升级/成本 | `Manager/BuildingManager.cs`、`Validation/BuildingTransactionRules.cs` | TryBuild、TryDeconstruct、TryUpgrade、GetMaxBuildable；先校验再提交 |
| 研究队列/付款 | `Manager/ResearchManager.cs`、`Runtime/ResearchState.cs` | TryStartNextQueuedResearch、TryPayResearchCost、Tick；前置、支付台账、ResearchPower |
| 工坊购买/效果 | `Manager/WorkshopManager.cs`、`Manager/ProgressionModifierManager.cs` | TryPurchase与ResearchEffectType消费链，不以定义存在证明效果生效 |
| 星区/殖民/战役/舰队 | `Manager/SectorManager.cs`、`Manager/CampaignManager.cs` | TryUnlock、TryOccupy、TickOccupiedResourceProduction、TickActiveColonization/Campaign；星区建筑与本土领土不同 |
| 存档失败/版本拒绝 | `Manager/SaveManager.cs`、`Data/SaveFormat.cs` | CaptureSaveData、LoadOrCreateGame、SaveNow；v9 主档事务、临时文件原子替换与成功后时间戳提交 |
| 数字/舍入/费用 | `Math/ExpantaNum.cs`、`Math/ExpantaNumExtensions.cs` | 调用点、闭式公式、容差，不重建BigNumber |
| UI页/详情/列表/研究图 | `UI/KingdomUIRoot*.cs` | 按ResearchTree、DetailPanel、DetailUI、PageRows、LiveRefresh、SceneLayout等partial定位 |
| 触摸/滚动/安全区 | `UI/UIResearchGraphGesture.cs`、`UI/UIResearchGraphDragForwarder.cs`、`UI/UIPageScrollDragForwarder.cs`、`UI/SafeAreaFitter.cs` | 节点转发、背景射线、真实边界、页面位置；规则见UI技能 |
| 音乐/教程/剧情 | `Manager/MusicManager.cs`、`Manager/TutorialManager.cs`、`Manager/StoryManager.cs` | 先读同主题交接；UI关闭不应停止业务，剧情历史不反向授予经济效果 |

## 当前tick顺序

以 `SimulationManager.ManualTick` 为依据：
1. BuildingManager准备资源满足率、效率与研究力。
2. GameManager.Tick，携带安全人口离开额度。
3. 星区占领产出，再ResourceManager.Tick。
4. 殖民与战役Tick。
5. ResearchManager.Tick，再刷新剧情。

不从旧目标架构段落推断当前存在自动建造队列。改变顺序是行为修改，须确定性回归，而不是格式重构。

## 状态与序列化

- `Data/GameDefinition.cs`、`Data/DataBase.cs`：稳定ID，按类型从Datas加载并建索引。
- `Runtime/`保存Resource/Building/Research/Game/Population/Territory/Workshop等State；SectorState实际位于 `Data/SectorState.cs`。
- `Misc/Tool.cs` 的Pair保留first/second、相等/哈希/解构。当前ResourceAmountDefinition在资产层保存resource/amount字符串，转换缓存为运行时 `Pair<Resource, ExpantaNum>`；这不是授权新增 `Pair<Resource,string>` 或重写合法资产。
- `Validation/`覆盖经济依赖、研究、星区和交易规则；必须读当前覆盖范围以及告警/失败语义。
- `Assets/Editor/Migration/DefinitionIdMigration.cs` 有写资产作用，先核对覆盖类型，不假定完整包含Workshop。

## 交接与旧材料

按主题寻找现行handoff：内容修缮、模拟器、滚动、安全区ticker、音乐、时代档案、资源素材。`CONTENTADVISE/`和日期审计是建议/决策背景，不是当前运行验收；旧归档不能覆盖当前代码。规则整理由项目开发技能同主题handoff持续记录。`CONTENTADVISE/` 已在提交 a85f0e7 从活动目录删除（引用它的文档属悬空引用）；全文恢复件在 `.codex/archive/recovered-20260915/CONTENTADVISE/`，用 `git show a85f0e7^:<路径>` 可复核，不要据此重建活动目录。
