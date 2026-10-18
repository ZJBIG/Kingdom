# 工坊预览重复重建优化（2026-10-18）

## 范围与行为
WorkshopManager净增10行，缓存当前候选的一份ProgressionModifierState，键为Current引用和canonical候选定义。研究完成/重置/恢复以及工坊购买/回滚/恢复都会替换Current，因此失效；切换候选也重建。数量、效率、幸福度及事件倍率不缓存，仍每次重新计算速率。无资产、经济数值、存档字段、meta/GUID变更；不执行PlayMode，不自动commit/push。基线见baseline.json/pre-existing-changes.patch，最终两文件指纹见source-inputs.json。

## 验证
- 真实Unity最终编译及EditMode：verified.xml，235/235，1.9924023秒；8类范围沿用round2，新增2方法（功能回归及独立Performance类别）。测试覆盖数量/效率/事件变化、候选切换、研究快照重建、已购候选不重复增益、恢复后重新显示。
- 开发程序集compile-final.log：0错误，6既有TextureImporter警告。之后只将分配门槛改为至少减少一半，最终由真实Unity重新编译验证。
- Unity ProfilerRecorder GC.Alloc：最终固定夹具（全部研究完成、无持有建筑、同一候选、各8次）记录到重复预览112字节/次，对比快照失效时1225字节/次，约91%下降。这是该调用的采样分配结果，不是整局内存/FPS测量，也不代表有建筑时仍按相同比例降低。
- 反证：临时只把缓存条件改为每次重建，uncached-regression.xml的1项性能测试按预期失败，记录1950/1737字节/次。之后从WorkshopManager.final.cs.txt完整恢复最终源码并完成verified.xml；工作树已无临时旁路条件。
- 测量迭代：before.xml使用当前Unity Mono返回0的.NET分配计数，不能作分配证据；uncached.xml暴露初版“略少即通过”的门槛会被记录器波动误判，已加强为至少减少一半（关系门槛，无写死精确字节断言）。after.xml为首次235/235，但最终依据verified.xml。
- 独立只读核查确认所有生产Current变更入口、交易回滚、Reset/恢复无回调间隙；Singleton.Instance只查找，不进行惰性初始化。未发现新增确定问题。git diff --check通过。
- Unity XML落盘后退出挂起，60秒包装清理自启动PID，runner exit1，详见verified-status.txt。未运行PlayMode/全套；持续目标保持active。

## 下一步
继续核查运行时代价与明确玩法缺陷，不扩展通用缓存架构；缓存仅保留一份候选，无集合增长。外部游戏体验由用户验收。
