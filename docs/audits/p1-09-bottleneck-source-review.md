# P1-09 瓶颈来源调节审查

审查日期：2026-08-30

## 结论

当前代码已经具备按瓶颈来源收窄作用域的调节面；本轮没有明确的数值或运行时缺口，因此不修改 `ProgressionModifier`、资源定义或 simulator。

P1-09 的调节规则仍应作为调参约束执行：先根据真实运行时的资源等待、研究进度或生产力阻塞占比定位来源，每批只改一个主要变量；不能用全局 `x0.5` 代替定位。

## 证据

- Todolist 的 P1-09 明确要求：资源等待优先检查上游解锁、producer/加工建筑回本和时代资源需求；研究进度检查 `BaseCost`、预期 `ResearchPower` 与知识建筑时机；生产力阻塞检查人口、Housing 和 `ProductivityConsumption`，且不恢复 workforce。
- `ProgressionModifierState` 已分别保存建筑级、建筑-资源级、资源级，以及建筑研究力、电力、物流和建造倍率（`Assets/Resources/Script/Manager/ProgressionModifierManager.cs:6-13`）。这些接口通过 `GetBuilding...` / `GetResource...` 暴露，而不是只能改全局值（同文件 `41-69`）。
- 建筑生产计算同时读取建筑级/全局建筑生产倍率；电力和物流读取各自的专用倍率（`Assets/Resources/Script/Manager/BuildingManager.cs:1214-1228`）。资源产出更新还按建筑、建筑-资源和资源三级作用域应用 modifier（同文件 `1410-1426`）。这允许针对具体生产链处理资源瓶颈，而不会波及所有资源。
- 研究速度使用研究目标时代的速度因子、`ResearchPower`、幸福度与全局研究倍率（`Assets/Resources/Script/Manager/ResearchManager.cs:582-590`）。因此全局研究倍率仍是现有能力，但没有证据表明它应成为本项的默认修复手段。
- 平衡文档将生产写为 `baseRate × amount × efficiency × researchModifiers`，并将研究成本按目标耗时和预期研究力倒推（`docs/balance/balance-model.md:21-35`），与 P1-09 的来源定位规则一致。
- 当前静态闭包为 Industrial `81/81` research、`37/37` Workshop、`50/50` building 可达（`data/content-closure-static.md:3-15`）。
- `data/economy-simulation/EconomySimulationReport.md:3-6` 明确声明模拟结果是冻结诊断、不是当前平衡或 Unity 验收证据；因此其中的瓶颈字段不能支持改倍率的决定。

## 验证边界

- 本轮未运行 Unity 测试，未运行冻结 simulator，也未改变现有工作区改动。
- `TestResults/Latest-Test-Errors.txt` 当前记录 7 个失败；没有把这些失败解释为 P1-09 的瓶颈证据。
- 要进行实际调参，下一步必须先取得真实 Unity/PlayMode 的瓶颈时间占比和 Before/After 时间，并只改对应来源的一项变量。
