# Runtime script scope

These instructions apply to `Assets/Resources/Script/**` and supplement root
`AGENTS.md`; start through the root entry and project-dev router. Global State,
transaction, reflection, inventory, math and serialization constraints are inherited,
not redefined here.

- Avoid public writable fields; Inspector references use `[SerializeField] private`.
- Avoid LINQ, closures, repeated Parse/GetComponent and temporary collections in simulation hot paths.
- Use explicit `deltaSeconds`; do not create permanent per-resource/building/research gameplay coroutines.
- Do not add BigNumber compatibility aliases or `Pair<Resource,string>` runtime APIs. Preserve the asset/runtime distinction in `docs/architecture/serialized-pairs.md`.
- Keep source text UTF-8.
- When touching UI code, follow the UI skill and `docs/architecture/ui-boundaries.md`; preserve centralized refresh and authored layout. Do not revive legacy Viewer migrations or perform unrelated layout/refresh refactors.
