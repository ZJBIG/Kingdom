# Kingdom 信息、恢复与选择质量审查

日期：2026-10-09。承接[逐阶段审查](2026-10-09-progressive-design-review.md)与[功能按钮审查](2026-10-09-ui-controls-and-feature-audit.md)，本轮自主选择三个互补方向：玩家看到的信息能否支撑正确判断、经营失败后能否恢复、不同投资是否有真实取舍。

只做策划与静态审查，不修改游戏代码、资产、数值、存档或规则。三个只读子代理分别核查信息、恢复和选择，主代理核对实际调用链及资产。没有启动 Unity、模拟器或真实存档。未执行真实 Unity 编译。

## 1. 优先发现

| 优先级 | 新发现 | 对体验的影响 | 最小下一动作 |
|---|---|---|---|
| P1 | 负生产力时，零生产力消费住房的可建数量预测与实际提交不一致 | 危机中玩家看到能建，却无法用直觉行动改善处境 | 建立负余量、材料与土地充足的边界场景，统一预测与交易语义 |
| P1 | 顶部电力/物流需求只统计建筑，实际供给还计工程和遗迹 | 可能显示有余量，实际效率却下降 | 显示完整需求或明确建筑/工程/遗迹分项 |
| P1 | 食物栏显示日常净流，战略活动另行扣粮 | 正净流与库存下降可能同时出现，看起来像结算错误 | 区分日常净流、战略消耗和实际库存变化 |
| P2 | 研究力没有与当前研究实际点数/秒连接 | 玩家难判断加速、时代门和粮食对科研的作用 | 当前研究显示实际速度与条件性剩余时间 |
| P2 | 离线摘要混用覆盖时长与经济折算时长 | 玩家难解释离线收益为何少于在线同长收益 | 分别标明纳入结算时长和有效经济时长 |
| P2 | 升级和倍率投资容易被理解为所有方面都更好 | 实际有生产力、土地、原料、人口兑现时间等取舍 | 按同一状态展示收益与新增负担，不先调数值 |

## 2. 方向一：信息能否让玩家作出正确判断

### I01 电力与物流的总览需求漏掉真实负荷

[顶部显示](../../Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs#L1197)调用 `CalculateRawFlowDemand`；[需求函数](../../Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs#L1241)只遍历建筑的定义消耗。实际满意度计算还计入[文明工程和遗迹负荷](../../Assets/Resources/Script/Manager/BuildingManager.cs#L1289)。

这是显示口径不完整的静态事实。建议总览使用完整需求，并支持建筑、工程、遗迹分项；若保留当前统计，则标成“建筑需求”，不能继续让玩家把它理解为整个文明的余量。不同系统不能各有一套互不相认的“剩余供给”。

验证场景：建筑供给足够、工程或遗迹运行后总需求超过供给。总览的缺口、满意度和详情中的新增负荷应能相互解释；暂停对应活动后缺口消失。非建筑负荷未运行时不显示虚假扣除。

### I02 食物正净流与库存下降可以同时发生

[FoodNetRate](../../Assets/Resources/Script/Runtime/GameState.cs#L21)只扣建筑和人口日常消耗；[食物栏](../../Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs#L1203)把它直接放在库存旁。文明工程、遗迹和星区操作还直接支付食物，例如[工程支付](../../Assets/Resources/Script/Manager/UltraProjectManager.cs#L353)、[遗迹支付](../../Assets/Resources/Script/Manager/RelicManager.cs#L165)、[星区支付](../../Assets/Resources/Script/Manager/SectorManager.cs#L1944)。

建议说明“日常净粮 +X/s，战略消耗 -Y/s”，或者展示统一口径的有效净流。Food 满仓时，生产盈余也不等于实际入库；封顶、一次费用和持续费用应分开，不把它们混成产率错误。普通资源仍不增加容量。

验证场景：日常正净粮但工程消耗更大；日常负净粮但有库存缓冲；满仓且正净粮；一次支付之后持续运行。玩家看到的库存变化必须能由这些来源解释，不改离线奖励来掩盖显示问题。

### I03 科研能力不是当前研究的实际速度

[顶部 ResearchPower](../../Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs#L1186)是基础与建筑能力；[实际推进](../../Assets/Resources/Script/Manager/ResearchManager.cs#L518)还乘时代速度、全局效率、全局研究倍率和幸福。[研究详情](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L589)有工作量和效果，却缺这两种口径的连接。

这是解释不足，不是把 ResearchPower 本身算错。建议当前研究详情显示有效点数/秒、主要倍率和“按当前条件预计剩余时间”。等待材料时显示材料等待，不给虚假的科研完成倒计时；条件变化后重算。顶部可保留“科研能力”，无须堆叠全部公式。

### I04 离线时长需要区分覆盖与折算

[摘要文案](../../Assets/Resources/Script/UI/KingdomUIRoot.OfflineSummary.cs#L18)将 SettledSeconds 称为“结算跨度（含分段折减）”；[SaveManager](../../Assets/Resources/Script/Manager/SaveManager.cs#L101)接收 `AdvanceOffline` 的返回值，但[循环累加](../../Assets/Resources/Script/Manager/SimulationManager.cs#L329)返回原始处理 step，经济推进另用折算后的秒数。

当前规则为前 2 小时全速、2 到 8 小时 60%、之后 25%。在 8 小时完整纳入结算的例子中，覆盖时长是 8 小时，经济有效时长是 `2 + 6 × 0.6 = 5.6` 小时；它们都可以正确，但不能用一个值同时表示两个概念。

建议分别显示“纳入结算的离开时长”和“折算后的有效经济时长”，说明折减。不能由有效时长直接乘当前产率承诺总收益，因为研究完成、库存短缺、工程暂停等都会改变过程状态。

### 已覆盖的关联项

资源详情过滤停工来源、来源不可定位、离线摘要只展开前五项，以及时代缺口未扣已有库存，分别已在前两份报告中记录，本轮不重复包装成新发现。Era 的产率已读取当前有效产出并应用幸福奖励，不应声称它使用了未受效率影响的理论产率。

## 3. 方向二：经营失败后能否理解并恢复

### H01 负生产力危机下，住房可建预测与提交不同

[生产力余量](../../Assets/Resources/Script/Manager/BuildingManager.cs#L127)为总量减使用量，人口流失后可能为负。[TryBuild](../../Assets/Resources/Script/Manager/BuildingManager.cs#L655)对零消费建筑也校验 `required <= available`，零大于负余量时会拒绝；[木屋](../../Assets/Resources/Datas/Building/Animal/WoodHouse.asset#L24)恰好消费零生产力。

但[GetMaxBuildable](../../Assets/Resources/Script/Manager/BuildingManager.cs#L846)只有消费大于零才限制生产力，[数量预测](../../Assets/Resources/Script/UI/KingdomUIRoot.Quantity.cs#L201)采用此结果，因此可能给出可建数量，提交却失败。这一边界有静态依据，仍需真实行为复现。

先用“余量为负、材料与土地足够、同一住房”核对单建、最大建、详情状态和 Manager 返回值，再决定零消费建筑的产品语义。不能只让 UI 变灰而保留规则分歧，也不直接新增免费救援系统。

### H02 饥饿不等于永久失败，恢复路径要能看懂

[人口流失准入](../../Assets/Resources/Script/Manager/GameManager.cs#L171)要求食物为零且日常净粮为负；[流失速度](../../Assets/Resources/Script/Runtime/PopulationState.cs#L302)按短缺比例与人口计算，人口可以降到零。基础粮食产出仍存在，[增长公式](../../Assets/Resources/Script/Runtime/PopulationState.cs#L290)也采用至少 1 人的增长基数，有住房和足够供给时零人口有恢复路径。

建议危机说明聚焦三个事实：库存缓冲还能撑多久、人口是否正在离开、哪个现有行动能改善供给或释放生产力。缺粮时先看粮食链和持续负担，不能把“再建一间房”当作普遍自救答案，也不能把每次短缺称作死锁。

验证应分别覆盖有库存负净粮、库存耗尽、净粮改善、零人口恢复；只在状态稳定时给条件性时间估算，不承诺固定死亡或恢复时刻。

### H03 拆除可以脱困，但代价与后果需准确

拆除可释放生产力和领土，但返还率默认较低；[返还计算](../../Assets/Resources/Script/Manager/BuildingManager.cs#L782)读当前返还倍率。已有交易失败回滚，应保留。

拆除预览除了退款，还应说明释放的生产力、减少的粮产/电力/物流，以及住房和 Food 上限变化。人口超过住房时当前[停止增长](../../Assets/Resources/Script/Runtime/PopulationState.cs#L150)，不会因拆房立即离开；不要用“马上失去这些人口”误导风险提示。

### H04 研究取消保留付款与进度，不是退款

[RemoveQueuedResearch](../../Assets/Resources/Script/Manager/ResearchManager.cs#L230)取消活动/队列并移除依赖项，只改状态，没有清除付款账本或进度，也不退款。重新加入可以继续既有投入。

因此玩家说明应是“移出队列、保留已付款和进度、同时移除以下依赖项”，而不是“取消后损失材料”或“取消可回收材料自救”。资金仍锁在研究中，玩家需要在首次全额付款之前理解这一点。

### 已有恢复机制

工程与遗迹已有供给暂停、保留进度和恢复校验，[工程界面](../../Assets/Resources/Script/UI/KingdomUIRoot.UltraProject.cs#L64)也说明暂停不消耗。后续按材料、电力、物流逐类验证，不再建立第二套暂停系统。工业缺料降效有明确规则，不仅凭低效率就判断硬死锁。

## 4. 方向三：选择有何区别，奖励何时兑现

### C01 农场升级是密度与储备选择，不是所有维度都更好

| 无倍率基础口径 | Farm | IrrigationWorks |
|---|---|---|
| 粮产 | 8/s | 20/s |
| 生产力需求 | 3 | 8 |
| 土地需求 | 4 | 6 |
| 粮食容量 | 0 | 200 |
| 每生产力粮产 | 约 2.67/s | 2.5/s |
| 每土地粮产 | 2/s | 约 3.33/s |

依据：[Farm](../../Assets/Resources/Datas/Building/Animal/Farm.asset#L21)、[IrrigationWorks](../../Assets/Resources/Datas/Building/StoneAge/IrrigationWorks.asset#L21)。灌溉更适合土地紧张和储粮需求，未必适合生产力最紧张时全部升级。旧农场高档解锁后停止新增，已有低档仍可保留，部分升级有实际价值。

一座农场升级为一座灌溉设施的净土地需求增加 2，不应描述成这一次升级直接释放土地。密度优势是达到同等粮产所需土地较少，必须使用同口径比较。

建议升级比较同时给新增粮产、额外生产力/土地、储粮收益；验证暂缓、部分和全部升级的不同适用场景。不能凭基础比例直接调低灌溉费用或改变低档停建规则。

### C02 住房收益分成容量、入住、生产力三个时点

[WoodHouse](../../Assets/Resources/Datas/Building/Animal/WoodHouse.asset#L21)容量 5，[StoneHouse](../../Assets/Resources/Datas/Building/StoneAge/StoneHouse.asset#L21)容量 12，同占地 3。容量增加 7 不等于立即新增 7 人；新增人口真正入住后，基础日常粮需增加 `7 × 0.8 = 5.6/s`，生产力才逐步兑现。

建议将住房回报解释为未来发展空间，同时说明满员粮需；不要为了即时反馈赠人口。验收分别用粮食宽裕与紧张状态记录升级后人口、净粮和可用生产力变化。

### C03 科研投资需要明确回报窗口

[KnowledgeSharing](../../Assets/Resources/Datas/Research/Animal/KnowledgeSharing.asset#L18)本身要投入研究工作，再提供全局科研加成和下游解锁。因此“直接冲时代”和“投资后续科研”应是合法不同目标，加速科技不能仅因为叫加速就被当作当期必买。

观察到当前时代门与下一时代第一组研究两个窗口，分别记录研究等待和材料等待。时代速度倍率、下游知识建筑、当前设施和已有倍率都影响收益，不把纯工作量静态回本计算当作真实总流程时间。

### C04 加工倍率增加吞吐量，未必改善原料转换效率

[PrecisionTooling](../../Assets/Resources/Datas/Workshop/PrecisionTooling.asset#L32)提升加工建筑生产倍率；[建筑速率更新](../../Assets/Resources/Script/Manager/BuildingManager.cs#L1517)会把相关倍率同时应用于资源产出和持续投入。原料已经卡住时，倍率提高需求，效率重算后可能难以兑现静态增产预览。

现有[工坊预览](../../Assets/Resources/Script/UI/KingdomUIRoot.DetailPanel.cs#L814)已明确按当前效率，且提醒供给变化会重算，不能说没有预览。后续需要证明玩家能区分“扩加工规模”与“减少每单位原料消耗”。验证上游富余、刚好满足、短缺三种状态，不把资源数量直接相加成总收益。

### C05 储粮与幸福不能替代持续供粮

[FoodPreservation](../../Assets/Resources/Datas/Research/Animal/FoodPreservation.asset#L28)提供容量加成，不增加持续粮产。负净粮下容量的价值是有粮可存后缓冲亏空，空仓时更大的容量无法自救。幸福奖励[有上限且先检查短缺](../../Assets/Resources/Script/Runtime/HappinessFormula.cs#L61)，供给不足时额外幸福加成不能替代供粮。

以“持续支持人口、允许离开更久、改善劳动与科研”分别说明回报。幸福接近上限时先看真实边际收益，不给支线加无意义奖励来保证每次点击都显得划算。

## 5. 后续顺序与边界

1. **先事实一致**：零消费住房预测/提交边界，完整电力/物流需求，食物日常/战略口径，离线时长标签。
2. **再恢复可解释**：缺粮、负生产力、取消研究、拆除与工程暂停，每种失败都有可指出的现有恢复动作。
3. **再验证选择**：从农场、住房、知识投资三个早期实例开始，分别证明不同约束下选择不同；之后才进入工业工坊。
4. **最后校准节奏**：只有真实行为、显示和恢复正确后，才按实测等待调整单个主要瓶颈。本轮不给最终倍率、目标通关时长或最优路线。

前两轮已列的按钮与导航改进继续沿原编号推进，本报告只追加新角度，不另建重复开发规范。所有数值比例是当前定义的静态示例，不能证明已经更好玩。未执行真实 Unity 编译，危机轨迹、收益兑现时间和玩家理解均未作运行验收。
