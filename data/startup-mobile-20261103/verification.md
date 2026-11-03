# 启动、移动端与生产树验证（2026-11-03）

## 实现
- 冷启动和运行中后台恢复使用同一个分帧离线会话，每帧预算 4 ms；同步 AdvanceOffline 保留。没有扩大动态积分步长或缩短收益窗口。保留 24h 上限、2h/8h 衰减及战役/殖民余量。
- Prefab 创作 LoadingOverlay/Status 和 SafeAreaRoot CanvasGroup；先绘制加载状态，再结算。加载期间禁止操作、教程接管、实时模拟和保存；切后台暂停并延长同一会话，错误保留原磁盘存档并显示原因。
- FeudalAdministration TerritoryReward 25 -> 2200。用户明确选择现有完成存档一次性 +2175；General.HasCurrentFeudalTerritoryReward 持久化领取状态。恢复已保存领土总量后补差，避免重建永久效果重复发奖。
- 生产树保留满足原前置条件的低级来源；按 Manager 实际升级链规则标灰并显示“已被替代 · 不可新建”。保留详情、图片、展开与拖动，集中刷新不重建展开树。建筑列表/实际建造规则不变。
- 移动端固定 30 FPS，移除交互升 60 的检测，保留 DPI 拖动阈值。音乐播放策略和导入参数保留。
- 回归发现生产树视口变化补偿与拖动组件裁剪的双重位置处理，统一到共享组件裁剪之前，新增尺寸往返测试；研究树默认路径保持原行为。

## 本轮证据
- closure-before.md / closure-after.md：静态闭包通过，Industrial 83/37/50、Spacer 47/52/26、Ultra 15/3/3 全可达。
- simulator-before.json / simulator-after.json：同一既有失败 happiness shortage cannot improve efficiency，其余诊断通过；未为本任务调整幸福度经济。
- edit-targeted.xml / edit-targeted.log / edit-targeted-errors.txt：57/57 Passed，运行器退出 0。包括同步/分帧一致性、衰减边界、延长会话、领土重复加载防重与补差、新研究奖励、生产树规则及模拟预算。
- play-gpu-final.xml / .log / .exit：31 项，30 Passed、1 Failed，退出 2。失败为旧测试访问当前 Prefab 不存在的 Heading，非产品异常；改为实际设施卡片菜单内边界断言。
- play-gpu-sector-accepted.xml / .log / .exit：上述剩余一项 1/1 Passed，退出 0。最终覆盖的 31 项均通过，分两次运行；没有声称整套同一次通过。
- GPU 覆盖隔夜启动、结算中暂停与禁存、替代建筑交互、截图/预览、研究树、手势、音乐续播/手动暂停及输入反馈。
- compile-final.log：真实 Unity 2022.3.62f3c1 编译，运行器退出 0；无 C# 编译或资产导入错误。最初 compile-first 返回 0 但存在重复 fileID 导入错误，已修正新遮罩 ID，仅最终日志作为干净编译证据。
- 34,800 秒启动回放：43 个切片，maxStepMs=0.841、maxSliceMs=4.44；startup-progress.png 显示“正在结算离线进度 0%”，测试另外断言跨帧推进和结算前磁盘字节不变。
- frame-cadence-20261103-050559-142.csv：同一 Overview，模拟暂停/音乐停止，每档 1s 预热 +3s 测量。60 cap 实际60.04 FPS，30 cap 实际30.01 FPS；两档 GameObject1896->1896、AudioClip43->43，GC collection 均0。当前线程分配计数 API 不支持，标 NA；短时 Mono 使用量增加，不据此断言泄漏或无分配。
- save-cost-20261103-050559-167.csv：隔离存档同步保存总耗时 4.164 /12.593 /6.697 ms，保存仍是顿挫候选。没有引入后台保存系统。
- 最初 GPU 重复 fileID 导入错误及旧裁剪失败证据保留，后续修复没有弱化树根可见断言；最新双尺寸生产截图位于 data/experience-20261023/production-pages/20261103-051000。
- git diff --check 通过（现有 LF/CRLF 提示保留）；字体按 font-before.sha256 恢复且 hash 一致，font-restored.sha256 已保存，本轮 backup 已移除。无本轮 Unity/临时测试目录残留。

## 边界
没有手机存档，隔夜测试使用隔离构造存档。没有运行全项目测试；没有手机功耗/温度/耳机设备切换实测。本轮未重建 Builds/Android/Kingdom.apk，不改真实存档，不提交或推送。

## 追加：正常游戏画面加速（用户最终要求）
- 最终行为取代上述遮罩/进度提示设计：没有新增横幅、百分比、加速文案或操作锁。离线结算时复用正常UI刷新，日期、资源和研究按真实结算状态快速推进；导航、建造、研究等命令可照常执行。读档尚未完成的短暂初始化阶段仅暂停尚无有效数据的刷新；异常复用原Tooltip显示实际错误。
- 删除本轮新增LoadingOverlay和CanvasGroup，不保留多余Prefab节点或交互门控；继续保留内部结算事务、正常tick防重和加载期间自动存档延后提交。强退回到上次已落盘状态。
- play-gpu-natural-acceleration.xml/log/exit：5/5 Passed、运行器退出0，实际GPU场景验证正常HUD日期变化、加速期间切换建筑页并成功建造、无LoadingOverlay、后台暂停继续、存档原字节不被半结算覆盖及音乐续播。
- 最新34,800秒样本223切片，maxStepMs=2.282、maxSliceMs=4.922。启动截图已核对为正常概览画面，无加速提示。该样本期间包含玩家建造和正常UI刷新，不能拿切片数直接比较纯后台样本性能。
- 额外旧用例NewGameCommands_BuildResearchAndReloadWithoutGrants在农场Efficiency>0断言失败（实际0），不在本次加速UI修复范围，未弱化断言或修改人口/生产规则；相关失败日志保留。
- 首次Android构建通过设置/UI/YAML预检，实际Burst编译OutOfMemoryException失败，旧APK没有更新。用户明确要求暂停打包，等其唤醒后再构建；当前不再启动Unity/构建任务。
- 用户已授权上传全部有意义源码/资产及清理生成测试文件。旧未跟踪data证据216文件/303139868字节，经逐文件SHA256验证归档后从原位移除；源码、资产、必要回归测试保留。当前生成证据也归档至同一目录的data/startup-mobile-20261103，活动目录只保留此摘要。构建缓存待恢复打包完成后清理，避免重建中间件。
- 原始XML/log/CSV/截图及历次失败均可从.codex/archive/2026-11-03-pre-apk-evidence恢复；归档不上传Git。字体恢复本轮前SHA256且临时备份删除。
