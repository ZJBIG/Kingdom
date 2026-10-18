# 快照与基础供给修复复核

本轮没有调整内容资产数值、游戏存档v1、稳定ID或GUID。诊断快照协议升级v4，旧v3需重新捕获：旧协议缺人口变化进度、基础木材产率，并可能已将研究队列排序。导出过程补齐未实例化建筑的零状态而不创建Runtime建筑；同类型ID必须唯一，跨类型合法同名、所有GUID全局唯一；按类型解析引用；Sector没有Runtime时代字段。

人口容量先从建筑派生，再恢复[0,1]的进度。研究队列保存原顺序。实际Runtime在资源预算纠偏重算建筑速率时，原来会清掉1/s基础木材；现在保留该基础速率。模型将快照BaseProductionRate计入满足率、投入预算和产出。

反证：snapshot-before.json为46中6失败；snapshot-namespace-before.json为47中1失败；最终snapshot-final-verified.json为49/49。真实Unity snapshot-base-before.xml为4中2失败，纠偏后木材产率0；修复后snapshot-base-final.xml为4/4通过（1.1141146秒）。这两次XML后进程退出挂起，包装60秒仅清自己的PID，runner1。

snapshot-base-final-input.json与.after.json未经加工送入.NET；snapshot-base-final-comparison.json为passed=true、differences=[]。覆盖1木屋/1人口/0.625进度/队首等待的单个0.1秒在线tick，包括人口、进度、Food、全部资源、活动研究与队列。该夹具为强类型状态构造，不是新档路线或完整parity；未运行离线分次对照或PlayMode。

Runtime修复后沿原唯一49命令表重跑工业全程：passive-supply-industrial.xml为1/1通过、226.3951645秒、runner正常0；152436.8游戏秒进入工业，与修复前相同。仅证明该固定连续在线路线的可达性和耗时，不证明玩家体验或分次离线节奏。

所有临时Program入口已按原字节恢复并重建。snapshot-developer-compile-verified.log为最终开发测试程序集编译exit0、0错误、6个既有废弃API警告。最终.NET构建0错误、18个既有nullable警告。最终独立子代理复核因transport失败，结论来自主Agent源码核查和实际运行结果。
