# 1.2 correction audit

## User-visible corrections

- Replaced the large notebook/editor interface with a 430 × 380 logical-pixel sticky note: narrow 83-pixel rail, 8-point identification text, small previews, paper-grain margins, glue strip, native editor and brief fade/fold indicator. The writing canvas is solid paper color, not a transparent/custom-rendered input surface. This is a sticky-note treatment, not a literal recreation of Windows 7 assets or physically simulated paper.
- Removed the visible title field, file menu, Markdown buttons and Read mode. Formatting and confirmed scheduling remain on shortcuts/right-click. Preserved old IDs and plain text; optional RTF is bounded and backed up with the same document.
- Gallery images are silhouette-cropped and rendered at higher resolution before downsampling. Softer outlines, cached coat gradients, cheeks, changed penguin face proportions and larger portraits replace the previous tiny previews. These remain seven procedural rigs, not newly commissioned premium sprite libraries.
- Locomotion phase is integrated from velocity rather than a fixed animation clock. Stance feet move backward relative to the body; swing feet lift. Acceleration adds damped lean; arrival braking/pauses reduce linear motion. Rabbit/penguin paws share the stance curve. This is not a complete world-space foot-lock/terrain IK solver.
- Toy reaching uses bounded two-link arms, a hand target and strain-dependent bracing. Detachment leaves a short disappointment state; recovery produces relief. Retrieval requests are throttled rather than resetting agency every simulation step. The twig is one simplified physical toy; not a general articulated rigid-body accessory system.
- Per-pet mood, energy/rest and attention now persist. A long absence relaxes state toward neutral/rested values; there are no chores or neglect penalties. This is modest emotional continuity, not a complete story memory.

## Music failure paths corrected

1. Reconcile all GSMTC sessions, not only GetCurrentSession. Prefer a playing current session, then a previously selected playing session, then another playing session.
2. Playback updates are independent of optional metadata/artwork requests and failures. Session changes/events trigger bounded refresh, with polling fallback.
3. Permission revocation is handled before the asynchronous busy gate; late optional metadata cannot repopulate revoked data.
4. Grounded dancing no longer waits behind a random dance cooldown. Real pause stops dance; calm mode, holding, airborne state and landing recovery retain priority. Fixed tests exercise 80 random seeds.
5. Unbound dropdown initialization prevents the displayed selection from resetting on parenting. Wine showed the old Size dropdown displaying Small while the stored value was Default. Stored music being switched off was a suspected consequence, **not established by that observation**.
6. Audio-device failure permits playback-only reactions and later output-analysis retry. No timer is represented as a measured beat; onset accents still require opt-in WASAPI samples.

Windows media APIs are absent in the Wine environment. The all-session selector and behavior are regression-tested, but real browser/player/device interoperability is not certified.

## Resource/lifecycle work

- Persistent top-down 32-bit premultiplied-alpha DIB and memory DC per visible overlay, recreated only on resize. Selected objects are restored before native deletion; the buffers/DCs are deterministically disposed.
- Default prop canvases: 48² + 36² = 3,600 pixels instead of 2 × 180² = 64,800 pixels, a **94.4% reduction in prop-canvas pixels**. This is not a whole-app RAM or CPU percentage.
- Cached book pixels; small-angle toy redraw threshold; position-only updates for unchanged prop pixels.
- Balanced rendering capped at 60 Hz, resting states at 24 Hz, sleep at 5 Hz; hidden drawing remains stopped. Smooth/high-refresh mode remains available.
- Window-event comparison moved inside world-revision reconciliation. Removed per-step toy surface sorting. Cached/disposed shell fonts, disposed rebuilt tray items and process diagnostics handle.
- Advanced diagnostics expose GDI handles and owned DIB count. Counter availability and resource behavior still need actual Windows soak evidence.

## Evidence collected

- 82 core tests, including 360,000 randomized simulation steps; current output in `test-results.txt`.
- 42 nonempty production-renderer poses under libgdiplus; contact sheet in `evidence/pose-contact-sheet.png`.
- Three Windows-component checks under Wine: 100 dropdown-parenting cycles without a change event; native RichEdit formatting/Unicode text round-trip; 2,400 UpdateLayeredWindow calls over 120 DIB allocation/disposal cycles, with owned-buffer count returning to baseline. These do not certify total Windows GDI/process leak freedom.
- Actual application launched under Wine. Notebook opened, accepted typing, saved plain+RTF, closed, and reopened the same content after app exit/relaunch. Gallery and live pet/prop overlays rendered. Screenshots are cropped captures from that compatibility environment, not Windows screenshots.
- Release build and self-contained x64 publish, plus independent extracted-source rebuild and packaging checks, are recorded separately in `packaging-audit.txt`.

No native Windows, multi-monitor/DPI hardware, real media device, Task Scheduler delivery, notification permission, multi-day soak or end-to-end latency acceptance is claimed. The full master brief remains unmet; see `ACCEPTANCE.md` for the substantive missing systems, not just missing tests.
