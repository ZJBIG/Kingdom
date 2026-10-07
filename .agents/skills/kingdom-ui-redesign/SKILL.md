---
name: kingdom-ui-redesign
description: Kingdom 项目主入口的UI领域分支，维护Unity布局、研究图、滚动/拖动、安全区及横屏交互验证；页面/详情语义按需读取唯一专题。用于Prefab、详情页与交互任务，纯UI不得改变经济、存档或定义ID。
---

# Kingdom UI 与研究图

若直接命中本技能，先经根 `AGENTS.md` → `.agents/skills/kingdom-project-dev/SKILL.md` 完成范围检查；同轮已读入口不回读。再读 `docs/architecture/ui-boundaries.md` 和相关handoff。保持Manager规则、经济公式、定义ID、存档格式不变；涉及玩法变化由主入口加读经济分支并确认授权。固定组件创作于Scene/Prefab，代码只绑定；仅数据驱动重复节点/连线与已记录的必要兼容回退允许运行生成。

## 固定层级、布局与兼容边界

- `SafeAreaRoot/Content/PageTool` 是页面级固定工具的统一外层；Overview导航工具与研究队列保持同级，由当前导航页控制可见性。运行时只绑定已有控件，不复制固定层级、按钮、文案或尺寸。
- 优先使用Prefab中的Vertical/Horizontal/GridLayoutGroup、ContentSizeFitter和LayoutElement承担固定布局；不要用逐卡片Update或手算sizeDelta替代布局所有权。展开卡片切换其Details，资源分类不靠重新挂入隐藏Transform实现。
- 共享详情外壳仅在旧root Prefab缺少兼容详情层级时允许重建；此兼容例外不覆盖页面工具栏、按钮、标签、固定尺寸或普通行。重复内容仍须真正由数据驱动。
- 页面刷新与研究页缓存的当前生命周期见 `docs/architecture/ui-boundaries.md`；不得仅因隐藏页面而停掉模拟、研究或MusicManager。

## 页面操作契约

涉及页面、详情或选择态时，读取唯一契约 `docs/ui/page-responsibilities.md`。页面职责、所有详情主按钮、星区入口、研究金色Outline、选择清理和单按钮内边距仅在那里维护。纯图手势或安全区任务无需加载无关页面条款。

## Canvas与参考

- 活跃CanvasScaler必须为 `ScaleWithScreenSize`、参考分辨率 `2640x1200`、Match Width；禁止ConstantPixelSize导致固定导航/详情之间的viewport宽度为负。
- 保留ResearchTreeSK样式基准：`NodeSize=(205,50)`、`NodeMargins=(50,10)`、`NodeFullSize=(255,60)`、`CurveRadius=10`、`LineThickness=4`、`ArrowThickness=16`，复用现有线、曲线、箭头、时代和进度纹理。
- 如仓库内 `ResearchTreeSK.il` 实际存在则读取；当前缺失时明确记录缺口，以当前Prefab、纹理和上述契约继续可验证工作，不声称已读取参考。外部checkout仅可选，不能成为强制依赖，不自动下载。

## 研究图布局

1. 以Prerequisites生成确定性、由左至右的整数拓扑网格；节点保持参考尺寸，连线位于节点后。
2. 行坐标采用左上角原点；节点与连线统一一次转换到Unity左下RectTransform空间，不能分别反转两次。
3. 共享整数网格线段作为总线；保留前置方向，选中节点时高亮完整传递前置闭包及相应连线。
4. 记录布局选择及原因、重复格/逆向边/inversion计数。当前 `CreateResearchTreePositions` 采用topology-only，不编造现有authored fallback。
5. 仅当以后明确获准需要authored回退时，记录诊断理由，将x/y都整数化并最小修复重复格和逆向列；保留不需修复的双轴，不能把原x任意换成深度列。不能仅因旧指引提及回退而恢复它。
6. 不加空白行伪造滚动；以实测viewport/content决定每轴pan范围。

## 触摸与隔离

- 每个研究Button转发拖动生命周期到图手势所有者；从节点和空白区开始均可拖动整图，短释放仍触发点击。
- 节点后必须有透明且可射线命中的背景拖动表面；空RectTransform或仅ScrollRect不足以初始化pointer drag。
- 按真实边界限制双轴，pinch zoom与拓扑独立；记录逐轴overflow，不在未溢出的轴声称滚动。
- UI归属 `KingdomUIRoot/SafeAreaRoot`；新UI启用前隔离旧Viewer/Displayer，防止旧对象渲染或接收输入。既有固定层级只能绑定，不能借隔离要求重新运行时搭建整个界面。

## 验证门槛

涉及图行为变化时运行真实Unity编译和PlayMode：
`ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping`。

要求日志证明：
- 实际定义总数与生成节点数一致，每个节点占唯一整数格；保留至少79节点的既有最小覆盖下限，不能把旧79当当前固定总数。
- 前置向前、拓扑诊断明确、viewport/content宽高为正、实际pan范围和内容位移、正确手势所有者与旧UI不活动。
- 无Null/MissingReference或Prefab导入错误；按钮短触、节点拖动、背景拖动和独立缩放真实可用。

静态Prefab配置、字符串检查、零项目用例或跳过关键交互不算运行验收。横屏、安全区和交互手感需直接运行证据；没有就明确未验证。未运行Unity须写“未执行真实 Unity 编译。”。命令/存档隔离与副作用参见项目开发技能验证手册。
