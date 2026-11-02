# 新手引导最终验证

本目录以 UTC/机器日期命名。工作区 HEAD 为 `b075387a84c30fc7a7d56e6459cfba027b8c4506`，存在未提交修改；最终输入见 `verified-inputs.json`。本次续接复核既有文案修改，补齐导航、前置预告与中文面板适配。

## 行为与证据

- 行动前解释收益：首次木屋导航明确“让族人有处落脚，也让研究开放”，再要求点击建筑入口。缺料不掩盖导航指令；进入详情后展示实际阻碍和下一步。
- 等待仍能理解目标：缺料、排队、研究中和实际生产等待保留 NextGoal，UI 解除输入门禁；同一状态不被“下一步”误记完成。前置研究和材料来源分支的收益/预告对应当前真实行动，不提前声称已到后续目标。
- 成果反馈：木屋落成提示研究开放；农业完成提示建农场；农场实际供粮、石材实际加工与时代跃迁沿用真实状态完成条件。农场首次产量不再声称食物已充足。
- 资源说明采用每秒净收入，匹配实际显示；长期目标改为“筹备下一次时代跃迁”。8 个正式教程资产及回退定义同步，ID、GUID、条件、存档与经济数值不变。

## 执行结果

1. `tools/codex/run-unity-tests.ps1 -Platform EditMode -ProjectPath D:/GitHub/Kingdom -UnityPath D:/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe -TestFilter 'TutorialStageTests.UnaffordableHomeExplainsNavigationBeforeShowingTheWait;TutorialStageTests.SettlementFollowsRealPrerequisitesThenPlanningAndTheEraTransition'`，本目录指定 `edit-review-before.xml/.log`：2 条均在修复前失败，分别复现导航说明缺失和前置研究预告跳步。XML 写出后退出超时，包装器在 180 秒结束自己启动的 Unity。
2. `tools/codex/compile-unity.ps1`，本目录指定 `compile-review-final.log`：真实 Unity 编译通过，退出 0。之后的 GPU 测试也编译了最终源码和测试。
3. `tools/codex/run-unity-tests.ps1 -Platform EditMode ... -TestFilter 'TutorialManagerTests;TutorialStageTests'`，指定 `edit-review-final.xml/.log`：63/63 通过、0 失败/跳过，正常退出。正文压缩后的再次验证 `edit-accepted.xml/.log` 同为 63/63，但退出阶段超时 180 秒；只结束包装器启动的进程，不记为正常退出。最后的长期目标标题/叙述字面量变更由下一项真实路线复验覆盖。
4. GPU 全类测试：`KingdomStagedOnboardingPlayModeTests;KingdomOnboardingPlayModeTests`。`play-gpu-final.xml/.log/.exit` 为首次 26/28，2 条因正文 112.95 高于 authored 面板 108 而失败；保留失败证据。压缩缺料说明和完成反馈后，`play-gpu-verified.xml/.log/.exit`：28/28，0 失败/跳过，Unity 退出 0，RTX 4060 实际渲染。
5. 最终长期目标改为可执行标题后，运行本目录 `run-gpu-verification.ps1`：`FirstSession_RealButtonsAndNaturalProductionReachStoneAge`，`play-gpu-route.xml/.log/.exit`：1/1 通过、0 失败/跳过，Unity 退出 0。真实按钮、自然产出、ManualTick 达 StoneAge，4484.8677 模拟秒；没有赠送资源，不代表真人游玩时间或节奏验收。
6. 已检查 GPU 截图：`screenshots-suite-passed/` 中首屏、木屋缺料、木屋完成、农业等待、农场等待和跃迁画面；`screenshots/` 保留最终完整路线画面。测试实际测量正文 preferredHeight 不超过固定面板；没有放宽断言或放大面板。初次截图留在 `screenshots-first-run/`，不作最终通过证据。
7. 最终 GPU 原始日志未发现 C# 错误、NullReferenceException、MissingReferenceException 或字体缺字警告。原始日志的无 AudioListener 提示保留；本次未改音频。`git diff --check` 通过。

## 收尾与边界

- 测试存档隔离，最终 Temp 目录不存在、无 Unity 进程；没有创建临时工程/worktree、操作真实存档或 commit/push。
- SIMSUN SDF 精确恢复运行前内容，SHA256 为 `15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71`；临时备份删除并核验，记录见 `font-review-restored.sha256`。
- 未运行全项目测试；无真人留存/吸引力结论。没有调整经济定义或首局长等待，因此不声称已解决节奏问题。教程路径、动态文案、输入释放和显示适配已通过上述相关验证。

## 2026-11-02 续接：等待可读性与成果停留

- 沿用 `D:/GitHub/Kingdom` 当前工作树，没有创建新分支或临时项目。保留开始时已有的全部教程资产/源码/测试改动和 `.workbuddy-ai/memory/`，没有 commit/push。
- 读取主入口、UI/测试规范和本机 `ponytail/5.3.3/skills/ponytail/SKILL.md`（full正文），独立子代理只读审查等待与成果交互并复核最终增量。
- `TutorialManager.FirstSession.cs`：农场/工坊待产提示加入已有真实 `Benefit`；零劳力时说明等待居民入住，否则指向建筑页的产出/停工原因。研究等待补充研究/时代页查看位置。农场的紧接目标缩为“研究采石”，工坊紧接目标缩为“研究定居聚落”，适配原572×108正文，不增加面板或新任务系统。
- `KingdomUIRoot.LiveRefresh.cs`：成果停留1.5→4秒；`KingdomUIRoot.Onboarding.cs`复用已有Next提前收起，按实际显示的成果模式判断，只清成果timer，不确认下一课程。等待依旧隐藏Next并释放输入门禁。
- 新增1条EditMode（无居民农场停工原因及入住后推进），2条PlayMode（成果可读/真实Next关闭且不确认下一课；跨页待产真实收益、页面位置及正文高度）。既有路线夹具以真实点击收起成果再继续操作，保留等待Next=false和gate释放断言。
- 修复前 `edit-production-before.xml/.log`：1/1按预期失败，缺少入住说明；Unity退出2，非通过。第一次GPU `play-gpu-continuation.xml` 27/30（新成果按钮与旧夹具冲突）；第二次 `play-gpu-continuation-final.xml` 28/30（待产三行正文112.95>108）均保留。修复夹具交互并压缩文案，没有放宽断言。
- 最终GPU `play-gpu-continuation-accepted.xml/.log/.exit`：30/30，0失败/跳过，Unity退出0，RTX4060渲染；正文高度检查通过。真实按钮、自然资源、ManualTick完成StoneAge，4484.8677模拟秒；不是真人游玩时间。最终原始日志未发现C#编译错误、Null/MissingReference或缺字警告。
- 最终EditMode `edit-continuation-final.xml`：64/64，0失败/跳过。Unity退出状态与字体恢复将在收尾记录补充；不将XML写出等同正常退出。
- 本轮没有经济定义变更，不扩展策略搜索或自动调参。首局材料长等待仍未改善，用户“更愿意继续玩”的完整目标不能用教程测试通过代替。后续具体动作：从真实路线定居聚落约3390秒材料等待定位扩产教学/回本，再按经济技能前后闭包、诊断和Unity门槛推进；没有把经济节奏永久排除在用户原始目标之外。

### 续接收尾确认

- 最终EditMode用例64/64通过、0失败/跳过，但XML写出后退出停滞；180秒包装超时，仅结束自己启动的Unity PID5812。包装结果 `Failed(Timeout)`，不能称为正常退出。较早同轮 `edit-continuation.xml` 为64/64并正常退出，最终正文缩短增量另由上述GPU30/30覆盖；如需严格复现仍以最终XML和原始日志为准。
- 字体已恢复运行前SHA256 `15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71`，核对成功，临时备份已删除且不存在。无Unity进程、无Temp/StagedOnboarding目录；未创建临时工程。
- 最终输入指纹 `data/experience-20261102/continuation-verified-inputs.json`；`git diff --check`通过。本轮已执行真实Unity编译（EditMode/GPU测试启动重新编译当前源码/测试），GPU全部通过并正常退出0。未运行全项目测试；未验证真人留存，首局定居长等待仍是下一具体工作项。


## 2026-11-02 续接：首局扩产教学与跃迁详情刷新

- 沿用现有 D:/GitHub/Kingdom 工作树；保留已有修改，没有创建项目副本、worktree、分支或提交。
- TutorialManager.FirstSession.cs：first-transition 缺研究材料时检查已安装来源的产能盈余，建立尚缺的材料来源；现货足够或研究已付清时直接保留研究。已有生产力与空余住房容纳的潜在生产力不足以支撑下一来源时，先引导木屋，明确居民入住后才增加生产力。木屋缺木时保持等待，防止木屋与伐木场互相递归。
- 安装产能比较包含永久研究倍率，不使用临时幸福、事件或停工效率来决定反复加建。已有基础盈余但当前净产出为零时只提示到建筑页检查劳力/停工；避免居民尚未入住时连续要求加建。已有来源基础产能不足以覆盖加工消耗时，仍能引导补上游。普通建筑推荐由“检查并启用”改为真实的建造与查看产出操作。没有修改经济资产、GUID、存档字段或版本。
- 注意纠正中途诊断：单采石场的原始1.8不能作为实际供给，采石与石料加工研究具有永久倍率。最终自然路线没有强制第二座采石场，不能宣称实际存在石料倒挂。
- KingdomUIRoot.LiveRefresh.cs：时代页刷新时，仅将已完成、且不高于当前时代的跃迁详情切换到下一时代目标；其他研究选择保留。切换同步更新实际选中研究及按钮对象，并重置新详情滚动位置。解决中央“迈向中古时代”、右侧仍为已完成“迈入石器时代”的截图问题。
- Editor新增4条覆盖正收入首来源、住房容量后回来源、加工上游不足、零效率已有产能不重复扩产。正收入首来源/住房2条修前2/2失败；正收入上游修前1/1失败。上游最初夹具非正收入已能触发现有逻辑，随后修正夹具；中间一次测试使用internal API导致编译失败，已改用公开State。
- 最终EditMode：edit-expansion-complete.xml/.log/-errors.txt，68/68通过，0失败/跳过，包装正常Passed。中间edit-expansion-final为旧输入66/67并退出超时；重叠启动edit-expansion-accepted触发项目占用而无XML，不作为通过证据。后续等待原包装仅结束其自有PID19692后再串行验证；未结束用户Editor。
- 最终GPU PlayMode：play-gpu-onboarding-final.xml/.log/.exit，31/31通过，0失败/跳过，Unity退出0。含真实按钮、无资源赠送、自然生产首局路线，以及新增跃迁详情回归。针对详情回归修前1/1失败、修后1/1通过。测试检查引导正文符合原有面板高度与等待输入释放。
- 首次跃迁自然路线由4484.86770353102降至2939.60853572061模拟秒，约减少34.45%；新增伐木场、黏土坑、纤维采集营地及住房支持。此为ManualTick真实Unity路线，不是玩家实时时长或留存效果。中间首座扩产路线2939.6151秒同样到达StoneAge。
- 修改前后静态闭包closure-expansion-before.md/after.md均可达：145研究、92工坊、79建筑。未变经济定义。离线诊断前后均NETSDK1004，缺tools/NewEconomySimulator/obj/project.assets.json；未restore或安装，不能称该诊断通过。
- 最终GPU日志未发现C#编译错误、Null/MissingReference或缺字警告。本轮已执行真实Unity编译；未运行全项目测试。截图保存screenshots/；上一轮截图已可恢复保留在screenshots-before-expansion、screenshots-first-expansion、screenshots-before-era-detail-fix。用户发来的旧详情截图是问题证据；不把截图文件存在等同人工视觉检查。
- 字体已精确恢复到运行前SHA256 15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71。删除本轮32MB临时备份的命令被执行策略拒绝，未换方式删除；保留data/experience-20261102/expansion-font.backup。无Unity进程、无Temp/StagedOnboarding临时存档。
- 已改善首局操作、收益解释、成果可读性、材料扩产与时代交接；没有证据宣称真人留存或“沉迷”效果。若继续评估首局节奏，当前剩余聚落材料积累应基于上述真实输入和日志，不重建基础系统或扩展模拟器策略搜索。

## 2026-11-02 续接：材料等待按实际瓶颈提示

- 原自然路线日志显示：定居聚落先提示原木约464秒，原木足够后才提示石块还需约1014秒。共享 FindMissingResearchResource 原来按定义顺序取首个缺料，不能代表材料备齐时间。
- 当前改为优先没有正净产出的缺料，否则选择（需求减已付款减库存）/当前净产出最大的缺料。继续使用当前幸福奖励后的实际速率，按刷新更新估计；不承诺固定倒计时。零净产出且已建来源时，明确材料名称并指向建筑页劳力/停工原因。没有修改生产、研究成本或经济定义。
- 新增参数化回归2例：较慢石块不能被较快原木遮蔽；停产石块不能被原木倒计时遮蔽；石块备齐后回到尚缺原木。修前 edit-material-eta-before.xml 为0/2，预期失败；修后 edit-material-eta-final.xml 为70/70，包装正常退出0。
- GPU play-gpu-material-eta-final.xml/.log/.exit：31/31，0失败/跳过，Unity退出0；实际自然路线从定居等待开始即显示石块瓶颈。原面板正文高度与真实点击/等待释放检查保持通过；未发现C#编译错误、Null/MissingReference或匹配的缺字警告。本轮真实Unity编译已执行；没有重复全项目测试。
- 截图前版本可恢复保留于 screenshots-before-material-eta，新截图仍为 screenshots/。不将截图文件存在当人工视觉检查。字体恢复原SHA256 15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71，无字体资产diff；既有backup继续保留，不重试被拒绝删除。
- 沿用当前工作树，未新增项目/分支/提交，保留既有工作。此次消除误导等待预期，不声称减少实际材料等待或证明真人留存。首局仍有材料积累时间，真实留存效果没有实测证据。

## 2026-11-02 续接：总览缺料可直达来源

- 总览“当前条件”在明确写出“还缺<资源>”或“<资源>来源已建”的阶段，将该资源名显示为链接；点击打开该资源详情，查看现有产出、消耗、净产出及生产建筑来源。普通时代/前置条件文案不做模糊资源名匹配。
- KingdomUIRoot.ReviewActions.cs 复用原有链接导航并新增 resource 目标分支；不写入游戏状态。测试将真实首局路线中的材料阻碍名称、链接目标和打开后的资源详情关联起来。
- 修复旧测试中过期的“建造条件”断言为当前界面标题“当前条件”。初次GPU运行在编译阶段发现新测试类型引用未限定；保留失败日志 play-gpu-overview-material-final.log，修正后 play-gpu-overview-material-final2.xml/.log/.exit 为 33/33、0失败/跳过，Unity退出0。覆盖新自然路线链接、既有UI只读引导、资源来源链接测试及引导组回归。
- 本轮真实Unity编译由上述PlayMode运行执行。git diff --check通过。字体已从既有备份恢复，原SHA256为 15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71；备份保留。收尾时确认无Unity进程。
- 仍不将模拟路线/自动化测试宣称为真人留存结论；没有改动经济定义或模拟器策略。

## 2026-11-02 收尾：材料积累期间给出可执行选择

- 研究尚未排队且缺料具有正净产出时，总览建议“查看缺料来源；保存后可暂离，回来查看离线进度。”沿用已有离线结算与保存机制；停产时继续优先提示恢复生产，已排队或研究中的操作说明保持对应状态。固定引导浮层未额外增加离线说明，避免挤占正文。
- TutorialStageTests.ResearchWaitIdentifiesTheLastMaterialToBecomeReady 两个分支同时覆盖正常积累与停产：正常积累包含来源/暂离选择，停产不显示等待估时或暂离建议。
- 最终 EditMode：edit-offline-choice-final.xml/.log/-errors.txt，70/70通过，0失败/跳过，包装正常Passed。最终GPU PlayMode：play-gpu-offline-choice-final.xml/.log/.exit，33/33通过，0失败/跳过，Unity退出0。已执行真实Unity编译；扫描这两份原始日志未发现C#编译错误、NullReferenceException、MissingReferenceException或匹配的缺字警告。没有运行全项目测试。
- 从既有备份恢复测试生成的SIMSUN SDF.asset，SHA256核对为15A052DF327A1E9D034458C890CCADCBB5F8723008D76C39D28213BF683C9A71，备份保留；收尾确认无Unity进程、无Temp/StagedOnboarding临时存档目录。沿用D:/GitHub/Kingdom现有工作树，未创建临时工程或新worktree。
- 最终针对性检查：git diff --check通过。经济定义未变；离线模拟器仍因缺project.assets.json未运行成功，未执行restore。首局自然路线到StoneAge的模拟耗时改善有测试证据，真人留存效果尚无实测数据。