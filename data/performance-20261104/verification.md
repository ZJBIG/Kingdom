# 2026-11-04 持续性能热点修复

输入：HEAD `45438758efaf2f86a5f5ff88b30b693679ac5488` 加本轮3个生产文件修改；Unity 2022.3.62f3c1。起始仅既有玩家体验handoff和 `.workbuddy-ai/memory/` dirty，均保留。不改定义、数值、存档格式、帧率或Prefab。

## 同机真实 Unity Editor 前后计时

每项先预热，多个批次取单次调用耗时的中位数。耗时下降率为 `(before-after)/before`。这些是CPU路径耗时，不是Android温度、功耗或整帧提升比例。

| 测量路径 | 修改前 ms | 修改后 ms | 耗时下降 |
|---|---:|---:|---:|
| 建筑劳动力/电力/物流四项汇总 | 0.106712 | 0.006427 | 93.98% |
| 幸福度稳定输入重复读取 | 0.001614 | 0.000366 | 77.32% |
| 星区折叠设施菜单的整页刷新 | 1.824150 | 0.461515 | 74.70% |
| 完整 ManualTick(0.1s)，前两项共同效果 | 0.173964 | 0.064796 | 62.75% |

运行时fixture：真实79建筑定义，拥有WoodHouse5/Farm3/Quarry2、人口10；7批中位数，汇总每批1000次、幸福度10000次、tick100次。星区fixture：隔离新场景受控设10星区占领，菜单折叠；5批、每批20次。受控fixture不是自然玩家进度或手机测试。原始数据在 `runtime-before.xml`、`runtime-verified.xml`、`ui-before.xml`、`ui-after.xml` 的 `[HotPathPerf]` 输出。

## 行为及编译

- 生产修改：BuildingManager三处复用原有按稳定ID排序的活跃索引；GameState按食物净速率、人口、食物满足率、幸福度加成四输入缓存；Sectors只刷新展开菜单，仅几何/视口变化时执行全Canvas和外层滚动配置。
- `run-unity-tests.ps1 -Platform EditMode -TestFilter 'RuntimeHotPathPerformanceTests;FoodEfficiencyTests;FlowEfficiencyTests;BuildingCostGrowthTests;SectorBuildingTests;SimulationDeterminismTests;SimulationBudgetTests'`：`runtime-verified.xml/log`，80/80 Passed，runner退出0。
- `run-unity-tests.ps1 -Platform PlayMode -TestFilter SectorBuildingPlayModeTests`：`ui-after.xml/log`，6/6 Passed，runner退出0。覆盖设施门禁、建造/启停/拆除、展开收起重排，以及折叠期间资源改变后展开立即更新、稳定刷新不显式强制Canvas。
- 两个最终运行均完成真实Unity编译，无C#编译/Prefab导入错误，无测试异常。启动时有授权令牌刷新警告，未阻止运行。
- 基线：`runtime-before.xml` 3/3写出，Unity退出阶段卡住，240s包装器超时终止其自建进程；基线计时已记录，不称正常退出。`ui-before.xml` 1/1，runner退出0。
- 中间 `runtime-after.log` 因前一自建Unity尚未退出，项目锁冲突退出1073741845，无XML；未计入成功证据。前一runner结束且确认进程退出后，最终 `runtime-verified` 正常通过。
- 修改前后静态闭包均通过，报告内容一致；离线诊断均73项中的同一既有 `happiness shortage cannot improve efficiency` 失败，前后完整输出相同（`simulator-before.txt` / `simulator-after.txt`），未改模拟器掩盖。
- `git diff --check` 通过。测试动态字体恢复到本轮前字节并核对，自己的备份已删除；测试生成的旧命名目录已移到本目录 `sector-test-artifacts/`，原目录不存在。无临时Unity项目、测试存档目录或本轮Unity进程残留。

未运行全项目套件，未测Android设备温度/功耗/整帧，未重打APK；三个局部路径均超过20%，不将它们相加或当作整机收益。
