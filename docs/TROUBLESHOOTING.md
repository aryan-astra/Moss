# Troubleshooting

## Only the tray icon appears

Exit old Moss processes through their tray menu before running the newly extracted EXE. The earlier state-5 visibility bug is covered by a regression test. The tray now shows the current visibility reason. **Show Moss / recover position** re-creates the overlay and reveals it for 30 seconds. Real fullscreen/presentation settings resume afterward. Capture exclusion can intentionally hide the pet from a remote-sharing stream even while it appears locally.

## My note is not saved

Keep the notebook open. Select and copy your text into a safe local document. Check available disk space and write permission for `%LOCALAPPDATA%\Moss\notebook`. A normal application exit is blocked if the notebook cannot save. No program can guarantee recovery after a forcibly terminated process or failing disk. Back up the local data folder separately.

`.bak` files hold previous revisions. `.corrupt-*` preserves damaged originals. `.deleted` means an explicitly archived page. Do not delete these files to reset pet settings.

## A command is rejected

Use `@timer 25m`, `@timer 1h30m`, `@remind tomorrow 9am call home` or `@alarm 7:30am`. Place the caret on that line or select it, then click Schedule. You must confirm the interpreted time. Without AM/PM, hours use the 24-hour clock. Invalid and daylight-saving-ambiguous wall times require correction; use a duration if necessary.

## Reminders do not arrive while closed

Closed-app delivery is opt-in and depends on successful Task Scheduler registration. Inspect the status under Notebook. Keep the executable at a stable path; toggle the option off/on after moving it. The user must have an available signed-in Windows session. The PC is not woken. Windows can delay missed task execution. This scheduler path has not been executed on native Windows in the build environment.

Due items persist in the Notebook area. Windows notification suppression does not dismiss them.

## Incoming notification awareness is unavailable

The portable build has no signed package identity/capability for UserNotificationListener. The feature is not offered as an enabled portable control. MSIX source configuration is included, but a trusted signing identity, installation and user permission are needed. There is no supplied signed installer.

## Music is not recognized

The player must publish a GSMTC media session. Confirm media permission and Music React/Dance. Metadata and speaker-onset analysis are independent opt-ins. Audio output alone does not fabricate a media session. Device failures degrade to ordinary behavior.

## Appearance, DPI or input looks wrong

Use Reset position, default pet size and the current app folder. Advanced diagnostics/world inspector shows the model. The physical-pixel arithmetic is tested, but actual Windows mixed-DPI, screen-edge behavior, overlay blending and input acceptance are not certified. See ACCEPTANCE.md rather than assuming the build has passed those hardware tests.

## Resource use is too high

Choose Battery, reduced motion, sound/audio analysis off, or Hide. Close the debug world inspector. Inspect delivered FPS and process metrics, not just the selected target. The product has not received an end-to-end Windows CPU/GPU/long-session profile.

## Remove everything cleanly

Exit Moss. Run the included portable-uninstall script to remove this user's startup/reminder task registrations, then delete the extracted app folder. Local notes are retained unless `-DeleteLocalData` is explicitly supplied. Do not run the deletion option if you want to keep notes.

Logs: `%LOCALAPPDATA%\Moss\moss.jsonl`, rotated to `.1`. Logs intentionally exclude private note/reminder text, media titles, window titles and notification content.

## Incorrect dropdown defaults

1.2 uses unbound dropdown items so parenting cannot reset the displayed selection. Wine reproduced the old Size field displaying Small while the stored value remained Default. That is not proof that stored music settings changed. The music fixes independently address session selection and behavior cooldowns.
