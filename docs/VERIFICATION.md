# Historical verification record

**For the current expanded delivery, read [ACCEPTANCE.md](ACCEPTANCE.md).** The text below records the earlier visibility-fix delivery and does not describe all newly added systems or their current test count.

# Verification report

## Release status — read first

This is a buildable implementation and a self-contained, unsigned Windows x64 distribution. It is **not a verified finished production product under the supplied completion criteria**. A Linux sandbox cannot establish that the Windows overlay launches, that Windows grants notification permission, or that multi-monitor interaction is correct on real hardware. Those gaps are not hidden behind a passing compilation claim.

The source implements the systems described below. It contains no fake Windows events, placeholder media player, screenshot-based window detector or cloud intelligence. Nevertheless, implementation is not synonymous with acceptance. The original request's complete native acceptance gate remains unmet.

## 1.0.1 correction

A user reported that the 1.0.0 application displayed only its system tray icon. Source review identified a deterministic visibility bug: shell state 5 (the ordinary accepts-notifications state) was classified as presenting. Since presentation defaults to Hide, normal desktop operation suppressed every render frame. This was not a missing character asset.

The enum mapping is corrected and centralized in a platform-independent, tested visibility policy. Tests now include all shell states, failure results, default/Calm policies, explicit visibility, recovery override, and error hiding. Additional changes prevent ordinary captioned maximized windows from being mistaken for fullscreen, expose visibility status, and provide explicit recovery and repeat-launch activation.

The source defect and its regression are verified. The user's particular Windows machine has not been inspected and the replacement executable has not been run here on Windows, so no claim is made that every potential cause of invisibility has been eliminated on every machine.

## Performed

Environment: Linux x64 sandbox; .NET SDK 8.0.425. No Windows VM, interactive Windows desktop, audio endpoint, signing identity or Windows SDK packaging tools were available.

| Check | Result |
|---|---|
| Restore complete solution | Passed |
| Release build of core, Windows app and tests | Passed; zero warnings and errors |
| Windows x64 self-contained publish | Passed; executable and runtime/content files produced |
| Automated tests | **46 passed, zero failed**; full output in `test-results.txt` |
| Long simulation | 360,000 fixed steps with bounded finite state assertions passed |
| Core microprofile | 120,000 fixed steps timed; see actual test output (Linux only) |
| Binary archive contents and ZIP integrity | Checked in packaging audit |
| Clean source archive rebuild/test | See final packaging audit |
| Interactive native Windows launch | **Not performed** |
| MSIX packing/signing/install | **Not performed**; no certificate supplied |
| Windows end-to-end CPU/GPU/RAM profile | **Not performed** |

The long simulation is 50 simulated minutes at 120 Hz, executed quickly without Windows rendering. It is not a 50-minute wall-clock desktop soak. Microprofile values do not include Windows APIs, bitmap drawing, GC behavior under the complete runtime, audio sessions or DWM.

## Automated coverage

- Physical-pixel/DIP conversion at 96,120,144,168,192,146,240 DPI.
- Negative-coordinate rectangles, mixed-monitor nearest calculations and offscreen recovery.
- Interval subtraction for exposed window surfaces; occlusion must not masquerade as movement.
- Gravity, swept crossing, settling, friction, jumping, support removal/movement.
- Spring grabbing, momentum-preserving release, impact/rebound, invalid-state recovery.
- Personality/real-media utility effects, petting and notification stimuli.
- Media state revision handling; silent audio and onset refractory behavior.
- Complete default content, unknown format/rig rejection, invalid colors/clips/numbers, sound-volume limits.
- Animation blend, protected one-shot transitions, event marker crossings.
- Settings round-trip, atomic replacement and invalid config rejection.
- Seeded randomized simulation numerical stability.

These are direct tests of the core, not fabricated mocks of successful Windows integration. See `tests/Moss.Tests/Program.cs` for every assertion.

## Native acceptance matrix

Every row here is **UNVERIFIED**, not a pass. This is a release engineering record, not a request for the recipient to finish implementing systems.

| Scenario | Intended behavior | Required native verification |
|---|---|---|
| A / T — logon/restart | Optional startup, no console | Portable Run entry and MSIX StartupTask; user/policy denial |
| B / S — unattended | Autonomous, bounded, rest/wake | Multi-hour/overnight idle soak and resource trend |
| C — proximity | Gaze / utility response | Different cursor speeds and privacy toggle |
| D — petting | Visual response | Stroke/click, no accidental desktop blocking |
| E / F — grab/drag | Spring following | Mouse capture, DPI boundary crossing, lost capture |
| G — throw | Ballistic momentum/impact | Slow/fast releases, window ledges and display boundaries |
| H / I / J — windows | Generic surfaces, carry/fall | Explorer, browsers, arbitrary apps, z-order and rapid close |
| K / L — displays/DPI | No permanent loss or offset | Left/right/above/below, 100/125/150/175/200%, negative origins |
| M / N / O — media | Real session-aware dance | Supported players, change/pause/resume, missing metadata |
| P — notifications | IDs-only reaction after grant | Signed installed package, allow/deny/revoke, multiple toast apps |
| Q — presentation | Safe default hiding | Presentation state, borderless fullscreen, different capture tools |
| R — normal desktop | Transparent pixels pass input | Browser/editor interaction under overlay, no focus theft |
| Extra — sleep/wake | Stable elapsed time and geometry | Lock/unlock, suspend/resume, hot-unplug monitor |
| Extra — error paths | Optional failures isolated | Audio device removal, malformed content, unwritable settings |
| Extra — lifecycle | Clean resource release | GDI handle stability, hooks and tray removed after exit |

## Explicit gaps and approximations

1. **No native launch/acceptance or Windows profiling was possible.** The request's production-completion criteria therefore are not satisfied.
2. **No signed installer/MSIX is included.** Notification listener code and packaging configuration exist, but portable notification reactions are unavailable by Windows design; the package permission path is unverified.
3. **Screen sharing is not universally detectable.** Best-effort capture exclusion plus manual quiet mode and Windows presentation reporting are provided. Capture exclusion is not a security guarantee.
4. **Rhythm is onset-reactive, not guaranteed musical beat/BPM tracking.** No false synchronization claim is made. Unsupported media sessions remain unsupported.
5. **Geometry is simplified.** Generic, exposed top ledges and work-area floors are modeled; not every UI surface, side taskbar wall, exclusive/protected window or secure desktop. Body clearance/occlusion is approximate. The pet does not modify applications.
6. **Character content supports one procedural rig family.** It has authored clips and configurable visuals/personality/sound/accessory, but not arbitrary skeletal art import, a character editor or a ragdoll body. Corner hiding is not true behind-window occlusion.
7. **Physics uses feet contact and swept one-way surfaces.** It is meaningful dynamics but not a general convex-body rotation/collision solver. Safety recovery after impossible/disconnected geometry can reposition the creature.
8. **Diagnostics do not measure GPU execution or total machine load.** Process CPU, working set, frame/simulation/render submission/world-scan timing and bitmap memory are measured. No validated resource budget is claimed.
9. **Animation polish has not been visually reviewed in Windows.** Procedural paths/clips are implemented, but successful compilation cannot establish consumer-grade animation quality.
10. **ARM64, installer trust, localization and accessibility auditing** were not tested. Settings use real WinForms controls with text labels; that is not a completed accessibility audit.

These disclosures should remain attached to the source and executable. Do not replace them with a blanket “complete and production-ready” statement unless the missing evidence is actually obtained and any discovered defects fixed.
