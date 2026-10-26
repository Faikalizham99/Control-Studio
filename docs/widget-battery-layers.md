# Battery layers

Included in **0.2.41 (43)**. The owner authorized pushing the previously committed Battery layer work with the rendering fix and new version tag, and will monitor the IPA build.

Add layer → Battery creates an independent layer in any supported iPhone widget size. It shares the editor's drag, centred pinch, corner resize, free rotation, opacity, visibility, locking, reordering, duplication and precise position controls. Battery layers count towards the existing 30-layer limit. They do not require a photo or a permission prompt.

The Shape panel offers a classic battery, horizontal bar, ring, semicircular arc and percentage-only display. Percentage and charging status are optional; percentage-only always displays the number. Charging uses a bolt; fully charged uses a check. Unknown readings show a neutral track and em dash, including when percentage labels are disabled. Unknown is never rendered as zero or charging.

Placement update in **0.2.42 (44)**: percentages sit inside the classic battery body, bar and ring, and in the arc's open centre. Percentage-only stays a standalone number. The bar is slightly taller to fit the label. Embedded numbers choose black or white against the selected fill and have a fine opposite-colour outline for legibility over empty track, transparent areas and photos. This changes drawing only; saved layer frames, colours, thresholds, switches and data formats are preserved. Existing library/slot PNG previews reflect their last save; opening and saving a design regenerates those images with the new placement. The live editor and native Home Screen scene use the new placement directly.

The Colour panel uses the same reusable colour picker as Settings and the text tools. Select a percentage stop to edit its threshold and colour. Ranges apply each stop until the next one: the defaults are 0–20% red, 21–50% orange, 51–80% blue and 81–100% green. Smooth interpolates RGB between stops as the battery percentage changes; it holds the final colour beyond the last stop. This is a colour change with battery level, rather than a spatial gradient across the shape.

There can be 2–8 stops. The first stays at 0%. Remaining thresholds are whole percentages, strictly increasing and at most 100. Add inserts a stop into the largest available gap. Remove keeps the first stop and at least two stops. Pending threshold and HEX inputs are validated before switching stops, tools, layers, widget size or saving. Invalid input stays editable and blocks those transitions. Changing shapes or colour modes retains the other settings.

## Readings and refresh

The active editor observes battery level/state notifications. It releases observation on suspension, navigation departure or handler disconnection, restoring the previous monitoring setting when its last observer leaves. Saving captures a reading on the main thread before background image composition. Device readings are not stored as layer configuration.

The Home Screen uses a live scene, rather than freezing the battery into the saved PNG. The extension enables UIDevice battery monitoring briefly and samples once per snapshot/timeline request on the main actor, then restores its previous state. The timeline requests another refresh after 15 minutes. iOS determines actual delivery; this is not continuous monitoring or a guarantee of an update on every percentage change. The library, slot and assignment pages show labelled saved previews. Reopen the editor or refresh/save to obtain a new in-app preview.

Apple references: [batteryLevel](https://developer.apple.com/documentation/uikit/uidevice/batterylevel), [battery monitoring](https://developer.apple.com/documentation/uikit/uidevice/isbatterymonitoringenabled), [WidgetKit refresh scheduling](https://developer.apple.com/documentation/widgetkit/keeping-a-widget-up-to-date/).

## Compatibility and review

Visible battery layers export scene schema 3; Clock-only remains schema 1 and Date remains schema 2. Static Photo/Text groups keep their ordering around all live layers. The catalog stays schema 1 with an optional battery payload; older layers omit it. The native decoder rejects malformed shapes, modes, thresholds, colours, geometry, duplicate live IDs and mixed part payloads. Existing image, scene, pixel and layer budgets remain in force. Colour-stop equality is structural so separately decoded catalogs and scenes validate consistently.

Automated coverage checks range boundaries, smooth interpolation, unknown readings, JSON compatibility/equality, independent duplication, vector resizing/locks, scene ordering/budgets, repository reopen/publication/slot persistence and atomic rejection. Headless app tests cover both themes, control state, cancelled colour pickers, lifecycle recovery, pending-input validation, placement, opacity and saving. Drawing tests inspect the shared editor/export artwork's geometry and switches. A shared schema-3 fixture and Swift tests cover the native decoder and colour calculations.

Windows verification cannot execute Swift/Xcode or inspect UIKit/SwiftUI pixels. Device checks remain necessary: shape parity between editor and Home Screen; charging/full/unplugged/unknown states; actual WidgetKit refresh timing; picker dismissal; VoiceOver; large text; gestures and scroll retention. Battery shape previews deliberately use an illustrative 75% reading; the live canvas uses this iPhone's reading.

The placement update has a shared label-layout/contrast fixture consumed by managed drawing and Swift contract tests. Tests cover 0%, 1%, 50%, 99%, 100%, unknown and hidden percentages; exact text boxes; black/white ink contrast; percentage-only colour retention; and the unchanged charging switches. Source checks connect both native and managed drawings to those layouts. The Swift test, actual text fit/outline appearance, photo backgrounds, light/dark mode, all sizes, opacity and rotated layers still require Xcode/iPhone confirmation.
