# Kingdom P0-04 存档 v9 单主档交接

## 任务与状态

用户授权将存档完全按新系统实现，不兼容任何旧存档，并进一步要求删除现有旧存档兼容代码。P0-04 代码与当前文档已完成；快速 Developer 编译通过。用户要求不要等待 Unity，因此真实 Unity 编译、EditMode、PlayMode、Console 和真机验证均未完成。

## 起始状态与保留工作

- 起始 commit：`a85f0e7`。
- 本任务开始时工作树干净。
- 执行期间出现了不属于本任务的并行变更：`.codex/handoffs/2026-09-13-project-dev-skill-handoff.md`、`.workbuddy-ai/memory/2026-09-15.md` 和 `outputs/Kingdom-Guidance-Consolidation-20260913.txt`；本任务没有修改或恢复这些内容。

## 文件与行为变更

- `SaveFormat.CurrentVersion`：`8 → 9`；删除 `MinimumSupportedVersion`，加载只接受 `Version == 9`。
- `SaveManager` 只管理 `KingdomSave.json` 和写入过程中的 `.tmp`。删除 `.bak` 创建、读取、恢复和路径代码；已有 `.bak` 被忽略，不进行旧数据清理或迁移。
- v9 要求 General、Resources、Buildings、Researches、Workshop、Sectors、Tutorial、Story 全部存在；损坏、缺段、版本不符或语义无效的主档会在彻底重置 Manager State 后只初始化一次新游戏。
- 保留 `.tmp → KingdomSave.json` 原子替换；运行时保存时间戳仍只在主档替换成功后提交。
- 暂停时在内存记录暂停起点并尝试保存；恢复只结算该暂停区间，并在结算前清除本次暂停标记。暂停或恢复保存失败不会让当前进程重复发放，同一次恢复也不会重复结算。
- 删除 `ResearchSaveData.LegacyFormat`、旧研究付款账本推断、退役定义 ID 映射及 `RetiredDefinitionMigration` 源码/meta。v9 中每个研究状态必须显式携带 `PaidResourceCosts` 列表。
- Resource/Building/Research 恢复只接受当前稳定 ID；旧 ID 和未知 ID 直接使主档失效。
- `tools/codex/build-developer-assembly.ps1` 跳过 Bee 响应文件中已不存在的源码项，避免删除源码后快速编译继续引用陈旧条目。
- 更新当前有效的 State、研究付款、剧情、内容路线、仓库地图、技能验收和根规则；历史审计、旧计划与 outputs 未按新事实改写。

## 验证执行

- `dotnet build Kingdom.Runtime.Developer.csproj --no-restore`：退出码 0；0 错误，3 个既有未使用项警告。
- `dotnet build Kingdom.Editor.Developer.csproj --no-restore`：退出码 0；0 错误，0 警告。
- `git diff --check`：通过，仅有现有换行符转换提示。
- 静态残留扫描：生产存档代码中未发现 `MinimumSupportedVersion`、`LegacyFormat`、`RetiredDefinitionMigration`、backup 路径或旧候选加载；`.bak` 只保留在“连续保存不生成备份”的测试断言中。
- 新增/调整但尚未由 Unity Test Runner 执行的覆盖：v8/v10 拒绝、损坏主档新游戏、缺顶层段新游戏、旧/未知 ID 拒绝、严格研究支付账本、暂停保存失败只结算一次实际暂停区间、无 `.bak`。
- 未执行真实 Unity 编译。曾启动批处理编译，但用户要求不要等待后立即停止；没有产生可用验收日志。
- 未执行 EditMode、PlayMode、Unity Console 和 Huawei P40 Pro 真机验证。

## 风险与证据限制

- Developer 编译不包含测试程序集，不能替代 Unity Test Runner。
- 暂停生命周期的强类型测试入口和新增测试尚需 Unity 执行验证。
- 冷启动、系统后台回收和真机杀进程仍属于 P0-05；现有实现只保留最后一次成功主档时间戳作为冷启动离线基线。
- 用户最新“删除旧兼容代码”指令覆盖原计划中的“启动清理遗留 `.bak`”；运行时不会读取、删除或管理旧 `.bak`。

## 下一具体行动

项目空闲且允许等待 Unity 时，依次执行：

1. `tools/codex/compile-unity.ps1`
2. `tools/codex/run-unity-tests.ps1 -Platform EditMode`
3. `tools/codex/run-unity-tests.ps1 -Platform PlayMode`

核对 XML 为非零用例且零失败，再检查 Console 无新增编译错误/异常。随后在 P0-05 做 Huawei P40 Pro 后台、杀进程和系统回收验证。
