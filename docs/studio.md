# Studio

**Current local work:** Clipboard contains only Text / Photo / Link at the owner's request on 22 October 2026. See [Clipboard behavior, storage and checks](clipboard.md). The landing category pills remain decorative; the switcher is inside Clipboard. The records below describe the earlier prototype and released 0.2.4 baseline.

The owner requested replacing the Shortcuts dashboard with **Studio** on 21 October 2026. The landing page has only two entry cards, **Widgets** and **Clipboard**, on the established plain Light/Dark background. A four-panel layout icon replaces the layered Shortcuts tab icon. Home, Settings, the spinning gear and Apple's native Shortcuts counter action remain separate.

## Reference landing-page styling

The owner supplied a dark Studio reference after release 0.2.4 and requested UI styling only, with category functionality deferred. The local landing page now has the subtitle “Create, capture and customise your content,” charcoal gradient glass cards, inset icon tiles, circular chevrons and category pills. Widgets uses blue icons with Counters / Dates / Photos; Clipboard uses purple icons with Text / Links / Images. The Studio tab icon is a four-square outline matching the reference. Light mode has an equivalent neutral surface treatment; the existing native tab bar, selected accent and spinning Settings gear remain.

The owner subsequently requested a broader Widgets description that allows additional widget types and four planned sizes. The landing card now reads “Small, Medium, Large and Extra Large widgets.” This is prototype copy; the native extension and current preview library still support Small and Medium counter widgets only. No additional family support is implemented by this wording change.

Category pills are static visual previews and have no individual action handlers. Existing whole-card navigation to the Widgets/Clipboard libraries is retained. No date/photo/image/link backend, schema or permission change was included in that prototype. Pills wrap on narrow screens and with larger type. Managed iOS compilation, asset generation, source/whitespace checks and 30 existing app tests passed at that stage. Final phone rendering, spacing, touch behavior and accessibility remain **NOT VERIFIED**. The owner authorized committing and pushing this styling and description update on 21 October 2026. No new release tag was requested; native CI and phone checks remain pending.

## Widgets

Widgets opens previews of the existing **Small** and **Medium** counter widgets. The previews show the shared counter and its update time; opening or refreshing them does not increment the counter. The medium preview includes the native design's blue four-square mark. A private fallback counter is never displayed as the widget's shared counter. Missing shared storage and read errors produce an unavailable state and setup guidance.

These are approximate MAUI previews, not embedded WidgetKit views. Font metrics, material, colors, geometry and timing can differ from the actual Home Screen rendering. The current native widget configuration still supports only Small and Medium. Custom layouts, widget creation/publishing and additional families are not implemented by this refinement. The setup button opens the existing instructions for adding the native widget through iOS.

## Clipboard

Clipboard reads existing `CopyTextParameters` records from the same private SQLite repository. Add opens a focused name/text editor. Copy runs the existing validated clipboard executor after an explicit tap. The item menu offers Edit and confirmed Delete. Text is limited to 10,000 characters and names to 60; the existing validator rejects empty/invalid input. Text payloads retain their whitespace and Unicode exactly.

Existing clipboard IDs, categories, icons, colors, favorite flags and ordering metadata survive edits. The simplified library shows saved text in stored order. Former web-link and counter dashboard records remain stored and validated; Studio does not display, execute or delete them. There is no schema migration, silent data reset, clipboard polling or background clipboard history. Saved clipboard data is not moved into the shared widget container.

The old generic dashboard/editor, search, category and favorites filters are removed from this UI. The shared action contracts and support for reading older records remain intact.

## Validation

Local checks on 21 October 2026: **30 app tests passed** (including seven Studio filtering/editing/preview contract tests), **17 infrastructure tests passed**, managed iOS compilation completed without reported warnings/errors, SVG asset generation and source/configuration/whitespace checks passed. No new package, App Group, entitlement, shared schema or native extension source change is required. The owner authorized committing/pushing this refinement and tag **v0.2.4**, version **0.2.4 (7)**, and will monitor the tag build personally. Native CI, IPA output and phone checks remain unverified here.

Phone checks remain **NOT VERIFIED**:

- Studio tab label/icon, both entry cards, Back navigation, native tab bar and selected accent in Light/Dark mode.
- Small/Medium preview appearance, full-range counter values, last-updated formatting and unavailable shared-storage state.
- Previews refresh on entry/Refresh without changing the counter or requesting an increment.
- Previously saved clipboard items appear with exact text; other saved record types remain preserved.
- Add/edit validation, Save/Cancel, copying, confirmed deletion, pull-to-refresh and relaunch persistence.
- Keyboard layout, large text, VoiceOver, and returning quickly while an operation is pending.
