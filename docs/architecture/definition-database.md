# Definition database

`Resource`, `Building`, and `Research` inherit from `GameDefinition` and are indexed by a stable `Id`.
Definitions must live below `Assets/Resources/Datas`.

## Common usage

```csharp
Resource wood = DataBase<Resource>.Find("WoodLog");
Building farm = DataBase<Building>.Find("Farm");
Research agriculture = DataBase<Research>.Find("Agriculture");
```

`SectorBuilding` is a `Building` subtype, not a wrapper around another
building asset. It keeps the inherited building fields and adds only its
`Sector` and `MaxAmount`. Therefore `DataBase<Building>.All` includes the same
asset instance, while the Buildings page filters the subtype out and the
occupied sector menu is its player-facing entry point.

`Find` is appropriate when the definition is required. It throws a descriptive `KeyNotFoundException` if the ID is missing.

For optional definitions:

```csharp
if (DataBase<Resource>.TryFind("WoodLog", out Resource wood))
{
    // Use wood.
}
```

Enumeration and checks:

```csharp
IReadOnlyList<Resource> resources = DataBase<Resource>.All;
int resourceCount = DataBase<Resource>.Count;
bool hasWood = DataBase<Resource>.Contains("WoodLog");
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
