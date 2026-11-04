# 离线分配、数字稳定与 Android 交付（2026-11-04）

- 输入：原工作树，起始 HEAD `45438758efaf2f86a5f5ff88b30b693679ac5488`；保留并整合既有两轮性能改动，未新建工程/工作树。
- 总览：固定 Prefab 工具栏显示离线余额，正常/5×/20×/1000×，消耗分别为0/5/25/500倍。默认正常速度，余额存于当前v1的General.RemainingOfflineSeconds（缺省0），仍封顶24小时。每个额外推进tick完成后才扣费，4ms预算在tick间检查，切回正常立即停止加速。
- 暂停：重复回调幂等；后台再次保存沿用首次暂停时间戳，恢复累计余额且保存，防止重复领取或少计。
- 资源：结算和UI共用GetNetRate；只抵消已短缺且处于六位算术误差内的平衡残差，保留真实微量生产与库存。
- UI：支付括号库存充足绿/不足红；固定需求/库存列、固定字号、等宽数字，资源列表/建筑数量/顶栏数字等宽。删除1.5×拖动类及引用，恢复原生ScrollRect/1×手势。删除旧离线摘要界面、按钮和失效加载进度字段；旧积分诊断限定Editor。
- 帧率：手机固定30→最高可用屏幕刷新率请求；模拟时钟独立。

## 验证

- 真实Unity EditMode：edit-final.xml 227/227，runner退出0。初轮225/227的两处旧fixture绕过Manager活跃索引，改用Manager建立状态，保留原断言。
- GPU PlayMode：play-verified.xml 50项中49通过；唯一失败为新增截图fixture全存档恢复遇到其自设星区不合法，改为只恢复General后，layout-verified.xml 1/1通过、退出0。最终50个相关用例全部通过。此前颜色测试等待不足与过期TMP引用的失败日志保留。
- 覆盖余额/四档按钮、耗尽/保存、重复暂停与暂停中保存、工具栏边界/支付红绿及列位置、滚动/研究图/剧情交互；无跳过项。真实图像已查看：hud-efficiency-audit-20261026/player-details/00-overview-offline.png、03-research-payment.png。
- 静态闭包通过：Industrial研究83/83、工坊37/37、建筑50/50；UI/Android设置检查与脚本GUID检查通过。
- deterministic.json：确定性诊断仍有既有 `happiness shortage cannot improve efficiency` 失败，未改幸福度规则、未将其报告为通过；无真实Unity trace parity新采集。
- APK：Unity真实Android编译/IL2CPP/Gradle退出0，0错误13警告；212190156字节。com.ZJ.Kingdom，0.5，ARM64，min22/target35。apksigner v1/v2通过。
- APK SHA256：DC7B7DB1E88A869AA61FEC85EA75457C2310C68513FA2907108A2B23656286E8。
- 字体恢复到运行前SHA256 722BFD776012F77B34C3C33AB7BC512BE7DDDDBC4FD9C44709CE0206689C539F；本轮备份已移除。未创建临时工程，未操作真实存档。生成截图/CSV归集在本目录，保留原有其他证据与用户改动。

未跑全项目测试。1000×为选定目标倍率，受每帧计算预算约束，按实际推进扣费。
