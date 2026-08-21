# Definition database

`Resource`, `Building`, and `Research` inherit from `GameDefinition` and are indexed by a stable `Id`.
Definitions must live below `Assets/Resources/Datas`.

## Common usage

```csharp
Resource gold = DataBase<Resource>.Find("Gold");
Building farm = DataBase<Building>.Find("Farm");
Research agriculture = DataBase<Research>.Find("Agriculture");
```

`Find` is appropriate when the definition is required. It throws a descriptive `KeyNotFoundException` if the ID is missing.

For optional definitions:

```csharp
if (DataBase<Resource>.TryFind("Gold", out Resource gold))
{
    // Use gold.
}
```

Enumeration and checks:

```csharp
IReadOnlyList<Resource> resources = DataBase<Resource>.All;
int resourceCount = DataBase<Resource>.Count;
bool hasGold = DataBase<Resource>.Contains("Gold");
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

Once an ID is used in code or a save file, renaming the Unity asset or changing its display `Label` is safe, but changing the stable `Id` is a data migration and should be intentional.

The database loads each definition type once and then performs dictionary lookups. Repeated lookups are inexpensive, but systems that use the same definition in a hot simulation loop should still keep a local reference.
