# Moss 1.3 — technical architecture and source map

**Document scope:** actual source in this distribution, including retained systems and the 1.3 changes. Research proposals are explicitly separated in `BRAINS-RESEARCH.md`. This is not a declaration that the original master brief is fully implemented or that the program is error-free.

## 1. Product boundaries and evidence

Moss is one local Windows desktop creature with native layered windows, procedural graphics, fixed-step physics, a small behavior controller, shared local notes, confirmed reminders, media reactions and physical props. It is not a chatbot or a desktop-control agent. There is no account, telemetry, online inference, model download, browser scraper, microphone input, automatic screenshot capture or arbitrary plug-in execution.

The user has reported successful operation of the prior app, including animation and climbing. That is useful external evidence, but it does not certify every new path. In particular, **there is no house-construction routine in this source**. A reported resemblance to construction must not be turned into a claim of implemented house building.

The 1.3 work adds output-level fallback, a music card, species music-device variation, hearts/notes/comments, proportion-preserving dance, occasional physical hops, revised grip forces, a football, an explicit settings-save action and additional regression coverage. It does not add a complete 100-moment authored animation library, visual screen understanding or a trained neural policy.

### Evidence categories

- Core tests establish deterministic/pure-model behavior for their covered cases.
- The renderer harness executes production drawing code under Linux/libgdiplus, not Windows.
- WinForms/GDI component and application exercises under Wine establish compatibility observations, not native Windows integration acceptance.
- Cross-compilation and ZIP checks establish build/distribution properties, not end-user hardware performance.
- Real Chrome/Spotify playback, audio-device switching, multi-monitor/DPI interaction, scheduled reminder delivery and extended Windows resource stability remain unverified in this environment.

The current logs and acceptance record accompany the build. Old evidence images/notes remain labeled by version; they are not proof of new behavior.

## 2. Technology decisions

### Why C#/.NET 8 and WinForms?

The program needs Win32 input/window management, DWM geometry, Windows media sessions, Core Audio and Task Scheduler. C# provides direct P/Invoke/COM/WinRT integration and a small maintainable source surface. WinForms supplies real text editing, IME, selection, accessibility infrastructure and conventional dialogs without a browser engine.

.NET 8 is the existing pinned project toolchain, not a claim to be the newest runtime or the smallest possible deployment. The self-contained archive includes its runtime so the user need not install an SDK. Servicing and future runtime migration still matter; a bundled runtime is a maintenance obligation. The distribution is unsigned.

### Why not Electron, a game engine, or WPF?

A browser shell is unnecessary for this UI and would introduce another runtime and rendering/input boundary. A full game engine would provide better authoring tools and sophisticated animation/physics but complicate transparent desktop windows and increase the deployment/resource surface. WPF could improve styling but would not solve the Win32/media/terrain constraints by itself. These alternatives are not inherently bad; the choice prioritizes native integration and a contained implementation over an advanced content-authoring workflow.

### Why procedural vector art?

It scales without a large frame atlas and supports continuous joint/pose variation. Seven characters can share rendering infrastructure with different proportions, tails/ears/wings, palettes and music devices. It is cheap to package and inspect.

The tradeoff is significant: parameterized shapes are not a substitute for premium independent art direction and animation. The current rigs remain simple. A better authored skeletal asset workflow or hand-drawn frame library may ultimately be appropriate. Generating hundreds of unrelated images would not automatically provide temporal consistency, correct contacts or attractive movement.

### Why a utility controller rather than an LLM?

This creature needs timely legal actions, not conversation. Scores, cooldowns and explicit physical state are understandable and bounded. They can still produce repetitive or awkward behavior; those faults require better action design and evaluation. A small learned policy is feasible but is only researched in this release. No language model is needed for the fixed, rare music compliments.

### Why GDI+ and layered windows?

The creature occupies a small local rectangle rather than a desktop-sized transparent surface. GDI+ draws an alpha bitmap; `UpdateLayeredWindow` submits premultiplied BGRA. Persistent DIB/DC ownership avoids creating a native bitmap and DC on every frame. This is a pragmatic small-surface renderer, not a GPU engine or vblank-synchronized swap chain.

## 3. Solution boundaries

```
Moss.Core (net8.0)
  world records · simulation · animation · props · settings/content validation
  note/reminder storage and grammar · media selection · level hysteresis

Moss.Windows (net8.0-windows10.0.19041.0, WinForms, WinExe)
  process/tray lifecycle · Win32/COM/WinRT adapters · rendering/windows
  native note editor · preferences · music card · notifications/scheduler

Moss.Tests
  console regression harness over Core

Moss.RenderChecks
  production renderer linked into Linux/libgdiplus test executable

Moss.WindowsChecks
  linked production dropdown / RichEdit / DIB / output-meter lifecycle checks
```

Core does not depend on WinForms. It is not entirely dependency-free: the retained Markdown utility uses Markdig. Windows references Core and NAudio.Wasapi. Windows SDK projections supply WinRT types. The Linux renderer test's older System.Drawing.Common reference is test-only and must not be copied into the production Windows dependency graph.

## 4. Startup, lifetime and ownership

`Program.Main` takes a named, per-session mutex. A second ordinary launch signals the existing instance rather than spawning another pet. A `--reminder` launch exits if an instance already exists. High-DPI mode and WinForms exception handlers are set before the application context runs.

`PetApplication` owns configuration, the active character/creature/animator/renderer, world and media observers, reminder service, sounds, tray menu, primary overlay, prop controller and optional forms. Settings are loaded with validation; invalid settings are preserved before fallback. Notes are loaded independently and are not reset with pet settings.

The frame pump posts coalesced callbacks to the UI thread. On normal exit, the notebook must save first; a failed note save blocks exit. The timer stops, settings/memory are attempted, forms close and owned resources are disposed. An idempotent application resource guard prevents duplicate disposal when WinForms and the surrounding `using` both dispose the context. Async media callbacks check revocation/disposal state. Notification enable completion now avoids saving into a disposed application.

Logs are bounded/rotated and deliberately record exception type/HRESULT rather than private text or exception messages. Logging itself is best effort. A generic frame/render exception hides the overlay and exposes recovery; reminder update I/O failures are now isolated so they do not masquerade as a renderer failure.

## 5. Scheduling and the frame pipeline

### Frame pump

`FramePump` creates a high-resolution waitable timer when available, falling back to an ordinary waitable timer. A background thread waits; an atomic pending flag allows at most one posted UI callback. It does not globally change the Windows timer period. Stop/rearm operations are serialized so shutdown cannot race a new wait. Disposal is guarded against duplicate calls.

This is pacing, not a guarantee of hardware refresh/vblank alignment. Windows scheduling, GDI+, DWM, UI messages and system load can delay a frame.

### UI-thread tick

1. Handle instance-activation/recovery signal.
2. Reconcile world/media state and decay measured onset accents.
3. Reconcile window events only when world revision changes.
4. Evaluate visibility policy and update the tray reason.
5. Advance a bounded accumulator with 1/120-second simulation steps, capped at 30 steps per callback.
6. Step the creature, select/blend animation, then step props.
7. Draw the creature if visible; update prop windows.
8. Record metrics and choose the next interval.

Paused behavior does not run autonomous creature decisions. Direct manipulation remains available. Hidden rendering stops, but periodic state reconciliation and bounded simulation can continue.

Balanced caps at 60 Hz; resting states cap at 24 Hz; Battery caps at 30 Hz; sleep/hidden paths are lower frequency. Smooth follows the nominal display rate up to the configured cap. Adaptive throttling uses measured process CPU, not a fabricated GPU/FPS target. There is no verified 240 Hz input-latency guarantee.

### Thread boundaries

Simulation and drawing are UI-thread-owned. WinRT continuations return through the UI synchronization context; event notifications increment a revision counter. WASAPI callbacks compute a scalar onset pulse without accessing controls. Output peak-meter queries are synchronous, rate-limited UI-thread calls. Their real-device worst-case latency still needs native profiling; this should be revisited if audio service stalls cause UI delay.

## 6. Coordinates, terrain and physics

All world geometry is expressed in physical virtual-desktop pixels. The creature position is its feet. Scale combines monitor DPI and the selected pet size. Negative monitor coordinates are allowed. A display contains bounds, work area, scale and nominal refresh rate.

`WorldObserver` uses monitor enumeration and window/DWM bounds, not screenshots. It filters own-process, minimized, cloaked, tool and excluded windows, calculates exposed ledges through occlusion subtraction and adds work-area floors. WinEvent hooks mark state dirty, with periodic reconciliation as a fallback. Application identities/titles are separately permission-gated.

The creature integrates gravity, velocity, friction/damping, a grab spring, one-way swept ledge landing, rebound/recovery and support motion. A moved supporting window carries the pet using support-origin deltas. Removing support causes a fall. Monitor loss/out-of-bounds recovery can reposition the creature; this is a safety fallback, not locomotion choreography.

Window climbing selects a reachable ledge and approaches an edge, then moves upward toward it. It is **not** a general surface-graph planner, full collision hull, inverse-kinematic wall grip or screen-corner construction system. Nearby geometry can still produce unintuitive results. Browser/webpage visual content does not become terrain.

## 7. Decisions, rest and stuck handling

The utility controller scores wandering, sitting, sleeping, investigating, play, dancing, hiding, attention and climbing. Personality, energy, curiosity, attention, nearby cursor and media state affect scores. Random weighting and cooldowns vary choices. Sleep recovers energy; no feeding or chore system exists.

Dancing has explicit priority when actual media/audio activity is present and the creature is grounded, not held, not in landing recovery and not under calm policy or a toy tug. Pause stops dance. Music start can wake a resting creature. Occasional dance hops detach support and apply a real upward velocity; reduced motion and insufficient ledge clearance suppress them.

A movement-intent watchdog detects several seconds of near-zero movement while a distant target remains and requests a new decision. Nearby substantial cursor movement can wake a sleeping pet or shorten a pending decision, with cooldown. These are bounded recovery measures, not proof that every geometry/behavior deadlock is solved. Intentional sitting or sleep is not classified as a stuck bug.

Per-pet explicit memory stores mood, energy/rest, attention and save time. Time away relaxes toward neutral/rested values rather than punishing absence. Position, route plans, arbitrary events and a rich autobiographical story are not persisted.

## 8. Animation, anatomy and effects

`Animator` blends a six-component pose: bob, lean, crouch, eyes, ears and arms. It holds 23 named motion states, honors minimum timing for selected one-shots and emits authored marker crossings. Gait phase advances from normalized velocity × elapsed time; it is not simply a walking timer.

`PoseDynamics.Foot` divides a stride into grounded stance and raised swing. Body-relative foot motion approximately compensates body translation during stance. This is not full world-space foot locking on arbitrary moving/rotated terrain. Two-link arm IK clamps reach and computes an elbow with a chosen bend direction.

The 1.3 dance override limits crouch to a small range rather than shrinking the whole body. Sway and arms counterpose; real hops remain physics-driven. Different species still share much of the state logic. The requested independently authored 100+ meaningful moments are not present.

`CreatureRenderer` owns cached brushes, pens, fonts, gradient and a reusable bitmap. `SpeciesRig` draws anatomy/music accessories. `CreatureEffects` draws bounded hearts, floating vector music notes and rare short random compliments. No unbounded particle list exists. Reduced motion lowers effect count/motion and disables automatic dance hops, but it is not a complete no-motion accessibility mode.

Music devices are visual rig attachments: Moss/Miso use headphones, Pip a CD player, Clover a cassette player, Lark/Puck radios and Inky turntables. They do not synthesize or replace the user's music. CD shine is decorative rotation, not a measured beat. Only opt-in WASAPI onsets produce audio-derived pulse accents.

The overlay alpha includes decorations; hit testing is not a separate skeletal collision mask. This can make a visible decorative pixel part of the overlay hit surface. A separate click-through effects layer would be needed to strictly separate all decoration input from body input.

## 9. Props and ownership

### Twig

`PropBody` has Carried, HeldByUser and Free states. A carried twig follows a grip point. Dragging uses a damped spring. While contested, a smoothed hand velocity and acceleration/shock estimate accumulate strain. Slow displacement alone no longer breaks the grip. Repeated violent reversals or a sufficiently sharp pull can release it once.

The creature is pulled by a bounded, damped spring toward the grip. Upward dragging can detach support through `LiftForGrip` without injecting a full jump. Arm IK reaches toward the actual toy position. When released as a free object, the toy falls/bounces against simplified ledges; local seeking and proximity can recover it. Recovery is not route-aware across disconnected monitors/windows.

This is a stylized constraint model, not a force sensor or a full rigid-body joint solver. Thresholds are normalized for scale, not calibrated to physical Newtons or every mouse polling rate.

### Football

A separate `Football` model rolls in from a work-area side, integrates gravity and bounce, rebounds at work-area edges, receives a kick impulse near the creature and expires after a bounded lifetime. The pet can seek it when not busy dancing/held/tugging. It can also be directly dragged.

Automatic arrivals are infrequent, gated by the surprise preference and reduced motion/visibility conditions. Pet → Roll a football provides explicit access. The ball is one temporary prop, not a complex sports AI, opponent or goal system. Its edge handling is confined to the nearest work area rather than seamless multi-monitor ball navigation.

### Native presentation

The twig uses a 48-pixel logical canvas; the book 36; the ball 30. The book is a clickable anchored launcher, not the held toy. `PropController` caches unchanged book pixels and only regenerates twig pixels after sufficient rotation/palette/size change. Position-only updates avoid unnecessary bitmap transfer. The ball has a small dynamically rotated drawing.

## 10. Media observation, browser fallback and music card

### GSMTC

`MediaObserver` requests the Windows session manager, reconciles every available session and uses `MediaSelection` to prefer an actually playing session. Playback events and periodic polling drive state. Metadata/artwork are optional and asynchronous; their failure must not clear known playback. Late callbacks check generation, permission and selected source.

Timeline display uses provider position/start/end and last-updated time, then short local interpolation with the reported playback rate. It is clamped to duration. Missing/invalid provider timelines are unavailable, not fabricated. Providers can still expose stale, incomplete or inconsistent metadata.

### Core Audio fallback

`OutputActivity` owns a device enumerator, selected default multimedia output device and peak-meter interface. It queries a scalar peak at approximately 8 Hz and reconciles the default device periodically. It never opens a PCM capture stream or microphone. `AudioActivity` requires sustained level before activation and holds through brief silence.

The fallback is enabled with media awareness by default in this revision and has a separate output-level setting. It covers common browser playback that does not publish a usable GSMTC session. It can also react to speech/game/system audio; it is not music classification. Non-default routes, exclusive-mode software metering, very quiet output and service/device failures are limitations. Windows documents that software peak meters can return zero in exclusive mode.

When only fallback is active, the card says audio is playing and does not display a stale paused player's title/timeline as if it belonged to the sound. Actual Chrome/device coverage still requires native tests.

### WASAPI rhythm accents

The separate opt-in `WasapiLoopbackCapture` receives output samples, calculates RMS/onsets and discards buffers. It is not required for dance and is distinct from the peak-meter fallback. There is no recording file, microphone stream, transcription or guaranteed BPM/beat tracker. Audio device failure falls back to playback reactions and retry.

### Music card and short comments

The overlay checks the approximate visible music-device region before beginning a body grab. A click there opens `MusicCard`; the tray also exposes Now playing. The card shows title/artist, a bounded cached artwork thumbnail and elapsed/duration/progress only when permitted and supplied. Its permission button enables metadata explicitly. It closes on deactivation/Esc/close and owns a short-lived refresh timer/fonts/thumbnail. No playback-control or seek command is implemented in this card.

The tiny compliments are selected from a fixed local set at infrequent random intervals while music presentation is active. They are subjective decoration, not evidence that the pet analyzed or understood a song. There is no speech audio or chat interface.

## 11. Preferences, notes and recovery

Preference changes generally save immediately; the name saves on leaving its field. Every main page now also has Save changes. Successful feedback means the settings write completed; a failed operation uses the existing error path. This is not a transactional database spanning startup registry, Task Scheduler and settings JSON.

`ChoiceControl` uses unbound enum items to avoid a binding-context reset on parenting. This fixes the previously observed incorrect displayed default. The earlier observation did not prove that every user's saved music setting had silently changed.

`NotebookForm` is a compact frameless sticky-note treatment with a narrow small-text rail, paper-grain margins, native RichEdit and keyboard/context commands. It intentionally omits a document toolbar/file menu. Text remains normal editable content. The first line identifies a note; optional RTF preserves formatting alongside a plain copy in the legacy `Markdown` field. Invalid RTF falls back to plain text. Literal old Markdown is not silently converted.

Autosave waits roughly 700 ms after edits and also runs on note changes/closing. The editor builds a replacement record and commits to disk before replacing the in-memory saved record. Failed saves keep the editor open. A normal exit is blocked if the current note cannot be saved.

`DocumentStore` flushes a temporary file, uses replacement with `.bak` for existing records, and preserves corrupted originals during backup recovery. Both encoded writes and reads are bounded at 8 MB. Encoding expansion matters: individually valid fields can serialize beyond the byte limit, and that write must fail before overwriting a readable note. The regression suite covers this case.

These local backups do not protect against disk loss. Forced process termination can lose edits inside the debounce interval. The simplified editor does not currently expose archive/export controls; archived files from previous versions remain preserved.

## 12. Reminders, startup and notifications

`NoteCommands` parses a bounded grammar for timers, reminders and alarms. It is not natural-language AI. A preview discloses exact time/offset/message; the user must confirm. Ambiguous/invalid DST wall times are rejected rather than guessed. Editing the original line does not silently reschedule an existing reminder.

`ReminderService` persists deadlines/statuses independently of notes. Due reminders can generate a pet stimulus, optional synthesized sound and a tray notification. Windows may suppress visible notifications. Due items remain available for acknowledgement. State changes now commit to storage before mutating the visible reminder record; update I/O errors are isolated from the drawing loop.

Optional Task Scheduler registration uses the current user's identity, non-elevated principal, timed/logon triggers and a fixed executable with `--reminder`. No reminder text or arbitrary command is put into the action. It does not wake a powered-off/asleep machine; missed events can be delayed. Moving the executable requires registration reconciliation. This path is not native-tested here.

Incoming third-party notification observation is different: `UserNotificationListener` needs package identity, capability and permission. The portable archive does not supply a signed installed MSIX. The observer only compares notification IDs, not body/sender text.

Startup uses the current-user Run entry for portable operation or StartupTask for a packaged application. No administrator or elevation bypass is requested. The uninstall script removes known startup/reminder registrations and keeps local data unless deletion is explicitly requested.

## 13. Resource ownership and known failure modes

| Resource | Owner | Release strategy |
|---|---|---|
| Creature bitmap, brushes, pens, fonts | CreatureRenderer | Dispose on replacement/shutdown |
| Native DIB + selected object + DC | LayeredSurface | Restore previous selection, delete bitmap/DC on resize/dispose |
| Prop bitmaps/overlay forms | PropController | Resize replacement and disposal |
| Peak-meter/device/enumerator COM refs | OutputActivity | Release on device change, failure, revocation and disposal |
| Loopback capture | MediaObserver | Unsubscribe, stop and dispose on revocation/failure/shutdown |
| GSMTC event subscriptions | MediaObserver | Reconcile removed sessions and unsubscribe on stop |
| Form timers/fonts/images | Owning forms | Dispose when forms close |
| WinEvent hooks | WorldObserver | Unhook on disposal |
| Waitable timer/thread | FramePump | Stop/wake, bounded join, close handle |
| Task Scheduler automation refs | ReminderService method scope | Release in reverse order in finally |
| Sound stream/player | Sounds | Replaced/disposed together |
| Diagnostics process handle | Metrics | Dispose with application |

Native handle counters and core tests do not prove leak freedom. Imported character replacement, async shutdown races, unsupported providers, invalid media payloads, arbitrary DPI, Windows service stalls and real long-running GDI behavior need continued native testing. The code contains guarded fallbacks, not a mathematical proof of perfect operation.

## 14. Verification and non-claims

The 1.3 regression additions cover level hysteresis, invalid levels, preserved dance proportions across all characters, affection lifetime, physical dance hops, reduced-motion hop suppression, long gentle grip movement, violent reversals, actual slow upward body lift and bounded football simulation. Existing geometry, physics, storage, permissions/policy and command tests remain.

Renderer evidence includes a 42-pose contact sheet and a 384-frame seven-rig exercise with **simulated media input**. The video is not a Spotify/Chrome recording and does not prove audio synchronization. Wine evidence includes real form/overlay execution plus component lifecycle checks. No unsupported capability is disguised as a passing hardware test.

Not delivered: a neural importer, OCR/capture perception, 100 independently authored meaningful moments, premium production-certified art, general route planning, house construction, a general physical accessory inventory, guaranteed browser metadata, universal screen-share detection, signed installation or certified zero-leak operation.

## 15. Build/distribution and change history

Use `scripts/build.ps1` for ordinary build/test/publish, or the commands in `BUILD.md`. Initial SDK/NuGet restore requires network; normal operation does not. Source includes package lock files. Self-contained x64 publishing includes character data and runtime dependencies; docs/licenses/uninstall script are copied during packaging. CI configuration exists but a workflow definition is not evidence that CI ran.

The earlier state-5 shell-notification visibility defect remains covered by regression tests. 1.1 added species/notes/reminders/profiles; 1.2 simplified the editor, revised media/session handling and reused native buffers; 1.3 addresses the feedback listed here. Old documents/evidence are retained as history with current acceptance taking precedence.

## 16. File-by-file inventory

The following inventory covers distribution source, assets, scripts, configuration and evidence; generated `bin`/`obj`, NuGet caches and Git internals are excluded. Source declarations are a navigation aid, not a substitute for the implementation and its call graph.

### `.github/workflows/windows.yml`

GitHub Actions Windows build/package workflow definition; it does not imply that CI ran for this delivery.

### `.gitignore`

Version-control exclusions for generated output and local state.

### `Directory.Build.props`

Shared C# language/nullability/implicit-usings, deterministic output, lock-file restore and cross-Windows-targeting properties.

### `LICENSE`

License/redistribution attribution. Runtime components retain their own terms; no downloaded third-party pet art is included.

### `Moss.sln`

Main solution connecting Core, Windows and core tests; specialized test projects can also be invoked directly.

### `README.md`

Current install/update/use/privacy/performance/data/build guide and links to acceptance evidence.

### `THIRD-PARTY-NOTICES.md`

License/redistribution attribution. Runtime components retain their own terms; no downloaded third-party pet art is included.

### `UPDATE.txt`

Short version-specific upgrade instructions and evidence boundary.

### `characters/character.schema.json`

External JSON schema for supported character fields, bounds and accessory/species choices. Core validation remains authoritative at runtime.

### `characters/clover/character.json`

Built-in clover data-only character: palette, dimensions, personality, 23 clip parameters, sound timbre, species and music prop. No executable code.

### `characters/inky/character.json`

Built-in inky data-only character: palette, dimensions, personality, 23 clip parameters, sound timbre, species and music prop. No executable code.

### `characters/lark/character.json`

Built-in lark data-only character: palette, dimensions, personality, 23 clip parameters, sound timbre, species and music prop. No executable code.

### `characters/miso/character.json`

Built-in miso data-only character: palette, dimensions, personality, 23 clip parameters, sound timbre, species and music prop. No executable code.

### `characters/moss/character.json`

Built-in moss data-only character: palette, dimensions, personality, 23 clip parameters, sound timbre, species and music prop. No executable code.

### `characters/pip/character.json`

Built-in pip data-only character: palette, dimensions, personality, 23 clip parameters, sound timbre, species and music prop. No executable code.

### `characters/puck/character.json`

Built-in puck data-only character: palette, dimensions, personality, 23 clip parameters, sound timbre, species and music prop. No executable code.

### `docs/ACCEPTANCE.md`

Supporting acceptance document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/ARCHITECTURE.md`

Supporting architecture document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/BRAINS-RESEARCH.md`

Researched lightweight-policy/perception/import proposal, cost arithmetic, privacy and evaluation design. Not shipped neural functionality.

### `docs/BUILD.md`

Supporting build document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/CHARACTERS.md`

Supporting characters document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/EXPANDED-ARCHITECTURE.md`

Supporting expanded architecture document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/RESEARCH.md`

Supporting research document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/REVISION-1.2.md`

Supporting revision 1.2 document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/REVISION-1.3.md`

Supporting revision 1.3 document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/TECHNICAL-ARCHITECTURE.md`

This detailed implementation/tradeoff/limitations document and distribution file inventory.

### `docs/TROUBLESHOOTING.md`

Supporting troubleshooting document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/USER-GUIDE.md`

Supporting user guide document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/VERIFICATION.md`

Supporting verification document. Historical architecture/verification records are superseded where the current architecture/revision states otherwise.

### `docs/evidence/1.3/component-checks.txt`

1.3 verification artifact. Render exercises use simulated state; Wine captures are compatibility evidence, not native Windows screenshots.

### `docs/evidence/1.3/music-card-wine.png`

1.3 verification artifact. Render exercises use simulated state; Wine captures are compatibility evidence, not native Windows screenshots.

### `docs/evidence/1.3/poses.png`

1.3 verification artifact. Render exercises use simulated state; Wine captures are compatibility evidence, not native Windows screenshots.

### `docs/evidence/1.3/rig-motion.mp4`

1.3 verification artifact. Render exercises use simulated state; Wine captures are compatibility evidence, not native Windows screenshots.

### `docs/evidence/gallery-wine.png`

Earlier-version verification artifact. Render exercises use simulated state; Wine captures are compatibility evidence, not native Windows screenshots.

### `docs/evidence/notebook-wine.png`

Earlier-version verification artifact. Render exercises use simulated state; Wine captures are compatibility evidence, not native Windows screenshots.

### `docs/evidence/pose-contact-sheet.png`

Earlier-version verification artifact. Render exercises use simulated state; Wine captures are compatibility evidence, not native Windows screenshots.

### `docs/evidence/windows-checks-wine.txt`

Earlier-version verification artifact. Render exercises use simulated state; Wine captures are compatibility evidence, not native Windows screenshots.

### `docs/history/UPDATE-1.0.1.txt`

Preserved historical release notes, superseded by current documentation.

### `docs/packaging-audit.txt`

Recorded build/extraction/archive checks and execution limits for the current package.

### `docs/test-results.txt`

Recorded Core regression output; consult the recorded version/host rather than treating it as native UI evidence.

### `global.json`

SDK selection/version policy for repeatable builds.

### `licenses/CsWinRT-LICENSE.txt`

License/redistribution attribution. Runtime components retain their own terms; no downloaded third-party pet art is included.

### `licenses/Markdig-LICENSE.txt`

License/redistribution attribution. Runtime components retain their own terms; no downloaded third-party pet art is included.

### `licenses/NAudio-MIT.txt`

License/redistribution attribution. Runtime components retain their own terms; no downloaded third-party pet art is included.

### `licenses/THIRD-PARTY-NOTICES.TXT`

License/redistribution attribution. Runtime components retain their own terms; no downloaded third-party pet art is included.

### `licenses/dotnet-desktop-LICENSE.txt`

License/redistribution attribution. Runtime components retain their own terms; no downloaded third-party pet art is included.

### `licenses/dotnet-runtime-LICENSE.txt`

License/redistribution attribution. Runtime components retain their own terms; no downloaded third-party pet art is included.

### `licenses/dotnet-runtime-ThirdPartyNotices.txt`

License/redistribution attribution. Runtime components retain their own terms; no downloaded third-party pet art is included.

### `packaging/AppxManifest.xml`

Optional MSIX identity/assets/capability/startup template. Not evidence of a signed installed package.

### `packaging/Assets/Square150x150Logo.png`

Original icon/store-logo packaging asset; not a creature sprite library or installed-package test.

### `packaging/Assets/Square44x44Logo.png`

Original icon/store-logo packaging asset; not a creature sprite library or installed-package test.

### `packaging/Assets/StoreLogo.png`

Original icon/store-logo packaging asset; not a creature sprite library or installed-package test.

### `packaging/Moss.ico`

Original icon/store-logo packaging asset; not a creature sprite library or installed-package test.

### `scripts/build.ps1`

Restore, compile, run Core tests, publish self-contained runtime and copy documentation/licenses into a portable archive.

### `scripts/package-msix.ps1`

Optional Windows SDK packaging/signing helper requiring developer-supplied tools and certificate.

### `scripts/uninstall-portable.ps1`

Remove known current-user startup/reminder tasks; preserve local data unless explicitly requested otherwise.

### `src/Moss.Core/Animation.cs`

Motion enum, six-channel Pose blending, clip timing/markers, displacement gait, bounded dance deformation and acceleration lean.

Declared types: `Motion`, `Pose`, `Animator`. Source length: 46 lines in this revision.

### `src/Moss.Core/AudioActivity.cs`

Pure, sample-free scalar-level hysteresis. Sustained output activates; brief silence holds; prolonged silence releases.

Declared types: `AudioActivity`. Source length: 17 lines in this revision.

### `src/Moss.Core/Content.cs`

Character, personality, sound and animation data contracts; bounded JSON/package validation and the shared JSON options.

Declared types: `Personality`, `Clip`, `SoundSpec`, `Character`, `Json`. Source length: 93 lines in this revision.

### `src/Moss.Core/Football.cs`

Temporary ball model: spawn, gravity, one-way bounce, edge reflection, kick proximity, local seeking and lifetime.

Declared types: `Football`. Source length: 39 lines in this revision.

### `src/Moss.Core/Interaction.cs`

Petting reversal detector, bounded event history, pet profiles/presets and explicit persistent mood/rest memory.

Declared types: `WorldEvent`, `EventHub`, `PettingGesture`, `PetProfile`, `SoundLevel`, `BehaviorPreset`, `PetSize`, `MusicStyle`, `PetMemory`. Source length: 47 lines in this revision.

### `src/Moss.Core/MarkdownDocument.cs`

Legacy Markdig-to-styled-run helper with link filtering. The current normal notebook does not use a Markdown read view.

Declared types: `TextRun`, `MarkdownDocument`. Source length: 48 lines in this revision.

### `src/Moss.Core/MediaSelection.cs`

Pure candidate prioritization: playing status dominates paused/current selection, with current/previous tie preferences.

Declared types: `PlaybackCandidate`, `MediaSelection`. Source length: 15 lines in this revision.

### `src/Moss.Core/Moss.Core.csproj`

Moss.Core target framework, references, build properties and linked files. Test-only graphics dependencies remain separate from production.

### `src/Moss.Core/Notebook.cs`

Note/reminder data, encoded-size bounds, per-document replace/backup/recovery/archive operations and confirmed-command grammar.

Declared types: `Note`, `DocumentStore`, `ReminderKind`, `ReminderStatus`, `Reminder`, `CommandPreview`, `NoteCommands`. Source length: 147 lines in this revision.

### `src/Moss.Core/PoseDynamics.cs`

Bounded planar two-link inverse kinematics and a stance/swing foot curve.

Declared types: `PoseDynamics`. Source length: 24 lines in this revision.

### `src/Moss.Core/Props.cs`

Twig ownership, damped hand following, smoothed movement/shock strain, body tug/lift, free fall and nearby recovery.

Declared types: `PropState`, `PropBody`. Source length: 61 lines in this revision.

### `src/Moss.Core/RhythmDetector.cs`

RMS-history onset detection and refractory timing. It does not infer guaranteed beats or BPM.

Declared types: `RhythmDetector`. Source length: 17 lines in this revision.

### `src/Moss.Core/Settings.cs`

Versioned settings/profile load/save/validation, privacy flags, media options, surprise/comment controls and frame limits.

Declared types: `QuietPolicy`, `PerformanceMode`, `Settings`. Source length: 71 lines in this revision.

### `src/Moss.Core/Simulation.cs`

Media state transitions and Creature state/utility/physics. Includes support, climbing, drag/throw, mood, hops, cursor wake and movement watchdog.

Declared types: `MediaState`, `Activity`, `Creature`. Source length: 240 lines in this revision.

### `src/Moss.Core/Visibility.cs`

Central quiet/fullscreen/presentation/visibility decision and shell notification-state mapping, including normal state 5.

Declared types: `ShellNotificationState`, `ShellContext`, `VisibilityDecision`, `DesktopVisibility`. Source length: 32 lines in this revision.

### `src/Moss.Core/World.cs`

Physical-pixel rectangles, displays/surfaces/windows, monitor nearest queries, exposed-ledge subtraction and unit conversion.

Declared types: `Box`, `Display`, `Surface`, `WindowInfo`, `World`, `Units`. Source length: 52 lines in this revision.

### `src/Moss.Core/packages.lock.json`

Pinned resolved dependency graph and package hashes for Moss.Core; not a runtime benchmark or security certificate.

### `src/Moss.Windows/ChoiceControl.cs`

Unbound enum dropdown construction that preserves the intended selected value when parented.

Declared types: `ChoiceControl`. Source length: 11 lines in this revision.

### `src/Moss.Windows/CreatureEffects.cs`

Bounded vector hearts/music notes and rare fixed-text random compliments. No chatbot, speech synthesis or unbounded particles.

Declared types: `CreatureRenderer`. Source length: 42 lines in this revision.

### `src/Moss.Windows/CreatureRenderer.cs`

Reusable GDI+ creature bitmap, palette/font/gradient ownership, main bean rig, common eyes/arms/paws and root transforms.

Declared types: `CreatureRenderer`, `DrawingExtensions`. Source length: 163 lines in this revision.

### `src/Moss.Windows/FramePump.cs`

Waitable-timer background loop, one-pending-callback UI coalescing, stop/rearm serialization and guarded disposal.

Declared types: `FramePump`. Source length: 47 lines in this revision.

### `src/Moss.Windows/LayeredSurface.cs`

Persistent top-down premultiplied-alpha DIB and memory DC; pixel copy, layered-window submission and deterministic native cleanup.

Declared types: `LayeredSurface`. Source length: 47 lines in this revision.

### `src/Moss.Windows/MediaObserver.cs`

GSMTC all-session reconciliation and subscriptions; optional asynchronous metadata, timeline interpolation, level fallback composition and opt-in WASAPI onsets.

Declared types: `MediaObserver`. Source length: 157 lines in this revision.

### `src/Moss.Windows/Moss.Windows.csproj`

Moss.Windows target framework, references, build properties and linked files. Test-only graphics dependencies remain separate from production.

### `src/Moss.Windows/MusicCard.cs`

Small native music popup with privacy consent, supplied title/artist/timeline and explicit unavailable states; owns timer/fonts.

Declared types: `MusicCard`. Source length: 59 lines in this revision.

### `src/Moss.Windows/Native.cs`

Central Win32/DWM ABI structs and imports for geometry, monitors, overlays, input, hooks, display affinity and shell state.

Declared types: `Native`. Source length: 57 lines in this revision.

### `src/Moss.Windows/NotebookForm.cs`

Compact normal RichEdit note window: small rail, grain margins, shortcuts/context actions, debounce save, save-error protection and close animation.

Declared types: `NotebookForm`, `before`. Source length: 185 lines in this revision.

### `src/Moss.Windows/NotificationObserver.cs`

Package/consent-gated incoming toast-ID observation; avoids notification text and revocation leaks.

Declared types: `NotificationObserver`. Source length: 46 lines in this revision.

### `src/Moss.Windows/OutputActivity.cs`

Default-render-device Core Audio COM peak meter, periodic reconciliation, unavailable-device fallback and explicit reference cleanup; no sample stream.

Declared types: `OutputActivity`, `Enumerator`, `Device`, `Meter`. Source length: 52 lines in this revision.

### `src/Moss.Windows/PetApplication.cs`

Application composition/lifetime, tray/forms, settings persistence, fixed-step tick, visibility, event wiring, music-device clicks and metrics.

Declared types: `PetApplication`. Source length: 282 lines in this revision.

### `src/Moss.Windows/PetOverlay.cs`

Small layered nonactivating form, capture/drag/release, per-pixel native behavior, special music clicks, hotkey and position/pixel presentation.

Declared types: `PetOverlay`. Source length: 90 lines in this revision.

### `src/Moss.Windows/Program.cs`

STA entry point, singleton mutex/activation, DPI setup, global exception boundaries and ApplicationContext lifetime.

Declared types: `Program`. Source length: 28 lines in this revision.

### `src/Moss.Windows/PropController.cs`

Owns twig/book/football models, bitmaps and native overlays; input wiring, surprise cadence, small-canvas drawing and cleanup.

Declared types: `PropController`. Source length: 93 lines in this revision.

### `src/Moss.Windows/ReminderService.cs`

Due reconciliation, persistence-first status changes, snooze/cancel/dismiss, limited-user scheduled actions and scoped COM cleanup.

Declared types: `ReminderService`. Source length: 70 lines in this revision.

### `src/Moss.Windows/Services.cs`

Local storage paths, private-data-minimizing rotating logs, startup integration and process/frame/GDI diagnostics.

Declared types: `Paths`, `Log`, `Startup`, `Metrics`. Source length: 89 lines in this revision.

### `src/Moss.Windows/SettingsForm.cs`

Native categorical preferences/gallery/notes/reminders, immediate changes plus explicit Save, permission controls and Advanced diagnostics.

Declared types: `SettingsForm`. Source length: 225 lines in this revision.

### `src/Moss.Windows/Sounds.cs`

One generated short wooden-pop WAV in memory, quiet level presets, rate-limited SoundPlayer and owned stream lifetime.

Declared types: `Sounds`. Source length: 21 lines in this revision.

### `src/Moss.Windows/SpeciesRig.cs`

Cat/dog/bird/octopus/rabbit/penguin anatomy, shared face/wearables and species music devices including the CD player.

Declared types: `CreatureRenderer`. Source length: 97 lines in this revision.

### `src/Moss.Windows/WorldDebugForm.cs`

Advanced mini-world/diagnostic inspection surface; not visual screen recognition or a route planner.

Declared types: `WorldDebugForm`. Source length: 27 lines in this revision.

### `src/Moss.Windows/WorldObserver.cs`

WinEvent-driven plus polled monitor/window reconciliation, DWM filtering, occlusion, work floors, permissions and refresh query.

Declared types: `WorldObserver`. Source length: 116 lines in this revision.

### `src/Moss.Windows/app.manifest`

Process manifest for least-privilege Windows behavior and display/DPI compatibility declarations.

### `src/Moss.Windows/packages.lock.json`

Pinned resolved dependency graph and package hashes for Moss.Windows; not a runtime benchmark or security certificate.

### `tests/Moss.RenderChecks/Moss.RenderChecks.csproj`

Moss.RenderChecks target framework, references, build properties and linked files. Test-only graphics dependencies remain separate from production.

### `tests/Moss.RenderChecks/Program.cs`

Executable test harness. Production renderer pose sheet and optional 384-frame seven-rig motion exercise.

Source length: 59 lines in this revision.

### `tests/Moss.RenderChecks/packages.lock.json`

Pinned resolved dependency graph and package hashes for Moss.RenderChecks; not a runtime benchmark or security certificate.

### `tests/Moss.Tests/Moss.Tests.csproj`

Moss.Tests target framework, references, build properties and linked files. Test-only graphics dependencies remain separate from production.

### `tests/Moss.Tests/Program.cs`

Executable test harness. Core regression cases and simulation timing.

Source length: 121 lines in this revision.

### `tests/Moss.Tests/packages.lock.json`

Pinned resolved dependency graph and package hashes for Moss.Tests; not a runtime benchmark or security certificate.

### `tests/Moss.WindowsChecks/Moss.WindowsChecks.csproj`

Moss.WindowsChecks target framework, references, build properties and linked files. Test-only graphics dependencies remain separate from production.

### `tests/Moss.WindowsChecks/Program.cs`

Executable test harness. WinForms/GDI/dropdown/output-meter lifecycle checks; host environment must be recorded separately.

Declared types: `Program`, `LayerWindow`. Source length: 53 lines in this revision.

### `tests/Moss.WindowsChecks/packages.lock.json`

Pinned resolved dependency graph and package hashes for Moss.WindowsChecks; not a runtime benchmark or security certificate.


Inventory contains 107 distribution source/support files at generation time. Generated binaries, private user files and caches are intentionally excluded.
