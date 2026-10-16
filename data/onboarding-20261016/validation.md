# 分阶段新手引导：本轮验证

工作目录：D:/GitHub/Kingdom，2026-10-16。当前工作树含用户既有未提交修改；本轮未提交、推送或创建临时项目。

| 验证 | 证据 | 结果 |
| --- | --- | --- |
| 真实Unity编译 | compile-fixed.log；最终playmode-verified.log | 初始重复LateUpdate已修；后续测试程序集真实编译成功 |
| Prefab创作 | authoring.log | Apply成功，Unity正常退出0 |
| GPU PlayMode | playmode-verified.xml / .log | 19/19通过，Unity正常退出0 |
| EditMode | editmode-verified.xml / .log | 40/40通过；结果写出后关闭停滞，150秒超时结束本轮进程，不算正常退出 |
| 视觉核对 | 下列三张PNG | 实际GPU渲染2640×1200，人工查看；指定目标金框与箭头、中文提示可读、面板不盖目标 |

截图：onboarding-2640x1200.png（资源导航）、resource-select-2640x1200.png（原木条目）、building-build-2640x1200.png（建造按钮）。截图通过专用RenderTexture相机取得，不以Editor GameView分辨率冒充参考尺寸。

PlayMode包括13项新测试、6项既有目标面板/剧情/导航/滚动回归；通过真实EventSystem.RaycastAll与pointer事件执行。覆盖阶段触发后的目标定位、只放行目标、嵌套控件隔离、跳过、支付不足释放与恢复、目标丢失/裁剪恢复、存档恢复及模拟继续。嵌套研究Pin是从真实资源卡Prefab实例化的测试fixture，正式研究Prefab未增加Pin。

EditMode包括8项新阶段测试和32项既有教程测试：触发、阶段顺序、完成/跳过分离、后续阶段、旧v1初始化基线、新游戏重置、存读档恢复、支付等待与目标重新绑定。

日志核对：最终PlayMode没有项目LogError/LogWarning、C#编译错误或缺失引用异常；许可客户端启动时有token刷新超时消息，未阻止测试及正常退出。不声称人工查看Editor Console。历史失败尝试保留用于追溯，以本文件列出的最终结果为准。

静态检查：本轮新增源码和测试无反射，相关C# diff空白检查通过；Root Prefab整体diff含Unity序列化空字段尾空格，未顺手格式化既有资产改动。

收尾：确认无Unity进程后，将SIMSUN SDF.asset恢复为测试前字节，SHA256为B9BD028E9AFC401532E35CE5A76782C40C7426FCEF7B0B94E12E53085CCBA5DE；font-before-tests.tmp已删除并验证不存在，Temp/StagedOnboarding-*残留0。没有创建临时工程或worktree。

边界：完成本主题定向自动验证与三处参考画面核对；未执行全仓测试、所有阶段逐屏视觉验收或完整经济发展流程。外部运行体验由用户自行验收。
