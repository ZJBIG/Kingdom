# Collection UI validation — 2026-10-12

Input: main at `6f2d606`, including the existing uncommitted button/background/polish worktree and this collection change. No Manager, definition, save format, main scene or ProjectSettings changes.

- Unity 2022.3.62f3c1, actual authoring/compilation: `KingdomCollectionAuthoring.Author`, final r4 process exit 0.
- Actual PlayMode: `collection-final-playmode-20261012.xml`, 10 passed, 0 failed, 0 skipped; Unity process exit 0.
- Actual EditMode: `collection-final-editmode-20261012.xml`, 26 passed, 0 failed, 0 skipped. Unity stalled in native/network exit cleanup after writing XML; the wrapper stopped only its own process after 180 seconds. This is a completed test result, not a normal Unity process exit.
- `git diff --check` passed after trimming only newly generated Prefab diff whitespace; dynamic font cache restored to the pre-run input. No fixture save directories or Unity process remain.

PlayMode covers resource/building/workshop/sector/sector-building/research pins, sorting/cancel/page persistence, independent pointer click and real detail pin raycast, unchanged purchase/building counts, real sector menu expansion and reflow, recursive multi-source production expansion/collapse/detail navigation and drag displacement, original research graph gestures/clipping, unified detail scrolling, workshop benefit consistency, and material/music refresh.

EditMode covers authored controls/ellipsis and seven era palettes, typed pin preferences, multiple producers, production inputs versus construction costs, path cycles/shared inputs, recurring versus one-time sector sources, and the existing background/button/phone configuration contracts. Navigation links have explicit underline/font/raycast assertions; material buttons retain the original material assertions.

15 actual Canvas screenshots: resources, buildings, workshop, sectors and Steel production tree at 2640×1200, 2200×1000, 1760×800 (all 11:5). Representative images from every view and all sizes were inspected. Screenshot fixtures use isolated saves and public Editor setup APIs to expose populated lists; they are UI evidence, not natural gameplay progression evidence.

`unity-evidence.zip` contains the final XML/logs, final author log, and the focused pin-raycast diagnostic. Source/prefabs/tests are in the same Git delivery. First failures and their fixes are documented in `.codex/handoffs/2026-10-12-button-redesign-handoff.md`.

Not run: full repository suite or physical touch hardware. Production tree rates are per-definition base rates; the preview does not solve total throughput or choose an optimal route.
