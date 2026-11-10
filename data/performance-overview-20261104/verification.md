# 全局总览与建筑页持续刷新优化（2026-11-04）

起始 HEAD：`1179f27ce3cc3858961ee4f813d52a81ae5217c5`。开始时跟踪源码和资产干净；既有未跟踪证据、`.workbuddy-ai/memory/` 均保留。

用户要求全局总览、精简不必要代码、降低持续负载并启用子代理，随后明确排除累计占用已经很低的优化。本轮收敛到一项已测量的建筑页热点。

## 总览与取舍

| 范围 | 当前事实与本轮证据 | 决定 |
| --- | --- | --- |
| 模拟与 State | Manager 持有唯一可变 State；SimulationManager 默认 0.1s tick。已有活跃建筑索引、供需快照、研究力脏标记和预算缓冲。早期 WoodHouse5/Farm3/Quarry2、人口10 的整 tick 中位耗时 0.063197ms。 | 保留现有实现；不继续零需求分支等微优化。 |
| 建筑 UI | 集中刷新每 0.25s 计算全部卡片 Max/Custom 数量和交易预览。受控长列表71行，实际只有9行可见、62行完全裁剪。原实现3.520680ms/次。 | 唯一实施项：自动刷新跳过整卡完全裁剪的行。 |
| 资源与剧情 UI | 资源详情初始中位耗时0.059020ms；剧情签名每秒检查18章，现有变化检测已生效。 | 不增加缓存或逐项微优化。 |
| 存档、音乐、定义加载 | 自动保存30–60s并检查状态版本；音乐4Hz轮询复用等待对象，停止/换曲释放Addressables句柄；定义一次加载并建立索引。此行是源码事实，不是本轮耗时/泄漏测量。 | 保留现有实现。 |

三位子代理分别审查模拟、UI、存档/音频生命周期；随后运行时代理仅补测试、UI代理仅改三份生产文件、生命周期代理只读复核最终生产diff。主代理独立读取调用者、Prefab根Graphic、SetPage及实际diff，统一执行Unity。

## 实施

- `KingdomUIRoot.AuthoredRows.cs` 缓存现有整卡Button的 `targetGraphic`。七个时代Prefab的该Graphic均为与整卡RectTransform同对象的根Image。
- `KingdomUIRoot.LiveRefresh.cs` 自动4Hz刷新传入可见行模式，跳过整卡 `canvasRenderer.cull=true` 的行。文字自身被裁剪时，整卡仍可见则照常更新。
- `KingdomUIRoot.cs` 在进入缓存Buildings页时标记首刷全量；首次完成后清标记。新Graphic字典与数量标签一起清理和重建。
- Quantity切档、自定义输入及现有无参Editor刷新保持全量；新增bool Editor重载用于验证同一自动路径。公开 `RefreshUI()` 沿用原来的0.25s门槛。
- 生产C#共新增16行、删除2行。测试文件新增194行、删除1行，补两组真实裁剪/缓存重开回归，沿用现有Ten/Max/Custom与稳定按钮用例。
- 玩法、经济定义、存档字段、固定布局、帧率、GUID和`.meta`没有改动。

## 测量

真实Unity 2022.3.62f3c1、Direct3D11、同机Editor。现有UiHotPaths fixture设置高时代、已有研究全部完成、资源1e9、已有建筑各1；是受控性能输入，不是玩家流程或时代通关证明。预热5次、每批10次、7批取中位数。初始探针与最后两批见对应XML/log和runner文件。

| 建筑页Max刷新 | ms/次 | 说明 |
| --- | ---: | --- |
| 最初原始实现 | 3.520680 | `play-baseline.xml`，真实原代码 |
| 旧循环复现 | 3.929280 | `play-old-loop.xml`，保留新测试/API，仅临时移除四行裁剪guard |
| 最终优化实现 | 0.942290 | `play-final.xml`，完整guard已恢复 |

相对最初原实现减少73.24%；相对同一新fixture下的旧循环减少76.02%。两种基线不能相加。稳定刷新需要计算的卡片从71降到9。首次进入与明确数量操作仍全量。

这是局部Editor CPU路径收益，未测量手机实际功耗或整帧收益。未将GC计数器不可用的NA解释为零分配。低占用资源详情仍保持原实现，不因跨批计时波动追加优化。

## Unity执行

| 证据 | XML结果 | 进程/runner |
| --- | --- | --- |
| `edit-baseline.xml/log` | 26/26断言通过，0跳过；UTC 12:20:11–12:20:13 | 官方runner写出XML后240s退出超时，runner1；不记为正常退出 |
| `play-baseline.xml/log` | 1/1通过，0跳过；UTC 13:58:42–13:58:46 | Unity0、包装命令0 |
| `play-old-loop.xml/log` | 2/3通过、1项按预期失败，0跳过；UTC 14:24:03–14:24:10 | Unity2、包装命令1；失败为完全裁剪卡片的数量仍从1刷新到2 |
| `play-final.xml/log` | 5/5通过，0跳过；UTC 14:27:08–14:27:18 | Unity0、包装命令0 |
| `edit-ui-final.xml/log` | UI状态边界3/3断言通过，0跳过；UTC 14:31:53–14:31:54 | 官方runner写出XML后120s退出超时，runner1；不记为正常退出 |

GPU完整CLI、filter、时间和退出码在 `play-*-runner.txt`。两个EditMode调用为官方 `tools/codex/run-unity-tests.ps1`，ProjectPath为本仓库，ResultsPath/LogPath/LatestErrorsPath均显式指定本目录；初始filter为 `RuntimeHotPathPerformanceTests;SimulationDeterminismTests;SimulationBudgetTests`，最终为 `KingdomUiLifecycleTests`。

真实Unity已编译本轮源码和测试。最终GPU和Editor原始日志未发现C#编译、Prefab导入、NullReference或MissingReference错误。EditMode退出挂起保留原日志，没有改测试/脚本以把失败包装成成功。

GPU最终覆盖完全屏外卡片跳过、真实滚入后原刷新周期内更新、缺料/补料按钮、只裁Amount但按钮仍可见、缓存页重开首刷、无参强刷、Ten/Max/Custom、稳定按钮零dirty。未运行全项目套件。

## 收尾

- 动态字体已单独复制恢复并核验本轮前SHA256：`722BFD776012F77B34C3C33AB7BC512BE7DDDDBC4FD9C44709CE0206689C539F`，字体无git diff。
- 初次字体备份放在Temp后被Unity退出清理；使用与本轮前哈希一致的既有备份恢复，后续任务备份放在本证据目录。既有备份未改动。
- 自动审批拒绝了包含字体恢复、任务备份删除与核验的组合命令，原因为 `blocked by policy`；该命令未启动。随后单独Copy-Item恢复字体成功；没有尝试其他删除方式。
- 本轮保留 `D:/GitHub/Kingdom/data/performance-overview-20261104/font-runtime-before.backup`，32,084,075字节（30.60MiB）。后续在允许清理的环境中移除这一单个任务备份，并确认该路径不存在。
- 本轮测试fixture目录与自建Unity进程均无残留；没有创建临时工程/worktree。没有新截图。原有其他输出、备份和用户文件均保留。
- 实施和相关GPU验证已完成；剩余具体事项为上述备份清理及已记录的Editor退出问题，不追加优化清单。
