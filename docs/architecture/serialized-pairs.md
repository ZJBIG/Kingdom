# Serialized Pair policy

## Current state

Kingdom3 has completed the canonical migration to `Pair<Resource, ExpantaNum>` for:

- building construction requirements;
- building generation rates;
- building consumption rates;
- research resource costs.

Do not repeat this migration and do not reintroduce string amounts.

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
