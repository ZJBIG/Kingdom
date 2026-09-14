# Conservative product defaults

本文件唯一维护未获明确产品变更授权时的默认决策，合并原 engineering rules 中的产品默认。实现事实仍以当前代码/资产为准；这些默认不构成本轮额外修改许可。

- Food保持GameState中的特殊值；容量边界遵守根AGENTS与 `docs/balance/no-resource-caps.md`。
- UI显示/隐藏不暂停模拟、研究或音乐；关闭设置页面不停止MusicManager。实现和生命周期见 `docs/architecture/ui-boundaries.md`。
- 纯UI重设计不扩展离线进度，不新增研究取消、已付款/当前研究的退款规则。
- 资源短缺按每tick统一满足率处理；不引入建筑优先级分配系统。计算契约见 `docs/architecture/runtime-state.md`。
- 人口仅在粮食不可用且Food/s持续为负时离开；降低住房容量不追溯移除现有人口。未使用的容量离开helper只是未来产品选项，不构成当前运行契约。
- 视觉设计以已有低饱和中世纪策略风格为起点，不未经确认更换颜色/字体；具体视觉调整需用户参考。当前移动端设备、分辨率和Canvas技术基线仅维护在UI技能，旧桌面分辨率不再构成验收门。

提交、PR、安装、升级、真实存档及发布权限统一遵守根 `AGENTS.md`，不在此维护第二套权限规则。
