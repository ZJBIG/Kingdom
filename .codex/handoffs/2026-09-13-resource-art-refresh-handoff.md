# Resource art refresh handoff

## Status

Replaced 44 existing resource textures with generated RGBA PNGs, then resized those same 44 files directly from 512x512 to 256x256 for runtime use. Scope covers 14 Industrial resources, 20 pre-Industrial resource textures, and 10 Spacer resource textures. Existing `.meta` files, ScriptableObjects, stable IDs, scenes, and code were not changed.

The follow-up clarity fix disables texture compression for every resource texture platform entry and enlarges the authored resource-card icon from 68x68 to 72x72 plus runtime research-requirement icons from 48x48 to 56x56. The current art and sizing baseline is documented in `docs/art/resource-icon-style.md`.

## Validation

- All 44 target files were located uniquely and replaced.
- The 44 replacement files validate as 256x256 images after direct resizing; alpha was preserved during conversion.
- Green chroma-key backgrounds were removed before replacement.
- `git status` confirms only the intended resource PNGs changed among this task's files; unrelated pre-existing worktree changes were preserved.
- All 67 resource texture `.meta` files now use `textureCompression: 0` for Default, Standalone, WebGL, and platform entries.
- Resource card and research requirement UI icon sizes remain integer-pixel values and preserve aspect ratio.

## Remaining risks

- Unity import/visual verification has not been run in this handoff.
- Uncompressed 256x256 RGBA textures increase memory/build size compared with compressed textures, but substantially less than the former 512x512 runtime version.
- Some generated icons may need individual artistic review at the in-game 64px scale.

## Next action

Open the resource list and research detail panel in Unity and perform a visual pass at the target UI scale; if still soft, inspect the Canvas scale and pixel alignment before changing source art. Follow `docs/art/resource-icon-style.md` for subsequent resource work.
