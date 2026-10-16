# 分阶段新手引导

## 授权与边界
- 用户确认核心系统首次可用时弹出、只跳过当前阶段、已有进度不补弹旧内容；不增加全局关闭、回放、奖励、自动操作或暂停模拟。
- 在当前脏工作树上精准追加，保留已有经济、UI、保存清理和文档修改。未创建临时工程、未操作真实存档、未安装依赖、未提交或推送。

## 实现
- `TutorialManager.Stages.cs` 在现有目标/剧情接口旁管理独立阶段，顺序为资源、建筑、人口、研究、生产链、时代、工坊、星区；用真实Manager可用条件触发，稳定目标ID定位。
- TutorialSaveData新增初始化标记、完成/跳过/历史基线、活跃阶段与目标、建造前数量。当前v1缺新字段的已有存档建立已解锁基线；新游戏清空。恢复时重新核对UI导航，不复用旧对象。
- `KingdomUIRoot.Onboarding.cs` 绑定Prefab遮罩、高亮、箭头与Next/Skip；定位真实控件，必要时框定滚动/研究目标，目标不可用或支付不足时撤下输入限制；实际页面/详情点击记录阶段进度。
- `OnboardingInputGate` 用Canvas射线过滤与EventSystem导航开关限制输入，不重写业务interactable；失活/销毁/等待/跳过时恢复。程序调用SetPage保留，实际玩家输入经UI过滤。
- `KingdomOnboardingAuthoring.Apply` 已在真实Unity执行，为Root Prefab补入固定层级；未修改其他Prefab布局、经济参数或稳定ID。既有.meta/GUID保留。

## 验证
- 本轮证据目录：`data/onboarding-20261016/`。首次编译发现重复LateUpdate，已修为接入既有LateUpdate；`compile-fixed.log`真实Unity编译通过，退出0。
- `authoring.log` 创作成功，Unity正常退出0。
- 最终 GPU PlayMode：`playmode-verified.xml`，19/19通过（13项本轮测试+6项目标面板/剧情/导航/滚动回归），Unity正常退出0。真实EventSystem射线与pointer事件覆盖导航、资源选择、建造支付、研究启动、人口/生产链/时代/工坊/星区说明、跳过、目标失活、裁剪恢复、支付不足、模拟继续及读档重定位。
- 额外嵌套控件回归：当前研究Prefab没有PinButton，测试从现有资源卡Prefab实例化真实Pin作为嵌套测试fixture；点击其区域只到达必需研究节点，置顶偏好保持不变并在finally恢复。正式资产未添加研究Pin。
- `editmode-verified.xml` 40/40通过（8项阶段测试+32项既有教程测试）；最终进程退出状态见证据目录`validation.md`。此前`editmode-final`与`playmode-fixed`虽生成全通过XML但关闭超时，不作为正常退出证据。
- 修复了实际建筑按钮定位、等待后研究重选、时代路径缺失`DataRows`，以及截图相机恢复后需等待布局稳定的问题。新增子控件过滤避免高亮节点内其他按钮继承点击权限，裁剪区检查避免越界点击。
- 已查看三张真实GPU参考截图：`onboarding-2640x1200.png`、`resource-select-2640x1200.png`、`building-build-2640x1200.png`；尺寸均2640×1200。金色框/箭头只指向当前目标，中文说明可读，提示面板不覆盖目标。未对所有阶段逐屏视觉验收。
- 最终PlayMode日志无项目LogError/LogWarning、缺失引用或C#编译错误；启动许可客户端出现token刷新超时，但Unity完成测试并正常退出0。Console核对依据本轮批处理日志，不声称人工Editor Console检查。
- 新增源码/测试静态检查通过，无反射。已有Root Prefab整体diff存在Unity序列化空字段的尾空格，未借本任务格式化用户既有资产修改。

## 收尾与证据边界
- 最终验证与字体恢复/fixture清理记录于`data/onboarding-20261016/validation.md`。
- 覆盖本主题定向自动测试与三处参考画面；未执行全仓测试或完整游戏发展流程。外部运行体验由用户自行验收。
- 未创建临时项目或worktree；不提交、不推送，保留当前工作树原有修改。
