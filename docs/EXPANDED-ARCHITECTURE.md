# 1.1 architecture record, with 1.2 corrections

**The notebook, media, rendering and memory details are superseded by [REVISION-1.2.md](REVISION-1.2.md).** Other sections describe retained systems.

# Architecture additions

Read ARCHITECTURE.md for the original coordinate/physics/Windows boundaries and ACCEPTANCE.md for current limits. This document supersedes older statements that the product has only one character, no notebook, only WinForms-timer frame pacing, no local event channel or no additional prop overlays.

## Domain modules

- `Notebook.cs`: note/reminder records, isolated document storage and bounded human-time command grammar.
- `MarkdownDocument.cs`: Markdig parser → safe styled runs. HTML disabled; no remote-resource loading; links allowlisted to HTTP/HTTPS.
- `Interaction.cs`: bounded non-content event stream, rubbing detector and profile/preset records.
- `Props.cs`: physical toy ownership, spring tug, strain-based detachment, release and landing. This is not a general-purpose rigid-body scene graph.
- `Visibility.cs`: tested enum mapping and visibility policy, including the previous state-5 regression.
- `Settings.cs`: global privacy/behavior/audio settings plus per-character name, scale and wearables. Built-in selection stores stable IDs, not installation paths; explicit custom imports use their copied local path.

## Windows/UI modules

- `NotebookForm`: compact global notebook, title/index/preview, Markdown source editing and separately styled reading, explicit command confirmation, delayed autosave, export and recoverable archive. Save failure blocks normal exit; pending text remains in the editor.
- `ReminderService`: deadline reconciliation and optional current-user Task Scheduler registration. Uses a fixed executable action (`Moss.exe --reminder`), current user's SID and lowest run level. Trigger descriptions/actions do not contain note/reminder contents. Generic schedule errors are shown, not silently treated as success.
- `FramePump`: one high-resolution waitable timer and background thread; at most one posted UI callback at a time. Stop signals the wait before joining/closing the native handle. No busy-spin or global timer-period change. WM/DWM presentation remains asynchronous.
- `SpeciesRig`: distinct procedural anatomy for cat, dog, bird, octopus, rabbit and penguin alongside the original leaf bean. Each pack selects proportions, sound envelope, personality, music prop, paper color and clip tuning.
- `PropController`: separate small per-pixel toy/book windows using the same overlay presenter. Only the creature owns the global hotkey. Hidden props release capture. The toy has physical state; the book is an anchored interactive visual. Props share the frame loop rather than each spawning a timer.
- `WorldDebugForm`: scaled live world inspector for monitors/work areas, usable ledges, pet contact location, cursor, horizontal target and recent event names. It is not a full-screen visual overlay, planned-route debugger or GPU profiler.

## Storage contract

`%LOCALAPPDATA%/Moss/notebook/notes/<GUID>.json` and `reminders/<GUID>.json` are independent records. Serialize into same-directory random temporary files, flush them to disk, then replace atomically. Existing primary becomes `.bak`. If loading recovered a backup, the damaged original is quarantined before subsequent save so it cannot overwrite the last good backup. Both unreadable primary and unreadable backup remain untouched. Archives rename to `.deleted`; no auto-pruning of user notes occurs. Files are bounded on load. A save failure does not reset the entire notebook.

This is per-file atomicity, not a multi-document database transaction. Import/export touches only files explicitly selected by the user. Note bodies and reminder messages are not logged or published through the event stream. A future storage migration must preserve existing schema-1 records and retain an offline copy.

## Time semantics

Durations are absolute deadline offsets from a supplied clock. Clock expressions are interpreted in a supplied time zone, with a 24-hour default when AM/PM is absent. Each command produces a preview; only confirmation creates a reminder. Ambiguous/nonexistent daylight-saving times are rejected. Deadlines serialize as DateTimeOffset values, allowing restart catch-up. Running timers also use wall-clock deadlines; changing the system clock can alter remaining time. There is no promise of monotonic timing across manual clock changes.

A Pending deadline becomes Due and is flushed before the user alert is requested. Due items stay actionable until dismissal/cancellation/snooze. The app emits an alert at most once per ID per process session, so a restart can remind again about an unacknowledged due item. This favors not silently losing a reminder over exactly-once delivery, which cannot be guaranteed across a process crash and a Windows notification request.

## Scheduler/lifetime

Closed-app delivery is separately opt-in. Task names include this user's SID and reminder GUID. Time triggers include an end boundary; a current-user logon trigger supports overdue catch-up. The task runs interactively as the current user without stored credentials or elevation, so no reminder can magically display in an unavailable session. `StartWhenAvailable` does not imply immediate catch-up. Disabling/removing reminders cleans known tasks; uninstall matches only this user's Moss prefix. Moving the installation requires reconciliation of paths.

The already-running process checks deadlines once per second, even with the pet hidden. A scheduled second process exits if the application mutex is already owned; it does not force a presentation-interrupting manual reveal. A genuinely new reminder launch suppresses the application shell. Ordinary explicit launches open it. The existing payload-free named event only activates an existing instance after an explicit user launch.

## Animation and accessibility

Switching pets fades the outgoing sprite then loads the selected character and fades it in; reduced motion performs the switch directly. The note form uses short fades/folds and switches directly in reduced motion. Reduced mode suppresses bob/secondary-pose terms and high-impact restitution, not fundamental gravity or all joint motion. It is implemented behavior, not merely a label, but not a full accessibility audit.

The seven packs reuse the 23-state base runtime. Merely multiplying 23 by seven would be a misleading claim of 161 meaningful unique animations; this delivery makes no such claim. Full rare-event choreography, emotional continuity, screen-edge IK and navigation are still absent or limited as stated in ACCEPTANCE.

## Testing

Core tests receive explicit clocks/worlds/seeds. New coverage directly exercises real Markdown parsing, time grammar, invalid/DST inputs, backup preservation, profiles, gestures and toy state. Windows-facing code compiles against actual API contracts; no fake implementation is used to mark Windows integration passed. A Windows CI recipe remains included, but no run was performed in this environment.
