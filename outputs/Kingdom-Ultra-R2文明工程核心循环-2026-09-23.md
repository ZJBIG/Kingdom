# 文明工程：当前合同与剩余验收

更新：2026-10-07。文明工程已实现；删除反复追加的修复流水、旧中间失败、编译次数和重新开发相同功能的待办。

## 当前循环

巨构供给→相位稳态认证→物质自治认证→文明连续性认证→最终能力与远征支撑。
定义：Assets/Resources/Datas/Ultra/UltraCivilizationEngineering.asset；
状态/命令：Assets/Resources/Script/Runtime/UltraProjectState.cs及Manager/UltraProjectManager.cs。

## 状态与费用

- Locked→Ready→Running↔Paused→ReadyToCommit→Committed；最终Committed对应Completed阶段，中间提交进入下一阶段。
- 启动先核验全部前置/材料再支付；已付启动费的恢复不重复收费。
- 放弃未完成阶段清除本阶段进度、不退已耗费，保留已完成阶段与已付启动费；Ready+LaunchFeePaid是合法保存状态。
- 连续材料/Food实际消耗与已付时间共同决定进度，供给不足暂停；阶段完成后停止额外扣费。
- 最终提交幂等；重复点击/重载不授予重复成果。
- 运行/远征姿态及切换条件由Manager校验，非法枚举拒绝，不静默回退。

## 保存与界面

- v9缺整个UltraProject区段初始化新Locked工程；显式null或非法对象/DTO拒绝。
- ProjectId、StateVersion、阶段连续性、暂停原因与时代跨段一致性应用前校验。
- 手动、供给不足、定义缺失暂停原因持久化，UI从强类型Preview读取。
- Overview显示阶段、阻碍与下一步，Locked状态可导航PhaseEnergyArray；详情复用固定Prefab姿态按钮，不保存临时UI字符串。

## 验证入口

Locked入口、真实背景拖拽、工程/科研/战役组合及跨2h/8h离线边界已有回归，不重复列为待开发。最新保留验证见[阶段交付](阶段交付.md)；单处回声铸造环也已完成；更多遗迹与其他独立机制仍须另行授权。本文维护行为合同，不表示本轮重新运行Unity。
