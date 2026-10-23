# Native Studio widget transparency — 0.2.23 (25)

The owner authorized implementation after the iPhone test showed that changing the Home Screen style to Clear made the dark widget container see-through. The exported photo already retained alpha; the remaining background came from the native widget container. This follow-up requests clear containers in normal Home Screen styles. It has not been verified on an iPhone. The owner subsequently authorized committing, pushing main and pushing the new version tag. The owner will monitor the tag-triggered IPA build.

## Research and scope

The public [ClearAndBlurredWidgets sample](https://github.com/pookjw/ClearAndBlurredWidgets/blob/main/MyWidget/FooObject.mm) intercepts WidgetKit's descriptor callback and sets `backgroundRemovable`, `transparent` and `preferredBackgroundStyle` (1 for clear) on mutable widget descriptors. These are private runtime APIs, not the public SwiftUI `containerBackground` API. The sample's [iOS 27 safety proposal](https://github.com/pookjw/ClearAndBlurredWidgets/pull/6) reports a physical-device crash while reconstructing the private Swift result and proposes defensive changes. It is not proof that every iOS 27 device supports this technique.

Widgy's [developer announcement](https://www.reddit.com/r/widgy/comments/1w5rb1m/announcing_widgy_v27_public_beta_next_week/) and WidgetLoft's [creator guide](https://mucahitk.com/blog/transparent-widget-iphone) describe screenshot-free transparent widgets. Their exact proprietary implementations were not inspected. The adapter follows the independently published descriptor mechanism; it does not claim access to either app's source.

The adapter runs only on iOS major version 27 and only changes the ten exact `ControlStudio.Slot.1` through `ControlStudio.Slot.10` kinds. Counter descriptors, controls, Live Activities and unrelated kinds retain their encoded values. All four Studio sizes use the same slot descriptors. Descriptor policy is per kind, so all ten slots request clear containers; solid designs retain their background inside their saved PNG. Empty/error views explicitly paint their own readable system background. No wallpaper screenshot, new setting, blurred-background option or data migration is introduced. Existing alpha exports, slot assignments and persistence remain unchanged.

## Defensive implementation

`native/WidgetRuntime/CSWidgetBackgroundAdapter.m` is compiled only into the widget extension, with ARC and a widget-only Swift bridging header. WidgetBundle initialization attempts installation and queues one main-thread retry. Unsupported OS versions, missing classes, unexpected method signatures and unavailable methods leave normal rendering in use.

The callback archives the original secure-coded result and decodes its original class. A public `NSKeyedUnarchiver` delegate substitutes independent mutable copies of matching descriptors. This preserves fields handled by the original class's coder, including future encoded metadata, instead of manually rebuilding a private Swift result with a selected set of fields. Each request has its own decoder/delegate state. The adapter checks descriptor method signatures before dispatch, rejects mutable copies that return the original object, bounds accepted archives to 8 MiB, and returns the original result if any target cannot be safely patched. No App Group reads or file writes occur in this hook. Logs contain only fixed status strings and a count.

This guards detected incompatibilities; it cannot guarantee that undocumented framework behavior remains compatible. The delegate substitution approach itself still requires native verification. Future OS major versions intentionally use normal rendering until reviewed. iOS may retain system chrome or alter rendering in Clear/Tinted styles even when the descriptor request succeeds; a fully invisible outline is not established by the sample or local checks.

## Review and verification

Local Windows verification: 32 packaging/build-graph tests pass, as do the source/policy checks, exact generated-project comparison and whitespace check. The three new graph tests verify that the adapter is compiled once, belongs only to the widget target, has the correct bridge, and inherits ARC/extension-safe build settings. Workflow triggers are unchanged. Host and native version/build numbers are synchronized at 0.2.23 (25).

Nine native Foundation tests were added for all ten kinds, original-object preservation, unchanged Counter and future metadata, unavailable runtime classes/setters, incompatible setter ABI, copy-returning-self, decode failure, concurrent requests and harmless macOS installation. They have **not run locally**: Windows has no Apple Objective-C/Swift compiler or Xcode. The existing macOS `swift test --package-path native` command includes the new tests; the iOS archive build also compiles the actual hook, which is inactive on the macOS test host.

Still required on macOS/iPhone:

- Run native tests and compile/link the widget extension before installing a tagged build.
- Add slots in Small, Medium, Large and iPhone XL; confirm gallery registration and no crashes/placeholders.
- Compare transparent cutouts against the actual wallpaper in normal, Clear and Tinted Home Screen styles, including edges and any remaining system outline.
- Mix transparent and solid designs across slots; reassign/save/reload, restart the app/device and verify persistent assignments and images.
- Verify readable empty/error slots in Light/Dark and unchanged Counter behavior, accent and theme.
- Check accessibility text/contrast for empty/error content, gallery rendering, and missing-API fallback with normal widget rendering.

No visual layout, interaction, gesture, focus, motion or saved-design schema changes are made. Device compositor behavior and signed-IPA runtime compatibility remain open verification items.
