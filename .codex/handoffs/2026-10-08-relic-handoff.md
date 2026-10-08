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
