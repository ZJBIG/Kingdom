# 居中与建筑界面边界审查（2026-10-15）

范围：建筑卡片、数量输入、容量/材料详情及其集中刷新。不宣称全游戏审查；不改经济数值、交易规则或存档。

## 修复

- 基础及7时代建筑Label改为水平居中，并用TMP Midline/Geometry按实际字形垂直居中（Horizontal=2，Vertical=4096）；数量输入文字及占位同样按字形居中。字号、卡片分栏、Pin与操作按钮保留。作者工具同步。
- 自定义数量输入框增加自身RectMask2D，padding=(12,4,12,4)，裁切长数字，防止侵入相邻控件。已有可见金属边框保留。
- 普通建造详情的材料需求乘以Manager现有GetConstructionCostMultiplier；此前遗漏建造效率折扣，显示需求可能高于实际扣款。升级继续使用现有GetUpgradeResourceDeltas。
- 详情刷新缓存新增可用土地、可用生产力、ProgressionModifierState引用和实际升级目标；资源版本签名同时覆盖升级目标材料。修复人口/土地改变、建造倍率改变、升级目标解锁或目标独有材料改变时，详情可能保留旧显示的问题。仍按现有0.25秒节流及变化判断刷新，不增加逐卡片Update。

## 验证

- `alignment-playmode.xml`：真实Unity/Direct3D11，12/12通过，0失败跳过，Unity退出0。
- 最终`audit-final-playmode.xml`：真实Unity/Direct3D11，ReviewInteractionPlayModeTests **13/13通过，0失败跳过，Unity退出0**。
- 新回归按实际TMP字形bounds检查名称、占位及短数字中心；长数字检查实际CanvasRenderer裁切。容量回归在不重选对象、不调用ShowDetails的情况下改变人口和土地，并核对详情更新；费用回归应用DraftingTables实际modifier，核对所显示材料值与真实建造扣款一致。
- 既有回归继续覆盖多项不足同时显示、批量、充足状态、住房恢复、升级、确认与研究手势。最终日志无CS、NullReference、MissingReference、UnassignedReference、Assertion错误。
- 已查看更新后的1760x800 `all-shortages.png`：名称居中、图标并排、自然语言容量提示及数量输入框可读。
- 8份建筑卡片静态居中配置通过；Root组件ID唯一、输入框mask引用通过；相关源码git diff --check通过。未运行全仓或EditMode，不将定向检查称为全面验收。

## 清理

字体恢复测试前字节，SHA256 B9BD028E9AFC401532E35CE5A76782C40C7426FCEF7B0B94E12E53085CCBA5DE；font-before-alignment-tests.tmp物理删除，无Unity进程及ReviewInteraction测试存档目录残留。无新commit/push，无临时项目或真实存档操作。
