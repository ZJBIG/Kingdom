# 剩余改动分类与清理

## 2026-10-22 最新：继续减小APK并上传Git

- 用户新目标：继续压缩APK大小，并把游戏上传git；明确授权当前游戏工程提交/推送，保留origin=https://github.com/ZJBIG/Kingdom.git和当前main。三路只读智能体审查音频方案、待提交工作树/未推送历史、SDK网络阻塞；没有发布到应用商店或上传APK到源码仓库。
- Android音频：56首由Vorbis quality0.5、保留源采样率，改为quality0.35、OverrideSampleRate=32000；默认平台仍quality0.6，Streaming/立体声/源OGG保留。PMusicAudioImporter为唯一参数来源，AndroidArtworkOptimization校验同步使用常量并检查采样率。32kHz去除16kHz以上频段，是有损压缩，未作耳机试听。LZ4HC及当前图片保持。
- 实测57音乐bundle由128,604,524变106,557,403字节（约122.65→101.62MiB）；APK由240,632,223变218,585,088字节（229.48→208.46MiB），减少22,047,135字节/9.162%。当前APK SHA256=f8141f177dea99c354e86e58070f2dbcaafc405c6884ed6afd5fa335ba2c85ce。
- build-audio32k-success.log确认真实Unity Android构建Success，errors0/warnings13，Unity正常退出0、wrapper退出0。1894输入文件构建前后SHA256完全一致。当前APK仍92条目、57bundle、4个ARM64原生库，无原OGG/源码/测试文件名/调试边车；签名验证通过，仍Android Debug证书，未配置商店正式签名。未运行测试套件或设备体验验收。
- 网络阻塞事实：SDKManager --list显式Proxy.NO_PROXY绕开JVM通用proxy，前两次代理方式无效；直连失败导致一次SDK列表构建失败，两次主动结束的进程均为自己启动的受阻构建。原生 --proxy=http --proxy_host=127.0.0.1 --proxy_port=7890 清单读取exit0。最终仅在本轮临时给Unity内置sdkmanager.bat追加原生代理参数，try/finally按原始字节还原；原始/还原SHA256均14866E8B3EDE1D90B633042748254BD264E0678F68B9390540E839588DBB47F4，未安装/升级SDK或修改永久机器/Git配置。Sdk脚本还原证据在sdk-script-restored.json；后续相同网络环境构建须使用SDK自身代理选项，单独JAVA_TOOL_OPTIONS不足且会被Unity清除。
- Git：发现2026-10-20遗留空index.lock，无Git进程、无reparse后用临时PS1精确清除；脚本已在收尾清理。现存待提交文件无>50MiB，原有2条未推送提交的460blob最大706270字节，敏感命名/高置信密钥扫描无命中。远端main为本地祖先；上传包含原有两条未推送提交和当前正式工程变更。APK、Library、Logs、UserSettings、本机配置与临时备份不提交。.gitignore新增.codex/tmp及生成Android.meta精确忽略。
- 最终PS1删除4549文件、4995.31MiB（4.88GiB），包括本轮Bee/缓存、DoNotShip、源输入备份、SDK临时备份/脚本、失败重试日志和冗余SDK清单；PhysicalPostScanRemaining=0，再预览0文件。脚本仅检查实际存在的目标，避免已暂存且已删除的历史清理项阻断无关缓存清理；存活目标的dirty/staged/reparse/进程保护保留。Builds递归仅剩当前APK，清理后hash一致。
- 当前减包证据：data/apk-clean-build-20261022/apk-audio32k-audit.json、audio32k-input-files.json、audio32k-exit.txt、manifest-audio32k.txt、signature-audio32k.txt、sdk-script-restored.json、cleanup-audio32k.txt及被忽略的build-audio32k-success.log。旧apk-audit/input记录保留为本轮229.48MiB比较基线。Git最终push和远端HEAD核对由本轮交付完成。


## 2026-10-22 最新：实际Android构建、包内检查与最终清洁

- 延续用户“多智能体全盘清洁、准备发布、去除无用build文件”授权；三路只读智能体复核仓库残留、资源重复打包与工具链/签名，主代理检查实际结果并执行PS1。未运行测试套件、未发布/上传、未操作真实存档、未commit/push。
- 实际运行 tools/codex/build-android.ps1，Unity 2022.3.62f3c1 Android IL2CPP/ARM64构建成功；Unity正常退出0，wrapper退出0，BuildReport errors=0、warnings=15。依赖首次下载耗时，Gradle最终成功；无需安装Unity包或改机器永久配置。15条构建警告为3项Editor spritesheet旧API警告及12个应用图标压缩格式警告；完整日志保留，不声称零警告。
- 最终输出 Builds/Android/Kingdom.apk，240,632,223字节（229.48 MiB），SHA256=4dfc9d22e3e284fcf7e7195ad254b82e4d66774d80efe3da994e761730fe69f4。包名com.ZJ.Kingdom，版本0.5/code1，minSDK22/targetSDK35，仅arm64-v8a，非Development且无debuggable/testOnly标记。apksigner验证退出0，v1/v2通过；证书CN=Android Debug，正式发布签名尚未配置，本APK不是商店正式签名包。
- APK ZIP实查92条目、57个Addressables bundle（56音乐+1目录），128,604,524字节；未发现原始OGG、DoNotShip、源码、脚本、日志、PDB或测试文件名。原生库4个且仅ARM64；boot.config wait-for-native-debugger=0，无自动连接Profiler标记。音乐122.65 MiB、Unity数据ZIP约89.55 MiB、原生库ZIP约16.87 MiB，清缓存不会继续缩小这些正式包内容。
- 构建前1896输入文件SHA256快照与构建后全部一致，无需还原字体或用户修改。临时快照ZIP核验无用途后删除；保留新生成Addressables内容更新基线bin/meta（约18 KiB，不进入APK）。
- 同一清理PS1移除整目录Builds目标，改为精确Kingdom_BurstDebugInformation_DoNotShip；增加Temp、Library/com.unity.addressables与本轮快照ZIP目标。保留原根路径/reparse/dirty/staged/进程/.patch保护，Unity正常退出后执行-Apply：4560文件、5212.42 MiB（5.09 GiB），PhysicalPostScanRemaining=0。包括Bee约4632.57 MiB、BuildCache245.82 MiB、音乐构建副本123.06 MiB、PlayerDataCache89 MiB、Burst88.70 MiB及其余日志/中间数据。
- 最终独立复核发现9月7日Windows平台Addressables旧构建状态（当前目标Android）。确认仅bin/meta、目录GUID无消费者且无本地修改后，同一PS1精确追加删除Windows目录与目录meta，共3文件19,188字节；再次PhysicalPostScanRemaining=0、预览0。Android新基线和生成的link.xml保留，后者供Addressables类型裁剪；无StreamingAssets/旧发布包。该3文件为审核后的有意删除，不属于Unity意外序列化。
- 复跑PS1预览为0文件/0 MiB；Builds/Android递归仅剩Kingdom.apk，清理后SHA256不变。正式277PNG、56OGG、字体、GUID、配置及Library导入/包缓存保留。源脚本diff --check通过。
- 本轮证据统一 data/apk-clean-build-20261022/：build.log、wrapper-exit.txt、apk-audit.json、manifest.txt、signature.txt、input-files.json、input-check.json、yaml-reference-audit.txt、cleanup.txt。未执行设备/游戏交互验收，不将构建成功称游戏运行验收。


## 2026-10-22 最新：三智能体发布前全盘清洁

- 用户要求多智能体全盘清洁、准备发布并去除无用build文件。三路只读审查分别覆盖构建/打包、正式美术音频字体依赖、工具文档/仓库残留；主代理复核候选GUID消费者、初始dirty、当前PS1并执行删除。未创建新Unity项目、未构建/发布APK、未运行测试。
- 本轮同一PS1 -Apply删除25文件、4,703,069字节（4.49 MiB），PhysicalPostScanRemaining=0：18个Unity模板/TMP旧文档/示例材质文件4,657,036字节；6个旧WorkBuddy项目笔记29,683字节；已完成文档恢复ZIP16,350字节。空.codex/tmp、.codex/archive一并移除。
- 资产清理精确内容：TextMesh Pro/Documentation与meta；UI Toolkit的PanelSettings/default theme及目录meta；Settings/Lit2DSceneTemplate.scenetemplate、仅被它引用的Scenes/URP2DSceneTemplate.unity及meta；Fonts & Materials下LiberationSans SDF - Drop Shadow与Outline两个示例材质及meta。删除前18文件11GUID在保留Assets/ProjectSettings/Packages中无消费者，全部原本Git干净。
- .workbuddy-ai/memory六笔记Git干净、无活动规范消费者，包含已失效的batchmode不可用/旧存档v9/退休路径，删除；保留.workbuddy-ai/skills/kingdom-project-dev/SKILL.md。用户级Codex memories未修改。先前段落保留ZIP的决定已被本轮完成审查后的删除取代，旧路径只作历史记录。
- 保留277正式PNG/56首OGG、SIMSUN SDF/TTC、运行时Noto、TMP隐式Resources.Load的LiberationSans SDF/Fallback、EmojiOne及署名、Shader fallback与URP配置。ReviewControls是保存/维修/队列等正式操作，保留；开发工具、测试和未完成补丁均有消费者，未按名称或时间清除。
- 发布入口静态核对：KingdomBuild仅StrictMode|CompressWithLz4HC，无Development/AllowDebugging/ConnectProfiler/IncludeTestAssemblies；唯一启用场景SampleScene，ARM64/IL2CPP/ASTC；音乐Addressables为正式本地资源且随Player重建。PlayMode测试具TestAssemblies与UNITY_INCLUDE_TESTS，Editor工具不打入普通Player。
- Assets无StreamingAssets/Plugins，无APK/AAB/DLL/AAR/JAR/SO/ZIP/PDB残留；根Build/Builds/Logs/TestResults及模拟器bin/obj已不存在。当前data只保留必要证据/来源/独有.patch，docs/outputs有未完成事项或独有合同，不进入普通APK。Library当前导入/包缓存与UserSettings保留。
- 4.49 MiB是工程目录减少量，不是APK实测减少。模板/PDF原本不进正常游戏包；两个Resources示例材质才涉及强制资源。未执行真实 Unity 编译。本轮引用、正式图清单及构建残留均静态复核，不声称已完成游戏运行/最终APK验收。

## 2026-10-22 最新：用户授权代理执行PowerShell与全目录清理

- 用户最新指令覆盖此前仅交付PS1的边界：明确要求代理执行PowerShell，并自主判断必要与废旧文件。已检查Unity/编译进程为空、工作树及精确目标；所有删除由tools/codex/cleanup-art-optimization-temp.ps1 -Apply执行。
- 第一批执行现有PS1：仅剩android-size-before512.zip，1文件220,816,366字节（210.59 MiB），PhysicalPostScanRemaining=0。此前其他临时目标已由用户清理。
- 第二批扩展同一PS1：1535文件、187.84 MiB，PhysicalPostScanRemaining=0。清除Library/Bee约105.05 MiB、BurstCache约78.66 MiB、9月LastBuild报告及空构建缓存目录；Logs与SafeNet Sentinel下仅含的2份旧日志；模拟器bin/obj、空Builds/TestResults/package-recovery。保留Library/Artifacts与PackageCache，避免重新导入当前Android纹理/下载包；保留UserSettings个人布局。
- 删除已完成的一次性Assets/Editor/KingdomOnboardingAuthoring.cs及meta：只有菜单入口，当前Prefab已有OnboardingOverlay且作者明确拒绝重复执行，源文件无本地改动、无代码或序列化GUID消费者。保留已创作Prefab、OnboardingInputGate、教程与所有有效测试/长期维护作者工具。
- 删除旧data/art-optimization-20261022全部4文件（768阶段，已由512 ASTC当前证据替代）；data/metal-consolidation-20261014/archive-original-consolidation的86个合并前定义副本；workshop-art旧compile-preview.log/import-check.txt/unity-exit.txt。旧段落相关路径仅记录历史行为，不再是当前入口。
- 保留data/android-size-20261022现有Android编译/导入/GPU证据、研究closure-final、92工坊manifest/提示词批次、金属art-source、独有.patch、4个活动handoff，以及仍有未实施候选/核算方法/待核清调查的docs/outputs。剩余工具经消费者复核保留，不以日期或Authoring/test名称直接删除。
- PS1增加源文件本地修改/暂存保护、.patch拒绝、空目录预期、许可证目录仅日志检查；原有仓库内绝对路径/祖先与子项reparse拒绝/Unity占用检查和物理复扫保留。两批合计1536文件，约398.43 MiB；无真实存档/包安装/commit/push/终止进程。
- 本轮不运行Unity或测试，避免清掉缓存后立即重新生成。未执行真实 Unity 编译。上一轮Android退出0证据保留，清理后仅做源图/引用/清单及无残留静态检查；不把旧编译称本轮编译。

## 2026-10-22 最新执行边界与Android优化

用户已自行启动PS1清理；代理此后不直接删除废旧文件。Android优化将181主图768→512并应用210纹理ASTC、Android音乐quality0.5与构建压缩设置。真实Android导入/编译/GPU预览完成且Unity退出0；全PNG源162.32 MiB，210纹理ASTC payload计算49.91 MiB，未构建APK。完整字段、验证及证据见building-icons-handoff最新节与data/android-size-20261022/。

新回退备份.codex/tmp/android-size-before512.zip已加入tools/codex/cleanup-art-optimization-temp.ps1，由用户确认后执行；本轮未运行该PS1，未直接清理文件。可复用AndroidArtworkOptimization.cs保留为当前维护工具，不属于废弃临时作者脚本。

用户授权检查交付后的剩余改动，清理无用文件。基线 main `b7bf4c08e6c8891c466cebe72547e9fd3740b730`，保留真实功能修复，不删除字体资产本体。

- 预览白名单 75 个目标、271 个文件、18,318,361 字节：仅未跟踪 tmp 诊断脚本/截图/沙盒、Python 缓存、3 份历史客户端笔记、旧 Android Addressables 构建状态及文件夹 meta。仓库已跟踪 tmp 文件保留。
- 所有目标先核验仓库内绝对路径、无 reparse point、无跟踪源码，随后移到 `.codex/archive/2026-10-09-repo-cleanup-204150/removed/`。`manifest.json` 记录逐项原路径/归档位置；75 项均核验原位置不存在、归档位置存在，未例行永久删除历史证据。
- `.workbuddy-ai/memory/MEMORY.md` 的既有改动及 `SIMSUN SDF.asset` 先复制到该归档 `modified/` 并核对内容一致，再还原 HEAD。字体差异仅新增18个动态字形/字符和atlas排布/像素，源TTC、GUID、动态配置均未变；个人旧笔记不作为当前规则交付。
- `.codex/config.toml` 是本机 Codex 设置，内容未改，仅在 `.git/info/exclude` 精确加入该路径，保持本地可用且不上传；未修改全局忽略或全局 Git 配置。
- 还原后 Git 内容 diff 为空；一次精确 `git add` 更新两个文件的行尾/索引状态，确认 staged diff 仍为空。主工作树全部原剩余 dirty/untracked 项已退出活动变更清单。
- `D:/GitHub/Kingdom-source-audit` 的未提交 BuildingManager/KingdomLogicTests 两处保留：属于把失败建造的状态注册推迟到提交阶段的真实修复，不是垃圾。尚无真实测试证据，不随本次清理上传；后续先验证无效数量、领土/生产力/资源不足不新增状态/事件，以及成功建造登记、扣费和事件，然后再交付。

本轮仅文件分类、归档与恢复，没有改游戏行为，没有运行 Unity。未执行真实 Unity 编译。交付只包含本交接，归档和本机设置不上传。

## 2026-10-10 补充：清除已确认的一次性审计文件

按用户明确授权，从工作树移除以下 10 个已跟踪且干净的文件：`tmp/verify_e08.py`、`tmp/verify_e08b.py` 至 `tmp/verify_e08g.py`、`tmp/verify_p003c.py`、`tmp/audit_upgrade_continuity.py`、`code-audit-refactor-candidates.csv`。

- 8 个 `verify_*.py` 是 2026-09-15 的单次资产核验脚本；仓库内无当前引用。升级连续性临时审计脚本由 `tools/codex/audit-upgrade-continuity.py` 覆盖，且无当前引用。旧 CSV 仅在 `docs/audits/2026-09-10-full-readonly-refactor-scan.md` 的历史关系说明中提及；该报告确认旧条目已逐项复核并更新行号。
- 修改前逐项确认路径在仓库内、不是重解析点、Git 跟踪且无本地改动；修改后确认 10 个原路径均不存在，Git 仅显示这 10 个预期删除。
- 保留 2026-09-10/11 历史审计报告：`ToDoList.txt` 仍将其标记为尚未逐项核清的调查；保留 `tmp` 中插画报告生成脚本和 `outputs/` 专题文档，因为仍有创作/历史用途或被正式索引。
- 本轮未运行脚本、Unity 编译或测试。删除的跟踪文件可从 Git 历史恢复；未删本机 `TestResults/` 和 `Logs/`。

## 2026-10-10 补充：临时验证项目收尾与约束

用户授权删除目前无用的临时项目，并为代理创建此类项目增加收尾约束。主目录既有暂存/未提交内容保留；不迁入游戏修复、不提交或推送。

- 根 `AGENTS.md` 的工作树条款新增：优先复用、必要时才创建隔离项目、登记目的/源版本/退出条件、不复制缓存；创建者负责同轮迁回成果、核对.meta/GUID、保留必要证据与独有补丁、注销worktree并核验无残留。阻塞须记录，不默认长期保留第二主项目。
- 归档：`.codex/archive/2026-10-10-temporary-project-cleanup/`。保留规则与本交接修改前快照；三个临时项目的独有改动、二进制patch（worktree）、日志/XML及review-evidence均已复制并逐项SHA256核验，清单在 `manifest.json`。ShaderGraph设置因首次Git差异枚举漏列，另存 `relic-verification/files/ProjectSettings/ShaderGraphSettings.asset` 并单独核验。
- 已完成：`C:/Users/19603/.codex/worktrees/relic-verification/Kingdom` 的功能代码与最终G3提交一致；归档差异后恢复其已保存文件并以 `git worktree remove` 正常移除，不使用force。确认目录不存在、注册项不存在，清理约2.70GB。
- 未完成：后续移除 `D:/GitHub/Kingdom-source-audit` 与 `D:/GitHub/Kingdom-review1009` 的命令被执行策略拒绝（blocked by policy，未给出更具体原因），按规则停止、不改方式绕过。两目录仍存在；source-audit两处未提交修复仍原样保留，且已存patch和源码副本。下一动作是在执行策略允许时核验归档后恢复已保存的两文件、正常注销source-audit，再移除已归档差异的review1009；不需再调查其用途。
- 验证：只读project probe `--check-skill` exit0，errors为空；ProbeHarnessTests 9/9通过；GuidanceContractTests failfast在第10项 `test_powershell_required_paths_exist` 失败，正则未处理当前CRLF行尾，抽取路径0项（要求>20），本轮未修改该测试或validate-guidance.ps1，不弱化断言。根规则diff检查通过。
- 未执行真实 Unity 编译。外部临时项目删除被阻断不是游戏验证失败；本轮清理未全部完成。

## 2026-10-10 补充：项目清理建议执行

按 `tmp/cleanup-audit-20261010/清理建议.md` 完成低风险缓存与失效产物整理；保留工作树内既有并行改动，不提交。

- 未跟踪 `Logs/`、过期 `TestResults/`、`UserSettings/`、`.vs/`、Android Burst 临时文件、模拟器 `bin/obj` 和 Unity 测试残留场景移至仓库外 `D:/Kingdom-cleanup-backup-20261010/`。TestResults 仅恢复建议中保留的批次，并恢复 `Latest-Test-Errors.txt` 当前证据入口；旧批次保存在外部备份。
- `output/fb89034f-45d8-4194-856b-175b429cc8d2/`、两个失效 story-art 生成脚本、无引用 contact sheet、四个无活动流程引用的失效/一次性工具脚本移至 `.codex/archive/2026-10-10-project-cleanup/`。归档包含 `manifest.json`（路径、长度、SHA256）。`output/` 空目录已移除。
- 移除空 `Kingdom/` 与三个空 ultra-r2 临时目录。建议中九个已删未落实索引状态的 `tmp/verify_*` 与 `tmp/audit_upgrade_continuity.py` 仍显示为预期删除；不执行暂存操作。原 handoff 已记录的 `code-audit-refactor-candidates.csv` 删除也保留为既有变更。
- 代码复核建议暂不删 C# 候选：Singleton 的公开 Save/Load 虚方法及多个 override 有外部绑定风险；若干 Editor/测试入口需进一步确认。未修改代码，也未运行离线编译。
- 历史审计/闭环 handoff 未归档：活动文档仍引用至少两份 handoff，且 ToDoList 仍把旧审计列为未核清；alien-war-first-version 涉及玩法语义，继续保留。孤儿 PNG、运行时 UI 构建、测试反射和定义字段未处理。
- 验证：核对目标缓存目录不再位于活动项目路径；TestResults 保留批次仍在，`Latest-Test-Errors.txt` 已恢复；跟踪 `output/fb...` 文件只显示预期删除。未执行真实 Unity 编译；无游戏行为修改。


## 2026-10-15 早期运行时代码死接口清理

用户要求重点审查项目早期代码，清理冗余/死代码。保留全部既有工作树改动，删除11源文件共105行已确认无消费者或冗余的实现：Singleton和4个Manager的10个Save/Load旧接口、音乐3个旧Play重载和无人读取的分类计数缓存、StartLoadedTrack恒真参数/无用返回值、SaveManager.MarkDirty、人口3个废弃常量、2个私有UI状态文案函数、Sector两个未使用Editor辅助方法。删除Food仅测试使用的无容量重载与1e1000000假上限，原测试改显式容量500并保持运算/上限/负delta校验。没有ID/GUID/资产/存档/公式/数值变化，未删除有效Manager、定义或数学库。

引用证据和逐项文件位于data/early-code-cleanup-20261015/validation.md。Assets源码及UnityEvent退休引用复扫通过，独立只读复核未发现遗漏消费者。真实Unity编译完成：EditMode核心KingdomLogicTests160/160、音效3/4；失败为既有Root缺SfxMute按钮断言，保持原断言/资产不变，EditMode关闭150秒超时由自有启动器结束。PlayMode ReviewInteraction13/13通过，跨页面材质测试第89行文字raycastTarget失败；原断言保留。将原测试音乐段拆成独立UnityTest（不删断言），music-playmode1/1通过，验证实际Addressables播放/暂停/恢复，Unity正常退出0。未运行全仓，不将两项现有UI测试失败隐藏为全绿；本轮清理已完成，已知UI配置问题单列报告。

测试后字体恢复B9BD028E9AFC401532E35CE5A76782C40C7426FCEF7B0B94E12E53085CCBA5DE，临时备份物理删除，无Unity或测试存档fixture残留。无commit/push、包安装、临时Unity项目或真实存档操作。


## 2026-10-16 两项UI留项修复完成

更正上一节：跨页面材质失败实际在下划线断言，非raycastTarget。生产树作者工具已明确取消下划线，星区名称仍要求下划线；测试按各自页面精确验证并保留原射线断言。

Root Prefab补入SfxMute，复用现有按钮样式，Controls内中心(380,48)、190x64、字体30，置于背景之后绘制。KingdomCollectionAuthoring同步完整DataRows/MusicSurface路径。绑定现有静音功能，文案随状态切换音效静音/恢复音效，点击即刷新，保留集中状态同步。测试验证真实点击切换、偏好恢复、与播放按钮/音效滑条不重叠、文字不截断及绘制顺序，保留音乐实际播放/暂停/恢复验证。

真实Unity编译完成，ui-final-editmode4/4、最终ui-visible-playmode2/2通过、0失败跳过；已查看1760x800真实GPU截图。两次Unity均写出XML后关闭120秒超时，由启动器结束自有进程，不称runner正常退出。最终日志无CS/Null/Missing/UnassignedReference/Assertion错误，相关diff检查通过。详细证据data/early-code-cleanup-20261015/validation.md；截图data/scroll-frames-20261013/pages/music-controls-1760x800.png。

字体恢复起始字节B9BD028E9AFC401532E35CE5A76782C40C7426FCEF7B0B94E12E53085CCBA5DE，临时备份物理删除，无Unity进程/测试存档fixture残留。没有经济数值/ID/GUID/存档变化，无commit/push、临时项目或真实存档操作。上轮两项留项完成，未执行全仓测试。

## 2026-10-22：完成确认及过时产物清理

- 用户明确要求再次确认完成并删除临时文件、过时报告/文档（含测试结果）。本轮没有修改游戏代码或正式Assets，没有运行新测试或再次启动Unity。
- 清理前及清理后均核对：92工坊定义完整，92 PNG与meta存在、Sprite GUID绑定正确，max1024/mipmap/Trilinear设置完整；四处研究前置存在。保留当前compile-preview.log、unity-exit.txt（NormalExit=True; UnityExitCode=0）及closure-final.md（143研究、92工坊、79建筑全可达）。前轮未运行测试或实际等待节奏实测的边界保持。
- 精确清理263个目标、692个文件、1,132,190,847字节（1079.74 MiB）。包含3个10/21失败尝试归档、旧mount/actions/inset及首版星云输出、被拒白底图、180张逐帧PNG、初始fixture、一次性作者/同步/核对脚本、旧PID/中间日志、1760重复截图、前中版闭包和失败诊断。保留最终有效测试XML/log、必要最终预览/编译证据、工坊清单和提示词，以及各主题独有.patch。
- 移除3份已被取代文档：2026-09-18-doc-consistency-and-regression-handoff、2026-10-08-document-cleanup-handoff、docs/audits/2026-10-14-metal-consolidation.md。原文与3份修改文档的运行前快照在.codex/archive/2026-10-22-document-cleanup.zip（6份文档及SHA256清单，逐项核对ZIP读回字节一致）；不把该归档设为活动开发依赖。
- 局部更新ToDoList.txt、outputs/README.md当前工作与证据导航，以及docs/ui/collection-design.md中已失效的建筑图标、节点/Pin尺寸说明。保留9月仍未逐项核清的调查、独有专题/候选、当前规范与交接。
- 删除前逐目标核对绝对路径限于仓库、不涉及Assets、无reparse、无已有Git修改/暂存文件、无.patch；删除后PhysicalPostScanRemaining=0。TestResults原本为空；正式图片及.meta保持。新清理脚本在完成后从.codex/tmp移除。不清理仓库外生成图、用户真实存档、Unity缓存或用户Editor，不提交推送。
## 2026-10-22 深度清理及图片压缩收尾

- 用户后续明确：清理交付PS1，由用户执行，代理不直接清理。已提供tools/codex/cleanup-art-optimization-temp.ps1，仅默认预览，显式-Apply删除本轮10个精确临时目标；不执行该脚本。512/384/256与Android纹理压缩仅讨论方案，本轮未进一步缩图或修改导入/构建配置。
- 用户撤回空白背景反馈，无需重生成。整张图等比缩放和PNG压缩，保留边角及环境，不裁剪、不减色。
- 深度清理第一批64文件、122,239,478字节：3个过时作者/迁移脚本（含相关meta）、旧StoneAge/ClayPit重复图、9份完成交接、6个旧UI证据目录。保留实际加载的KingdomUIReviewControls.prefab、有效测试和独有.patch。
- 第二批163文件、26,683,280字节：75张无消费者的旧贴图及meta、空目录meta、6张被当前预览取代的工坊图集/精选/卡片截图。删除前核对Assets及ProjectSettings GUID、其他meta依赖和动态Resources加载路径。保留应用图标、MusicIcons、ResearchTree、Pin等使用中图像。两批共227文件、148,922,758字节，已物理复扫不存在；不含临时文件清理量。
- 79建筑、92工坊、10星区：1254方图至768方图；18剧情：1672×941至1280×720（整数尺寸取整）；金属图集1774×887至1024×512，7个精灵矩形同步缩放，GUID/spriteID/internalID保持。小图仅压缩，九宫格UI尺寸保持。
- 最初优化294张，其中49张后来确认无用并删除。最终保留245张优化PNG，200张缩图，源文件671,667,190→285,341,443字节（640.55→272.12 MiB，减少57.52%）。不是游戏包测量。当前images.json只列保留图；92工坊manifest更新当前SHA256和768尺寸。
- 真实Unity编译、294图导入、200纹理实际尺寸/GUID检查、79建筑/92工坊/33资源/10星区Sprite及图集边界检查完成，已目视GPU预览。证据：data/art-optimization-20261022/{compile-import.log,import-status.txt,imported-artwork.png,images.json}。Unity进程自然结束，操作系统退出码未捕获，不宣称退出码0。删除废图后全部引用再次静态复核通过，保留图尺寸/GUID/精灵ID、92工坊SHA256通过。
- 未运行测试套件或构建包，未修改玩法数值/稳定ID/真实存档。最终临时清理被自动审批审查拒绝（blocked by policy，未给具体原因），尚未移除Assets/Editor/ArtworkOptimizationAuthor.cs及meta、.codex/tmp/art-originals、art-review、optimize-art.py、prune-finished-work.ps1、art-optimization.json、art-unity-pid.txt、unused-art-candidates.json、final-cleanup-stats.json。下一动作是删除这些精确目标并物理复扫；不要误称临时清理已完成。旧段落已删除路径为历史行为，不是当前入口。

## 2026-10-22 C/D build residue cleanup
ExternalCleanupFiles=14; Bytes=9237357; Targets=10; Remaining=0; APKUnchanged=True; BranchPreserved=True
Removed exact reviewed external targets: C:\Users\19603\.codex\worktrees\relic-verification; C:\Users\19603\AppData\Local\Temp\kingdom-building-icons-review.jpg; C:\Users\19603\AppData\Local\Temp\Unity\Editor\Crashes\Crash_2026-10-21_103936201; C:\Users\19603\AppData\Local\Unity\Editor\Editor.log; C:\Users\19603\AppData\Local\Unity\Editor\Editor-prev.log; C:\Users\19603\AppData\Local\Temp\UnityCBMCrashes; C:\Users\19603\Documents\Codex\2026-10-20\new-chat\work\kingdom-full-status-after.txt; C:\Users\19603\Documents\Codex\2026-10-20\new-chat\work\kingdom-full-status-before.txt; C:\Users\19603\Documents\Codex\2026-10-20\new-chat\work\kingdom-status-before.txt; D:\Android\GradleHome\daemon\7.5.1. Pruned stale Kingdom-source-audit Git registration; retained branch and current v0.5 APK. Shared SDK/dependency caches, saves and generated image originals retained. No Unity compile or tests run.

Icon preview: outputs/app-icon/Kingdom-v0.5-story-reporter.png. Built-in image_gen; Story_BeyondTheSky_16_Main and Story_IndustrialAwakening_08_Main used as style references. Prompt: square full-bleed Story style illustration, five mousefolk surrounding a microphone reporter discussing rocket launch behind them, natural daytime, restrained warm colors, no text/neon. Supersedes rejected single mouse reporter preview. Existing application icon and APK unchanged; awaiting visual selection.

## 2026-10-22 v0.5 Story icon APK
User approved latest group mousefolk reporter image for APK. Replaced KingdomAppIcon.png with full-composition 512x512 RGB downsample of outputs/app-icon/Kingdom-v0.5-story-reporter.png; retained meta GUID and PlayerSettings binding. First full-resolution build succeeded but added 6.4 MiB; final 512 build succeeded, 219006004 bytes (208.86 MiB), errors=0 warnings=13, Unity normal exit 0. Version 0.5 ARM64; apksigner verified v1/v2 Android Debug certificate. SDK script restored byte-identically (sdk-restored.txt). Packaged 192px icon compared to source in audit.json. Evidence data/apk-icon-v05-20261022. No tests/device runtime run, no commit/push. PS1 cleanup removed 4538 files/4966.78 MiB of rebuild caches with residual0; temporary backup APK and wrapper removed through PowerShell.
