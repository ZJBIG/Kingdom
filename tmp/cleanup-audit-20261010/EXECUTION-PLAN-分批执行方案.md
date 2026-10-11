# 大清洁 · 分批执行方案

> 依据：`SUMMARY-大清洁候选清单.md`（发现阶段结论）。
> 原则：**可恢复优先**（能归档就不删）→ **小批次**（每批验证后推进）→ **不碰他人工作树**。
> 当前工作树含 84 项未提交改动（含并行任务），执行时**只触碰本方案列出的路径**。

---

## 前置检查（每批执行前都做）

```bash
git status --short          # 确认无新增冲突
git stash list              # 确认无待恢复 stash
```

---

## 批次 0 · 零风险（空目录 + 悬挂条目 + 测试残留）

**内容**
- 删空目录：`tmp/ultra-r2-editmode-717-first-run/`、`tmp/ultra-r2-editmode-before-retry/`、`tmp/ultra-r2-playmode-before-final/`、`output/fb89034f-.../stage2/intermediate|stage3|trace/`、`Kingdom/`（含 `Kingdom/outputs/`）
- 同步 9 个"已删未 rm"的悬挂跟踪条目：`tmp/audit_upgrade_continuity.py`、`tmp/verify_e08{,b,c,d,e,f,g}.py`、`tmp/verify_p003c.py`
- 删 `Assets/InitTestScene639269782161251882.unity`（+ `.meta`）——Unity Test Framework 残留，已忽略、未跟踪

**命令**
```bash
rmdir tmp/ultra-r2-editmode-717-first-run tmp/ultra-r2-editmode-before-retry tmp/ultra-r2-playmode-before-final
rmdir output/fb89034f-45d8-4194-856b-175b429cc8d2/stage2/intermediate output/fb89034f-45d8-4194-856b-175b429cc8d2/stage3 output/fb89034f-45d8-4194-856b-175b429cc8d2/trace
rmdir Kingdom/outputs Kingdom
git add -u tmp/                                  # 落实 9 个已删除条目的跟踪状态
git status --short tmp/                          # 应为空
```
**风险**：无（空目录 / 已不存在的文件）。**回滚**：`git checkout` 恢复跟踪条目。

---

## 批次 1 · 低风险（未跟踪本地缓存与过期证据）

**内容**（全部未跟踪，删除不进版本库）
- `TestResults/` 过期批次（约 12 组，见 SUMMARY §1.1）；**保留** `main-integration-*`、`*-full-r2`、`*-r3`/`r4`、`review1009-*`、`Latest-Test-Errors.txt`
- `Logs/`（21M）、`UserSettings/`（1.9M）、`.vs/`（416K）
- `tools/NewEconomySimulator/bin/`、`obj/`（可用 `git clean`）

**建议做法**：先整目录移到仓库外的备份（如 `D:/Kingdom-cleanup-backup-20261010/`），确认无误后再删。

**风险**：低（可重新生成）。**回滚**：从备份目录还原。

---

## 批次 2 · 中风险（历史文档与 .codex 中间产物归档）

**内容**
- 归档（**移动**到 `.codex/archive/`，不删）：`docs/audits/2026-09-10-full-readonly-refactor-scan.md`、`docs/audits/2026-09-11-readonly-scan/03|04|05`、`docs/audits/2026-10-09-*`（3 份）、`docs/audits/2026-10-10-review-implementation.md`、`docs/content/alien-war-first-version.md`
- 删除 `.codex/archive/` 内无价值中间产物：`shadercompiler-UnityShaderCompiler.exe0.log`、`detail-ui-migration-current.log`（3.2M）、`pdf-intermediates-20260914-100336/`、空 `package-recovery/`
- 归档 3 份已闭环 handoff 到 `.codex/archive/`

**注意**
- 归档后需更新引用：`docs/repository-map.md`、`.agents/skills/*` 中若指向被移文件，需同步改路径（**先 grep 确认引用面**）。
- `outputs/` 18 个 md **不动**（经核实仍在用）。

**风险**：中（文档引用需同步）。**回滚**：从 `.codex/archive/` 移回。

---

## 批次 3 · 中风险（代码死代码，需编译验证）

**内容**：SUMMARY §2.1 表中 15 项 + §2.5 中 `BuildingTransactionRules.cs:3` 死分支。

**验证要求**
- 删前先跑一次离线编译门：`tools/codex/build-developer-assembly.ps1` + `compile-developer-tests.py`（不启动 Unity，本机可用）
- 删后重跑，确认无编译错误
- **`Singleton.Save()/Load()` 需先确认无 UnityEvent 绑定**（SUMMARY §6-4），确认前不动

**风险**：中。**回滚**：`git checkout -- <文件>`。

---

## 批次 4 · 高风险（需产品/架构确认后才能动）

- 76 个孤儿 PNG（`git rm`）——**需确认龙立绘/宝石图标是否规划中**
- UI 运行时构建 4 处（`Era.cs`/`Story.cs`/`DetailUI.cs`/`Sectors.cs`）——**需架构确认是否属"已记录例外"**
- 9 条测试反射断言改写 + 脆弱断言加容差——需编译+跑测试验证

**风险**：高（影响玩法/UI/测试保护力）。**必须逐项确认后单独执行**。

---

## 批次 5 · 待决策（不做）

`com.unity.timeline` / `com.unity.visualscripting` 移除、`SectorDefinition.MapX/MapY` 等字段迁移、`tmp/`+`output/` 的 git rm vs 移出——见 SUMMARY §6。

---

## 建议执行顺序

`0` → `1` → （验证工作树干净）→ `2` → `3` → 停下，等产品/架构确认后再评估 `4`。

每批结束输出：改动文件清单、验证命令与结果、剩余未处理项。
