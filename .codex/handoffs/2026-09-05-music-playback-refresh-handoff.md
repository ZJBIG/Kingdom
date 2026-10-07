# Kingdom 音乐系统交接

## 当前实现

- 音乐资源位于 `Assets/Musics/PMusic`，共 56 首，分为 Tense 15、Day 14、Night 15、AllTime 12；迁移前后的音频 `.meta` GUID 一致。
- 音乐使用 Addressables 1.22.3，本地 `Kingdom Music` 组采用 LZ4、Pack Separately；曲目地址为 `music/{category}/{clipName}`。
- `MusicCatalog` 只保存 ID、显示名、分类、地址和时长；`MusicManager` 启动时异步加载目录，播放时按需加载 `AudioClip`，切歌/停止/销毁时释放 handle。
- 音乐 UI 使用目录元数据显示曲名和时长，不再扫描或加载 `Resources` 音频。
- 配置/构建入口：`Tools/Kingdom/Rebuild Music Addressables`、`Tools/Kingdom/Build Music Addressables`。
- Developer 构建脚本区分 Player Runtime 与 Editor Runtime：Editor 会生成 `Kingdom.Runtime.Editor.dll`，保留 `SetIdForEditor` 的 Editor-only 边界。

## 变更文件

- `Assets/Resources/Script/Manager/MusicManager.cs`
- `Assets/Resources/Script/Data/MusicCatalog.cs`
- `Assets/Resources/Script/UI/KingdomUIRoot.Music.cs`
- `Assets/Resources/Script/UI/KingdomUIRoot.SceneLayout.cs`
- `Assets/Editor/MusicAddressablesConfigurator.cs`
- `Assets/Editor/PMusicAudioImporter.cs`
- `Assets/Resources/Script/Kingdom.Runtime.asmdef`
- `tools/codex/build-developer-assembly.ps1`

## 已验证证据

- Addressables Windows 内容构建成功：57 个 Bundle（56 首音乐 + 1 个目录），总计约 150.52 MB。
- 真实 Unity 编译：无 C# 编译错误。
- `Kingdom.Runtime.Developer.csproj` Debug/Release：0 错误；Player 响应文件不含 `UNITY_EDITOR`。
- `Kingdom.Editor.Developer.csproj` Debug/Release：0 错误；Editor Runtime 响应文件包含 `UNITY_EDITOR`。
- 用户要求删除的音乐测试与 `.meta` 已全部删除；此前执行过的音乐 EditMode 4/4、音乐 PlayMode 2/2 及完整 PlayMode 35 项（34 通过、1 Ignored）仅作为历史验证。
- 最新完整 PlayMode 报告：0 失败；1 个既有测试因未测量 Overview 溢出而 Ignored。

## 剩余事项

- 尚未执行运行期播放验证。
- `TestResults/PlayMode-results.xml` 是删除测试前生成的历史结果；如 Unity 释放文件锁，可移入 `.codex/archive/`，不作为当前测试源。
- Runtime 仍有 3 个与本任务无关的既有未使用变量/字段警告，按用户要求未修改。

## 用户决策

- 音乐采用本地 Addressables，不配置远程 CDN。
- 不保留 Resources 音乐回退。
- 相关音乐自动化测试和断言已按用户要求移除。
