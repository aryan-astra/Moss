# Character packages · format 1

A character is a portable folder containing **`character.json`**. In this format all visuals and animation assets are inspectable numeric data for the `bean-1` procedural rig; no binary art dependency is missing. Copy the included `characters/moss/character.json` to begin editing, and import it from Settings → Character. Imports are validated then reserialized into `%LOCALAPPDATA%\Moss\characters\<random-id>\character.json`.

The runtime reads at most 256 KiB and limits JSON nesting to 16. No archives are extracted and no paths/URLs, executable code, sound recordings, shader programs or script hooks are loaded from a character. Unknown rig/version values are rejected. Additional unknown JSON properties are ignored for forward-compatible metadata, but have no runtime effect. Removing required clips is an error.

## Top-level fields

JSON property names are case-insensitive on load; standard examples use camelCase.

| Field | Allowed value / meaning |
|---|---|
| `formatVersion` | `1` |
| `name` | Nonblank, up to 40 characters |
| `rig` | `"bean-1"` only |
| `body`, `belly`, `ink`, `accent` | Exactly `#RRGGBB`; no gradients or external assets |
| `width` | 40–90 rig-local DIPs |
| `height` | 40–100 DIPs |
| `earLength` | 0–35 DIPs |
| `headphones` | Boolean; enable the dance accessory |
| `sound.frequency` | 100–2000 Hz; original synthesized pop's starting frequency |
| `sound.decay` | 20–150; exponential decay coefficient |
| `sound.volume` | 0–0.15 of signed PCM full scale, bounded to avoid extreme loudness |
| `personality.curiosity` | 0–1; environment investigation and climbing |
| `personality.playfulness` | 0–1; play and dance utility |
| `personality.sociability` | 0–1; attention to nearby cursor |
| `personality.calmness` | 0–1; sitting/rest utility |
| `animations` | Object mapping exact case-sensitive Motion names to clips; at most 40 entries |

Sound effects remain user-opt-in even when a character defines sound parameters. The user can override personality in Settings. Physical constants and security permissions belong to the engine, not untrusted content.

## Required clips

```
Idle Walking Running Jumping Falling Climbing Sitting Sleeping Waking
Looking Reacting Dancing Petted Grabbed Held Thrown Airborne Impact
Recovery Startled Hiding Investigating Celebrating
```

A clip describes a procedural pose and its movement envelope, not a list of static image filenames. The engine owns the joint hierarchy and interpolation; content owns the parameter values.

| Clip field | Meaning |
|---|---|
| `rate` | Oscillator cycles/second, >0 and <=30 |
| `bob` | Vertical movement amplitude in DIPs, absolute value <=30 |
| `lean` | Base body rotation in degrees, absolute value <=30 |
| `crouch` | Deformation, -0.3 (stretch) to 0.65 (crouch) |
| `eyes` | Openness from 0 to 1 |
| `ears` | Ear pose coefficient; absolute value <=30 (small values look best) |
| `arms` | Arm pose coefficient; absolute value <=30 |
| `duration` | Loop period / one-shot duration in seconds, >0 and <=30 |
| `loop` | Repeat clip timing/markers or stop one-shot timing |
| `markers` | Up to 16 normalized marker positions, 0–1; crossing emits an engine animation event |

Use the original authored values as practical ranges; values near validator limits can be artistically unattractive or clipped despite being safe. Animation dimensions are bounded for safety, not guaranteed silhouette fit for every combination. The overlay has 180 DIPs of canvas; extreme combinations of height, ears, lean and arms can leave it. The default Moss pack is intentionally sized to fit.

Clip changes blend exponentially. Locomotion speed adds tilt; impact, balance, cursor gaze, blinking, breathing and ear movement add context. A state's one-shot completion does not choose a new behavior by itself: the simulation's timers and physical priorities own that decision. Grab, throw, impact and waking have protected minimum durations unless urgent physical interaction interrupts. `Celebrating` markers can trigger the configured sound when the user's sound permission is enabled. Other markers are emitted but have no arbitrary package-defined side effects.

Example modification (retain all other fields/clips from the original file):

```json
{
  "name": "Fern",
  "body": "#91AD9C",
  "accent": "#DCB185",
  "headphones": true,
  "personality": { "curiosity": 0.9, "playfulness": 0.4, "sociability": 0.5, "calmness": 0.8 }
}
```

This fragment is **not a complete package**; the fully runnable reference is `characters/moss/character.json`. `character.schema.json` documents the field constraints for authoring tools. The C# validator is the runtime authority. No editor or third-party skeletal/sprite format is included. A new engine-authored rig could extend the allowlist and renderer factory in a future format without giving packages code-execution permission; that extension is not implemented here.

## License

The original Moss rig parameters, icon drawings and sound design are MIT licensed with the project. Creators can choose their own license for newly authored content; add a human-readable `LICENSE` beside the JSON. The current importer copies the JSON only, so distribute your license with the original pack as well.


## Expanded renderer fields (schema 1, backward compatible)

`species` is one of `bean`, `cat`, `dog`, `bird`, `octopus`, `rabbit`, `penguin`; these select engine-authored anatomy. `paper` is a validated #RRGGBB notebook background. `musicProp` is `headphones`, `radio`, `turntable` or `cassette`. Default absent values retain the original leaf-bean pack. See each of the seven complete character folders for usable examples.

The shared format version is retained because these are optional fields with safe defaults, not incompatible serialization changes. `rig` remains the historical engine rig-family identifier `bean-1`; the species option adds explicitly supported shapes within that renderer. It is not evidence of arbitrary skeleton/script import. Hats and glasses are user profile options rather than permanent character pixels.
