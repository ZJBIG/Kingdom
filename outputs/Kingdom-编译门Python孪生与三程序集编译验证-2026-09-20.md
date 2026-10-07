# Python开发者编译入口

更新：2026-10-07。以下保留当前核对方法与行为合同，已实现功能不再列为待开发。

Python入口已经存在；本专题说明其编译参数、产物判定与验证边界。

- 入口为tools/codex/compile-developer-tests.py，参数以当前实现为准；与PowerShell入口都只负责编译。
- 长编译参数使用响应文件；源文件清单应包含当前源码，引用指向本次Runtime产物。
- 当前Python入口先移除目标DLL与ref DLL，再执行编译；PowerShell入口只检查DLL存在，不能将二者表述为相同的新产物保证。
- 不从特定历史客户端的原生进程故障推导今天只能走某种shell。
- 真正Unity编译与测试仍需独立日志。

核对入口（仓库相对路径）：tools/codex/compile-developer-tests.py；tools/codex/compile-developer-tests.ps1。

本次仅修订文档；未执行真实 Unity 编译。
