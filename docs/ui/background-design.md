# 背景材质与边缘场景

2026-10-12 用户授权实施：统一环境背景，主要内容使用暗色石材，导航与状态栏使用旧木铜边；保留当前按钮、布局、剧情插画及预览遮罩。不自动commit或push。

三张素材由内置 image_gen 生成，原PNG未编辑，复制到 `Assets/Resources/Art/UI/Backgrounds/`。全屏图使用原始比例及EnvelopeParent覆盖窗口，边缘可裁切；石材与木材为9-slice，边框(64,40,64,40)，无mipmap。环境不接收射线，现有面板Surface也不接收射线，保留专门的滚动/研究图输入表面。

`KingdomBackgroundAuthoring.Author`负责导入与创作，固定EnvironmentBackground在Root下第一层；现有SafeAreaRoot/Background保留但透明。主内容/详情石材96%不透明，亮度0.68；导航/状态/通知木材95%不透明，亮度0.85。现有Button、Story、Review作者接入Apply，重复执行不新增层级。背景不随页面、时代或模拟状态变化。

Runtime只新增固定环境对象的旧UI隔离保留条件；现有详情兼容重建保留Prefab的Surface，运行时详情外壳透明，以免覆盖作者材质。所有其他旧详情对象仍禁用并销毁，滚动与输入归属不变。AspectRatioFitter必须先设EnvelopeParent再设置比例，避免None模式自动重算为1:1。

用户补充硬约束：保持原手机横屏比例2640×1200（11:5），不改变Canvas参考分辨率、Match Width、ProjectSettings或主场景。环境素材通过内置image_gen重新构图为11:5；生成原PNG1860×845，Sprite矩形居中取1859×845，排除仅1像素宽度误差，使Sprite比例精确11:5；不拉伸、不编辑PNG。原16:9素材与被替代回归截图已在2026-10-13获准清理。

当前资源/建筑等页面真实Unity三尺寸截图位于 `data/production-compact-20261013/`，2640×1200、2200×1000、1760×800均为原11:5比例，其他尺寸仅为测试渲染目标，不改产品布局。概览页原有长工具按钮文字溢出在旧按钮截图中已存在，本轮只修改背景，未扩展布局修复。

## 完整生成提示词

### KingdomEnvironment 手机比例修正版

内置image_gen编辑，以首版环境图作为参考。最终项目引用此修正版，首版提示词保留作来源记录。

Edit this architectural mouse kingdom background to match the game's ORIGINAL PHONE LANDSCAPE ASPECT RATIO 11:5 (2.2:1), equivalent to 2640 by 1200. Output a wider opaque horizontal canvas in precisely this aspect ratio. Preserve the hand-painted stone, timber beams, copper workshops and miniature town along far left/right and bottom edges, same dark gray-green/walnut/copper palette and quiet lighting. Recompose by extending the quiet central stone wall horizontally, moving the edge architecture toward the new outer edges; do not stretch existing stone or wood. Central 75 percent remains low contrast dark empty wall, no characters, text, symbols, interface or bright focal subjects. Keep all upper beam and bottom floor decoration visible within the wider phone canvas, with thin restrained perimeter details. Do not include letterbox bars or transparent margins.

### KingdomEnvironment

Production game UI environment background, wide horizontal 16:9 painterly mouse kingdom craftsmanship matching carved stone, aged wood and copper buttons. Dark desaturated gray-green stone architecture, old timber beams and restrained copper workshop details on far left and right edges and bottom perimeter. Central 75 percent is quiet nearly uniform dark charcoal green subtle wall texture with extremely low contrast, intended behind dense cream text and research graphs. Edge scenery evokes tiny medieval mouse town and workshop without characters or animals. No bright lights, no focal subject, no text, symbols, logos, interface panels or buttons. Orthographic frontal interior architectural backdrop, refined hand-painted illustration, restrained depth, no dramatic perspective. Full opaque image, no transparency.

### ContentSlate

Single opaque rectangular game UI panel texture, aspect ratio 2:1. Dark charcoal desaturated gray-green slate stone, very fine restrained hand-painted worn mineral surface. Center 90 percent quiet, nearly uniform, extremely low contrast, intended behind dense cream Chinese text and thin research graph lines. Very slender subtly carved aged stone perimeter border, no thick frame or corner ornament. Flat orthographic frontal surface, evenly lit, no shadow outside, no bright highlights, no letters symbols icons buttons scene objects. Pixel artwork suitable for Unity nine-slice panel, matching old timber copper and stone mouse kingdom UI. Fill canvas edge to edge, no transparency or outside margins.

### NavigationTimber

Single opaque rectangular Unity game UI panel texture, aspect ratio 2:1, edge-to-edge with no outside margins. Dark old desaturated walnut timber, very subtle fine horizontal wood grain, quiet low contrast charcoal-brown center 95 percent suitable behind cream Chinese text. Slender restrained aged copper perimeter seam, only tiny modest joins, no rivets inside content area, no bulky ornament. Hand-painted storybook mouse kingdom architectural craftsmanship matching slate stone and brass wood buttons. Orthographic flat frontal evenly lit, no glow highlights scene shadows letters symbols icons buttons or objects. Thin border suitable for nine-slice resizing, no transparency.

## 单层阅读背景（2026-10-12后续调整）

用户选择去掉内层黑框与大深色块。Content/Detail保持石材亮度与细边框，alpha从0.90改0.96；概览大卡、音乐外层和控制区、剧情摘要与总览外层透明，时代说明和外壳透明。详情Header背景透明。原11:5参考、控件尺寸、文字及按钮材质不改。

ResearchGraphViewport采用Prefab创作的Mask，showMaskGraphic=false；Image不透明白色用于stencil和射线，sprite清空，不绘制第二层黑底。禁止简单把此Image alpha改为0；保留原RectMask2D和专用拖动输入表面。

专用列表行配色改白色alpha0.05/0.09，源行Prefab默认0.05；音乐刷新也复用轻微行色，当前曲目保持铜色RGB且alpha0.14。保持可点击反馈和操作按钮状态。时代有操作卡与星区工程项复用轻微行底色；不修改全局Panel/PanelRaised，剧情插画阅读遮罩、纯预览和确认弹层均保留。作者入口同步这些定向配置。

单层布局沿用至今，当前截图入口为`data/production-compact-20261013/`。旧迭代截图已获准删除；历史验证事实见原按钮UI交接。

## 页面内容与边框打磨（2026-10-12）

用户授权补边框、移开音乐遮字图标、检查页面实际内容，追加顶部资源去掉纯红。

- Content/Detail Surface内固定四条2单位铜灰线，内缩2，颜色(0.30,0.22,0.13,0.72)，均不接收射线；底板仍是一层。
- 八格顶部资源改共用石材9-slice，灰绿(0.75,0.82,0.77,0.85)，原数值与危险文字语义保持。
- 音乐图标固定左上(28,-40)、48×48，曲名从104开始，间隔28，不靠旧中心绝对偏移；完整Track.Label绑定不变。
- 概览卡片用作者化VerticalLayoutGroup/ContentSizeFitter管理整体首选高度与间距，移除主卡重复高度归属，文字增长不重叠；两快捷按钮字号28、宽208/288、高56。
- 建筑名称/时代/数量/状态/操作各自列区间分离；长名称可换行/省略，完整内容保留详情。工坊使用新KingdomUIWorkshopCard固定模板，名称0～0.43，状态0.45～0.76，购买0.82～0.98，避免英语长名盖住状态。
- 普通研究线提升为铜灰(0.46,0.43,0.35)，选中仍白，原线宽/拓扑不变。

当前截图入口为`data/production-compact-20261013/`；工坊fixture通过公开Editor API开放实际升级项，避免空页截图。只有隔离测试状态改变，真实存档不动。旧迭代截图已获准删除。
