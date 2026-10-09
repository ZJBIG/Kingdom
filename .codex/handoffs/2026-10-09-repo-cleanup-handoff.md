# 剩余改动分类与清理

用户授权检查交付后的剩余改动，清理无用文件。基线 main `b7bf4c08e6c8891c466cebe72547e9fd3740b730`，保留真实功能修复，不删除字体资产本体。

- 预览白名单 75 个目标、271 个文件、18,318,361 字节：仅未跟踪 tmp 诊断脚本/截图/沙盒、Python 缓存、3 份历史客户端笔记、旧 Android Addressables 构建状态及文件夹 meta。仓库已跟踪 tmp 文件保留。
- 所有目标先核验仓库内绝对路径、无 reparse point、无跟踪源码，随后移到 `.codex/archive/2026-10-09-repo-cleanup-204150/removed/`。`manifest.json` 记录逐项原路径/归档位置；75 项均核验原位置不存在、归档位置存在，未例行永久删除历史证据。
- `.workbuddy-ai/memory/MEMORY.md` 的既有改动及 `SIMSUN SDF.asset` 先复制到该归档 `modified/` 并核对内容一致，再还原 HEAD。字体差异仅新增18个动态字形/字符和atlas排布/像素，源TTC、GUID、动态配置均未变；个人旧笔记不作为当前规则交付。
- `.codex/config.toml` 是本机 Codex 设置，内容未改，仅在 `.git/info/exclude` 精确加入该路径，保持本地可用且不上传；未修改全局忽略或全局 Git 配置。
- 还原后 Git 内容 diff 为空；一次精确 `git add` 更新两个文件的行尾/索引状态，确认 staged diff 仍为空。主工作树全部原剩余 dirty/untracked 项已退出活动变更清单。
- `D:/GitHub/Kingdom-source-audit` 的未提交 BuildingManager/KingdomLogicTests 两处保留：属于把失败建造的状态注册推迟到提交阶段的真实修复，不是垃圾。尚无真实测试证据，不随本次清理上传；后续先验证无效数量、领土/生产力/资源不足不新增状态/事件，以及成功建造登记、扣费和事件，然后再交付。

本轮仅文件分类、归档与恢复，没有改游戏行为，没有运行 Unity。未执行真实 Unity 编译。交付只包含本交接，归档和本机设置不上传。
