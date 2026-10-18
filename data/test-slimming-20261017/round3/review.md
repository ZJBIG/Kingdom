# 第三轮测试精简

16份测试净删229行、41处断言、12个普通测试标记；时代校验2个原独立测试转为TestCase，状态覆盖不减，TestCase标记增加2。三轮累计净删2465行、736处断言、93个普通标记。源码标记不等于runner用例数量。

合并工业章节子集扫描/顺序/可读性到全量章节检查，保留稳定ID、时代、正文约束与MachineFactory真实前置；人口导航合并零容量→缺粮→恢复状态序列；删除纯静态Spacer名单回读。Ultra重复恢复合并保留恢复后可启动，时代拒绝4种状态保留。Relic测试未删，其转换和失败边界独有。

Pin重复检查集中到全Prefab扫描，卡片保留Pin存在性；基本resource root身份并入多source测试；删除仅固定常数而不比较模拟器的伪parity方法，保留真实runtime/simulator比较；删除重复surface alpha快照。5个渲染循环删除同2.2比例的2200×1000中间尺寸，保留2640×1200与1760×800边界；Review数量输入裁剪/居中并入真实批量数量测试，少1次场景加载。

验证：开发测试程序集编译exit0、0errors/6既有警告，12.46s；真实Unity10类EditMode95/95、0跳过、1.6483717s（editmode.xml/log）。Unity写XML后仍原生退出挂起，60秒清理自启动PID，runner exit1。PlayMode未运行，无实际提速测量。git diff --check通过，本轮未发现未引用private方法；生产6份修复hash仍与旧manifest一致。baseline/after/summary/test-inputs.json保存计数与最终输入。没有commit/push。

用户随后将任务转向实际运行代码重构，停止继续扩展测试清理。
