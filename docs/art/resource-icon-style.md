# Resource Icon Art Specification

## Purpose

This is the visual baseline for Kingdom resource icons. It applies to existing-era, Industrial, Spacer, and future resource icon replacements unless a specific resource requires an explicit exception.

## Composition

- Use the existing stone icon as the composition reference: one centered object, clear silhouette, generous transparent safety margin, and a slight three-dimensional presentation.
- Keep the main object large enough to remain recognizable when displayed at approximately 48-72 UI pixels.
- Use a simple, readable arrangement with a small number of meaningful parts. Do not add a scene, environment, ground plane, or decorative background.
- Preserve the resource's established shape language whenever it is already recognizable. Change the shape only when the old resource is semantically unclear.

## Rendering style

- Clean stylized game-resource illustration: crisp outer contour, controlled planes, limited highlights, and restrained shadowing.
- Colors may be bright and clearly differentiated, but must remain unified and practical for a general-purpose resource list. Avoid neon saturation, excessive gradients, glitter, noisy texture, and photorealistic rendering.
- Materials must be distinguishable through shape and a few characteristic surface cues rather than dense detail.
- No text, numbers, logos, labels, watermark, or visual effects unrelated to the resource.

## Material and shape cues

- Ceramic: square or block-like fired pieces, pale ceramic tones, clean hard edges; do not depict a pottery vessel unless the source resource specifically calls for one.
- Glass: transparent or translucent block/bottle language with restrained reflections.
- Concrete: heavy gray construction block or slab with a solid aggregate feel.
- Coke: black, irregular carbon chunks with a dry matte surface.
- Machinery: compact mechanical structure with visible gears, housings, and bolts.
- Engine: recognizable industrial power device with a body, cylinders or power core, pipes, and a flywheel or equivalent mechanical feature; it must not look like a generic gear pile.
- Electronics: circuit-board or component arrangement with controlled cyan/blue accents.
- Copper wire: clearly visible copper-colored coil or bundled conductor.
- Crude oil, lubricant, refined fuel: distinguish liquid, container, and fuel color/shape language; lubricant must read as a filled container, not a stone sculpture.
- Rubber: rounded dark rubber material with a deliberate manufactured form; avoid an ambiguous rock-like silhouette.

## File and UI requirements

- Runtime resource PNGs are 256x256 RGBA with transparent corners. Keep any higher-resolution master separately when available; do not re-render merely to change runtime size.
- When converting an approved icon to another size, resize the existing image directly with alpha-preserving high-quality resampling. Do not use image generation or redraw unless the user explicitly requests a new design.
- Resource-card icons use an integer 72x72 layout size. Research requirement icons use an integer 56x56 layout size. Preserve aspect ratio.
- Keep resource texture compression disabled for the approved icon set when clarity is prioritized. Reassess memory/build impact before extending this setting to new large asset groups.
- Validate at the actual small UI scale: silhouette clarity, edge quality, transparent fringe, color separation, and consistency with adjacent resources.
