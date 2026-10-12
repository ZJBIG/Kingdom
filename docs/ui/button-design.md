# 游戏按钮材质与交互策划

2026-10-12 用户授权：所有游戏按钮重新设计，先策划再实施。允许不同用途使用不同材质。只改变呈现，不改变费用、操作条件、剧情、模拟、存档或页面职责。

## 视觉分工

| 用途 | 材质与层级 | 保留的状态信息 |
|---|---|---|
| 页面导航、主要跳转 | 细纹石板、浅色铭刻 | 当前页暖铜色，禁止重复跳转，但不显示成普通禁用灰 |
| 建造、购买、维修、推进、确认 | 木质铜边 | 可执行操作的暖色强调与支付/可用条件 |
| 数量、筛选、定位、展开、复位、返回 | 简洁金属 | 悬停、按下、键盘选择、数量选中 |
| 拆除、拆解等危险操作 | 暗红金属 | 原有危险语义色与确认流程；退出确认仍是普通辅助操作 |
| 剧情插画操作 | 深绿纹理与铜框 | 已完成插画样式、展开条件与预览关闭 |
| 资源/建筑等整行点击、音乐只读行底板 | 细边金属，不使用厚操作边框 | 原有明暗交替、选中项、数据列和点击权限 |
| 研究树与研究队列节点 | 精细金属底板，保留时代/进度层 | 205×50 尺寸、连线、状态；仅正在研究目标可用金色外框 |
| 音乐控制 | 金属底板与独立原图标 | 播放/暂停/上一首/下一首图标和播放状态 |
| 文字链接与复选框 | 明确的下划线链接、轻金属勾选框 | 可点击反馈与原勾选状态 |

按钮材质按用途分配，不随时代动态换皮。复用三张中性材质纹理，通过既有语义色表达普通、主操作和危险状态。普通操作使用浅色文字；小按钮优先清晰、克制，长标签保持合适内边距，不缩字硬塞。整行卡片和密集研究节点使用更细的边框。

## 状态与布局

- 常态保持清楚的文字与材质对比；悬停适度提亮，按下变暗，键盘选择有可见反馈；禁用变灰。当前页面/当前数量与不可执行操作分别呈现。
- 保持已有按钮高度和页面布局分工；数量标签居中，普通操作文字左右留边，音乐图标独立于底板。
- 不给透明关闭遮罩或背景手势表面添加可见装饰。详情主按钮保留左右48内边距，研究金框只表示真实研究中状态。
- 素材用9-slice，保留边角、不拉伸纹理；固定层级在Prefab中创作，运行时只绑定数据、状态和回调。

## 实施顺序与验收

1. 生成石板、木质、金属三套无文字透明底图，保留原PNG，通过Sprite矩形排除透明外边距；剧情复用现有素材。
2. 作者迁移全部活跃UI Prefab，覆盖当前60个源Button组件及真实Toggle；新增公共操作按钮Prefab，覆盖时代条件和星区建筑操作的动态入口。
3. 音乐背景/图标分离，修正运行时覆盖；维护Story、Relic、Review三个既有作者入口，重新生成不会退回纯色按钮。
4. EditMode验证素材、源Button覆盖、图标分离和透明遮罩；PlayMode验证刷新后样式、导航与数量状态、实际点击/滚动以及研究金框原有语义。
5. 用真实Unity Canvas渲染概览、建筑、研究、音乐、剧情页面，检查长标签、密集按钮、可用/禁用对比。相关回归非零通过，Console无新增错误后停止扩展。

不安装依赖、不新增经济或操作逻辑、不恢复旧UI、不为每个按钮添加Update。当前工作树已有 `docs/story/story-button-art-generation.md` 删除，保留，不重建该文件。此任务不自动提交或推送。


## 素材生成记录

三张无文字透明PNG由内置 image_gen 工具生成。最终文件位于 `Assets/Resources/Art/UI/Buttons/`；只调整Unity Sprite导入矩形，原PNG不编辑。

### NavigationStone

Single finished horizontal rectangular Unity game UI button background sprite. Light neutral gray carved limestone tablet, very subtle fine stone grain, thin incised antique border, softly beveled corners, restrained hand-painted mouse-civilization storybook craftsmanship. Neutral material colors; no colored glow. Center smooth quiet medium-light gray so runtime dark tint and cream Chinese labels remain legible. One button, ratio 3:1, almost fills canvas, tiny transparent outer margins. Transparent background, no scene no external shadow no letters numbers icons symbols logo. Narrow edge ornament suitable for 9-slice resizing, straight-on orthographic.

### ActionWood

Single finished horizontal rectangular Unity game UI button background sprite. Neutral light desaturated oak wood with fine understated grain, slender aged brass/copper edge and modest forged corner details, tactile hand-painted fantasy craftsmanship matching mouse-civilization illustrations. Center quiet pale gray-beige wood, suitable for runtime tint and cream text; no decorative center motifs. One wide rectangular button ratio 3:1 almost fills canvas, tiny transparent outside margins, no external cast shadow. Actual transparent background, no scene no letters numbers icons logo. Narrow edge decoration for 9-slice resizing, straight-on orthographic.

### ControlMetal

Single finished horizontal rectangular Unity game UI control background sprite. Brushed neutral light pewter/steel face with very subtle grain, thin darker gunmetal rim, tiny restrained rivets at outer corners, refined painterly industrial game UI. Smooth unobtrusive light gray center for runtime recoloring dark teal or dark red and cream text, no glow no flashy ornament. One wide rectangle ratio 3:1, softly beveled corners, button almost fills canvas, tiny transparent outside margins, no external cast shadow. Actual transparent background, no scene no text letters numbers icons logo. Frame only narrow perimeter for 9-slice stretching, straight-on orthographic.
