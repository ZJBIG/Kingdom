# 测试精简：2026-10-17

## 范围和结果
仅修改55份测试源码；此前6份未提交玩法代码SHA256与上轮source-inputs.json一致。没有修改生产代码、资产、meta、GUID或真实存档，没有提交或推送。
本轮基线34865行、6479处断言、920个[Test]/[UnityTest]标记、183个TestCase；最终33294行、5918处断言、864个普通测试标记、180个TestCase。净删1571行、561处断言、56个普通测试方法及3个参数用例。以上为源码静态计数，不是runner实际用例数或加速幅度。
其中PlayMode净删291行、158处断言、3个UnityTest，97→94个普通测试标记。

## 删除与保留
- 删除新建对象默认值、getter/DTO赋值回读、复制资产费用/颜色/尺寸/字体数值、过时布局与反射结构断言、重复周期参数与重复场景测试。
- 删除188处随后立即解引用同一对象的冗余非空前置检查；对象缺失仍会使行为测试失败。明细redundant-preconditions.json。
- 移除失去调用的辅助方法、字段、导入以及不可达分支；没有清除无关生产代码。
- 合并PlayMode独有的锁定剧情保密和月球导航检查到保留用例，保留交互、几何、raycast、音乐切换和页面差异。
- 保留真实Manager存取、事务原子性、非法输入、资源守恒、缺料降产、幸福度和人口模式进度回归、稳定ID和存档协议检查。
- 修正旧的跨页面下划线要求：生产树不加下划线，星区建筑名称保留。
- 首轮运行发现ResetDerivedEconomy旧检查要求临时容量覆盖载入食物；该要求与真实容量重建后截断食物的既有修复冲突。改为验证中间步骤保留食物且库存不虚增容量；实际SaveApply路径仍单独验证。

## 验证
- dotnet build Kingdom.DeveloperTests.csproj --no-restore：exit0，0errors，6条既有TextureImporter.spritesheet废弃API警告；覆盖Editor和PlayMode测试编译。证据compile-tests-final.log。此后最后一处断言修正由真实Unity重新编译并运行。
- 首次真实Unity导入60秒超时，无XML；日志editmode-final.log，未作通过证据。
- 缓存导入后的26类EditMode：439用例，438通过、1条上述旧断言失败，0跳过，6.5106804秒；editmode-cached.xml/log。失败证据保留。
- 修正后整个KingdomLogicTests重跑：158/158通过，0跳过，2.2346202秒；editmode-reset-final.xml/log。其他281用例沿用前批次通过结果，没有把合并结果说成单次全绿。
- Unity均在结果落盘后原生退出挂起，包装器60秒清理自启动进程，exit1；runner-cached-status.txt及runner-reset-status.txt。断言通过不等于runner正常退出。
- git diff --check通过；无空测试方法。未运行PlayMode或完整测试套件，不声称已测实际加速。

## 可恢复性
pre-existing-changes.patch保存本轮开始前已有改动；baseline.json/after.json/summary.json保存计数，test-inputs.json保存最终测试源码SHA256。未清理此前tools/codex/__pycache__。
