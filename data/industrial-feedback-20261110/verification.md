# 工业时代反馈与建筑需求星标验证

日期：2026-11-10。项目：`D:\GitHub\Kingdom`，Unity `2022.3.62f3c1`，D3D11 批处理。
输入 HEAD：`1179f27ce3cc3858961ee4f813d52a81ae5217c5`，叠加本轮工作树；起始未提交内容见 `input-working-tree.patch`。既有建筑列表屏外刷新优化及其测试完整保留，未提交或推送。

## 已实现行为

- 机器制造厂完成精密制造研究后显示并可正常建造；按用户后续要求，已移除精密机床组的工坊建造前置。精密机床组作为后续产量升级，详情不再将其列为缺少的建造条件。
- 全部 26 栋 `TechLevel: 3` 工业建筑的 109 项建造材料为原来的 5 倍。逐项前后值见 `building-cost-changes.json`。仅改建造成本，增长率、占地、生产、持续消耗和解锁条件保持原值；化工厂另有已授权的说明变化。
- 移除 1000× 速度及对应控件。保留 1×/5×/20× 速度，离线储备消耗倍率由 0/5/25/500 改为 0/200/500；5×、20× 分别每实际运行秒消耗 200、500 秒储备，仍仅按实际完成的加速 tick 扣费。
- 研究节点用 Prefab 中的 CanvasGroup 统一透明度，后创建的 TMP 字形子网格随节点一起变亮，不影响研究进度颜色、活动研究金框和前置路径。
- 化工厂的火箭燃料配方由“轨道工程 / OrbitalEngineering”解锁。使用已完成研究构建的倍率快照，实际在线/离线生产、恢复研究状态、详情和推荐共同遵守；现有工厂完成研究后开始产出。轨道工程自身不需要火箭燃料，避免自我供料循环。
- 建筑拆除按当前数量直接调用原有原子事务；单座与批量都不再弹确认。
- 按“设计建筑需求星标UI”会话，在基础和七时代建筑卡 Prefab 中绑定现有星标素材。资源实际净消耗或当 tick 供给不足时标出可补充的来源，也覆盖粮食、电力、物流、生产力与必要住房缺口；缺口解除就清除。升级卡比较升级前后贡献，保底来源即使零数量 State 被清除仍刷新星标。

## 真实 Unity 验证

均使用隔离测试存档；没有操作真实存档、创建临时工程或结束用户 Editor。

| 运行 | 时间（UTC） | 结果 | Unity 退出码 | 证据 |
| --- | --- | --- | --- | --- |
| 修复前机器卡与燃料复现 | 以 XML 为准 | 3 项预期失败 | 2 | `edit-repro.xml/log` |
| 修复前研究文字复现 | 以 XML 为准 | 1 项预期失败，后来创建字形仍为 0.3 透明度 | 2 | `play-repro.xml/log` |
| 行为、离线和 Prefab 针对验证 | 以 XML 为准 | 31/31 通过 | 0 | `edit-behavior-final.xml/log` |
| 成本调整后的扩大 EditMode | 02:13:28–02:13:57 | 287/293 通过，6 项既有冲突，0 跳过 | 2 | `edit-final.xml/log` |
| UI、离线及工业生产 PlayMode | 02:18:58–02:19:11 | 11/12 通过；1 项在同帧中检查尚未到刷新周期的详情 | 2 | `play-final.xml/log` |
| 星标和工坊详情最终验证 | 02:21:06–02:21:11 | 2/2 通过，0 跳过 | 0 | `play-stars-verified.xml/log` |

最后一次仅给测试增加一次 0.3 秒等待，保留缺少前置提示必须清除、建筑属性仍显示的断言，生产代码没有为此改动。合并最后两次结果，12 个不同的相关 PlayMode 用例最终全部通过。机器卡、燃料、费用增长、闭包、离线扣费及 Prefab 的相关 EditMode 在最终扩大运行中通过。

运行方式（实际过滤器分别保存于 `edit-final-filter.txt`、`play-final-filter.txt`、`play-stars-verified-filter.txt`）：

```text
D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe
  -batchmode -force-d3d11 -projectPath D:\GitHub\Kingdom
  -runTests -testPlatform EditMode|PlayMode -testFilter <对应过滤器>
  -testResults <本目录对应 XML> -logFile <本目录对应 log>
```

已执行真实 Unity 编译。最终日志没有脚本编译、Prefab 导入、NullReference 或 MissingReference 错误。EditMode 中的 `building-observer-test`、`research-observer-test`、`workshop-observer-test` 异常为原有观察者隔离测试主动注入且预期的日志，相应测试通过。

研究图运行日志确认：145 个节点，重复格 0、逆向边 0；`ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping` 通过。自动屏外跳过和重新可见刷新两项既有回归也通过。

星标实际画面见 `building-recommendation.png`（受控工业 fixture，2640×1200）。已查看：星标位于图钉上方，名称与建造/拆除按钮不重叠，星标不拦截点击；画面中的时代提示来自测试预置研究状态。

## 6 项既有 EditMode 冲突

逐项用 `git show HEAD:<路径>` 核对，以下测试、校验器和触发字段在本轮前就保持相同值；没有为通过测试修改无关经济定义。

| 用例 | 现有冲突 |
| --- | --- |
| `C606_IndustrialPowerAndLogisticsRolesRemainDistinct` | 测试要求 RailHub 耗电为 0，HEAD 与当前 `powerContribution` 均为 -8。 |
| `C607_IndustrialProductionGraphHasNoRecipeCycle` | 综合校验器在 CivilizationPlanning 空效果处提前失败，尚未检查生产图，并非查出新生产循环。 |
| `EveryResearchAndWorkshopEffectHasAValidTarget` | CivilizationPlanning 在 HEAD 与当前均为 `effects: []`、`AdvancesTechLevel: 0`，不满足旧测试条件。 |
| `IntegratedDependencyGraphIsReachable` | 被同一个 CivilizationPlanning 空效果校验提前阻止。 |
| `所有工业建筑必须拥有持续资源维护回路` | OilDerrick 在 HEAD 与当前均无持续材料消耗；测试要求所有工业建筑的材料消耗列表非空。 |
| `炸药应作为工业与远征中的持续消耗资源` | 首条断言要求 OilDerrick 持续消耗炸药，但其列表原本为空。IntegratedMetalMine 的 0.15、RailHub 的 0.03 炸药持续消耗仍在。 |

## 静态与确定性检查

- 修改前、加入燃料科技门后、费用调整后分别执行 `tools/codex/content-closure-check.ps1 -ProjectRoot . -ReportPath <本目录对应报告>`，全部通过，见 `closure-before.md`、`closure-gate.md`、`closure-after.md`。
- 从新游戏可达 Industrial 及之前的研究 83/83、工坊 37/37、建筑 50/50；Spacer 与 Ultra 定义也全部可达，最高 TechLevel 5。独立闭包与资源流向检查通过，不能将上面的综合校验器失败表述为整体定义套件通过。
- 离线确定性诊断通过已有 `tools/NewEconomySimulator/NewEconomySimulator.csproj` 运行。修改前 72/73，修改后 74/75，新增的化工厂燃料门检查通过。唯一失败前后相同：`happiness shortage cannot improve efficiency`；详情均为 `Same net food/population, decreasing stored food: 1, 0.99, 0.625.`。报告为 `deterministic-before.json`、`deterministic-after.json`；前者需跳过开头构建警告再解析 JSON。不声称整个确定性套件或游戏节奏已通过。
- 独立复核全部 26 栋成本、109 条金额及未授权字段；10 份 Prefab 无重复 fileID，已有 `.meta`/GUID 未变，1000× 对象和引用已完整移除。
- `git -c core.safecrlf=false diff --check` 通过。未增加反射、存档迁移、库存上限或依赖。

## 收尾

- 动态字体已从本轮前备份恢复并确认无 Git diff，SHA256 为 `722BFD776012F77B34C3C33AB7BC512BE7DDDDBC4FD9C44709CE0206689C539F`。
- 自动审批拒绝了恢复字体并删除本次备份的组合命令，工具原因仅为 `blocked by policy`，该命令未执行；单独复制恢复成功。未换用其他删除方式。唯一保留的本轮清理项：`data/industrial-feedback-20261110/font-before.backup`（32,084,075 字节）。后续在允许清理时删除此单文件并确认不存在；不触及其他轮次备份。
- 本轮测试进程均已退出，EarlyGameUi、ReviewInteraction、KingdomPlayModeTests 临时存档目录无残留。未创建临时工作树或项目。
- 后续若处理 6 项既有测试冲突，先确认对应定义与旧断言的产品意图；本轮不扩展无关经济修改。

## 2026-11-10 续接：移除机器制造厂工坊前置（当前行为）

- 用户明确要求取消等待精密机床组的门槛。`MachineFactory.asset.requiredWorkshopUpgrades` 从仅包含 `PrecisionTooling` 的列表改为 `[]`；精密制造研究、建造材料 ×5 和工坊的产量升级效果不受此次调整影响。
- 同步两项 EditMode 和一项 PlayMode 既有测试：实际未购买精密机床组时，卡片可见、按钮可用、点击后建筑数量增加；工坊仍能提升产量。旧的“必须先购买工坊”断言已按新授权行为替换。
- PlayMode 先验证并建造工厂，再购买工坊以保留零数量保底建筑 State 清理的星标回归；购买不再是首座工厂建造的步骤。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 修改前后静态闭包 | 均通过，Industrial 研究83/83、工坊37/37、建筑50/50，可达最高TechLevel5 | `factory-unlock-closure-before.md`、`factory-unlock-closure-after.md` |
| 修改前后确定性诊断 | 均74/75，唯一既有幸福度检查失败完全相同 | `factory-unlock-deterministic-before.json`、`factory-unlock-deterministic-after.json` |
| 最终 EditMode | 10/10通过，0跳过，Unity退出0，UTC 04:09:10–04:09:11 | `factory-unlock-edit-verified.xml/log` |
| 最终 PlayMode | 2/2通过，0跳过，Unity退出0，UTC 04:10:21–04:10:25 | `factory-unlock-play.xml/log` |

过滤器见 `factory-unlock-edit-filter.txt`、`factory-unlock-play-filter.txt`，运行方式与上文相同。包含工厂无工坊建造、工坊仍提升产量、完整ContentProgressionValidator定义/source-sink/可达性测试、实际卡片与建造及工业生产。首次 EditMode 启动因测试使用了字符串专用断言而编译失败，证据保留为 `factory-unlock-edit.log`；改用集合断言后最终编译和测试通过，没有弱化新规则的断言。

新增截图 `building-recommendation-factory-unlocked.png` 已查看，旧截图保留。测试后字体恢复原SHA256且无diff，复用已有备份，没有新增备份或临时项目；本次测试进程与临时存档目录无残留。`git diff --check` 通过，资产GUID未变。
