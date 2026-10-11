# 11 · Manager 层 C# 源码区块（只读盘点）

## 1. 范围

- 审查路径：`Assets/Resources/Script/Manager/`，全部 **18 个 .cs**（逐文件完整读，共 14377 行）。
- 文件清单（行数）：TutorialStepDefinition(33)、GameBootstrap(81)、CampaignManager(181)、EraGoalEvaluator(201)、RelicManager(302)、ProgressionModifierManager(386)、WorkshopManager(410)、GameManager(467)、SimulationManager(563)、ResourceManager(591)、UltraProjectManager(708)、StoryManager(735)、MusicManager(743)、SaveManager(999)、ResearchManager(1341)、BuildingManager(2000)、SectorManager(2061)、TutorialManager(2575)。
- 只读：仅运行 `git log/show`、`rg`/`grep`、`wc`。未修改任何项目文件。

## 2. 结论清单

### 建议删除（死代码 / D9）

- [High] `Assets/Resources/Script/Manager/UltraProjectManager.cs` — 维度 D9；证据：私有方法 `CalculateProgressRate(UltraProjectStageDefinition, ExpantaNum)`（行 589-594）全仓仅此 1 处定义、0 处调用。grep 式：`rg "CalculateProgressRate"` 命中结果中，`UltraProjectManager` 只出现在定义行 589，其余命中全为 `CampaignManager.CalculateProgressRate`（不同类）。实际进度率在 `GetPreview()`（行 304-305）与 `Tick()`（行 348-349）内联计算，该方法已被内联取代；引入于 `fdcfd3f`（2026-10-07）。建议：删除；理由：纯死代码，无调用点。
- [High] `Assets/Resources/Script/Manager/SectorManager.cs` — 维度 D9；证据：`SectorSnapshot` 私有类（行 151-162）+ `CaptureSectorSnapshots()`（行 1883-1903）+ `RestoreSectorSnapshots(...)`（行 1905-1922）构成一个孤立簇，全仓 0 处外部调用。grep 式：`rg "CaptureSectorSnapshots|RestoreSectorSnapshots|SectorSnapshot"` 命中全部落在该文件这 6 行内（定义+自身内部互引），无任何调用点。快照回滚已被各事务内联的 `previous*` 变量 + `state.RestoreExact(...)` 取代。引入于 `fdcfd3f`（2026-10-07）。建议：整簇删除；理由：完全未被引用的快照/恢复辅助代码。
- [High] `Assets/Resources/Script/Manager/TutorialManager.cs` — 维度 D9；证据：私有静态方法 `IsProductionChainEndpoint(Building, IReadOnlyList<Building>)`（行 2070-2097）全仓仅 1 处定义、0 处调用。grep 式：`rg "IsProductionChainEndpoint"` 仅命中行 2070 定义。生产链判断实际走 `TryFindOwnedProductionChain`（行 2358-2418）。引入于 `21e1774`/`80521ca`（2026-08~09）。建议：删除；理由：未被引用的判定辅助函数。
- [Med] `Assets/Resources/Script/Manager/CampaignManager.cs` — 维度 D9；证据：`CalculateEffectivePower` 共 3 个重载（行 14 五参、行 30 七参、行 48 八参）。五参重载（行 14-28）全仓 0 调用：`rg "CalculateEffectivePower"` 在 `Assets` 内的调用点仅 SectorManager.cs:1346（八参）与测试 KingdomLogicTests:2857/2865/2873/2890/2898（七参）、SectorManagerTests:2267/2276、SpaceProgressionTests:502/511（八参），无任一为五参。建议：删除五参重载；理由：唯一的重载变体无调用点（七参仍被测试引用，保留）。
- [High] 未使用的 `using`：`GameManager.cs:2`、`ResearchManager.cs:3`、`SectorManager.cs:3` 三处 `using System.ComponentModel;` 均无实际使用。证据：`rg "DefaultValue|INotifyPropertyChanged|\[Description|PropertyChangedEventHandler|System.ComponentModel"` 在 Manager 目录仅命中这 3 行 import，无任何该命名空间类型的使用。建议：删除该 using；理由：无效引用（编译期告警/噪声）。

### 保留观察（结构重复，非陈旧）

- [Low] `Assets/Resources/Script/Manager/` 各 Manager 均各自实现 `TryGetStateByStableId`（ResourceManager:97、BuildingManager:394、ResearchManager:142、SectorManager:229、WorkshopManager:347）与 `Parse(...)`（ResourceManager:579、BuildingManager:1987、ResearchManager:1252、GameManager:454、SectorManager:1752）。证据：逐文件比对签名与实现体几乎一致。维度 D8。建议：保留观察；理由：各操作不同状态字典/不同 Owner 名，属按类型特化的合理重复，不构成"同一内容多处维护"，强行抽象反而引入泛型耦合。本次不列为清理项。
- [Low] `Assets/Resources/Script/Manager/SectorManager.cs:692` 局部变量名 `ownsLegacyCampaignSlot`（行 692、711）含 "Legacy" 字样。证据：行 692 `bool ownsLegacyCampaignSlot = !runtimeState.Campaign.Active;`，用于判断是否需 `BeginCampaign` 建立战役槽。建议：保留观察；理由：仅为命名措辞，逻辑为当前在用分支，非陈旧代码（若清理阶段顺手，可改名为 `ownsCampaignSlot`，不影响行为）。

## 3. 硬约束专项核查（未发现违规）

- Runtime State 唯一权威 = Manager：Manager 层为 State 的唯一校验/修改者，UI 只发命令（本区块内未发现 UI 直改 State 的入口）。未发现。
- 禁止反射：`rg "System\.Reflection|BindingFlags|Activator|GetType\(\)\.GetMethod"` 在 Manager 目录 **0 命中**。命中的 `.Invoke(`（如 ResourceManager:335、各 event `?.Invoke()`）均为委托/事件调用，非反射。未发现。
- 存档只接受当前 v9、无旧版迁移：`SaveFormat.CurrentVersion = 9`（Data/SaveFormat.cs:3）；`SaveManager.IsSupportedVersion`（行 974-977）仅 `version == CurrentVersion`；`SaveManager` 内无 v1–v8 迁移、无旧 ID 映射、无备份恢复分支。未发现。
- 建造/拆除/研究/工坊先全量校验后提交、失败不部分修改：BuildingManager `TryBuildInternal`/`TryDeconstruct`/`TryUpgradeInternal` 均先校验资源/领土/生产力再 `TryApplyAtomicPayment/Changes` 并带 rollback（行 638-777、863-923、1056-1163）；ResearchManager `TryPayResearchCost` 原子支付（行 650-702）；WorkshopManager `TryPurchase` 原子支付+回滚（行 175-260）。研究全额支付后才推进（`Tick` 行 574-591 要求 `CostPaid`）。未发现。
- 库存不得为负：ResourceManager `ApplyAtomicChanges` 行 229-231 校验 `amount < -entry.Value` 即拒绝；各 Restore 校验 `< Zero` 抛错。未发现。
- Food 唯一可封顶：`GameManager.AdvanceFood`（行 174-196）仅对 Food 施加 `capacity` 上限；ResourceManager `AdvanceAmount`（行 393-405）无容量截断，仅 `Max(0, …)`。未发现普通资源容量/MaxAmount/仓储截断。
- 不恢复 workforce：`rg "workforce|Workforce"` 在 Manager 目录 **0 命中**。未发现。

## 4. 不确定项

- `CampaignManager.CalculateEffectivePower` 五参重载判死基于"全部调用点参数个数"的人工核对（7/8 参）。若存在跨程序集（如未纳入本次 grep 的插件/DLL）调用则结论不成立；当前仓库范围内为 0 调用。需在清理执行前以编译验证兜底。

## 5. 未覆盖项

- 未运行 Unity、未编译、未跑测试（准则禁止）。死代码结论基于静态 grep，未做编译级确认。
- `Library/Bee/artifacts/**/il2cppOutput/**` 等构建产物虽命中 `CalculateEffectivePower`，属 D4 构建缓存（他组范围），本报告未计入证据。
- UI / Data / Runtime / Math / Misc / Validation 目录（其他子代理范围）未审。
