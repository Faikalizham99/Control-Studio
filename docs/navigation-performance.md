# Navigation responsiveness — 6 November 2026

Version 0.2.53 (55) reduces avoidable control construction and gallery work. It does not change saved data formats, rendering sizes, widget slots, drafts, cut-out assets or release triggers.

## Findings and changes

- The widget editor constructed every font specimen/weight control and blend choice while their panels were hidden. Font specimens and blend choices now load on first opening; weight controls load only for selected fonts. Saved style/sample values are retained before loading. Opening the panels still uses the existing viewport-preservation hooks. Controls remain bounded and reusable for the editor lifetime.
- My Widgets rebuilt every matching card after returning, even when independently decoded library data was unchanged. It now compares complete design values, render paths and timestamps before rebuilding. Changed names, layers, dimensions, files and deleted designs invalidate the cards. Retained cards release their image sources when hidden; sources are restored only after revalidation. The existing six-source viewport budget remains.
- Passive gallery publication previously kept actions disabled while preparing Home Screen payloads. It now runs as an observed, cancellable operation after the library is usable. Explicit Refresh still awaits publication and reports its result. Generation/lifetime checks reject late passive results after Refresh, deletion, navigation or backgrounding. Publication file/JSON work runs on a worker; native reloads run on the main thread and recheck cancellation.
- Studio, gallery, slot, assignment and Clipboard navigation share duplicate-tap protection. Initial reads wait for the push transition to finish, rather than populating a complex list during the animation. Studio gives immediate pressed feedback. Loading captions/spinners use the app accent; read-only loading allows Back. Save, assignment and other mutations remain protected. Back during a push waits for that transition; duplicate assignment-close requests are rejected.
- Clipboard reads apply local results only in the current foreground lifetime. Completed pin/delete writes finish their persistence/asset cleanup, while stale UI results are discarded and a fresh read is queued. Cancelled reads, failed factories and backgrounding release action guards for retry.

## Local measurement

`NavigationPerformanceTests.Managed_construction_and_unchanged_gallery_probe` constructs the real production editor with one photo, then returns to the real gallery containing 50 designs. Stores return immediate local fixtures. The baseline used the same probe/interface before the optimizations. Each isolated run uses one cold editor, seven warm editors, then seven unchanged gallery returns. Warm values below are medians.

| Managed work | Before | After |
| --- | ---: | ---: |
| Cold editor construction | 214.365 ms | 158.796 ms |
| Warm editor construction | 109.699 ms | 77.814 ms |
| Warm editor thread allocations | 21,246,120 B | 14,855,672 B |
| Editor visual elements | 1,607 | 1,238 |
| Unchanged gallery return | 91.653 ms | 8.390 ms |
| Gallery return thread allocations | 20,403,368 B | 1,145,784 B |

Environment: Windows, Release, .NET 10.0.401, handlerless MAUI controls. The allocation probe measures the current managed thread. Timings vary with JIT, GC and machine load. These are **not iPhone tap-to-frame measurements**: they exclude UIKit layout, image decoding, AOT, native presentation, filesystem latency and rendering. Deferred work moves to first opening of its panel; it is not eliminated. No brittle wall-clock pass/fail threshold is added.

Navigation logs record page type, construction, appearing, transition completion and data readiness elapsed times. They exclude item titles, content, paths and URLs. Data readiness means the managed model is applied, not that native images have rendered. Timing failures cannot break navigation; production log filtering may omit these events.

## Review and verification

Automated coverage exercises lazy panel selection, hidden-control safety, fonts/weights, gallery card identity and invalidation, independently decoded battery options, image-source release, cancelled/foreground reads, passive versus forced publication, stale publication results, Back during reads/transitions, repeated taps, construction failure, logging failure and background cancellation. Existing editor tests cover history, gestures, panel scroll position, draft persistence and picker recovery.

Verification: all 560 App tests and 67 packaging tests pass; source guards pass; managed iOS compilation passes with zero warnings/errors. No native Swift code or workflow trigger changes are included. Managed compilation on Windows does not run Xcode, UIKit or a device.

Physical iPhone verification remains:

1. Compare cold and warm Studio → My Widgets, Add/Edit, Counter, Clipboard and slot/assignment navigation. Use a populated library and the largest photo-heavy design. Check first Font/Blend opening as well as subsequent openings.
2. Tap rapidly, use Back while loading, background/resume, cancel Photos/Files and reopen an editor with an unsaved draft. Check controls recover and no duplicate pages or stale cards appear.
3. Edit/save/delete designs, refresh explicitly and assign each supported size. Confirm previews, slot assignments and Home Screen updates remain correct.
4. Check light/dark mode, extreme accents, VoiceOver, larger text and Reduced Motion. Check loading rows do not cause clipping or unexpected scroll jumps.
5. Profile a Release build with native timing tools if remaining pauses occur. Capture the exact page/operation and cold versus warm behavior before choosing further optimizations.
