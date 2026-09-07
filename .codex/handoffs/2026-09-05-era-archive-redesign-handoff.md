# 时代档案页重构交接报告

更新时间：2026-09-05

## 本轮变更

- 将 Era 页面从 TextRow 列表重构为 Overview/Story 风格的运行时卡片树。
- 新增时代档案头部、文明进程时间线、跃迁仪表盘、研究前置、资源储备和当前首要阻碍卡片。
- 资源卡读取 `EraGoalEvaluator` 的库存、需求、已支付、缺口、净产出并显示 ETA/无法估算原因。
- 移除 Era 页的 Overview 式发展准备度、生产链和软建议内容。
- 更新 Era PlayMode 契约测试以验证卡片结构、独立分组、操作按钮和旧软建议缺失。

## 验证

- 静态内容闭包检查通过：Industrial closure complete；Industrial/Spacer/Ultra 可达性无回归。
- `dotnet build Kingdom.Runtime.Developer.csproj --no-restore` 通过，0 警告、0 错误。
- `dotnet build Kingdom.Editor.Developer.csproj --no-restore` 通过，0 警告、0 错误。
- `git diff --check` 通过（仅有现有换行格式提示）。

## 未完成验证与风险

- 未执行真实 Unity 编译、PlayMode 和 Console 检查。
- 需要在 Unity 中确认卡片高度、长文本截断、滚动范围和 Huawei P40 Pro 横屏布局。
- 需要确认 Era 卡片刷新时销毁/重建 DataRows 子节点不会影响共享 ScrollRect。

## 下一步

在 Unity Editor 中刷新并运行 `EraPage_RendersCurrentNextEraProgressAndGoal` 及相关 onboarding/UI 测试；检查卡片层级、Action 按钮点击后的 Research/Resources 详情绑定和终末时代显示。

## 2026-09-05 后续收敛

- 移除完整文明时代时间线，仅保留当前时代与下一跃迁所需内容。
- 资源条件详情不再切换到 Resources 页面，改为直接调用 `ShowResourceDetails` 更新右侧详情面板。
- 跃迁研究卡增加 `加入队列` 按钮，与 `查看详情` 并列；研究状态为 Completed 时隐藏该按钮。
- 更新 PlayMode 契约以验证不再显示完整时代时间线，并验证未完成跃迁研究存在队列按钮。
- Runtime/Editor 开发程序集重新编译通过，静态闭包检查通过。

## 2026-09-05 内容增补与颜色统一

- 当前时代卡新增基于已完成研究效果的“本时代能力”摘要，最多 3 条。
- 下一时代新增“进入后变化”摘要，最多 2 项研究与 2 项建筑。
- 首要阻碍增加研究前置/资源缺口/无生产来源/净产出不足分类。
- 首要阻碍卡背景改为 `Panel`/`PanelRaised`，不再使用 Copper/Positive 整块高亮。
- 更新 PlayMode 契约以检查新增内容和阻碍分类。
- Runtime/Editor 编译与静态闭包检查再次通过。

## 2026-09-05 整体区合并与浅深交替

- Era 页面进一步合并为三个整体区：`EraCurrentArchive`、`EraTransitionSection`、`EraRequirementSection`。
- 当前时代区按身份/定义/能力分区；跃迁区按目标/状态/进入后变化分区；条件区将研究与资源统计、项目及首要阻碍纳入同一容器。
- 子区背景固定按 `Panel`/`PanelRaised` 交替，状态仅通过文字颜色、标记和按钮表达；首要阻碍背景不再使用 `Copper` 或 `Positive`。
- 资源条件详情继续只调用 `ShowResourceDetails`，不切换页面；跃迁研究保留详情与加入队列按钮，完成后隐藏队列按钮。
- PlayMode 契约已更新为验证三大整体区、资源详情不离开 Era、条件项目颜色交替及新的“下一时代跃迁”标题。

## 当前验证

- `content-closure-check.ps1`：通过，Industrial/Spacer/Ultra 可达性无回归。
- `dotnet build Kingdom.Runtime.Developer.csproj --no-restore --nologo --verbosity quiet`：0 warnings / 0 errors。
- `dotnet build Kingdom.Editor.Developer.csproj --no-restore --nologo --verbosity quiet`：0 warnings / 0 errors。
- `git diff --check`：通过（仅 CRLF 提示）。
- 未执行真实 Unity 编译、EditMode/PlayMode、Console 与 Huawei P40 Pro 实机布局验证。

## 2026-09-05 文本溢出修正

- 修正 `CreateEraCard`：带详情/队列按钮的卡片正文现在使用独立的剩余列宽（扣除右侧 560px 操作区），不再占满整张卡片。
- 长跃迁描述会在按钮左侧区域自动换行，避免文字绘制到按钮下方或越出卡片边界。
- Runtime/Editor 开发程序集重新编译通过，0 warnings / 0 errors；仍未执行真实 Unity 编译和运行时布局验证。

## 2026-09-05 卡片内边距调整

- 带操作按钮的 Era 卡片正文列宽由扣除 560px 调整为扣除 540px，在保持按钮避让的同时放宽文字区域。
- `CreateEraCard` 使用统一 20px 上下内边距和 14px 标题/正文间距，确保外圈矩形内部上下留白对称。
