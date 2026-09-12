# Engineering architecture

## Stack decision

Moss uses C#/.NET 8, direct Win32 P/Invoke, GDI+ procedural vector drawing and a **small** layered Win32 window. WinForms provides the message loop, secondary settings window and tray integration; it does not render a full-desktop transparent form. The simulation is platform-independent C#.

This trades the ~186 MB self-contained .NET desktop distribution and some managed overhead for direct access to Windows media/notification projections, reliable resource ownership and straightforward inspectable content. A native C++/Direct2D implementation could have a smaller distribution and lower drawing overhead, but would require more custom UI, COM/WinRT lifetime and ABI code. Electron/Chromium and full game engines add unnecessary runtime services and full-window rendering assumptions. The choice is not a claim that GDI+ is universally the fastest renderer; Windows runtime profiling is still outstanding.

## Structure

```
src/Moss.Core/
  World.cs          physical geometry, monitors, windows, clipped surfaces, DPI units
  Simulation.cs     creature state, utility selection, fixed-step dynamics
  Animation.cs      state transitions, pose blending, markers
  RhythmDetector.cs bounded local onset detector
  Content.cs        versioned safe character model and validation
  Settings.cs       local configuration and atomic save
src/Moss.Windows/
  Native.cs         Win32 ABI boundary
  WorldObserver.cs  WinEvent hooks + reconciliation scan
  PetOverlay.cs     layered tool window and input capture
  CreatureRenderer.cs articulated vector rig
  MediaObserver.cs  GSMTC sessions, opt-in WASAPI loopback
  NotificationObserver.cs package-gated notification ID observation
  PetApplication.cs runtime composition / frame scheduling
  SettingsForm.cs   secondary settings UI
  Services.cs       paths, logging, startup and metrics
  Sounds.cs         character-configured local pop synthesis
  Program.cs       DPI setup, single instance, exception boundary
characters/moss/character.json
packaging/          MSIX manifest and original icon assets
scripts/           portable publishing, MSIX packaging, uninstall
tests/Moss.Tests/  dependency-light executable regression harness
```

## Coordinate contract and world model

All world and physics coordinates are **physical pixels in Windows virtual-desktop space**, including negative coordinates. Creature position denotes the center between its feet. Render coordinates are rig-local DIPs scaled by the nearest monitor's DPI, then positioned at `feet - (90,142)*scale`. The overlay target is `ceil(180*scale)` square, never desktop-sized. PMv2 is set before any window creation through both application manifest and managed startup. The overlay ignores the suggested WM_DPICHANGED rectangle; the world/render contract supplies its physical rectangle. The settings form uses WinForms DPI scaling.

`EnumDisplayMonitors`, monitor bounds/work areas and effective DPI build display records. Work-area bottoms are persistent floor surfaces, with negative device-name-derived IDs. This naturally handles ordinary bottom taskbars, including work-area reservations on secondary monitors; side/top taskbars are represented by reduced work areas rather than literal climbable taskbar side walls. Auto-hide taskbars reserve whatever work area Windows reports.

`EnumWindows` provides z-ordered, generic top-level windows. Minimized, invisible, DWM-cloaked, current-process, tool, transparent/system shell windows are excluded. DWM extended frame bounds are preferred to raw window rectangles. Top ledges must have body clearance. Occlusion intervals from preceding windows split usable ledges. A separate anchor X preserves actual window movement independently of clipped segment boundaries. Handles identify windows; identity/title strings are fetched only with explicit permission.

Hooks for foreground, creation/destruction/show/hide, location and minimize changes mark the model dirty. A 100 ms throttle coalesces event storms; a two-second full reconciliation repairs missed events. EnumDisplayMonitors is included in each reconciliation. Display/power messages invalidate immediately. This is a targeted event-first architecture with a conservative polling fallback, not a screenshot model.

Limitations: windows are one-way top surfaces, not arbitrary UI meshes or rigid blocks. Secure/UAC desktops, exclusive rendering and inaccessible/protected surfaces are not bypassed. HWND reuse is possible between reconciliations. Monitor membership uses nearest physical geometry; safety recovery can relocate the body after a disconnected monitor or invalid numerical state. Monitor transitions select a new scale immediately, not an artistic size crossfade.

## Perception and behavior

Environment observations update a `World`, not renderer frames. New visible windows, foreground changes and media revisions increase curiosity via a throttled contextual stimulus. Notification IDs similarly produce curiosity, without content. Supporting-window movement creates balance disturbance; disappearance removes support.

Creature state includes energy, mood, curiosity, attention, velocity, facing, balance, support, target, timers and seeded randomness. Energy recovers while resting and drains slowly while active. There is no hunger, punishment or persistence of behavioral obligations.

Candidate activities have utilities, cooldowns, durations and preconditions. Scores are modulated by personality and bounded random weights, then the highest score wins. Sleep has a recovery threshold. Dance requires real media-playing state and energy; pause removes its utility. Reachable window ledges enable climbing. Cursor proximity enables attention seeking. Priority physical/reaction states interrupt routine animation; held state suspends autonomous translation. Hiding is a corner-seeking crouch, not fake behind-window compositing.

The planner is intentionally lightweight: horizontal target approach and edge climbing, not a full navigation mesh. Play initiates ballistic hops and may cross surfaces depending on momentum/geometry. It does not guarantee shortest-path routes or that every reachable window will be visited.

## Dynamics

Semi-implicit Euler at **120 Hz**. Frame elapsed time is capped at 250 ms, with at most 30 integration steps per callback to avoid a resume/backlog spiral. Physics includes scaled gravity, horizontal drive acceleration, grounded friction, airborne drag, velocity, swept one-way ledge crossing, landing restitution, surface attachment, jump impulse and impact recovery. The body is a simplified feet contact model, not a general rotating rigid-body collider. Balance is a damped procedural tilt rather than a multi-body solver.

Dragging uses a damped spring: `a = (target-position)*180 - velocity*25`. Target incorporates the original grab offset. Release retains velocity, clamped to a safety ceiling. It does not teleport to the cursor or play a canned throw path. Lost mouse capture releases the body. Moving supports carry attached bodies; excessive movement influences reaction and tilt. Support removal lets gravity take over.

## Animation and rendering

The `bean-1` rig is an original procedural character: curved body/tail, pivoted leaf ears, arms and feet, separately aimed eyes, breathing, blink, cheek/mouth expressions and a headphone accessory. JSON clips define bob, lean, crouch, eye openness, ear/arm posture, rate, duration, looping and normalized markers. Every specified motion state has an authored clip. Clip selection follows physical and behavioral state; pose parameters exponentially blend across transitions. Brief thrown, grab, impact and waking states have minimum presentation time with urgent interaction overrides. Marker crossings drive the optional celebratory pop.

Body deformation and secondary movement are drawn with antialiased paths, not disconnected bitmap swaps. A reused premultiplied-alpha bitmap feeds `UpdateLayeredWindow`. Temporary GDI handles are selected back and deleted in `finally`. Alpha-zero areas pass mouse input through; a non-interactive setting adds `WS_EX_TRANSPARENT`. Only the small per-pixel visible rig/shadow area can intercept input. `WS_EX_NOACTIVATE` and `MA_NOACTIVATE` avoid stealing focus. No global cursor hook or desktop-sized input window is used.

Renderer limitations: this implementation supports one rig family, not arbitrary rig import, skeletal keyframe assets, physically rotating ragdolls or occlusion-aware behind-window hiding. Character graphics are data-driven procedural assets. Sound synthesis and headphone availability are character parameters, not executable content.

## Media

GSMTC manager/current-session events plus bounded reconciliation update real playback state. No metadata is needed for that path. Optional metadata is held only in RAM; artwork reads are bounded to 2 MiB. No artwork is displayed or uploaded. An OS request already dispatched may complete after a privacy toggle; the completion path checks permissions before retaining optional data.

NAudio.Wasapi is used only when output analysis is explicitly enabled. Loopback opens the default render endpoint, computes RMS from subsampled PCM/float buffers, and discards them. A smoothed envelope against a slow baseline plus a 230 ms refractory interval produces onset accents. This is not an FFT tempo estimator, beat grid, microphone input or recorder. Device errors disable/retry analysis without disabling simulation. Audio callbacks never draw or touch WinForms controls.

## Notifications, privacy and capture

`UserNotificationListener` is package-identity-gated and requests access on the UI thread. An initial snapshot prevents old Action Center items appearing as new stimuli. Subsequent 2.5-second snapshots compare only IDs; content and sender properties are not accessed. The listener checks access revocation. A short-lived toast may be missed; no notification content is persisted. Unpackaged mode clearly reports the unavailable capability.

Permissions are independent local configuration. There is no upload client, listening IPC port, account system, screen capture, microphone, clipboard or file scanning. `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` is best-effort capture exclusion, not a security boundary. `SHQueryUserNotificationState` and foreground geometry provide presentation/fullscreen policy; universal screen-sharing detection is not claimed. A registered hotkey/tray quiet mode is the deterministic manual override.

## Settings, lifetime and security

Atomic same-folder settings replacement, bounded JSON length/depth, invariant validation and default fallback handle corruption. Character imports deserialize/validate and reserialize into a new local folder. No ZIP extraction, script evaluation, external resource resolution or package code execution exists. The package identity and allowlisted rig name form an extension boundary rather than a promise to load arbitrary code. Runtime requires standard-user privileges only.

A per-session named mutex enforces one process instance. There is no network or payload-based IPC listener. A same-session named AutoReset event, `Local\Moss.Desktop.Show`, lets an explicit second launch request recovery and Settings activation without creating another pet. Start/stop lifetime disposes hooks, WinRT subscriptions, audio endpoints, tray, GDI objects and forms. Portable startup uses current-user Run; packaged startup uses a declared StartupTask. Optional integration failures are logged without propagating into the core. Frame failures hide the overlay with a recovery message instead of continuing to draw broken content.

Local memory is currently session state. JSON configuration and data-only character records leave room for a future inspectable local memory store, but no activity history store is implemented.

## Diagnostics and performance

Balanced/Smooth/Battery choose upper render targets of 45/60/25 fps. WinForms timers are subject to Windows scheduling granularity; these are not guaranteed delivery rates. Sleeping uses 12 fps; hidden uses 5 Hz callbacks batching fixed simulation steps with **zero drawing**. Paused updates use 10 Hz. Process CPU load lowers active render frequency. There is no system-wide CPU-load sensor or GPU counter.

Advanced diagnostics report process CPU as a percentage of all logical processors, working set, delivered frames, frame/simulation/render times, last world-scan time, surface/display counts, state, rig bitmap memory and integration status. Debug mode writes metric summaries every 30 seconds. Render timings include bitmap preparation and compositor submission, not measured GPU execution. Simulation time includes behavior and physics, not separate CPU profiler spans. GPU use is explicitly shown as unavailable.

Linux core timing in `test-results.txt` is **not an end-to-end Windows performance claim**. Windows CPU, GPU, GDI handle counts, memory stability, multi-day soak and long idle overhead still require measurement.

## Testing and build

The executable regression harness covers geometry, DPI conversions, attachments, occlusion, falling, swept collision, throwing, monitor recovery, state utilities, content/config validation, animation blending/markers and onset detection. Seeded long simulations test bounded numerical behavior. It does not mock a successful Windows desktop test. Build scripts run it before self-contained publishing; a Windows CI definition is provided but was not executed here. See BUILD and VERIFICATION for exact results and outstanding native acceptance.

## API references

- https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-updatelayeredwindow
- https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwineventhook
- https://learn.microsoft.com/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows
- https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager
- https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/notification-listener
- https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity
- https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shqueryusernotificationstate


## Visibility correction in 1.0.1

`DesktopVisibility` owns the tested shell-state mapping and visibility precedence. Shell state 5 is ordinary operation, not presentation. State 4 means presentation; state 3 means exclusive D3D fullscreen. Failed/unknown results are not interpreted as presentation. Captioned maximized windows are excluded from geometric fullscreen detection. Status reasons are displayed in the tray and written only on transitions to the local log; no private window information is added.

Explicit recovery provides a 30-second automatic-policy override but never defeats a subsequent explicit Hide or quiet command. Startup via `--startup` respects the saved visibility setting. Ordinary explicit launches restore visibility, while the default automatic fullscreen/presentation safeguards remain active. Rendering submission now preserves the Win32 last error for diagnosis. Recovery recreates the overlay handle and re-applies interaction and capture-exclusion settings.

Windows enum reference: https://learn.microsoft.com/windows/win32/api/shellapi/ne-shellapi-query_user_notification_state
