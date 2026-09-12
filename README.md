# Moss
## A small life on your desktop — version 1.0.0

Moss is a local Windows desktop companion: one creature inhabits window ledges and the taskbar, responds physically to grabbing and throwing, notices music, and carries a little toy alongside a notebook.

**Read the delivery boundary:** this is an expanded, buildable implementation—not a verified fulfillment of the master production specification. It includes seven procedural species, but not seven independently authored premium animation libraries or the requested 100+ meaningful cinematic interactions. Screen-edge construction, general navigation planning, full emotional/object stories and native Windows acceptance remain incomplete. Earlier builds were cross-compiled on Linux and exercised under Wine; no native Windows machine was available for certification. No “fully finished” claim is made.

## Download, update and run

Target: **Windows 10 build 19041+ / Windows 11, x64**. This targets those APIs; it is not a statement that Microsoft still supports every historical Windows release.

1. If an older Moss is running, **right-click its tray icon → Exit Moss**. Launching a new EXE cannot replace code in an old running process.
2. Download `Moss-1.0.0-windows-x64.zip` from the [Releases page](https://github.com/aryan-astra/Moss/releases) and extract **all of it** into a local folder you intend to keep. Do not launch from inside the ZIP. (`Moss-1.0.0-windows-x64.exe` is the same single-file build for direct download.)
3. Double-click **Moss.exe**. A separate application shell opens on ordinary launches. Closing the shell leaves the pet alive. Quiet `--startup` launches do not open the shell.
4. No administrator access or separate .NET runtime is required. The portable archive includes the runtime, character packs and licenses. Allow roughly 200 MB of extracted space; real Windows CPU/GPU/RAM budgets have not been validated.

The executable is unsigned. Respect Windows/organization security policy; do not disable system security to run it. There is no signing certificate or signed installer in this delivery.

The earlier tray-only bug remains fixed: normal shell notification state 5 is **not** presentation mode. The tray status explains why the creature is visible or hidden. **Show Moss / recover position** performs explicit recovery and a 30-second reveal; automatic fullscreen safeguards resume afterward. Hide and quiet mode always take precedence over that reveal. Launching a second updated instance requests recovery/Settings rather than creating another creature.

## About version 1.0.0

Version 1.0.0 is the first release built automatically from source on GitHub: music-aware dancing with a clickable music card, species CD/cassette/radio/headphone visuals, floating notes and optional rare compliments, proportion-preserving dance with physical hops, petting hearts, a force/movement-based twig grip, temporary football play and an explicit Save changes action.

## The shell

- **Pet:** portrait, a simple name field, Small/Default/Large sizing, startup and visibility.
- **Notebook:** open shared pages; see pending/due reminders and live remaining time; dismiss, snooze or cancel them.
- **Customize:** choose a pet, a hat or glasses; import a data-only character.
- **Sounds:** Off/Soft/Normal/Full and reminder notifications.
- **Behavior:** Calm/Playful/Spirited, original character personality, and Music Off/React/Dance.
- **Settings:** plain-language context/privacy permissions.
- **Advanced:** precise activity/sensitivity controls, measured diagnostics, frame cap and world inspector.

Changes generally save immediately, and each main page also offers Save changes. Names save when the name field loses focus. Profiles keep names, sizes, hats, glasses and a small mood/rest memory independently per character. Memory saves periodically and on normal exit. Time away is rest, not a neglect penalty. A species switch crossfades out/in unless reduced motion is enabled. Notes are never copied between pet profiles. Behavior/audio/privacy choices are global; this is not a fully per-pet sound-settings editor.

## Meet the residents

| Name | Character | Movement/visual identity | Music | Paper |
|---|---|---|---|---|
| Moss | Woodland bean | Leaf ears and tail, soft breathing | Headphones | Cream |
| Miso | Cat | Pointed ears, whiskers, curling tail, restrained gait | Headphones | Warm cream |
| Pip | Dog | Floppy ears, muzzle, collar, eager tail | CD player | Warm kraft |
| Lark | Bird | Crest, beak, wings and small stepping feet | Radio | Pale handmade-paper tone |
| Inky | Octopus | Eight procedurally waving arms and rounded mantle | Turntable | Cool paper |
| Clover | Rabbit | Long articulated ears, wide feet, cotton tail | Cassette-style player | Ivory |
| Puck | Penguin | Tapered flippers, beak, belly and waddling posture | Radio | Pale blue |

These are original procedural vector designs sharing an engine and base state set. Their anatomy, proportions, clip tuning, timbre, personality and music visuals differ. They are **not** claimed to have received final visual/animation review on Windows. The sound implementation is synthesized timbre variation, not a complete animal-specific library of recorded effects.

## Touch and play

- Move the cursor nearby: gaze and curiosity can respond.
- Rub back and forth over the head: reversal/distance detection recognizes petting, and recognized petting shows hearts. A single entry or one-direction mouse move is not automatically a pet.
- Hold the left button on the creature and move: a damped spring follows your hand. Release to retain physical momentum. Hold still before releasing for a gentler drop.
- Grab the small twig toy: the creature initially resists through a spring constraint. Slow displacement remains attached; sufficiently fast pulling or repeated shocks can detach it; release to throw. The pet can seek and recover a settled nearby object. Retrieval across arbitrary disconnected surfaces is not guaranteed.
- Click a visible music device while dancing for the music card, or use tray → Now playing. Permit song details if you want title, artwork and a supplied timeline.
- Use Pet → Roll a football, or leave occasional football play enabled under Behavior.
- Click the little book alongside the pet, or open Notebook in the shell/tray.
- Right-click the creature for its menu.
- **Ctrl+Shift+F12:** quiet mode, hiding the pet and props until toggled back. A hotkey conflict is logged; the tray has the same command.

Visible window tops are generic terrain, using DWM/Win32 geometry, not screenshots or an application allowlist. Cloaked/minimized/tool/protected windows and occluded ledges are excluded. Window climbing is implemented; true screen-corner grip/pull-up construction choreography is not. The contact model uses feet and one-way ledges, not rotating convex-body rigid physics. Safety recovery may relocate an offscreen pet after monitor loss.

## Your notebook

The notebook is a compact floating window. Its paper color follows the active pet. Notes are global and remain when you switch pets.

1. Click **+** or press **Ctrl+N**, then type. The first line identifies the note; the narrow left rail uses small text and a short content preview.
2. Use **Ctrl+B/I/U** for native rich-text formatting. Right-click holds editing commands. There is no file menu, title box, Markdown toolbar or separate Read view.
3. Notes autosave after about **700 ms** of inactivity, when switching notes and when closing. **Ctrl+S** saves immediately. A failed save leaves the note open with an error.
4. **Esc** or **×** closes it. Drag the top strip to move; drag edges to resize. The default is 430 × 380 logical pixels, with paper-grain margins, a glue strip and brief fade/fold indicator. The native writing area is deliberately solid paper color for crisp normal input.

Old note IDs, content and backups are preserved. Optional RTF stores formatting alongside a plain-text copy in the existing `Markdown` field. Old Markdown remains literal editable text; invalid RTF falls back to the plain copy. No existing notes are deleted or automatically reformatted. No archive/export control is exposed in this simplified editor; old archived files remain retained on disk.

### Reminder and timer commands

Type a command on its own line, place the caret on it (or select it), then press **Ctrl+Enter**:

```
@timer 25m
@timer 1h30m
@timer 1h 30m
@remind in 10 minutes Test reminder
@reminder 30m drink water
@remind in 2 hours call home
@remind tomorrow 9am submit assignment
@remind Friday at 5pm meeting
@alarm 7:30am
```

A preview shows the exact date, clock time, offset and message. **Nothing is scheduled until you confirm.** Editing a command does not automatically change a previously created reminder. Manage existing reminders from the Notebook area of the shell. Rejected lines explain the expected form; fix the line rather than guessing.

Times without AM/PM use the **24-hour clock**, explicitly disclosed in confirmation. Past clock times roll forward. Durations support seconds/minutes/hours up to one year. Ambiguous/invalid daylight-saving wall times are rejected with a request to use a duration. This is a bounded grammar, not an arbitrary-natural-language AI parser.

Deadlines/statuses persist independently of note files. When due, a reminder becomes Due, the pet receives a stimulus, optional sound plays, and a standard Windows tray notification is requested if enabled. Windows may suppress notifications. Due items remain in the shell until dismissed; snooze sets a new five-minute deadline. A due item is announced at most once per application session. Relaunch can re-announce unacknowledged Due items; it does not silently dismiss them.

### Delivery while Moss is closed

Opt in to **Let Windows deliver reminders when Moss is closed**. Moss registers current-user, non-elevated Task Scheduler tasks that launch this executable with `--reminder`. No reminder text, passwords or arbitrary commands are placed in task actions. Registration failure is shown in the UI; persisted reminders still work while Moss is running.

Your Windows session must be available. The PC is not awakened from sleep, nothing runs while powered off, and missed scheduled tasks may be delayed by Windows (including a documented catch-up queue delay). A logon trigger and due-state reconciliation provide catch-up. This mechanism was built but **not tested on native Windows here**; it is not a guaranteed delivery SLA.

Keep the EXE at a stable path. After moving the app, toggle closed-app reminders off/on to replace registered paths. Turning the option off removes known Moss reminder tasks. The uninstall script also removes this user's `Moss-<SID>-*` tasks. Incoming third-party Windows notification observation is a separate feature with different package/permission requirements.

## Music

Music **Off** disables media awareness; **React** allows contextual reactions without dance selection; **Dance** allows actual media-playing state to influence dancing. Grounded pets now enter dance without the former random cooldown delay; holding, landing recovery and calm mode still take precedence.

GSMTC supplies state and optional metadata. All sessions are reconciled: a playing session takes priority over a paused shell-selected player. Playback polling does not wait for optional metadata/artwork. Metadata/artwork is opt-in and in RAM only; no title is fabricated. Not every player/site supplies a Windows session. Without a shared session, the separately configurable default-output level fallback can still trigger reactions. It cannot identify the track or distinguish music from other sustained sound. Non-default/exclusive/very quiet output may be missed. No recording is opened for this fallback.

Opt-in speaker-output analysis uses local WASAPI loopback, discards samples and adds onset-linked dance accents. It does not record, upload, transcribe, open a microphone or claim guaranteed beat/BPM synchronization. Sound effects stay quiet by default and use each character's synthesized timbre.

## Privacy, notifications and non-interference

Defaults retain cursor/window/foreground awareness, media state and default-output level fallback, while identity/title reading, metadata, speaker analysis and incoming-notification observation are off. There is no telemetry, account, cloud dependency, screenshot capture, clipboard access, file scanning or browser scraping. Notebook files are user-authored local content, not scanned personal files. Logs omit note bodies, reminder text, media/window titles and incoming notification text.

Incoming notification observation requires a signed installed MSIX with `userNotificationListener` capability and user access. **The portable edition cannot obtain it**, and no signed package is supplied. It is not represented as a working portable capability.

Fullscreen and Windows-reported presentation default to Hide; Calm and Stay remain configurable. Universal screen-sharing detection is not available. Capture exclusion is best-effort and depends on the sharing software. Use quiet mode for deliberate non-interference before sharing. The requested Corner/Minimal/Click-through automatic policy variants are not all implemented.

## Motion, scaling and performance

Reduced motion removes notebook fades/folds and pet-switch fades, suppresses several secondary movements and disables high-impact rebound. It does **not** eliminate every animation or underlying physical movement; dragging/falling remain usable. It is not an accessibility-certification claim.

Small/Default/Large are per-pet. Coordinates remain physical virtual-desktop pixels with PMv2 awareness. Display changes and periodic reconciliation recover monitor geometry. Nominal display refresh is read without changing display configuration.

- Balanced follows the display up to 60 Hz; resting states cap at 24 Hz.
- Smooth follows nominal refresh up to the advanced cap (240 Hz default, adjustable).
- Battery caps at 30 Hz.
- Sleeping renders at 5 Hz; hidden pets stop drawing.

Each overlay reuses a premultiplied-alpha DIB and memory DC until resized. The book uses a cached 36-pixel canvas and the toy a 48-pixel canvas at default scale, instead of two 180-pixel canvases. Unchanged props move without regenerating pixels. These are allocation/transfer reductions, not claimed Windows CPU/RAM benchmarks.

A high-resolution waitable timer coalesces UI callbacks; it does not guarantee vblank synchronization or hardware acceptance at 240 Hz. Diagnostics report delivered frames, process CPU, working set and timing. GPU execution, full-system load and end-to-end latency are not measured.

## Data safety and locations

`%LOCALAPPDATA%\Moss` contains:

- `settings.json`: versioned global settings and per-pet profiles.
- `notebook\notes\<id>.json`: individual notes with ID, title, content and timestamps.
- `notebook\reminders\<id>.json`: independent reminder/timer records.
- `.bak`: previous document revision; `.corrupt-*`: preserved damaged originals; `.deleted`: explicitly archived notes.
- `characters`: imported data-only packs.
- `moss.jsonl` / `.1`: rotated structured logs (note bodies, reminder text, media/window titles and notification content are intentionally excluded).

Document writes flush a temporary file before atomic replacement. Corruption recovery preserves the last good backup and the damaged original. Unreadable files are retained and warnings surfaced. **Back up this directory for your own long-term protection; local backups on the same disk do not protect against disk loss.**

Before manually editing/resetting `settings.json`, exit Moss and keep a backup. Do not delete `notebook` to reset settings. Reset position is available without touching notes. Imported packs are bounded JSON, with allowlisted rig/species and no executable code.

## Exit, startup and uninstall

Closing the shell leaves the pet running. **Tray → Exit Moss** exits the process. The notebook is saved first; if saving fails, Moss stays open rather than silently discarding the current note.

Startup is optional under Pet. The portable edition uses the current user's Run entry; signed packages use StartupTask. If you move the folder, toggle startup off/on to update its path. Startup never intentionally opens a console.

To uninstall: exit, run `uninstall-portable.ps1` if you enabled startup/closed-app reminders, and delete the app folder. Local notes are retained. Only use `-DeleteLocalData` if you explicitly want notes, reminders, settings, imported content and logs removed too. Windows may restrict task-removal commands by policy; do not bypass organization controls.

## Custom characters

A character is a folder containing **`character.json`** — inspectable numeric data for the `bean-1` procedural rig, never executable code. Copy `characters/moss/character.json` to start editing, then import it from Settings → Character. Imports are validated and reserialized into `%LOCALAPPDATA%\Moss\characters\<random-id>\character.json`. `characters/character.schema.json` documents the constraints; the in-app validator is authoritative.

Key bounds: `formatVersion` is `1`; `rig` is `"bean-1"`; `name` is nonblank, up to 40 characters; colors are exactly `#RRGGBB`; `width` 40–90, `height` 40–100, `earLength` 0–35; `species` is one of `bean`, `cat`, `dog`, `bird`, `octopus`, `rabbit`, `penguin`; `musicProp` is one of `headphones`, `radio`, `turntable`, `cassette`, `cd`; `animations` maps the 23 Motion names (Idle … Celebrating) to clips with bounded rate/bob/lean/crouch/eyes/ears/arms/duration values. Files over 256 KiB, unknown rigs/species, non-finite numbers and missing clips are rejected. Unknown extra properties are ignored.

The original rig parameters, icon drawings and sound design are MIT licensed with the project. Creators may license newly authored content as they choose; add a human-readable `LICENSE` beside the JSON.

## Troubleshooting

- **Tray only:** exit old versions before launching this one; inspect the tray visibility reason and use Show / recover. Actual fullscreen still hides by default. In remote sharing tools, capture exclusion can intentionally omit the pet.
- **Notes say not saved:** keep the editor open, select and copy your text to a safe local document, and check disk space/permissions. Exit is blocked on an unsaved-note failure.
- **A command is rejected:** use the exact forms above, place the caret on the line and confirm the preview. Hours without AM/PM are 24-hour; ambiguous daylight-saving times must be re-entered as durations.
- **Reminder not delivered while closed:** check opt-in, scheduler status, stable EXE path, Windows user/session availability and task policy. Missed deadlines catch up when the app next runs.
- **Timer not showing a live countdown:** use the Notebook area of the application shell, not a typed command line in the editor.
- **Music not noticed:** confirm a supported Windows media session and permission; metadata is not required for state-aware behavior.
- **Notification observation unavailable:** portable identity is insufficient; this requires the optional signed MSIX installation and Windows permission.
- **A window is not terrain:** minimized/protected/cloaked/tool surfaces and insufficient exposed clearance are excluded.
- **Unexpected display position:** reset the pet; the actual mixed-DPI hardware matrix remains unverified.
- **Resources seem high:** Battery mode, reduced motion, audio analysis off, or Hide; inspect Advanced metrics. Do not mistake a target frame rate for a measured guarantee.

## Build from source

Install the .NET 8 SDK, clone this repository, and run from the repository root:

```powershell
./scripts/build.ps1
```

This restores NuGet dependencies, builds, runs the regression harness, publishes the self-contained Windows x64 application plus a single-file `Moss.exe`, and packages the portable ZIP with checksums. Initial NuGet restore needs internet. No runtime internet connection is required. `Moss.sln` can also be opened in Visual Studio. The script is the same process GitHub Actions runs; MSIX packaging/signing is intentionally out of scope (no signing credentials exist).

### Releases

Releases are built automatically by GitHub Actions — no manual packaging:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The tag must match `<Version>` in `Directory.Build.props`. The workflow restores, builds, runs the regression harness, publishes the portable Windows x64 distribution plus a single-file `Moss.exe`, and attaches the ZIP, the EXE and checksums to the GitHub Release.

## How it works

`src/Moss.Core` (portable `net8.0`) holds the simulation: world geometry in physical pixels, fixed-step creature physics (gravity, spring dragging, one-way ledge landing, support tracking), a utility-behavior controller, animation blending over 23 motion states, props (twig, football), settings/content validation, note/reminder storage with a bounded command grammar, and media-selection/level-hysteresis logic. It depends only on Markdig.

`src/Moss.Windows` (`net8.0-windows`, WinForms) owns the process: tray lifecycle, Win32/DWM world observation, layered-window GDI+ rendering, the native note editor, preferences, music card, GSMTC/WASAPI media observation, Task Scheduler reminders, and diagnostics. A high-resolution waitable timer paces UI-thread ticks; simulation steps are fixed at 1/120 s.

`tests/Moss.Tests` is a small deterministic harness over `Moss.Core` (content validation, audio hysteresis, command grammar, version stamp). There is no chatbot, telemetry, account, cloud dependency, screenshot capture, microphone input, or plug-in execution anywhere in the tree.

## Third-party software

Moss's source, original procedural character, parameter animations, icon art and synthesized sound are covered by the root MIT `LICENSE`. No downloaded artwork, fonts or recordings are bundled. Segoe UI is requested from Windows, not redistributed.

Runtime dependencies:

- **.NET 8 / Windows Desktop runtime 8.0.31** — Microsoft and .NET contributors, MIT with component notices. The self-contained distribution includes the runtime. See `licenses/dotnet-runtime-LICENSE.txt`, `licenses/dotnet-desktop-LICENSE.txt` and the third-party notice files in `licenses/`.
- **NAudio.Core and NAudio.Wasapi 2.2.1** — Mark Heath and contributors, MIT. Used for opt-in Windows speaker-output analysis. See `licenses/NAudio-MIT.txt`.
- **Windows SDK .NET projection 10.0.19041.56 / WinRT.Runtime (CsWinRT)** — Microsoft, MIT. See `licenses/CsWinRT-LICENSE.txt`.
- **Markdig 0.37.0** — Alexandre Mutel and contributors, BSD 2-Clause. Used for Markdown parsing in the core; it does not download images or execute embedded HTML. See `licenses/Markdig-LICENSE.txt`.

Build-only tools: .NET 8 SDK (initial restore needs network; running Moss does not). Exact resolved package versions are pinned in each project's PackageReference; this file is not a security audit.

## Source provenance

The `src/` tree was recovered from the shipped portable binaries with ILSpy, whose original C# source was not in version control. It compiles cleanly, reproduces the shipped binaries to matching size, and the rebuilt application was smoke-tested. Only build hygiene changed versus raw decompiler output (NuGet references instead of absolute DLL paths, SDK-generated assembly info, one restored `using` alias, two restored fire-and-forget discards); no product behavior was altered. If the original source tree resurfaces, it supersedes this reconstruction.

Moss is free, local and MIT licensed. There are no accounts, ads, subscriptions, AI requirements or payment systems.
