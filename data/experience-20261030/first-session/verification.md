# 首局成长引导验证（2026-10-30）

## 输入与范围

- 工作树基础 HEAD：`5ade66a7d409edda4ca60805bc015686389c0e07`；包含实施前已有未提交改动。相关原始差异见 `pre-existing.patch`，最终源码/教程资产 SHA256 见 `verified-inputs.json`。
- 最终运行 Unity：`D:/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe`，工程 `D:/GitHub/Kingdom`。
- 没有修改经济定义、支付/交易接口、存档版本或已有资产 GUID；新增 FirstSession partial 具有独立 `.meta`。没有创建临时 Unity 项目或 worktree。

## 最终有效证据

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 真实 Unity 编译 | 通过，Unity/包装退出 0 | `compile-final.log`、`compile-final.exit` |
| EditMode | 78/78，失败 0，跳过 0；正常退出 | `edit-verified.xml`、`edit-verified.log`、`edit-verified-errors.txt` |
| GPU PlayMode | 27/27，失败 0，跳过 0；Unity/包装退出 0 | `play-gpu-complete.xml`、`play-gpu-complete.log`、`play-gpu-complete.exit` |
| 当前静态内容闭包 | 145 研究、92 工坊、79 建筑全部可达 | `closure.md` |
| 本次涉及文件 diff 空白检查 | 通过 | `git diff --check --` 后接本交接列出的源码/测试/教程资产 |

- EditMode filter：`TutorialManagerTests;TutorialStageTests;StoryManagerTests`；使用 `tools/codex/run-unity-tests.ps1 -Platform EditMode`，本目录下的 `edit-verified.*`。
- GPU PlayMode：`-batchmode -projectPath D:/GitHub/Kingdom -runTests -testPlatform PlayMode -testFilter "KingdomStagedOnboardingPlayModeTests;KingdomOnboardingPlayModeTests"`，本目录下的 `play-gpu-complete.*`；未加 `-nographics`。
- 最终编译：`tools/codex/compile-unity.ps1 -ProjectPath D:/GitHub/Kingdom -UnityPath D:/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe -LogPath D:/GitHub/Kingdom/data/experience-20261030/first-session/compile-final.log -TimeoutSeconds 180`。
- 最终运行日志无 C# 编译错误、NullReferenceException、MissingReferenceException、AssertionException 或缺字警告。Unity 许可证启动提示保留于原始日志。
- 较早的 `edit-first/second/third/final/complete`、`play-first/second/third/gpu-final/gpu-verified` 是迭代证据，不代替上述最终结果。

## 真实按钮路线与等待

`FirstSession_RealButtonsAndNaturalProductionReachStoneAge` 使用隔离全新存档、真实 EventSystem 点击和 ManualTick，不赠送资源，不设置研究完成。经过木屋、农业、农场实际供粮、采石研究/采石场、切石研究/工坊实际产出、真实定居前置、定居聚落、文明规划和石器跃迁。

- 合计 `4484.86770353102` 秒模拟时间（约 74.75 分钟），不是玩家实时时长或吸引力测评。
- 最长停留行动为 `research:StoneAgeSettlement`：3565.93 秒，其中材料等待 3390 秒，研究 175.93 秒。按需求保留现有经济数值。
- 完整等待段与建造时点：`actual-waits.log`；按行动汇总：`actual-waits.csv`。
- 覆盖缺料/失败、研究排队及进行中、生产零效率/零秒 tick、人口并行反馈、跳过、提前完成、保存恢复、共享目标、门禁释放/恢复、真实支付和反馈去重。
- 第一座木屋真实建成时记录整合后的 `resources` 完成，保留 FirstFire 剧情依赖；跃迁完成后不再重播资源/人口/首次时代操作课。

## 视觉与清理

- `screenshots/` 八张 2640×1200 GPU 截图：首屏、木屋缺料、建造按钮、木屋完成、农业研究等待、农场建成后等实际生产、加工建成后等实际生产、石器时代后续目标。主代理逐张检查；正文 preferredHeight 由测试检查，不超出正式面板，按钮/高亮可达。沿用原 Prefab，无需布局变更。
- 字体精确恢复实施前已有修改，SHA256：`15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71`，见 `font-restored.txt`；临时 `font-before.asset` 已删除，哈希记录保留。
- `Temp/StagedOnboarding-*` 无残留，本轮启动的 Unity 均已退出；未操作真实存档。

## 证据限制

- 离线确定性诊断尝试 `dotnet run --project tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore -- --json`，被 NETSDK1004 阻塞：缺 `tools/NewEconomySimulator/obj/project.assets.json`。未执行包安装/restore，不宣称离线诊断或 parity 已通过。
- 未跑全项目测试；没有把自动按钮回归或模拟时间描述为玩家体验验收。
