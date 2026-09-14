# Serialized Pair policy

## Current state

Keep the asset representation separate from the runtime API:

- `ResourceAmountDefinition` serializes `resource` and an `amount` string in
  definition assets. This is an existing compatible representation, not a defect.
- `ResourceAmountDefinitionList` converts and caches those values as
  `Pair<Resource, ExpantaNum>` for building requirements, generation/consumption
  and research costs. The implementation is in
  `Assets/Resources/Script/Misc/Tool.cs`.
- Do not introduce `Pair<Resource,string>` runtime APIs, repeat the completed
  Pair migration, or rewrite valid asset amounts to satisfy an obsolete guide.

## Use Pair when

- the value is exactly two items;
- the owner/list name provides sufficient meaning;
- no additional field, unit, invariant or behavior is required;
- structural equality is appropriate.

## Use a dedicated type when

- a third field is required;
- units or validation differ;
- behavior is attached;
- save schema/versioning needs explicit names;
- Inspector clarity would otherwise be poor.

## Compatibility

- keep serialized fields named `first` and `second`;
- preserve Pair equality/hash/deconstruction behavior;
- do not expose mutable public fields;
- Save DTOs use named fields, not Pair;
- any future serialized field-type change requires an Editor migration and round-trip test.
