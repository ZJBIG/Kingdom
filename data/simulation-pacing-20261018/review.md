# 模拟一致性修复与单主线节奏目标（2026-10-18）

## 本轮范围与输入
用户确认：从新档至工业时代约1–3天，开局反馈密集，后续适合分次游玩；只保留一条策略树，阻止膨胀。沿用既有主线，不新增平行路线、决策AI、搜索或自动调参。设计目标登记在docs/content/progression-roadmap.md；尚非节奏实测结论。
本轮开始前tools/NewEconomySimulator与该路线图无未提交修改。其余工作树已有多轮修改，pre-existing.patch只记录当时已跟踪文件diff，不覆盖未跟踪成果。source-inputs.json记录最终输入指纹。运行代码历史见2026-10-17-runtime-refactor-handoff.md。

## 实现
- SimulationState不再自行实现人口增长/饥荒公式；csproj直接链接现有Runtime PopulationState.cs，复用零时长、模式切换和容量关系处理。游戏源码与资产数值未改。
- ApplyGame以起始粮食和净消耗拆分有粮/断粮时间；整段粮食积分保持一次，恰够覆盖不判饥荒。
- 研究调度共用队首选择/原子付款helper，不跳过未满足前置或材料不足的队首；付款成功才出队。在线完成后启动下一项但不推进；离线使用全窗口判断完成，再按完成耗时扣余量并重建倍率。
- 已激活但未付款的输入与Runtime一致保持等待。队首付款通知在真正成功付款时发出。
- 新增固定fixture回归，不新增路线/策略系统；数值关系或容差比较，研究余量容差1e-5覆盖现有ExpantaNum标量中间值1e-6量化，避免对不同运算顺序作虚假精确要求。
- 字段前后值：无生产/研究资产价格、倍率、稳定ID、GUID或存档schema变化。

## 验证及命令
- dotnet build tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore：exit0，0错误，18个既有nullable警告（build.log）。
- dotnet run --project tools/NewEconomySimulator/NewEconomySimulator.csproj --no-build -- --json：最终38/38通过，exit0（verified.json）；原有23/23通过（before.json）。
- 将两份模拟器实现临时换回当前HEAD原字节，保留最终fixture复测：38项中10项失败（expanded-before.json，exit1），覆盖恰够粮、模式切换、研究余量/完成边界/队首/付款；finally恢复最终字节后重新编译及38/38复验。原始reproduction为早期34项fixture，不作最终计数。
- tools/codex/content-closure-check.ps1 -ProjectRoot . -ReportPath data/simulation-pacing-20261018/closure.md：exit0；工业Research81/81、Workshop37/37、Building50/50可达。仅静态定义闭包。
- 真实Unity 2022.3.62f3c1：EditMode过滤KingdomLogicTests、ResearchPaymentAutoTests、ResearchPaymentSaveTests、SimulationDeterminismTests、LongHorizonSimulationTests，191/191、0失败/跳过，1.6858602秒（editmode.xml/log）。日志有compile time=1215ms、无error CS；Console的坏存档日志来自预期拒绝测试。写XML后退出挂起，包装60秒仅停止自启动PID，runner exit1（unity-status.txt），最终无Unity进程。不把XML通过称作正常退出。
- git diff --check通过；只读子代理复核无确定新增错误，并已补其要求的研究完成线/准确余量/倍率更新与满住房恰耗尽检查。
- 未运行PlayMode、完整测试或真人体验；未commit/push，未触及真实用户存档。

## 尚未验证与下一步
没有新档→工业1–3天的真实完整路线证据，本轮不调整经济资产，也不声称耐玩性达标。snapshot仍缺人口变化进度，且人口阶段幸福度快照与Runtime动态读取有进一步对照空间；当前38项为固定fixture诊断，不是完整Unity trace parity。
下一具体动作：沿既有唯一主线建立正常新档库存、公共建造/研究命令的固定顺序Unity EditMode trace，先覆盖开局10分钟及Animal→Stone，记录有效节点与纯等待窗口；补齐快照人口进度输入后做同输入对照，再向工业延伸。禁止补料冒充无作弊进度，禁止引入第二条策略树。主目标保持active。
