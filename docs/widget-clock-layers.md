# Clock layers — 0.2.31 (33)

The original clock-editor follow-ups are included in **0.2.32 (34)**; later preview, font and scrolling updates are labelled below through **0.2.35 (37)**. Individual implementation records retain their test counts and historical approval scope. The owner authorized the v0.2.35 release and will monitor the IPA build.

The owner approved adding Clock alongside Photo in the existing widget editor, then authorized committing and pushing main and the new v0.2.31 tag. App and extension versions are 0.2.31 (33). The owner will monitor the IPA build. IPA workflow triggers remain unchanged.

## Optional AM/PM suffix — included in 0.2.32

12-hour clocks now offer **Show AM/PM**, using the same aligned, accent-aware switch as Show seconds. Turning it off displays `3:45:12` instead of `3:45:12 PM` (or `3:45` with seconds off). The row is hidden in 24-hour mode; returning to 12-hour mode restores the per-layer choice. The control retains its VoiceOver label and follows the editor's existing busy/lifecycle guards.

The saved `showAmPm` Boolean defaults to true in both managed and native decoding when absent, preserving existing catalogs and scenes without a migration. Invalid types/null are rejected. Draft selection changes and save/reopen/publication retain the choice. Older strict-decoding app builds cannot read this new saved field, so downgrading newly saved designs remains unsupported.

Editor text, saved previews and native live text use the same explicit hour-cycle and suffix choices, without trailing spaces. Midnight/noon remain 12 in 12-hour mode. The 24-hour display ignores the suffix choice. No timer, photo import, source-list or workflow change is introduced.

Verification: **142 Core, 265 app, 66 Infrastructure and 34 packaging checks pass (507 total)**, along with source/project generation and managed iOS compilation (zero warnings/errors). Coverage includes old scenes missing the field, Boolean validation, midnight/noon/seconds formatting, 24-hour independence, switch visibility/mode restoration, saved settings and shared scene publication. Native Swift regressions cover the eight format combinations and missing/invalid field decoding, but have not run locally on Windows. Corrected Xcode compilation and actual iPhone preview/Home Screen/VoiceOver checks remain pending. The owner authorized committing and pushing this follow-up to main; no tag or version change is included.

## Compact clock layer rows — included in 0.2.32

The layer list now shows the clock name and **Clock**, without a 12-hour/24-hour suffix. This matches the compact photo-row metadata and gives the visibility switch, lock and menu more room beside it. Changing hour format no longer changes the row's detail width. All format/AM-PM controls remain in Clock settings; saved clock options, rendering, ordering, visibility and lock behaviour are unchanged. Existing accessible action/state descriptions and responsive wrapping for narrow screens or large text remain.

All **265 app tests** and managed iOS compilation pass with zero warnings/errors; whitespace checks pass. Actual iPhone row layout and VoiceOver checks remain pending. The owner authorized committing and pushing this follow-up to main; no tag or version change is included.

## Horizontal centring and stable clock pinch — included in 0.2.32

The selected clock's panel adds **Centre horizontally**, with an accent-aware vector icon and an accessible action description. It centres the layer frame left-to-right while retaining Y, width, height, opacity and clock settings. It does not create a persistent alignment lock: a later drag may move the clock sideways. Text alignment remains independent and is labelled **Text alignment** to distinguish it from placement. Locked clocks disable the action; busy, background, reordering and active-gesture guards prevent it from editing the draft. Pending text-colour HEX is validated/committed first. Placement is draft-only until Save widget and uses the existing frame fields without a schema change.

Clock pinches now resize proportionally around the frame's original centre, ignoring movement of the fingers' midpoint. One-finger dragging and corner resizing retain their existing behaviour. Centre-preserving scale limits are calculated before placement, including the outer workspace limits, so clamping a position afterwards cannot shift the centre. Clock format/font/colour options remain intact.

The old native pinch callback applied another scale/translation on `Ended`, using a fresh `LocationInView` without checking the number of touches. Apple's [gesture-location documentation](https://developer.apple.com/documentation/uikit/uigesturerecognizer/location(in:)) describes that location as usually the touch centroid. The final touch set therefore was an unsafe input to placement and is a likely explanation for the owner's reported jump; the exact device transition has not been captured locally.

Both photo and clock pinches now apply updates only for `Changed` with exactly two touches. `Ended` commits the last valid frame without reading a new touch centre. A changed touch count (including a third finger) freezes/commits that frame and releases ownership; late updates cannot mutate it. Photos retain two-finger translation and focal-point scaling while their touch set remains valid. Cancellation/failure, page disappearance, backgrounding and handler disconnection retain snapshot rollback and scroll-state restoration.

Local verification: **150 Core, 271 app, 66 Infrastructure and 34 packaging checks (521 total)**, source/project generation, whitespace checks and managed iOS compilation with zero warnings/errors. New regressions cover horizontal-only placement, unchanged draft/options, lock and invalid-colour guards, off-centre clock pinches, extreme workspace limits, last-valid photo/clock release frames, touch-count changes, cancellation and subsequent dragging. The tests exercise the same touch-input policy used by the native callback, but do not execute UIKit recognition, touch delivery or scroll restoration. Xcode/AOT compilation and physical iPhone pinch-release, centring, both themes, VoiceOver and background/scroll checks remain pending. The owner authorized committing and pushing this follow-up to main; no tag or version change is included.

## Clock toolbar and pinned preview — included in 0.2.32

Clock layers now share the photo editor's single scrolling icon toolbar and 48-point native targets. The seven actions are **Time**, **Font**, **Colour**, **Size**, **Position**, **Opacity** and red **Delete**. Time contains 12/24-hour format, seconds and optional AM/PM; Size adjusts text size within the frame; Position offers horizontal centring and text alignment. Frame/crop precision remains available through the layer menu. Delete uses the existing confirmation flow, including for locked layers.

Tapping a tool opens one settings panel beneath the preview; tapping the active tool closes it. Edits keep their panel open. Changing/deselecting layers resets the active panel. Expanded icons follow the accent and expose their expanded state to VoiceOver; panels have accessible headings. Pending HEX is validated before changing/closing tools, selecting another layer or saving. Invalid HEX keeps/reopens Colour so its error is visible. A system colour picker temporarily disables editing and keeps Colour selected when dismissed, including on cancellation/failure. Ordinary backgrounding closes panels; clock preview lifecycle and draft/save semantics remain unchanged.

The editor uses one permanently mounted preview above a separate vertical settings scroll area for both photo and clock tools. The toolbar remains directly beneath that preview, and selecting a tool reveals its controls at the top of the lower area. Name/size, layers, background and Save remain in that same lower scroll area. The fixed toolbar slot prevents selection/tool changes from resizing the canvas. There is one compositor and no duplicated preview or canvas reparenting.

The preview fits the available display height as well as width while preserving saved widget proportions for Small, Medium, Large and iPhone XL. Outside workspace padding reduces on compact/keyboard-height layouts. Preview scaling does not alter saved dimensions, layer geometry or export quality. Photo tools retain their existing controls and now benefit from the same pinned preview. Canvas operation guards are explicit because it no longer inherits disabled state from the settings form; an active gesture remains enabled until it finishes, while imports, saves, colour pickers and layer reordering block new canvas input.

Verification: **282 app, 150 Core, 66 Infrastructure and 34 packaging checks (532 total)**, managed iOS compilation with zero warnings/errors, source/project generation and whitespace checks. Added/updated coverage includes panel exclusivity/toggling, stable canvas parent and single compositor, option persistence, native-picker cancellation/deactivation recovery, invalid-HEX routing, disabled canvas/tool actions, locked/delete semantics, 48-point targets, preview proportions at five viewport heights for all four sizes and compact-screen space for settings. These are managed/layout-policy checks; physical iPhone scrolling, keyboard/large-text fit, toolbar discoverability, VoiceOver, colours, touch gestures, deletion confirmation and final native layout remain pending, along with Xcode/AOT verification. The owner authorized committing and pushing this follow-up to main; no tag or version change is included.

## Add/Edit live preview scale correction — included in 0.2.33

The height-limited preview introduced in 0.2.32 reduced Large and XL canvases to fit the pinned area, making the live preview smaller than the previous editor. Add/Edit now sizes the canvas from the available width and preserves its full proportional height, including saved device measurements. The complete page scrolls vertically; the outside workspace still scrolls horizontally. Name/size precedes the preview, the existing icon toolbar stays immediately below it, and tool panels follow the preview. Opening a panel reveals those controls rather than jumping to the page top. There is still one compositor with a stable parent, and the fixed toolbar slot keeps selection changes from resizing the canvas.

The canvas and its ancestors stay enabled during pinch/drag; other actions remain guarded during the gesture. Native scroll arbitration continues to suspend enclosing scrolling during manipulation and restore it when the gesture finishes or cancels. Picker cancellation/recovery, background lifecycle, colour validation and save semantics remain covered by the existing editor tests. Gallery, Widget slots, Assign design, saved proportions and export rendering are unchanged.

Automated checks cover all four sizes with default and saved dimensions over five viewport heights, full-height scrolling structure, toolbar/panel stability and enabled gesture ancestors. These checks do not establish actual UIKit measurements or device touch behaviour. Physical iPhone checks remain required for preview size, vertical/horizontal scrolling, pinch/drag, keyboard/large text, VoiceOver and picker/background recovery. The owner authorized committing, pushing main and pushing the new v0.2.33 tag. App and extension versions are 0.2.33 (35); the owner will monitor the IPA build. Workflow triggers remain unchanged.

Local verification passed: **292 app tests**, managed iOS C# compilation with **zero warnings/errors**, source/project-generation validation and whitespace checks. Xcode/AOT compilation and physical iPhone rendering have not run on this Windows machine.

## Font specimens and Japanese display fonts — included in 0.2.34

The initial Font panel added seven choices: System, Rounded, Mono, Serif, RocknRoll One, Dela Gothic One and Kaisei Decol. The SF Pro Rounded follow-up below adds an eighth and labels the existing Rounded choice **Arial Rounded**. Each row renders its name and `12:34:56` in its own face. The vertical list avoids squeezing long names or wide clock samples into small segments. Rows have at least 48-point native targets, scalable names, fixed-size specimen digits and a selected VoiceOver description. The selected row uses the app accent with contrast-aware text in Light and Dark appearance. Selecting a font keeps the Font panel open and immediately updates the draft; existing colour, locking, visibility, alignment, placement, format and save/cancel behaviour are preserved.

The three new unmodified regular fonts and their original SIL Open Font Licenses are bundled in both the host and WidgetKit extension, registered through `UIAppFonts`. They work offline and require no photo access or runtime downloads. [The asset record](../assets/clock-fonts/README.md) includes pinned upstream revision, PostScript names and SHA-256 digests. Full fonts total 9,694,824 bytes in each bundle before package compression. App labels and UIKit composition/export share one managed font catalog; Swift scene validation and rendering use the matching names. UIKit reports an unavailable face rather than silently saving a different one. SwiftUI uses fixed point sizes for custom faces, retaining their own digit spacing; the System clock retains monospaced digits. The new faces have line metrics of 1.448 times the point size, so both native drawing paths also fit the font line to the layer height to avoid clipping. No saved canvas or layer geometry is changed.

Existing font names remain stable, the default stays Rounded, and older saved designs need no migration. New names persist through catalog/scene save, reopening, slot assignment and publication. Downgrading an edited catalog to a build that does not recognize the new font names is unsupported, consistent with the existing strict clock schema.

Packaging validation now rejects missing font registration, missing/changed font bytes or missing licence files in either rendering bundle. Source tests inspect the actual TTF PostScript names and glyph maps, including all clock digits, colon, AM/PM and specimen names, and check both resource graphs. The native test suite adds scene-name acceptance and CoreText registration checks; those two tests have not run on Windows.

Local verification passed: **297 App + 153 Core + 69 Infrastructure + 39 packaging = 558 checks**, source/project generation and managed iOS compilation with zero warnings/errors. A Windows font specimen image was also inspected for the three actual TTFs in light/dark colours; this does not establish native MAUI or WidgetKit layout. Pending device checks: compare all seven fonts in the picker, live editor, saved preview and Home Screen; check narrow screens, large accessibility text, VoiceOver, 12/24-hour formats, seconds/AM-PM toggles, save/reopen, background/picker recovery and several mixed-font clocks for memory/rendering performance. Xcode/Swift/AOT and final iPhone rendering remain unverified. The owner authorized committing, pushing main and pushing the new v0.2.34 tag. App and extension versions are 0.2.34 (36); the owner will monitor the IPA build.

Implementation references: [Apple custom-font registration and use](https://developer.apple.com/documentation/swiftui/applying-custom-fonts-to-text), [UIAppFonts](https://developer.apple.com/documentation/bundleresources/information-property-list/uiappfonts) and [MAUI font consumption](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0).

## SF Pro Rounded and per-clock weights — included in 0.2.34

SF Pro Rounded is a separate eighth choice. It uses Apple's public rounded system-font descriptor in UIKit and the rounded system design in SwiftUI; no Apple font files are downloaded or redistributed. The existing `rounded` saved value continues to select Arial Rounded Bold. System and SF Pro Rounded offer **Light, Regular, Medium, Semibold, Bold and Heavy**. Mono and Serif offer Regular/Bold through their actual Menlo/Georgia faces. Arial Rounded remains Bold and the three bundled Japanese faces remain Regular; their fixed weight is explained instead of showing unavailable choices.

Weight controls appear directly beneath the selected font's specimen, inside the Font panel. The horizontally scrolling choices show their own face/thickness, keep 48-point targets and grow from a 100-point minimum width for larger accessibility text. The selected weight follows the accent, retains readable foreground contrast and has a selected VoiceOver description. Selecting a weight keeps Font open, updates the selected font-name/digit specimens and live composition, and does not change layer placement. Locked clocks retain editable appearance controls. Stale controls from another font, closed panels, native pickers, active canvas gestures and inactive/departed pages retain their editing guards.

A dedicated clock-font label handler supplies exact system designs and all six weights for specimens and outside-canvas clock text; ordinary app labels use their existing handler. Named faces retain MAUI's normal font path. The same UIKit font factory draws live compositions and saved snapshots. WidgetKit validates the same weight matrix and uses matching SwiftUI weights, rounded design, named Regular/Bold faces, fixed sizing, height fitting and system monospaced digits.

The optional nullable `weight` field is stored per clock in catalogs and scene files. Missing/null preserves the previous face: System defaults to Semibold; Arial Rounded/Mono/Serif default to Bold; the three Japanese fonts default to Regular. SF Pro Rounded defaults to Semibold. Font switching keeps an explicitly chosen compatible weight; an unsupported weight resets to the destination font's default. Unsupported combinations and invalid enum strings/numbers/types are rejected. Save, reopening, slot assignment and publication retain the weight. Existing libraries need no migration; downgrading libraries written with the new field remains unsupported by older strict readers.

Review covered real-weight availability, all font/weight round trips, legacy defaults, incompatible switches, locks/placement, unsaved drafts, closed/stale control callbacks, background recovery, accessibility targets and accent contrast in Light/Dark. Local verification passed: **306 App + 169 Core + 78 Infrastructure + 39 packaging = 592 checks**, source/project generation and managed iOS compilation with zero warnings/errors. Two additional Swift tests cover default/explicit weights, Regular face names and invalid/unavailable values, but have not run on Windows. Xcode/AOT and physical iPhone checks remain required for exact glyphs/weights across picker/editor/saved preview/Home Screen, horizontal scrolling, large text/VoiceOver, seconds/AM-PM, mixed clocks and lifecycle performance. The owner authorized committing, pushing main and pushing the new v0.2.34 tag. App and extension versions are 0.2.34 (36); the owner will monitor the IPA build. Workflow triggers remain unchanged.

References: [Apple rounded font descriptors](https://developer.apple.com/documentation/uikit/uifontdescriptor/withdesign%28_%3A%29), [SwiftUI system font weights/designs](https://developer.apple.com/documentation/swiftui/font/system%28size%3Aweight%3Adesign%3A%29-697b2) and [.NET iOS rounded descriptor binding](https://learn.microsoft.com/en-us/dotnet/api/uikit.uifontdescriptor.createwithdesign).

## Settings scroll stability — included in 0.2.35

Photo/clock settings, font weights and blend controls no longer request automatic scrolling. Panel transitions protect the current viewport against content shrinking, while keeping manual scrolling and full-size preview geometry. See [root cause, behaviour and verification](widget-editor-scroll.md).

## Editor behaviour

- **Add layer** offers **Photo** and **Clock**. Photo retains Photo Library and Files selection. Cancel returns to the usable editor without changing the design.
- Clock creates a selected digital clock using the current app accent. It needs no photo permission or imported asset. The combined limit is eight photo/clock layers.
- Clock controls offer 12/24-hour format, seconds, the eight fonts and supported weights listed above, text size, Left/Centre/Right alignment, opacity and the existing reusable colour picker, including HEX entry.
- Time follows the iPhone's timezone. The chosen hour cycle is explicit; digits are Latin and the 12-hour suffix uses English AM/PM. No fonts or other clock assets are downloaded.
- Clock frames use the same move, pinch/corner resize, off-canvas clipping, precision position/size, selection, lock/unlock, visibility and animated ordering as photo layers. Locked placement remains protected while clock appearance can be edited. Blank-space deselection closes the selected clock's controls.
- Photo crop, flip, rotation and blend controls apply to photos. Clock opacity is in the Clock panel, and deletion is available from its layer menu.
- Small, Medium, Large and iPhone XL use the existing saved canvas proportions and matching static widget slots. Clock creation requires iOS 18 or later; XL still requires iOS 27. Photo-only designs remain usable on iOS 17.

## Live rendering and saved previews

The editor draws photos and formatted clock text through the existing Core Image compositor. A timer redraws only while the live composition is loaded and active: once per second when a visible clock shows seconds, otherwise at minute boundaries. It reuses decoded photos and cached composition inputs. Leaving the editor, covering it with a picker, deactivating the app, clearing the design or disconnecting the view stops the timer and releases native drawing resources. Activation refreshes the timezone and current time.

The Home Screen uses SwiftUI's public `Text(.currentDate, format:)`/`TimeDataSource` path with `Date.VerbatimFormatStyle`. It does not request a WidgetKit timeline reload each second. iOS controls live-text rendering and may simplify or suppress seconds while the display is dimmed; continuous one-second delivery in every device state is not promised.

My Widgets and Widget slots retain full saved PNG snapshots and explicitly label clock designs as saved previews. Opening the editor resumes live time. A live Home Screen design requires a valid live scene rather than silently showing a frozen snapshot if scene loading fails.

References: [Apple TimeDataSource](https://developer.apple.com/documentation/swiftui/timedatasource), [live Text initializer](https://developer.apple.com/documentation/swiftui/text/init%28_%3Aformat%3A%29-8sfgg), [dynamic widget dates](https://developer.apple.com/documentation/widgetkit/displaying-dynamic-dates) and [verbatim date formatting](https://developer.apple.com/documentation/foundation/date/verbatimformatstyle).

## Photo blends and stacking

Consecutive photos retain all existing 14 blend modes. Each clock separates those photos into groups, allowing live text between photo artwork without flattening the time into a PNG. Photos within each group blend together; clock text uses normal alpha compositing and does not participate in photo blend modes. Hidden or zero-opacity clocks retain group boundaries, so hiding a clock does not also change neighbouring photo colours. Photo-only designs retain their existing rendering path.

## Persistence, publication and bounds

Clock options are stored in the existing design catalog. Existing photo records without clock fields retain their defaults; IDs, slots and source photos remain intact. Older app builds that cannot decode clock fields are not supported as a downgrade target.

Saving produces an immutable full snapshot, thumbnail, scene JSON and the required photo-plane PNGs. These files are written before the catalog references the new render key. Invalid or failed saves leave the old catalog generation intact and remove incomplete new files. Clock-only designs require no photo file. Hidden-only clocks need no live scene.

Shared publication validates the scene against the saved design and copies photo planes and scene before publishing the catalog. Original photo assets remain private. Cached shared generations retain the existing seven-day cleanup grace period. Unreferenced private scene/plane files are collected with other render artifacts.

Scene manifests are limited to 32 KB and eight parts. Photo planes are limited to 1,024 pixels on either edge, three million decoded pixels and four MB of encoded data in aggregate. Export reduces plane resolution when necessary to meet the live-scene budget; the saved gallery snapshot keeps its existing quality. Native reading independently validates geometry, part addresses, colours, fonts and image bounds before decoding.

## Review and verification

The review covered mixed ordering and lock semantics, old photo compatibility, all sizes, invalid clock options, colour-picker cancellation/failure, pending HEX validation, picker results delivered while the app is inactive, shared layer limits, scene consistency, atomic publication, cleanup and memory bounds. The clock panel uses the app's shared accent-aware controls, switch alignment, colour picker and accessible labels. Native text remains available to VoiceOver.

Local verification passed:

- 264 app tests, 136 Core tests, 66 Infrastructure tests and 32 packaging checks: **498 total**.
- Managed iOS C# compilation with zero warnings/errors.
- Source policy checks, generated native project validation and whitespace checks.

Three native Swift tests were added for the shared scene fixture, explicit time formatting, bounded scene loading and invalid-data rejection. They have **not run on this Windows machine**. Swift compilation, Xcode linking/AOT and WidgetKit rendering remain pending.

### Native bridge build correction — 25 October 2026, included in 0.2.31

[Test run 40108539648](https://github.com/Faikalizham99/Control-Studio/actions/runs/40108539648) passed the managed job and native Swift storage tests, then failed in the standalone bridge compilation before Xcode built the extensions. The compiler reported `cannot find type 'WidgetLayerMarker' in scope` in `WidgetDesignStore.swift`; its Decodable/Equatable failures were consequences of that missing type.

The clock source had been included in both generated Xcode targets and discovered automatically by the Swift Package tests, but omitted from the explicit `swiftc` input list in `scripts/build-native.sh`. That bridge list now includes `native/Shared/WidgetClockScene.swift`. Both regular native CI and tag builds use this same script.

Two packaging regressions verify that the clock/store dependency pair is included exactly once in both extension targets and that the standalone bridge input set matches every shared Swift source plus its bridge entry point. This catches future shared-source omissions independently of Swift Package test success. All **34 packaging checks** pass locally. Native compilation of the corrected script still requires the pinned macOS/Xcode environment; no successful corrected CI build or iPhone verification is claimed. The owner authorized committing and pushing main, including the previously local lock-row fix, then authorized the v0.2.31 release tag. Workflow triggers remain unchanged.

Required native/device checks:

- Build the shared Swift tests and both native extensions with the pinned Xcode toolchain.
- Add Photo and Clock, cancel each picker, change colours/HEX and return from the background; verify controls recover and settings persist after reopening.
- Compare 12/24-hour clocks with seconds on/off, midnight/noon and a timezone change; confirm explicit format survives the device's opposite hour-cycle setting.
- Place multiple clocks between photos; hide, lock, drag, pinch, reorder and save/reopen. Verify all four sizes, off-canvas clipping, photo blend grouping and transparent/solid backgrounds.
- Assign designs to slots and confirm Home Screen time advances without opening the app. Check dimmed display/Low Power behaviour, accessibility, Light/Dark appearance, text alignment and built-in font matching.
- Exercise eight mixed layers, large images and repeated editor/background transitions while checking extension memory and smoothness. Local byte/pixel bounds do not establish native archive or runtime memory use.
