# Kingdom 功能与按钮专项审查

审查日期：2026-10-09。范围：从新档开局到文明工程、远星战役、遗迹与剧情的玩家可见页面、按钮、筛选、详情操作和必要辅助功能。目标是减少无意义或误导操作，补齐能直接改善经营因果理解和恢复能力的入口。本轮只读源码、Prefab、UI契约和测试；没有修改代码、资产、数值或存档。

## 1. 判断原则与证据边界

按钮不是因为调用相同 Manager 就自动重复。列表快捷操作和详情决策操作可以同时存在，前提是状态、文案、可用性和结果一致。仅有底层 API 也不等于玩家缺少功能；相反，按钮存在但名称与实际命令不同，或玩家看到缺料却无法定位原因，属于优先问题。

当前页面职责已有明确边界：[页面职责契约](../ui/page-responsibilities.md)。Overview 负责“现在做什么”，Era 负责“如何推进时代”，Research 负责研究树和队列，Workshop 负责工坊购买，Buildings 负责普通建筑，Sectors 负责星区，Resources 负责状态和详情，Story 负责历史反馈。不要为了减少按钮而合并这些职责，也不要在详情页重复保存资源或研究的权威状态。

本轮未执行真实 Unity 编译，未启动玩家流程，未验证目标分辨率、触摸命中、按钮遮挡或实际手感。静态证据支持的是入口语义和调用链判断；涉及可读性、重复目标频率和操作成本的建议仍需运行观察。

## 2. 现有功能盘点：应保留

| 区域 | 已有入口 | 判断 |
|---|---|---|
| 全局导航 | Overview、Resources、Buildings、Research、Era、Workshop、Music、Sectors、Story；工坊和星区按解锁隐藏 | 九页职责不同，全部有真实绑定。[导航绑定](../../Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs#L385) [可见性](../../Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs#L461) |
| Overview | 发展指导、当前目标、当前时代快捷定位、离线摘要 | 保留。它们虽然可能指向相同研究，但分别承担指导、目标和时代定位，先做目标去重和改名，不直接删除。[指导导航](../../Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs#L1059) [定位](../../Assets/Resources/Script/UI/KingdomUIRoot.ResearchTree.cs#L893) |
| Resources | 资源卡片、库存/产出/消耗/净流和供给状态详情 | 保留。无建造或购买主按钮符合契约；补来源定位比增加交易按钮更有价值。[资源详情](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L317) |
| Buildings | 卡片详情、建造/升级、拆除、1/10/最大/自定义数量、详细说明切换 | 保留。批量模式已实现，输入框聚焦会自动切换自定义，不要再加“应用数量”按钮。[数量模式](../../Assets/Resources/Script/UI/KingdomUIRoot.Quantity.cs#L59) [建筑行](../../Assets/Resources/Script/UI/KingdomUIRoot.AuthoredRows.cs#L176) |
| Research | 研究树依赖、队列图、详情加入/移出队列、拖动和缩放 | 树与队列不是重复：一个看依赖，一个看执行顺序。研究详情已有动态加入/删除语义。[队列标签](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L1085) |
| Era | 当前时代能力、硬条件、建议、跃迁详情和研究导航 | 保留。时代页是推进诊断，不应被 Overview 取代。[时代页](../../Assets/Resources/Script/UI/KingdomUIRoot.Era.cs#L28) |
| Workshop | 卡片详情、行内购买、详情购买、显示已购、仅可支付筛选 | 快捷购买和完整详情入口均有价值。已购买项当前已不可购买，不应误报为可重复购买。[行内购买](../../Assets/Resources/Script/UI/KingdomUIRoot.AuthoredRows.cs#L304) [详情购买](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L928) |
| Music/声音 | 上一首、下一首、播放/暂停、单曲播放、进度、音乐音量、间隔、音效音量、静音 | 功能不同，保留。旧 Stop 只是兼容重命名，不是玩家看到的重复按钮。[绑定](../../Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs#L169) |
| Sectors | 星区详情、建筑展开、建造/拆除、解锁、探索、远星战役、维修、占领、姿态 | 入口集中且与普通建筑分离，保留，但运行态主按钮有遮蔽问题，见 P1。[星区操作](../../Assets/Resources/Script/UI/KingdomUIRoot.Sectors.cs#L85) |
| Relic | 调查、路线确认/取消、准备、分配、暂停/恢复 | 链条完整。工坊与遗迹均能制造支援是减少往返的快捷入口，不是重复系统。[遗迹绑定](../../Assets/Resources/Script/UI/KingdomUIRoot.Relic.cs#L68) |
| Story | 已完成章节展开/收起，最新章节导航 | 保留阅读和行动两种用途；最新章节不能收起是便利问题，不代表整个剧情按钮重复。[章节卡](../../Assets/Resources/Script/UI/StoryChapterCard.cs#L29) |
| Ultra | 启动/暂停/恢复/提交认证、工程姿态、战役姿态 | 工程提交是正向认证，不需要泛化确认弹窗。放弃工程会丢进度且不退启动材料，底层 API 不应为了“按钮齐全”暴露。[工程操作](../../Assets/Resources/Script/UI/KingdomUIRoot.UltraProject.cs#L276) |

## 3. 需要优先修正的按钮或功能

### P1：时代页“加入队列”实际可能取消研究

Era 页固定创建“加入队列”按钮，并调用 `HandleResearchAction`。[按钮](../../Assets/Resources/Script/UI/KingdomUIRoot.Era.cs#L157) [调用](../../Assets/Resources/Script/UI/KingdomUIRoot.Era.cs#L164) 但 Manager 对正在研究或已排队目标会移除队列并返回 Cancelled。[Manager](../../Assets/Resources/Script/Manager/ResearchManager.cs#L182) [取消分支](../../Assets/Resources/Script/Manager/ResearchManager.cs#L190)

这不是重复入口，而是确定的文案/行为错位。复用研究详情的动态状态：可研究时“加入队列”，排队时“移出队列”，正在研究时“取消当前研究”；已完成时隐藏或显示不可操作状态。成功标准是同一个目标在 Era、Research 详情和队列中的按钮都表达相同状态。

### P1：伤亡会遮蔽远星战役暂停

星区详情先判断有伤亡并把主按钮改成“维修舰队”，后面才判断活动战役并提供“暂停远星战役”。[伤亡分支](../../Assets/Resources/Script/UI/KingdomUIRoot.Sectors.cs#L673) [暂停分支](../../Assets/Resources/Script/UI/KingdomUIRoot.Sectors.cs#L710) 材料不足时维修仍可点击并失败，玩家同时失去停止持续消耗的入口。

保留暂停作为活动战役的稳定主操作，维修改成次按钮或并列操作；维修预览当前伤亡、可支付数量和成本。底层支持按请求数量维修，但 UI 当前请求全部伤亡。[维修入口](../../Assets/Resources/Script/UI/KingdomUIRoot.Sectors.cs#L850) [部分维修](../../Assets/Resources/Script/Manager/SectorManager.cs#L970)

### P1：按钮写“暂停”实际是取消远星战役

`PauseCampaign` 调用的是 `CancelCampaign`。[UI](../../Assets/Resources/Script/UI/KingdomUIRoot.Sectors.cs#L803) 取消会清除活动状态，并刷新遗迹支援；已分配的支援可能随非活动战役被清除。[取消](../../Assets/Resources/Script/Manager/SectorManager.cs#L1107) [支援清理](../../Assets/Resources/Script/Manager/RelicManager.cs#L143)

先明确产品语义：若这是撤退，就改名“撤退/终止远星战役”；若继续叫暂停，必须保留进度和支援。保留现有取消语义时，至少在已有支援时展示损失并确认，不要给所有普通操作增加弹窗。

### P1：批量拆除可直接拆光，缺少影响预览

数量模式同时服务建造和拆除；拆除的“最大”直接取当前全部持有，行按钮随后直接调用 `TryDeconstruct`。[最大数量](../../Assets/Resources/Script/UI/KingdomUIRoot.Quantity.cs#L188) [直接执行](../../Assets/Resources/Script/UI/KingdomUIRoot.PageRows.cs#L154)

普通拆除保留快捷操作；当数量为最大或多于 1 时，提供一次轻量预览：将减少的住房、生产力、电力、物流、产出和退回资源，并确认。成功标准是玩家能在提交全拆前知道会失去什么，且不改变 Manager 的原子交易契约。

### P1：取消研究会递归移除后续队列，却无影响预览

研究详情按钮已有加入/取消，功能本身不缺。[动态标签](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L1088) 但 `RemoveQueuedResearch` 会把依赖被取消研究的后续项目一起移除。[取消队列](../../Assets/Resources/Script/Manager/ResearchManager.cs#L230) 建议仅在存在关联后续项时显示“将移除 N 项后续研究”的预览，并列出被移除项目；普通单项取消不增加确认。

## 4. 玩家明显需要、但当前入口不足的功能

### P2：资源详情不能定位生产来源，也隐藏停工来源

资源详情只把生产/消耗建筑拼成文字，没有点击跳转。[文字详情](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L375) 且效率为零或数量为零的建筑被过滤。[过滤](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L436)

建议把来源分为“正在运行、已停工、已解锁可建”，停工项显示首要原因，点击后跳到 Buildings 或 Sectors 并定位卡片。这样缺资源时能从“缺什么”直接走到“哪条链没运行”，比增加资源购买按钮更符合经营初衷。

### P2：研究队列节点不能定位研究图

队列节点目前只选择详情。[队列点击](../../Assets/Resources/Script/UI/KingdomUIRoot.ResearchQueueGraphic.cs#L319) 研究图已有 `FocusResearchNode`，但主要由 Overview 快捷入口使用。[现有定位](../../Assets/Resources/Script/UI/KingdomUIRoot.ResearchTree.cs#L974)

给队列详情补“定位研究图”，直接复用现有定位能力；再根据实际节点数量决定是否增加搜索、可研究筛选或按时代筛选。当前没有证据支持先造完整搜索系统。

### P2：建筑禁用按钮只显示不可点，原因出现得太晚

建筑行只根据 `GetMaxBuildable` 设置按钮状态，[行按钮](../../Assets/Resources/Script/UI/KingdomUIRoot.AuthoredRows.cs#L193) 具体失败原因通常要真正点击后才通过 Tooltip 展示。[失败反馈](../../Assets/Resources/Script/UI/KingdomUIRoot.PageRows.cs#L152)

禁用按钮旁显示首要限制，例如“缺土地/生产力/资源/研究前置”，并让点击行或详情直接跳到对应条件。不要把所有灰按钮改成可点后反复失败。

### P2：研究队列缺少低成本的顺序调整

研究队列是先进先出；队首缺料会挡住后续。现有取消再加入可以间接调整，但取消会连带依赖项。[队列](../../Assets/Resources/Script/Manager/ResearchManager.cs#L50) [移除](../../Assets/Resources/Script/Manager/ResearchManager.cs#L230)

只在队列存在多项时提供“提前/后移”或“设为下一项”，并尊重未完成前置；不要自动跳过队首缺料研究，否则会改变玩家的明确计划。

### P2：自动保存和读档异常缺少玩家可见反馈

自动保存、强制保存和失败日志都存在，但 UI 未绑定最近成功保存、失败或读档回退状态。[保存](../../Assets/Resources/Script/Manager/SaveManager.cs#L151) [失败](../../Assets/Resources/Script/Manager/SaveManager.cs#L181) 读档失败后会开始新游戏，若没有提示容易被误解为正常开局。

增加最近成功保存时间、保存失败提示和“重试保存”；读档失败时明确“未加载有效存档，已开始新游戏”。不新增多槽、旧版本迁移或恢复系统。

### P2：遗迹支援按钮状态会让玩家点到失败

“分配支援”只检查 SupportReady，没有检查是否有活动远星战役；Manager 之后才返回 CampaignMissing。[UI状态](../../Assets/Resources/Script/UI/KingdomUIRoot.Relic.cs#L189) [Manager检查](../../Assets/Resources/Script/Manager/RelicManager.cs#L115)

无活动目标时禁用并显示“需活动远星战役”；有目标时显示目标名称。准备支援本身可以在没有活动战役时提前库存，属于有价值的准备功能，应保留。

### P2：遗迹“暂停/封存”在空闲状态也可能显示

当前暂停按钮主要按 `!Suspended` 显示，[显示逻辑](../../Assets/Resources/Script/UI/KingdomUIRoot.Relic.cs#L190) 空闲或尚未开始持续工作时可能出现没有可暂停对象的按钮。只在调查、路线认证、维护委托或其他真实持续工作状态显示；空闲时用状态文字。

### P2：工坊两个筛选同时开启时语义不清

“显示已购”和“仅可支付”都能开启，组合结果会排除已购项目，导致玩家不明白为什么目标消失。[筛选](../../Assets/Resources/Script/UI/KingdomUIRoot.WorkshopFilters.cs#L19) [过滤逻辑](../../Assets/Resources/Script/UI/KingdomUIRoot.AuthoredRows.cs#L347)

可选互斥两个 Toggle，或改成明确的三态筛选。低成本方案是保留控件但在组合时给出状态说明；不要因为筛选结果为空就删除任一筛选。

## 5. 次要便利与语义整理

| 优先级 | 项目 | 策划动作 |
|---|---|---|
| P2 | Overview“当前时代”按钮实际定位本时代首个可用研究 | 改名为“定位本时代研究”，或改为打开 Era；与“当前目标/指导”同指时合并或降低次要入口。[选择逻辑](../../Assets/Resources/Script/UI/KingdomUIRoot.ResearchTree.cs#L930) |
| P2 | 研究图没有玩家可用的定位当前研究/重置视图入口 | 先补两个最小按钮，复用已有 Focus 和缓存；搜索在验证找寻负担后再决定。 |
| P2 | 离线摘要最多显示 5 项资源变化，其余仅报项数 | 补“展开全部变化/收起”，缺料研究和供给阻塞项可直达已有详情；不要声称摘要已经记录停工时长。[离线摘要](../../Assets/Resources/Script/UI/KingdomUIRoot.OfflineSummary.cs#L26) |
| P2 | 跨页打开建筑详情后没有定位执行行的便利入口 | 补“前往建筑列表/所属星区并定位”，保留普通建筑只在列表建造的职责；不要恢复详情建造按钮。[建筑详情契约](../ui/page-responsibilities.md#L24) |
| P3 | 最新完成章节只能导航，不能收起 | 同时保留导航与收起，允许最新长正文折叠。[章节绑定](../../Assets/Resources/Script/UI/StoryChapterCard.cs#L49) |
| P3 | 输入非法数量时只恢复旧值，没有说明 | 输入旁显示整数范围和归一化后的最终数量；不增加提交按钮。[输入处理](../../Assets/Resources/Script/UI/KingdomUIRoot.Quantity.cs#L99) |
| P3 | “自定义”按钮与输入框聚焦自动切换存在功能重合 | 若触摸检查证明输入框易发现，可合并为输入即自定义；否则保留模式入口，不凭静态重复直接删。[输入选择](../../Assets/Resources/Script/UI/KingdomUIRoot.Quantity.cs#L81) |
| P3 | 工程完成后显示不可交互“文明工程已完成” | 换成阶段成果/已解锁能力状态文本或导航入口。[完成状态](../../Assets/Resources/Script/UI/KingdomUIRoot.UltraProject.cs#L330) |
| P3 | 工程姿态和战役姿态共用相似详情位置 | 文案明确“工程姿态/战役姿态”，同时显示时间、供给、伤亡变化，避免误解为同一开关。[姿态入口](../../Assets/Resources/Script/UI/KingdomUIRoot.UltraProject.cs#L159) |
| P3 | 音乐页同时管理音乐和音效 | 可将页面名改成“声音”或将音效区域标成独立区；暂不拆新设置大厅。 |
| P3 | 详情字号固定 | 先做目标分辨率和真实阅读检查，再决定标准/大字两档；不凭静态字号直接新增设置。 |

## 6. 不建议新增或删除

- 不删除 Overview、Era、Research、Workshop 等导航页；它们职责不同。
- 不新增研究“补前置”按钮，研究加入队列已经自动补入前置。[前置队列](../../Assets/Resources/Script/Manager/ResearchManager.cs#L199)
- 不新增全局游戏暂停、自动策略、重置循环、每日任务或新货币；当前问题是因果和恢复入口，不是系统数量不够。
- 不删除工坊列表购买或详情购买；两者分别支持快速执行和完整成本决策。
- 不新增放弃文明工程按钮；暂停可保留进度，放弃会丢进度且不退启动材料。
- 不把兼容 Stop、旧 Viewer、未实例化的独立 QuantityControls Prefab 说成玩家可见重复按钮。

## 7. 建议执行顺序

1. **R0 交互安全**：Era 队列按钮状态、远星战役暂停被遮蔽、暂停/取消语义和批量拆除预览。
2. **R1 因果可追踪**：资源来源分层与定位、禁用建筑原因、保存/读档反馈、遗迹支援可用状态。
3. **R2 研究操作**：队列取消影响预览、队列定位研究图、低成本顺序调整；之后才评估搜索。
4. **R3 信息整理**：Overview 目标去重与改名、工坊筛选组合、工程/战役姿态文案、剧情收起。
5. **R4 可读性验证**：目标分辨率、触摸命中、长文本、研究图缩放和按钮遮挡；没有运行证据不称完成。

每批只记录实际状态、按钮文案、可用性、Manager 返回结果和恢复动作。实施时遵循现有 UI 边界：固定按钮由 Scene/Prefab 创作，UI 只绑定状态和发命令；定义或经济变化另走内容验证流程。

## 8. 结论

当前没有证据表明需要大规模删按钮或增加新系统。最有价值的提升来自四类小而明确的交互修正：按钮名称必须与实际命令一致；高风险批量操作要有影响预览；正在运行的战略活动必须始终有停止入口；资源、研究和建筑详情要能把问题带回可执行的生产链。其余新增功能按真实找寻负担和分辨率验收后再决定。
