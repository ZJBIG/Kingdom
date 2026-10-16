# 2026-10-15 早期代码冗余清理

## 范围与依据

用户授权重点审查项目早期代码，清理冗余/死代码。核对git早期历史（2025-11至2026-02的Singleton、Manager、定义及旧UI路径）、当前实现、Assets全仓C#和Unity序列化事件引用、tools调用。保留既有未提交改动；未删除现用Manager、定义、数学库或资产，没有ID/GUID/存档/数值调整。

## 实施

- Singleton删除空Save/Load；Game/Resource/Building/Research四Manager删除8个同名转发。实际保存/恢复仍走SaveManager.SaveNow/LoadOrCreateGame，原接口无源码消费者和UnityEvent。
- MusicManager删除3个无人使用的Play重载（string双参/string/AudioClip）；保留UI与内部PlayTrack、历史恢复FindTrack、Addressables句柄释放和播放控制。删除无人读取的MusicTypes列表及Clear/Add/Insert和分类计数；MusicCategories实际筛选仍保留。StartLoadedTrack不再携带唯一调用永为true的addToHistory参数，也不返回无人读取的bool。
- SaveManager删除无消费者的MarkDirty入口；保留内部dirty状态、签名检测、自动保存与失败重试。
- GameManager删除仅测试使用的无容量AdvanceFood及1e1000000假上限。原测试改用显式500容量，保留消耗、归零、上限及负delta验证；计算结果使用容差。实际运行原本就使用明确FoodCapacity，公式不变。
- PopulationState删除Eight、DepartureRatePerSecond与其唯一依赖SecondsPerDeparture；实际缺粮人口离开逻辑保留。
- 删除UI私有未调用GetResearchStatus、ResourceTreeNodeStatus，保留现用状态和文案路径。
- SectorDefinition删除无消费者的SetLocationForEditor、SetColonizationCostsForEditor；序列化字段与实际读写保留。
- 总计移除105行实现代码（11源文件）。调整Food测试；将现有音乐播放/暂停/恢复断言从跨页面材质大测试拆成独立UnityTest，保留原断言，避免无关页面失败阻止音乐验证。

## 验证与限制

- 当前全Assets源码/序列化资产：删除符号与退休Save/Load/Play的UnityEvent引用复扫通过；实际消费者与保留辅助函数已独立只读复核。
- 真实Unity编译完成。editmode.xml：KingdomLogicTests160/160，AudioFeedbackRegressionTests3/4；总163通过、1失败。进程在关闭阶段150秒超时，启动器仅结束自有Unity，不称runner通过。
- 保留的失败为AuthoredMusicSurfaceContainsIndependentSfxControls：当前Root Prefab没有SfxMute按钮，原测试第71行要求非空。本轮未改该资产/测试断言，未用放宽断言掩盖。
- playmode.xml：ReviewInteraction13/13通过；跨页面材质测试在第89行文本raycastTarget断言失败，尚未执行到音乐段。该断言保留，没有伪称音乐已验收；Unity正常结束、退出2。
- music-playmode.xml：独立音乐路径1/1通过，实际Addressables播放/暂停/恢复与图标一致性验证，Unity正常退出0。
- 未执行全仓测试，未改经济定义/公式/设计，未扩展模拟器或调参。两项现有UI配置/材质问题未纳入本轮死代码清理，保留实际失败证据。
- 字体恢复起始字节，SHA256 B9BD028E9AFC401532E35CE5A76782C40C7426FCEF7B0B94E12E53085CCBA5DE；备份物理删除，无Unity进程或本轮存档fixture残留。相关diff检查通过，最终日志无编译或Null/Missing/UnassignedReference错误。未commit/push，未创建临时Unity项目。


## 2026-10-16 两项UI回归收尾（完成）

更正上文定位：跨页面材质测试失败实际为第89行的下划线断言，而非raycastTarget。ResourceTreeAuthoring明确移除生产树名称下划线，Sector作者工具仍要求建筑名称下划线；测试据页面分别精确验证，保留射线开关断言。

Root Prefab新增现有KingdomUIButton样式的SfxMute（Controls内中心380/48、190x64、字体30），位于背景Surface之后绘制，与播放按钮及右侧音效滑条无重叠。固定控件只在Prefab创作，KingdomCollectionAuthoring同步准确DataRows路径。绑定现有ToggleMuted；按钮随状态显示“音效静音/恢复音效”，点击后立即RefreshMusicPage，同时保留集中刷新对其他状态来源的同步。

实际PlayMode已验证两项测试通过（ui-final-playmode2/2），但关闭120秒超时，启动器结束自有进程；后续为按钮最终绘制顺序增加配置断言和单张GPU截图。ui-final-editmode4/4通过；最终ui-visible-playmode2/2通过、0失败跳过，并输出单张1760x800真实Camera截图（data/scroll-frames-20261013/pages/music-controls-1760x800.png），已目视确认按钮可见、文字完整且不重叠。两次自有Unity均在写出XML后关闭120秒超时，启动器结束自有进程，不称runner正常退出。测试保存并恢复音效偏好；未新增反射，未更改经济或存档。


收尾：相关diff检查通过，最终日志无编译/Null/Missing/UnassignedReference/Assertion错误。字体恢复起始字节B9BD028E9AFC401532E35CE5A76782C40C7426FCEF7B0B94E12E53085CCBA5DE，font-before-ui-followup.tmp物理删除，无Unity进程或本轮测试存档fixture残留。两项上轮留项已解决；未执行全仓测试，未commit/push，无临时Unity项目或真实存档修改。
