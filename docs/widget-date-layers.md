# Date layers — 0.2.37 (39)

Add layer now offers **Photo, Clock and Date**. Date uses the same selected-layer icon toolbar and inline settings host as Clock. Its first icon is a calendar opening Date format. Font/weight, colour, text size, alignment, horizontal centring and opacity are shared. The live preview keeps its existing full widget proportions for Small, Medium, Large and iPhone Extra Large. The eight-layer limit includes photos, clocks and dates together.

## Formats and editing

The four presets show current examples and a selected accent surface:

| Preset | Example |
| --- | --- |
| Full date | 25 October 2026 |
| Short date | 25 Oct |
| Numeric date | 25/10/2026 |
| Month | October |

Date format also includes **Show weekday** and **Weekday style: Short / Full**. These apply independently to every preset: `Sun, 25 October 2026`, `Sunday, 25 Oct`, `Sun, 25/10/2026` or `Sunday, October`. Disabling the switch keeps the style selection for later. The style selector stays visible but disabled while weekdays are off, so toggling does not remove a settings row. Preset examples, live preview, export and the Home Screen use the same choice, with a single weekday prefix.

New Date layers start with weekdays off. Saved options missing the new switch retain the earlier appearance: Short date includes an abbreviated weekday; other presets omit it. A nullable compatibility value represents those older records. Any format change captures the current effective switch value, so switching presets does not unexpectedly enable/disable the weekday. Missing weekday style defaults to Short. Explicit on/off and Short/Full values persist independently of font or colour edits.

These first presets intentionally use English month/day names and the Gregorian calendar, matching the explicit examples. They follow the iPhone's local time zone. There is no custom format or locale selector in this change. Seconds, 12/24-hour and AM/PM controls are hidden when a Date is selected and are not stored in its settings. Selecting a Clock restores its time controls and digit specimens; selecting Date uses letter/digit font specimens.

Date layers inherit the app accent on creation and retain their selected colour afterwards. All eight existing fonts and their real supported weights are reused. Shared controls keep the same appearance, contrast rules, tap sizes and accessible action descriptions. Date format rows include an accessible current example and selected state. Layers use a calendar thumbnail, short Date subtitle, visibility switch, lock and options controls.

Drag, off-canvas placement, centred pinch sizing, precise size/position, ordering and horizontal centring reuse text-layer geometry. Locks protect placement while keeping appearance editable; they do not add a Locked suffix or change row height. The layer-row anchoring, picker recovery, retained panels and manual-scroll precedence from 0.2.36 apply to dates. Date and Clock separate photo blend groups; text itself is not photo-blended.

## Rendering, lifecycle and persistence

`WidgetTextLayers` adapts separate clock/date records to the same appearance, text drawing and geometry paths. Date stores only format and applicable appearance fields. The same native font factory and UIKit compositor draw preview/export; rendering caches include the actual formatted text, so the bitmap changes when the local date changes. Editor live-text ticks refresh both text and text-fit metrics. Date-only previews use the existing minute-aligned update cadence; seconds-enabled clocks keep their existing faster preview cadence. Coverage/backgrounding suspends the preview and resumes with current local time when the editor returns.

The published live scene retains raster photo groups but carries date and clock layers as native text, in stack order. Home Screen Date uses SwiftUI `Text(.currentDate, format:)` with an explicit `Date.VerbatimFormatStyle`. The provider's existing refresh request is additionally bounded by the next local calendar day. This uses calendar arithmetic rather than adding 24 hours, so DST transitions can have 23- or 25-hour days. The app does not request repeated WidgetCenter reloads to tick a date. WidgetKit still controls actual scheduling/display behaviour.

All text scenes require iOS 18 or later, consistent with the existing live clock requirement. iPhone Extra Large retains its iOS 27 requirement. The editor blocks creating live text layers on earlier iOS versions; existing unsupported live scenes show the established requirement message rather than a frozen date image.

Existing photo/clock catalogs and schema-1 scenes remain readable, and clock-only publications remain schema 1. Null date fields are omitted when writing existing layer/scene records. Visible Date scenes use schema 2; the Swift decoder supports both versions, enforces one payload per part, and validates formats, appearance, bounds and unique IDs. Hidden/zero-opacity text needs no live scene unless another visible text layer remains. Source photos stay private; publication shares only rendered photo groups and the bounded text manifest. Slots retain their existing saved design IDs through edits.

Once a catalog contains Date layers, older app versions cannot edit those new records. Their strict decoding must not reinterpret dates as photos or silently drop them. No automatic downgrade conversion is added.

## Verification

Managed tests cover all presets; local midnight, year and leap-day changes; all font/weight/alignment combinations; strict JSON and invalid mixed payload rejection; shared centred geometry and locking; mixed photo/date/clock ordering; old clock and new date fixtures; date-only and mixed storage/reopen/publication/slots; hidden date scene cleanup; shared toolbar editing and saved state; calendar semantics; picker cancellation/validation; retained rows/scroll offset; preview ticking and pinch position without photo reads. Existing clock, scroll, font and persistence regressions are retained.

Seven new native Swift tests cover the shared date fixture, formatting parity, optional weekday and both styles across all presets, legacy defaults, invalid weekday flags/styles, local midnight, DST day boundaries, schema/format/duplicate rejection and date-only scene loading. They are added to the existing Swift Package test target, whose source discovery already includes the shared scene file. Both native extensions and the standalone bridge already compile that shared file; no new Swift source-list dependency is required.

Automated verification passes: **192 Core tests, 81 Infrastructure tests, 346 App tests and 41 packaging checks** (660 total), source/project-generation checks and managed iOS C# compilation with zero warnings/errors. Weekday regressions cover all four presets in Off/Short/Full states, missing-field defaults, format changes, invalid settings, local-midnight pairing, saves/reopening/publication/slots, retained disabled style, both themes, preview text and scroll position. Font checks inspect actual bundled glyphs, including date letters and punctuation. Windows cannot run Swift/Xcode, UIKit or physical iPhone checks; the seven new native tests have not been run here. Required device checks: Add Date in all sizes, compare editor/export/Home Screen formats and fonts, toggle weekday visibility and Short/Full across presets, verify long weekday/month names fit, place photo/clock/date layers in different orders, use locks/hide/reorder and off-canvas pinch, switch between Clock and Date controls, cancel colour/photo/file pickers, background/resume, check large text and VoiceOver in both themes, and verify the Home Screen through local midnight/time-zone changes. Widget slot assignments and the saved static gallery preview should remain intact; the Home Screen text should stay live.

References: [Apple TimeDataSource](https://developer.apple.com/documentation/swiftui/timedatasource), [Apple date format styles](https://developer.apple.com/documentation/foundation/date/formatstyle), [Apple weekday representations](https://developer.apple.com/documentation/foundation/date/formatstyle/symbol/weekday), [Apple verbatim format strings](https://developer.apple.com/documentation/foundation/date/formatstring). The owner authorized committing, pushing main and pushing v0.2.37. App and extension versions are 0.2.37 (39); the owner will monitor the IPA build. Workflow triggers are unchanged.
