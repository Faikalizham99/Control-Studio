# Custom Text layers

Included in **0.2.39 (41)**. The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build. Signing and IPA workflow behavior are unchanged.

**Add layer → Text** creates a selected layer named Text 1, Text 2, etc., starting with “Your text” in the current app accent. Photo, Clock, Date and Text share the eight-layer limit. Text supports Small, Medium, Large and iPhone Extra Large with the editor's existing full-proportion live preview.

## Editing and behavior

The existing shared Clock/Date toolbar adapts its first icon to Text **Content**. A multiline editor has a live 500-unit UTF-16 counter. Line endings become LF, unsupported control characters are removed, malformed Unicode becomes replacement characters, and length limiting preserves grapheme boundaries. Blank text is allowed and displays nothing, with an inline notice. The input field keeps a fixed height and scrolls internally so typing does not continuously resize the page. Ordinary edits do not reassign the editor's text or reset its caret; scroll anchoring covers the blank-content notice's layout change.

Font specimens, all existing fonts and available weights, colour picker/hex validation, text size, alignment, opacity, Centre horizontally and keyboard-style position arrows reuse the same controls and dynamic accent/disabled surfaces. Time, seconds, AM/PM, date format and weekday controls are hidden and do not enter Text data. Text layers use the Text icon and a short Text subtitle in the existing one-row layer controls.

Dragging, off-canvas placement, centred pinching, precision size/position, lock/unlock, hide/show, ordering and duplicate actions reuse the existing layer paths. Duplicate retains content and appearance with a new independent ID. Locks protect placement while leaving content/appearance editable. Hidden or zero-opacity Text is omitted from saved visible content. Save widget persists the draft; editing and duplicating do not autosave.

## Rendering and persistence

Text appearance uses its own typed options, adapted to shared font/colour/layout appearance without persisting clock/date switches. Layer kind Text is appended after the existing Photo/Clock/Date values; its optional payload is omitted from older records. Catalog schema remains 1. Mixed/missing payloads, invalid appearance and over-limit content are rejected before persistence. Older versions cannot edit Text catalogs; no lossy downgrade conversion is introduced.

Preview and export use the same native UIKit compositor. Multiline layout measures attributed strings using paragraph alignment, word wrapping, line fragments and font leading, then reduces font size as needed and clips only to the layer/widget frames. This follows Apple's [multiline sizing guidance](https://developer.apple.com/documentation/foundation/nsstring/boundingrect%28with%3Aoptions%3Aattributes%3Acontext%3A%29/). Native fonts, paragraphs, attributed strings and temporary attribute dictionaries are disposed after use. The outside editing ghost uses the same font-size calculation; the actual iOS canvas uses the shared raster compositor.

Custom Text joins consecutive photos/static content groups. Clock and Date remain live scene separators, preserving all stack ordering. Text-only designs use their saved PNG without a live scene or editor timer. Mixed designs publish static image groups with the existing schema-1 Clock/schema-2 Date manifest; no selector, WidgetKit family or scene schema changes are needed. Source photos remain private; existing slot assignments stay attached to the same design ID. This static Text rendering also works without Clock/Date's iOS 18 requirement; iPhone XL still requires iOS 27.

Home Screen widgets expose visible custom text to VoiceOver as the widget's accessibility value in addition to the existing design label. Hidden, zero-opacity and blank text are excluded. Content and all generated files remain on the device and its App Group.

## Verification and remaining device checks

Automated tests cover multiline/Unicode normalization, boundaries and malformed input; every font/weight and appearance round-trip; mixed payload rejection; backward serialization; static and live group ordering; all-size centred pinch/nudge/lock/duplicate behavior; shared UI editing in both themes; hidden time/date settings; content limits; picker cancellation recovery; stable scroll offsets; independent copies; the eight-layer limit; save/reopen/publication and unchanged slot IDs. A shared Text catalog fixture exercises the managed contract and native decoder test.

Windows can run Core, Infrastructure, headless MAUI App and packaging tests, source consistency checks and managed iOS compilation. It cannot run the two added Swift tests or verify actual UIKit text layout/keyboard/VoiceOver behavior.

Completed verification: 213 Core, 87 Infrastructure, 366 headless MAUI App and 41 packaging tests passed (707 total). Source consistency and whitespace checks passed. Managed iOS compilation completed with zero warnings/errors. The two added Swift tests remain unexecuted on Windows.

Physical iPhone checks: add Text and enter several lines, emoji, Japanese and long words; inspect all fonts/weights/alignment and very small/large frames; check caret/keyboard and manual scrolling; pinch/drag/nudge/lock/hide/duplicate/reorder; save and reopen; assign static-only and mixed Text/Clock/Date designs to each family; verify Home Screen ordering, transparency, quality, ticking and VoiceOver content. No iOS delivery acknowledgement is implied by local tests.
