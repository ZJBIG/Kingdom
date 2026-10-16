# 运行时玩家文案审查（2026-10-15）

本分工枚举 UI 全部42个C#文件、Manager18个、Runtime15个、Data16个、Misc2个，提取中文及Unicode转义的候选文本，并按Overview、时代、研究、资源/生产树、建筑交易、工坊、音乐、离线、剧情、星区、文明工程、遗迹逐项对照消费端。Manager重点人工审查TutorialManager全部教程与推荐文案、StoryManager条件/进度提示、ResearchManager支付失败及SaveManager对UI暴露的错误。Data/Runtime大部分为Inspector标签与异常，未把诊断文字批量改成玩家提示。枚举Kingdom UI Prefab全部m_text字段，重复卡片多数运行时绑定；UnsafeAreaTicker*、AuthoredRows、DetailPanel、卡片图标由主代理负责。未覆盖第三方、二进制文本和真实运行视觉验收。

已改：
- 教程去掉Manager初始化/定义/真实行动等开发者语言，Ultra/Archotech不再称远期框架，明确工程、遗迹、远星目标。
- 土地与生产力通用失败提示加对应解决方法；时代准备信息明确已用/总生产力。批量数字和图标交由主代理统一实现。
- 每时代概述改为当前采集、住房、加工、发电、运输、轨道、远征与认证的具体行动。
- Overview建筑建议复用建筑作用摘要，工坊建议使用定义Description；作用摘要fallback使用定义说明，删去“提供新的发展能力”“值得查看详情”等空话。
- 研究/工坊/人口/剧情/星区通知直接说明状态与下一入口；首位居民入住不再断言供给已可持续。
- 星际姿态/预算改为预览估算；供给指标显示满足率（0—1）；离线折算、数量取整、遗迹/工程错误均改为玩家语言。
- 锁定StoryChapterCard改用现有GetChapterProgressHint，显示时代/前章/研究/建筑等未完成条件；正文插画仍保持锁定，未改剧情完成规则。

覆盖的改动文件：TutorialManager.cs、StoryManager.cs、KingdomUIRoot.cs、LiveRefresh、Era、PageRows、Story、Sectors、UltraProject、Relic、OfflineSummary、Quantity、ReviewActions，以及StoryChapterCard.cs；测试更新KingdomOnboardingPlayModeTests中锁定剧情的旧占位断言，要求实际绑定条件提示且不泄漏正文。

定向git diff --check通过，仅LF/CRLF提示。主代理统一编译/测试。本分工未启动Unity；未执行真实 Unity 编译。

Root.prefab三个固定占位文案匹配准确，但写入被拒绝，未绕过：Custom应为“自定义”；????/??/??应为“正在载入日期”；SYSTEM READY · TOUCH A SECTION TO CONTINUE应为“选择左侧栏目查看王国”。数量和日期运行时有覆盖，也不等于占位源已清洁。

续接结果：上述Root.prefab三个占位文字现已全部写入，blocked-ui-copy.patch反向检查通过。前文写入拒绝为历史记录。最终验证见handoff.md的续接章节。
