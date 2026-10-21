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

### Reference profile layout refinement, 21 October 2026

The owner requested the supplied Edit profile layout with the established plain Light/black background. The local implementation adds a frosted header with circular Back and capsule Cancel controls; a photo card with a large circular avatar/halo, picture-icon Choose photo and trash-icon Remove photo buttons; an inset display-name field with a clear control and information row; and a separate rounded Save profile button with a check icon. The existing native tab bar remains in place. Action surfaces follow the selected accent, with black/white foreground chosen for readability. Icons are local vector drawing code; no wallpaper, portrait asset, icon font or package is added.

Photo Library, Files and crop preview remain available. Tap the avatar to reframe, or use Choose photo > Adjust current photo when a photo exists. The avatar initial previews display-name edits. Custom Back and Cancel retain draft-discard behavior; Save validates the name and keeps the selected crop/source. A scoped name-entry handler removes its duplicate native border without changing other fields. A busy indicator and disabled controls prevent overlapping operations.

Local checks: managed iOS compilation completed without reported warnings/errors; 23 existing app appearance/crop tests, source/configuration checks and whitespace checks passed. Actual layout fidelity, touch/keyboard behavior, custom navigation, draft Save/Cancel and VoiceOver on a phone remain **NOT VERIFIED**. The owner authorized committing/pushing the profile layout and a new version tag on 21 October 2026. Release **0.2.3 (6)** includes the photo/crop, accent, Settings border/hex and profile layout refinements. The owner will monitor the tag build; no successful native CI or phone result is claimed here.

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

## Button-label contrast refinement, 21 October 2026

The owner reported hard-to-read black button labels in both appearances and required the exact selected accent to remain the button fill. Source review found that the old maximum-contrast rule intentionally selected black for the default blue and most presets; it did not base that choice on Light/Dark mode. No separate native rendering fault was reproduced on this Windows machine.

Shared action buttons now use 19-unit bold labels and prefer white when it reaches 3:1 against the unchanged accent; brighter accents retain black. Compact/smaller labels retain the 4.5:1 requirement. This follows the [large-text and normal-text contrast distinction](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html), not a claim of complete device accessibility verification. Default blue, purple and pink use white on the large bold action buttons; green, orange, cyan, yellow and white keep dark labels. Neither theme changes nor foreground selection modifies the stored accent or button RGB.

`AccentButton` recomputes its foreground from its own background, size and weight whenever these change, including a live accent update. Home, Clipboard, widgets setup and other shared action buttons use it. The compact 12-unit Settings preview uses the stricter rule automatically. Small selection labels, avatar initials and text-only/navigation controls retain their existing appropriate resources. The pinned [MAUI iOS button implementation](https://github.com/dotnet/maui/blob/10.0.110/src/Core/src/Platform/iOS/ButtonExtensions.cs) applies an explicit text colour to normal, highlighted and disabled states.

Profile primary buttons use matching bold labels/icons on the exact accent. Their caption-area gloss overlay is removed so it cannot change the background used by the contrast calculation. Press feedback scales the surface slightly instead of fading its text and fill; disabled profile controls fade less than before. The native crop confirmation uses a scalable bold font and chooses its foreground from the actual font size, applying it explicitly to normal/highlighted/disabled states.

Local validation: **47 app tests pass**, including preset/bright/dark/custom colour checks, compact-font fallback, and 4,096 RGB samples against both label-size requirements. Managed iOS compilation passes. Source/configuration and whitespace checks pass. Phone rendering, immediate live updates, pressed/disabled feedback, larger-font layout and native crop Dynamic Type remain **NOT VERIFIED**. The owner subsequently authorized committing/pushing these changes and a new tag for **v0.2.5**, version **0.2.5 (8)**, including the Clipboard work. The owner will monitor its build personally; no successful native CI or device result is claimed here.

After GitHub recorded no IPA run for the existing v0.2.5 tag, the owner authorized a fresh release commit and tag **v0.2.6**, version **0.2.6 (9)**, to retry the tag trigger. The functionality is unchanged. The original tag is preserved; only new-run creation will be checked here, with build progress/outcome monitored by the owner.
