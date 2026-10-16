# 建筑卡片大图标与同时不足（2026-10-15）

当前实现：普通建筑卡片按所选建造/升级批量显示所有不足图标（土地3、生产力4、材料5），无旁边文字；全部满足显示普通材料图标2。已有建筑的生产效率移至完整/简版详情。时代/研究/工坊等前置门槛仍显示实际原因。

- `BuildingManager.GetConstructionFailures`复用原交易校验收集多项不足；真实建造/升级仍按原顺序拒绝失败交易。保留费用倍率、合并费用、浮点边界容差、升级既有生产力超额占用规则。
- 基础及7时代BuildingCard的Effect字号上限128、自动适配、居中、无换行；状态栏从.48-.62拓宽到.40-.64，时代栏.23-.34、数量栏.34-.40。作者工具同步。
- 卡片不再显示不足数字、可建或已有建筑效率；详情保留所选批量的需要/空闲/缺口、建议和生产效率。

## 当前验证

- `multiple-playmode.xml`：真实Unity、Direct3D11，ReviewInteractionPlayModeTests **11/11通过，0失败/跳过，Unity退出0**。覆盖两项/三项不足同时显示、数量切换、逐项解除不足、材料充足图标、已有建筑效率详情。
- 三图标测试检查真实TMP字符元素/索引、每个glyph高度至少卡片高度65%、glyph横向位于状态区域内；不使用TMP3.0.9有缺陷的spriteCount。
- 实际RT截图1760x800：`all-shortages.png`、`land-shortage.png`（土地+生产力）、`productivity-shortage.png`、`materials-shortage.png`、`ready.png`。人工查看全部不足及充足代表图，三个警告图标完整且未重叠操作按钮。
- `targeted-editmode.xml`：BuildingCostGrowthTests、SectorBuildingTests及FinalIndustrialChains_UpgradeTransactionsAndSaveJsonRemainValid **24/24通过，0失败/跳过**。追加升级三项不足、复用列表清空、预览数量不变和失败交易拒绝覆盖。Unity已写出XML，但关闭阶段180秒超时，由启动器结束本轮自有进程；不能称runner通过。
- 首次较大EditMode集合`transactions-editmode.log`在180秒内未产出XML，不计通过证据。此前72字号和单图标结果仅为历史；当前证据以上述multiple/targeted文件为准。
- 当前日志无CS编译错误、NullReference、MissingReference、UnassignedReference错误。相关源码/8卡片Prefab/docs的`git diff --check`通过。未运行全仓测试，未扩展修复Editor关闭问题。

## 收尾

测试前动态字体已从本轮备份恢复，SHA256 `B9BD028E9AFC401532E35CE5A76782C40C7426FCEF7B0B94E12E53085CCBA5DE`，备份物理删除。无Unity进程及ReviewInteraction测试存档目录残留。保留其他已有工作树修改，无新commit/push、包安装或真实存档操作。
