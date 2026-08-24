# Kingdom 全游戏主审查与改进蓝图

> 状态：静态全覆盖已经完成；性能优先批次已收敛到用户实测研究页切换卡顿消失，当前转入世界观与游戏引导的渐进改进。任务开始时采用只读单文档模式，后续用户已授权性能、编译、Android 配置及无设计分歧的文本/引导修复。

## 1. 文档目的与禁止事项

本文档是当前 `Kingdom` 工作树的单一主审查文档，目标是以当前代码、定义资产、Scene、Prefab、测试源码和允许引用的当前报告为依据，形成可由后续实施智能体逐批执行的玩法正确性、内容闭环、经济进度、移动端 UI/UX 与产品完整度蓝图。

任务开始时仅允许修改本文档。2026-08-23 起用户分阶段放宽范围，先允许修复明确文本，随后允许性能源码、C# 编译、Android 打包配置和不改变经济规则的引导修复；当前仍不改稳定 ID/GUID，不清理用户工作树，不把 CLI 编译冒充 Unity 编译，也不在教程纯展示批次运行闭包或模拟器。历史只读边界保留为任务起点记录，不再覆盖用户后续授权。

事实来源按以下顺序处理冲突：当前工作树代码/资产/Scene/Prefab；当前作用域规则；`.codex/prompts/CODEX_ECONOMY_PROMPT.md`；允许引用的当前报告；当前 `docs/`。`.codex/archive/`、旧 Kingdom 审查、日期迭代、旧日志和旧构建产物不作为当前证据。

结论状态采用 `已确认`、`强推断`、`待运行验证`、`策划机会`、`已失效`、`已解决`；证据等级采用 E1（当前代码/资产/Scene/Prefab）、E2（允许引用的当前报告）、E3（跨系统静态推导）、E4（需要运行或产品决策）。

锁定边界：Food 是唯一有库存容量的资源；不得重引 workforce；定义/Runtime State/Manager/UI 的权威边界不推倒重做；`SimulationManager` 是唯一玩法时钟；支付先全量验证再统一提交；普通资源和 Food 不得结算为负；建筑成本等比增长、批量操作采用闭式公式；优先补齐现有内容联系；模拟结果不得冒充 Unity 证据；玩法正确后再调节节奏。

## 2. 审查元数据

| 项目 | 当前值 |
|---|---|
| 审查日期 | 2026-08-23（Asia/Shanghai） |
| 分支 | `main` |
| HEAD | `22de990c34a7a2f7fdfbdb5b59f5175f62d04261` |
| 与远端关系 | `main...origin/main [ahead 96]` |
| 初始工作树 | 干净；`git diff --name-status` 无输出 |
| 仓库锁定 Unity 版本 | `2022.3.62f2c1`（规则） |
| 当前项目序列化版本 | `ProjectSettings/ProjectVersion.txt` 为 `2022.3.62f3c1`，见 `BUILD-P2-001` |
| 主场景 | `Assets/Scenes/SampleScene.unity`；Build Settings 中唯一启用场景 |
| 目标设备 | Huawei P40 Pro 横屏 |
| 主审查文件初始状态 | 本轮开始时不存在，首次创建 |
| 后续写入授权 | 用户已允许性能、C# 编译、Android 配置及无设计分歧的文本/引导修复；经济闭包与断言按用户要求留到后续收敛 |

已亲自完整读取的规则与必读资料：`AGENTS.md`、两个作用域 `AGENTS.md`、经济提示词、`kingdom-content-expansion`、`kingdom-economy-simulation`、`kingdom-ui-redesign`、`stop-that-shit`，以及目标要求列出的 balance/content/testing/architecture 文档。当前代码、定义资产、主 Scene、Prefab、测试源码和允许引用的当前报告均已进入静态覆盖矩阵。

已读取的当前报告证据仅包括：

- `data/content-closure-static.md`；
- `data/economy-simulation` 根目录以及 `Fast`、`Normal`、`Conservative` 的当前摘要、警告、里程碑、研究/建筑/Workshop 时间线与资源流；
- `TestResults/Latest-Test-Errors.txt`。

明确未执行：

- 未执行真实 Unity 编译。
- 未执行 Huawei P40 Pro 真机验收。
- 本轮未运行闭包检查、离线模拟器、EditMode 或 PlayMode；只读取了现有证据。

## 3. Kingdom 产品定位与核心体验判断

Kingdom 的当前核心承诺是：玩家把一个聚落从原始生存推进到跨星际文明，通过 Food/人口、资源加工、研究、领土、生产力、电力、物流与经营式战役形成逐时代扩展的决策网络。当前里程碑不是批量铺设 Ultra/Archotech，而是先证明玩法交易和模拟行为正确，并使早期纵向切片及当前活跃时代形成可玩闭环。

静态内容最完整、时代能力变化最明确的是 Industrial：电力、物流、石油、化工、多金属、机械化农业、Workshop 与高密度人口在同一时代汇合。Animal 的生存与定居引导基本成形；Neolithic 的实际内容已扩展到铜石并用、青铜、铁器与煤矿，时代名称比内容窄；Medieval 有钢、贸易物流、城市住宅和学术链，但部分节点在移除前 Spacer 战斗后退化为相近的全局产能百分比；Spacer 定义密度很高，并形成理论→工坊设备→建筑/战役的多数闭环，但多产物建筑的整栋增产和少数描述—效果错配削弱了选择可读性。Ultra 只有一项跃迁研究，Archotech 没有内容，因此两者目前是远期框架而不是可声称完成的游戏时代。

当前静态闭包覆盖到 Ultra，但当前模拟快照与资产总数不一致；再加上当前 HEAD 的编译阻断，本轮不能据报告断言真实节奏或 Unity 可玩性。以下玩家旅程均明确标为静态推演，所有交互、视觉、帧率与设备体验仍待运行验证。

## 4. 当前游戏内容和系统快照

### 4.1 定义数量（当前资产直接计数）

| 定义类型 | 总数 | Animal | Neolithic | Medieval | Industrial | Spacer | Ultra | Archotech |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Resource | 40 | 6 | 6 | 3 | 18 | 7 | 0 | 0 |
| Building | 65 | 9 | 10 | 5 | 26 | 15 | 0 | 0 |
| Research | 128 | 16 | 17 | 12 | 35 | 47 | 1 | 0 |
| WorkshopUpgrade | 82 | 0 | 0 | 0 | 36 | 46 | 0 | 0 |
| Sector | 9 | — | — | — | — | 9（系统阶段） | — | — |
| TutorialStep | 8 | 早期流程共用 | — | — | — | — | — | — |

计数来源为 `Assets/Resources/Datas/{Resource,Building,Research,Workshop,Sector,Tutorial}/**/*.asset`，时代统计采用资产序列化 `TechLevel`。五项 `AdvancesTechLevel` 研究为 `NeolithicSettlement`、`FeudalAdministration`、`Industrialization`、`InterstellarNavigation`、`TechnologicalSingularity`。

### 4.2 Scene、页面、UI 与媒体

- `Assets/Scenes/SampleScene.unity` 是唯一启用的游戏 Scene；另一个 `.unity` 是 `Assets/Settings/Scenes/URP2DSceneTemplate.unity` 模板，不计为游戏场景。
- 主页面共 8 个：Overview、Resources、Buildings、Research、Era、Workshop、Music、Sectors；均可在 `KingdomUIRoot.prefab` 及 `KingdomUIRoot` 分部类中定位。
- Kingdom UI 有 13 个 Prefab：根、页面、资源/建筑/研究行、数量控件、研究图/节点/线/时代带/工具栏、音乐行、文本行。
- `KingdomUIRoot.prefab` 的 CanvasScaler 静态配置为 `ScaleWithScreenSize`、`2640x1200`、Match Width（`m_MatchWidthOrHeight: 0`）；`KingdomUIRoot.Awake/ConfigureP40CanvasScaler` 也会重申该契约。是否在设备上得到正确 viewport/content 和安全区仍待运行证据。
- 当前资产共有 129 张 PNG；其中包含研究树和 Kingdom UI 资源，也包含大量未确认是否仍被运行时引用的宝石、龙/Golem 等旧风格素材，阶段 4 将审查引用与一致性。
- 当前音乐目录有 18 个音频文件（14 MP3、4 OGG）；尚未据静态文件名推断授权、分类或风格验收。

### 4.3 测试与报告快照

- 测试源码含 34 个 Editor 测试文件和 2 个 PlayMode 测试文件；当前静态声明计数为 555 个 `[Test]/[TestCase]` 标注与 16 个 `[UnityTest]` 方法。参数化标注数量不是实际运行用例数。
- `TestResults/Latest-Test-Errors.txt`（生成于 2026-08-24 02:02:00 +08:00）记录 `Mode: 0`、554 total、552 passed、2 failed。失败为 `PopulationNetRate_UsesStarvationDepartureBeforePositiveGrowth` 缺少测试 `BuildingManager`，以及 `WorkshopRestore_AcceptsPurchasedIdsInNonTopologicalOrder` 在恢复时缺少状态字典项；该文件仍没有证明 16 个 PlayMode 方法被执行，因此不作为 PlayMode 验收。
- `data/content-closure-static.md` 报告：40 Resource；Industrial 及以前 Research 80/80、Workshop 36/36、Building 50/50 可达；Spacer 47/47、46/46、15/15 可达；Ultra Research 1/1 可达，无 Ultra Workshop/Building。此为静态闭包 E2，不是 Unity 行为或节奏证据。
- `data/economy-simulation/PacingAcceptance.txt` 当前为 `FAIL`。Normal/Fast 在 24 小时内未到 Medieval；Conservative 在 426.32 分钟到 Medieval、1142.83 分钟到 Industrial、未到 Spacer；各路线目标窗口也有偏差。
- 当前模拟报告声明的严格快照为 40 Resource、69 Building、121 Research、73 Workshop，而当前资产为 40/65/128/82。由此模拟与 PacingAcceptance 不能代表当前定义集合，见 `REPORT-P1-001`。
- Normal 与根报告均在 76.32 分钟到 Neolithic 后未到 Medieval；Fast 在 79.12 分钟到 Neolithic 后未到 Medieval；Conservative 在 64.58 分钟到 Neolithic、426.32 分钟到 Medieval、1142.83 分钟到 Industrial。以上仅是过期输入快照的诊断背景，不作当前玩法结论。

## 5. 全量覆盖矩阵

“静态已检查”表示当前代码、资产、Prefab、测试源码和允许引用报告已经交叉核查；它不等于运行验收。所有 UI/运行时项目即便静态实现存在，也仍缺 Unity 日志、PlayMode、玩家测试或真机证据。

| 系统/时代 | 已检查代码与资产/证据 | 状态 | 发现编号 | 最高证据 | 尚缺验证 |
|---|---|---|---|---|---|
| 定义数据库、稳定 ID、GUID | `DataBase.cs`、`GameDefinition.cs`、核心定义资产与 `.meta` 引用 | 静态已检查 | `COMPILE-P1-001`、`SAVE-P0-001` | E1 | Unity 导入与真实编译 |
| Resource 全时代 | 40 个 Resource、来源/用途、UI 可见性与当前报告 | 静态已检查 | `REPORT-P1-001`、`RESOURCE-P2-001` | E1/E2/E3 | Unity tick 与页面渲染 |
| Building 全时代 | 65 个 Building、交易、升级链、产消、电力/物流字段 | 静态已检查 | `ECON-P1-001`、`ECON-P2-003` | E1/E3 | 生产、升级、退款 PlayMode |
| Research 全时代 | 128 个 Research、前置、支付、效果、跃迁 | 静态已检查 | `RESEARCH-P1-001`、`ECON-P1-002`、`ECON-P2-004` | E1/E2 | 队列、支付、效果运行证据 |
| Workshop | 82 个资产、前置、保存、效果与页面可见性 | 静态已检查 | `SAVE-P0-001`、`COMPILE-P1-001`、`WORKSHOP-P2-001`～`002` | E1/E2 | 购买/保存/显示 PlayMode |
| Animal | 6 R / 9 B / 16 Research；早期启动与教程 | 静态已检查 | `ONBOARD-P2-001`、`TUTORIAL-P2-001` | E1/E3 | 新游戏 1/10 分钟实玩 |
| Neolithic | 6 R / 10 B / 17 Research；定居、农业、冶金 | 静态已检查 | `ERA-P2-001` | E1/E3 | 跃迁与时代理解测试 |
| Medieval | 3 R / 5 B / 12 Research；钢、物流、城市、学术 | 静态已检查 | `ERA-P2-002` | E1/E3 | 时代选择与回本实玩 |
| Industrial | 18 R / 26 B / 35 Research / 36 Workshop | 静态已检查 | `ECON-P1-001`～`002`、`ECON-P2-003`、`TEXT-P3-003` | E1/E2/E3 | 电力/物流/Workshop Unity 行为 |
| Spacer | 7 R / 15 B / 47 Research / 46 Workshop / 9 Sector | 静态已检查 | `SECTOR-P1-001`、`WORKSHOP-P2-002`、`ECON-P2-004` | E1/E2/E3 | 战役、殖民、维修与 UI 运行证据 |
| Ultra | 1 Research，无 R/B/Workshop | 静态已检查；远期框架 | — | E1/E2 | 产品决策与可玩目标 |
| Archotech | 无定义资产 | 静态已检查；远期框架 | — | E1 | 产品决策 |
| Food/人口/幸福/生产力 | Runtime State、Manager、公式与人口建筑 | 静态已检查 | `POP-P2-001`、`ECON-P1-002`、`ECON-P2-004` | E1/E3 | 满载、短缺与恢复 PlayMode |
| 领土/电力/物流 | State、Building/Research/Workshop、满足率 | 静态已检查 | `ECON-P1-001` | E1 | 实际 tick 与瓶颈反馈 |
| Simulation/离线 | `SimulationManager`、`SaveManager`、允许引用的报告 | 静态已检查 | `REPORT-P1-001`、`SIM-P2-001` | E1/E2 | 帧率/离线一致性 |
| 存档/迁移/备份 | `SaveManager`、DTO、回退与 Workshop 恢复 | 静态已检查 | `SAVE-P0-001` | E1 | 损坏回退、旧版与重复加载 |
| Sector/殖民/战役 | 9 资产、Definition/State/Manager/Test 源码 | 静态已检查 | `SECTOR-P1-001` | E1 | 完整战役 PlayMode |
| Tutorial | 8 资产、Manager、导航、奖励与保存 | 静态已检查；两项导航修复待运行 | `TUTORIAL-P2-001`、`TUTORIAL-P3-002`～`003` | E1 | 导航、节奏、奖励 PlayMode |
| UI 状态边界/页面生命周期 | `KingdomUIRoot*`、主 Scene、13 Prefab | 静态已检查 | `UI-P2-001`～`003`、`WORKSHOP-P2-001` | E1 | 页面生命周期与移动端交互 |
| ResearchTreeSK 图 | `.il/.res`、图代码/Prefab/测试源码 | 静态已检查 | `TEST-P2-001`、`UI-P2-002`～`003` | E1 | 当前内容口径专项日志 |
| P40 横屏/安全区 | PlayerSettings、CanvasScaler、SafeAreaFitter | 静态已检查 | `BUILD-P2-001`、`UI-P2-003` | E1 | 截图、viewport/content、真机 |
| 文本/音画/可访问性 | 129 PNG、18 音频、定义/UI 文本 | 静态已检查 | `TEXT-P2-001`、`TEXT-P3-002`～`003`、`AUDIO-P2-001`～`002`、`ASSET-P3-001` | E1/E4 | 字体/视觉/音量/授权/无障碍 |
| 测试证据 | 555 静态标注、16 UnityTest 方法、当前 Mode 0 失败报告 | 最新 EditMode 报告执行的是改动前 554 项且有 2 项失败；PlayMode 未证明 | `COMPILE-P1-001`、`TEST-P2-001`、`REPORT-P1-001` | E1/E2 | 修复后 EditMode 报告、明确 PlayMode XML 与日志 |
| 构建/编辑器契约 | ProjectVersion、Build Settings、规则 | 已检查冲突 | `BUILD-P2-001` | E1 | 指定 Unity 编译 |

当前覆盖进度：所有真实系统和时代均已完成静态检查；剩余项目全部属于明确列出的 Unity、PlayMode、日志、玩家测试或 P40 真机证据缺口。

## 6. 执行摘要

当前未解决静态计数为 P0 = 1、P1 = 5；另有 1 项 P1 编译阻断已完成静态源码修复，但尚未取得 Unity 编译证据。最高风险不是“内容不够”，而是合法 Workshop 存档可能无法恢复、研究支付不原子、多输入建筑满足率公式错误、三个定义重复叠加 Food 效果、本地殖民会清空并行战役，以及模拟报告输入版本失配。它们分别阻断存档、交易、生产、经济、后期状态与可信调参，必须早于 UI 美化、节奏调参和 Ultra/Archotech 扩展。

内容侧的正面结论是：128 个 Research 与 82 个 Workshop 都有真实效果，所有前置不存在时代倒挂，五条主要建筑升级链把早期住房、研究、农业、物流和材料工业延续到 Spacer；满载人口的同代粮食供给静态比例也合理。主要内容风险集中在时代命名、Medieval 选择密度、多产物建筑的效果粒度、少数 Spacer Workshop 描述—效果错配，以及 Ultra/Archotech 仍为空框架。

用户授权后，本轮已直接完成 25 个资产的纯文本修复（`TEXT-P3-003`），恢复 `WorkshopUpgrade.cs` 的字符串边界/乱码，并修复教程生产链的箭头和显示端点（`COMPILE-P1-001`、`TEXT-P3-002`）；未改变经济数值、链条拥有判定或教程完成条件。

## 7. P0/P1 优先问题

当前未解决静态计数：P0 = 1；P1 = 5。`COMPILE-P1-001` 保留 P1 历史优先级，但已完成静态修复并移出未解决计数。

- `SAVE-P0-001`：Workshop 合法购买集合按稳定 ID 顺序保存，却按“前置必须先出现”恢复；含逆字典序前置的正常进度可使主存档和备份均无法应用并回退新游戏。
- `COMPILE-P1-001`（已解决）：`WorkshopUpgrade.cs` 原有三处未闭合字符串和大面积乱码已经恢复，枚举成员及显式值未变；2026-08-24 的 Unity EditMode 已执行 554 项测试，低风险 CLI 编译也为 0 错误，证明原语法阻断已解除。完整 EditMode 仍有 2 项独立失败。
- `RESEARCH-P1-001`：研究资源成本被允许分资源、分多次部分扣除，直接违反“开始前原子支付”的锁定规则。
- `ECON-P1-001`：多输入建筑把各资源满足率相乘而不是取瓶颈最小值，系统性低估生产效率。
- `ECON-P1-002`：Calendar、IndustrialAgriculture 和 AgriculturalMachinery 重复声明同一 Food 效果目标；运行时会重复相乘，当前定义测试源码也明确拒绝。
- `REPORT-P1-001`：当前离线模拟/PacingAcceptance 使用的定义快照与工作树不一致，阻断当前纵向切片的可信节奏判断。
- `SECTOR-P1-001`：本地殖民完成会无条件清空并行星际战役的全局状态。

## 8. 按系统分类的完整发现

### REPORT-P1-001 当前模拟输入快照与工作树定义集合不一致

- 标题：当前模拟输入快照与工作树定义集合不一致
- 类型：验证证据 / 经济模拟
- 状态：已确认（单处运行时混用已中性化，核心正典仍待决策）
- 优先级：P1
- 证据等级：E1 + E2
- 置信度：高
- 玩家发生场景：不直接发生于玩家端；影响任何基于当前报告安排的早期主线与节奏修改。
- 玩家可感知症状：若据旧快照调参，玩家可能继续遇到真实阻断或得到针对已删除/已新增定义的错误节奏修改。
- 具体影响：当前资产有 65 Building、128 Research、82 Workshop；四份模拟摘要均声明 69/121/73。`PacingAcceptance.txt` 的 FAIL 不能归因到当前定义，Workshop 购买时间线也不能覆盖当前 82 项资产。
- 当前证据：`Assets/Resources/Datas/**` 的直接文件与 `TechLevel` 计数；`data/content-closure-static.md` 与当前资产一致为 65/128/82；`data/economy-simulation/{,Fast,Normal,Conservative}/EconomySimulationReport.md` 均写 69/121/73。
- 根因或设计诊断：保留的“当前”模拟输出不是由当前定义集合生成，证据链版本失配。
- 为什么不符合 Kingdom 当前目标：玩法正确后才允许调节节奏，而当前节奏诊断不覆盖当前玩法输入。
- 推荐的最小解决方向：未来实施批次先定位快照版本失配的来源；在不扩展策略的前提下，用当前定义重新生成严格快照并确认输入计数等于 40/65/128/82，再解释 PacingAcceptance。
- 可选方案及取舍：若当前定义尚不应纳入模拟，应显式列出排除规则并使闭包与模拟采用同一口径；代价是需证明每个排除项不属于当前纵向切片。
- 明确不建议采用的方案：不建议直接调 Research/Building 数值让旧报告变绿；不建议扩展路线评分、决策 AI 或 trace 功能。
- 涉及的系统、代码和资产：`Assets/Resources/Datas`、`tools/EconomySimulator`（未来定位）、`data/content-closure-static.md`、`data/economy-simulation/**`。
- 前置依赖：先证明当前快照加载器与 `.meta` GUID 解析采用工作树定义。
- 风险和可能回归：重新生成会改变全部当前时间线；必须保留策略冻结及 Unity 证据边界。
- 工作量：S～M
- 是否可以交给实施智能体：是，作为独立 Wave 0 证据修复批次。
- 后续实施验收标准：输入计数与当前资产一致；自测/奇偶性门通过后重新运行；PacingAcceptance 明确记录真实当前结果；不得把模拟当 Unity 验收。
- 尚缺的验证证据：本轮禁止重新运行快照、模拟、构建和 Unity，因此尚未确认当前输出失配发生在哪个生成步骤。

### BUILD-P2-001 锁定 Unity 补丁版本与项目序列化版本不一致

- 标题：锁定 Unity 补丁版本与项目序列化版本不一致
- 类型：构建可复现性 / 文档矛盾
- 状态：已确认
- 优先级：P2
- 证据等级：E1
- 置信度：高
- 玩家发生场景：开发者用锁定编辑器打开、编译或构建项目时。
- 玩家可感知症状：本轮没有运行证据；潜在表现为版本升级/降级提示、重新序列化或构建差异。
- 具体影响：规则锁定 `2022.3.62f2c1`，`ProjectSettings/ProjectVersion.txt` 当前写 `2022.3.62f3c1 (1623fc0bbb97)`，无法同时声称两者是当前唯一编辑器版本。
- 当前证据：`AGENTS.md` 与本审查目标的锁定边界；`ProjectSettings/ProjectVersion.txt`。
- 根因或设计诊断：项目元数据和治理规则未同步。
- 为什么不符合 Kingdom 当前目标：Unity 编译、测试和真机证据必须可复现，版本口径冲突会削弱后续验收。
- 推荐的最小解决方向：由用户确认真正锁定版本；后续单独批次只同步规则或项目版本，不在内容批次混改资产。
- 可选方案及取舍：以当前项目序列化版本 `f3c1` 为准可减少降级风险；以规则 `f2c1` 为准则需验证无重序列化/兼容影响。
- 明确不建议采用的方案：不建议本审查直接打开 Unity 或批量重序列化资产。
- 涉及的系统、代码和资产：`ProjectSettings/ProjectVersion.txt`、`AGENTS.md`、CI/本地 Unity 环境。
- 前置依赖：用户/项目维护者确认版本决策。
- 风险和可能回归：Scene、Prefab、ScriptableObject 重新序列化及包兼容。
- 工作量：S（决策）/M（若需验证迁移）
- 是否可以交给实施智能体：决策后可以。
- 后续实施验收标准：治理文档、ProjectVersion、实际 Unity 可执行版本一致；完整编译与测试记录该版本。
- 尚缺的验证证据：未用任一版本打开或编译项目。

### COMPILE-P1-001 Workshop 效果源文件的未闭合字符串与乱码已静态修复

- 标题：Workshop 效果源文件的三处未闭合字符串与乱码已恢复
- 类型：编译阻断 / 文本编码
- 状态：已解决（静态修复，待 Unity 验证）
- 优先级：P1
- 证据等级：E1 + E2
- 置信度：高
- 玩家发生场景：修复前的源码被用于 Unity 编译、测试或后续构建时。
- 玩家可感知症状：修复前源码无法形成可运行的新版本；当前 Unity 已能编译到 Test Runner 并执行 EditMode，但尚未取得完整绿色测试和 Player 构建。
- 具体影响：原 `WorkshopEffectType` 的两个 `Description` 特性和默认异常文本缺少结束引号，且同文件中文说明大面积乱码；当前差异已恢复这些文本与字符串边界。
- 当前证据：当前 `Assets/Resources/Script/Data/WorkshopUpgrade.cs` Git diff；`CreateAssetMenu`、19 个 `Description` 和默认异常文本已恢复为可读中文，三处字符串边界闭合；`WorkshopEffectType` 成员及其显式整数未改变；`git diff --check` 通过。`TestResults/Latest-Test-Errors.txt` 在修复后执行 554 项 EditMode，低风险 CLI 编译生成 3 个程序集、0 错误、5 个既有序列化字段警告。
- 根因或设计诊断：一次错误的文本转码/替换把中文写回源码，同时破坏了三个字符串边界；现有测试报告与当前 HEAD 版本脱节。
- 为什么不符合 Kingdom 当前目标：当前里程碑要求先保证玩法正确性与 Unity 编译，编译阻断使所有运行时结论都无法复验。
- 推荐的最小解决方向：源码修复已按最小边界完成；后续只需以最终确认的 Unity 补丁版本取得当前工作树的真实编译、Console 与定义测试证据。
- 可选方案及取舍：可从提交前版本提取原中文后逐项对照；若原文不可恢复，可按枚举语义重写短说明，但必须审阅术语且不得改变序列化值。
- 明确不建议采用的方案：不建议删除 `Description` 特性掩盖错误，不建议借机重排或重编号 `WorkshopEffectType`。
- 涉及的系统、代码和资产：`WorkshopUpgrade.cs`、所有 Workshop 效果资产、编译和测试证据链。
- 前置依赖：先确认 `BUILD-P2-001` 的 Unity 补丁版本决策。
- 风险和可能回归：枚举值变化会错读既有 ScriptableObject；文本恢复不完整会继续向 UI 暴露乱码。
- 工作量：源码修复已完成；验证为 S。
- 是否可以交给实施智能体：仅剩验证，可在用户允许 Unity 后执行。
- 后续实施验收标准：保持所有枚举整数不变；指定 Unity 版本无编译错误；Workshop 定义/效果测试通过；Console 无序列化缺失或未知效果；新测试报告时间与 HEAD 一致。
- 尚缺的验证证据：按用户要求未执行编译，故未取得真实 Unity 编译、Console、EditMode 或 PlayMode 结果。

### SAVE-P0-001 Workshop 合法购买进度可能使主存档与备份均无法恢复

- 标题：Workshop 保存顺序与恢复前置校验顺序冲突
- 类型：存档 / 数据丢失 / Workshop
- 状态：已确认
- 优先级：P0
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：玩家购买了稳定 ID 字典序在其前置之前的 Workshop 升级，完成至少一次保存，随后载入；当主存档和 `.bak` 都已轮转到这类正常进度时风险最高。
- 玩家可感知症状：载入主存档失败后尝试备份；若备份也含相同合法购买集合，则系统记录错误并开始新游戏，玩家看到进度回退或全部丢失。
- 具体影响：`DataBase<WorkshopUpgrade>` 按稳定 ID 排序，`CaptureSaveData()` 依该顺序写 `PurchasedUpgradeIds`；`RestoreSaveData()` 却在单次顺序遍历中要求每个前置已经加入局部 `purchased` 集合。当前 64 条直接 Workshop 前置边中有 33 条前置 ID 位于依赖项之后，例如 `AdaptiveArmorRepairSystems <- InterstellarCombatSupplySystems`，合法集合因此会被当成损坏存档。
- 当前证据：`Assets/Resources/Script/Data/DataBase.cs:63-97`；`Assets/Resources/Script/Manager/WorkshopManager.cs:137-186`；`Assets/Resources/Script/Manager/SaveManager.cs:74-102`、`:179-228`；当前 `Assets/Resources/Datas/Workshop/*.asset` 和对应 `.meta` 的稳定 ID/GUID 交叉计数。
- 根因或设计诊断：保存格式表达的是无序购买集合，恢复实现却把列表顺序错误地当成拓扑顺序；备份轮转只能抵御最近一次文件损坏，不能抵御确定性的应用逻辑错误。
- 为什么不符合 Kingdom 当前目标：正常玩法可生成无法恢复的存档，直接违反稳定 ID 迁移、备份回退和可恢复进度的基础契约。
- 推荐的最小解决方向：未来恢复时先完成 ID 解析、去重、时代与研究校验，形成完整购买集合；再针对完整集合验证 Workshop 前置，最后一次性提交 State 和重建效果。不要依赖序列化顺序。
- 可选方案及取舍：也可在保存时输出稳定拓扑序，但读取端仍应接受任何无重复顺序，以兼容旧存档和外部 JSON 排序；只改写出端不足以修复现有文件。
- 明确不建议采用的方案：不建议删除前置完整性检查，不建议仅将 82 个资产改名来迎合字典序，也不建议静默丢弃无法按序恢复的升级。
- 涉及的系统、代码和资产：`DataBase<T>`、`WorkshopManager`、`SaveManager`、82 个 WorkshopUpgrade 资产及其前置图。
- 前置依赖：先修复 `COMPILE-P1-001`，并保留现有存档格式版本或提供明确兼容路径。
- 风险和可能回归：两阶段恢复若过早修改 State，异常时可能留下部分效果；必须保持失败回滚和重复加载幂等。
- 工作量：M。
- 是否可以交给实施智能体：可以，作为 Wave 1 的独立存档批次。
- 后续实施验收标准：对全部 82 项和每个可达购买前缀执行保存/载入往返；随机打乱合法 ID 顺序仍恢复同一集合；未知、重复、越时代和缺前置数据仍被原子拒绝；主/备份回退和连续两次载入不残留效果。
- 尚缺的验证证据：尚未在 Unity 中实际生成并载入主/备份文件；P0 定级来自当前代码与全部资产前置图可构造的确定性失败路径。

### RESEARCH-P1-001 研究资源成本不是开始前原子支付

- 标题：研究成本被分资源、分多次部分扣除
- 类型：研究 / 交易正确性 / 规则冲突
- 状态：已确认
- 优先级：P1
- 证据等级：E1
- 置信度：高
- 玩家发生场景：玩家对一个需要多种资源或当前库存不足的研究点击支付，或让队首研究等待资源。
- 玩家可感知症状：只要任一种所需资源有余额，按钮即可执行并扣走当前可用部分；研究仍未支付完成，资源已不可用于其他选择，后续只能继续补缴。
- 具体影响：`CanPayResearchCost()` 只检查“任意剩余成本且库存大于零”；`TryPayResearchCost()` 对每种资源取 `Min(remaining, available)` 后提交，并把结果记入 `PaidResourceCosts`。这把一次性选择变成不可撤销的分期沉没成本，也使队列、存档和不足反馈更难理解。
- 当前证据：`Assets/Resources/Script/Manager/ResearchManager.cs:326-410`、`:512-555`；`Assets/Tests/Editor/KingdomLogicTests.cs:1808-1833` 的 `ResearchCostPayment_IsIncrementalAndOnlyPaidOnceWithoutUi` 与 `:2057-2080` 的 `ResearchCostPayment_PaysAvailableResourcesIndependently` 明确固化部分扣款。
- 根因或设计诊断：资源管理器的单次扣款本身是原子的，但研究层先把不足成本裁成可用部分，错误地把“这次提交原子”当成“整项研究成本原子”。
- 为什么不符合 Kingdom 当前目标：锁定规则要求研究在开始前先完整验证全部资源、再统一提交；当前实现与测试同时违反该产品契约。
- 推荐的最小解决方向：未来在支付前汇总整项剩余成本并验证所有库存，完整可付时一次扣除并标记 `CostPaid`；不足时任何资源和 State 都不改变，同时返回包含全部缺口的反馈。
- 可选方案及取舍：对旧存档中的部分缴款，可在迁移时按稳定 ID 精确退款后采用新规则，或把既有缴款视为一次性历史信用；退款更符合无损迁移但需要容量仅对 Food 生效的精确处理。
- 明确不建议采用的方案：不建议保留部分扣款只修改按钮文案，也不建议自动从未来产出持续抽税直至付清。
- 涉及的系统、代码和资产：`ResearchManager`、`ResearchState.PaidResourceCosts`、`ResourceManager.TryApplyAtomicPayment`、研究队列、存档 DTO、研究详情 UI 和上述测试。
- 前置依赖：`COMPILE-P1-001`；确定旧部分付款存档的迁移/退款口径。
- 风险和可能回归：队首等待状态、无资源成本研究、重复点击、存档迁移和 Food 精确退款可能回归。
- 工作量：M。
- 是否可以交给实施智能体：可以，作为 Wave 1 的研究交易批次。
- 后续实施验收标准：任一资源不足时所有库存与研究支付 State 不变；全部满足时只扣一次；重复调用幂等；队列不会因付款隐式重排；旧部分缴款存档无资源丢失；更新后的 EditMode 与相关 PlayMode 通过。
- 尚缺的验证证据：本轮未运行 UI、队列、存档或 Unity 行为；当前测试源码证明的是错误契约被刻意实现，不代表目标规则通过。

### ECON-P1-001 多输入资源满足率被相乘而不是取瓶颈

- 标题：建筑多输入资源满足率采用乘积导致系统性过度惩罚
- 类型：生产 / 资源 / 效率
- 状态：已确认
- 优先级：P1
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：任何同时消耗两种或更多资源的建筑在一个 tick 内至少有两个输入未完全满足时。
- 玩家可感知症状：建筑效率低于最短缺输入能够支持的比例；补充一个非瓶颈输入也可能产生难以解释的非线性变化。
- 具体影响：两个输入各满足 50% 时，当前资源约束得到 25%，而共同生产批次能支持的瓶颈应为 50%；输入种类越多，误差指数式放大，并继续与幸福度、电力、物流等独立约束组合。
- 当前证据：`Assets/Resources/Script/Manager/BuildingManager.cs:1045-1066` 在遍历 `ResourceConsumptionRates` 时执行 `resourceSatisfaction *= GetTickSatisfaction(...)`；`docs/architecture/runtime-state.md` 的生产契约规定多输入满足率取最小值。
- 根因或设计诊断：把多个互为同一生产批次约束的输入当成独立概率相乘；这与资源管理器已经计算出的单输入满足率语义不一致。
- 为什么不符合 Kingdom 当前目标：当前里程碑先保证生产行为正确；错误的效率函数会污染生产链、离线进度和之后所有节奏调整。
- 推荐的最小解决方向：只把资源输入聚合改为从 1 开始逐项取 `Min`；保留全局效率、幸福度、电力和物流各自现有组合方式，避免扩大修改范围。
- 可选方案及取舍：若设计确实希望多重短缺叠乘，必须改写架构契约、UI 解释和模拟口径；但它会制造强烈非线性，不符合当前可理解反馈目标。
- 明确不建议采用的方案：不建议通过提高建筑基础产量或降低资产消耗来抵消算法错误，也不建议为每种输入增加特判。
- 涉及的系统、代码和资产：`BuildingManager.CalculateEfficiency`、`ResourceManager` tick 满足率、所有多输入 Building、离线模拟和建筑详情效率显示。
- 前置依赖：`COMPILE-P1-001`；在未来实现批次先锁定该函数的目标公式。
- 风险和可能回归：实际产量会提高，可能暴露此前被错误效率掩盖的下游瓶颈；必须在行为测试通过后才重新评估节奏。
- 工作量：S。
- 是否可以交给实施智能体：可以，作为 Wave 1 的独立正确性批次。
- 后续实施验收标准：0/1/多输入分别覆盖；多个满足率取最小值；普通资源和 Food 不为负；在线与离线同样结果；随后用当前 40/65/128/82 定义重跑闭包和模拟但不得用其替代 Unity 证据。
- 尚缺的验证证据：未运行真实 tick、离线恢复或 Unity 测试；多输入资产的实际玩家节奏影响待行为修复后重新测量。

### POP-P2-001 超容量人口去留在代码、测试和模拟口径间冲突

- 标题：住房拆除后的超容量人口没有统一产品契约
- 类型：人口 / 容量 / 文档矛盾
- 状态：强推断
- 优先级：P2
- 证据等级：E1 + E2 + E3
- 置信度：中高
- 玩家发生场景：玩家先获得住房人口容量与人口，再拆除或替换住房，使人口高于当前容量，同时仍能供应 Food。
- 玩家可感知症状：当前静态行为允许超容量人口长期保留并继续影响生产力；玩家可能把拆房保人口当成最优技巧，也可能无法理解容量为何不再约束已有居民。
- 具体影响：`PopulationState.AdvanceDeparture()` 实现了超容量离开，但当前仓库没有调用点；`HousingRemoval_AllowsOvercapacityWithoutCapacityRatchet` 又明确要求拆房后保留 14 人/9 容量。允许引用的模拟说明却把超容量离开作为恢复行为之一，三者无法同时成立。
- 当前证据：`Assets/Resources/Script/Runtime/PopulationState.cs:181-250` 及全仓调用搜索；`Assets/Tests/Editor/KingdomLogicTests.cs:688-718`；当前 `data/economy-simulation` 报告的人口行为说明。
- 根因或设计诊断：曾设计的离开路径被从主推进函数移除或从未接入，而测试把当前实现固化，模拟仍保留另一套规则；这是产品决策冲突，不能仅凭静态证据直接定为 Bug。
- 为什么不符合 Kingdom 当前目标：人口容量、Food 满载承载和生产力必须形成可理解的反馈环；未统一口径会让住房价值、拆除退款与节奏报告全部失真。
- 推荐的最小解决方向：先由产品负责人选择并写成单一契约。建议容量长期约束人口：保留短暂宽限/可见倒计时，超容量人口按受控速率离开；缺粮离开仍走独立路径。
- 可选方案及取舍：若明确允许永久拥挤，则应删除死的常规离开逻辑，在 UI 显示 `人口/容量` 超额及幸福度/增长后果，并让模拟和测试采用同一规则；代价是住房拆除可能成为无成本人口容量套利。
- 明确不建议采用的方案：不建议静默把人口瞬间 Clamp 到容量，不建议重新引入 workforce，也不建议只改模拟器掩盖运行时分歧。
- 涉及的系统、代码和资产：`PopulationState`、`BuildingManager` 住房建造/拆除、Food 满载模型、人口 UI、相关测试和模拟说明。
- 前置依赖：用户确认永久拥挤或受控离开哪一个是产品目标。
- 风险和可能回归：存档中的既有超容量人口、拆除回滚、离线长时间推进、Food 短缺和幸福度可能交互。
- 工作量：M。
- 是否可以交给实施智能体：产品决策后可以。
- 后续实施验收标准：在线/离线采用同一规则；拆房事务失败完全回滚；不瞬间丢人口；Food 满载公式可复算；UI 明确解释容量、宽限和离开原因；更新相反口径的测试与模拟说明。
- 尚缺的验证证据：尚未实际观察 Unity 中拆房后的长时间人口曲线；当前报告又使用过期定义快照，只能用于证明口径冲突，不能证明当前节奏。

### SECTOR-P1-001 本地殖民完成会清空并破坏并行星际战役状态

- 标题：本地殖民与星际战役可并行且共用全局完成操作
- 类型：Sector / 战役 / 跨系统状态
- 状态：强推断
- 优先级：P1
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：玩家在一个本星系 Sector 继续殖民，同时另一个远星 Sector 的战役已经活动并产生伤亡；本地殖民先完成。
- 玩家可感知症状：远星战役可能被悄然重置全局目标/伤亡后继续，舰队维修和战役状态显示不一致；之后保存的局部 Sector 伤亡与全局战役伤亡可不一致，下一次载入失败并触发存档回退。
- 具体影响：殖民入口不检查 `runtimeState.Campaign.Active`；`SimulationManager` 每 tick 先推进殖民再推进战役；`CompleteOccupation()` 无条件调用 `runtimeState.CompleteCampaign()`。同 tick 随后的活动战役会重新 `BeginCampaign`，但 Sector 已累计伤亡不会同步回全局，`ValidateCampaignState()` 在载入时要求两者严格相等。
- 当前证据：`Assets/Resources/Script/Manager/SectorManager.cs:495-627`、`:680-790`、`:1009-1037`、`:1279-1306`、`:1694-1703`；`Assets/Resources/Script/Manager/SimulationManager.cs:186-194`、`:300-308`；现有 `SectorManagerTests.cs` 没有殖民与远星战役并行用例。
- 根因或设计诊断：本地殖民复用了“占领完成”辅助函数，而该函数错误地拥有全局战役生命周期；两个独立活动槽缺少互斥或目标所有权判断。
- 为什么不符合 Kingdom 当前目标：战役应是经营系统的确定性结果，不能由另一条 Sector 活动清空全局状态，更不能把正常玩法导向不可恢复存档。
- 推荐的最小解决方向：把本地殖民完成与远星战役完成拆成两个明确提交路径；殖民完成不得触碰全局战役。另在开始/恢复/每 tick 验证“最多一个远星战役且全局目标与局部状态一致”。
- 可选方案及取舍：若产品只允许殖民或战役二选一，可在两类入口原子拒绝并给出 `CampaignInProgress`/`ColonizationInProgress`；实现更小但减少并行经营选择。
- 明确不建议采用的方案：不建议放宽存档伤亡一致性校验来掩盖状态分叉，也不建议在 UI 层单独禁用按钮而保留 Manager 可并发入口。
- 涉及的系统、代码和资产：`SectorManager`、`CampaignState`、`SectorState`、`SimulationManager`、Save DTO、Sector UI 与全部 9 个 Sector。
- 前置依赖：`COMPILE-P1-001`；明确殖民/战役是否允许并行。
- 风险和可能回归：取消、维修、失败回滚、离线 60 秒步进和旧存档中孤立活动状态需要兼容。
- 工作量：M。
- 是否可以交给实施智能体：可以，作为 Wave 1 的独立 Sector 状态批次。
- 后续实施验收标准：并行允许与禁止两种产品口径择一测试；殖民完成不改变其他战役；全局/局部目标、活动、伤亡和比率始终一致；在线/离线相同；保存/载入、备份、取消与维修往返无分叉。
- 尚缺的验证证据：本轮没有运行构造场景；需要未来 EditMode 精确反例与 PlayMode/Console 证据确认实际生命周期顺序。

### SIM-P2-001 Player 长帧积压已改为跨帧有界结算，待 Unity/P40 验证

- 标题：长帧保护把编辑器假设无条件应用到移动端 Player
- 类型：Simulation / 帧率一致性 / 移动端恢复
- 状态：待运行验证（修复已实施）
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：P40 Pro 上出现一次超过 `tickIntervalSeconds * maximumTicksPerFrame = 2s` 的主线程停顿，但系统没有派发完整的应用暂停/恢复生命周期。
- 玩家可感知症状：修复前，这段时间的生产、Food、人口、研究和战役全部不推进，且没有补算或提示；2.01 秒会比 1.99 秒少结算近两秒。当前实现应把超出单帧预算的部分延后结算，但尚无 Unity/真机证据。
- 具体影响：当前 `Update()` 只在 `UNITY_EDITOR` 中丢弃超过单帧预算的域重载假长帧；Player 会把真实 `frameDelta` 交给 `Advance()`。`Advance()` 每次仍最多执行 `maximumTicksPerFrame` 个 tick，但不再把剩余 accumulator 截到两秒，后续帧可用 `Advance(0)` 继续排空。
- 当前证据：当前 `Assets/Resources/Script/Manager/SimulationManager.cs` Git diff；`Assets/Tests/Editor/SimulationBudgetTests.cs` 已把旧“截断 backlog”断言替换为“输入五倍单帧预算、每帧只排空一份预算且最终不丢时间”的回归用例。该新断言在旧实现中首帧会得到一份预算而不是四份，能够命中已知缺陷。
- 根因或设计诊断：把“编辑器域重载假时间”与“Player 真实经过时间”混成同一保护分支。
- 为什么不符合 Kingdom 当前目标：`SimulationManager` 是唯一玩法时钟，帧率变化不应改变累计经济结果；目标设备上的耗电、热降频和恢复都要求可解释的时间口径。
- 推荐的最小解决方向：最小源码修复已实施；后续只需验证分段等价、暂停/恢复不双计、每帧 tick 上限和 P40 长帧恢复成本，不再扩大时钟设计。
- 可选方案及取舍：可把超过阈值的部分排入跨帧 backlog，避免单帧尖峰；结果稍后收敛但不会丢时。也可直接走离线 60 秒步进，需防止与 `OnApplicationPause` 双计。
- 明确不建议采用的方案：不建议单纯提高 `maximumTicksPerFrame`，这会放大卡顿；不建议把 `Time.deltaTime` 换成缩放时间破坏暂停语义。
- 涉及的系统、代码和资产：`SimulationManager`、`SaveManager`、全部在线/离线玩法系统、P40 生命周期。
- 前置依赖：真实 Unity 编译与目标 EditMode；P40 生命周期验证仍独立待办。
- 风险和可能回归：双计离线时间、恢复帧尖峰、战役 60 秒成本事务和研究完成事件可能回归。
- 工作量：源码与回归测试已完成；运行验证为 M。
- 是否可以交给实施智能体：不再申请智能体；由当前主任务继续验证。
- 后续实施验收标准：0.1、1.99、2.01、10 秒分段与等总时长小步结果在约定误差内一致；暂停恢复只结算一次；积压跨帧有上限且 Console 明示；P40 后台/前台实测不丢时、不双计。
- 尚缺的验证证据：真实 Unity 编译/目标 EditMode 尚未取得；未在真机制造长帧、热降频或后台恢复，性能成本和 Unity 生命周期顺序仍待验证。

### UI-P2-001 研究支付 blocker、直接前置、逐项资源缺口与结构化效果已可见

- 标题：研究详情同时解释直接前置、支付阻碍、逐项缺口和实际效果
- 类型：UI / 研究反馈
- 状态：已解决（源码与 CLI 编译完成，待 PlayMode/P40 验证）
- 优先级：P2
- 证据等级：E1
- 置信度：高
- 玩家发生场景：玩家选中一个尚未解锁、资源管理器未就绪或当前无法支付的研究。
- 玩家可感知症状：修复前按钮不可用但只显示“支付资源”，自由文本描述之外也看不到直接前置或结构化效果；现在正文先列每个直接前置研究及其当前状态，再显示效果类型、目标与数值，按钮和需求行分别解释支付 blocker 与“缺 X / 可支付 / 已支付”。
- 具体影响：`AppendResearchPrerequisites()` 直接读取 `Research.Prerequisites` 和现有 `ResearchState.Status`；它不把前置状态错误并入支付 blocker，因为当前契约和测试允许为同时代的指定后续研究提前付款。逐项缺口仍使用 `max(0, 需求-已支付-库存)`；`AppendResearchEffects()` 仍直接读取真实效果。`RESEARCH-P1-001` 的支付规则是独立问题，本条不宣称已修复。
- 当前证据：当前 `KingdomUIRoot.DetailPanel.cs` 的 `AppendResearchPrerequisites()`、`ConfigureResearchPaymentButton()`、`FormatRequirementAmount()` 与 `AppendResearchEffects()`；`KingdomLogicTests.PayResearchCost_OnlyPaysTheSpecifiedResearch` 明确覆盖提前支付指定后续研究的现有契约；三个 C# 项目 CLI 编译 0 错误。
- 根因或设计诊断：原先两个相互排斥的按钮文案分支被写成连续赋值；需求行只显示已支付/需求/库存，详情正文只依赖自由文本和图形连线，没有提供移动端可读的直接前置、缺口与效果摘要。
- 为什么不符合 Kingdom 当前目标：移动端详情必须说明禁用原因和解决途径，研究是时代推进的核心入口。
- 推荐的最小解决方向：最小修复已完成：直接前置和效果追加在同一详情正文，按钮保留支付 blocker，完整缺口留在现有可滚动需求列表；不新增状态模型或第二个面板。
- 可选方案及取舍：也可把全部缺口塞进按钮，但会产生过长触控标签；当前“短 blocker + 逐项列表”更适合移动端。
- 明确不建议采用的方案：不建议只依赖颜色或 Console；不建议把英文内部 blocker 原样当最终中文产品文案。
- 涉及的系统、代码和资产：研究详情、`Research.Prerequisites`、`Research.Effects`、`ResearchManager.CanPayResearchCost`、需求列表、ResearchNode/Queue 入口。
- 前置依赖：无；未来 `RESEARCH-P1-001` 改为完整原子支付后仍须回归已支付/剩余口径。
- 风险和可能回归：多前置/多效果研究使正文变长、长数字文本溢出、详情滚动位置，以及旧存档已有部分付款时的显示；均未改变前置、效果或支付 State。
- 工作量：S，源码完成。
- 是否可以交给实施智能体：否，当前工作树已实施；仅剩验证。
- 后续实施验收标准：直接前置行数、名称和状态与定义/State 一致；提前支付指定后续研究的既有契约不变；每个禁用原因均有中文可见文本；多资源研究逐项显示正确缺口；效果行数、类型、目标和值与定义一致；P40 长文本可滚动且不截断。
- 尚缺的验证证据：未运行包含多前置、多资源、多效果研究详情的 PlayMode，未截图或触控验证 P40 长文本；最新 EditMode 报告早于本轮 UI 文本改动。

### WORKSHOP-P2-001 Workshop 已购项与直接下一层路线现可回看

- 标题：Workshop 列表以渐进前沿同时展示购买历史、根节点和直接下一层
- 类型：Workshop / 进度可见性 / UI
- 状态：已解决（静态实现与 CLI 编译通过，待 Unity/P40 验证）
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：玩家首次进入 Industrial Workshop、规划后续升级，或回看已经购买的效果与依赖。
- 玩家可感知症状：修复前购买后该行立即消失，未满足前置的升级也完全不可见。现在已进入时代范围内的已购项、无 Workshop 前置的根节点，以及所有直接 Workshop 前置均已购买的下一层会保留在列表；行内显示首个真实 blocker，打开详情后还能看到全部直接研究前置和工坊前置及其当前状态。
- 具体影响：`ShouldRevealWorkshop()` 实现按时代和直接 Workshop 前置展开的渐进前沿，不会一次铺出全部 82 项；`GetWorkshopAvailability()` 返回首个真实阻碍及具体资源缺口，`AppendResearchPrerequisites()` 和 `AppendWorkshopPrerequisites()` 则直接读取同一前置集合及 Manager State 展开完整说明。购买刷新继续复用 Workshop 行池，避免路线展开时反复销毁/实例化整批卡片。
- 当前证据：当前 `KingdomUIRoot.AuthoredRows.cs` 的 `BuildAuthoredWorkshopRows()`、`ShouldRevealWorkshop()`，`KingdomUIRoot.DetailPanel.cs` 的两个前置摘要、`GetWorkshopAvailability()` 和结构化效果，`KingdomUIRoot.LiveRefresh.cs` 的研究/Workshop 脏标记，以及 `KingdomUIRoot.cs` 的行池与复用清理门；三个 C# 项目 CLI 编译均为 0 错误。当前 Workshop 为 Industrial 36、Spacer 46，全部有真实效果和 64 条直接升级前置。
- 根因或设计诊断：把“当前可操作集合”误当成“玩家需要理解的进度前沿”；直接展示全部 82 项会制造移动端密度和刷新成本，而只给首个 blocker 又不足以规划完整投资，因此采用“列表渐进前沿 + 详情完整直接前置”的两层信息结构。
- 为什么不符合 Kingdom 当前目标：Workshop 必须形成有意义的第二发展路线；隐藏未来和历史会把选择退化为被动购买。
- 推荐的最小解决方向：源码实现已经达到最小范围；下一步只运行验证渐进揭示、完整前置状态、真实 blocker、购买后展开和行池复用，不继续增加第二套 Workshop 树。
- 可选方案及取舍：若运行验证仍显示列表过密，可再考虑时代折叠或“仅可购买”筛选；当前不预先增加筛选状态和维护成本。
- 明确不建议采用的方案：不建议复制一套 Workshop 树或新增新货币，不建议继续以隐藏作为锁定反馈。
- 涉及的系统、代码和资产：Workshop 页面、`WorkshopManager`、82 个 WorkshopUpgrade、建筑卡片 Prefab 与详情面板。
- 前置依赖：`COMPILE-P1-001` 已解除语法阻断；`SAVE-P0-001` 的恢复缺陷仍需独立修复，不由 UI 隐藏或吞掉合法状态。
- 风险和可能回归：研究完成或购买后路线未及时展开、详情前置状态未刷新、复用行保留旧监听/旧定义、滚动跳动，以及 P40 多前置正文和当前沿较宽时的密度。
- 工作量：S～M，源码完成。
- 是否可以交给实施智能体：否，当前任务不调用智能体；主任务继续分批实施。
- 后续实施验收标准：购买后原条目仍显示“已拥有”并可打开详情；根节点与直接下一层按规则出现；详情列出的研究/Workshop 前置数量、名称和状态与定义/State 一致；每项首个阻碍顺序与 `TryPurchase` 一致；资源不足按钮打开详情；刷新不重复创建已有行或叠加监听；滚动位置稳定；P40 触控与列表性能通过。
- 尚缺的验证证据：未在 Unity 观察购买/研究前后的实际行数、详情前置状态、池大小、滚动、监听和“查看缺口”交互；未执行 P40 真机验收。

### TUTORIAL-P2-001 教程被动跳步已增加按步骤页面访问门，待运行验证

- 标题：教程从“状态满足即推进”改为“正确页面已访问且状态满足才推进”
- 类型：教程 / 新玩家旅程 / 状态触发
- 状态：待运行验证（首批修复已实施）
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：新游戏运行 10 秒后，起始木材库存已经为正；旧实现会在后续概览刷新中自动跳过 Resources。当前实现要求玩家在 Resources 成为活动步骤后打开资源页，返回概览后才允许原状态公式推进。
- 玩家可感知症状：修复前 Resources 可能只存在一个刷新周期；当前理论行为是每个步骤至少停留到玩家进入该步骤指定页面。错误页面不计入，前一步访问不会继承给下一步。
- 具体影响：原有 `calendar-days>=1`、库存、建筑拥有、人口、研究完成、生产链和时代条件一行未改；`AdvanceCompletedSteps()` 只新增 `HasVisitedPageForStep(step)` 前置。页面访问按活动步骤 ID 记录，新游戏/载入时清空，不新增存档字段或经济事件。
- 当前证据：当前 `TutorialManager.RecordPageVisited/HasVisitedPageForStep/AdvanceCompletedSteps`；`KingdomUIRoot.SetPage` 在真实页面切换时登记；`RefreshDevelopmentGuidance` 补登记早于 TutorialManager 启动的初始 Overview；`TutorialStepRequiresVisitToItsCurrentNavigationPage` 断言错误页不计入、正确页计入、下一步不继承且新游戏清空。三个 C# 项目均 0 编译错误。
- 根因或设计诊断：旧实现只有玩法 State 条件，没有“玩家已经看过该步骤入口”的最小交互事实；因此已有库存或旧进度会被误当成学习动作。
- 为什么不符合 Kingdom 当前目标：前 1/10 分钟必须建立资源、建造、研究和恢复路径的理解；自动跳步直接削弱当前纵向切片的可玩性证据。
- 推荐的最小解决方向：当前最小页面门已经实施，先取得真实新游戏 PlayMode/观察证据；若仍出现“打开页面即跳过但没有理解动作”，再只为对应步骤增加成功命令事件，不先建立通用教程事件总线。
- 可选方案及取舍：页面访问门允许已有进度在玩家主动查看后确认通过，兼容旧存档且无需迁移；逐命令事件更严格，但会侵入 Building/Research 等多个成功提交路径。
- 明确不建议采用的方案：不建议用更长固定计时器延迟自动跳过，不建议让 UI 直接修改玩法 State 来完成教程。
- 涉及的系统、代码和资产：`TutorialManager`、8 个 TutorialStepDefinition、概览指导卡、页面导航、存档 Tutorial DTO。
- 前置依赖：源码首批修复无经济依赖；真实研究步骤最终理解仍依赖研究支付反馈。
- 风险和可能回归：载入后访问记录有意清空，未完成步骤需要重新打开一次对应页面；必须验证初始化顺序、重复加载、Era 页即时 Evaluate 和已完成集合幂等。
- 工作量：首批源码 S，已完成；运行验收与可能的逐命令增强为 M。
- 是否可以交给实施智能体：不再申请智能体；由当前主任务继续验证和收敛。
- 后续实施验收标准：Orientation 在 Overview 可见至少到日历条件；Resources 未访问时不推进；错误页面不推进；正确导航抵达目标页；下一步骤不继承访问；载入后可重新访问且完成集合幂等；PlayMode 真实驱动 UI 而非只反射辅助方法。
- 尚缺的验证证据：新增 Editor 断言尚未由用户的 Unity Test Runner 执行；没有当前 PlayMode/XML 或新游戏实录证明实际刷新与导航顺序。

### TEST-P2-001 ResearchTree 专项验收门槛与当前内容及测试断言不一致

- 标题：规则要求 79 个唯一节点，而当前定义和测试动态目标为 128
- 类型：测试证据 / ResearchTree / 文档矛盾
- 状态：已确认
- 优先级：P2
- 证据等级：E1
- 置信度：高
- 玩家发生场景：后续团队试图用专项 PlayMode 宣称研究图在当前内容下已经验收。
- 玩家可感知症状：不是直接玩家症状；错误验收可能把缺节点、零尺寸、无滚动或不可拖动的研究图带入构建。
- 具体影响：作用域规则仍要求专项日志报告 79 个唯一节点；当前有 128 个 Research，测试用 `DataBase<Research>.All.Count` 动态等待/断言全部节点。测试没有把 viewport/content 明确断言为正尺寸，且只有检测到 vertical overflow 时才要求拖动移动，因此“无溢出、无移动”也可能通过；当前 Mode 0 又没有 PlayMode 用例证据。
- 当前证据：根 `AGENTS.md` 的 Research UI 验收条款；`Assets/Tests/PlayMode/KingdomPlayModeTests.cs:392-570`；当前 128 个 Research 资产；`TestResults/Latest-Test-Errors.txt`。
- 根因或设计诊断：内容扩张后，治理文档的固定节点数未同步；专项测试把关键验收条件写成条件式诊断而非强制成功标准。
- 为什么不符合 Kingdom 当前目标：研究页是主要页面，测试必须证明当前目标节点集、正边界和真实移动，不能仅证明测试源码存在。
- 推荐的最小解决方向：先决定当前图应展示全部 128 还是明确的 79 项纵向切片；同步唯一权威计数，并把正 viewport/content、预期溢出轴、实际空白/节点拖动位移及拓扑计数写成不可跳过的断言。
- 可选方案及取舍：采用动态 128 能随内容增长，但应另有受审阅的当前快照/最小数量防止资产静默消失；固定 79 更稳定但与现有 128 定义冲突。
- 明确不建议采用的方案：不建议删除节点来让旧数字变绿，不建议只改日志文本或用反射直接调用手势替代真实 EventSystem 触控验收。
- 涉及的系统、代码和资产：根规则、128 个 Research、`KingdomUIRoot.ResearchTree`、`UIResearchGraphGesture`、PlayMode 专项测试和报告保存流程。
- 前置依赖：用户确认研究图当前目标节点集合。
- 风险和可能回归：动态内容增长、隐藏节点策略、横/纵溢出预期和测试时长。
- 工作量：S～M。
- 是否可以交给实施智能体：决策后可以，作为 Wave 0 证据批次。
- 后续实施验收标准：目标计数只有一个口径；节点数=唯一格数=目标数；viewport/content 两轴均为正；预期溢出轴为真且两种拖动实际移动；短按仍点击；拓扑决定与重复/反向/倒置计数被断言并保留日志/XML。
- 尚缺的验证证据：当前没有该专项测试的成功 XML/日志，也没有 128 节点在 Unity 中的实际布局数据。

### UI-P2-002 ResearchTree 的拓扑可用性判定结果被计算后忽略

- 标题：拓扑候选即使被判定不可用也仍被返回
- 类型：ResearchTree / 布局正确性
- 状态：强推断
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：当前或未来研究前置图使拓扑布局出现重复格、反向边或倒置交叉，`topologyUsable=false`。
- 玩家可感知症状：节点或连接线可能重叠、反向、穿过中间节点；日志却仍写 `layoutSource=topology-integer-grid`，无法解释所谓 fallback。
- 具体影响：`CreateResearchTreePositions()` 计算并记录 `topologyUsable`，但无论真假都返回 `topologyPositions`；注释声称不可用时使用参考资产网格，当前 Research 定义又没有任何序列化网格坐标字段，所以静态上不存在可执行 fallback。
- 当前证据：`Assets/Resources/Script/UI/KingdomUIRoot.ResearchTree.cs:1083-1149`；`Research.cs` 与 128 个 Research 资产无布局坐标；同文件日志始终声明 topology source。
- 根因或设计诊断：早期 fallback 设计被移除或未完成，留下一个无效布尔结果和误导日志。
- 为什么不符合 Kingdom 当前目标：规则要求拓扑决定和重复/反向/倒置数量可解释，且若使用资产 fallback 必须能由这些计数说明；当前实现不能满足。
- 推荐的最小解决方向：不要先增加复杂布局器。未来先运行当前专项取得真实计数；若候选全部为零，移除虚假的 fallback 叙述并把零计数变成硬断言；若不为零，只实现一个明确、可测试的保守 fallback 或修正当前整数拓扑算法。
- 可选方案及取舍：给每个 Research 新增资产网格可获得策划控制，但会给 128 项增加迁移和维护成本；在当前纵向切片不应作为默认方案。
- 明确不建议采用的方案：不建议继续记录 `accepted=false` 后照常使用，也不建议通过空白行制造“看似不重叠”的滚动空间。
- 涉及的系统、代码和资产：研究布局、连接线总线、ResearchTreeSK 参考、Research 资产、专项 PlayMode。
- 前置依赖：`TEST-P2-001` 的目标节点口径与真实运行计数。
- 风险和可能回归：坐标转换、时代分区、滚动边界、连接线缓存和构建性能。
- 工作量：S（若当前计数全零，仅收紧契约）/L（若需要真实 fallback）。
- 是否可以交给实施智能体：先取得运行日志后再交付。
- 后续实施验收标准：`accepted` 与实际返回 source 一致；false 分支有真实实现或明确失败；节点/线共用一次顶左到 Unity 底左转换；专项断言重复、反向和倒置计数符合决策。
- 尚缺的验证证据：本轮未取得当前 128 节点的 runtime topology counts，不能断言玩家已经遇到重叠。

### UI-P2-003 P40 原生参考下关键触控与研究文本几何过密

- 标题：48～50 像素控件和无换行溢出文本不适合 P40 横屏触控
- 类型：移动端 UX / 触控目标 / 文本密度
- 状态：强推断
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：中高
- 玩家发生场景：在 2640×1200 P40 Pro 横屏上点击播放/上一首/下一首或研究节点，阅读长中文研究名并拖动图。
- 玩家可感知症状：小按钮和仅 50 高的节点容易误触；长研究名可能溢出节点并与相邻节点/连接线竞争，拖动与短按的容错区域很窄。
- 具体影响：CanvasScaler 在目标分辨率下 Match Width，参考像素基本按原生几何呈现；Pause/Previous/Next 为 48×48，研究节点 205×50、纵向网格间距 60。标题区只有节点上方 66% 高度，字体 24、不换行、`Overflow`；当前存在“星际占领行政体系”等长标签。根 Prefab 还有 16/18/20 字号的时间/音量文本。
- 当前证据：`KingdomUIRoot.cs:209-221`；`KingdomUIRoot.ResearchTree.cs:13-16`、`:1916-1934`、`:1985-2023`；`Assets/Resources/UI/Kingdom/KingdomUIRoot.prefab` 和 `KingdomUIResearchNode.prefab` 的 RectTransform/TMP 字段。
- 根因或设计诊断：ResearchTreeSK 桌面参考几何与高像素密度手机直接一比一复用，没有建立移动端最小命中区和长文本策略。
- 为什么不符合 Kingdom 当前目标：P40 横屏是唯一目标设备，触控命中、字号和文本完整性优先于在一屏塞入更多节点。
- 推荐的最小解决方向：未来以透明 hit target 扩大关键按钮/节点命中区，不必先放大全部视觉；研究标题采用受控缩放、两行或截断+长按详情之一，并保留短按/拖动阈值。
- 可选方案及取舍：整体放大节点会增加内容尺寸和滚动距离；仅扩大命中区成本更小，但文字仍需单独处理。
- 明确不建议采用的方案：不建议只调低所有字体，不建议让 Overflow 文本越过节点，也不建议用颜色变化代替命中反馈。
- 涉及的系统、代码和资产：CanvasScaler、音乐控件、ResearchNode、GraphGesture、Tooltip、TMP 字体与 P40 SafeArea。
- 前置依赖：真实 P40 截图、触控和字体渲染数据；`TEST-P2-001` 的专项门槛。
- 风险和可能回归：扩大 hit target 后节点间命中区重叠，双指缩放和拖动手势可能被按钮吞掉。
- 工作量：M。
- 是否可以交给实施智能体：可以先做测量/原型，最终值需真机验收。
- 后续实施验收标准：所有主要命中区达到确认后的物理尺寸；相邻 hit target 不重叠；长标签无跨节点覆盖；节点短按、节点起拖、空白拖动和双指缩放互不冲突；P40 截图与手工触控清单通过。
- 尚缺的验证证据：没有截图、实际 DPI 命中测量、色觉/字体可读性测试或 P40 真机结果。

### TEXT-P2-001 “人类文明”路线图与“鼠托邦/鼠族”运行时身份冲突

- 标题：核心文明物种与产品名称没有单一口径
- 类型：产品定位 / 文本 / 教程
- 状态：已确认
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：新游戏进入概览、阅读教程文明语境、查看王国名称和跨时代叙事。
- 玩家可感知症状：默认王国叫“鼠托邦”，教程反复描述“鼠族复兴”；当前产品路线图和主审查定位却说玩家管理一群人类。玩家无法判断这是鼠族文明游戏还是人类文明游戏。
- 具体影响：身份冲突跨越默认存档值、7 个 Tutorial NarrativeText、`GetCivilizationContext()` 全时代文本与多条指导语；资源/建筑/研究主体多为通用人类工业术语，进一步放大不一致。
- 当前证据：`Assets/Resources/Script/Manager/GameManager.cs:19`、`Runtime/GameState.cs:9`；`TutorialManager.cs:296-315` 及多处“鼠族”；`Assets/Resources/Datas/Tutorial/*.asset`；`docs/content/progression-roadmap.md` 的“玩家管理一群人类”。此前 `Research/Spacer/FirstContact.asset` 还单独写“人类与星际文明”，现已中性化为“王国与星际文明”，不借此代替产品正典决策。
- 根因或设计诊断：旧产品主题文本仍是运行时默认，而较新的内容路线图改成了人类文明，没有完成产品层决策与术语迁移。
- 为什么不符合 Kingdom 当前目标：时代特色、美术、教程语气、外星战争和品牌都依赖玩家文明身份；在身份未定前继续写内容会成倍返工。
- 推荐的最小解决方向：由用户选择“人类”或“鼠族”为唯一正典。依据当前路线图，默认建议人类；未来只迁移玩家可见名称、教程和描述，稳定 ID、存档结构与系统命名保持不变。
- 可选方案及取舍：若选择鼠族，应反向更新产品路线图、美术方向和所有“人类”叙述；主题辨识度更强，但需要证明现有工业/太空美术与物种设定一致。
- 明确不建议采用的方案：不建议把两者解释成未铺垫的同义词，也不建议重命名稳定 ID 或清空旧存档来完成文案迁移。
- 涉及的系统、代码和资产：GameState/GameManager 默认名、Tutorial、概览/时代页、后续剧情、美术与市场文案。
- 前置依赖：用户产品身份决策。
- 风险和可能回归：旧存档自定义名称、翻译、教程存档与已有品牌素材。
- 工作量：M。
- 是否可以交给实施智能体：决策后可以，作为 Wave 2 的文本迁移批次。
- 后续实施验收标准：玩家可见文本全仓术语扫描无冲突；新游戏、旧存档、教程八步和各时代语境采用同一身份；稳定 ID/GUID 不变；中文截图审阅通过。
- 尚缺的验证证据：没有当前产品品牌说明或用户最终决策；静态证据只能证明冲突存在。

### TEXT-P3-002 教程生产链摘要的乱码箭头与错误端点已修复

- 标题：生产链推荐文本已恢复箭头，并显示下游建筑的实际产物
- 类型：文本编码 / 教程反馈
- 状态：已解决（静态修复，待字体渲染验证）
- 优先级：P3
- 证据等级：E1
- 置信度：高
- 玩家发生场景：教程进入 ProductionChain，玩家已经拥有一座输出资源与另一座消费该资源的建筑。
- 玩家可感知症状：最初推荐行动的箭头乱码；箭头恢复后，原料生产建筑没有资源输入时，算法仍可能把共享中间资源同时作为两端，显示成“粗石 → 粗石”。当前资源加工链显示为“粗石 → 石砖”一类真实转换。
- 具体影响：`TryFindOwnedProductionChain()` 的布尔判定、建筑拥有条件和教程完成语义均未改变；只把摘要端点从“上游输入或共享资源 → 共享资源”改为“下游实际消耗资源 → 下游实际产物”。
- 当前证据：当前 `Assets/Resources/Script/Manager/TutorialManager.cs` Git diff；`TutorialManagerTests.ProductionChainRequiresMatchingOutputAndInputAcrossBuildings` 现在精确断言中间资源与目标产物；Runtime、PlayModeTests、Editor 三个 C# 项目均 0 编译错误。
- 根因或设计诊断：Unicode 箭头曾被错误转码；端点选择又描述了上游而不是被检测到的下游转换，导致无上游输入时两端退化为同一资源。
- 为什么不符合 Kingdom 当前目标：教程应直接解释生产链，乱码让玩家误判资源关系。
- 推荐的最小解决方向：源码和精确回归测试已完成；后续只需由用户复跑 EditMode，并在 Unity 中验证当前 TMP 字体、真实已拥有生产链与概览换行。
- 可选方案及取舍：可改成“原木 生产 石砖”以适配读屏；更长但语义更明确。
- 明确不建议采用的方案：不建议用图标替代全部文本而不提供语义标签。
- 涉及的系统、代码和资产：TutorialManager、TutorialManagerTests、概览指导文本、共享 TMP 字体。
- 前置依赖：无剩余源码依赖；仅待字体渲染验证。
- 风险和可能回归：字体缺字、文本宽度增加；只消费资源且没有资源产物的下游建筑仍沿用共享资源作为摘要回退，不影响完成判定。
- 工作量：源码修复已完成；验证为 S。
- 是否可以交给实施智能体：仅剩 Unity/UI 验证。
- 后续实施验收标准：精确端点 EditMode 通过；至少一条真实生产链在概览显示可读方向；中文字体无缺字方块；教程完成状态与修复前相同。
- 尚缺的验证证据：新回归测试尚未由 Unity Test Runner 执行，且未在 Unity 渲染该字形。

### DOC-P2-001 当前文档仍要求 workforce，与锁定规则直接冲突

- 标题：内容路线图和数值基线保留已否决的劳动力系统
- 类型：文档矛盾 / 治理风险
- 状态：已确认
- 优先级：P2
- 证据等级：E1
- 置信度：高
- 玩家发生场景：不直接发生在玩家端；后续内容/经济实施者按较低优先级文档设计时代闭环时。
- 玩家可感知症状：若误用，可能重新出现 AssignedWorkforce、劳动力容量或与现有生产力重复的门槛。
- 具体影响：`progression-roadmap.md` 写“组织：劳动力”和“人口提供劳动力”；`balance-model.md` 仍给出 `TotalWorkforce - AssignedWorkforce`，而根规则明确“不得重新引入 workforce；人口和生产力是当前玩家可见系统”。
- 当前证据：上述两个 docs 文件；根 `AGENTS.md` 非协商规则；当前代码使用 `ProductivityGrantedPerPerson`、`productivityConsumption`，没有 workforce State。
- 根因或设计诊断：Runtime/内容规则迁移完成后，旧设计文档术语未同步。
- 为什么不符合 Kingdom 当前目标：文档是后续智能体的重要输入，冲突会诱发不必要的大规模回退重构。
- 推荐的最小解决方向：未来治理批次把 workforce 表述改为“人口→生产力”和 `TotalProductivity - UsedProductivity`，并保留历史决策说明；不改运行时代码。
- 可选方案及取舍：可在文档顶部加显式 superseded 提示，修改更小但正文仍可能被片段引用。
- 明确不建议采用的方案：不建议为让文档“正确”而重新实现 workforce。
- 涉及的系统、代码和资产：两个 docs、人口、生产力、Building 字段、未来内容提示词。
- 前置依赖：无；本轮写入范围禁止直接修文档。
- 风险和可能回归：术语替换必须保留领土与生产力是不同门槛的含义。
- 工作量：S。
- 是否可以交给实施智能体：可以，作为 Wave 0 文档治理批次。
- 后续实施验收标准：当前 docs 全仓搜索不再把 workforce 当现行机制；示例公式与当前 State/Manager 名称一致；规则优先级明确。
- 尚缺的验证证据：无运行证据需求；这是直接文本冲突。

### AUDIO-P2-001 音乐资产缺少仓库内授权与署名证据

- 标题：18 首音乐无法从仓库证明可发行权利
- 类型：音频 / 发行风险 / 产品完整度
- 状态：强推断
- 优先级：P2
- 证据等级：E1 + E4
- 置信度：中高
- 玩家发生场景：项目对外分发、商店审核、宣传录屏或直播使用当前音乐。
- 玩家可感知症状：玩家端未必立即可见；发行方可能面对下架、静音、版权申诉或不得商用的问题。
- 具体影响：仓库没有音乐 license/credits/attribution 台账；文件名包含特定作品/角色/表演者线索，例如 `LullabyOfWoeAshleySerena.ogg`、`ThrumboRest.ogg`、`ThrumboWalkAround.ogg`，但文件名既不能证明侵权，也不能证明授权。
- 当前证据：`Assets/Resources/Musics` 的 18 个音频与 `.meta`；全仓当前材料只在本审查中提到授权问题，没有可核对的许可证、来源 URL、采购单或署名要求。
- 根因或设计诊断：音频进入仓库时没有随资产保存来源与发行权证据。
- 为什么不符合 Kingdom 当前目标：产品完整度包括可安全分发的音画；静态审查不能把“能播放”当作“有权发行”。
- 推荐的最小解决方向：用户先提供项目外授权台账；未来逐首记录来源、作者、许可证版本、商用/改编/署名条件和证明位置。无法证明者进入替换清单，不在当前玩法批次混改。
- 可选方案及取舍：全部替换为自制/委托/明确商用许可音乐最稳妥但成本高；逐首清权保留风格成本低但依赖外部证据。
- 明确不建议采用的方案：不建议凭文件名直接宣布侵权或直接删除素材，也不建议只在最终 credits 写名字而不验证许可。
- 涉及的系统、代码和资产：18 个音乐文件、MusicManager、Music 页面、发行包和 Credits。
- 前置依赖：用户提供授权/来源台账或确认接受替换。
- 风险和可能回归：替换会改变时长、音量、循环间隔、风格与包体；旧曲名 PlayerPrefs 未直接持久化但需复核。
- 工作量：S（清点）/L（全部替换与验收）。
- 是否可以交给实施智能体：台账可交付；权利判断仍需用户/法务确认。
- 后续实施验收标准：每首曲目有可审计权利记录；要求的署名出现在产品；不允许发行的文件不进构建；音量与播放控制回归通过。
- 尚缺的验证证据：仓库外许可、法务结论和实际发行地区要求。

### AUDIO-P2-002 音乐目录为 160.37 MiB 且运行时没有真实分类

- 标题：移动端音乐包体较大，全部曲目统一标成 `All Music`
- 类型：音频 / 包体 / 信息架构
- 状态：已确认
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：下载/更新 P40 构建、打开音乐页、按时代或氛围寻找曲目。
- 玩家可感知症状：安装包和更新成本偏高；音乐页“分类”列对 18 首歌全部显示相同值，不能传达时代或氛围。
- 具体影响：14 MP3 + 4 OGG 的源压缩总量为 168,161,600 字节（160.37 MiB）；`Resources.LoadAll<AudioClip>` 建目录并给每首 `Category="All Music"`。Importer 使用 Streaming/后台载入且不预载音频数据，降低常驻内存风险，但不消除构建体积与扁平分类。
- 当前证据：`Assets/Resources/Musics` 文件尺寸及 `.meta`；`Assets/Resources/Script/Manager/MusicManager.cs:67-117`；Music 页面显示 `track.Category`。
- 根因或设计诊断：音乐被当成一个 Resources 文件夹目录，没有产品级元数据、时代映射或移动端体积预算。
- 为什么不符合 Kingdom 当前目标：P40 是固定目标，包体和页面信息都应可验收；音频风格也应支持时代辨识而不是文件列表。
- 推荐的最小解决方向：在授权清点后先删出/替换无权或低价值曲目，再为保留曲目建立最小静态目录元数据（显示名、时代/氛围、顺序）；不要先做复杂动态音乐系统。
- 可选方案及取舍：保留全部曲目并改为远程/可选下载可降首包，但引入网络、缓存和失败状态，超出当前纵向切片；不推荐现在实施。
- 明确不建议采用的方案：不建议只进一步压低质量掩盖 160 MiB 目录，也不建议扩展复杂自适应配乐 AI。
- 涉及的系统、代码和资产：MusicManager、18 个音频、Music UI、Android 构建体积。
- 前置依赖：`AUDIO-P2-001` 清权结果和目标包体预算。
- 风险和可能回归：流式读取、首播延迟、寻轨、间隔、音量一致性和曲目缺失处理。
- 工作量：M。
- 是否可以交给实施智能体：可在清权后交付。
- 后续实施验收标准：记录最终 APK/AAB 音频贡献；每首曲目有非空可信分类；空目录/缺文件仍稳定；P40 首播、切歌、暂停、seek 和后台恢复通过。
- 尚缺的验证证据：本轮未构建 Android 包、测量安装体积、I/O、内存、耗电或实际音量。

### ASSET-P3-001 至少 44 张无引用遗留 PNG 仍位于 Resources

- 标题：龙、Golem 与异种宝石素材没有当前引用但会进入 Resources 管理范围
- 类型：美术资产 / 包体 / 遗留内容
- 状态：已确认
- 优先级：P3
- 证据等级：E1
- 置信度：高
- 玩家发生场景：构建移动端包、开发者浏览可加载资源，或未来用宽泛 Resources 路径误加载素材。
- 玩家可感知症状：当前没有证据表明这些图会出现在界面；潜在影响是额外包体、错误风格素材被误用和资产检索噪声。
- 具体影响：`Texture/Pawn` 24 张龙/Golem 和 `Texture/Resource/ExtoicGem` 20 张宝石，共 44 张、源 PNG 约 1.97 MiB；其 GUID 在当前 Scene/Prefab/asset/设置中引用为 0，代码也没有对应 Resources 路径。
- 当前证据：上述两个目录的 PNG/`.meta`；当前 YAML/GUID 交叉扫描与代码路径搜索。其余 85 张 PNG 未被本条宣称为无引用。
- 根因或设计诊断：旧题材素材留在可运行 Resources 树，未经过当前 Kingdom 产品边界清点。
- 为什么不符合 Kingdom 当前目标：当前纵向切片应只携带可解释的资源与 UI；但此问题低于玩法正确性和清权。
- 推荐的最小解决方向：未来先用 Unity 依赖与构建报告再次确认；确属无用时移动到仓库历史/非 Resources 范围或按项目资产政策归档，同时保留 `.meta` 与来源记录。
- 可选方案及取舍：暂时保留但建立 `legacy-not-for-runtime` 清单，零代码风险但仍占构建/仓库空间。
- 明确不建议采用的方案：不建议本轮删除，不建议因素材存在而反向新增龙/Golem/宝石玩法。
- 涉及的系统、代码和资产：44 个 PNG/`.meta`、Resources 构建、未来美术方向。
- 前置依赖：授权来源核查、Unity Build Report 和用户确认素材不再使用。
- 风险和可能回归：代码字符串间接加载或外部流程引用可能未被 GUID 扫描捕获；移动必须保留 GUID。
- 工作量：S。
- 是否可以交给实施智能体：证据复核后可以，作为 Wave 5/维护批次。
- 后续实施验收标准：Unity 依赖和构建报告确认零运行引用；移动前后当前 UI 图标完整；Build Report 记录体积变化；来源/授权台账保留。
- 尚缺的验证证据：未生成 Build Report、未运行 UI、未检查项目外部工具对路径的依赖。

### TUTORIAL-P3-002 Tutorial 非经济完成回执已实现

- 标题：教程步骤完成后显示直接前驱目标，不新增经济奖励
- 类型：教程 / 完成反馈 / 内容契约
- 状态：已解决（源码与测试已实施，待 Unity Test Runner 执行）
- 优先级：P3
- 证据等级：E1
- 置信度：高
- 玩家发生场景：玩家完成一个教程步骤并进入它的直接后续步骤。
- 玩家可感知症状：修复前步骤只会静默推进；现在概览引导先显示“上一步已完成：……”再显示当前目标。
- 具体影响：回执完全从现有 `CompletedStepIds` 和步骤 `NextStepId` 推导，只接受当前步骤已经完成的直接前驱；不新增奖励、计时器、存档字段或重复领取路径。8 个资产的空 `RewardId` 仍保留为未启用的定义字段，不向玩家承诺经济奖励。
- 当前证据：`TutorialSnapshot.CompletedGoal`、`TutorialManager.FindPreviousCompletedStep()`、`KingdomUIRoot.LiveRefresh.cs` 的概览文本，以及 `CompletedGoalFeedbackUsesCompletedDirectPredecessor` Editor 回归测试；三个 C# 项目最近一次 CLI 编译均为 0 错误。
- 根因或设计诊断：教程已有稳定的完成 ID 与有向步骤链，缺少的是把现有状态转换为玩家可见反馈，而不是新的奖励系统。
- 为什么不符合 Kingdom 当前目标：教程完成需要清晰反馈，但当前不应为此新增平行货币或破坏早期经济。
- 推荐的最小解决方向：最小非经济回执已经完成；保持 `RewardId` 为空，除非未来玩家测试证明奖励确有必要。
- 可选方案及取舍：删除未用字段可简化契约，但会改 ScriptableObject 结构；当前保留为空且不消费，序列化风险最低。
- 明确不建议采用的方案：不建议新增教程专属货币或大额资源跳过早期闭环。
- 涉及的系统、代码和资产：Tutorial DTO、8 个资产、概览指导、SaveManager。
- 前置依赖：`TUTORIAL-P2-001` 的按步骤页面访问门已实施。
- 风险和可能回归：错误地显示非直接前驱，或旧存档完成集合存在分支项时显示无关目标；当前查找规则与回归测试都限定为直接前驱。
- 工作量：S，源码与回归测试已完成。
- 是否可以交给实施智能体：否，当前工作树已实施；仅剩验证。
- 后续实施验收标准：完成当前步骤后概览只显示它的标题一次；未完成或非直接前驱不显示；新游戏、载入和后续推进不产生经济 State 变化；Unity Test Runner 包含该 Editor 用例。
- 尚缺的验证证据：最新 554 项 EditMode 报告早于本用例，尚无 Unity Test Runner 通过记录和 P40 实机显示证据。

### TUTORIAL-P3-003 时代页指向自身的可点击空操作已修复

- 标题：Era 教程目的地为 Era 时不再显示可点击但无效果的导航
- 类型：教程 / UI 反馈 / 导航
- 状态：已解决（源码与测试已实施，待 PlayMode 执行）
- 优先级：P3
- 证据等级：E1
- 置信度：高
- 玩家发生场景：教程处于 EraGoal 或 LongTerm，玩家已经打开时代页并点击“引导推荐行动”。
- 玩家可感知症状：修复前该行仍写“打开时代页面”且可点击，但 `NavigateToTutorialPage("Era")` 立即返回，点击没有任何反馈。
- 具体影响：`BuildEraPage()` 无条件传入非空导航闭包，使 `AddEraTextRow()` 把按钮设为可交互；实际目标又是当前页，形成明确的虚假可操作性。
- 当前证据：当前 `KingdomUIRoot.Era.cs` Git diff；目的地为 Era 时现在传入 `null` Action，并显示“查看下方时代条件与当前主要阻碍”。`EraPageIncludesOnboardingGuidance` 把教程恢复到 `era-goal` 后断言运行时第四个引导行按钮不可交互；三个 C# 项目 0 编译错误。
- 根因或设计诊断：导航函数有同页保护，但按钮可交互状态只看是否传入闭包；调用者没有把“无需导航”转换为 `null`。
- 为什么不符合 Kingdom 当前目标：引导按钮必须提供可预测结果；移动端点击空操作会让玩家怀疑触控或页面失效。
- 推荐的最小解决方向：最小修复已完成；保留 `NavigateToTutorialPage` 的防御性检查，等待真实 PlayMode 验证按钮状态与文字。
- 可选方案及取舍：也可让按钮滚动到“当前主要阻碍”，但当前行已紧邻下方条件，增加滚动控制没有必要。
- 明确不建议采用的方案：不建议保留可点击状态只播放音效，也不建议重复重建 Era 页面来模拟导航。
- 涉及的系统、代码和资产：`KingdomUIRoot.Era`、TutorialSnapshot.NavigationPage、KingdomUITextRow、Onboarding PlayMode。
- 前置依赖：无玩法依赖。
- 风险和可能回归：仅影响 Era 同页教程行；其他 Overview/Resources/Buildings/Research 导航仍走原路径。
- 工作量：S，源码与回归测试已完成。
- 是否可以交给实施智能体：否，当前工作树已实施；仅剩验证。
- 后续实施验收标准：EraGoal/LongTerm 在 Era 页的行动行不可交互且说明当前页内容；其他目的页仍可点击并正确导航；PlayMode XML 包含该用例。
- 尚缺的验证证据：新增 PlayMode 断言尚未由 Unity Test Runner 执行，没有真机触控证据。

### ECON-P1-002 三个定义重复声明同一 Food 效果目标并被运行时重复相乘

- 标题：Calendar、IndustrialAgriculture 与 AgriculturalMachinery 重复叠加同目标 Food 效果
- 类型：经济正确性 / 定义验证 / 研究与 Workshop
- 状态：已确认
- 优先级：P1
- 证据等级：E1
- 置信度：高
- 玩家发生场景：依次完成历法、工业农业研究或购买农业机械装备后。
- 玩家可感知症状：Food 产能跳升远大于单条效果看起来的幅度；修复当前编译阻断后，定义测试还会直接拒绝这些资产。
- 具体影响：`Calendar` 的 `1.21` 与 `1.1` 实际相乘为 `1.331`；`IndustrialAgriculture` 的 `1.875 × 1.8 × 1.8 = 6.075`，另对 `PlantingField` 提供 `×2.4`；`AgriculturalMachinery` 的两个 `×1.3` 实际为 `×1.69`，另对 `PlantingField` 提供 `×1.6`。这些不是 UI 合并显示，而是 `MultiplyGlobalFoodProductionMultiplier` 的真实重复提交。
- 当前证据：`Assets/Resources/Datas/Research/Animal/Calendar.asset`、`Research/Industrial/IndustrialAgriculture.asset`、`Workshop/AgriculturalMachinery.asset` 的 `effects`；`ProgressionModifierManager.MultiplyGlobalFoodProductionMultiplier`；`Assets/Tests/Editor/GlobalEconomyDefinitionTests.cs` 的“每个研究/工坊不能重复声明同一效果目标”。
- 根因或设计诊断：同一轮内容合并保留了多个历史 Food 倍率，没有在资产层收敛为唯一效果目标；Food 又是少数采用乘法组合的全局效果，因此重复项被放大。
- 为什么不符合 Kingdom 当前目标：当前纵向切片的 Food、人口与幸福度是核心反馈环；定义本身既违反测试契约，又使任何当前节奏结论失真。
- 推荐的最小解决方向：未来数值批次先确定每个定义唯一的目标 Food 倍率，只保留一条 `GlobalFoodProductionMultiplier`；再记录修改前后值并在行为正确后重新生成当前模拟证据。
- 可选方案及取舍：若产品确实要保留复合提升，可把等效总倍率写成单条效果；这样通过唯一目标规则，但会保留目前极高的增幅，仍需单独证明合理。
- 明确不建议采用的方案：不建议删除测试、改成允许重复目标或用描述文本掩盖叠加；本轮也没有未经模拟直接猜测新数值。
- 涉及的系统、代码和资产：上述 3 个资产、`ProgressionModifierManager`、Food/人口/幸福度、定义测试、未来模拟报告。
- 前置依赖：先修复 `COMPILE-P1-001`；再确认当前 Food 目标时长与同代生产者数量。
- 风险和可能回归：现有存档完成状态会在重建 modifiers 后立即采用新倍率，Food、人口增长和幸福度都会变化。
- 工作量：S（定义收敛）+ M（当前证据与节奏复验）。
- 是否可以交给实施智能体：可以，作为独立 Wave 1 定义批次。
- 后续实施验收标准：三项资产各自同目标 key 计数为 1；定义测试通过；记录实际合成倍率；闭包保持通过；当前模拟和 Unity Food tick 均使用同一资产版本。
- 尚缺的验证证据：本轮未运行定义测试、闭包、模拟器或 Unity，尚未测量玩家实际 Food 曲线。

### ECON-P2-003 多产物建筑的整栋增产让专业技术同时提升无关产物

- 标题：专业研究/工坊通过 BuildingProductionMultiplier 放大聚合建筑的全部产物
- 类型：生产链 / 效果粒度 / 内容兑现
- 状态：已确认
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：购买轨道纺织机组、轨道真空炉、轨道自主采矿等专业升级，或完成同类研究后。
- 玩家可感知症状：看似只改善布料、冶金或采矿的升级，会同时提高同一聚合建筑里的其他矿物、布料和钛合金；玩家难以从描述预测真实收益。
- 具体影响：当前有 16 座多产物建筑。`OrbitalResourceExtractionArray` 同时输出 8 种资源，却被 7 条整栋生产效果命中；例如 `OrbitalTextileLooms` 的 `×1.4` 会同时提高钛/镍精矿、铝土、铜/锡/铁矿和钛合金。`MachineFactory` 同时输出 Machinery、Engine、Composite，并被 7 条整栋效果命中。Building 级效果采用增量叠加，因此影响不是仅限描述中的工序。
- 当前证据：`OrbitalResourceExtractionArray.asset`、`MachineFactory.asset` 及其 generation；`OrbitalTextileFabrication`、`OrbitalVacuumMetallurgy`、`AutonomousOrbitalMining`、对应 Workshop 的 Building 效果；`ProgressionModifierState.AddBuildingProductionMultiplier`。
- 根因或设计诊断：为避免每种资源一个工厂而合并了生产节点，但已有 Building 粒度 modifier 无法表达“这座综合体中的某一种产物”。
- 为什么不符合 Kingdom 当前目标：旧工业延续和综合设施本身符合长期决策，但效果粒度把专业选择变成隐藏的全产线增益，削弱策略可读性并放大 Spacer 产能。
- 推荐的最小解决方向：逐项区分“整座设施优化”和“单一产物工艺”；后者优先改用现有 `ResourceProductionMultiplier`，前者才保留 Building multiplier，不新增平行工厂。
- 可选方案及取舍：拆分聚合建筑可让效果更直观，但会违反优先复用现有内容并增加移动端 UI、升级链和维护负担；不作为当前首选。
- 明确不建议采用的方案：不建议为每种工业资源新增轨道替代工厂，也不建议只把描述改成“提升全部产物”来接受失去专业选择的问题。
- 涉及的系统、代码和资产：16 座多产物建筑、Research/Workshop modifiers、资源流 UI、当前闭包与未来模拟。
- 前置依赖：`ECON-P1-001` 与 `ECON-P1-002` 的行为正确性先稳定；明确每条升级的设计目标。
- 风险和可能回归：改成资源级 multiplier 会影响同资源的其他来源；必须核对是全资源来源还是仅聚合建筑的某个产物更符合意图。
- 工作量：M。
- 是否可以交给实施智能体：可以，但需逐条效果清单，不应批量机械替换。
- 后续实施验收标准：为每条专业升级列出预期受益产物；测试证明非目标产物不变；升级后 UI 显示的产出变化与描述一致；闭包与当前模拟复验。
- 尚缺的验证证据：未运行实际 modifier 重建与 Unity 产出 tick；当前结论由字段和实现直接推导。

### ECON-P2-004 星际自治宪章把“+6%”格式写成了原始 HappinessBonus 1.06

- 标题：InterstellarAutonomyCharterTheory 一次把幸福度奖励推到全局上限
- 类型：幸福度 / 数值语义 / Spacer 研究
- 状态：已确认
- 优先级：P2
- 证据等级：E1
- 置信度：高
- 玩家发生场景：完成“星际自治宪章理论”后且 Food 不短缺时。
- 玩家可感知症状：幸福度奖励直接达到 `1.5` 上限，早期草药、公共卫生、现代医学和精准医疗的小额幸福奖励被该节点完全覆盖；描述没有说明这是封顶效果。
- 具体影响：资产写 `HappinessBonus=1.06`；`AddHappinessBonus` 直接累加原始值，而 `HappinessFormula` 最终把倍率截到 `1 + MaximumBonus = 1.5`。同类效果使用 `0.02/0.04/0.06/0.08`，因此该值明显采用了 multiplier 的 `1.06` 表达而不是 bonus 的 `0.06` 表达。
- 当前证据：`InterstellarAutonomyCharterTheory.asset`；`ProgressionModifierManager.AddHappinessBonus`；`HappinessFormula.MaximumBonus` 与 `CalculateMultiplier`。
- 根因或设计诊断：效果类型命名为 Bonus，但资产沿用了“倍率从 1 起”的写法；当前验证只检查类型存在，没有范围/单位一致性测试。
- 为什么不符合 Kingdom 当前目标：幸福度应由 Food 与多项社会/医疗研究共同构成可理解反馈，而单节点静默封顶使此前投入失去边际价值。
- 推荐的最小解决方向：未来确认设计意图；若是 +6%，把值改为 `0.06`，并增加 HappinessBonus 合理范围与合成结果测试。
- 可选方案及取舍：若确实要让宪章直接封顶，应把描述明确为强终局能力并重新评估所有早期幸福研究的价值；这会削弱长期组合，不推荐默认采用。
- 明确不建议采用的方案：不建议提高全局幸福上限来容纳 `1.06`，也不建议把其他 bonus 全部改成 `1.xx`。
- 涉及的系统、代码和资产：该 Research、ProgressionModifier、HappinessFormula、人口增长/生产力奖励。
- 前置依赖：确认 HappinessBonus 的产品单位。
- 风险和可能回归：降低 bonus 会改变 Spacer 人口增长、生产力与战役准备节奏。
- 工作量：S。
- 是否可以交给实施智能体：产品确认 `0.06` 后可以。
- 后续实施验收标准：完成前后合成 bonus 与最终 HappinessMultiplier 有明确断言；Food 短缺仍优先压制奖励；旧存档重建 modifiers 后结果一致可解释。
- 尚缺的验证证据：未运行 Unity 或当前模拟；没有玩家数据证明目标幅度。

### ONBOARD-P2-001 新游戏前一分钟没有可支付的第一座建筑

- 标题：零库存、1 原木/秒和 80 原木木屋形成至少 80 秒的被动开局
- 类型：新手旅程 / 早期节奏 / 反馈
- 状态：强推断
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高（时间下界高，实际感受待运行）
- 玩家发生场景：全新存档进入 Animal 时代，尚未拥有任何建筑或人口。
- 玩家可感知症状：前 60 秒仍只能看原木自动增长，第一座无前置建筑仍买不起；修复前建筑步骤只说“缺少能解决问题的建筑”，现在会按 `TryBuild` 的实际拒绝顺序显示推荐建筑的领土、生产力或下一座资源缺口，资源不足时再按当前净产出给出约等待秒数。
- 具体影响：新游戏 WoodLog 为 0、基础产出 1/s；`WoodHouse` 无研究前置但成本 80 WoodLog，是最早的人口容量入口。因此不考虑加载/离线时，最早支付时间下界是 80 秒，晚于目标审查中的“前 1 分钟”。
- 当前证据：`ResourceManager.EnsureStartingResource`；`KingdomPlayModeTests` 源码对新游戏 WoodLog=0、ProductionRate=1 的断言；`WoodHouse.asset` 的 80 WoodLog、无前置与 5 人容量；`TutorialManager.DescribeBuildingBlocker()` 与 `BuildingManager.TryBuild()` 的领土→生产力→资源顺序；三个 C# 项目 CLI 编译 0 错误。
- 根因或设计诊断：开局仍把等待当作唯一资源获取方式；本轮已补足“目标建筑—首个真实阻碍—资源净产出—约等待时间”反馈，且不再在领土或生产力先行阻断时误报资源，但尚未增加可选择的准备或主动行为。
- 为什么不符合 Kingdom 当前目标：玩家第一分钟应理解行动—反馈，而不是教程自动跳页后等待唯一资源。
- 推荐的最小解决方向：只读反馈缓解已经实施。先用真实新游戏计时判断明确倒计时是否足够；若玩家仍因纯等待流失，再在玩法正确后从“恰好足够第一座木屋的起始原木”或“首座木屋成本降到 60 秒以内”二选一，并运行闭包、模拟与 PlayMode。
- 可选方案及取舍：当前保留等待、只增加动态缺口/ETA，零经济回归但不能创造主动选择；加入主动点击采集会新增重复操作和自动化负担，不如起始库存简洁。
- 明确不建议采用的方案：不建议新增开局专属货币、点击器系统或跳过整个 Animal 生产链的大额礼包。
- 涉及的系统、代码和资产：新游戏初始化、WoodLog、WoodHouse、Tutorial、Overview/Buildings 页面。
- 前置依赖：先修复当前编译阻断和 Tutorial 被动跳过语义，再做真实 1/10 分钟测试。
- 风险和可能回归：当前 ETA 只按这一刻的净产出估算，玩家消费资源或生产率变化后会随两秒刷新变化；未来起始 Wood/成本改动会改变首批建筑和研究节奏，必须限制到一个明确 bootstrap 目标。
- 工作量：S。
- 是否可以交给实施智能体：行为方案经产品确认后可以。
- 后续实施验收标准：建筑步骤与 `TryBuild` 一样先显示领土不足，再显示生产力不足，最后显示同成本倍率下的资源缺口和当前净率 ETA；全部满足时提示可以建造；新玩家 60 秒内完成一次有意图的购买或选择；教程停留到对应行动；若改经济数据，当前模拟重新记录首建时间。
- 尚缺的验证证据：领土/生产力/资源三类动态阻碍已通过三个 C# 项目编译，但未由 Unity Test Runner 或新游戏实测验证；仍未确认 UI 首屏是否提供其他有意义操作。

### ERA-P2-001 “新石器时代”实际覆盖铜石并用、青铜、铁器和煤矿

- 标题：时代显示名称比当前 Neolithic 内容边界明显更窄
- 类型：时代定位 / 内容编辑 / 玩家预期
- 状态：强推断
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高（内容事实高，命名决策待用户确认）
- 玩家发生场景：完成定居聚落后，继续推进整个 Neolithic 研究与建筑列表。
- 玩家可感知症状：页面仍显示“新石器时代”，但玩家连续解锁铜、锡、青铜、铁、煤矿、多金属冶炼、文字、度量衡和议事治理，难以判断时代主题与下一跃迁边界。
- 具体影响：17 项 Neolithic Research 包含 `Smithing_Copper/Bronze/Iron`、`WrittenRecords`、`Measurement`；10 座建筑包含 CoalMine、MetalMine、MetalSmelter。`FeudalAdministration` 又以青铜、铁器和文字作为 Medieval 前置，证明这些不是偶然的跨时代副作用。
- 当前证据：Neolithic Research/Building 资产及其 TechLevel、前置；`TechLevel.Neolithic` 当前显示“新石器时代”。
- 根因或设计诊断：为了不新增多个短时代，内容把新石器定居到早期铁器阶段压缩在同一枚举值中，但玩家可见名称没有同步扩展。
- 为什么不符合 Kingdom 当前目标：时代应带来清晰的新决策方式和中期目标；名称过窄会把合理的压缩设计表现为历史/内容错配。
- 推荐的最小解决方向：不拆分 TechLevel；由用户选择更宽的显示名或副标题，例如“定居与早期金属时代”，并在 Era 页面明确本阶段目标是农业定居→文字治理→铁器基础。
- 可选方案及取舍：保留“新石器时代”但加说明成本最小，仍有术语张力；新增铜石/青铜/铁器时代最精确，但会扩大研究树、存档、UI、闭包和节奏范围，不适合当前里程碑。
- 明确不建议采用的方案：不建议现在新增 2～3 个 TechLevel 或机械重分全部资产。
- 涉及的系统、代码和资产：TechLevel 显示、Era 页面、17 Research、10 Building、教程与路线图。
- 前置依赖：用户确认产品更偏历史准确还是压缩式文明阶段。
- 风险和可能回归：文案、截图、教程和外部宣传术语同步；不应修改稳定 ID。
- 工作量：S（显示名/说明）或 XL（不推荐的时代拆分）。
- 是否可以交给实施智能体：显示名决策后可以。
- 后续实施验收标准：时代名称、Era 目标、研究/建筑描述一致；玩家测试能正确说出本时代起点、终点和三项核心能力。
- 尚缺的验证证据：没有玩家理解测试或用户最终命名决策。

### ERA-P2-002 移除前 Spacer 战斗后，Medieval 部分节点退化为相近的全局产能百分比

- 标题：Medieval 的非战斗替代效果缺少足够差异化的时代选择
- 类型：时代策略 / 研究内容 / 纵向切片
- 状态：强推断
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：中高
- 玩家发生场景：进入 Medieval，在钢铁、城市、学术和贸易主线之外完成 Fortification、StandingArmy 与 Gunpowder 等历史稳定 ID 对应节点。
- 玩家可感知症状：可见名称已变成“基础设施规划”“标准化工作流”“化学工艺与标准”，实际分别只给全建筑 `+5%/+6%/+7%`；玩家获得的是相近数字，而非新的决策方式。
- 具体影响：`PreSpacerCombatRemovalTests` 明确要求 Animal～Medieval 五个原战斗节点改为全局生产倍率，并禁止前 Spacer 军事属性。该边界合理，但 Medieval 三个节点连成单一百分比梯度；相较同期钢、物流、住房、公共卫生和学院，它们缺少独立角色。
- 当前证据：`Fortification.asset`、`StandingArmy.asset`、`Gunpowder.asset`；`PreSpacerCombatRemovalTests.C811_ConvertedResearchUsesScopedProductionMultipliers`。
- 根因或设计诊断：战斗移除时优先保证闭包与无死效果，使用了最通用的生产倍率作为替代，尚未完成非战斗时代身份重构。
- 为什么不符合 Kingdom 当前目标：每个时代应改变玩家决策，而不是只扩大数值；当前 Medieval 是早期纵向切片的一部分。
- 推荐的最小解决方向：保留稳定 ID 与无前 Spacer 战斗边界，优先用现有效果类型把三项区分为建设/物流/研究或幸福等不同角色，并让名称、描述、成本和前置对应；不新增系统。
- 可选方案及取舍：仅继续润色名称风险低但无法解决伪选择；删除节点会缩短内容却破坏前置与稳定 ID，不优先。
- 明确不建议采用的方案：不建议重新引入前 Spacer 战斗、军力建筑或 workforce。
- 涉及的系统、代码和资产：三项 Medieval Research、两个更早转换节点、ResearchTree、测试与模拟。
- 前置依赖：先修复行为 P0/P1；再用当前模拟确认各效果对节奏的边际影响。
- 风险和可能回归：研究成本时长、后续前置、当前测试精确值和旧存档完成状态。
- 工作量：M。
- 是否可以交给实施智能体：策划分工确认后可以。
- 后续实施验收标准：三个节点的玩家可见角色互不重复；每个有可观测结果；前 Spacer 仍无军事效果；闭包、定义测试和当前模拟通过。
- 尚缺的验证证据：未进行玩家选择测试，尚不能量化这些节点的实际购买顺序与感知价值。

### RESOURCE-P2-001 Resource.TechLevel 的“首次来源”与“主要用途/UI 分类”语义未统一

- 标题：8 种资源的 TechLevel 与最早生产时代不同，UI 又直接按该字段排序
- 类型：资源分类 / 时代一致性 / UI 信息架构
- 状态：已确认（字段差异）+ 强推断（理解影响）
- 优先级：P2
- 证据等级：E1 + E3
- 置信度：高
- 玩家发生场景：早期生产陶瓷、铁，或 Industrial 开始生产复合材料、火箭燃料与钛链后打开 Resources 页面。
- 玩家可感知症状：资源只在有库存/流量时出现，却按 TechLevel 从高到低排序；“工业/太空”分类资源可能在较早时代出现在列表顶部，字段又没有玩家可见的解释。
- 具体影响：Cloth 标 Animal 但首个建筑源在 Neolithic；Iron/IronOre 标 Medieval 但首源在 Neolithic；Ceramic 标 Industrial 但首源在 Neolithic；Composite、RocketFuel、TitaniumConcentrate、TitaniumAlloy 标 Spacer 但首源在 Industrial。当前测试还明确要求后四者的 Industrial 来源。
- 当前证据：8 个 Resource 资产、所有 Building generation；`KingdomUIRoot.IsResourceVisible` 只看 amount/production/consumption，`CompareResourceRows` 按 `right.TechLevel` 排序；`AdvancedMaterialProgressionTests` 要求 Industrial source。
- 根因或设计诊断：TechLevel 同时被当作内容分类和排序键，但资产有的按首次来源、有的按主要用途/目标时代填写，契约没有单一定义。
- 为什么不符合 Kingdom 当前目标：时代描述和资源路线需要可预测；未定义语义会诱导后续智能体错误移动来源或改变 UI。
- 推荐的最小解决方向：用户先选择字段语义。若是首次来源，统一 8 个值并验证排序；若是主要用途，保留资产值但把 UI 分类/说明明确为“主要应用时代”，且另从实际来源生成解锁信息。
- 可选方案及取舍：取消 TechLevel 排序、按首次出现/稳定 ID 排序可避免误导，但会失去时代聚类；新增独立 `PrimaryUseTechLevel` 更明确但增加序列化字段，不是当前最小方案。
- 明确不建议采用的方案：不建议因字段不一致删除 Industrial 的钛、复合材料和火箭燃料来源；这些来源被当前闭包与测试用于进入 Spacer。
- 涉及的系统、代码和资产：Resource 定义、Building 生产链、Resources UI、定义测试与路线图。
- 前置依赖：产品字段语义决策。
- 风险和可能回归：资源排序、Era 条件、测试和历史存档展示；稳定 ID/GUID 不变。
- 工作量：S（契约与 UI 文案）/M（字段统一与测试）。
- 是否可以交给实施智能体：决策后可以。
- 后续实施验收标准：所有 Resource.TechLevel 符合同一书面规则；8 个边界案例有测试；资源页面排序/分类能被玩家解释；闭包仍通过。
- 尚缺的验证证据：未在 Unity 查看实际列表顺序与玩家理解。

### WORKSHOP-P2-002 四个 Spacer Workshop 的描述与实际效果指向不同系统

- 标题：时序、轨道规划、模块舱与可复用发射级没有兑现描述承诺
- 类型：Workshop 内容 / 描述—效果一致性
- 状态：已确认
- 优先级：P2
- 证据等级：E1
- 置信度：高
- 玩家发生场景：玩家查看并购买四项高成本 Spacer Workshop。
- 玩家可感知症状：购买前读到的目标与购买后 modifier 不一致，无法比较路线价值。
- 具体影响：`CausalSynchronizationArray` 描述安排远征阶段与研究任务，实际只把补给成本乘 `0.94`；`GravityAssistTrajectoryPlanner` 描述落实到资源提取阵列，实际只给全局探索 `1.18`；`ModularHabitatSystems` 描述降低扩建成本/提高居住，实际只提高 OrbitalStation 物流 `1.25`；`ReusableLaunchStages` 描述降低发射中心燃料/结构压力，实际降低全局舰队维修成本到 `0.85`。
- 当前证据：四个 Workshop 资产的 Description 与 effects；`WorkshopEffectType` 运行时分支。
- 根因或设计诊断：理论→设备内容批量扩展时复用了可用 modifier，但玩家文案仍保留另一项设计意图。
- 为什么不符合 Kingdom 当前目标：Workshop 应成为第二条有意义的发展路线；高成本升级若不能从描述预测结果，就成为不可审计选择。
- 推荐的最小解决方向：逐项由产品确认“描述还是效果”为权威，再只改一侧；同时在详情 UI 生成结构化效果摘要，避免以后仅靠自由文本。
- 可选方案及取舍：纯改描述最小但可能保留错误玩法；改效果更符合原承诺但会改变战役/建筑平衡。四项不能批量采用同一答案。
- 明确不建议采用的方案：不建议为了快速一致而把四段描述改成晦涩的通用“提高效率”，也不建议新增四种效果类型。
- 涉及的系统、代码和资产：四个 Workshop、ProgressionModifier、Workshop UI、战役/探索/物流/建造。
- 前置依赖：`WORKSHOP-P2-001` 已提供锁定/已购项的渐进可见基础；产品逐项确认意图。
- 风险和可能回归：前置链、战役供给、探索、OrbitalStation 和维修成本。
- 工作量：S（文案）或 M（效果与测试）。
- 是否可以交给实施智能体：逐项决策后可以。
- 后续实施验收标准：每项 Description、结构化效果摘要与实际 modifier 一致；购买前后 UI 数值变化可观测；对应单元测试断言目标系统。
- 尚缺的验证证据：未运行 Workshop 购买或详情 UI；当前无法判断哪一侧是最终产品意图。

### TEXT-P3-003 明确术语、资产描述与效果枚举中文已直接修复

- 标题：纯文本错字、术语、内部 ID、产物描述和玩家可见效果名已收敛
- 类型：内容编辑 / 已实施修复
- 状态：已解决
- 优先级：P3
- 证据等级：E1
- 置信度：高
- 玩家发生场景：查看 Industrial/Spacer 的研究、Workshop 与建筑详情。
- 玩家可感知症状：修复前会看到“钹合金”“炸破”“分馀”“相变材料”、`Composite`/`Biomass`/内部稳定 ID、`Spacer` 英文名、重复标点及未列全实际产物的描述；研究推荐还会暴露 `BuildingProductionMultiplier` 等内部枚举名，两个 Workshop 的全局粮食效果会显示 `GlobalFoodProductionMultiplier`。
- 具体影响：用户授权后先修改 25 个资产、30 行 `Label`/`Description`；随后为全部 30 个 `ResearchEffectType` 和缺失描述的 Workshop 全局粮食效果补齐中文 `Description`，并让教程研究推荐复用已有 `GetDescription()` 缓存。所有枚举成员、显式整数、资产效果和运行时分支保持不变。
- 当前证据：当前 Git diff；涉及 25 个定义资产、`ResearchEffect.cs`、`WorkshopUpgrade.cs` 与 `TutorialManager.DescribeResearchRole()`；Runtime、PlayMode 测试程序集和 Editor 程序集 CLI 编译均为 0 错误。
- 根因或设计诊断：批量内容扩展混入了机器式设计说明、英文内部名、术语误写和描述滞后。
- 为什么不符合 Kingdom 当前目标：时代内容必须让玩家从名称与说明理解真实能力；这些问题可以不改变玩法地直接收敛。
- 推荐的最小解决方向：已完成；保持本批次为纯文本，不夹带效果或数值修改。
- 可选方案及取舍：可统一把全部 YAML 字符串改为同一种转义风格，但会制造无关差异，本轮未做。
- 明确不建议采用的方案：不建议重命名稳定 ID、移动资产、改 GUID，或用文本掩盖 `WORKSHOP-P2-002` 等真实效果歧义。
- 涉及的系统、代码和资产：25 个 Research/Workshop/Building 资产、两个效果枚举与教程研究推荐；无 `.meta`、稳定 ID、枚举整数、效果或数值变化。
- 前置依赖：用户已明确授权简单名称/描述修复。
- 风险和可能回归：TMP 字体缺字、详情宽度和翻译一致性；枚举整数已保持，玩法数据零变化。
- 工作量：S，已完成。
- 是否可以交给实施智能体：否，当前工作树已实施；未来只需验证。
- 后续实施验收标准：静态扫描不再出现已修复词；教程推荐和 Workshop 效果摘要不显示内部英文枚举名；Unity 中所有中文可渲染且不截断；ID/GUID/前置/效果和枚举整数 diff 为零。
- 尚缺的验证证据：未启动 Unity，未验证教程推荐、Workshop 效果摘要、字体、换行、详情面板高度或 P40 真机显示。

## 9. 按时代分类的玩家旅程审查

本章是静态玩家旅程推演，不是实际游玩记录。代码、资产和测试源码能证明可用入口与公式；“玩家是否理解、是否觉得等待过长、触控是否顺畅”仍是 E3/E4。

### 9.1 新游戏、前 1 分钟与前 10 分钟

| 旅程节点 | 当前静态证据 | 判断 | 尚缺证据 |
|---|---|---|---|
| 进入新游戏 | Food=300、人口=0、WoodLog=0 且基础产出 1/s；WoodHouse 无研究前置、成本 80 WoodLog | 至少 80 秒才能支付第一座人口建筑，前 1 分钟缺少可支付的主动选择，见 `ONBOARD-P2-001` | 新游戏录像、首次点击/购买时间 |
| 教程方向与资源 | 日历、WoodLog 等原状态条件保留；当前另要求活动步骤对应页面已被访问 | 被动连续跳步已增加最小页面门，静态实现完成但待运行验证，见 `TUTORIAL-P2-001` | 新 Editor 断言、Tutorial PlayMode 与玩家观察 |
| 首次建造 | WoodHouse 提供 5 人容量且不消耗生产力；Farm 提供 8 Food/s | bootstrap 结构本身简洁：住房→人口→生产力→Food；问题主要是第一步等待与反馈 | 实际按钮阻断、动画、音效、批量操作 |
| 首次资源短缺 | UI 具备 amount、production、consumption、net 和建筑来源/去向详情 | 静态信息入口存在；多输入建筑错误满足率会让玩家无法按最小瓶颈推断结果 | Unity 刷新、数值一致性、缺口文案 |
| 首次研究 | Research 有基础成本、资源成本、前置和效果；支付按钮显示具体 blocker，需求行逐项显示缺口 | 资源成本仍允许部分扣款；UI 反馈源码已修复但待 PlayMode/P40 验证 | 原子支付 PlayMode、失败恢复、详情长文本 |
| 前 10 分钟 | 当前允许引用的模拟输入已过期，不能据其断言当前里程碑 | 只能确认系统路径，不能给出当前真实到达时代/研究时间 | 当前定义模拟与真实 Unity 计时 |

### 9.2 逐时代内容、主题与决策变化

| 时代 | 内容与能力 | 时代适配判断 | 主要风险 |
|---|---|---|---|
| Animal | 6 Resource、9 Building、16 Research；火、木石、农业、畜养、早期知识、住房 | 生存→定居的主题基本连贯；KnowledgeCircle、Farm、WoodHouse 构成最小纵向切片 | 第一座建筑至少等待 80 秒；教程被动跳过；部分研究已提前给后续建筑效果但前置无倒挂 |
| Neolithic | 6 Resource、10 Building、17 Research；灌溉、陶瓷、纺织、文字、度量衡、铜锡青铜铁煤 | 生产链完整，但内容远超“新石器”，实际是定居与早期金属时代 | `ERA-P2-001`；不应通过新增多个 TechLevel 解决当前命名问题 |
| Medieval | 3 Resource、5 Building、12 Research；钢、贸易物流、城市住宅、图书/学院、公共卫生 | 钢铁、Caravanserai、TownHouse 与学术链能形成新角色；没有新 Food 建筑但继续使用 IrrigationWorks，符合旧内容长期价值 | 三个战斗转换节点成为相近全局产能百分比，见 `ERA-P2-002`；选择密度低于 Industrial |
| Industrial | 18 Resource、26 Building、35 Research、36 Workshop；电力、物流、石油、化工、工业农业、大学、机械与多金属 | 当前最完整、最像独立时代；研究→Workshop→建筑→持续消耗的链条多数可解释 | 当前编译阻断、满足率、重复 Food 效果、多产物整栋增产和 Workshop 可见性使该时代尚不可验收 |
| Spacer | 7 Resource、15 Building、47 Research、46 Workshop、9 Sector；轨道工业、生命保障、量子/相位、舰队、探索、殖民和战役 | 多数“理论→实体设备→建筑/战役 modifier”配对清楚，且大量旧工业资源持续作为成本/维护输入 | `SECTOR-P1-001`、`WORKSHOP-P2-002`、`ECON-P2-003/004`；定义密度很高，Workshop 现按直接下一层渐进揭示，仍待 P40 验证 |
| Ultra | 1 项 TechnologicalSingularity，无 Resource/Building/Workshop | 目前只是从 Spacer 抵达的远期标记，不是有循环的可玩时代 | 不应在当前纵向切片前批量扩展；需要产品定义终局目标 |
| Archotech | 只有 enum，无定义 | 纯远期框架 | 不应声称可达或完成；需要单独路线与内容决策 |

### 9.3 建筑延续、人口供粮与恢复路径

当前升级链有 37 条直接边，核心长期链包括：`WoodHouse→StoneHouse→TownHouse→IndustrialHabitationComplex→OrbitalHabitatMegastructure`、`KnowledgeCircle→ScribeHut→Library→Academy→University→DeepSpaceObservatory→QuantumComputingArray→InterstellarTheoryNexus`、`FiberGatheringCamp/IrrigationWorks→PlantingField→OrbitalAgroecologyArray`、`Caravanserai→RailHub`，以及矿业、冶金、纺织、焦化和石化向 Industrial/Spacer 的升级。它们证明当前内容总体遵循“让旧建筑继续有价值”，不需要为每个时代另建平行工厂。

按 `PopulationState.FoodConsumptionPerPerson=0.8/s` 静态估算，满载住房的同代 Food 支持关系合理：WoodHouse 5 人需 4/s，Farm 8/s；StoneHouse 12 人需 9.6/s，IrrigationWorks 20/s；TownHouse 40 人需 32/s，约 2 座 IrrigationWorks；IndustrialHabitationComplex 240 人需 192/s，4 座 PlantingField 提供 192/s；OrbitalHabitatMegastructure 3000 人需 2400/s，约 4 座 OrbitalAgroecologyArray 提供 2880/s。该计算没有纳入当前重复 Food multiplier，也不是实际回本/节奏验收。

失败恢复方面，Food 短缺会压低幸福并触发人口离开，代码还存在“安全离开额度”与测试/模拟口径冲突（`POP-P2-001`）。普通资源不足应阻止交易，但研究当前会部分扣款（`RESEARCH-P1-001`）。存档损坏有主/备份回退设计，Workshop 顺序缺陷却可能让两份合法数据都无法应用（`SAVE-P0-001`）。因此当前不能声称失败后恢复路径已通过。

## 10. 经济、资源和进度审查

### 10.1 可达性与定义完整性

- 当前 `data/content-closure-static.md` 报告 Industrial 及以前 Research 80/80、Workshop 36/36、Building 50/50，Spacer 47/47、46/46、15/15，Ultra Research 1/1 可达；没有不可达条目。它是 E2 静态证据，不是运行时支付、生产或节奏证据。
- 直接解析当前前置图没有发现 Research、Workshop 或 Building 的高时代前置倒挂。9 条跨时代 Building 效果均是旧技术支持后续设施：ControlledFire/Mining/NaturalPhilosophy/WrittenRecords、Coking/MechanizedForestry 等；本轮已为机械化林业补上轨道延续说明。
- 128 个 Research 均有真实效果或时代推进，82 个 Workshop 均至少有一个效果。当前问题不是“空效果节点”，而是少数效果重复、粒度错误或描述不一致。
- Food 仍是唯一允许有库存容量的资源；本审查没有建议普通资源容量、仓库、MaxAmount 或 workforce。

### 10.2 当前经济正确性风险

1. `RESEARCH-P1-001` 破坏支付原子性，任何节奏数据都可能建立在已损失部分材料的异常状态上。
2. `ECON-P1-001` 让 36/65 个多输入建筑把满足率相乘而非取最小瓶颈，输入越多越被额外惩罚。
3. `ECON-P1-002` 让三项 Food 定义重复相乘，并直接违反当前定义测试源码。
4. `ECON-P2-003` 让专业升级放大聚合建筑的无关产物；这是效果粒度问题，不应通过新增轨道平行工厂解决。
5. `ECON-P2-004` 把 HappinessBonus `1.06` 当成原始加值，直接触顶。
6. `POP-P2-001` 使超容量人口离开口径在代码、测试和模拟之间不一致。

### 10.3 资源时代与长期价值

Industrial/Spacer 的建筑持续消费 WoodLog、Biomass、Cloth、Ceramic、Steel、CopperWire、Electronics、Chemical、Rubber、Glass、Coke、Fuel/Lubricant 等旧资源，满足长期价值方向。MachineFactory、ChemicalPlant、RareMetalMine 和 TitaniumMetallurgicalComplex 在 Industrial 就建立 Composite、RocketFuel 和钛链，为进入 Spacer 提供来源；当前测试也明确要求这些工业入口。

问题在于 `Resource.TechLevel` 的含义未统一（`RESOURCE-P2-001`），以及 `OrbitalResourceExtractionArray` 把矿业、钛冶金与纺织合并后，专业 Building multiplier 会一起提高全部八种产物。后续应在不增加平行工厂的前提下调整效果粒度。

### 10.4 节奏证据边界

`PacingAcceptance=FAIL` 与静态闭包通过可以同时成立：闭包只证明定义图存在路径，节奏报告则基于 40/69/121/73 的旧快照，而当前资产是 40/65/128/82。当前既不能用 FAIL 证明主线真实不可达，也不能用闭包通过证明可玩。Wave 3 之前必须先修复行为与证据版本（`REPORT-P1-001`）。

## 11. UI、研究树和移动端审查

### 11.1 已确认的静态契约

- `KingdomUIRoot.prefab` 与运行时配置均设为 `ScaleWithScreenSize`、2640×1200、Match Width；主 Scene 中历史零尺寸/零缩放覆写由 `EnsureRuntimeCanvasGeometry` 尝试修复。
- 主导航含 8 页，资源与建筑卡、详情面板、研究图、Workshop、Music 和 Sector 均有 authored prefab 路径。
- 资源详情能列库存、产出、消耗、净值与建筑来源/去向；Era 条件能导航到相关资源。静态入口存在不等于移动端可读性通过。

### 11.2 主要问题

- `UI-P2-001` 已在源码修复：研究支付 blocker 不再被覆盖，正文显示直接前置状态和结构化实际效果，需求行逐项显示缺口；仍待 PlayMode/P40 验证。
- `WORKSHOP-P2-001` 已在源码解决：已购项、已进入时代范围内的根节点与直接下一层现可渐进回看，行内显示首个真实阻碍，详情显示全部直接前置状态，刷新复用行池；仍待 Unity/P40 验证。
- `TEST-P2-001`：ResearchTree 专项验收仍要求 79 节点，而当前内容和测试目标是 128，验收门槛自身过期。
- `UI-P2-002`：图布局计算 `topologyUsable` 后不使用结论，资产网格 fallback 的适用性无法从日志证明。
- `UI-P2-003`：P40 原生参考下存在静态几何过密风险，触控、字号和详情区仍需真机。
- `RESOURCE-P2-001`：Resources 页面按 Resource.TechLevel 倒序，但该字段语义不统一。

### 11.3 研究树验收边界

不得用静态实现意图声称节点不重叠、可垂直滚动或拖拽/缩放无冲突。未来专项 PlayMode 必须使用与当前内容一致的目标节点数，记录 topology 决策、重复/反向/倒置边、viewport/content 正尺寸、每轴 overflow 和真实 content movement；节点、空白区、短按与双指手势还需在 P40 上验证。

## 12. 教程、文本、音画和可访问性审查

### 12.1 教程

8 个步骤覆盖方向、资源、建筑、人口、研究、生产链、时代和长期目标，导航页字段也与对应系统一致。原完成公式保留，但现在每个非终局步骤还必须在成为活动步骤后访问自己的 NavigationPage；这使教程从纯被动 State 推进变为“页面确认 + State”推进（`TUTORIAL-P2-001`）。Era 页指向自身的行动已经从可点击空操作改为只读条件说明（`TUTORIAL-P3-003`）。概览已从现有完成 ID 推导并显示直接前驱目标，提供非经济完成回执；8 个资产的 `RewardId` 仍为空且没有消费者，不引入教程货币（`TUTORIAL-P3-002`）。生产链摘要的乱码箭头和下游产物端点已经修复（`TEXT-P3-002`）；生产力充足但尚无相连生产/加工建筑时原本为空的 blocker 也已补成中性说明，未改链条拥有判定或步骤完成条件。Orientation 在访问概览但首日尚未到达时也不再显示“暂无阻碍”，而是直接说明尚未推进满一天；推荐行动从重复“查看概览”改为继续推进时间并观察人口和核心资源，同页引导卡不再暴露可点击空操作。人口步骤在尚未访问建筑页时也会把按钮指向该步骤真正要求的建筑页；访问后才根据实时食物/幸福度改为资源页建议，避免 blocker 与按钮互相矛盾。资源步骤及其内置回退文本也已从泛称“木材”统一为起始资源 `WoodLog` 的实际玩家可见名称“原木”。日历、人口、食物公式与页面门本身均未改变。新 Editor/PlayMode 断言及 Unity 字体渲染仍待用户复跑。

当前最小实施顺序为：错误摘要、按步骤页面访问门、非经济完成回执与 Era 同页空操作已经实施；下一步先用不依赖“人类/鼠族”正典的中性能力信息改善时代引导；正典确认后再统一教程与全时代叙事。若页面门的运行验证仍不足以证明学习动作，再针对失败步骤补成功命令事件，不先建立通用事件总线。

### 12.2 文本与时代术语

- 路线图承诺人类文明，运行时默认“鼠托邦/鼠族”，需要产品身份决策（`TEXT-P2-001`）。
- workforce 仍出现在当前 balance/roadmap 文档，与非协商规则冲突（`DOC-P2-001`）。
- 本轮已直接修复 25 个资产的明确文本问题，并把研究推荐/Workshop 摘要中的内部效果枚举名改为中文描述（`TEXT-P3-003`）；未触碰四项设计意图不明确的 Workshop（`WORKSHOP-P2-002`）。
- Neolithic 显示名与内容边界需要用户决策（`ERA-P2-001`），不应在文本批次擅自新增时代。
- Era 页已新增不依赖“人类/鼠族”正典的“本时代能力”摘要，逐时代概括当前真实 Research/Building/Workshop 内容；Ultra 与 Archotech 明确说明目前只是远期框架。该行不修改 TechLevel 名称、资产或玩法数据，仍待 PlayMode 与 P40 长文本验证。

### 12.3 音频、美术与发行

18 首音乐源文件合计 160.37 MiB，全部运行时分类为 `All Music`，仓库没有逐首授权/署名台账（`AUDIO-P2-001/002`）。44 张龙、Golem 与异种宝石 PNG 当前无 GUID/代码引用但仍在 Resources 范围（`ASSET-P3-001`）。这些问题低于玩法 P0/P1；未来必须先清权和 Build Report 复核，不能凭文件名直接认定侵权或删除资产。

### 12.4 可访问性边界

静态代码同时使用文本、数字和颜色表达部分状态，但按钮禁用原因、长中文、触控目标、色觉区分、读屏语义、字体字形和音量一致性均没有运行/真机证据。没有截图、辅助技术测试和 P40 记录，本轮不声称无障碍或视觉验收通过。

## 13. 存档、离线、模拟和性能风险

### 13.1 存档

`SaveManager` 有主存档、备份、校验和、版本化 DTO、退休 ID 迁移与应用失败回退，方向符合 Runtime State 权威边界。`SAVE-P0-001` 是当前最高风险：Workshop 保存按 ID 排序，而恢复时要求前置升级已先出现；64 条直接 Workshop 前置中 33 条为逆字典序，因此正常购买集合可能同时使主存档和备份应用失败。后续必须以拓扑顺序恢复或分两阶段验证/提交，不得放宽未知 ID/重复 ID 校验。

### 13.2 离线与玩法时钟

Scene 配置为 30 秒自动保存、最大离线 24 小时；`SimulationManager` 是唯一玩法时钟，0.1 秒 tick、每帧最多 20 tick。`SIM-P2-001` 已把长帧保护限定到 Editor，并让 Player 超额积压在后续帧按同一预算继续结算；真实编译、后台/恢复、低帧率与离线一致性仍未在 Unity/Android 验证。

### 13.3 当前报告与性能

当前模拟报告版本失配（`REPORT-P1-001`），不能作为当前离线行为回归；现有 PlayMode 报告含零用例。最新 Mode 0 已执行 554 项 EditMode，552 通过、2 失败；本轮新增的生产链端点断言晚于该报告，仍需用户复跑。音乐 importer 采用 Streaming/后台加载，降低常驻内存风险，但 160.37 MiB 源音频的 APK/AAB、I/O、耗电和热量均未测量。

## 14. Sector、战役与后期内容审查

当前 9 个 Sector 分为 5 个本星系探索目标（LowOrbit、Moon、Mars、MainAsteroidBelt、JovianSystem）和 4 个星际战役目标（AlphaCentauri、ProximaB、TauCetiFoundry、SiriusResourceBelt）。描述、域与成本总体形成“轨道探索→资源支点→深空战役”梯度；星际战役持续消耗 Food、RocketFuel、物流与高级材料，舰队维修也消耗 TitaniumAlloy、Composite、PhantomWeave 和 RocketFuel，符合经营系统导向。

`SECTOR-P1-001` 使这条链尚不可验收：完成本地殖民会无条件清空共享 Campaign 状态，即使另一星际战役正在进行。该问题必须先明确是否允许殖民/战役并行，再把状态清理限定到对应 operation。当前模拟明确不模拟 Sector occupation/campaign，因此不能用其领土或后期节奏结论验收战役。

Sector 长期输出应继续保持原料、领土、一次性奖励和有限战略流量，不应取代玩家建筑的高级生产。`OrbitalResourceExtractionArray` 已是玩家建设的聚合设施，未来修复效果粒度时不应反向新增一组无限 Sector 工厂。Ultra/Archotech 也不得在战役正确性、补给反馈和维修恢复未验证前批量扩展。

## 15. 跨系统矛盾和文档矛盾

1. 当前资产与静态闭包为 40/65/128/82，而当前模拟摘要为 40/69/121/73（`REPORT-P1-001`）。
2. 锁定 Unity 为 `2022.3.62f2c1`，项目元数据为 `2022.3.62f3c1`（`BUILD-P2-001`）。
3. 当前 Mode 0 报告已晚于 `WorkshopUpgrade.cs` 修复并证明 Test Runner 可执行，但 554 项中仍有 2 项失败；它不能代表完整 EditMode 或任何 PlayMode 验收。
4. `progression-roadmap.md`/`balance-model.md` 仍要求 workforce，而当前规则与运行时采用人口→生产力（`DOC-P2-001`）。
5. 路线图叙述人类文明，运行时使用“鼠托邦/鼠族”（`TEXT-P2-001`）。
6. ResearchTree 专项门槛仍以 79 节点为成功标准，当前资产是 128 Research（`TEST-P2-001`）。
7. 当前三项资产重复 Food 效果，而定义测试源码明确禁止同目标重复（`ECON-P1-002`）。
8. Resource.TechLevel 有时表示主要用途、有时似乎表示来源，UI 却直接用它排序（`RESOURCE-P2-001`）。
9. Workshop 的四段玩家描述与实际 modifier 指向不同系统（`WORKSHOP-P2-002`）。

处理原则：遵循当前代码/资产与高优先级规则；较低优先级文档冲突只记录并等待独立治理批次，不通过回退 Runtime State、重引 workforce 或改稳定 ID 来“对齐”。

## 16. 分阶段实施路线图

### Wave 0：恢复可复现证据链

1. `COMPILE-P1-001` 已按最小边界恢复字符串与编码且未重排 enum；Unity EditMode 已进入测试执行，后续收敛剩余失败并保留 XML/日志。
2. 用户确认 Unity `f2c1/f3c1`；用最终版本取得当前 HEAD 的真实编译、Console、EditMode 和明确 PlayMode XML。
3. 修复 `REPORT-P1-001` 的快照版本，计数必须与 40/65/128/82 一致；仍不扩展模拟策略。
4. 更新 ResearchTree 专项目标口径并取得运行日志；同步 workforce 等治理文档冲突。

### Wave 1：玩法正确性与当前纵向切片

1. `SAVE-P0-001` Workshop 拓扑恢复。
2. `RESEARCH-P1-001` 原子支付与失败回滚。
3. `ECON-P1-001` 多输入满足率改为最小瓶颈。
4. `ECON-P1-002` 收敛重复 Food 效果。
5. `SECTOR-P1-001` 限定战役状态清理范围。
6. 复核 `POP-P2-001` 与 `ECON-P2-004`，取得运行断言。

### Wave 2：反馈、教程与移动端体验

1. 运行验证已修复的研究 blocker，以及 Workshop 渐进路线、真实阻碍和行复用；四项描述—效果冲突仍需产品逐项决策。
2. 生产链箭头/端点、按步骤页面访问门与 Era 同页空操作已修；下一步补非经济完成反馈，并验证新游戏 1 分钟 bootstrap。
3. 解决时代显示名、资源 TechLevel 语义与人类/鼠族产品身份。
4. 在 P40 验证安全区、触控、研究图手势、长文本、字体和非颜色反馈。

### Wave 3：行为正确后的数值节奏

1. 用当前定义重跑闭包与冻结策略的模拟；先记录再调参。
2. 调整 Food、幸福、人口、研究时长和建筑回本；处理多产物效果粒度。
3. 对 Fast/Normal/Conservative 分别记录里程碑、资源流与失败原因，不把模拟当 Unity 验收。

### Wave 4：Spacer、Sector 与后期闭环

验证探索→殖民→星际战役→维修→占领收益的完整闭环；确认 Sector 不替代玩家高级生产；再收敛 47 Research/46 Workshop 的路线密度和重复价值。

### Wave 5：远期愿景与表现

只在前述门通过后定义 Ultra/Archotech 最小循环、完成音乐清权/分类、遗留美术处置、音效与可访问性完善。不得同时实施所有 Wave。

## 17. 高优先级问题的实施简报

| 顺序 | 发现 | 最小实施边界 | 必须验收 |
|---:|---|---|---|
| 1 | `COMPILE-P1-001` | 已恢复 `WorkshopUpgrade.cs` 三个字符串边界与乱码说明，枚举名和值不变 | 指定 Unity 版本真实编译、Console、enum 序列化断言 |
| 2 | `SAVE-P0-001` | Workshop 恢复采用拓扑序或两阶段提交，不放宽 ID 校验 | 构造含 33 条逆字典序前置的合法存档；主/备份/重复加载通过 |
| 3 | `RESEARCH-P1-001` | 开始前汇总验证全部资源，再统一扣款；旧部分付款明确迁移/退款 | 多资源任一不足时零扣款；成功只扣一次；保存/加载幂等 |
| 4 | `ECON-P1-001` | 多输入资源满足率取最小值，电力/物流约束保持现有组合规则 | 1、2、3 个输入的瓶颈测试与 Unity tick；普通资源不为负 |
| 5 | `ECON-P1-002` | 三资产每个同目标只保留一条 Food 效果，不先猜节奏 | 唯一目标测试、合成倍率、闭包、当前模拟与 Unity Food tick |
| 6 | `SECTOR-P1-001` | 只清理完成的本地 operation，不触碰无关全局 campaign | 本地殖民与星际战役并行 PlayMode；保存/加载/维修不丢状态 |
| 7 | `REPORT-P1-001` | 用当前定义重建证据，不扩展策略/AI | 40/65/128/82 输入；报告 HEAD/时间；Acceptance 与 Unity 边界清楚 |

推荐按表中依赖顺序分批交付，每批保持精准差异并独立回归；不要把七项合成一次大改。

## 18. 创意机会库

以下是策划机会，不是已确认缺陷；优先级均低于 Wave 0/1。

### 18.1 当前纵向切片必要：时代能力摘要（第一步已实施）

1. 解决问题：玩家难以从大量节点理解本时代会新增什么决策，Neolithic 名称和 Medieval 百分比节点尤甚。
2. 复用内容：现有 TechLevel、跃迁研究、Building/Research/Workshop 效果与 Era 页面。
3. 为何不能先用现有内容：可以且应当只用现有内容；不新增定义，只把能力变化汇总出来。
4. 新决策：玩家可在“人口/食物、研究、材料、物流、电力、战役”之间选择下一条能力线，而非追最近节点。
5. MVP：第一步已在 Era 页面增加一条中性“本时代能力”，覆盖全部七个 TechLevel，并对目前只有单项跃迁或无独立内容的 Ultra/Archotech 如实说明；未来若需要 3～5 条可点击能力路线，再从实际前置和关键效果结构化生成。
6. 负担：UI S～M、内容编辑 S、EditMode/PlayMode M；无新资源。
7. 依赖：当前中性摘要不依赖 `ERA-P2-001`、Resource.TechLevel 语义或文明正典；未来自动生成路线仍依赖明确效果分类，研究 blocker 的源码修复只剩运行验证。
8. 时机：Wave 2；第一步源码与 PlayMode 断言已实施。
9. 验收：各时代摘要与当前资产一致；Ultra/Archotech 不虚构生产循环；Era 页在 P40 横屏不截断；新增 PlayMode 断言通过。玩家是否能在 30 秒内说出目标和替代路线仍需后续可用性测试。

### 18.2 当前系统完成后的高价值改进：Workshop 路线与效果回执

1. 解决问题：已购项隐藏和直接下一层不可见已在源码修复；四项描述仍可能与 modifier 漂移。
2. 复用内容：82 个 Workshop 的 requiredResearch、requiredUpgrades、effects、SortOrder 与现有详情面板。
3. 为何不能先用现有内容：现有数据已经足够，不需要新增 Workshop；缺的是可见路线和结构化回执。
4. 新决策：玩家能比较“现在购买”“为后续铺路”“已购组合”三种投资，而非只买当前可见项。
5. MVP：已进入时代范围内只常驻已购项、根节点和直接下一层，显示锁定/可购/已购、首个前置或资源缺口，并由 effect 生成一行收益；前半部分已实施，结构化收益仍待产品批次。
6. 负担：UI M、无新资产、测试 M；移动端密度需 P40 验证。
7. 依赖：`SAVE-P0-001`、`COMPILE-P1-001`、`WORKSHOP-P2-001/002`。
8. 时机：Wave 2。
9. 验收：任一当前前沿条目能看到首个阻碍，购买后直接下游出现；结构化效果与实际 modifier 相同；已购状态保存/加载后可见。

### 18.3 Spacer/外星战争：战役补给预案

1. 解决问题：战役已有 Food、Fuel、物流、高级材料、时长和伤亡，但玩家缺少投入—风险—恢复的统一预览。
2. 复用内容：9 Sector、Campaign rates、Fleet power、repair costs、现有资源净流和 modifier。
3. 为何不能先用现有内容：可以用现有字段计算；不新增战役资源、战术小游戏或 Sector 工厂。
4. 新决策：玩家比较立即出征、先扩物流、先备维修材料或换目标，而不是点击后才发现资源税。
5. MVP：战役确认面板显示预计每分钟消耗、当前可支撑时长、最低物流、可能伤亡与维修材料；只读预测。
6. 负担：UI/计算 M、测试 M；不改 Sector 定义。
7. 依赖：先修 `SECTOR-P1-001`，并验证 Campaign tick、暂停、保存与维修。
8. 时机：Wave 4，不占用当前纵向切片正确性。
9. 验收：预测与固定 60 秒 PlayMode 实际消耗误差为零或有明确舍入；不足原因可导航到资源/建筑。

### 18.4 Ultra/Archotech 远期：奇点工程项目

1. 解决问题：TechnologicalSingularity 之后没有可玩目标，Ultra/Archotech 只是枚举终点。
2. 复用内容：QuantumComputingArray、InterstellarTheoryNexus、PhaseMaterial、Phantom 材料、Sector 领土与研究力。
3. 为何不能只靠现有内容：现有内容能作为输入，但没有持续选择、完成条件或回报，不能构成新时代循环。
4. 新决策：在研究、材料、能源和星区治理之间分配长期产能，选择奇点工程的阶段顺序与机会成本。
5. MVP：只做一个多阶段工程，3 个互斥阶段顺序、复用现有资源，不先新增资源/工厂；完成后再决定是否进入 Archotech。
6. 负担：设计 L、UI M、内容 M、测试/模拟 L、维护 M。
7. 依赖：所有 Wave 0～4、当前 Spacer 战役闭环、真实终局节奏数据。
8. 时机：Wave 5 以后，仅远期构想。
9. 验收：至少两种顺序在成本/收益上各有合理场景；旧工业资源仍有投入；不靠无限 Sector 高级产出完成。

### 18.5 不推荐的“机会”

不推荐新增普通资源容量/仓库、重引 workforce、为每种工业资源做轨道替代厂、扩展模拟器策略 AI、在当前 Bug 未修前批量增加 Ultra/Archotech 节点，或因遗留龙/Golem 素材存在而反向设计玩法。这些方案要么违反锁定边界，要么增加系统/UI/测试负担却不解决当前根因。

## 19. 不推荐实施或已否决方案

- 普通资源容量、仓库、`MaxAmount` 或隐藏 clamp。
- 重新引入 workforce。
- 推倒重做 BigNumber、Pair、Runtime State、Manager/UI 分离或稳定 ID。
- 为每种工业资源建立轨道替代工厂，或让 Sector 无限替代玩家高级生产链。
- 在当前纵向切片与行为未验收前批量扩展 Ultra/Archotech。
- 为让旧模拟报告变绿而直接调数值，或扩展模拟策略/路线评分/决策 AI。
- 用静态代码意图、测试源码或模拟结果冒充 Unity/PlayMode/P40 验收。
- 为修正“新石器时代”名称而立即新增铜石、青铜、铁器多个 TechLevel。
- 把 `WORKSHOP-P2-002` 四项一律只改文案而不确认真实设计意图。
- 拆分所有多产物建筑；优先用现有 Resource multiplier 修正专业效果粒度。
- 提高幸福上限以容纳误写的 `HappinessBonus=1.06`。

## 20. 待运行验证清单

1. 使用当前已修复的 `WorkshopUpgrade.cs`，在 Unity `2022.3.62f3c1` 已执行 Mode 0 EditMode（554 total、552 passed、2 failed）；该报告早于教程生产链端点、按步骤页面访问和直接前驱完成回执等新增断言，仍须复跑并保留 Console/XML，当前尚未全绿。
2. 明确执行 16 个当前 PlayMode 方法并保留 XML/日志；Mode 0 不能替代。
3. 执行与当前 Research 数量口径一致的 `ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping`，证明唯一网格、正 viewport/content、真实 content movement、拓扑与每轴溢出日志。
4. 验证页面停用后模拟和音乐继续、恢复时 UI 全量刷新；验证研究 blocker 与 Workshop 三态。
5. 真实计时新游戏 1/10 分钟、第一座建筑、第一次资源短缺、研究失败/成功原子支付和时代跃迁。
6. 验证 Food 满载、人口离开/恢复、电力/物流瓶颈、多输入建筑和多产物 modifier 的实际 tick。
7. 验证主/备份、逆字典序 Workshop、重复加载、旧版迁移、后台/恢复、24 小时离线与长帧停顿。
8. 完整跑通本地探索、殖民与并行星际战役、补给、伤亡、维修、占领和保存/加载。
9. 用 Huawei P40 Pro 横屏验证安全区、触控目标、节点/空白拖动、短按、双指缩放、滚动、长中文、音量、耗电与热量。
10. 未来在允许实施时重新生成与 40/65/128/82 当前资产一致的闭包/模拟证据；本轮未运行。
11. 在 Unity 中验证本轮文本资产、教程摘要、按步骤页面访问门的导航顺序、字体字形、换行、截断和详情面板高度。

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。

本轮未运行闭包检查、离线模拟器、EditMode 或 PlayMode；只读取了现有证据。

## 21. 需要用户决策的问题

1. Unity 锁定版本最终采用规则中的 `2022.3.62f2c1`，还是当前项目元数据的 `2022.3.62f3c1`？
2. 产品身份最终是“人类文明”，还是“鼠托邦/鼠族”？这会决定教程、默认王国名、时代叙事和宣传文本。
3. Neolithic 是否改显示名/副标题为更宽的“定居与早期金属时代”，还是保留“新石器时代”并接受压缩历史边界？
4. `Resource.TechLevel` 表示首次来源、主要用途，还是纯 UI 分类？必须选定一个契约。
5. `InterstellarAutonomyCharterTheory` 的 HappinessBonus 是否应为 `0.06`？当前 `1.06` 会直接封顶。
6. 四项 `WORKSHOP-P2-002` 以描述还是当前效果为最终设计权威？需要逐项决策，不能批量猜测。
7. Ultra/Archotech 在早期纵向切片完成前是否仅保留远期框架？本审查建议保留现有单项跃迁闭包，不继续批量扩展。
8. 音乐与遗留美术素材的授权/来源是否已有项目外部台账？仓库静态文件无法独立证明授权。

## 22. 变更记录

- 2026-08-24：清理 PageHost 无用壳层：删除各页面 Heading，删除 Overview 的 DataRows，删除 Research 的 ResearchTreeToolbar 及其运行时绑定；Overview 现在直接由 PrimaryCard/SecondaryCard 承载，其他页面继续使用各自 DataRows。`git diff --check` 通过；未执行真实 Unity 编译、PlayMode 或 P40 验收。

- 2026-08-24：Overview 引导改为纯文本概述：移除运行时创建/绑定引导卡片按钮的逻辑，并禁用场景中已有的该卡片按钮；时代页及时代导航的跳转逻辑未改。同时移除 Overview 引导文本的 `fontSize`、`fontSizeMin/Max` 与自动缩放硬编码，字号改由场景 Prefab 控制。`git diff --check` 通过；需 Unity/P40 验证 Prefab 字号、长文本高度与时代按钮跳转。
- 2026-08-24：Overview 引导卡片字号从 `24` 调整为 `30`，自动缩放上限同步为 `30`；最小字号、换行、文本内容和其他页面不变。`git diff --check` 通过；需 Unity/P40 验证实际容纳高度与长文本显示。
- 2026-08-24：统一 Overview 工坊提示术语：从中英混用的 `Workshop` 改为与标题及其他页面一致的“工坊”；未改变提示条件、购买、前置、效果或 State。CLI 编译 0 错误、5 个既有警告；`git diff --check` 通过。
- 2026-08-24：Overview 的 Workshop 可用提示从泛化的“查看资源需求与效果”，改为说明 Workshop 是基础建设之外的渐进发展路线；未引用存在冲突风险的定义描述，不改变购买、前置、效果或 State。`dotnet build Kingdom.Runtime.csproj --no-restore --nologo` 0 错误、5 个既有警告；`git diff --check` 通过。
- 2026-08-24：修正 Overview 通用研究进行中提示：从“保持研究力与资源供应”改为“保持研究力，并查看完成后的建筑/生产链/时代条件解锁”；研究资源仍按现有规则在开始前原子支付，未改变研究 State、进度、成本或结算。`dotnet build Kingdom.Runtime.csproj --no-restore --nologo` 0 错误、5 个既有警告；`git diff --check` 通过。
- 2026-08-24：对本轮 Tutorial fallback 与 Orientation 引导改动执行 `dotnet build Kingdom.Runtime.csproj --no-restore --nologo`，0 错误、5 个既有 `SectorDefinition` 序列化字段警告；`git diff --check` 通过。未执行 Unity 编译、PlayMode 或 P40 真机验收。
- 2026-08-24：Animal 开局 Orientation 引导从仅提示“推进一天”，改为说明等待期间可查看资源页观察原木与食物变化；同样同步正式资产、fallback 描述和运行时推荐行动。未改变一天完成门槛、时间速度、页面访问门或任何经济 State。`git diff --check` 通过；首分钟真实体验仍待 Unity/PlayMode 验证。
- 2026-08-24：补齐 `TutorialManager.BuildDefaultSteps()` fallback 的 8 个 `NarrativeText`，并同步正式资产的完成条件与导航字段；资产加载/链条校验失败时仍能显示完整的“背景—目标—行动”引导。正常资产路径、State、经济数据和推进逻辑不变。`git diff --check` 通过；Unity/PlayMode/P40 待验证。
- 2026-08-24：修正内置教程 fallback 链中 Population/ProductionChain 的导航页：从构造函数默认的 `Overview` 同步为正式资产使用的 `Buildings`，避免资产加载失败时页面访问门把玩家错误带回概览；不改变正常资产路径、步骤链、完成条件或 State。`git diff --check` 通过；Unity/PlayMode/P40 待验证。
- 2026-08-24：同步 `TutorialManager.BuildDefaultSteps()` 的 8 条 fallback 描述，使资源加载失败或教程链校验失败时仍使用与当前 Tutorial 资产相同的行动目标；仅改 fallback 文本，未改变步骤链、触发/完成条件、导航、奖励或 State。`git diff --check` 通过；Unity/PlayMode/P40 待验证。
- 2026-08-24：Overview 引导卡片在叙事文本后增加“完成方式：”标签，明确区分背景说明与当前步骤的行动目标；仅改变 UI 文本前缀，不改变内容、刷新顺序、导航、State 或教程推进逻辑。`git diff --check` 通过；Unity/PlayMode/P40 待验证。
- 2026-08-24：`TutorialManager.GetCivilizationContext()` 从各时代的泛化背景句，改为引用当前已有能力的短叙事：Animal 的食物/原木/定居、Neolithic 的灌溉/储粮/文字、Medieval 的行政/贸易/城市、Industrial 的电力/铁路/机器、Spacer 的轨道/航行/舰队，并明确 Ultra/Archotech 仍属远期框架；仅改显示文本，未新增时代、资源、研究或玩法。`git diff --check` 通过；Unity/PlayMode/P40 待验证。
- 2026-08-24：教程 8 个步骤的 `Description` 从泛化目标改为与现有完成条件和导航一致的行动说明：推进一天、查看资源、建造建筑、观察人口增长、完成研究、连接生产链、完成时代条件和继续长期目标；仅修改文本资产，未改变条件、State、导航、奖励或经济数据。`git diff --check` 通过；Unity/PlayMode/P40 待验证。
- 2026-08-24：新手引导前两步的推荐行动从“查看资源/建筑页面”改为明确说明因果：先查看原木净产出为第一座建筑准备材料，再选择能解决当前阻碍的推荐建筑；仅修改 `TutorialManager.BuildDetails()` 的显示文本，不改变推荐对象、阻断判断、导航、State 或完成条件。`git diff --check` 通过；Unity/PlayMode/P40 待验证。
- 2026-08-24：教程背景文本从八条相互独立的复兴标语，改为沿“时间与资源 → 建筑 → 人口 → 研究 → 生产链 → 时代条件 → 长期扩张”逐步承接的叙事；仅修改 8 个 Tutorial 资产的 `NarrativeText`，未改变 ID、触发条件、完成条件、导航、奖励、经济数据或 State。`git diff --check` 通过；Unity/PlayMode/P40 待验证。
- 2026-08-24：`GameManager.AdvanceFood()` 的默认无容量路径从每次调用解析字符串上限，改为复用静态 `UncappedFoodCeiling`；`GameState.BaseFoodProductionRate/BaseFoodCapacity` 也从属性内重复构造改为静态值。Food 上限选择、容量 Clamp、非负约束和结算顺序不变。`git diff --check` 通过；CLI 编译仍受本机 SDK 目录访问拒绝阻断，Unity/PlayMode/P40 待验证。
- 2026-08-24：人口 tick 的固定 `ExpantaNum` 值从属性访问/离开计算时重复构造，改为 `PopulationState` 内部静态只读值；同时把战役剩余两个 `/60d` 分支统一复用既有 `Sixty` 常量。人口增长、生产力、食物消耗、超容量离开、食物短缺离开公式及取整顺序不变。`git diff --check` 通过；CLI 编译仍受本机 SDK 目录访问拒绝阻断，Unity/PlayMode/P40 待验证。
- 2026-08-24：`CampaignManager` 活动战役公式中的 0.3、0.35、0.25、0.65、0.75 与 60 秒常量从每次计算重复构造，改为静态只读 `ExpantaNum` 常量；从每个战役 tick 重复创建不变数值对象变为初始化时创建一次，公式分支、运算顺序、输入输出和战役 State 不变。`git diff --check` 通过；CLI 编译仍受本机 SDK 目录访问拒绝阻断，Unity/PlayMode/P40 待验证。
- 2026-08-24：资源详情刷新从分别遍历全部建筑状态两次（生产者一次、消费者一次），改为一次遍历同时汇总两类流量，再分别按原速率/ID规则排序；`AddResourceBuildingFlow()` 保留原先的缩放、资源倍率、幸福度奖励和非正值过滤顺序，未改变正文、净变化或 State。静态检查通过；理论上该路径的建筑状态扫描由 2 次降为 1 次，CLI 编译仍受本机 SDK 目录访问拒绝阻断，Unity/PlayMode/P40 待验证。
- 2026-08-24：选中建筑详情的实时刷新从同一次刷新内重复读取 `BuildingManager.Instance` 三次，改为先解析一次并复用局部引用；从重复单例 getter/空判断变为一次解析，未改变详情刷新触发版本、升级判定、流量计算、需求行、文本或滚动状态。`git diff --check` 通过；CLI 编译仍受本机 SDK 目录访问拒绝阻断，Unity/PlayMode/P40 待验证。
- 2026-08-24：资源与建筑列表首次构建从每行重复读取 `ResourceManager.Instance`/`BuildingManager.Instance`，改为每次页面构建保留一个局部 Manager 引用；从重复单例 getter 与空判断变为一次解析后复用，未改变定义排序、可见性、数量、升级/拆除条件、按钮回调或 State。`git diff --check` 通过；CLI 编译仍受本机 `C:\Users\19603\AppData\Local\Microsoft SDKs` 访问拒绝阻断，Unity/PlayMode/P40 待验证。
- 2026-08-24：修复研究页首次打开时顶部研究队列可能不同步：从页面构建期间提前消费 `researchQueueUiDirty`，改为仅在 `researchQueueViewport`、`researchQueueContent` 与 `ResearchManager.Instance` 均就绪后消费；`SetupResearchQueueGraphic()` 绑定 authored 队列后保留一次刷新请求，确保首次绑定不会丢失当前活动研究与排队研究。未改变研究状态、队列顺序、支付、研究线拓扑或模拟时钟；`git diff --check` 通过，Unity/PlayMode/P40 待验证。
- 2026-08-24：研究教程推荐从无序遍历 `ResearchManager.States.Values` 改为按 `DataBase<Research>.All` 的稳定 ID 顺序选择首个 `Available` 研究；活动研究优先级、可用状态集合、前置、支付和完成条件不变，避免状态重建后推荐项跳变。`git diff --check` 通过，Unity/PlayMode/P40 待验证。
- 2026-08-24：研究批量连接器补齐 Unity `Image.GenerateSimpleSprite` 的 Sprite padding 几何：从把包含透明边缘的曲线/箭头纹理整块拉伸，改为按 `DataUtility.GetPadding` 缩进绘制矩形；保留 `OuterUV`、拓扑、节点位置、批量数量和层级策略不变。布局诊断继续使用未缩进的逻辑矩形；`git diff --check` 通过，需 Unity 运行确认材质比例与曲线方向。
- 2026-08-24：Sector 页面从行构建、详情刷新和操作回调中的重复 `FindObjectOfType` 场景扫描，改为复用既有 `CacheRuntimeManagers()` 缓存；从每次路径扫描变为首次解析后复用，未改变 SectorManager 调用、失败提示、State 或存档行为。本轮 `git diff --check` 通过；C# 编译因本机 `C:\Users\19603\AppData\Local\Microsoft SDKs` 访问被拒而未完成，Unity/PlayMode/P40 仍待验证。
- 2026-08-24：性能批次在 `GameManager.Tick` 与 `TickOffline` 中缓存本次人口增长率和粮食短缺判定；从同一结算路径重复读取/判断改为各读取一次，未改变 State、人口、食物、日历结算顺序。`Kingdom.Runtime.csproj` 编译 0 错误、5 个既有序列化字段警告；Unity/PlayMode/P40 仍待验证。
- 2026-08-24：研究树先恢复到最新 `HEAD` 快照，再仅修复 `UIResearchConnectorBatch` 的显式左下角尺寸契约与普通/高亮层级顺序；未改变研究线拓扑、UV、矩形公式或批量渲染策略。`git diff --check` 通过，真实 Unity 视觉验证仍待执行。
- 2026-08-23：首次创建主审查并完成项目快照、证据边界和覆盖矩阵。
- 2026-08-23：完成运行时、交易、存档、Sector、教程、UI、音画与测试源码静态审查；首次记录 P0=1、P1=6。
- 2026-08-23：按用户追加要求完成 128 Research、82 Workshop、65 Building 的时代/前置/描述/效果交叉审查；新增 `ECON-P1-002`、`ECON-P2-003/004`、`ONBOARD-P2-001`、`ERA-P2-001/002`、`RESOURCE-P2-001`、`WORKSHOP-P2-002`。
- 2026-08-23：用户放宽只读范围后，直接修复 25 个定义资产的 30 行名称/描述文本，记录为 `TEXT-P3-003`；该资产批次未改 ID、GUID、前置、效果或数值。
- 2026-08-23：按同一简单文本授权恢复 `WorkshopUpgrade.cs` 的乱码与三个字符串边界，以及 `TutorialManager.cs` 的生产链箭头；未改枚举成员/值或玩法逻辑。`COMPILE-P1-001`、`TEXT-P3-002` 转为静态已解决，当前未解决计数为 P0=1、P1=5。
- 2026-08-23：完成玩家旅程、分阶段路线图、实施简报、创意机会与对抗性边界收敛；剩余均为明确的运行/真机/产品决策项。
- 2026-08-24：性能优先批次实施 `SIM-P2-001`：Player 不再截断超过两秒的真实积压，每帧仍最多 20 tick，并新增五倍单帧预算跨五帧排空的 EditMode 回归测试。静态差异检查通过；真实 Unity 启动因审批服务 503 未执行。低风险 CLI 编译在临时补齐被忽略的过期 `.csproj` 对既有 `EraGoalEvaluator.cs` 的漏项后通过，3 个程序集、0 错误、5 个既有序列化字段警告；临时项目文件补项随后已移除，该结果不替代 Unity 编译或 Test Runner。
- 2026-08-24：按用户新增的荣耀 X50 打包基准检查现有 `Kingdom.apk`：Manifest 为 minSdk 22、target/compileSdk 34，但原生库仅有 `armeabi-v7a`，属于 32 位包；项目现已配置 targetSdk 35、ARM64、IL2CPP Release、自动安装位置、Android Low managed stripping 和 2.4 最大长宽比。包名、版本号、最低 SDK 与签名配置未改；必须重建后再用 `aapt2` 证明新 APK 含 `arm64-v8a`，当前旧 APK 不能作为修复完成证据。
- 2026-08-24：读取用户当时运行的 EditMode 报告：548 total、541 passed、7 failed。已修复 TutorialManager 在 EditMode 误用 `DontDestroyOnLoad`、测试单例清理、Research 测试 BaseCost 夹具及 PrecisionManufacturing 文案断言回归，CLI 编译 0 错误；修复后由用户复跑。
- 2026-08-24：读取用户复跑的最新 EditMode 报告：554 total、552 passed、2 failed；剩余失败为人口净变化测试缺少 BuildingManager 夹具，以及 Workshop 非拓扑顺序恢复缺少状态项。世界观/引导阶段首先把生产链摘要从可能的“粗石 → 粗石”修为“粗石 → 石砖”，链条拥有判定和教程完成条件不变；新增精确 Editor 回归测试，三个 C# 项目编译 0 错误、5 个既有警告，新测试仍待用户用 Unity Test Runner 执行。
- 2026-08-24：为 8 个教程步骤增加按活动步骤 ID 隔离的页面访问门：从“State 满足即可推进”变为“访问当前步骤 NavigationPage 且原 State 条件满足才推进”。错误页和前一步访问不继承，载入/新游戏清空瞬时访问，不改原完成公式、存档 DTO、经济 State 或定义资产；新增 Editor 回归测试，三个 C# 项目编译 0 错误、5 个既有警告，Unity Test Runner/PlayMode 待用户执行。
- 2026-08-24：修复 EraGoal/LongTerm 在 Era 页显示可点击同页空操作：从“打开时代页面”且点击无结果，改为不可交互的“查看下方时代条件与当前主要阻碍”；扩展 Onboarding PlayMode 断言，三个 C# 项目编译 0 错误、5 个既有警告，真实 PlayMode 待用户执行。
- 2026-08-24：教程完成反馈从“步骤静默推进”改为概览显示“上一步已完成：直接前驱标题”；完全复用 `CompletedStepIds` 与 `NextStepId`，不新增奖励、存档字段或经济 State 变化。新增 Editor 回归测试；最新 554 项 EditMode 报告早于该用例，仍待用户复跑。
- 2026-08-24：Era 页从只显示时代名、教程和推进条件，增加一行不依赖文明正典的“本时代能力”；七个摘要均依据当前 128 Research、65 Building、82 Workshop 内容，Ultra/Archotech 明确标注内容边界。未改 TechLevel、定义资产、效果或数值；三个 C# 项目 CLI 编译 0 错误，新增 PlayMode 文本断言尚未由 Unity Test Runner 执行。
- 2026-08-24：建筑教程阻碍从泛化的“缺少能解决问题的建筑”改为与 `TryBuild` 一致的领土→生产力→资源首阻碍；资源不足时使用同一施工倍率显示下一座真实缺口，并按当前净产出显示约等待秒数，全部满足时明确提示可以建造。只读现有 State，不改库存、产量、成本、购买条件或存档；三个 C# 项目 CLI 编译 0 错误，真实新游戏和 PlayMode 待验证。
- 2026-08-24：复核发现研究支付 blocker 覆盖已在当前源码中由互斥分支修复；进一步把研究需求行从“已支付/需求（库存）”改为逐项显示“缺 X / 可支付 / 已支付”。仅改变显示格式，未改研究成本、队列、付款 State 或存档；三个 C# 项目 CLI 编译 0 错误，PlayMode/P40 待验证。
- 2026-08-24：Workshop 从“仅显示可购买项、购买后消失”改为性能安全的渐进路线：已进入时代范围内保留已购项、根节点和直接下一层，行内显示“已拥有 / 可购买 / 需研究 / 需工坊 / 资源不足”，资源不足可打开详情查看具体缺口；不一次铺出全部 82 项。刷新继续复用行池并清除旧监听，研究与购买事件会标记路线刷新。三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：研究教程推荐从直接显示 `ResearchEffectType` 英文枚举名改为使用中文 `Description`；补齐全部 30 个研究效果及 Workshop 全局粮食效果的玩家可见中文名。枚举成员、显式整数、资产效果和运行时分支不变；三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：研究详情从“自由文本描述 + 成本”改为追加“效果类型 / 目标 / 数值”的结构化摘要，直接读取现有 `Research.Effects` 并复用中文描述；同时用 `StringBuilder` 收敛原多段字符串拼接。未改研究效果、成本、队列、支付或存档；三个 C# 项目 CLI 编译 0 错误，PlayMode/P40 待验证。
- 2026-08-24：研究详情进一步追加“直接前置研究 / 当前状态”，直接读取现有前置和 State；没有把前置状态混入支付 blocker，保留测试明确要求的指定后续研究提前付款行为。未改前置、队列、支付或存档；三个 C# 项目 CLI 编译 0 错误，PlayMode/P40 待验证。
- 2026-08-24：Workshop 详情从只显示描述、效果和资源成本，改为同时列出全部直接研究前置与工坊前置及其当前状态；列表仍只承担首个 blocker 和渐进揭示。未改 `TryPurchase`、前置、购买顺序、效果或存档；三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：建筑详情从只显示描述、数量、时代、土地、生产力、成本和产消，改为同时列出全部直接研究前置与工坊前置及当前状态；首次打开与实时刷新统一复用 `SetBuildingDetailBody()`。未改 `TryBuild`、建造前置、成本、产消或存档；三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：Workshop 详情前置状态从“仅打开详情时生成、研究完成或购买后可能停留旧值”，改为复用现有研究/购买事件脏标记，在 Workshop 页停止滚动后的首轮刷新正文；拖动期间不重复生成字符串。未新增轮询，未改 `TryPurchase`、前置、效果、资源或存档；三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：星区列表摘要从“首次构建后保持静态”，改为只在 Sectors 页可见且停止滚动时每秒更新现有 9 个 Subtitle 引用，使探索/战役进度、攻防、战斗比率和舰队生存倍率随 Runtime State 显示；不重建行、不重绑按钮、不刷新隐藏页。未改 SectorManager、战役、补给、奖励或存档；三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：已打开的星区详情从“只在点击或操作回执时生成”，改为与列表共用可见、非滚动的 1 秒刷新节奏，使预计剩余时间、补给、推进速度、伤亡和满意度随 Runtime State 更新；正文复用单一 `StringBuilder`，操作按钮只在锁定、活动、完成或维修等离散状态变化时重绑。切换到其他详情会清除星区选择，未改任何 SectorManager 行为、补给公式、奖励或存档；三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：已占领星区从仍可能显示“开始探索/开始远星战役”并被 Manager 以 `AlreadyOccupied` 拒绝，改为保留正文“已占领”状态且隐藏无效操作按钮；当前 9 个 Sector 的 `repeatable` 均为 false。只删除不可达 UI 命令，未改占领、探索、战役、持续产出或存档；三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：星区详情从只显示领土与一次性资源回报，改为同时显示 9/9 当前星区都已配置的“当前占领持续产出”；显示值读取 `OccupiedResourceRatesPerSecond` 并应用与 `TickOccupiedResourceProduction()` 相同的全局倍率。资源成本/速率格式化改为复用 256 字符缓冲，未修改 Sector 资产、持续产出数值、倍率、结算或存档；三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：已占领星区的列表摘要从继续显示“探索/远星战役、战斗比率、100% 进度”，改为显示“已占领 / 当前持续产出”；与详情复用同一有效产出字段、全局倍率和格式化缓冲。占领分支在本星系攻防和 `GetCampaignPreview()` 之前提前返回，不再为已结束行动计算无意义 Preview；未改任何星区状态、产出或战役逻辑，三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：星区详情状态从“任意正进度统一显示进行中”改为按现有 `SectorState` 区分未解锁、已解锁、探索中、远星战役中、等待维修、等待占领、已暂停与已占领；暂停状态不再冒充活动行动，完成/维修状态也能解释当前按钮。只读取既有字段，未改进度衰减、活动标记、占领或维修逻辑；三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：星区详情从不显示路线依赖，改为列出全部直接前置星区及“已占领/未占领”状态；当前 1 个根节点显示“无”，其余 8 个星区各显示 1 个直接前置，状态随现有详情刷新更新。标题明确限定为“直接前置”，不冒充发射中心或星际路线等完整解锁判定。复核同时否定了“活动战役不能维修”的假设：`TryRepairFleetForState()` 允许维修当前同一目标，故保留伤亡后维修按钮；未改 Manager、定义、路线或存档，三个 C# 项目 CLI 编译 0 错误，Unity/P40 待验证。
- 2026-08-24：未解锁星区从“按钮统一显示解锁星区、点击后才得知失败”，改为直接复用 `SectorManager.GetUnlockFailure()` 显示并禁用当前真实 blocker；`TryUnlock()` 也复用同一查询，原 `UnknownSector → AlreadyUnlocked → PrerequisiteNotOccupied → LaunchCenterRequired → InterstellarSystemLocked` 失败顺序和成功提交保持不变。发射中心检查从每次详情刷新都 `FindObjectOfType<BuildingManager>()` 扫描场景，改为首次解析后缓存、对象失效时再解析；通用详情按钮配置会恢复可交互状态，避免禁用的星区按钮污染后续真实动作。三个 C# 项目 CLI 编译均为 0 错误，Runtime 仅有 5 个既有 `SectorDefinition` 序列化字段警告；本轮未运行 Unity Test Runner，Unity/P40 待验证。
- 2026-08-24：音乐页时间标签从“10 Hz 刷新时每次都创建曲名、当前时间和总时长的完整字符串，即使整数秒显示未变化”，改为先比较曲名、当前整数秒和总整数秒，只在任一可见值变化时调用原 `FormatMusicTime()` 和 `SetTextIfChanged()`；稳定播放时格式化路径由最高约 10 次/秒降为约 1 次/秒。页面重建会显式失效秒缓存；进度滑块仍按原 10 Hz 更新，播放、暂停、定位、总时长、文本格式和 Manager 状态均未改变。三个 C# 项目 CLI 编译均为 0 错误，Runtime 仅有 5 个既有警告；当前没有新的 `Temp/KingdomPerf.log`，因此该频率结论是静态可证的理论改进，不冒充运行时毫秒或 GC 实测，本轮未运行 Unity Test Runner，Unity/P40 待验证。
- 2026-08-24：运行时离线结算的每个 60 秒步长从重复读取 `BuildingManager.Instance` 2 次、`GameManager.Instance` 6 次、`ResourceManager.Instance` 4 次和 `ResearchManager.Instance` 1 次，改为在该步首次使用时分别保留局部引用，访问次数变为 1/1/1/1；24 小时上限共 1,440 步，静态上最多省去 12,960 次重复 Singleton getter。局部引用没有跨步保存，因此研究完成等同步事件之后的下一步仍会重新解析 Manager；离线步长、衰减有效秒数、Building→Game→Resource→Sector→Research 顺序、State、参数和返回值均未改变。三个 C# 项目 CLI 编译均为 0 错误，Runtime 仅有 5 个既有警告；当前没有新的 `Temp/KingdomPerf.log`，该项只记录为低风险常数 CPU 优化，不声称可感知毫秒收益，本轮未运行 Unity Test Runner，Unity/P40 待验证。
- 2026-08-24：在主要切页卡顿已由用户实测消失、当前又没有新性能日志的前提下，性能工作暂不继续扩张低频微优化，转回世界观与引导。生产链教程从“可用生产力大于零但尚无相连建筑时把空 `chainSummary` 直接显示为 blocker”，改为明确显示“尚未拥有一组相连的生产与加工建筑”；已有生产链仍显示原摘要，生产力不足分支、`HasOwnedProductionChain`、页面访问门、步骤推进、推荐页面、State 与存档均未改变，文本不依赖人类/鼠族正典。三个 C# 项目 CLI 编译均为 0 错误，Runtime 仅有 5 个既有警告；本轮未运行 Unity Test Runner，Unity/P40 待验证。
- 2026-08-24：Orientation 教程从“玩家已访问概览、`CalendarDays` 仍为 0 时显示‘暂无阻碍’”，改为显示“王国时间尚未推进满一天”；新游戏仍从第 0 天开始，完成仍严格使用既有 `CalendarDays >= 1`，未访问页面时仍由原页面门优先提示，达到首日后仍按原顺序推进到资源步骤。未改日历速度、教程资产、步骤公式、State、存档或文明正典；三个 C# 项目 CLI 编译均为 0 错误，Runtime 仅有 5 个既有警告，本轮未运行 Unity Test Runner，Unity/P40 待验证。
- 2026-08-24：Orientation 推荐行动从已在概览时仍提示“查看概览”改为“让王国时间继续推进，观察人口与核心资源的变化”；概览引导卡从允许点击并重复执行当前页 `SetPage` 改为目的地等于当前页时不可交互，其他跨页导航仍保持原行为。未改 `CalendarDays >= 1`、时间速度、步骤推进、State、存档或文明正典；三个 C# 项目 CLI 编译均为 0 错误，Runtime 仅有 5 个既有警告，Unity Test Runner/P40 待验证。
- 2026-08-24：人口教程在尚未访问其定义页面且食物/幸福度不足时，从“blocker 要求查看步骤页、按钮却先导航到不会满足页面门的资源页”改为先导航到资产定义的建筑页并说明查看人口容量/变化；建筑页访问完成后，既有食物不足分支仍恢复导航到资源页。未改 Population 完成条件、页面访问判定、食物、幸福度、人口 State 或存档；三个 C# 项目 CLI 编译均为 0 错误，Runtime 仅有 5 个既有警告，Unity Test Runner/P40 待验证。
- 2026-08-24：核对七个“本时代能力”摘要与当前 Research/Building/Workshop 后未发现需要改写的事实错配；另将资源教程资产、内置回退步骤和推荐行动中的“木材”统一为起始资源 `WoodLog` 在资源页的实际名称“原木”。未改资源 ID、Label、产量、教程完成条件或文明正典；三个 C# 项目 CLI 编译均为 0 错误，Runtime 仅有 5 个既有警告，Unity Test Runner/P40 待验证。
- 2026-08-24：正典清点确认运行时默认名、七个教程 NarrativeText 与全时代语境主要采用“鼠族”，路线图采用“人类”；同时把唯一直接混入 Research 描述的 `FirstContact` 从“建立人类与星际文明的正式接触”中性化为“建立王国与星际文明的正式接触”。只改 Description，不改 Research ID/GUID、成本、前置、效果、坐标或玩法；闭包/模拟按用户要求留到最终批次，C# 与 Unity/P40 验证待本批次收口。
