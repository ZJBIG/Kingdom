# 2026-10-15 卡片名称垂直居中与说明清理

## 最终表现

用户明确更正“居中”为上下居中。本轮覆盖前次建筑名称水平居中的实现：建筑、资源、工坊卡片名称统一靠左，采用字形几何垂直居中（Horizontal=1、Vertical=4096）。标题区域纵向覆盖整张卡片，字号上限42/下限30，换行、自适应与长名称省略保留。基础3份及资源/建筑7时代共17份Prefab同步；时代标题底板也覆盖完整高度。数量输入框仍保留水平居中。

资源与工坊Metadata关闭，并删除普通资源/工坊及远征支援组件的无效描述绑定。完整Description保留详情；数量、变化率、时代、购买状态、按钮和建筑不足图标保留。星区摘要仍有实际用途，保留其共享Metadata组件及绑定。作者工具与相关测试同步，未改变经济数值、ID、GUID或存档。

## 本轮证据

- 真实Unity编译完成。GPU PlayMode：vertical-playmode.xml，17/17通过、0失败0跳过，Unity正常退出0；覆盖ReviewInteraction、KingdomCollection及多时代画廊。
- 定向EditMode：vertical-editmode.xml，11/11通过、0失败0跳过；关闭阶段120秒超时，启动器结束自有进程；用例通过，不称runner正常退出。
- 三种分辨率资源/建筑/工坊截图由实际Camera输出；已查看资源、工坊、建筑1760x800和7时代资源2640x1200截图。
- 17份Prefab标题锚点、水平/垂直对齐、换行、自适应与说明隐藏静态检查通过。
- 现有运行测试新增三类卡片实际glyph垂直中心与名称靠左断言，保留长标题裁切检查；数量框仍验证水平及垂直中心。
- XML/log位于本目录；截图位于data/production-compact-20261013、data/planner-pages-20261013及本目录all-shortages.png。

未运行全仓测试。字体已恢复起始字节（SHA256 B9BD028E9AFC401532E35CE5A76782C40C7426FCEF7B0B94E12E53085CCBA5DE），临时备份物理删除；无Unity进程及本轮存档fixture残留；未创建临时Unity项目，未commit/push。
