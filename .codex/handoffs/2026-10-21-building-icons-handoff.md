# Kingdom 建筑图标交接

## 2026-10-22 清理后当前证据入口

用户随后明确授权代理执行PS1。已删除旧768阶段data/art-optimization-20261022、工坊旧编译三文件与512前回退ZIP；当前美术证据统一读取data/android-size-20261022/（imports.json、import.log、unity-exit.txt、compressed-preview.png等），当前92图来源/绑定为data/workshop-art-20261022/manifest.json及batch提示词。旧段落已删除路径仅为历史记录。AndroidArtworkOptimization长期维护工具与全部正式图片保留。清理执行与1536文件/约398.43 MiB统计见repo-cleanup-handoff最新节；本轮清理未重新执行Unity或测试。

## 2026-10-22 当前：Android包体优化已应用

- 用户批准按512与ASTC方案继续；废旧文件清理仅提供PS1由用户执行。本轮没有执行清理脚本或删除旧文件。
- 79建筑/92工坊/10星区共181主图整张768→512，Lanczos、不裁剪，PNG源209.75→98.13 MiB。剧情保留1280×720，金属图集保留1024×512。全部Assets PNG源现在162.32 MiB（不是APK大小）。工坊manifest SHA256/尺寸与原优化清单同步为当前源图。
- Assets/Editor/AndroidArtworkOptimization.cs为可复用Editor政策入口，Menu Tools/Kingdom/Optimize Android Artwork Imports；Android主图ASTC6×6/max512并保留MipMap（卡片/生产树缩放/大图共用）；18剧情ASTC6×6、无MipMap；金属图集及10张固定背景/时代卡ASTC4×4、无MipMap。细线按钮/Pin等不扩大处理。共210纹理。Android override/HQ编码100，不使用不支持ASTC的Crunch；桌面默认压缩设置保持。
- PMusicAudioImporter仅增加Android Vorbis quality0.5，默认平台仍0.6；56曲目保留Streaming/立体声/采样率/本地Addressables。AddressableAssetSettings.m_BuildAddressablesWithPlayerBuild从PreferencesValue(0)改为BuildWithPlayer(1)，避免下次构建使用旧音乐包。KingdomBuild增加ASTC子目标和CompressWithLz4HC，不改架构/玩法/存档或删音乐。
- 真Unity2022.3.62f3c1：-batchmode -projectPath D:/GitHub/Kingdom -buildTarget Android -executeMethod AndroidArtworkOptimization.ApplyFromCommandLine -logFile data/android-size-20261022/import.log。自有进程27852自然结束，unity-exit.txt记录0。imports.json记录210图实际Android格式（199 ASTC_RGB_6x6、11 ASTC_4x4），79建筑/92工坊/33资源/10星区Sprite及图集边界、56音频override全部通过。compressed-preview.png已目视检查真实GPU渲染的270与136像素四样图，未见明显压缩块；不是完整页面交互验收。
- 按ASTC块与Mip层级计算的210纹理payload共49.91 MiB，不含包格式开销/其余纹理/音乐/引擎，不是APK实测。未运行测试套件、未生成APK或Addressables bundle，不声称最终安装包大小或音乐节省比例。
- 静态复核：181图512完整；92工坊SHA256正确；333图片/音频GUID与spriteID/internalID不变；266变动meta只涉及授权压缩/Mip/Android音频override。Unity另新增92份disabled Standalone块，与原Default完全等值，不改变桌面行为。diff检查通过。
- 本轮前512回退素材及meta在.codex/tmp/android-size-before512.zip；已加入tools/codex/cleanup-art-optimization-temp.ps1精确列表，由用户自行执行。没有新建临时Unity项目，未commit/push，未操作真实存档。当前Editor活动目标为Android。

## 任务与状态

用户要求为所有建筑制作并接入图标，图像需结合建筑 `Label` 与完整 `Description`；建筑需成为画面绝对主体（约占主题 80% 以上），背景简洁且符合环境，不出现人类、鼠族、动物或其他角色。当前图标与 UI 接入已完成，静态检查通过。

## 起始状态与保留工作

- 仓库：`D:\GitHub\Kingdom`；检查时 HEAD 为 `e899f2f82943235b43c8bd33bafdbcaa5ff949e1`。
- 工作树有大量其他游戏逻辑、数据、测试和交接修改。建筑图标任务仅涉及 `Assets/Resources/Art/Building/`、79 个建筑定义中的图标引用、9 个建筑卡 Prefab，以及本交接文件；不要重置或覆盖其余修改。
- 图标文件曾由并行图像工作生成和复核；本轮只做资产、引用、Prefab 静态核对，并清理定义中图标字段后的多余空行。

## 文件与行为变更

- `Assets/Resources/Art/Building/`：为 Animal 9、Industrial 26、Medieval 5、Spacer 26、StoneAge 10、Ultra 3 共 79 个建筑提供 PNG 和 Unity Sprite `.meta`。另有未引用旧图 `StoneAge/ClayPit.png`，本轮保留。
- `Assets/Resources/Datas/Building/`：79 个建筑定义均引用对应 PNG Sprite；没有变更建筑 Label、Description、ID 或定义 GUID。修整 6 个新增定义中 `icon` 字段后的空白行。
- `Assets/Resources/UI/Kingdom/KingdomUIBuildingCard.prefab`、`KingdomUISectorBuildingCard.prefab` 和 `EraCards/*/KingdomUIBuildingCard.prefab`：共 9 个建筑卡 Prefab 含 `BuildingIcon`，Image 保持比例且不接收射线；图标由既有建筑定义绑定逻辑填充。PinButton 保留在各卡片布局内的现有独立位置。

## 验证执行

- 通过本地 Python 静态检查：79/79 定义包含 Sprite 引用；79 个引用 GUID 均在建筑图标 `.meta` 中恰好命中一次；无重复 GUID、无损坏引用。
- PNG 目录共 80 张：79 张与定义按时代目录及文件名一一对应；唯一未引用图片为既有 `StoneAge/ClayPit.png`。所有图像都存在配套 `.meta`；另有目录 `.meta`，检查过的 GUID 全部唯一。
- 静态检查 9 个建筑卡 Prefab：均包含直接挂在卡片根节点下的 `BuildingIcon`，Image 的 `m_PreserveAspect=1`、`m_RaycastTarget=0`；PinButton 仍存在。
- 生成了临时缩略图总览并目视复核 80 张 PNG：没有看到角色或生物；建筑/设施为视觉焦点，时代与场景环境按定义区分。总览文件位于 `C:\Users\19603\AppData\Local\Temp\kingdom-building-icons-review.jpg`，不属于仓库交付资产。
- `git diff --check` 对相关定义与 Prefab 未发现空白错误；Git 输出既有 LF/CRLF 自动转换提示。
- 未运行测试或真实 Unity 编译；**未执行真实 Unity 编译。**

## 风险与证据限制

- 缩略图总览能确认整体构图和无角色/生物，但不能代替 79 张原图逐张的高分辨率细节审查。并行图像工作已分别报告地面、工业、Medieval 和太空组逐张检查；本轮复核了总览及少量原图。
- Unity Sprite 导入和实际卡片运行显示未在 Unity Editor 中验证。
- 多个时代卡 Prefab 以及一张未引用旧 PNG 属于既有/并行工作树；保持现状，除非另有明确任务。

## 下一具体行动

图标交付已完成。若继续验证，先在隔离的 Unity Editor 中检查 Prefab 导入与 BuildingDef.Icon 运行绑定，再决定是否运行相关 UI PlayMode；目前不需要更改资产或删除未引用旧图。

## 2026-10-21 追加：星区环境图标与资源树接入

用户追加要求：为星区制作带环境的星球图标，并将建筑 Sprite 接入资源生产树。此追加已完成。

### 追加文件与行为

- `Assets/Resources/Art/Sector/` 与目录 `.meta`：新增 10 张 1254×1254 PNG 和配套单 Sprite 导入 `.meta`，对应 AlphaCentauri、AzurePool、DawnRing、HeliosCore、ProximaB、ShardCrown、SiriusResourceBelt、TauCetiFoundry、Terminus、ThunderGate。每次生成输入均包含定义的 Label 和完整 Description；行星、环带、碎裂天体或恒星近冠区依据各定义绘制，保持天体/环境主体突出，无人物、鼠族或其他生物。
- `Assets/Resources/Datas/Sector/*.asset`：10 个 `icon` 字段从 `{fileID: 0}` 改为对应新 Sprite 的 `{fileID: 21300000, guid: ..., type: 3}`，保持定义 `.meta` GUID、稳定 ID 和其他定义字段。
- `KingdomUIRoot.Production.cs`：共用 `CreateProductionNode` 的图标绑定从仅 `node.Resource?.Sprite` 改为资源 `ResourceDef.Sprite`、建筑 `BuildingDef.Icon`、星区 `SectorDef.Icon`，覆盖建筑根、产出来源、升级建筑和星区来源节点。
- `KingdomUIRoot.Sectors.cs`：绑定列表中的 `SectorIcon` 到 `SectorDef.Icon`，缺图时隐藏 Image。
- `KingdomUISectorRow.prefab`：新增卡片根直接子级 `SectorIcon`，72×72、白色、保持比例、不接收射线；名称与摘要的左边界从 74 移至 154 为图标留位。保留原 pin 和建筑展开按钮。
- `docs/ui/page-responsibilities.md`：按本轮要求更新“建筑无需图标”的旧说明，明确生产树显示资源、建筑及星区图标。
- 上述 C# 文件和 Prefab 原有较大定义类型迁移、布局等并行修改均保留；本轮仅拥有图标绑定和新增图标布局的增量。

### 追加检查与限制

- 10 张 PNG 均可读取，尺寸均为 1254×1254；10 个 Sprite GUID 唯一，单 Sprite 导入配置正确；10/10 星区定义引用按名称匹配对应图像。
- 逐张目视查看生成输出：天体或对应星区环境为主体，没有角色或生物，冰海、近冠日焰、荒原、碎冠带与气态巨行星等环境可区分。
- 星区 Prefab 的 62 个序列化对象 ID 唯一，内部 fileID 均可解析，`SectorIcon` 的 72×72、保持比例及关闭射线设置均核对通过。
- 既有生产树节点 `Icon` 为白色、保持比例、关闭射线；绑定使用同一个 `CreateProductionNode`，既有展开、详情按钮与拖动转发代码保留。
- 相关改动 `git -c core.safecrlf=false diff --check` 通过。
- 总览：`C:\Users\19603\.codex\visualizations\2026\10\20\01a15972-945e-71c0-9622-d6a6853de4a1\sector-icons-overview.jpg`。
- 未启动 Unity 或执行测试，实际运行显示未验证。**未执行真实 Unity 编译。**

## 2026-10-21 追加：星区生产树与建筑链显示

### 用户要求与实现范围

- 星区接入生产树，复用资源/建筑的独立 Production 页面；正确处理解锁、当地建筑及图标可读性。
- 用户要求资源生产来源复用已有建筑链“存高排低”逻辑。本轮直接绑定 `BuildingManager.ShouldDisplay`：保留有存量的旧级，空旧级让位给最高已解锁级，来源按时代降序，同一时代保留定义顺序。未修改经济、Manager 规则、定义 ID/GUID 或存档格式。
- `ResourceProductionTree.cs`：增加星区根、SectorFacility/SectorOutput 关系，按所属星区列当地设施，持续产出/一次性奖励分开。资源路径内的星区来源仍为叶节点。新增可选 producerVisible 回调仅过滤生产来源与当地设施，不截断 Upgrade 节点的已解锁路径。
- `KingdomUIRoot.Production.cs`：星区根入口/标题/状态/图标绑定；每 0.5 秒重新检查已缓存节点，数量变化也触发显示更新，拖动时延后，保留展开状态、缩放与根视觉锚点。
- `KingdomUIRoot.ResourceTree.cs`：已解锁或已占领星区详情显示“查看星区生产树”；读取实际 SectorState，避免缓存尚未刷新的入口缺失。
- `KingdomUIRoot.Onboarding.cs`：教程创建生产树时同样绑定 ShouldDisplayBuilding。
- `KingdomUIResourceTreeRow.prefab`：节点 248×88 → 320×104，图标 32×32 → 56×56；标题文字左边界 80、右边界 40，字号 20–24 自动适配并允许两行，底部关系提示独立排布。保持图片比例，不接收射线。
- `docs/ui/page-responsibilities.md`：记录星区入口、当地设施前置、建筑链过滤与排序规则。
- `ResourceProductionTreeTests.cs`：增加4个模型测试，覆盖星区归属/解锁缓存、奖励与速率区分、来源过滤不截断升级、时代稳定降序。保留既有定义类型迁移改动。
- `ResourceProductionTreePlayModeTests.cs`：真实场景测试加入星区占领/内部探索、Sprite 绑定、图文间距、低级存量保留，以及两种分辨率名称不溢出断言；沿用 Temp 下独立存档，先销毁场景对象再清除存档重定向。截图前在测试存档跳过引导，刷新引导本身，不调用整个 UI 刷新。修正原轮询在找到节点后仍跨帧持有可被重建 Transform 的脆弱引用。

### 验证入口与过程

- 当前输入：HEAD `e899f2f82943235b43c8bd33bafdbcaa5ff949e1` 加已有大量未提交改动及本轮增量；不代表干净 HEAD 验证。
- 真实 Unity：`D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe`，本工程直接运行，未创建副本，未操作用户存档或用户 Editor。
- EditMode：`tools/codex/run-unity-tests.ps1 -Platform EditMode -TestFilter 'ResourceProductionTreeTests;ResourceProductionTreeLayoutTests'`，输出 `data/sector-production-20261021/editmode.xml` 和 `.log`。17/17通过，0失败/跳过，真实脚本编译成功；Unity在关闭阶段停滞，超过一分钟后结束本任务启动的进程，wrapper退出1 / Unity退出-1，不能记为正常退出。
- PlayMode直接CLI：`-batchmode -projectPath D:\GitHub\Kingdom -runTests -testPlatform PlayMode -testFilter ResourceProductionTreePlayModeTests -testResults .../playmode.xml -logFile .../playmode.log`，不传 `-nographics` 或 `-quit`，真实图形设备渲染2640×1200及1760×800。
- 首次PlayMode启动遇到上述退出进程保留的工程锁，未执行测试，记录于 blocked-start。第一次实际运行1/2通过：测试误把戴森球归属设为DawnRing，核对资产后改为HeliosCore。第二次2/2通过且退出0，但新手引导遮罩遮暗截图。第三次截图清理误用全UI刷新，暴露旧测试跨帧持有被销毁节点的问题；仅调整测试轮询与截图前引导刷新，没有修改产品逻辑。所有尝试证据保留，最终结果以后续追加为准。

### 最终结果与收尾

- 最终 PlayMode：`data/sector-production-20261021/playmode.xml`，2/2通过，0失败/跳过；`playmode-exit.txt` 记录 `UnityExitCode=0`，测试进程正常结束。新星区用例与原有生产页交互用例均实际执行，未替换/跳过既有断言。
- 第四次中间运行发现过早跳过原交互用例的引导会让后续新解锁引导主动重建图，干扰“仅查看详情不重建”的断言。最终仅对新增星区用例的截图使用可选引导跳过，原交互用例保留原引导流程；产品教程逻辑未改。
- 最终截图目录 `data/sector-production-20261021/pages/`：9个画面×2640×1200/1760×800，共18张；包含星区当地建筑、长名工程持续投入、保留存量旧级的资源来源、星区列表，以及既有生产页展开/缩放截图。目视查看了最终星区根和建筑链来源，图标比例正确、名称/状态/展开按钮无重叠；真实TMP断言检查截图内节点名称无文字溢出。
- 实际图形日志记录正viewport、可见节点数、图内容尺寸、双轴溢出；最终日志无 C# 编译错误、NullReferenceException 或 MissingReferenceException。Unity许可客户端启动时报令牌更新失败后成功解析授权，不影响本轮测试执行与正常退出。
- 截图前的去遮罩只用于隔离测试存档；没有操作用户真实存档。未运行全仓库测试，也未进行外部设备体验验收。
- 测试前保存3个SDF字体资产的工作树版本，退出后仅SIMSUN动态字体资产发生变化，已恢复并核对与运行前副本一致；3个临时副本及备份目录均已移除。没有创建临时Unity工程，没有遗留Unity进程。
- 中间尝试的XML/log/截图已移至 `.codex/archive/2026-10-21-sector-production-attempts/`，活动证据目录只保留本轮EditMode与最终PlayMode结果。未提交/推送。
- 下一动作：本需求实现与针对性验证已完成，不需继续改经济、定义或教程。

## 2026-10-21 追加：图片装裱、点击预览与缩小采样

### 本轮需求与实现

- 用户反馈建筑/星区图片直接贴在UI上突兀，要求加外框并允许点击预览；同时反馈缩小后观感差，建议降低分辨率。
- 实测原图均1254×1254，原建筑卡48、树节点56、星区行72像素；全部未启用Mipmap，26张工业图还与其他图使用了不同的默认有损压缩设置。仅降低分辨率不能替代缩小时的层级采样。
- 精确按定义icon GUID筛选79建筑+10星区的89张实际引用图片，保持源PNG、GUID及未引用旧图不动。其`.png.meta`：enableMipMap 0→1、filterMode Bilinear→Trilinear、顶层及默认平台maxTextureSize 2048→1024各89处；默认textureCompression 1→0共26处，其余原本无压缩，未启用override的非默认平台块不改。1024用于约600–1024像素的预览；因关闭了工业图原有压缩，不将本轮宣称为总纹理内存优化。
- 11个列表/节点Prefab：原图对象保留直接路径BuildingIcon/SectorIcon/Icon，增加预览Button和非射线ArtworkFrame。边框复用ControlMetal的实际9-slice Sprite子资产（fileID -5939887275736818340），旧铜色(.9,.78,.6)，外扩4px、FillCenter=false、PPU multiplier48。资源节点关闭装饰与Button组件，避免disabled Button tint影响资源图标。
- 普通8建筑卡：图标48→64，左中(16,10)；Pin48→24，左中(36,-48)；Label左边界74→100，右边界/字号保留。136px紧凑行高下，图片含框与Pin均在卡内、相距10px。SectorBuildingCard：Icon64左上(16,-48)，Name左边界100，Pin24左上(36,-106)，其480px卡片高度及其他控件不变。
- `KingdomUIRoot.Artwork.cs`及.meta：共用绑定图片、边框、预览按钮，复用已有StoryIllustrationPreview（滚轮/双指缩放、拖动、复位、关闭/遮罩/Esc），不另造预览界面。普通建筑、星区列表、星区设施卡和生产树均接入；图中图片使用已有拖动转发器，名称仍独立打开详情。新增脚本GUID为e89230b44c054381a0590e15d8616c7e。
- `KingdomUIRoot.prefab`仅现有StoryIllustrationPreview子树新增顶部96px近不透明HeaderBackdrop，使标题不再叠在游戏HUD上；实际ImageRect增加随图片缩放的空心金属框。运行时不创建固定UI布局。
- `docs/ui/page-responsibilities.md`同步点击预览/采样契约。
- 只读/互斥委派分别负责导入meta、11个图标Prefab、root预览子树；主代理整合绑定并检查真实截图。未改经济、真实存档、ID或既有GUID。

### 验证与截图过程

- 本轮仍使用当前脏工作树，保留原定义迁移等并行改动；主工程无用户Editor占用时串行运行Unity，不创建临时工程。
- 真实GPU PlayMode沿用`ResourceProductionTreePlayModeTests`的隔离存档fixture；新增校验实际纹理宽度≤1024、mipmapCount>1、Trilinear、图框可见、预览Sprite绑定、缩放/拖动/复位、关闭后保留图根、列表入口、紧凑卡图片边框在卡片实际矩形内。保持旧解锁/来源/升级/拖动断言。
- 第一轮2/2通过、Unity退出0。截图发现原建筑小图靠上漂浮，调整为上述64px及Pin/Label布局；第二轮2/2通过、退出0，但目视发现预览框错误引用主Sprite fileID 21300000，导致空白Image遮住原图。已改为实际9-slice子资产fileID，并加sprite非空、sprite.border非零、FillCenter=false回归断言。第三轮补查资源Button disabled tint后2/2通过、退出0；最终结果随后追加。
- 全部截图写入新目录`data/artwork-framing-20261021/pages/`，不覆盖上轮`sector-production-20261021`证据。截图包括建筑列表、星区列表/树、资源来源、建筑/星区大图预览，分辨率2640×1200和1760×800。

### 本轮最终结果

- 最终真实Unity编译及GPU PlayMode：`data/artwork-framing-20261021/playmode.xml` 2/2通过、0失败/跳过；`playmode-exit.txt` 为UnityExitCode=0，正常退出。未重新运行EditMode或全仓库测试。
- `pages/`最终24张截图（12画面×两种分辨率）。目视查看最终建筑大图、星区大图、星区列表，以及布局调整后的建筑列表和星区树：预览原图正常显示，边框不遮图，独立标题底栏遮住底层HUD文字，列表图片/Pin不再越界。新增回归同时验证预览框Sprite实际存在且有slice border，避免静态GUID存在却fileID错误时产生白色遮挡。
- 本轮相关diff --check通过；最终日志无error CS、NullReferenceException、MissingReferenceException。启动许可客户端令牌更新警告之后成功授权并执行测试，不影响最终退出0。
- 已恢复本轮运行前SIMSUN动态字体资产；其余两SDF资产与备份一致，3个备份文件和备份目录已移除。不存在本任务遗留Unity进程或临时Unity工程。
- 3轮中间截图/log/XML已可恢复归档至`.codex/archive/2026-10-21-artwork-framing-attempts/`；最终证据保留在活动目录。未提交或推送。
- 本轮需求已完成。源1254PNG保留；1024是Unity运行纹理上限，不是覆盖/重采样原始素材。

## 2026-10-21 追加：参考策略游戏重做图片容器，同类卡片同步放大

### 用户修正与参考

- 用户否定上一轮铜色按钮纹理边框，允许参考其他游戏/GitHub；随后允许加高、拉长Prefab，并要求相关、相似组件同步调整。本节替代上一节对外框审美的完成结论，保留既有图片、采样与预览能力。
- 只读委派实际读取Beyond All Reason的`luaui/Widgets/gui_flowui.lua`与`gui_buildmenu.lua`：图像、暗边、弱顶部高光与交互状态分层；0 A.D.的`binaries/data/mods/public/gui/session/sprites.xml`与`styles.xml`：黑色肖像底座，正常/悬停/禁用状态分离。参考设计原则，没有复制外部素材或引入依赖。
- 参考URL： https://github.com/beyond-all-reason/Beyond-All-Reason/blob/master/luaui/Widgets/gui_flowui.lua ， https://github.com/0ad/0ad/blob/master/binaries/data/mods/public/gui/session/sprites.xml ， https://github.com/0ad/0ad/blob/master/binaries/data/mods/public/gui/session/styles.xml 。

### 实现与字段

- 新增`KingdomUIArtworkFrame.prefab`及meta，GUID `74352f10d87f4d2fae4cd0c9c4071b27`。固定UI用原生Image创作：外扩6px的深色衬边、1px低饱和细边、2px局部短角标。所有装饰都在图片矩形外侧，非raycast，无中心填充，不再引用按钮材质。
- 11个列表/节点Prefab及root预览嵌入同一个共享frame，保留直接`BuildingIcon/SectorIcon/Icon/ArtworkFrame`绑定路径。嵌套实例显式override名称为ArtworkFrame，避免Unity按Prefab文件名命名实例造成绑定缺失。
- 8个普通建筑卡（基础+7时代）统一高192（原基础104、时代136/144），图片112×112（本轮初稿76，前轮64），左中(24,12)；Pin24×24左中(68,-76)，Label左边界160、右边界保留。时代背景色白→(.52,.56,.55)，压低亮边，时代纹理及GUID保留。
- 星区行高104→192，图72→112，左上(24,-84)，Pin48→24，移至(68,-168)，标题/摘要左边界160；星区设施卡480→560、图64→112，同样采用(24,-84)/(68,-168)，标题/摘要左边界160。
- 生产树共用节点320×104→360×144，图56→88，名称左边界80→112，关系提示保持底部，展开按钮28→36；资源、建筑、星区节点均使用同一扩大后的节点模板。布局器继续读取Prefab尺寸。
- `KingdomUIRoot.Production.cs`移除运行时把名称左边界重设80的旧覆盖，保留Prefab布局权威。`StoryIllustrationPreview.ResetView`按创作外框的sizeDelta为图片fit留出边缘空间，避免外框被viewport裁掉；缩放/拖动/关闭逻辑不变。图片Button增加适度hover提亮。
- `docs/ui/page-responsibilities.md`更新共用图片容器契约。测试以实际装饰几何与不遮图、不挡输入替换已失效的按钮Sprite断言，并增加实际展开星区设施卡截图及图像尺寸一致性检查。
- 未改经济/解锁/Manager规则、图片原PNG或既有GUID。未创建临时Unity项目，未操作用户真实存档、未提交推送。

### 验证过程

- 本轮证据目录`data/artwork-mount-20261021/`，沿用隔离存档和真实GPU PlayMode入口，不使用nographics。测试前备份3个SDF当前工作树版本。
- 初次导入1/2通过：嵌套Prefab默认实例名称与绑定路径不一致，显式名称override后2/2通过、退出0；该结果发生在用户要求放大之前。
- 放大后首次测试新增fixture误用不存在的SectorManager.Instance，真实编译失败，已改为GameManager.Instance.Sectors。下一轮1/2通过，暴露产品代码把生产树名称左边界写死80而覆盖Prefab，已移除该覆盖；未降低图文不重叠断言。
- 以上中间结果均可恢复归档至`.codex/archive/2026-10-21-artwork-mount-attempts/`，活动结果待最终运行追加。

### 后续用户反馈与配套排版

- 用户进一步要求图片必须被完整边框围住，避免图像与背景颜色变化突兀。共享Frame四边改为2px连续闭合低饱和金属边，上沿(.62,.59,.48)，其余(.48,.46,.38)，6px深色内衬和短角标保留。测试检查四边均存在、可见且有实际厚度。
- 普通8建筑卡名称列右界扩展至28%，时代列28–38%、数量38–44%、状态44–64%，避免放大图片后名称被挤成过多行；左边界160保持。
- 增加`KingdomUISectorBuildingMenu.prefab`及meta，GUID `b7e150e09ea542a787d3a8b2b15fd65f`，将原先运行时创建且错误跨满菜单的工程标题改为Prefab固定顶部48高区域。`KingdomUIRoot.Sectors.cs`绑定该Prefab，重复建筑卡仍由数据驱动创建。修复截图中“星区工程”标题与首卡效果文字重叠；设施说明/效果文字20→24。
- 星区展开截图fixture最初取“第一个可见星区”，占领DawnRing后会显示前置满足的AzurePool，导致点击了未占领星区。已按稳定ID明确定位DawnRing，保留真实占领条件和实际展开断言；非产品解锁规则问题。
- 新测试同时测量工程标题底部位于第一张设施卡顶部之上，不按字符串/固定坐标猜测排版。

### 最终验证与收尾

- 最终真实Unity编译成功；GPU PlayMode `ResourceProductionTreePlayModeTests`在`data/artwork-mount-20261021/playmode.xml`记录2/2通过，0失败/跳过；`playmode-exit.txt`为UnityExitCode=0，正常退出。未重跑EditMode或全仓库测试。
- 最终26张截图（13画面×2640×1200/1760×800）在`data/artwork-mount-20261021/pages/`，含普通建筑列表、星区列表、展开设施卡、生产树及建筑/星区大图。目视核对闭合图片边框、长名排版、工程标题与设施说明无重叠；新增几何断言确认装饰不遮图、四边可见、标题在卡片上方。点击/缩放/拖动/复位、旧级保留、星区前置沿用非零真实用例检查。
- 最终日志未发现error CS、NullReferenceException、MissingReferenceException或Prefab读取错误；相关diff --check通过。源码输入仍为原脏工作树及本轮增量，不代表干净HEAD验证。
- Unity关闭后恢复运行前SIMSUN SDF；其余两SDF与备份一致。三份备份和备份目录已移除并核对不存在，无遗留Unity进程、临时Unity项目。两个Temp辅助作者脚本已被Unity清理，核验均不存在。
- 最终标题文案明确为“星区工程”（字号28），与早一轮误保留“星区”的截图区分；之前成功/失败尝试均保存在同主题archive。未提交或推送。

## 2026-10-21 追加：独立图形操作按钮与大图生产卡片

### 用户约束与实施

- 建造和拆除使用不同图形：新增原生Image组成的锤子`KingdomUIActionGlyph.prefab`（GUID 5ed9d917a2594b09a5f330a2c6be6668）和履带推土机`KingdomUIDemolitionGlyph.prefab`（GUID 2b608ca4b34347fcae06ad74f6539b5e）。固定形状都创作于Prefab，非raycast；没有新增图片依赖或安装包。
- 8套普通建筑卡片与星区设施卡的动作按钮统一88×104，右侧固定位置，独立保留×N数量。普通建筑初始绑定与LiveRefresh均写数量；新数量helper只用于建筑，不改变工坊/遗迹的共享文字helper。长按说明保持建造/升级/拆除语义，星区设施说明保留下一工程全名；已完成设施显示“完成”。原交易/解锁/拆除确认逻辑不变。
- 保留基础及7个EraCards独立Prefab、时代材质、GUID和完整时代名称（沿用TechLevel.GetDescription，未修改名称定义）。名称列左界160→200，时代列34–47%，数量47–54%，状态54%至右侧保留224；时代文字自动适配20–32，避免完整名称挤压数量。
- 普通建筑卡保持高192，左图112→164，外框留8px外沿；星区行同样164，标题与摘要左界200。星区设施卡高560保持，左图112→532，文本左界560，左图按卡高铺满；保持完整图片外框、预览绑定及独立工程信息。
- 生产树节点360×144→304×374，上方名称、右上36展开按钮、中央288×288图片横向铺满主体、底部关系说明。图片保持比例，不拉伸、不裁切；布局器继续从Prefab读取尺寸，展开/来源/星区前置规则未改。
- 用户要求直接删除多余内容：实际删除普通8卡、星区行和生产节点的EraAccent对象及全部4个组件/本地引用；不是隐藏。星区设施本来无此对象。Production.cs同步删除其专用时代色线绑定。原卡片闭合边框和图片外框保留。
- `docs/ui/page-responsibilities.md`同步操作图标及生产卡片契约。既有PlayMode用例仅把旧“左图右名”几何断言更新为“标题在图上、说明在图下”，本轮未新增或执行测试。

### 编译、排版预览与边界

- 本轮通过临时Editor捕获器执行真实Unity编译和原生Prefab离屏GPU渲染，没有进入游戏PlayMode、创建Manager或读写真实存档。截图是填入示例数据的Prefab排版预览，不是完整运行页面或玩法验收。
- 最终命令：Unity.exe -batchmode -projectPath D:\GitHub\Kingdom -executeMethod ArtworkActionPreviewCapture.Capture -logFile D:\GitHub\Kingdom\data\artwork-actions-20261021\compile-preview-era-final.log。
- `data/artwork-actions-20261021/unity-exit.txt`记录UnityExitCode=0；最终log记录捕获完成，无error CS、NullReferenceException、MissingReferenceException。未运行EditMode/PlayMode或全仓库测试；原本交接中的2/2是上一轮证据，不能用于本轮结果。
- 3张1400×900预览：`building-actions-prefab.png`使用真实原始/工业/太空时代Prefab及完整时代名称；`production-cards-prefab.png`显示大图主体；`sector-facility-prefab.png`检查设施图片及说明列。已目视检查全名与数量无重叠、操作图形不同、图片闭合边框和色线删除。
- 13个相关Prefab静态检查：序列化对象ID不重复，本地fileID均解析，EraAccent已无残留。相关git diff --check通过。未修改经济、原图、旧资产meta或用户其他脏改动，未提交推送。
- 临时Editor捕获源码保留于本轮证据目录供重现，Assets下临时cs/meta和作者脚本已移除。3份SDF按运行前备份恢复并比对，备份文件/目录移除；没有临时Unity工程或遗留Unity进程。
- 后续若继续改布局，从本轮原始时代卡片与完整名称开始；不要用通用底卡和手写简称替代真实预览。完整页面交互与多分辨率PlayMode本轮未验证。

## 2026-10-21 追加：图片内缩到时代卡片视觉边界以内

- 用户以太空时代卡片截图标示内侧视觉边界，要求继续缩小图片。本轮只调整左图和关联图钉位置，不改变时代卡片材质、完整时代名称、按钮、卡高或生产树。
- 基础及7个时代普通建筑卡、星区行：左图164×164→136×136，左位置14→28，垂直中心保持。连同6px外扩图片框，距卡片左边和上下边缘均为22px（原8px），避免压住时代装饰边框。
- 星区设施卡同步左图532→504，左位置14→28，卡高560保持，同样为22px外缘留白。其文本左界560保持。
- 图钉移至图片与文字之间的窄列，避免缩图后原图钉从图片下方露出一截：普通卡(182,0)、星区行(182,-96)、设施(544,-280)，保持原组件/事件路径。
- 复用上一轮Editor捕获源码，真实Unity编译及Prefab离屏GPU渲染完成，data/artwork-inset-20261021/unity-exit.txt为UnityExitCode=0，compile-preview-final.log记录完成，无error CS、NullReferenceException、MissingReferenceException；相关git diff --check通过。目视核对原始/工业/太空3种真实时代卡片图片外框均在装饰内边界以内。
- 证据为1400×900的Prefab示例排版图，不是完整游戏运行截图；本轮未运行PlayMode/EditMode测试。未改变经济、原图、GUID、存档或用户无关改动，未提交推送。
- 临时Editor捕获器cs/meta移出Assets，源码保留在本轮证据目录。3份SDF恢复到本轮开始前并比对，备份已移除。无临时Unity工程。

## 2026-10-22 续接：图钉、无黑边图片框与工坊样图

- 共享ArtworkFrame移除4个Mat黑色衬边对象，外扩12→4，保留贴图边缘的2px闭合边框。11种卡片Pin按钮56×56、Icon44×44；工坊仅放大Pin，保留原布局和购买控件。
- 星区行/设施复用现有Spacer卡片材质与EraDesign装饰；普通建筑名称列拓宽至0.375，时代列0.375–0.505、数量0.505–0.575、效果从0.575开始，保留完整时代名称。详见Prefab差异与docs/ui/page-responsibilities.md。
- data/sector-pin-20261021/compile-preview-final.log记录Compilation and authored Prefab captures completed，unity-exit.txt为UnityExitCode=0，无error CS/NullReferenceException/MissingReferenceException。完成真实Unity编译与示例数据Prefab离屏渲染，未执行EditMode/PlayMode测试；不作为完整游戏运行验收。相关diff --check通过。
- 临时捕获源码已移至证据目录；Assets临时cs/meta与author-sector-pin.py已移除。3份字体从运行前备份恢复、SHA256逐个一致后移除备份文件和空目录。未创建临时Unity工程。
- 用户最新要求先看3张工坊icon样图。使用内置imagegen，依据实际Label和Description生成催化高压反应器、真空电弧熔炼炉、可复用发射级。约束：无任何人物/动物、主体80%以上、相关环境、无文字与烘焙UI边框。预览保存在C:/Users/19603/.codex/generated_images/01a15972-945e-71c0-9622-d6a6853de4a1/，分别为exec-7eb017a9-1104-4fba-a94e-1a5f8221bd1c.png、exec-12f76117-cbe7-4bae-bd50-cda972ccb755.png、exec-0a6bc690-4db3-4e58-9243-fa8591cad542.png。本轮只展示样图，尚未接入工坊资产。

## 2026-10-22 追加：星区卡片最终采用资源太空时代底稿与星空

- 用户先否决复用卡片，后连续否决平面航图、金色宝石框、不对称冷色装甲等方案，最终明确要求在资源卡「太空时代」原卡片上改造，并采用星空样式。以最新指令为准，不恢复被否决的独立平面框。
- 两个星区专用Prefab保留自身GUID、交互层级及图片/Pin路径，根Image改用EraCards/Spacer/KingdomUIResourceCard的实际Sprite、材质、白色和9-slice参数。SectorDesign在Prefab中创作，包含内缩24px/16px的星空背景与上下偏置柔光轨道；原底稿内停用的EraDesign旧装饰未复制，废弃装饰直接移除。
- 新增Art/UI/Sectors/SectorStarfield.png（内置imagegen生成，无文字人物星球的蓝紫星空；提示词语义为深蓝低亮中心、右上和左下稀薄星云及小星点）与SectorGlowRail.png（代码创作透明柔光轨道）及对应Unity meta。工坊和资源原卡片未改。未接入工坊样图。
- 星区工程按钮放在右下，设施的Build/Deconstruct分别保持独立图形与原事件，按钮外观沿用资源太空边框。共享EraStyle增加SectorDesign识别，使其使用Prefab配色，避免运行时按时代重染背景。Manager/经济/存档未改。
- 曾经读取GitHub arwes/arwes的FrameSVGKranox及propjockey/augmented-ui切角源码作为参考；演示网页连接失败。最终采用用户指定项目内太空时代底稿，没有引入第三方代码或依赖。
- 最终真实Unity命令executeMethod SectorCardDesignAuthor.AuthorAndCapture；data/sector-design-20261022/compile-preview-starfield-visible.log记录Compilation and authored Prefab captures completed，无error CS/NullReferenceException/MissingReferenceException；unity-exit.txt为UnityExitCode=0。早期临时作者脚本出现Image方法名冲突，已修复，失败日志保留，不能与最终日志混用。
- 已目视检查sector-cards-prefab.png与sector-facility-prefab.png：星空可见、标题状态可读、图片边框闭合且无黑色内衬、操作按钮位于卡内。本轮为示例数据Prefab离屏GPU预览，未运行EditMode/PlayMode测试或完整游戏页面交互。
- 最终相关git diff --check通过。临时Editor作者/捕获脚本cs/meta移出Assets，源保留在证据目录；三份字体恢复到运行前备份并逐个SHA256比对后删除备份及空目录。被否决自制SectorNavigationPanel及meta已移至证据目录，正式Art目录不留废弃资产。未创建临时Unity工程，未commit/push。

## 2026-10-22 追加：自主选择三处局部UI美化

- 用户授权“找几个感兴趣的地方美化UI”。选择生产树节点、时代页分区、音乐列表，限定三份Prefab与视觉契约，无Runtime代码修改；未改已确认星区、建筑时代卡、工坊、定义、经济或真实存档。
- KingdomUIResourceTreeRow：根色(.32,.36,.34)→(.45,.51,.49)，增加标题/关系说明深色凹槽与细分隔线；展开背板与状态文字提亮。根304×374、插图288×288、所有原点击/拖动/预览路径保持。
- KingdomUIEraSection：根背景(.07,.09,.08,.48)→(.065,.085,.078,.95)，新增现有金属Sprite标题底板与顶缘，铜色闭合边框和分隔线，标题30→32加粗。Title/Content位置、完整时代名称、运行时高度计算保持。
- KingdomUIMusicTrack：行高/PreferredHeight56→76，名称34→30、时长34→26居中；增加alpha .30背景与列间分隔，改进现有播放按钮底板，Icon内缩24→20。Prefab列与现有运行时50%/18%/14%/18%一致，增加原绑定已支持的Category Image槽。动态当前曲目/交替行底色和播放逻辑保持，装饰全部非raycast且绘制在控件后。
- 子代理只读核对三目标消费端与装饰/选中态风险，未编辑或运行测试。最终真实Unity编译/离屏GPU捕获：data/ui-polish-20261022/compile-preview-final.log记录3 final layout captures completed，unity-exit.txt为UnityExitCode=0，无error CS/NullReferenceException/MissingReferenceException。已查看production-after.png、era-after.png、music-after.png。
- 截图是示例填充Prefab，不是完整运行页。第一轮Era示例Body未按运行时排版、Music缺分类图标，已修正最终捕获；初始before图移至initial-fixture，不能当准确完整前后对照。最终音乐背景用近似运行时颜色，未执行真实音乐Manager状态绑定/播放。
- 本轮未运行EditMode/PlayMode测试，未以静态检查声称完整交互验收。相关diff --check通过。临时KingdomThreePanelPreview.cs/meta从Assets移除，源码保留证据目录；三份字体从备份恢复并逐个SHA256比对后清理备份文件/空目录。无临时Unity项目，未提交推送。

## 2026-10-22 追加：星区星云动态背景

- 用户要求星云背景动态化。新增SectorNebula.shader与共享SectorNebula.mat（含Unity生成meta），两个星区Prefab仅将SectorDesign/Starfield的Image.material由null绑定至共享材质。原星空贴图、金属外框、星球、文字、按钮和布局保持。
- Shader采用Unity内置_Time.y驱动缓慢有界UV漂移（0.015）和局部轻微扭动（0.003），星点亮度轻微闪烁、星云亮度呼吸2.5%。UV缩放0.94并内移0.03防止Clamp边缘拉伸，不增加每卡Update、协程或运行时材质实例。保留uGUI Stencil、ClipRect和AlphaClip支持，背景不拦截点击。
- 本轮真实Unity批处理执行SectorNebulaAuthor.Run，shader.isSupported=true、ShaderUtil无错误消息，完成24帧Prefab离屏GPU渲染。compile-preview.log记录_Time.y从0.72增长到7.66；首尾像素差RGB均值约1.245/1.763/2.781，已目视检查中间帧。nebula-preview.gif按300ms原速合成（短片循环接点不代表游戏动画循环）。仅星区行有连续预览，设施使用相同材质绑定，未做设施独立动画捕获。
- Unity在完成捕获、调用Exit(0)后卡在网络请求退出阶段；205秒后仅终止本轮启动且路径/ID已核对的Unity进程。unity-exit.txt明确NormalExit=False，不能宣称Unity正常退出；编译/Shader支持和渲染完成证据有效。未运行EditMode/PlayMode测试或完整游戏交互。
- 证据位于data/sector-nebula-20261022/。临时Editor源码移出Assets并归档于该目录，cs/meta均移除；3份字体恢复运行前备份并SHA256逐个一致，备份文件与空目录已清理。无临时Unity工程、无真实存档变更、未commit/push。

## 2026-10-22 追加：星云顿挫观感与研究树双层底板增强

- 用户反馈星云顿挫，并确认同时增强研究树整张底板和每个研究节点。上轮GIF每帧300ms（约3.3fps），确有预览采样顿挫；没有游戏内性能采样，不能据此断言游戏帧率问题已定位或修复。
- SectorNebula.shader：星点改为固定UV，仅柔化后的星云纹理随有界漂移移动；用移动/静止低频采样差叠加保留原构图，取消细小星点整体移动与局部高频UV扭动。漂移sin/cos移至顶点计算，闪烁幅度18%→10%、星云呼吸2.5%→1.5%；保留UI裁切/Stencil。SectorStarfield导入启用mipmap、Bilinear→Trilinear供低频星云采样使用。共享材质与两张星区Prefab绑定路径保持，没有逐卡Update。
- 新增ResearchGraphPanel.png（1024×640、代码创作的石墨蓝底、淡网格、双层闭合边框与铜色角线）与meta，border24九宫格。KingdomUIRoot的真实ResearchGraphViewport和备用KingdomUIResearchGraph各创作首子节点ResearchBackdrop，Image非raycast；不占用或替换手势拖动面、遮罩Image、节点/连线层。
- KingdomUIResearchNode的EraFrame透明度0.16→0.36；普通底色(.16,.19,.19,.96)→(.22,.27,.28,1)，锁定(.10,.12,.12,.94)→(.13,.16,.17,1)，聚焦(.30,.36,.36,1)→(.32,.40,.42,1)，普通轮廓(.28,.30,.30,1)→(.38,.47,.49,1)。ResearchTree和ResearchQueueGraphic的初建/刷新统一引用原分部类内的两项共享颜色，避免刷新回退；未改时代贴图/进度贴图/金色研究框规则/节点尺寸/拓扑/交互。
- 最终真实Unity执行ResearchNebulaPolishAuthor.Run，compile-preview-final.log包含Shader支持与180帧GPU捕获完成，无error CS/Shader error/Exception。连续采集6.300秒，平均28.413fps，帧间隔中位35.271ms、P95 37.062ms、最大38.007ms；这是离屏编辑器采集速度，不是游戏性能测量。nebula-smooth-preview.png为保留实际时长的180帧APNG，单次播放避免短片首尾跳变；该图是星云局部预览，不能作完整星区卡片布局截图。
- research-panel-and-nodes.png已目视检查：使用真实节点Prefab与时代/进度纹理，四时代、四状态填充样例；示例连接为演示线段，不是真实运行的研究图/拓扑验证。未运行EditMode/PlayMode测试或游戏内帧率分析。
- 两次批处理Unity均完成编译/捕获后卡在网络请求退出阶段，由启动包装器在各自150s/90s时限后仅清理本轮进程。最终unity-exit.txt为NormalExit=False，不宣称正常退出。第一轮截图尺寸不适用于完整星区卡片，连同日志/帧归档在initial-fixture。
- 临时Editor源码移至证据目录，Assets内cs/meta已清理；三份字体从本轮前备份恢复、SHA256逐一一致后删除备份及空目录。仅修剪目标Prefab新增序列化行末空格，相关git diff --check通过。未建临时Unity工程、未动真实存档、未提交推送。证据目录data/research-nebula-polish-20261022/。

## 2026-10-22 追加：全部工坊物品插图与原卡片接入

- 用户要求生成全部工坊图片，喜欢此前3张样图；后续允许蓝天白云和不同风格，但必须突出物、无人物。依据当前92个WorkshopUpgradeDef的实际Label/Description建立data/workshop-art-20261022/manifest.json（工业37、太空52、极致3），保留3张已批准样图，其余用内置imagegen一物一次生成。图中无人物、鼠、动物或人形角色，物件近景约80–90%，无烘焙UI边框/标题/水印；室外晴空森林田野货场、室内工厂实验室、轨道坞等按物品语义选择。
- 首轮部分结果成为纯白/纯黑产品底，40张已明确退回并重新生成真实环境，旧JSON与正式PNG可恢复归档在rejected-white-background/，原始generated_images未删。最终92张均图审，联系表覆盖全部：workshop-gallery-01..04.png，精选workshop-art-selection.png；每件完整提示词和源路径在batch-*.json，3个复用样图及制图台在reused-and-first.json。
- 正式图片Assets/Resources/Art/WorkshopIcons/{ID}.png（92 PNG及92 meta）。WorkshopUpgradeDef新增私有SerializeField Sprite icon、只读Icon和Editor强类型SetIconForEditor；AuthoredRows为工坊绑定现有BindArtworkIcon，复用既有大图预览Button行为。
- KingdomUIWorkshopCard保留原背景/时代风格和104高度，Prefab创作WorkshopIcon（复用现有BuildingIcon的Image+Button+闭合ArtworkFrame），88×88左x12；Pin56×56移至x112，名称/metadata左边距184。图片正常撑满无黑色内衬，时代完整名称与原购买布局保留。没有运行时造固定层级。
- Unity作者源WorkshopArtworkAuthor.cs、run-author.ps1在证据目录。真实Unity 2022.3.62f3c1执行WorkshopArtworkAuthor.Run；compile-preview.log含Bound all 92 icons, authored the WorkshopCard, and captured its preview，无error CS/NullReferenceException/MissingReferenceException；unity-exit.txt为NormalExit=True; UnityExitCode=0。workshop-icons-prefab.png已目视检查四张真实Prefab示例：原时代卡片保留、图/Pin/完整时代/购买按钮可读。
- 所有图片Single Sprite/FullRect、max1024、mipmap、Trilinear、Clamp、无压缩；保留生成原图。check-import.py核对92个唯一Sprite GUID引用与导入设置；definitions-before-import.json记录运行前当前文本，导入后恢复Unity无关重序列化，仅增加icon行，验证92定义其余字段逐字一致（忽略行尾空白）。原已存在的工坊前置/效果用户变更保留。
- 本轮未运行EditMode/PlayMode测试、未实际点击预览或游戏内长程节奏验收。Prefab示例渲染与静态检查不代表全页运行交互验收。研究前置四处调整见同日simulation-pacing-handoff与data/research-order-20261022/。
- 临时Assets/Editor/WorkshopArtworkAuthor.cs与meta已清除；三份TMP字体从本轮前备份恢复且SHA256逐个核对，备份文件/空目录已清除。无Unity进程、无临时Unity副本、真实存档未动，未commit/push。仅修剪目标Prefab新增行尾空格。
- 收尾检查：本轮相关Prefab/工坊定义/四研究定义/UI绑定的git diff --check通过；最终closure-final.md退出0，143研究、92工坊、79建筑仍全可达，92 PNG/meta均存在。import-check.txt记录92引用/导入设置及非icon字段保持检查通过。

- 2026-10-22用户授权清理后：上述旧尝试/中间帧/被拒图片/一次性作者脚本/审查联系表已删除，历史段落路径仅记录当时行为，不再是当前入口。当前保留工坊manifest与batch提示词、正式92图片、四页最终图集、精选图、Prefab最终预览、compile-preview.log、unity-exit.txt和import-check.txt。复核92/92引用及四研究前置通过，详情见同日repo-cleanup-handoff。
## 2026-10-22 图片优化与废图清理收尾

- 建筑79/工坊92/星区10张整图至768×768，剧情18张至1280×720，金属图集至1024×512并同步7精灵矩形；无裁剪，环境构图保留，其他小图仅PNG压缩。GUID、spriteID/internalID保持，92工坊manifest已同步SHA256和尺寸。
- 当前保留245张优化图源640.55→272.12 MiB，减少57.52%，未测量构建包。75张无消费者的旧贴图及旧ClayPit重复图删除；应用/研究树/音乐/Pin动态加载图保留。旧图集/精选/Prefab截图已删除，当前预览data/art-optimization-20261022/imported-artwork.png。
- 真实Unity编译/导入/GPU预览完成：导入时294张，200缩图；79建筑、92工坊、33资源、10星区图片引用有效。Unity自然结束，OS退出码未捕获。废图删除后静态引用和92工坊SHA256再次通过。未运行测试套件或构建包。最后临时清理被自动审批审查拒绝，临时作者工具、原图备份、审查拼图及脚本尚存，精确残留和下一动作见repo-cleanup-handoff同日收尾节。旧段落路径仅为历史记录。

