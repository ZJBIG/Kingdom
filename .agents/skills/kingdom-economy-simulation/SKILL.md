---
name: kingdom-economy-simulation
description: Kingdom 内容设计与经济验证统一入口。适用于玩法、时代进度、Research/Resource/Building/TechLevel/Workshop、生产消耗、人口领土、星区战役、数值平衡和可达性；修改相关定义前必须加载。负责可玩内容设计、闭包与确定性诊断、真实Unity验收，不用于纯UI样式或纯ExpantaNum内部实现，不进行策略搜索或自动调参。
---

# Kingdom 内容与经济

保留稳定技能名称，统一内容设计和经济验证。不要创建平行的内容/经济规则副本。若直接命中本技能，先经根 `AGENTS.md` → `.agents/skills/kingdom-project-dev/SKILL.md` 完成范围检查，再返回本分支；同轮已读入口不回读。一般工作树与交接继承主入口规则。

## 开始前

读取当前根/就近AGENTS、相关handoff、真实定义及其消费端；按需定位 `docs/repository-map.md`。再读取：

1. [内容设计与质量门槛](references/content-design.md)。
2. `docs/balance/no-resource-caps.md`、`docs/balance/balance-model.md`。
3. `docs/content/progression-roadmap.md`、`docs/testing/acceptance-checklist.md`；运行场景按需读 `docs/testing/playmode-test-plan.md`。`content-balance-tests.md` 仅为旧链接兼容入口，无需回读。
4. 当前closure/parity/测试摘要及其输入版本；不存在或陈旧的输出记为缺口。

不要求加载历史TODO/CSV，不从旧优先级清单重建已有系统。`.codex/prompts/CODEX_ECONOMY_PROMPT.md` 只是到本技能的入口，无需从本技能再回读形成循环。

## 唯一执行顺序

1. **确认授权**：只读审计不运行写报告/缓存/存档的命令；以下执行门针对获准的定义/运行时实施任务，不能把审计升级成修改。
2. **改定义前**运行 `tools/codex/content-closure-check.ps1`；参数是 `ProjectRoot`，默认写 `data/content-closure-static.md`。使用当前环境允许的PowerShell执行，不绕过安全策略。
3. 运行 `tools/NewEconomySimulator` 的确定性诊断，核对真实输入覆盖；当前CLI/副作用查项目技能 `references/validation.md`。人工fixture自测不等于真实Unity trace对比。
4. 复现相关Gameplay行为，区分定义图错误、生产死锁、资源短缺、建造/研究/工坊等待、输入不匹配、玩法bug和runtime parity差异。
5. 先修行为，再验证当前时代闭环。相关输入/parity失败、编译错误或主线不可达时，不盲调成本、不扩展后时代。只修改授权字段，记录前后值和依赖。
6. 修改后重跑相同检查：定义合法性、资源source/sink、进度可达性、Unity编译、EditMode、相关PlayMode、Console。UI受影响同时读取UI技能。
7. 仅在真实运行/试玩证据支持下校准节奏。记录预期/实测等待、回本与前后变化；缺运行证据就标记未验证，不以模拟器自测填充“节奏合格”。
8. 按原handoff交付字段差异、输入/输出版本、测试用例数、日志、阻断和下一动作；纯只读仅回复。

## 严格输入与状态契约

- Resource、Building、Research、Workshop、TechLevel、Sector与runtime/save状态采用强类型快照，以 `.meta` GUID解析资产。缺ID/meta、断GUID、同类型重复ID、重复资源对必须失败，不能静默丢定义。
- 完整覆盖Workshop解锁、研究/工坊前置、资源成本、购买和真实效果；先支付后授予效果。
- 研究支付完整剩余成本后才推进；保留支付台账和前置闭环。排队成功不等于付款成功。
- 采用当前代码常量/公式，保留事务、非负库存、State权威及ID/GUID兼容；不依据日期审计重新实现。
- 未经明确授权不改ID、GUID、坐标、文案、无关效果或meta；内容质量要求不能当作额外修改许可。

## 证据边界

`tools/NewEconomySimulator` 仅作确定性回归，不是玩法权威。真实Unity运行、PlayMode日志和玩家试玩是行为/节奏依据。

- `data/economy-parity/` 仅保存快照、按序事件、首次差异；不输出路线评分、策略推荐或“平衡验收”。禁止扩展策略搜索、决策AI和自动调参。
- 旧Fast/Normal/Conservative、PacingAcceptance、日期迭代和旧Workshop购买记录只作历史背景，不能用于当前调参。
- 静态可达不证明实际生产/消耗正确；fixture与self-test不证明真实Unity parity；零项目PlayMode不算验收。
- 未执行Unity时明确注明：`未执行真实 Unity 编译。`
