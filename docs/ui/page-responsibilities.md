# 页面职责与详情操作

本文件唯一维护页面入口、详情按钮及选择态语义，合并旧 detail-action-policy 与UI技能中的重复条款。布局/手势技术契约在UI技能维护，State/刷新/生命周期在 `docs/architecture/ui-boundaries.md` 维护；实际数值由当前Definition、State和Manager决定。

## 页面入口

| 页面 | 职责与入口边界 |
|---|---|
| Overview | 回答“现在做什么”，显示当前目标、阻碍和推荐行动 |
| Era | 回答“如何推进时代”，显示时代条件；详情可跳到对应研究、资源或建筑 |
| Research | 研究树、研究队列与研究详情 |
| Workshop | 工坊升级与购买状态，不复用研究详情内容 |
| Buildings | 普通建筑；过滤全部SectorBuilding |
| Sectors | 星区状态；占领后行右侧才显示“建筑”展开按钮，未占领不创建或加载建筑菜单 |
| Resources | 资源状态和详情，不承担建造、研究或工坊购买 |
| Story | 叙事与历史反馈，只导航到真实系统页面，不直接修改游戏状态 |

## 详情主操作

| 详情对象 | 允许的主操作 |
|---|---|
| Research | 加入/移出研究队列；不提供独立付款按钮 |
| Workshop | 购买当前所选工坊升级，不显示研究详情文本 |
| Era | 时代推进或对应详情/页面导航 |
| Building | 无建造/升级主按钮；普通建筑在建筑列表操作，星区建筑仅在已占领星区菜单操作 |
| Resource | 无主操作按钮 |

- 队首研究由ResearchManager自动付款；支付/不足等待与台账契约见 `docs/architecture/research-queue-payment.md`，UI不得另行扣款。
- 建造/拆除调用 `BuildingManager.TryBuild/TryDeconstruct`，不在UI保存权威数量、库存或购买结果。
- 详情主按钮采用单按钮布局，左右各48像素内边距，由Prefab创作；不恢复贴满详情面板或双按钮支付布局。
- 详情按钮文字、可用性和委托来自当前对象及其Manager状态；切换对象必须清除上一对象的选择状态，避免旧研究状态污染建筑/工坊详情。

## 选择态与显示状态

- 仅当前正在研究的目标在研究树和队列中使用金色Outline。等待项、普通节点、选中态及连接线不得因此变成金色。
- 展开/折叠与选择仅属UI内存，不写Runtime State或存档。
- 可见页面刷新显示；刷新不得推进模拟或重复订阅。研究缓存页隐藏时的输入屏蔽遵循UI生命周期文档，不要求所有页面一律Deactivate。

回归场景见 `docs/testing/playmode-test-plan.md`；同一规则的测试断言不是第二份产品规则。
