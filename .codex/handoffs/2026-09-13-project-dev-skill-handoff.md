# 2026-09-13 Kingdom 项目开发 Skill handoff

## Current status — 文档合并续接

2026-09-14已继续完成单主入口合并，静态入口及23项只读/控制流检查通过；完整文件系统/真实链接工具回归仍未通过，不宣称整体工具回归完成。当前合并结果见文末“单入口收敛续接”；此前创建、清理和失败记录保留历史身份。

## Initial delivery (historical)

完成用户要求的项目全范围盘点与项目针对性技能开发。新增 `kingdom-project-dev`，作为开发导航与验证总入口，保留既有三个领域技能。起始 HEAD 为 `d0ef657b2e45fb9e0199c569868275f2ec091c9d`；未修改游戏代码、数值、Scene/Prefab、依赖或原技能。

## Scope and method

起始版本管理文件1744；自有C#147（Runtime75、Editor13、EditMode40、PlayMode5、模拟器14）；内容定义355。完成目录盘点、关键实现读取、两路成功子代理的内容/UI与测试工具扫描、独立安全审查与修复复审。生成缓存/归档/第三方实现/二进制素材不作逐行语义审查。扫描期间其他任务新增 ToDoList、DeepAudit输出和工作记录，予以保留，不算本任务产物。

## Changes

- `.agents/skills/kingdom-project-dev/SKILL.md`：定位、领域路由、权限边界、实施和交付闭环。
- 同目录 `references/`：project-map、validation、subagents、evidence、acceptance-cases，共5份按需参考。
- 同目录 `assets/handoff-template.md`：持续交接栏目。
- `.workbuddy-ai/skills/kingdom-project-dev/SKILL.md`：WorkBuddy发现入口，仅引用规范技能，不复制领域规则。
- `tools/codex/kingdom_project_probe.py`：标准库只读巡检、指定文本指纹、必要路径和简单本地内联链接校验，stdout输出；无联网/shell/Unity调用。
- `tools/codex/test_kingdom_project_probe.py`：19项工具测试，fixture限定已验证的项目outputs。
- `AGENTS.md`：只新增项目开发技能/辅助工具导航段，不改旧质量门槛。
- `outputs/Kingdom-Project-Skill-Report.md`：扫描、验收、安全审查与使用说明；另交付标准技能zip和项目集成zip，以及巡检JSON、测试日志与文件哈希清单。

## Validation performed

- 工具测试19项：18通过、1跳过；真实符号链接创建受Windows权限限制，模拟链接、UNC拒绝、链接检查顺序和输出目录保护均通过。
- 技能frontmatter与命名基础检查通过；真实项目必要路径/技能引用检查零错误，最终白名单巡检1741文件（与起始Git全仓1744文件口径不同）。
- 标准ZIP 7文件、项目集成ZIP 10文件完成CRC及内容核对；12项文件/包哈希清单复核通过。2个Python文件AST检查通过；git diff --check通过，已跟踪文件仅AGENTS增加13行导航。
- 初审发现3个P1并修正：链接检查必须先lstat、测试outputs需验证整条目录链、纯只读不默认写交接。独立复审确认在可信本地静止工作树范围无未解决P0/P1，人工静态审查口径为P2；非第三方认证。
- 12条技能行为场景仅作静态核对，不声称客户端自动触发端到端测试。
- 未执行真实 Unity 编译。未重跑经济closure或模拟器；本任务未改经济定义。旧661/661和旧PlayMode33通过/1跳过仅作背景，不是本轮游戏结果。

## Remaining risks

- 不保证映射网络盘识别或恶意并发文件替换下的原子隔离；真实Windows junction/符号链接隔离仍待补测。
- Markdown链接验证只覆盖简单内联本地链接，不验证引用式语法/锚点。
- WorkBuddy入口文件已接入；当前会话热加载未实测，必要时新会话显式指定技能。
- 原领域技能/架构说明存在旧79节点、Pair序列化、模拟器输出、测试口径漂移；已记录但未擅改原技能。

## Next concrete action

在下一项实际Kingdom开发任务中显式使用 `kingdom-project-dev`，核对新入口路由；按用户授权的具体问题收集真实Unity证据。另立小批次整理旧文档契约，保留运行质量门槛，不因旧指引漂移扩大为全仓重构。

## 文档合并续接 — 2026-09-13

### 已有工作与本轮范围

接手时根AGENTS、repository-map、经济/UI技能、经济prompt和校验入口已存在未提交合并；content-expansion旧目录已移出，project-dev与经济references尚未跟踪。本轮保留这些工作，没有修改游戏C#、资产、GUID、依赖或真实存档，没有提交或推送。

规范结构维持三个技能：project-dev负责工作流，economy-simulation统一内容/经济，ui-redesign负责布局/交互。根/scoped AGENTS维护硬约束，README/prompt/WorkBuddy入口只导航；历史交接和归档不改写为当前事实。

### 变更与归属

- README：去掉已退休内容技能和策略轨迹要求，指向现行入口。
- docs/architecture/serialized-pairs.md：明确ResourceAmountDefinition资产string与runtime Pair边界，保留兼容契约。
- docs/architecture/runtime-state.md：删除旧Kingdom3目标tick、重复段落和转义残留；引用实际ManualTick与唯一顺序地图，保留满足率/事务/存档约束。
- docs/architecture/ui-boundaries.md：只维护State/生命周期；以KingdomUIRoot刷新和Research CanvasGroup缓存纠正旧Viewer迁移目标。固定层级、PageTool、详情回退和页面操作完整迁入UI技能。
- docs/architecture/research-queue-payment.md：调用入口改为ManualTick→ResearchManager.Tick，支付/旧档台账契约不变。
- docs/testing/content-balance-tests.md：缩为兼容导航；定义与可达性归acceptance-checklist，启动/保存/后台/页面/剧情场景归playmode-test-plan，取消“已有场景必须新增”的旧状态断言。
- 两份scoped AGENTS：区分隐藏与Deactivate，允许布局测试测量相对几何而非写死坐标，不要求无关重构。
- project-dev SKILL及验证参考：补充合并流程、单测写入/清理副作用与保护阻断时停跑规则。
- validate-guidance.ps1：补齐实际保留的参考、模板与验收文档依赖。
- 经济技能直接读取验收清单，避免回读兼容入口形成循环；路线图统一为确定性回归诊断，不再使用“仅历史诊断”的冲突描述。

### 验证结果

- validate-guidance.ps1 -ProjectPath D:/GitHub/Kingdom -RepositoryPath D:/GitHub/Kingdom：退出0。
- Python 3.13.12，kingdom_project_probe.py --project-root D:/GitHub/Kingdom --check-skill --format text：1735文件，Errors=0，退出0。仅证明白名单静态入口/简单链接，不证明所有Markdown语法、锚点或游戏行为。
- git diff --check：通过；两个probe Python文件AST解析通过。Assets/Packages/ProjectSettings范围仅两份AGENTS修改，无游戏实现/资产差异。
- 活动docs与技能目录未发现已退休技能或旧project-map引用。根规则中的State/事务、Food唯一封顶、反射禁止、稳定ID和数值容差门槛保留。
- 第一轮工具测试的异步结果无法回取，不作为通过证据。再次运行的完整日志为 outputs/Guidance-Consolidation-Tests-20260913.txt：25项，1 failure、27 errors（含teardown重复计数）。主要为客户端删除保护要求确认；真实链接用例另有断言失败及访问拒绝，原因未查明。停止重跑，未绕过保护、未弱化测试。
- 定位出两个测试自身缺陷并修复（tools/codex/test_kingdom_project_probe.py）：(1) unittest默认在cleanup抛错后仍继续后续setUp，导致失败后仍不断新建fixture；改为入口 failfast，并在验证参考中要求外部runner同样开启。(2) 真实链接用例用 `link.unlink()` 清理Windows目录symlink（WinError 5拒绝访问）；改为按lstat类型分派：Windows目录链接用rmdir，其余用unlink，仅删除已核验归属的链接本身并检查残留。同时把“链接创建是否真的成功”改为独立lstat断言，不再用被测is_link当跳过条件。
- 新增不创建fixture、不真实删除的纯内存回归 `ProbeHarnessTests`（9项）：覆盖failfast停止、cleanup抛错不再进入下一个setup、三类链接清理分派、非链接保留、残留报错、不吞删除保护、拒绝删除非归属链接。9项全部通过。
- 子代理完成了前置文档/源码只读审查；后续两次独立复核均因服务连接失败未完成，主代理接管diff和静态复核。
- 未执行真实 Unity 编译。未运行经济模拟/closure或Unity测试；仅规则整理，不宣称游戏回归通过。

### 归档与未解决事项

- 初次巡检发现outputs有64个旧probe-test目录。逐文件确认均为fixture文本后，完整移动至 .codex/archive/guidance-fixtures-20260913-2200/，移动前后SHA-256一致，原位置剩余0；没有永久删除。
- 先前规则备份 .codex/archive/kingdom-guidance-before-20260913-1451.zip CRC校验通过。旧包及报告仅作历史，不是新版技能交付。
- 本轮失败测试在 .codex/archive/ 顶层留下46个probe-test目录。已按用户明确授权处理：先逐文件核对内容（1,016个文件、26,598字节，全部为fixture文本，无符号链接），打包为 .codex/archive/probe-fixture-residue-backup-20260913-2307.zip 并回读校验（CRC通过，1,016/1,016文件哈希与清单一致），再分批删除这46个目录，全部原位置已确认移除。
- 核验结果：清单内46个目录全部删除（0残留）；.codex/archive、outputs、.agents/skills 下无probe-test残留；历史归档 guidance-fixtures-20260913-2200/（64个目录）与其他归档条目未受影响。记录见 outputs/Probe-Fixture-Cleanup-Manifest-20260913.json 的 cleanup 段。
- 清理仅针对已核验并已备份的枚举目录，未使用通配批量删除，未触碰其他归档、游戏代码或资产。备份ZIP保留，可按清单恢复。
- 下一步：在受支持的执行环境中完整运行 tools/codex/test_kingdom_project_probe.py（含真实文件系统与真实链接用例），确认先前1 failure/27 errors中的链接用例已随本次修复通过；若删除保护再次拦截，保持停跑并保留现场。控制流回归通过不等于完整回归通过。文档合并本身已落盘，不为修复工具测试扩大为游戏重构。

## 临时文件清理收尾 — 2026-09-14

- 用户确认仅清理本次技能任务临时产物，保留正式技能、最终文档、可恢复备份及失败证据，不处理其他任务的PDF/HTML等产物。
- 64组旧fixture的松散文件已逐项与 `.codex/archive/kingdom-guidance-before-20260913-1451.zip` 核对后清理；旧content-expansion空目录及其原有6个文件已退出活动目录，规范技能为3项。
- 本日完整工具回归运行10项后failfast：9项控制流通过，真实链接用例失败。诊断显示创建结果是普通目录（mode 040777、attributes 16、reparse tag 0，readlink报4390），不是符号链接；不弱化断言或改记通过。随后诊断子集因客户端批量删除保护在第10项cleanup报错而停跑，不能声称33项通过；日志保留在 `outputs/Guidance-Verification-20260914.txt`。
- 失败子集产生的 `.codex/archive/probe-test-x2s7xg2c` 共22个文件已先打包校验到 `probe-test-x2s7xg2c-backup-20260914.zip`，再按用户确认分批清理。最后只剩14个空目录，本次确认后已实际移除整棵空目录树。
- 原松散归档 `guidance-fixtures-20260913-2200/` 已移除；压缩备份保留，避免全仓搜索继续命中测试规则。未触碰真实项目 `.workbuddy-ai/`、游戏代码、资产或存档。
- 最后入口检查通过（1736个白名单文件，Errors=0；validate-guidance退出0），但完整工具回归仍未通过。暂停所有会生成fixture的测试，后续仅在链接语义与正常清理权限明确的环境中继续。未执行真实 Unity 编译。

## 单入口收敛续接 — 2026-09-14

### 范围与保留

本次用户要求继续合并相近Skill、AGENTS及开发文档。接手时已有大量未提交的上一轮合并，旧content-expansion六文件删除已存在。本次只修改指导文档与验证契约，未改游戏C#、资产、GUID、依赖或存档，未提交/推送。并行清理任务在本handoff追加的“临时文件清理收尾”原文保留，不计作本次执行。

### 合并结果

- 根AGENTS只保留硬约束并指向project-dev；唯一任务路由移到主技能。README、经济prompt和领域元数据先经主入口；已读入口不递归回读。领域名称保留，未新建同义技能。
- 主技能新增 `references/guidance-maintenance.md`：主题归属、独有条款迁移、快照、旧入口兼容、历史边界、安全及交付流程。
- engineering rules缩为指针；产品默认归conservative-defaults，消除重复Current UI contract与冲突桌面分辨率。人口容量不追溯踢人、退款/离线进度保守边界保留。
- 页面/详情职责集中page-responsibilities，保留Resource无主按钮、左右48内边距、Outline非当前项/连线排除及选择清理；detail-action-policy变指针，UI技能和验收引用同步。
- Kittens参考边界并入经济content-design，保留原参考链接和禁止项；原文档变指针。scoped AGENTS继承全局，仅补目录规则；explicit deltaSeconds、存档ToString和禁止BigNumber别名安置到权威文档。
- 旧UI-ready计划加历史标记但不删原文；tools说明撤掉不存在的content-dependency路径，模拟器README按csproj/Program纠正net9.0 Exe和CLI事实，并指向唯一命令手册。
- validate-guidance不再依赖忽略的archive/README；补齐新参考、UI/产品专题和scoped AGENTS。probe仅变必要文件清单，不改扫描/清理算法。

### 验证与安全

- 23项 `GuidanceContractTests ProbeHarnessTests` 通过，0失败/错误/跳过；前者新增14项只读契约/内存反例，后者9项原控制流。未运行会创建fixture的完整套件；不覆盖真实链接语义。
- validate-guidance退出0；真实仓库probe --check-skill扫描1736个白名单文件、Errors=0。简单内联链接检查不证明所有Markdown语法/锚点或客户端自动发现。
- git diff --check通过；仅指导和工具契约范围变更。两个辅助审查代理分别因提供方400参数拒绝、500服务不可用失败，主代理接管；没有独立代理复审结论。
- 专项安全审查能力本会话不可用；对技能正文、全部references/assets和相关脚本采用人工静态核对。没有引入联网、下载执行、凭据访问、提权或自动安装。存量全套测试存在已披露的创建/清理fixture副作用，继续停用；不声称安全认证。
- 未执行真实 Unity 编译。未运行Unity游戏测试、经济closure/模拟器或设备验证。

### 备份与交付

修改前56文件快照：`.codex/archive/guidance-before-20260914-124959.zip`；模拟器README补充快照：`.codex/archive/guidance-extra-before-20260914.zip`；均CRC和内容哈希核验。未删除原指导文件，旧路径保留兼容。

交付目标：`outputs/Kingdom-Guidance-Consolidation-20260914.md`、同名前缀的Verification.json、项目指导集成ZIP。包是Kingdom项目相对路径快照，不是可全局安装的通用技能；恢复或应用前比较工作树，不能覆盖别人的未提交改动。

下一动作：后续Kingdom任务从根AGENTS进入主技能按需选择分支；在支持正常链接和清理的环境单独解决完整工具回归，并另行验证客户端新会话的自动发现。

## 用户确认的备份清理 — 2026-09-15

已按明确列举的6文件范围，通过环境受保护的回收站入口逐项处理；每项验证原路径消失，6项全部成功，总计1,162,160字节。未使用永久删除，未清空回收站，未禁用或绕过保护。

- `.codex/archive/kingdom-guidance-before-20260913-1451.zip`
- `.codex/archive/probe-fixture-residue-backup-20260913-2307.zip`
- `.codex/archive/guidance-before-20260914-124959.zip`
- `.codex/archive/probe-test-x2s7xg2c-backup-20260914.zip`
- `.codex/archive/guidance-extra-before-20260914.zip`
- `outputs/Kingdom-Guidance-Consolidation-20260913.txt`

前文及旧报告中的“备份保留”仅代表当时状态；以上备份现已不在仓库，恢复需在回收站尚未清空时进行。最新合并报告、Verification.json、指导集成ZIP、两份失败日志与清理清单均保留；47份正式指导/工具文件与交付包哈希一致，包CRC正常。未改游戏实现/资产或其他任务产物，未新增清理报告或备份。未执行真实 Unity 编译。

## 用户确认清理过程报告 — 2026-09-15

用户进一步选择“本次整理的失败日志也清理”，并逐项确认以下3文件移入回收站：
- `outputs/Guidance-Consolidation-Tests-20260913.txt`
- `outputs/Guidance-Verification-20260914.txt`
- `outputs/Probe-Fixture-Cleanup-Manifest-20260913.json`

执行前校验路径链非链接及文件SHA-256与确认清单一致；通过受保护回收站入口逐项操作并验证，总计300,153字节，3项原路径全部消失，未清空回收站。没有新建备份或清理报告，未扩展至其他任务文件。旧报告中的失败日志路径及“保留”结论仅代表历史；完整工具回归仍未通过，不因移除日志改变结论。

最新合并报告、Verification.json及指导包仍存在，指导包CRC正常。额外一致性检查未通过：当前7份指导文件与20260914包的哈希不同，分别为 `.agents/skills/kingdom-project-dev/references/acceptance-cases.md`、`AGENTS.md`、`docs/architecture/research-queue-payment.md`、`docs/architecture/runtime-state.md`、`docs/content/progression-roadmap.md`、`docs/repository-map.md`、`docs/story/story-data-authoring.md`。本次仅操作3个指定过程文件，没有修改或覆盖这7份文件，也未自动重打包；不可再声称旧包与当前工作树完全一致。未执行真实 Unity 编译。

