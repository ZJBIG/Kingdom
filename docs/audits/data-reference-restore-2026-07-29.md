# Data 引用恢复记录

## 结果

- 根据 Git 中旧 `.meta` 与当前 `.meta` 建立 242 个 GUID 映射。
- 修复 187 个 `Assets/Resources/Datas/**/*.asset` 文件。
- 修复 968 处旧 GUID 引用。
- 当前 Data 资产引用对整个 `Assets/**/*.meta` 检查：未发现未知 GUID。
- 示例 `SteamPlant` 的 StoneBrick、Steel、Coal、Industrialization、SteamPower 引用已恢复。

## 当前仍缺失的内容

当前恢复回来的 Data 目录没有以下本轮新增工业资产：

- `IndustrialCopperSmelter`
- `IndustrialTinSmelter`
- `IndustrialBronzeFoundry`
- `BlastFurnace`
- `BauxiteMine`
- `AluminumSmelter`
- `ConcreteWorks`
- `CentralPowerStation`

对应新增研究和工坊资产也尚未落地。`GlobalEconomyMigration` 已保留生成逻辑，但执行它需要 Unity 释放当前项目锁。

## 模拟边界

GUID 修复后的独立模拟器读取到 200 个现有定义，说明引用恢复已生效。该模拟仍不包含缺失的新工业资产，不能作为完整工业内容验收。

## 后续执行

关闭当前占用 `D:\GitHub\Kingdom\Kingdom` 的 Unity 进程后，执行：

```powershell
& 'D:\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'D:\GitHub\Kingdom\Kingdom' `
  -executeMethod Kingdom.EditorTools.GlobalEconomyMigration.RunFromCommandLine
```
