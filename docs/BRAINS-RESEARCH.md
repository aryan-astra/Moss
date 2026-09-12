# Small pet brains: feasibility, privacy and safe import

Research date: 12 September 2026. This document is a design investigation, not a claim that neural inference, visual recognition or trained-brain import ships in Moss 1.3. The shipped decision system remains procedural utility selection plus explicit safety/interaction rules. No neural runtime, model downloader or screenshot collector was added.

## 1. Recommendation

**Yes: a genuinely small neural policy is feasible. Start with structured, permission-filtered observations, not pixels, and never give an imported model direct control of Windows.** The policy should suggest a small number of legal creature actions. Physics, privacy, fullscreen policy, cooldowns and user control stay in deterministic code.

For the first implementation, a fixed-shape managed multilayer perceptron with validated numeric weights is a better fit than adding a general ONNX runtime. That is an engineering recommendation for this application, not a claim that managed code universally outperforms ONNX. The network is so small that deployment size, input design and safety dominate arithmetic throughput.

Different species can have different weights, preference vectors, motion libraries and short recurrent state while sharing one executable and one physics/safety layer. Only the selected pet needs an active policy.

### What a brain would and would not solve

| Need | Neural policy useful? | Other system still needed |
|---|---|---|
| Select a varied, appropriate next activity | Potentially | Authored activities, hysteresis and cooldowns |
| Remember recent interactions | Small recurrent policy or explicit memory | Bounded persistence and reset semantics |
| Recognize nearby window ledges | Not necessary | Existing Win32/DWM geometry |
| Recognize a toolbar, button or text field | Sometimes | Opt-in UI Automation or separately consented visual perception |
| Read song identity from Chrome | No | Browser/platform metadata cooperation; a level meter cannot recover a title |
| Animate a convincing dance | Not by itself | Poses, timing, joint limits, contacts and species choreography |
| Plan a route between disconnected ledges | Could rank candidates | A surface graph and a verified planner |
| Safely import arbitrary executable intelligence | No | Narrow data-only interface and resource controls |
| Know what every desktop pixel means | No | A suitable model, training data, permissions, uncertainty handling and substantial evaluation |

Do not sell “a neural brain” as a fix for missing art, weak locomotion, unreliable media APIs or incomplete navigation. Those are separate engineering problems.

## 2. What information can the pet perceive?

### Layer A — geometry and explicit events: recommended default

Existing inputs already include monitor/work rectangles, exposed window tops, support identity, body velocity, cursor coordinates when permitted, recent interaction types, media-playing state, affection and rest state. Add a compact neighborhood summary rather than a variable-length dump of every window.

No screenshots or private application text are necessary to learn that the pet is near an edge, a ledge disappeared, a toy was pulled, or music started. Use validity bits for missing/denied observations. Do not encode “permission off” as a plausible coordinate or as silence indistinguishable from valid zero data.

### Layer B — semantic UI structure: optional, separate permission

Windows UI Automation can expose control types, bounds and provider-supported properties. It is not uniformly available, and retrieving names/value text could disclose private data. A conservative adapter would initially expose only coarse roles and rectangles inside a user-approved window; never password/value/text fields by default.

Microsoft documents integrity-level boundaries and specifically says UIAccess must not be used by non-assistive applications merely to appear above other windows. Moss must remain a normal-user desktop companion, not pretend to be an accessibility tool to bypass those boundaries. [Microsoft UI Automation security](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview)

### Layer C — pixels: explicit opt-in research track only

Visual recognition means capturing pixels. That is a new privacy capability, not a harmless extension of window geometry. A possible implementation would use a user-selected window/region, a visible capture state, low sampling frequency, in-memory frames and immediate discard. No capture on the secure desktop, no silent monitor-wide capture, no training-history collection and no model-defined expansion of scope.

Consider a separate permission for OCR; text extraction is more sensitive than recognizing coarse shapes. Never assume a “small model” makes capture private. Local-only processing reduces transmission exposure but does not eliminate collection or inference risks.

A 224 × 224 RGB float input is 602,112 bytes (~588 KiB), before activations or weights. A hypothetical one-million-weight float model alone needs about 4 MB of weights. Neither number is a whole-process RAM estimate. Detection activations, image conversion, capture buffers and runtime libraries can dominate the tiny policy's cost. These are arithmetic examples, not measured performance promises.

## 3. Candidate brain architectures

| Candidate | Strengths | Weaknesses | Recommendation |
|---|---|---|---|
| Utility rules + small explicit memory | Explainable, bounded, cheap, controllable | Needs authored diversity and careful tuning | Keep as fallback and safety reference |
| Fixed MLP over structured observations | Tiny arithmetic/storage, easy validation and imitation training | No inherent temporal memory; limited generalization | Best first imported policy |
| GRU over structured observations | Learns short temporal context | Harder debugging, hidden-state handling, oscillation risk | Optional after MLP acceptance |
| General ONNX policy | Familiar training/export ecosystem, optimized kernels | Larger dependency and graph validation surface | Only when model complexity justifies it |
| Tiny vision network + small policy | Can infer coarse visual categories | Capture consent, dataset/domain-shift problems, higher resource use | Separate opt-in component, not default |
| Local language model | Rich text interpretation | Unnecessary memory/compute and conversational scope | Reject for this non-chat creature |
| Online reinforcement learning on the user's desktop | Can adapt in principle | Uncontrolled exploration, interruption incentives, privacy and evaluation problems | Do not use as initial product behavior |

### A concrete small policy budget

Example topology: **32 inputs → 24 tanh hidden units → 12 output scores**.

- First-layer weights: 32 × 24 = 768; biases: 24.
- Second-layer weights: 24 × 12 = 288; biases: 12.
- Total parameters: **1,092**.
- Raw float32 weight/bias storage: **4,368 bytes**.
- Dense multiply-accumulates per inference: **1,056**, plus activations, normalization and selection.
- At four decisions per second: 4,224 dense multiply-accumulates/second.

JSON encoding and validation objects cost more than the raw array. Inference buffers should be allocated once. The arithmetic is small, but startup, GC, frame scheduling and the renderer still require actual measurement.

A GRU with 32 inputs and 16 hidden units needs, under a one-bias-per-gate formulation, 3 × (32×16 + 16×16 + 16) = 2,352 parameters; a 16→12 head adds 204. Framework bias conventions can differ. This is an order-of-magnitude illustration, not an exported model specification.

## 4. Training pipeline

1. Freeze a versioned observation/action contract. Record which fields can be unavailable and how normalization works.
2. Generate synthetic worlds: multiple DPI scales, negative monitor coordinates, moving/deleted ledges, different cursor paths and toy interactions. Do not gather personal screenshots as an implicit training step.
3. Produce demonstrations from corrected hand-authored behavior or explicit developer annotation. The current utility controller is a starting teacher, not a source of perfect labels.
4. Split training/validation by world layout and interaction sequence, not adjacent frames from the same episode. Otherwise temporal leakage can make results deceptively good.
5. Train a small policy with imitation learning. Balance rare but important states such as interrupted climbs and missing permissions.
6. Evaluate the closed-loop policy in the physics simulator. One-step classification accuracy is insufficient; count invalid actions, interruptions, oscillations, stuck episodes and unsafe route suggestions.
7. Distill/prune only if measurements justify it. Keep the deterministic fallback and a known-good model.
8. Tune on a native Windows acceptance matrix without capturing private content. Ship weights only after provenance/license and safety review.

Reward design must not turn the pet into a demand machine. Penalize obscuring user work, repeated attention seeking and oscillation; do not reward extracting clicks, maintaining streaks, or making the user feel guilty for absence. Rest and quiet should be successful outcomes.

## 5. Runtime architecture and hard limits

```
Windows adapters → permission filter → fixed observation vector
                                         ↓
                                  policy suggestion
                                         ↓
                      finite-value / legal-action / cooldown gate
                                         ↓
                         behavior state machine → physics → rig
```

The policy cannot open files, inspect URLs, call Windows APIs, change permissions, control reminders, launch processes or inject input. It cannot override quiet mode, fullscreen hiding, pause, dragging, safe body bounds or capture permissions. It may rank actions that already exist.

Suggested initial cadence: 2–4 Hz when awake; suspend inference when hidden/paused; keep rendering independent. These are proposed scheduling targets, not tested latency guarantees. Debounce decisions and impose minimum activity durations. After repeated invalid/late output, fall back immediately and show a non-private status.

A hand-written fixed topology has bounded loops and no arbitrary graph operators. If general ONNX is later supported, host it in a separate constrained worker, use a watchdog, fixed shapes, bounded tensor sizes and an explicit operator allowlist. A file extension or cryptographic hash alone does not make a model safe.

### ONNX resource tuning, if adopted

ONNX Runtime documents that its default intra-op thread configuration can use physical cores and that worker spinning consumes CPU/power. For a tiny policy, begin with one intra-op thread, sequential execution and spinning disabled; benchmark rather than copy settings blindly. Availability of newer spin-duration controls depends on the pinned runtime version. [ONNX Runtime threading](https://onnxruntime.ai/docs/performance/tune-performance/threading.html)

Quantization is not lossless. Static quantization needs representative calibration; dynamic quantization adds runtime parameter computation. For ~4 KB of float weights, quantization complexity may save less than it costs. Evaluate behavioral trajectories, not just tensor error. [ONNX Runtime quantization](https://onnxruntime.ai/docs/performance/model-optimizations/quantization.html)

Microsoft's ONNX Runtime security policy provides a reporting channel, not a guarantee that arbitrary third-party models are sandboxed. Follow the dependency's advisories and pin/update versions. [Security policy](https://github.com/microsoft/onnxruntime/blob/main/SECURITY.md)

## 6. Proposed import format and menu

This is a **proposal**, not an existing button in 1.3. Do not expose a decorative Import Brain control until validation, inference, rollback and documentation actually work.

Suggested package: one bounded JSON file for the fixed MLP, with:

- format version and model ID;
- display name, author, license and training/provenance note;
- exact observation/action schema IDs and species compatibility;
- normalization version;
- fixed-size numeric weight/bias arrays;
- SHA-256 digest over a defined canonical weight payload;
- optional bounded preference defaults, never code or capability requests.

Reject unknown topology, NaN/infinity, excess lengths, absurd magnitudes, oversized files and incompatible schema versions before activation. Do not support DLLs, scripts, pickle, arbitrary callbacks, custom ONNX operators, external tensor files, or paths/URLs inside the model. A ZIP container would additionally require traversal, duplicate-entry and decompression-bomb defenses; a single bounded file is simpler.

The import flow should show identity/license/schema and exactly which observations are available. Importing does not grant access to missing inputs. Activate only after validation, retain the previous model atomically, and provide “Use built-in behavior” and “Remove imported brain.” An untrusted local model still needs validation; “offline” is not equivalent to harmless.

A separate per-pet setting chooses the policy. Notes, reminders and permissions remain outside the model. Neural hidden state should not be persisted across incompatible models; small explicit memory is easier to inspect and migrate.

## 7. Acceptance and measurement plan

Measure cold load, first inference, steady-state p50/p95/p99 inference time, allocations per decision, private working set, thread count, whole-process CPU and energy impact. Compare identical event traces with the built-in controller. Include a no-model baseline; don't report only kernel microbenchmarks.

Behavior tests: missing permissions, invalid inputs, changing DPI, monitor removal, prolonged silence, paused sessions, input grabs, rapid target changes, rest, long absence, windows disappearing during a plan and repeated invalid outputs. Require a bounded fallback response. A safety gate should reject every intentionally illegal test action even if the model assigns it the highest score.

Fuzz the importer, test corrupt/truncated weight arrays and large numbers, and verify rollback after process termination. Test repeated load/unload and resource handles on Windows. Imported policies should not weaken the existing note-safety or least-privilege guarantees.

## 8. Music research relevant to perception

Chrome documents Media Session metadata/action integration: the website/browser cooperates in supplying identity and controls. This is not equivalent to every audible tab publishing a usable Windows session. Do not ship a browser-history scraper to fill the gap. [Chrome Media Session](https://developer.chrome.com/blog/media-session)

Windows peak meters expose normalized maximum levels; software meters may report zero in exclusive mode. They cannot identify a song, distinguish speech from music, supply duration, or prove beats. This is why 1.3's fallback can trigger animation but must not manufacture metadata. [Microsoft peak meters](https://learn.microsoft.com/en-us/windows/win32/coreaudio/peak-meters)

Historical advice about Chrome's experimental media-key flags is not a robust current integration contract. The product does not depend on changing undocumented browser flags. Native Chrome/Spotify/device testing remains necessary; Wine does not validate these APIs.

## 9. Decision summary

Keep the pet non-speaking except for the explicitly requested rare short music compliments; a neural policy does not require a chatbot. Start with structured perception and a tiny fixed model. Make vision a separately consented capability. Keep user control and physics outside the model. Treat imported brains as untrusted data. Improve authored behavior and art alongside, not instead of, the policy.

No trained model, neural import path, OCR, UI Automation adapter or screen-capture pipeline is included in this release. The research explains how to add them responsibly without claiming they already exist.
