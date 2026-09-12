# Moss 1.3 — interaction and music revision

## Implemented

- Browser/audio fallback uses the default output peak meter when no GSMTC session reports playing. Enabled alongside media awareness by default, with its own setting. No recording or microphone; not song identification or guaranteed beat detection. It can react to other sustained audio and can miss non-default/exclusive/very quiet output.
- Click a visible headphone/player region, or tray → Now playing, for a compact native music card. Metadata is opt-in. Supplied title/artist, bounded artwork thumbnail and reported elapsed/duration/progress are shown when available. Missing metadata is explicitly unavailable, never replaced with a paused Spotify title for unidentified audio.
- Species devices: headphones, CD player, cassette, radio and turntables. Floating vector notes and rare short randomized compliments accompany playback; comments can be disabled.
- Dance preserves body proportions, adds side-to-side weight shifts and occasional physics-driven hops. Reduced motion/calm policy suppress automatic hops. Calm no longer enters autonomous jump/climb decisions.
- Petting emits a bounded burst of hearts for every species. There is no unbounded particle system.
- Slow dragging in any direction no longer breaks the twig just because it is far away. Smoothed movement and shocks accumulate grip strain; hard pulls/reversals can detach it. A damped constraint lets the body follow and lift with the twig without a full jump impulse. Reaching switches to the appropriate side.
- A temporary football can roll from either work-area side, bounce, be kicked/chased locally or be dragged. Automatic arrivals are infrequent and configurable. Pet → Roll a football is also available.
- Main preferences retain immediate saves and now also expose Save changes. Names still save on leaving their field. Notes retain their own autosave/error protection.
- Added cursor-travel wake handling and a movement-intent recovery watchdog. Intentional resting is not treated as a failure.
- Additional audit fixes include persistence-first reminder state changes, isolating reminder storage failures from the render loop, shutdown guards and output-meter COM cleanup/retry.

## Validation scope

Current test output is in `test-results.txt`. New tests cover scalar audio hysteresis, dance deformation bounds, heart lifetime, physical hops and reduced motion, gentle/violent/vertical grip behavior, football lifetime/scale bounds and calm-mode jump suppression.

`evidence/1.3/poses.png` exercises 42 production-renderer poses. `rig-motion.mp4` is a 384-frame, seven-rig exercise driven by simulated media input—not a Spotify/Chrome recording or proof of beat synchronization.

Wine exercised the application, music-card unavailable/consent state and football overlay. Component checks exercise dropdown parenting, RichEdit, repeated DIB submission/disposal and safe output-meter failure/revocation. Wine has no usable audio device/media-session service here, so these are not real browser/device tests.

Native Windows media/player/device compatibility, mixed-DPI hardware, notifications, scheduler and long-run resource acceptance remain unverified. No claim of zero bugs/leaks or completed premium art/100-moment master scope is made.

## Requested documents

- `BRAINS-RESEARCH.md`: researched lightweight neural-policy/perception/import design, explicit cost arithmetic, security/privacy and evaluation. Neural import is a proposal, not a shipped feature.
- `TECHNICAL-ARCHITECTURE.md`: actual system architecture, tradeoffs, lifecycle, modules, failure boundaries and file-by-file distribution inventory.
