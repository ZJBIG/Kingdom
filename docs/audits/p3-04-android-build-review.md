# P3-04 Android 构建审查

审查日期：2026-08-30

## 当前静态配置

- 包名：`com.ZJ.Kingdom`
- 版本：`0.5`，versionCode 1
- Unity：`2022.3.62f3c1`
- Scripting Backend：IL2CPP
- ABI：ARM64
- minSdk：22
- targetSdk：35
- 方向：仅横屏
- 主场景：`Assets/Scenes/SampleScene.unity`
- CanvasScaler：ScaleWithScreenSize，2640x1200，Match Width

`validate-android-settings.ps1`、`validate-ui-contract.ps1` 和
`verify-yaml-references.ps1` 均通过。

## 真实构建尝试

在 `D:\CodexTemp\KingdomTodoValidation_20260830` 隔离副本执行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/codex/build-android.ps1 `
  -ProjectPath D:\CodexTemp\KingdomTodoValidation_20260830 `
  -UnityPath D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe `
  -TimeoutSeconds 1800
```

结果：

- Unity 授权成功；
- Android 平台模块成功加载；
- 没有 C# 编译错误、Gradle 失败、授权错误或磁盘不足信息；
- 日志停在 Bee `ScriptAssemblies` 后端启动，之后不再更新；
- 1800 秒后脚本按上限终止隔离 Unity；
- 未生成 `Builds/Android/Kingdom.apk`；
- 父进程消失后残留的隔离 `bee_backend` 已停止；
- 原项目 Unity PID 32928 未停止或修改。

## 结论

P3-04 的项目配置已经完成，但 APK 产物验收未完成。当前阻塞是隔离副本的 Bee 编译缓存/后端冻结，不能写成玩法代码失败，也不能写成 Android 构建通过。

下一次只需在可用构建环境中清理隔离 Bee 缓存或在原项目关闭后复用其有效 Library，再运行同一脚本。无需为此修改玩法、经济或 UI 定义。

Huawei P40 Pro 安装、启动和触控验收仍需真实设备。
