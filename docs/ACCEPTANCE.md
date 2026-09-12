# 1.3 acceptance update

The current regression result is 95 core tests passing. Four component checks passed under Wine, including output-meter unavailability/revocation. 42 production-renderer poses and a 384-frame seven-rig exercise were generated. Read REVISION-1.3.md and TECHNICAL-ARCHITECTURE.md for current implementation and limits. Real Chrome/Spotify/device fallback, native Windows performance and the full master scope are not certified. Neural import and visual recognition remain researched proposals, not implemented capabilities.

The following 1.2 matrix is retained as historical context and is superseded where the 1.3 documents differ.

# 1.2 delivery acceptance report

## Decision

**The complete master specification is NOT satisfied.** This delivery is an expanded implementation, not a finished production-certified product. It would be false to present the remaining gaps as merely a lack of test evidence: several requested systems are not fully implemented. No paid service, screenshot analysis, chatbot or fabricated event is used to hide those gaps.

The deliverable includes the executable, full source, build/package configuration, original procedural assets, character data, documentation and regression tests. It is usable only to the extent established by code and subsequent native execution; no native Windows execution was possible here. Wine compatibility execution is recorded separately in REVISION-1.2.md.

## What was actually performed

See [REVISION-1.2.md](REVISION-1.2.md) for the latest corrective work: 42 renderer poses, three Wine component checks and UI save/reopen evidence. Wine is not native Windows certification.

- Inspected the prior application and retained the regression fix for the tray-only visibility defect.
- Researched current primary Windows API documentation, available desktop-pet project/product material, and scheduling constraints; evidence and uncertainty are in RESEARCH.md.
- Built the complete solution targeting Windows with .NET 8 on Linux, without errors or warnings.
- Ran the core regression harness: **82 tests passed**. Complete actual output is in test-results.txt.
- Tests include 360,000 fixed simulation steps, physical attachment/throwing, geometry, DPI arithmetic, visibility policy, content validation, Markdown AST interpretation, safe-link filtering, command times, DST ambiguity, atomic note backups/quarantine, reminders, profiles, gestures, bounded events and toy detachment.
- Published self-contained Windows x64 binaries and checked PE architecture/GUI subsystem and ZIP integrity.
- Rebuilt/tested source extracted into a separate clean directory as part of final packaging.

No CI workflow, Task Scheduler registration, native desktop overlay, notebook form, audio device or notification-listener path was executed on Windows. A compile-pass is not a runtime pass. No multi-day or full Windows process profiling was performed. Linux microprofile output excludes rendering, DWM, actual Windows polling, scheduler, UI, audio and storage workload.

## Implementation versus evidence

| Area | Code delivered | Acceptance boundary |
|---|---|---|
| Overlay | Small native per-pixel layered creature/prop windows; click-through transparency; capture/release; hide/recover | Wine overlay/GDI exercise completed; actual Windows focus/hit testing remains unverified |
| Core dynamics | Fixed-step gravity, friction, spring dragging, momentum, ledge landing, impact and recovery | Simplified feet/one-way ledges; no full rotational rigid-body solver |
| Windows world | Generic DWM ledges, monitor/work areas, event-first reconciliation, original window climbing | No surface-graph planner, fully authored screen-edge grip/hang/pull-up or guaranteed route traversal |
| Characters | Seven original species-specific procedural anatomies, different clip parameters, personalities, timbres, music visuals and paper colors | Shared state library and common locomotion; not seven independently polished premium motion libraries |
| Animation | 23 motion states, distance-driven stance/swing, acceleration lean, jointed toy reach, pose blending and species variations | **No independently authored 100+ meaningful contextual-moment library**; no construction/screen-break rare-event library |
| Pet interaction | Head rubbing detector, pet spring dragging/throwing, one tug/release toy with physical detachment | Toy retrieval is local horizontal seeking; no comprehensive emotional-story engine or route-aware recovery |
| Props | Physical twig toy; separate interactive notebook prop; graphical music/wearable accessories | Notebook is anchored; music props/hats/glasses are rig visuals, not a general rigid-body ownership/accessory system |
| Notebook | Compact native sticky-note form, small text rail, normal RichEdit formatting, autosave, backups and plain/RTF migration | Grain on paper margins, solid native text canvas and brief fade/fold indicator; no physically deforming page simulation |
| Commands | Bounded @timer/@remind/@reminder/@alarm parser with exact preview and explicit confirmation | Not arbitrary natural-language understanding; DST ambiguity rejected |
| Reminders | Local deadlines/status, due list/countdown, dismiss/snooze/cancel, pet stimulus and tray notifications | Notification display may be suppressed; Windows scheduling path unverified |
| Closed-process delivery | Opt-in current-user Task Scheduler time/logon triggers and overdue reconciliation | Requires interactive user availability; does not wake the PC; catch-up can be delayed |
| Media | All-session GSMTC playback selection, independent optional metadata/artwork, opt-in local WASAPI onset accents | Not guaranteed beats/BPM; no occasional music-text phrase system |
| Incoming notifications | Permission/package-gated ID observation | Portable edition lacks capability; signed MSIX not supplied or tested |
| Shell | Separate WinForms navigation, gallery, rename, size presets, reduced motion, privacy and advanced diagnostics | Not an Apple-level polished/animated shell verified by design review; no complete accessibility audit |
| Persistence | Global/profile settings; independent note/reminder documents and backups | Per-pet mood/rest/attention memory added; no comprehensive position or story memory; not all adjustments are per-pet |
| Refresh/power | Nominal per-monitor rate query; waitable-timer UI pacing; sleep/hidden reduction | No vblank/VRR synchronization or native 60–240 Hz acceptance measurement |
| Diagnostics | CPU/working set/frame/simulation/render/world timing, status, bounded events and miniature world inspector | No actual GPU counters, end-to-end input latency, separate every-subsystem spans, object hitbox map or planned-route visualization |
| Fullscreen | Hide/Calm/Stay, quiet override, best-effort capture exclusion | Not every requested Corner/Minimal/Click-through policy; no universal screen-share detector |
| Distribution | Unsigned portable x64 build, source, scripts, licenses and optional MSIX manifest | No signed installer, installed-package test or trust certificate |

## Native acceptance still unverified

Launch/render/show/hide, shell navigation, head rubbing, pet/prop dragging, notebook typing/autosave/closing, scheduled wake of a closed app, actual notification delivery, app/window movement and close, taskbar and exposed ledges, mixed DPI, multiple/removed displays, high refresh, screen-sharing exclusion, audio endpoint changes, startup/shutdown/logoff, memory/GDI stability and extended runtime.

This table is a transparent delivery record, **not a roadmap presented as completion or a request that the user finish engineering**. A truly finished product requires implementing the missing systems and then producing the missing native evidence. This environment has not done that, and the archive is labeled accordingly.
