# Duplicate layer — 0.2.38 (40)

The three-dot menu offers **Duplicate layer** for Photo, Clock and Date, including locked or hidden layers. It targets the row whose menu was opened, even when a different layer was selected.

A duplicate keeps every setting except its identity and display name: content/source image, kind, frame/position/size, crop/zoom/fit, rotation/flips, blend mode, opacity, visibility, lock state and all clock/date text options. Date weekday visibility/style and font weights are retained. The copy receives a new GUID and a unique name such as `Photo 1 copy` or `Photo 1 copy 2`. Names stay within the existing 40-UTF-16-unit limit and truncate at Unicode text-element boundaries; collisions are checked without case sensitivity. Existing records and the original layer are unchanged.

The copy is inserted immediately above its source in both drawing order and the reversed Layers list. It keeps the exact position, so an opaque visible copy can look identical until moved. A hidden copy stays hidden, and a locked copy must be unlocked before moving/resizing it. The copy becomes selected; tools/precision close as with a new selection. A status/VoiceOver announcement identifies the created copy. The design remains a draft until Save widget is chosen.

The eight-layer limit is unchanged. At eight layers the native menu omits Duplicate layer, and the operation independently checks the limit before mutation to reject a late callback safely. Failed duplication does not alter selection, layer order, originals or saved data. Existing busy, picker, foreground and departure guards cover the menu action.

## Shared sources, persistence and lifecycle

Duplication uses immutable record copying rather than manually rebuilding selected fields. The returned design has a new layer array; editing the duplicate replaces only its own ID's record. Clock/date scene entries receive separate IDs and remain live. This adds no new persisted fields or native widget configuration.

Photo copies reuse the same private asset key without importing or writing another original image. Existing reference checks retain that image while any remaining layer/design needs it. Removing one copy must not remove the other copy's image; removing the last saved reference permits cleanup. Newly picked photos retain their existing draft-ownership tracking, so cancelling the editor still cleans abandoned imports once, while saved references remain protected. Publication continues to share render/scene artifacts, not private source photos.

The scroll guard can hand its captured source-row position to the newly inserted selected copy. It waits for arranged geometry and uses the existing bounded position compensation; it adds no MakeVisible request, timer or offset-restoration loop. Manual scrolling, coverage, resizing, handler removal and departure still cancel pending corrections. Offscreen sources do not trigger a forced reveal.

## Review and verification

Review covered all layer kinds/settings, immutable edit isolation, source versus selected-row targeting, selection/status accessibility, locked/hidden behaviour, unique long/Unicode names, list/drawing order, maximum-count guards, stable existing row instances, viewport handoff, stale queued callbacks, original image reuse, last-reference cleanup, slot persistence and distinct live-scene IDs. Existing cancellation/busy/foreground guards remain in use. Versions and workflow triggers are unchanged.

Automated results: **199 Core tests, 84 Infrastructure tests, 353 App tests and 41 packaging checks** (677 total) pass. Source/project-generation and whitespace checks pass. Managed iOS C# compilation passes with zero warnings/errors.

New tests verify complete copied settings for Photo/Clock/Date, independent edits, ordering and names; repeated copies/limits; Unicode boundaries and name collisions; all three menu paths and selected/unselected targets; late callbacks at the limit; selected row/scroll retention; anchor handoff and manual-scroll cancellation; shared photo save/reopen/publication/cleanup; and distinct published clock/date IDs with independent options. Native Swift code and data schemas are unchanged by this enhancement.

These tests use managed controls and simulated geometry, plus actual filesystem repositories; they do not execute UIKit touch/layout or Home Screen rendering. On an iPhone, duplicate each kind from its menu, confirm selection/placement and no unexpected jump, duplicate from open colour/precision panels, test long names and large text in both themes, unlock/show inherited states, edit only the copy, save/reopen, confirm Home Screen updates, delete either photo copy, and test cancellation/backgrounding plus the seven-to-eight-layer boundary. The owner authorized committing, pushing main and pushing v0.2.38. App and extension versions are 0.2.38 (40); the owner will monitor the IPA build. Workflow triggers are unchanged.
