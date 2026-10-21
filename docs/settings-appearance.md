# Settings appearance

The owner requested the reference Settings layout on 20 October 2026, with a correction: **plain light / black backgrounds, no wallpaper**. The local implementation uses four rounded glass-style cards: Profile, Theme, Accent/Custom Colour, and Live Preview. It retains the native tab bar and spinning Settings gear.

## Behavior

- Profile defaults to **Faikal** with an initial avatar. Edit profile changes the local display name and imports a photo from **Photo Library** or **Files**. Both sources open a crop editor with a circular live preview, drag/pinch positioning, a zoom slider, Rotate, Reset, Cancel and Use photo. The selected crop also previews in Edit profile before Save profile. Adjust photo reopens the editor using a retained, bounded source image; older photos fall back to their existing cropped image. Cancel keeps the current photo; Save commits edits, while leaving without saving discards drafts. Images are limited to 10 MB on import, decoded with orientation correction to at most 2048 pixels on their longest edge, and cropped to a private 512 × 512 JPEG. The bounded source is saved privately for later recropping. Replacing/removing a photo deletes its crop and retained source. There is no account, upload or camera access. The gallery uses the system photo picker to select one image without requesting broad photo-library access.
- System / Light / Dark tiles update and persist the existing appearance preference. The background is solid `#F2F2F7` in Light mode and black in Dark mode; no wallpaper is used.
- Blue, Purple, Pink, Green and Orange presets set the app accent. Custom Colour enables the hue slider, hexadecimal entry and system colour picker. The rainbow swatch opens the system picker. Invalid hex text reports an error and leaves the previous accent intact.
- The selected accent updates primary buttons, navigation actions, selected tab, favorites switches and the native photo editor. Primary button backgrounds use the exact selected RGB; their text switches between black and white for readability. Text-only controls and navigation use a readable shade for the current Light/Dark appearance. User-defined action tile colors remain separate.
- The Live Preview includes a working sample button, switch, slider and accent card. Preview interactions do not change the counter or dashboard records.
- Widget signing/setup and privacy information remain accessible from **Widget & Shortcuts setup** below the cards.

The glass surface uses UIKit's `UIVisualEffectView` through a local MAUI content-view handler. Reduce Transparency uses an opaque system background instead. Theme icons and the picker symbol are project SVG assets, converted by MAUI; no icon font, raster wallpaper or new package is needed.

Appearance/profile preferences are private UserDefaults. Imported photos are private app files. Existing shared counter storage and App Group configuration are untouched. Accent edits preview immediately; drag gestures save the final choice rather than writing preferences on every frame.

The Photo Library and interactive crop refinement was implemented locally on 21 October 2026. It uses MAUI 10.0.110's single-image system picker and a UIKit scroll/zoom crop view. Local validation: **23 app appearance/crop tests passed**, including source-coordinate mapping for portrait/landscape images, zoomed crops, edge clamping and invalid dimensions; managed MAUI iOS compilation completed without reported warnings/errors, and source/configuration and whitespace checks passed. Native rendering, gallery/iCloud selection, gesture behavior, rotation, cancellation and final saved previews remain pending phone validation. The owner authorized committing and pushing these refinements on 21 October 2026; no new release tag is included in that instruction.

The owner subsequently reported that accent changes did not apply to buttons on other pages. The Shortcuts Run button was overriding its dynamic resource with a fixed white background and black text. That override is removed. Home, Run, Save action/profile and Live Preview use the shared primary-button resources; Manage uses accent text on its white surface. Navigation uses the page's public IconColor property for Add, Refresh, Cancel and Back actions; favorites switches, search Cancel and the native crop editor also use the accent. Theme changes refresh the text/navigation shades, including system appearance changes. The 23 app tests pass with contrast checks for presets and custom black/white/yellow/pastel colors in both appearances; managed iOS compilation and source/whitespace checks pass. Cross-page updates, relaunch persistence, navigation tint and final device rendering remain **NOT VERIFIED**. These changes are included with the photo refinement in the approved branch commit; a new IPA requires a separately approved version tag.

The owner's Light-mode screenshot also showed nearly invisible white card borders and a doubled, oversized hex field. The local refinement uses neutral outer/inner strokes (`#C8CEDA` / `#D2D7E1`) with separate Dark-mode strokes, clearer surface fills and a visible divider. The color editor is now one rounded row containing a flat 40-point swatch, HEX label, left-aligned monospaced value and a 44-point picker button. A dedicated entry handler removes only this field's native iOS border; other inputs retain their existing styling. Focus uses a readable accent outline; invalid RGB uses an error outline/message and preserves the previous accepted color. Autocorrection/spellcheck are disabled for this field. The 23 existing app tests, managed iOS compile and source/whitespace checks pass; no visual or keyboard behavior on a phone is claimed verified. This refinement is included in the same approved branch commit. Native CI and phone validation remain pending.

## Validation

Release **0.2.1 (4)** introduced the original Settings layout; the later refinements above require a subsequent tagged release. Core validation tests cover RGB normalization, malformed input, luminance calculations and bounded Unicode profile names. Full native linking is checked by the approved version-tag build; material rendering and device interactions still require phone validation.

Local validation on 20 October 2026: managed MAUI iOS code compiled with no reported compiler warnings/errors; MAUI Resizetizer generated the assets; **78 Windows tests passed** (40 Core, 17 Infrastructure, 21 Python); source/configuration checks and whitespace checks passed. The owner authorized committing, pushing and creating the new release tag on 20 October 2026. Phone checks remain pending below.

| Phone check | Result |
|---|---|
| Compare card spacing, corners, header, fonts and colors with the reference on iPhone 14 Pro Max | NOT VERIFIED |
| Light background is plain; Dark background is black; no wallpaper appears | NOT VERIFIED |
| System follows iOS appearance; Light and Dark override it and persist after relaunch | NOT VERIFIED |
| Presets, hue slider and native color picker update the preview, Home button and selected tab; hex value/selection stay in sync | NOT VERIFIED |
| Reject invalid RGB without crashing or replacing the last valid accent | NOT VERIFIED |
| Custom Colour switch disables/enables the custom editor; choices persist after relaunch | NOT VERIFIED |
| Profile Save, Cancel, replace/remove photo and relaunch behave correctly; cancelled drafts are not retained | NOT VERIFIED |
| Picker Done/swipe dismissal/Cancel paths return safely; importing an oversized or unsupported image shows a useful error | NOT VERIFIED |
| Photo Library and Files both open cropping; drag, pinch, slider, rotate/reset and Use photo match the circular profile preview | NOT VERIFIED |
| Crop Cancel preserves the previous photo; Adjust photo can reframe the retained source; profile Cancel discards both draft files | NOT VERIFIED |
| Large text, VoiceOver, keyboard safe areas and Reduce Transparency remain usable | NOT VERIFIED |
| Existing counter, widget refresh, Shortcuts action and spinning gear continue working | NOT VERIFIED |

## Launch crash correction, 21 October 2026

The owner reported that **0.2.1 (4) closes immediately on launch**. The release passed compilation and package validation, but those checks did not exercise appearance initialization. With the pinned Microsoft.Maui.Graphics 10.0.110, `Color.ToArgbHex()` returns `#007AFF` for the opaque default blue. The new UI incorrectly removed the first three characters as if the value were always `#AARRGGBB`; it passed `7AFF` to the six-digit luminance validator, which throws `ArgumentException`. This runs while constructing the Settings page (injected before the App constructor), and again when applying the startup button accent.

The fix for **0.2.2 (5)** centralizes RGB conversion in `AppearanceColors`, explicitly requests alpha before removing it, and uses the helper for startup contrast, Settings selection marks, hue-slider values and native picker results. It preserves RGB and discards only alpha. No broad catch hides the exception, and no counter, signing or App Group settings change.

A new app appearance test project runs the actual pinned MAUI Graphics implementation and the production helper on Windows/macOS. Tests reproduce the released exception and check default/preset/custom accents, white-button contrast, alpha handling and every integer hue position. Both CI workflows include these tests; IPA generation remains restricted to a newly pushed version tag. Full native compilation and phone launch of the fix remain **NOT VERIFIED**. The owner authorized committing/pushing the fix and tag `v0.2.2` on 21 October 2026 and explicitly chose to monitor the build personally.

Local validation on 21 October 2026: **92 tests passed** (14 app appearance, 40 Core, 17 Infrastructure, 21 Python); the locked app-test dependency restore, managed MAUI iOS compilation, source/configuration checks, workflow lint and whitespace checks passed.

The reference's device frame, status icons and portrait are not reproduced as app artwork. Exact material rendering varies by OS, accessibility settings and screen size.
