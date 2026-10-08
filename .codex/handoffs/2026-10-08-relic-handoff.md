# G 回声铸造环续接

用户授权：恢复 G 单处遗迹，E 继续暂缓；选择方案②维护委托与自主制造，玩法细节由代理确定；每个验证完成阶段提交并推送 GitHub。

## 合同与阶段

- 唯一遗迹位于既有鲸鱼座工业前哨，Ultra、技术奇点、星际导航、已有巨构和首个文明工程认证构成准入。保留现有星区奖励、文明工程状态及 v9。
- 一次调查后修复/拆解互斥，封存保留进度。修复后主动接固定维护委托，拆解后额外逆向认证并在工坊制备支援。不会运行随机任务生成器。
- 两路线均制备一份相同远征支援，最多一份待用或服役；显式绑定活动远星战役，该战役 Food/材料补给费用乘 0.85，完成或撤退后消费不退。不增加产量、战斗力或新的资源库存。
- G1：定义、状态、事务、供给模拟及可选 v9 保存；真实 Unity 编译和针对性 EditMode。
- G2：工坊制造、战役实际消费和收益预览；针对性交易/战役/保存回归。
- G3：Prefab 操作与不可逆选择确认、真实页面与离线回归；编译、相关 EditMode/非零 PlayMode/Console。各阶段验证后独立 commit/push。

## 修改前事实与基线

- 分支 `codex/ultra-project-manager-tick-tests`，起点 `11b3e26`；origin 为 `https://github.com/ZJBIG/Kingdom.git`。
- 已有未提交 Story 插画代码、字体、客户端记忆和临时文件保留且不纳入 G 提交；工作树清单已检查。
- 修改前 `content-closure-check.ps1 -ProjectRoot D:/GitHub/Kingdom -ReportPath D:/GitHub/Kingdom/data/content-closure-static.md` exit0；Industrial 主线及 Spacer/Ultra 定义静态可达。
- 修改前 `dotnet run --project tools/NewEconomySimulator/NewEconomySimulator.csproj --no-restore -- --json` exit0，`passed:true`；仅确定性 fixture 维护诊断，不是遗迹覆盖、真实 Unity parity 或节奏验收。
- 当前工具外层 PowerShell 7 缺内置模块；使用现有 `C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -File` 执行仓库脚本，不安装工具、不绕过策略。
- Unity 安装 `D:/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe`，开始时没有用户 Unity 进程。

## 本轮进度

G1 已完成：新增 RelicDefinition/RelicState/RelicManager 及唯一 EchoFoundryRing 定义；GameManager 持有状态所有者，BuildingManager 纳入供给负荷，Simulation 在线/离线使用同一 Tick，SaveManager 加可选 v9 Relic 段、缺段初始化、非法 present 段拒绝、版本签名与交叉前置校验。

- 调查/修复/拆解认证分别 1800/1800/5400 秒，维护委托 1200 秒，满供给口径；不是实玩节奏验收。
- 维护总材料：相位材料540、幽影合金1200、钛合金3400、铜线1800、陶瓷1800、火箭燃料1600；自主制造分别1200/2400/7000/3600/3600/3200。复用原有来源，制造每项成本均大于完整维护委托，换取即时独立制备。
- 真实 Unity 编译 `Logs/outputs-G1-compile-20261008.log` exit0；后续 r3 EditMode 又实际编译了最新源码。
- `TestResults/outputs-G1-editmode-r3-20261008.xml` 40/40，exit0，覆盖状态/Manager事务/供给/可选保存段。日志 `Logs/outputs-G1-editmode-r3-20261008.log`。
- r1 在程序集重载阶段中断，无XML；r2 39/40，缺研究夹具清支付却未清 Completed 状态，修夹具保留拒绝断言；r2另有原生退出停滞，核验命令行后仅终止本代理该测试进程，整轮计失败并保留证据。
- 修改后 closure exit0；模拟器13项 Passed True、exit0，仅数学/fixture维护诊断，不覆盖新增遗迹、不等于真实Unity parity。
- G2/G3 工作文件正在实施，未纳入G1提交。下一步验证真实战役扣费、工坊交易，再完成Prefab交互与完整回归。

## G2 战役与工坊

SectorManager 的预览和真实 Food/材料扣款统一应用绑定战役的 0.85 支援倍率；完成/撤退清除服役支援。WorkshopManager 复用现有解锁门槛，提供原子制造命令。

TestResults/outputs-G2-editmode-r2-20261008.xml 8/8，Unity exit0；首轮 XML 8/8 但原生退出1，不作为通过证据。覆盖逐项实际扣款、战力不变、缺料原子失败、撤退/完成消费、目标绑定、制造门槛与重复付款拒绝。

G1 提交 86d03c8faa92a480f8287f664a824471e4d72cfc 已由 origin 分支精确 SHA 核验。TLS 断连返回不确定，通过远端核验确认上传。

## G3 界面与最终回归

已完成固定 RelicActions Prefab（Unity Editor 作者工具生成，保持GUID）、星区详情绑定、永久路线确认/取消、封存/恢复、制备/派遣及数据驱动工坊行；所有操作调用 Manager，页面关闭不停止模拟。详情仍复用唯一 UIDetailRequirementScrollGesture，确认区实测可滚动到达。工坊页打开的遗迹详情也进入共享实时刷新，战役结束后不残留旧服役状态。

- G2 提交 `99d45a1` 已正常推送 origin。
- 真实 Unity 作者工具 `Logs/outputs-G3-authoring-r2-20261008.log` exit0，实际编译当前源文件并生成正式Prefab。
- 隔离检出 `C:/Users/19603/.codex/worktrees/relic-verification/Kingdom` 从已上传G2开始，仅复制G3授权文件；未包含原工作树暂缓的Story插画修改。关键源文件/Prefab逐项SHA256一致。Unity自行生成的字体/ShaderGraph缓存修改未纳入交付。
- `TestResults/outputs-G3-editmode-full-r2-20261008.xml`：833/833，Unity exit0；54项Relic用例含真实ManualTick/AdvanceOffline Food与材料支付、零供给暂停、线上/离线一致性、定义引用/source-sink与v9校验。日志同名位于Logs。
- `TestResults/outputs-G3-playmode-full-r2-20261008.xml`：47/47，Unity exit0；其中6项Relic真实Scene/Canvas/Button路径覆盖确认取消、路线互斥、修复委托、拆解工坊制造、重复事件拒绝、支援预览、封存恢复、页面切换、工坊共享详情自动刷新及滚动按钮可达。日志同名位于Logs，无意外游戏异常或C#编译错误。
- 末次content-closure-check exit0，原有Industrial/Spacer/Ultra主线可达；遗迹定义的来源和前置另由强类型测试检查。确定性模拟器已在修改前/后13项通过，不声称它覆盖遗迹或玩家节奏。

失败记录保留：原工作树首次PlayMode 0/5被已有StoryChapterCard缺失阻断，不修改E；隔离首次4/5是夹具误认原生ScrollRect为滚动所有者，改用既有真实手势并保留几何断言；完整EditMode首轮802/833是旧代表存档夹具的null Relic被JsonUtility序列化为无效对象，补合法初始段并增加真实Capture→JSON→Apply往返，未放宽非法存档拒绝规则；完整PlayMode首轮46/47是LoadScene尚未替换旧根时夹具抓取旧UI，等待新场景生效后47/47。

最终状态：G1/G2/G3完成。E插画继续暂缓，原未提交Story代码、字体、客户端记忆和临时文件均保留且不上传。当前原工作树的Story卡片缺失仍属于已有暂缓修改；干净G交付已在隔离检出验证。数值是首版设计，未声称实玩节奏或设备触摸手感验收；外部运行由用户自行验收，不列为代理未完成门槛。