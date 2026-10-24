# Clock layers — unreleased

The owner approved adding Clock alongside Photo in the existing widget editor, then authorized committing and pushing main. This change has no release-version bump or tag push. IPA workflow triggers remain unchanged.

## Editor behaviour

- **Add layer** offers **Photo** and **Clock**. Photo retains Photo Library and Files selection. Cancel returns to the usable editor without changing the design.
- Clock creates a selected digital clock using the current app accent. It needs no photo permission or imported asset. The combined limit is eight photo/clock layers.
- Clock controls offer 12/24-hour format, seconds, System/Rounded/Mono/Serif fonts, text size, Left/Centre/Right alignment, opacity and the existing reusable colour picker, including HEX entry.
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

Required native/device checks:

- Build the shared Swift tests and both native extensions with the pinned Xcode toolchain.
- Add Photo and Clock, cancel each picker, change colours/HEX and return from the background; verify controls recover and settings persist after reopening.
- Compare 12/24-hour clocks with seconds on/off, midnight/noon and a timezone change; confirm explicit format survives the device's opposite hour-cycle setting.
- Place multiple clocks between photos; hide, lock, drag, pinch, reorder and save/reopen. Verify all four sizes, off-canvas clipping, photo blend grouping and transparent/solid backgrounds.
- Assign designs to slots and confirm Home Screen time advances without opening the app. Check dimmed display/Low Power behaviour, accessibility, Light/Dark appearance, text alignment and built-in font matching.
- Exercise eight mixed layers, large images and repeated editor/background transitions while checking extension memory and smoothness. Local byte/pixel bounds do not establish native archive or runtime memory use.
