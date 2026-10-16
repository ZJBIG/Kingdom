# 建筑名称、容量提示与数量输入（2026-10-15）

本文件记录后续修改，截图同名文件已更新，优先于此前单纯大图标的描述。

## 最终界面

- 普通建筑卡片不显示下方功能介绍；保留公共模板的Metadata组件并禁用，取消普通建筑的无效绑定。星区建筑和资源卡片仍保留各自说明。GetBuildingRoleSummary仍被星区建筑使用，不是死代码。
- 基础及7时代建筑卡片Label占完整标题区（anchorMin.y=.4→0），字号上限30→42，下限24→30，允许长名换行和自动适配。作者工具同步，8份Prefab静态核对通过。
- 详情按最终用户要求显示“土地：需求 60，缺少 20”，无不足则“土地充足”；生产力同理。零消耗住房在已有超额生产力占用时仍显示生产力充足。升级释放容量用“释放 x”。不改变需求和缺口计算。
- 自定义数量输入框去掉长占位，改为“数量”；复用ControlMetal真实子Sprite（fileID -5939887275736818340）、Sliced、pixelsPerUnitMultiplier=14，深色底与相邻按钮对齐。避免Image颜色与Selectable颜色重复相乘导致黑块；输入、聚焦、数量解析与命令行为不变。

## 验证

- `label-final-playmode.xml`：ReviewInteractionPlayModeTests和KingdomCollectionPlayModeTests共14/14通过，0失败跳过，Unity退出0。首次回归发现旧“隐藏时代装饰颜色”断言和旧“住房显示空闲量变化”断言，分别改为真实时代画作及零需求/零缺口语义，保留建造、升级、置顶、支付和长名称截断行为检查。
- `readable-final-playmode.xml`：最终自然语言提示、充足提示、标题和数量框回归11/11通过，0失败跳过；XML已生成，Unity关闭阶段超时，自有进程已结束，不计runner成功。
- `input-skin-playmode.xml`：最终输入框颜色修正后，真实GPU定向1/1通过，0失败跳过。检查普通/警告glyph、全部不足、批量切换、标题未截断、详情需求/缺少和充足提示，以及数量框有效纹理/深色Selectable配置。XML生成后关闭阶段100秒超时，自有进程已结束，不计runner成功。
- 真实Unity编译完成；最终日志无CS错误、NullReference、MissingReference、UnassignedReference。相关源码和文档diff检查通过。Editor资产测试的断言已按新语义更新并编译，未重新运行EditMode或全仓测试。
- 已查看最终1760x800 `all-shortages.png`：大名称无下方小字、三图标并排、自然语言需求/缺少、数量输入框金属边框及暗底均已显示。充足图见`ready.png`。

字体已恢复本轮起始字节，SHA256 B9BD028E9AFC401532E35CE5A76782C40C7426FCEF7B0B94E12E53085CCBA5DE；临时备份物理删除，无Unity进程及ReviewInteraction/Collection测试存档fixture残留。无经济数值、GUID、稳定ID或存档格式修改，无commit/push。
