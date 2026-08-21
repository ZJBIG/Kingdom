# Runtime script scope

These instructions apply to `Assets/Resources/Script/**`.

- Preserve Runtime State as the only mutable authority.
- Managers validate and mutate State; UI only binds and issues commands.
- Do not reintroduce BigNumber or `Pair<Resource,string>`.
- Avoid public writable fields; Inspector references use `[SerializeField] private`.
- Avoid LINQ, closures, repeated Parse, reflection, repeated `GetComponent` and temporary collections in simulation hot paths.
- No permanent per-resource/building/research gameplay coroutine.
- Resource satisfaction must use exact per-tick availability and remain deterministic.
- Resource and food inventories never finish below zero.
- Build/deconstruct/research payment validate first and commit second.
- Do not add gameplay/UI convenience APIs to ExpantaNum.
- Preserve `.meta` files and UTF-8.
- When touching Viewer/Displayer code, follow the UI skill and remove manual layout/refresh ownership rather than moving it elsewhere.
