# Definition database

`ResourceDef`, `BuildingDef`, and `ResearchDef` inherit from `GameDefinition` and are indexed by a stable `Id`.
Definitions must live below `Assets/Resources/Datas`.
Every concrete or intermediate class derived directly or indirectly from `GameDefinition` uses the `Def` suffix.
The base `GameDefinition` keeps its name. Script filenames match their classes; renaming a script moves its existing `.meta` with it and preserves asset GUIDs, stable IDs, serialized field names and resource directories.

## Common usage

```csharp
ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
BuildingDef farm = DataBase<BuildingDef>.Find("Farm");
ResearchDef agriculture = DataBase<ResearchDef>.Find("Agriculture");
```

`SectorBuildingDef` is a `BuildingDef` subtype, not a wrapper around another
building asset. It keeps the inherited building fields and adds its
`Sector`, `MaxAmount` and optional `RequiresInteriorExploration` gate. Therefore `DataBase<BuildingDef>.All` includes the same
asset instance, while the Buildings page filters the subtype out and the
occupied sector menu is its player-facing entry point.

`Find` is appropriate when the definition is required. It throws a descriptive `KeyNotFoundException` if the ID is missing.

For optional definitions:

```csharp
if (DataBase<ResourceDef>.TryFind("WoodLog", out ResourceDef wood))
{
    // Use wood.
}
```

Enumeration and checks:

```csharp
IReadOnlyList<ResourceDef> resources = DataBase<ResourceDef>.All;
int resourceCount = DataBase<ResourceDef>.Count;
bool hasWood = DataBase<ResourceDef>.Contains("WoodLog");
```

IDs are compared without case sensitivity, but code should use the canonical spelling stored on the asset.

## Adding a definition

1. Create the ScriptableObject below `Assets/Resources/Datas`.
2. Give the asset its intended stable English identifier as the asset name.
3. Run `Tools > Kingdom > Definitions > Assign Missing IDs From Asset Names`.
4. Run `Tools > Kingdom > Definitions > Audit Stable IDs` before committing.

The command-line equivalent is:

```powershell
powershell -ExecutionPolicy Bypass -File tools/codex/apply-definition-ids.ps1
```

Asset filename, Unity asset name, ID and Label must describe the same concept. When a concept changes, update its ID and code references together. The current pre-1.0 workflow discards incompatible saves instead of preserving misleading IDs or adding aliases and migration mappings.

The database loads each definition type once and then performs dictionary lookups. Repeated lookups are inexpensive, but systems that use the same definition in a hot simulation loop should still keep a local reference.
