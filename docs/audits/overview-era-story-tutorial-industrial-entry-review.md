# Overview / Era / Story / Tutorial 工业时代入口实施后复核

日期：2026-08-25  
范围：首次进入 `TechLevel.Industrial` 后，玩家从 Overview、Era、Story、Tutorial 获得的状态与目标提示。  
说明：以下原始问题已按最小方案实施修复；本文件保留原证据链，并补充当前实现状态。未执行真实 Unity PlayMode 与真机验收。

## 结论

原始断点是：玩家完成时代跃迁、首次进入工业时代后，教程从 `era-goal` 进入 `long-term` 时可能只显示下一时代目标，工业主线未承接。当前代码已在 `long-term` 分支优先调用 `BuildIndustrialGuidance`，工业入口会显示第一项真实工业阻碍。

这是曾经的“状态已变化、目标未承接”断点，不是缺少工业内容：工业的 Workshop、机器工厂、蒸汽动力、工业居住、铁路、冶炼、化工、电网和大学等路径都已存在。

## 证据链

1. Tutorial 链在 `production-chain` 后固定进入 `era-goal`，随后进入 `long-term`：
   - `Assets/Resources/Script/Manager/TutorialManager.cs:203-216`
   - `era-goal` 的完成条件是 `era-reached`，导航页是 `Era`（同文件约 `208-212`）。
2. `BuildDetails` 只有在步骤不是 `EraGoal` 且不是 `LongTerm` 时才调用工业主线：
   - `Assets/Resources/Script/Manager/TutorialManager.cs:619-623`
   - 因此进入工业时代后，`long-term` 不会调用同文件 `:681-900` 的 `BuildIndustrialGuidance`。
3. `long-term` 会走 `BuildEraGoalGuidance`，将目标页设为 Era，并优先给出下一时代跃迁研究/资源条件：
   - `Assets/Resources/Script/Manager/TutorialManager.cs:603-605`
   - `Assets/Resources/Script/Manager/TutorialManager.cs:626-679`
4. Overview 原样展示 `CurrentGoal`、`NextEraGoal`、`Blocker` 和 `RecommendedAction`，按钮只跳到 TutorialSnapshot 的导航页：
   - `Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs:752-803`
   - `Assets/Resources/Script/UI/KingdomUIRoot.LiveRefresh.cs:828-889`
5. Era 页也同时显示引导目标、下一时代、当前任务和时代条件；工业入口后的默认 Tutorial 导航仍是 Era：
   - `Assets/Resources/Script/UI/KingdomUIRoot.Era.cs:60-90`
   - `Assets/Resources/Script/UI/KingdomUIRoot.Era.cs:141-203`
6. Story 页拥有工业第一步的真实条件和可导航动作，但它是被动入口：
   - `industrial-awakening` 要求 `Industrialization`：`Assets/Resources/Script/Manager/StoryManager.cs:112-121`
   - 下一章要求 `FactoryOrganization` + `MachineFactory`：同文件 `:122-129`
   - Story 页才会为下一锁定章节建立“前往完成条件”按钮：`Assets/Resources/Script/UI/KingdomUIRoot.Story.cs:75-96`、`:261-288`
7. 工业主线引导本身已经定义了可复用的最小顺序，首个分支是 `IndustrialWorkshop`，随后是 Workshop 改良、`FactoryOrganization`、`MachineFactory`、`SteamPower`、`SteamPlant`：
   - `Assets/Resources/Script/Manager/TutorialManager.cs:681-752`
8. 当前静态内容闭合并不是该断点的原因：工业闭合为完成，工业研究/Workshop/建筑均可达：
   - `data/content-closure-static.md:4-8`
   - 历史模拟报告曾显示 Medieval/Industrial 未达到里程碑；由于模拟器已冻结且输入快照与当前资产不一致，该结果只能作为历史诊断背景，不得用于当前 pacing、平衡或运行时验收：`data/economy-simulation/MilestoneSummary.csv:1-5`、`data/economy-simulation/EconomySimulationReport.md:37-57`

## 已实施改进（不新增系统）

在现有 `TutorialManager.BuildDetails` 的目标合并处复用 `BuildIndustrialGuidance`：当当前时代为 Industrial 且步骤为 `long-term` 时，工业主线覆盖 `Blocker`、`RecommendedAction`、`NavigationPage`、`NavigationTargetId`；保留现有 Tutorial 的长期步骤、完成记录和 Save 数据，没有新增状态字段。

预期首次进入工业时代的闭环：

`时代跃迁完成 → Overview 显示工业第一阻塞项 → 按钮跳 Research/Workshop/Buildings 并定位目标 → 返回后状态刷新 → Story 显示对应工业记忆`

优先复用的现有入口是 `BuildIndustrialGuidance` 和 `NavigateToTutorialTarget`。不要把 Story 阅读变成奖励或新状态机；Story 继续作为叙事确认，Manager 仍是状态/目标来源。

## 验证状态

- 静态代码已确认：工业入口不再只指向下一时代，首先指向 `IndustrialWorkshop` 的真实前置或其首个可执行前置。
- 现有导航字段仍指向 Research、Workshop 或 Buildings 页面。
- Story/Tutorial 反馈已接入 live refresh，完成动作后可更新，不依赖重新加载场景。
- 未新增 Manager、页面、资源、研究、保存字段或系统。

未执行真实 Unity 编译。  
未执行 Huawei P40 Pro 真机验收。
