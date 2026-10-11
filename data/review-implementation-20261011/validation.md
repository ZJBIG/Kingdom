# 10 月 9 日审查实施验证（2026-10-11）

范围：继承前次实现，完成三份 10 月 9 日审查的明确修缮，补齐本轮复核遗漏。按用户最终指示先提交本轮审查成果；并行插画任务的背景卡、正文滚动、插画预览及其新测试保留在主工作树，由该任务以后单独交付。

## 输入与隔离

- 基线：`main / ee6e1739d7f5a5e9bea1190dbab434f2a4137922`。主仓在起始时已有大量前次暂存实现及独立清理变更；本轮没有丢弃这些修改。
- Unity：`D:/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe`，版本 `2022.3.62f3c1`。
- EditMode 在主仓运行；相关 Manager、Runtime、定义与本轮测试输入与交付一致。其运行期间的并行剧情 UI 不属于本轮提交。
- PlayMode 复用已有 `D:/GitHub/Kingdom-review1009`，逐文件对齐主仓索引中的 Assets/Packages/ProjectSettings，覆盖前独有内容保存在 `.codex/archive/review1011-validation-input/previous/`。没有复制 Library/Temp/Logs，没有再新建项目。实现暂存树为 `70cd2e51a52cd9186fb42e7b2c1ab19596afbd24`，不含此后添加的交接与证据。
- PlayMode 使用既有存档隔离 fixture，不访问用户真实存档。主仓的未暂存 Prefab/场景/字体以及并行插画源码均保留。

## 结果

成功 XML 与完整诊断原样压缩在 `results-and-diagnostic.zip` 中，原始日志及首次失败保存在 `logs-and-initial-failures.zip`。压缩只减少版本库中的重复文本，不修改测试结果。下表 XML/JSON 名称均为前一个 ZIP 内的文件名。

| 检查 | 实际结果 | 证据 |
|---|---|---|
| 新档静态闭包，修改前/后 | exit0；Industrial/Spacer/Ultra 当前定义全部可达 | `content-closure-before.md`、`content-closure.md` |
| 原始建筑资源流 | 72 资产，正向产出/消耗资源重叠 0，exit0 | `tools/codex/building-resource-flow-check.ps1 -ProjectPath D:/GitHub/Kingdom` |
| 确定性诊断 | 13 项，passed=true，exit0 | `simulator.json` |
| 真实 Unity 编译 | 主仓首轮 exit0；最终交付输入的隔离 PlayMode 导入/编译也正常 | ZIP 内 compile 与最终 PlayMode 原始日志 |
| 相关 EditMode | 336 通过，0 失败，0 跳过；Unity exit0 | `editmode.xml`、ZIP 内 `review1011-editmode-r3.log` |
| 相关 PlayMode | 55 通过，0 失败，0 跳过；Unity exit0 | `playmode.xml`、ZIP 内 `review1011-playmode-r2.log` |
| Console | 最终 PlayMode 无 C# 编译错误、NullReferenceException、MissingReferenceException；EditMode 的故意保存失败由对应失败路径测试捕获 | ZIP 内原始日志 |

EditMode 过滤：`AuditRuntimeRegressionTests;EraInventoryShortfallTests;InvestmentChoiceTests;StrategicPreviewRegressionTests;TutorialManagerTests;StoryManagerTests;RelicManagerTests;RelicCampaignTests;RelicSaveTests;GlobalEconomyDefinitionTests;ContentProgressionValidatorTests;C6IndustrialClosureAuditTests;IndustrialWorkshopAvailabilityTests;FlowEfficiencyTests;FoodEfficiencyTests;BuildingCostGrowthTests;ResearchPaymentAutoTests;UltraProjectManagerTickTests;KingdomLogicTests;SaveManagerTests`。

PlayMode 使用 `-batchmode -force-d3d11 -runTests -testPlatform PlayMode`，过滤：`ReviewInteractionPlayModeTests;StoryIllustrationPlayModeTests;KingdomOnboardingPlayModeTests;RelicPlayModeTests;KingdomPlayModeTests`。覆盖最新剧情折叠/导航、真实研究图布局与拖动、批拆/研究取消确认、资源来源导航、有效数量反馈、住房恢复/升级解释、远征撤退/部分维修、遗迹两条路线支援、新档公开命令与保存读回。

## 首次失败与修正

- EditMode 首轮 335/336：旧存档 fixture 尝试暂停尚未开始工作的遗迹。先通过强类型测试入口建立调查，再暂停；读档后额外断言回到 Discovered。产品的暂停状态校验没有放宽。
- 一次主仓编译因并行插画写入时尚未完成的新类型而失败；随后真实编译通过。保留该轮日志，没有纳入进行中的类型。
- 隔离 PlayMode 首轮 53/55：Onboarding 曾夹入并行任务的新节点路径；住房说明测试未打开既有 ShowDetails。只修正交付版本 fixture 的固定节点路径和明确选择完整详情，保留原行为与布局断言；主仓并行文件不覆盖。
- 失败 XML 和原始日志均在 `logs-and-initial-failures.zip` 内，未覆盖或删除。

## 清理与证据边界

本轮成果已经在主仓索引，成功 XML/日志及旧 review-evidence 已迁回主仓。尝试原生 PowerShell 删除 `D:/GitHub/Kingdom-review1009` 时，自动执行策略拒绝整个命令，只返回 `blocked by policy`；未换方式绕过。目录仍保留且不在 git worktree 注册中。下一清理动作是在策略允许后核对上述归档，直接移除该精确目录及其缓存。未触碰 `D:/GitHub/Kingdom-source-audit` 的独有未完成补丁。

这些是定向回归及静态检查，不是全仓测试、真实 Unity economy parity、玩家节奏或设备试听验收。确定性诊断含人工 fixture；没有用它调成本、倍率或扩展策略搜索。外部运行体验由用户自行验收，不列为本代理待完成门槛。
