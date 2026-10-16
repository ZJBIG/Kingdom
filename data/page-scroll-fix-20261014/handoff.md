# 页面外侧滑条与空白滚动修复（2026-10-14）

续接 `.codex/handoffs/2026-10-12-button-redesign-handoff.md` 的滚动边界主题；当前权限下 .codex 不可写，补充交接保存在本目录。保留全部既有工作树修改。

- `KingdomUIRoot.prefab` 与 `KingdomPageBoundaryAuthoring`：PageVerticalScrollbar 从 PageHost 移到 Content 同级，保持14宽，位于视口右侧12间隔；上下分别比视口内缩6，AutoHide。作者重复执行复用已有对象。
- `KingdomUIRoot.cs`：普通DataRows取消旧标题的-82偏移；外页高度由内容决定，去掉1400下限和180额外空白。Research/Music仍按视口测量尺寸，独立滚动不变。
- `KingdomUIRoot.Sectors.cs`：展开/折叠重排也使用实际行高，避免再次写回1400下限。
- `KingdomUIRoot.SceneLayout.cs`：沿用布局重建流程，在应用缓存normalized位置前明确回到顶部原点，避免短页面保留旧位移。
- 现有边界测试更新为滑条在视口外侧，并补首次顶部对齐、短页滚轮不移动和长/短页面覆盖断言。

验证：
- `tools/codex/build-developer-assembly.ps1 -Assembly Editor` 成功，日志 editor-build.log；3处既有spritesheet弃用警告。
- `tools/codex/compile-developer-tests.ps1 -Target All` 成功，Editor/PlayMode compiler_exit=0，日志 test-compile.log。只编译，没有执行测试。
- 静态Prefab块检查通过：父子引用一致，位置/尺寸与作者一致，滑条与视口水平间隔12，上下内缩6。
- 未执行真实 Unity 编译。此前本会话Unity启动未成功，没有本次PlayMode运行XML或新截图，不以历史通过记录替代。

未创建临时项目，无存档/经济改动，无commit/push。下一步可在Unity可启动时运行KingdomPageBoundaryTests及KingdomPageBoundaryPlayModeTests验证实际布局和滚动。

## 后续：边框漏字与初始化异常

- 用户报告 Scrollbar.Rebuild → SetVerticalNormalizedPosition 的 UnassignedReferenceException。检查确认 PageHost Prefab m_Content=0，但滑条已启用，Editor Prelayout可在Runtime绑定前调用它。
- Prefab及KingdomPageBoundaryAuthoring为共享ScrollRect预绑定Overview；TryBindAuthoredShell也立即绑定，ConfigureOuterPageScroll先设content再启用滑条。
- PageHost原mask为0内缩，边框位于1内缩且宽2，存在框外仍渲染的窄条；mask改为四边3内缩，与框内沿一致。四个装饰边框Image maskable=false，保持自身可见；其他独立viewport不变。
- KingdomPageBoundaryTests新增初始化前Scrollbar.Rebuild无异常及裁切在边框内沿的回归用例。
- 本轮mask-editor-build.log、mask-test-compile.log均编译成功；静态验证默认content、mask padding及四边Image配置通过；git diff --check通过。
- 检测到用户Unity Editor运行，不终止、不对同一工程另启实例。未执行真实 Unity 编译。未执行新增Unity测试或截图验证。
