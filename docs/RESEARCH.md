# Technical/product research — 12 September 2026

## Evidence boundary

This is a desk-research and engineering decision record, not a benchmark of competing products. No competitor binaries were executed. Product websites describe intended features, not independently verified behavior; user reviews are anecdotes, not reproducible performance evidence. Two attempted documentation URLs returned 404; those are not used as technical evidence. Research informed implementation choices but cannot substitute for Windows execution.

## Comparable products

| Product | What the available evidence establishes | Design implication |
|---|---|---|
| Shimeji-ee | A distributed source/readme describes Java, image sets, action/behavior XML and configurable supported windows. It also describes tray control and a 46-image convention. [5](https://github.com/Shreyas-Sonawane/Shiemji_DESKTOP_PETS) | Preserve inspectable content and a small tray. Do not equate frame count with animation quality or hardcode just a few supported apps. This is a mirrored distribution, not a confirmed canonical upstream audit. |
| Desktop Mate | Steam user reviews request tray/minimize/startup behavior; other reviews complain of obstruction and GPU cost. Those are user reports, not measured resource results. [3](https://steamcommunity.com/app/3301060/reviews/?browsefilter=toprated&snr=1_5_100010_) [4](https://steamcommunity.com/app/3301060/reviews/?l=spanish) | Test non-interference and idle cost rather than relying on attractive animation alone. Do not copy reported performance figures. |
| VPet Simulator | The repository documents separate Windows/core/tool projects, interactions and Workshop/mod support. It is WPF based and describes additional core-content setup. [2](https://github.com/LorisYounger/VPet/blob/main/README_en.md) | Engine/content separation is useful; deliver all required core content instead of a build that starts with missing assets. Do not adopt its maintenance mechanics or execute unrestricted mods. |
| Desktop Pet / DPET | “Desktop Pet” is an ambiguous product name. The available Pixel AI Pet comparison mentions DPET, but is competitor marketing, not an authoritative audit of DPET's implementation. [1](https://pixelaipet.co.uk/blog/best-desktop-pets-for-windows.html) | No unique codebase or validated performance conclusion is asserted under this generic name. |
| MewMuze | Its official site advertises cursor response, window physics, reminders, local-first operation and adaptive rendering, with a paid Pro offer. [1](https://mewmuze.com/) | Creature-associated utility can be coherent. Avoid copying its payment model, clipboard access or claiming that its marketing proves performance. |
| Pixel AI Pet | Its own comparison advertises AI-task reactions, window/taskbar walking, a focus timer, many pets and a paid unlock. [1](https://pixelaipet.co.uk/blog/best-desktop-pets-for-windows.html) | Deliberately exclude AI-agent integration, progression obligations and payment infrastructure. Quantity alone is not evidence of distinct animation quality. |
| AniMate Waifu | The Microsoft Store listing describes VRM import, cursor/head-pat/drag interactions, music behavior and a transparent click-through overlay. It explicitly distinguishes planned conversation from currently shipped functionality. [1](https://apps.microsoft.com/detail/9phrl2t3f0bs?hl=en-US&gl=TZ) | 3D interchange has a different asset/runtime cost. Maintain accurate boundaries between present behavior and promises. Do not add chat because a competitor plans it. |
| Beat Pet | A giveaway listing reproduces a description of a music-listening, drumming desktop pet and links Steam app 4819850. This is a secondary source, not proof of its beat-detection algorithm. [1](https://www.steamgifts.com/giveaway/Ab6WW/beat-pet) | Rhythm should visibly affect physical movement, but an implementation must disclose onset detection versus actual tempo/beat tracking. |
| Clover Shimeji / other current projects | A public project documents tray/input/positioning and configurable frame timing; its README itself qualifies the 60 fps claim. [1](https://github.com/Stuocs/Clover_Shimeji) | Do not report target FPS as delivered FPS. Keep measured metrics available. |

The research did **not** establish independent memory/CPU benchmarks for these products. There are no invented complaint frequencies, measured rankings or claims that all listed products were hands-on tested.

## Native window and rendering contract

Microsoft's `UpdateLayeredWindow` documentation says the whole layered surface is updated and recommends keeping it small. The source DC provides per-pixel alpha via `BLENDFUNCTION`. This favors one small creature overlay plus small relevant-object overlays, not a full-desktop transparent render target. `WS_EX_NOACTIVATE`, a tool-window style and alpha-based hit testing keep ordinary work usable. `SetLayeredWindowAttributes` is not mixed into the pet's per-pixel rendering path. Errors retain the Win32 last-error code.

References:
- https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-updatelayeredwindow
- https://learn.microsoft.com/windows/win32/winmsg/window-features
- https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwineventhook
- https://learn.microsoft.com/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows
- https://learn.microsoft.com/windows/win32/api/dwmapi/nf-dwmapi-dwmgetwindowattribute

The app retains the existing PMv2, physical virtual-pixel coordinate model: monitor/work rectangles, negative coordinates, visible DWM bounds, z-ordered occlusion and event-throttled reconciliation. GDI+/WinForms is retained rather than rewritten to WinUI/DirectComposition during this change. This reduces integration churn after the earlier visibility defect, but **does not prove** that GDI+ meets the requested premium rendering/performance bar. Direct2D/DirectComposition would merit actual Windows comparison before making a production-performance claim.

## Fullscreen correctness

The original bug is explicitly covered by regressions: `QUNS_ACCEPTS_NOTIFICATIONS=5` means ordinary operation, not presentation. `QUNS_PRESENTATION_MODE=4` is distinct from `QUNS_RUNNING_D3D_FULL_SCREEN=3`. Unknown/failed results do not invent a presentation state. Captioned maximized windows are excluded from geometric fullscreen classification.

https://learn.microsoft.com/windows/win32/api/shellapi/ne-shellapi-query_user_notification_state

## Refresh rate and frame pacing

`EnumDisplaySettingsW(..., ENUM_CURRENT_SETTINGS, ...)` exposes `dmDisplayFrequency`; Microsoft states the API does not participate in DPI virtualization. The app reads the current mode per device and never changes it. A high-resolution waitable timer wakes a background pacing thread, which coalesces at most one UI callback. Hidden/resting states lower wake frequency. There is no global `timeBeginPeriod` change and no spin loop.

This is nominal-mode-aware scheduling, **not** proof of present-to-vblank synchronization, variable-refresh synchronization or exact fractional refresh accuracy. DWM/GDI scheduling and available CPU still determine delivered frames; diagnostics show measured FPS.

- https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsw
- https://learn.microsoft.com/windows/win32/api/synchapi/nf-synchapi-createwaitabletimerexw

## Media and notifications

GSMTC exposes current-session playback and optional metadata, with events. Audio onset accents use explicit opt-in, local render-endpoint loopback; no microphone, recordings or transcription. Missing metadata is not synthesized. Incoming Windows notification observation still uses package identity plus `UserNotificationListener` permission; it is not falsely presented as available in the unsigned portable edition.

- https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager
- https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmediaproperties
- https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/notification-listener
- https://github.com/naudio/NAudio

Scheduled toasts have a packaged/unpackaged registration distinction: the available Microsoft scheduling documentation explicitly requires compatibility registration rather than assuming `ToastNotificationManager` works unpackaged. [2](https://learn.microsoft.com/sr-cyrl-rs/windows/apps/design/shell/tiles-and-notifications/scheduled-toast?view=xamarinios-10.8)

For this portable app, optional closed-process reminder delivery uses **current-user Task Scheduler** to launch the installed executable, plus persisted due-state reconciliation and ordinary tray notifications. No task message text, credentials or external command code is stored in the task action. The only action is this executable with `--reminder`. The user must opt in. Task Scheduler is a different choice from a Windows App SDK toast pipeline; its Windows integration has not been executed in this environment.

Microsoft documents current-user interactive-token registration without elevation, and notes `StartWhenAvailable` catch-up can be delayed, with a default queue delay of ten minutes. Accordingly, the product does **not** promise reminders while powered off, signed out, or exactly on time after missed execution. [3](https://learn.microsoft.com/en-gb/windows/win32/taskschd/tasksettings-startwhenavailable)

https://learn.microsoft.com/windows/win32/taskschd/security-contexts-for-running-tasks

## Notebook storage and Markdown

Use independently stored, bounded documents with atomic replacement, disk flush, a previous revision and quarantine of damaged originals. This isolates note failures rather than placing every note in one fragile configuration JSON. An explicit archive retains deleted data. Markdig parses Markdown; native RichTextBox renders styled runs. Raw HTML is disabled, remote images are not downloaded, and only explicit HTTP/HTTPS links can launch a browser. The editor has Markdown formatting actions and a separate read view, not a full WYSIWYG document engine.

https://github.com/xoofx/markdig

## Animation/physics decision

Retain fixed-step, deterministic-testable dynamics and procedural rig poses, add species-specific anatomy/dance tuning and spring-constrained toy interaction. This is meaningful local simulation and avoids GIF-only locomotion. Shared locomotion, one toy class and basic page-fold animation do **not** satisfy the requested independently authored 100+ animation-moment library, general-purpose physical object rigging or cinema-quality notebook motion. See the acceptance report rather than inferring those capabilities from a feature count.

## 1.2 corrective investigation

Primary documentation was rechecked for these specific failure paths:

- [GSMTC GetSessions](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager.getsessions): exposes the available session list. A paused current session need not be the only player worth inspecting. The implemented selector prioritizes actual playing status; artwork is independent.
- [CreateDIBSection](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection): directly accessible pixel storage, top-down BITMAPINFO layout, selected-object and DeleteObject ownership. The implementation keeps the DIB/DC across frames, flushes GDI before direct writes and supplies premultiplied BGRA to the layered window.
- [Rich Edit controls](https://learn.microsoft.com/en-us/windows/win32/controls/about-rich-edit-controls): native Unicode, selection, undo, text/RTF and formatting. Used to replace a cluttered document-style UI, not to implement a custom keyboard/text engine.

First-principles motion correction: when a body advances at speed v, a planted foot must move backward relative to it at approximately v, then return during swing. The gait now advances with displacement (normalized velocity × dt) and uses a stance/swing curve. Acceleration feeds damped lean; finite two-link IK bounds arm reach. This is still not complete terrain foot locking or skeletal routing.

Frame count is not an art-quality metric. The revision uses a shared procedural rig rather than downloading hundreds of inconsistent frames. The observed visual improvement and remaining premium-art gap are documented separately; no competitor's performance or art pipeline is claimed as reproduced.
