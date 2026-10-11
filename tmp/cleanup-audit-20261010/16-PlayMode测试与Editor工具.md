# 16 · PlayMode 测试与 Editor 工具区块审计

只读盘点。基准：当前磁盘文件（含未提交改动）。未运行 Unity、未跑测试、未改任何项目文件。
准则：`tmp/cleanup-audit-20261010/00-审查准则.md`。

---

## 1. 范围（真实数字）

| 路径 | 文件数 | 清单 |
|---|---|---|
| `Assets/Tests/PlayMode/` | 8 | PageScrollPosition、SectorBuilding、UnsafeAreaTicker、KingdomOnboarding、StoryIllustration、KingdomPlayModeTests、ReviewInteraction、Relic |
| `Assets/Editor/`（含子目录） | 16 | 根 8：KingdomPerfTestTools、KingdomBuild、PMusicAudioImporter、MusicAddressablesConfigurator、EconomyParitySnapshotExporter、RelicUIAuthoring、StoryIllustrationAuthoring、ReviewUIAuthoring；`Drawers/` 2；`Content/` 2；`Codex/` 3；`Migration/` 1 |
| 合计 | **24** | — |

`Assets/Editor/` 全部 16 个 `.cs` 均有同名 `.meta`；`Assets/Tests/PlayMode/` 8 个 `.cs` 均有 `.meta`（D5 无缺失）。全部无 `System.Reflection`/`BindingFlags`/`Activator`（唯一 `GetProperty` 命中是 `ResourceExpantaNumPairDrawer.cs:44` 的 Unity `GetPropertyHeight` 覆写，非反射）。全部无 `TODO/FIXME/HACK/OBSOLETE`；唯一 `#if` 是 `SolutionSync.cs:1` 的 `#if UNITY_EDITOR`（Editor 程序集恒真，冗余但无害）。

**工作树状态**（`git status --porcelain`）：`ReviewUIAuthoring.cs`、`ReviewInteractionPlayModeTests.cs` 为新增（A）；`KingdomPlayModeTests.cs`、`StoryIllustrationPlayModeTests.cs` 已改（M）；`RelicPlayModeTests.cs` 已暂存+工作区再改（MM）。按铁律 4 以磁盘为准。

**判定所依据的"当前引用"权威文档**：
- `.agents/skills/kingdom-project-dev/references/validation.md`（项目开发主入口的验证流程登记）
- `tools/README.md`、`docs/repository-map.md`、`docs/architecture/definition-database.md`、`AGENTS.md`
- `tools/codex/*.ps1`（唯一以 `-executeMethod` 调用 Editor 代码的入口）

`-executeMethod` 全仓仅 4 处调用（`grep` 命中）：`apply-definition-ids.ps1:15` → `DefinitionIdMigration.ApplyFromCommandLine`；`reserialize-definitions.ps1:15` → `DefinitionIdMigration.ReserializeFromCommandLine`；`sync-solution.ps1:15` → `SolutionSync.Run`；`build-android.ps1:43` → `KingdomBuild.BuildAndroid`。

---

## 2. 结论清单

### 2.1 建议归档

- [High] `Assets/Editor/Codex/EnsureManagerComponents.cs` — 维度 D9/D2；证据：全仓（排除 `Library/Logs/.codex/archive/tmp`）grep `EnsureManagerComponents` **只命中自身文件**；4 个 `-executeMethod` 入口均不调用它；无任何 docs/handoff 引用；`git log` 增于 2026-08-21、此后未改。功能是打开并保存 `SampleScene`、给 `Manager` 对象补 `GameBootstrap/SaveManager/WorkshopManager`（`EnsureManagerComponents.cs:12-31`），属 Codex 时代一次性场景修复；该场景接线现由 `KingdomPlayModeTests.LoadIsolatedNewGame`（`KingdomPlayModeTests.cs:1679-1697`）等启动测试持续验证。建议：归档；理由：无调用点、一次性、职责已被启动测试覆盖。

- [High] `Assets/Editor/Content/RuntimeClosureValidatorCommand.cs` — 维度 D2/D9；证据：全仓 grep `RuntimeClosureValidator` **只命中自身文件**，`RunFromCommandLine` 无任何 `.ps1`/文档/测试调用；其唯一逻辑是转调 `EconomyDependencyValidator.Validate(...)`（`:17`），而该同一校验器已在启动路径 `Assets/Resources/Script/Manager/GameBootstrap.cs:58` 被调用，并被 `Assets/Tests/Editor/C6IndustrialContentTests.cs`、`GlobalEconomyDefinitionTests.cs` 直接测试。建议：归档；理由：重复包装，无独立入口，职责由启动校验+Editor 测试承担。

### 2.2 建议更新

- [Med] `tools/README.md:13-15` — 维度 D1/D2；证据：该行声明"内容依赖分析现位于 `Assets/Editor/Content/ContentDependencyAnalyzer.cs`"，但实测无任何自动化调用该 `.cs`（见 2.4），且其产出 `data/content-dependency-analysis.md` **不存在**。建议：更新（由 tools 区块负责人处理）；理由：README 把"唯一权威闭包工具"（`content-closure-check.ps1`，见 `tmp/cleanup-audit-20261010/10-tools脚本工具链.md:52`）与一个无入口的 Editor 菜单混为一谈。此条跨 `tools/` 区块，仅作交叉提示。

### 2.3 保留观察

- [Med] `Assets/Editor/Content/ContentDependencyAnalyzer.cs` — 维度 D2/D8；证据：(1) 唯一入口是手动菜单 `Codex/Content/Analyze Dependency Closure`（`:16`），`AnalyzeFromCommandLine`（`:31`）无任何 `.ps1` 调用；(2) 产出文件 `data/content-dependency-analysis.md` 实测 **NOT EXIST**；(3) `docs/audits/2026-09-10-full-readonly-refactor-scan.md:173` 与归档 `CONTENTADVISE/06-existing-evidence.md:44` 均判定其为"三份平行闭包实现之一"（另两份：`ContentProgressionAudit.Run`、`EconomyDependencyValidator`）；(4) 但 `tools/README.md:13-15` 声称它才是当前归宿。建议：保留观察（候选归档，需用户裁决）；理由：文档声明与"无调用点/产出缺失"事实相矛盾，不宜单方判死。

- [Med] `Assets/Editor/EconomyParitySnapshotExporter.cs` — 维度 D1/D10；证据：`ExportMenu` 写 `data/economy-parity/UnitySnapshot.json`（`:11,13-19`），但 `data/economy-parity/` 实测 **NOT EXIST**（`data/` 下仅 `content-closure-static.md`）；`AGENTS.md:34` 以"存在时的 `data/economy-parity/`"条件引用它；`docs/repository-map.md:16` 明确"当前不存在"；文件最后修改 2026-10-07（近期维护过）。建议：保留观察；理由：按需再生的导出器，当前无产出但文档承认其存在，非陈旧。

- [Med] `Assets/Editor/KingdomPerfTestTools.cs` — 维度 D2/D9；证据：被离线开发程序集显式排除——`Kingdom.Editor.Developer.csproj:18` 与 `tools/codex/build-developer-assembly.ps1:111,139` 均 `continue` 跳过 `Assets/Editor/KingdomPerfTestTools.cs`；是手动 `EditorWindow`（`Tools/Kingdom/Performance Test Tools`，`:19`），依赖运行时 `KingdomEditorPerfLog`（定义于 `Assets/Resources/Script/Manager/SimulationManager.cs:502`）；`[KingdomPerf]` 插桩仍在 11 个运行时文件内活跃（见 `tmp/cleanup-audit-20261010/10-tools脚本工具链.md:71`）。建议：保留观察；理由：非自动流程的人工性能工具，被有意排除出开发程序集，非死代码。

- [Low] `Assets/Editor/Codex/SolutionSync.cs:1` — 维度 D9；证据：`#if UNITY_EDITOR` 包住整个文件，而该文件位于 Editor 程序集（`Assets/Editor/`），条件恒真。建议：保留观察（可选清理）；理由：冗余守卫，无害，非功能问题。

### 2.4 明确保留（在用）

- [High] `Assets/Editor/Migration/DefinitionIdMigration.cs` — **单独成节见 §3**。
- [High] `Assets/Editor/Codex/SolutionSync.cs` — 证据：`sync-solution.ps1:15` 以 `-executeMethod Kingdom.EditorTools.SolutionSync.Run` 调用；`validation.md:140` 登记为"生成/修改工程文件"高副作用工具。保留。
- [High] `Assets/Editor/Codex/LatestTestErrorReport.cs` — 证据：`[InitializeOnLoadMethod]`（`:15`）自动注册 TestRunner 回调，产出 `TestResults/Latest-Test-Errors.txt`；`AGENTS.md:34` 将其列为"当前证据入口"。被开发程序集排除（`csproj:18`、`build-developer-assembly.ps1:110`）是因依赖 `UnityEditor.TestTools.TestRunner.Api`，不代表废弃。保留。
- [High] `Assets/Editor/KingdomBuild.cs` — 证据：`build-android.ps1:43` `-executeMethod KingdomBuild.BuildAndroid`；`validation.md:141` 登记为 Android 构建唯一入口。保留。
- [High] `Assets/Editor/MusicAddressablesConfigurator.cs` — 证据：`docs/audits/2026-09-11-readonly-scan/03-runtime-data-validation.md:44` 记录它是 `MusicCatalog.ReplaceEntries` 的"当前唯一调用方"；`Assets/Musics/PMusic/` 目录实测存在（Tense/Day/Night/AllTime 四类），路径有效。保留。
- [High] `Assets/Editor/PMusicAudioImporter.cs` — 证据：`AssetPostprocessor.OnPreprocessAudio`（`:11`），`MusicPath="Assets/Musics/PMusic/"` 实测存在；自动生效。保留。
- [High] `Assets/Editor/Drawers/ExpantaNumDrawer.cs`、`Drawers/ResourceExpantaNumPairDrawer.cs` — 证据：`[CustomPropertyDrawer(typeof(ExpantaNum))]`（`:6`）与 `[CustomPropertyDrawer(typeof(Pair<Resource, ExpantaNum>))]`（`:6`），Inspector 自动生效。保留。
- [High] `Assets/Editor/StoryIllustrationAuthoring.cs` — 证据：`.codex/handoffs/2026-10-09-story-illustrations-handoff.md:17` 记录以 `-executeMethod StoryIllustrationAuthoring.AuthorStoryPresentation` 实际运行过；产出 `Assets/Resources/UI/Kingdom/StoryChapterCard.prefab` 实测存在。保留（一次性作者工具，产出已入库）。
- [High] `Assets/Editor/RelicUIAuthoring.cs` — 证据：产出 `Assets/Resources/UI/Kingdom/KingdomUIRelicActions.prefab` 实测存在；被 `RelicPlayModeTests` 通过 `ui.RelicActionsForEditor` 消费。保留。
- [High] `Assets/Editor/ReviewUIAuthoring.cs`（新增未提交）— 证据：产出 `Assets/Resources/UI/Kingdom/KingdomUIReviewControls.prefab` 实测存在（mtime 2026-10-10 13:26）；被 `ReviewInteractionPlayModeTests`（`SafeAreaRoot/ReviewControls/...`）消费。保留。

### 2.5 PlayMode 测试

- 8 个文件全部有效、无 `[Ignore]`/`[Explicit]`、无注释掉的测试、无反射（grep 全空）。所有类均用 `SaveManager.SetSaveRootOverrideForTests` 隔离存档根目录并在 `TearDown` 清理 `Temp/` 子目录（如 `KingdomPlayModeTests.cs:31-64`、`RelicPlayModeTests.cs:24-58`、`ReviewInteractionPlayModeTests.cs:21-46`）；写副作用限于 `Temp/`，未见写默认存档或仓库文件。
- 测试引用的 Editor 专用钩子类型均存在于运行时源码：`GetStoryNavigationPageForEditor`/`GetStoryNavigationTargetForEditor`、`StoryPageBuiltForEditor`、`RefreshStoryPageIfChangedForEditor`（`Assets/Resources/Script/UI/KingdomUIRoot.Story.cs`）、`RelicActionsForEditor`（`KingdomUIRoot.Relic.cs`）、`GetDeconstructButtonForEditor`（`KingdomUIRoot.ReviewActions.cs`）、`SetResearchGraphLinesVisibleForPerfTest`（`KingdomUIRoot.ResearchTree.cs`）、`GetCancellationPreview`/`CanMoveQueuedResearch`（`ResearchManager.cs`）。未发现悬空引用（D1）。
- `validation.md:130` 明确把 `KingdomPlayModeTests.ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping` 登记为当前研究树 PlayMode 测试，`validation.md:131` 登记 `Assets/Tests/PlayMode/` 为滚动/教程/星区/ticker 主题测试来源 → 整区块在用。
- [Low] 潜在覆盖重复（D8）：Story 页滚动位置恢复被至少 3 处断言——`PageScrollPositionPlayModeTests.StoryPageRestoresPositionAfterLeavingAndReturning`（`:33-67`）、`KingdomOnboardingPlayModeTests.OuterPageScroll_RestoresAfterResearchWarmup`（`:40-89`）、`StoryIllustrationPlayModeTests.ToggleReclaimsIllustrationSpaceAndFollowingChapterRemainsReadable`（`:126-133`）。建议：保留观察；理由：断言角度不同（纯位置恢复 / 从 Research 预热回切 / 插图展开后回切），非纯重复，但确有交叠，若后续收敛可合并。
- [Low] 跨层交叠（D8）：`SectorBuildingPlayModeTests`（星区建筑门控）与 Editor 侧 `SectorBuildingTests.cs`（`validation.md:127` 登记）主题相邻；`KingdomPlayModeTests.BuildingManager_RequiresEveryResearchAndWorkshopPrerequisite`（`:474-519`）与内容/经济 Editor 测试主题相邻。建议：保留观察；理由：PlayMode 验证真实 UI/场景路径，Editor 验证纯逻辑，属有意分层，非冗余。

---

## 3. `Assets/Editor/Codex/` 专项结论（3 个文件）

Codex 是外部 AI 工具痕迹；项目现行指导体系为 `.agents/skills/`（`docs/repository-map.md:14`）。逐文件判断：

| 文件 | 调用点 | 结论 |
|---|---|---|
| `EnsureManagerComponents.cs` | **无**（全仓仅自身命中） | **历史遗留 → 建议归档**（见 2.1）。一次性场景修复，`public static void Run()` 无任何入口调用。 |
| `SolutionSync.cs` | `tools/codex/sync-solution.ps1:15` → `Kingdom.EditorTools.SolutionSync.Run` | **仍在用 → 保留**。`validation.md:140` 登记为工程文件生成工具。 |
| `LatestTestErrorReport.cs` | `[InitializeOnLoadMethod]` 自动注册（`:15`） | **仍在用 → 保留**。产出 `TestResults/Latest-Test-Errors.txt`，`AGENTS.md:34` 列为当前证据入口。 |

**明确判断**：`Assets/Editor/Codex/` 目录名是 Codex 时代遗留，但**目录整体未废弃**——3 个文件中 2 个仍有真实调用/自动生效。仅 `EnsureManagerComponents.cs` 是无人引用的历史遗留，可单独归档（不必整目录归档）。这两个在用文件位于 `Codex/` 子目录仅为组织命名，其类均在 `Kingdom.EditorTools` 命名空间，与"是否用 Codex 工具"无关。

---

## 4. `Assets/Editor/Migration/` 专项结论（1 个文件）

`DefinitionIdMigration.cs` — **正当工具，非被禁止的旧版本迁移 → 保留**。

- 职责核实：它做的是**定义稳定 ID** 的审计与补全（`Audit Stable IDs`、`Assign Missing IDs From Asset Names`，`:12-16`），对 `Resource/Building/Research/StoryChapterDefinition` 扫描空 `Id` 并按资产名补齐、检测重复 ID（`:79-120`）；另有 `ReserializeFromCommandLine`（`:20-41`）做资产重序列化。**完全不涉及游戏存档版本迁移**——无旧版本号分支、无旧 ID 映射、无备份恢复。团队关注点"是否在做 v9 之前存档迁移"经核实**不成立**。
- 与项目规则一致性：项目规则要求"优先 Editor 迁移"，且此工具恰是"稳定定义 ID"的当前官方流程——`docs/architecture/definition-database.md:43-54` 明确记载新增定义后运行该菜单及其命令行等价物 `tools/codex/apply-definition-ids.ps1`；`docs/repository-map.md:53` 记载"`Assets/Editor/Migration/DefinitionIdMigration.cs` 有写资产作用，先核对覆盖类型"（提醒其覆盖范围不含 Workshop）。
- 调用点：`apply-definition-ids.ps1:15` 与 `reserialize-definitions.ps1:15` 均以 `-executeMethod` 调用；`validation.md:139` 登记为"资产/序列化变更"高副作用工具。
- 结论：保留。不是陈旧代码，也不是被禁止的旧存档迁移。

---

## 5. 不确定项（需用户裁决）

1. **`ContentDependencyAnalyzer.cs` 去留**：`tools/README.md:13-15` 称它是内容依赖分析的当前归宿，但实测无自动化调用、产出文件缺失，且历史审计判其为平行冗余实现。二者矛盾，需用户/内容负责人定夺（归档 or 补上入口）。
2. **`EnsureManagerComponents.cs` 归档安全性**：它会给场景补 `GameBootstrap/SaveManager/WorkshopManager`。若未来 `SampleScene` 接线被误删，它可作为一次性修复手段。若确认场景接线已由 Git 版本化稳定，则可归档。
3. **`EconomyParitySnapshotExporter.cs` 的定位**：`AGENTS.md:34` 保留"存在时"引用，`repository-map.md:16` 说当前不存在。是否仍需要"经济对等快照"能力需经济技能负责人确认。

---

## 6. 未覆盖项

- `Library/`（6.6G 缓存）、`Logs/`、`.codex/archive/`、`tmp/` 内产物：按准则跳过，仅作交叉引用证据。
- TextMesh Pro、Addressables、Visual Studio 集成等第三方包：非项目代码。
- `Assets/Editor/` 之外的其他 Editor 代码（如 `Assets/Resources/Script/` 内的 `#if UNITY_EDITOR` 片段、`Assets/Tests/Editor/`）：不在本子代理范围。
- 未编译、未运行任何 PlayMode/EditMode 测试，故"测试有效"仅指静态引用完整与结构合规，不含运行通过证明。
