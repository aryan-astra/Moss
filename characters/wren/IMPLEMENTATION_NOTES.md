# Wren — experimental bird (implementation notes)

Wren is an additional first-class character. No existing character was replaced or modified.
Select it via Customize (the gallery enumerates every `characters/*/character.json`
automatically), or via Feature Lab → Music → "Wren pet". What you see on screen is the
supplied atlas artwork — not a vector redraw. Nothing was traced or restyled.

## 1. Atlas analysis (visual source of truth)

- Source: `Moss Bird Asset .png`, **2816×1536**, `Format24bppRgb`, ~7.0 MB.
- **No alpha channel**: the checkerboard is baked pixels (light ≈ (224,224,224),
  dark ≈ (199,199,198), 24 px period, ±3 noise). Raw cells cannot be composited.
- 8 semantic bands confirmed by projection analysis (art-band heights 108–170 px,
  8 baked label strips skipped): core (~20 cells), wing (15), expressions (27 with
  eye tiles), physical (17), desktop (17), window+object (17), house&music (17),
  components (13). 135 candidate boxes detected; 42 selected (see `atlas.json`).
- Detection: strict two-tone background model → connected components → fragment
  merge (4 px expansion) → watershed split of wide boxes at emptiest vertical cut.
  Every selected cell was verified on a labeled overview before packing.
- Skipped, not forced in: baked cursor arrows/hands (physical row), baked
  planks/walls/ledges/windows/houses/balls/tools, the heads/eyes row (no bodies),
  near-duplicate strides/flaps, the left-facing glide (runtime mirrors
  right-facing art via `Facing`), one ambiguous edge cell. Full list in `atlas.json`.
- Atlas is right-facing only; left-facing is runtime mirroring, same as the engine.

## 2. Packed runtime asset

- `characters/wren/atlas.png`: **one** 1024×793 32-bit texture with real alpha
  (~0.9 MB on disk, ~3.2 MiB in memory), 42 sprites (28 motions; walk/flap/dance/
  throw/carry/hammer/sleep/investigate multi-frame).
- Cleaning per cell: 3 px padded crop → border-flood background removal (interior
  whites such as eye highlights survive: flood cannot reach them) → rim
  decontamination (coverage estimated from checker distance, color unblended
  against the local checker tone) → trim to content + 1 px pad, feet-anchored
  bottom-center. Verified sprite by sprite against magenta.
- `characters/wren/atlas.json`: packed layout (`png`, `width`, `height`, `cells`,
  per-frame source provenance) plus the analysis docs, per-motion `frames` table,
  and limitations. The 7 MB raw source is never sampled at runtime and is not
  shipped with the character.

## 3. Architecture reuse (engineering source of truth)

| Concern | Existing system reused | Wren-specific data |
|---|---|---|
| Character data | `Moss.Core/Character.cs` + schema (now allows optional `frames` per clip and `spriteAtlas`) | `character.json` (species `bird`, 28/28 clips validate, frame keys per clip) |
| Atlas metadata | new optional `Character.SpriteAtlas` block (nullable, backward compatible) | `atlas.json` + `atlas.png`, loaded lazily for the active character only |
| Rendering | `CreatureRenderer` (GDI+, one reused overlay bitmap) | sprite branch inside the existing species path: same Pose transforms (bob/lean/crouch), same wearables/effects; falls back to procedural art if the sheet is absent |
| Animation | `Animator` (Pose blend, gait, markers, one-shot/loop) | `Clip.Frames` sequenced on existing clocks — gait cycle for locomotion (speed-scaled), time/duration otherwise; `rate`/`duration`/`AnimationSpeed` rescale timing with no new artwork |
| Physics | `Creature` fixed-step integrator + surfaces | none — velocity/support/impact drive `Motion`, animation reacts |
| Cursor | `PetOverlay` grab/release/click/stroke + `PettingGesture` + `SpecialClick` | headphones hit-test path (`musicProp: headphones`) |
| Window/world/DPI/monitors | `WorldObserver`, `ScaleAt × PetScale`, `Displays` | none — inherited automatically; sprites drawn under the same `ScaleTransform`, sharp to 200%+ |
| Selection | Customize gallery enumeration | none — directory picked up automatically; no hard-coding |
| Testing | Feature Lab (movement/physics/climb/build/objects/emotion/music/animation) | one additive "Wren pet" music-device button; Animation page already lists all 28 motions |

## 4. Motion, not a slide show

`IDLE → LOOK → WALK → JUMP → FALL → LAND → IDLE` flows through the production
`Creature.SelectMotion` state machine; `Animator` blends `Pose`
(Bob/Lean/Crouch/Eyes/Ears/Arms) at `BlendRate` while `FrameIndex` steps atlas
keyframes (e.g. 3-frame walk on `GaitCycle`, 3-frame flap on clip time). No image
is rendered per frame; keyframes are states, motion between them is interpolation.

## 5. Physics drives the visuals (examples)

Falling follows gravity velocity (`Falling` flap while airborne fast); landing
impact force selects the flare `Impact` then touchdown `Recovery`; bounce keeps
momentum with damped reflection; drag uses the grab spring (`Held` neutral body
leans with the cursor); release clamps to throw power (`Thrown` tumble retains
momentum); lean/balance react to acceleration and surface shifts; climb uses the
real `ClimbSession` (`Climbing` flutter pair, `Hanging` upside-down grip,
`Peeking` alert, `Balancing` wobble).

## 6. Performance

No AI, no web/browser rendering, no video textures, no per-frame allocations of
note. One packed sheet loads lazily when Wren becomes active and is disposed with
the renderer on switch — never hundreds of files, never a desktop framebuffer
pass. Reference footprint is orders of magnitude below per-cell textures
(42 files × ~130 px); overlay bitmap size is unchanged (fixed 180×scale surface).

## 7. Verification

- `dotnet build Moss.sln` + `dotnet run --project tests/Moss.Tests` (covers Wren).
- Headless harness: 30 checks — selection, 28/28 frame coverage, transition chain,
  physics, cursor, climb, all 28 motions drawn at 100%/150% both facings, sprite
  pixels provably on screen (differ from procedural fallback, atlas palette
  present), Lark byte-identical before/after.
- Gallery renders of Idle/Walking/Jumping/Falling/Dancing/Sleeping/Hammering/
  Carrying eyeballed against the source art.
