# Live clock gradient compatibility correction

Included in **0.2.42 (44)**. The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build. The owner reported that a Home Screen clock stops advancing its seconds when Gradient is enabled and resumes when switched to Solid. The editor and saved image are separate renderers; successful in-app rendering does not establish Home Screen ticking.

## Change and rationale

Previously, the renderer applied `.foregroundStyle(AnyShapeStyle(...))` after `.lineLimit` and `.minimumScaleFactor`. At that point styling uses the general `View` modifier. The correction styles the incoming `Text` directly, before view layout, through a helper with an explicit `Text` return type. Gradient uses a concrete `LinearGradient`; Solid uses a concrete `Color`. This selects the text-specific API and retains the dynamic source inside a `Text` value. The source order and type boundary are confirmed; whether this resolves the WidgetKit behaviour is pending device testing.

Both Clock and Date use this helper. Clock still constructs `Text(.currentDate, format: clock.formatStyle())`; Date retains its own current-date format. Neither becomes a preformatted string or an image. The three saved stops at 0%, 50% and 100%, explicit direction coordinates, solid colour, formats, font/weight, scale, alignment, clipping, opacity, rotation, frame and position are preserved. Disabled gradients still use the remembered solid colour. No data migration or change to saving, duplication, slots or refresh policy is needed.

Apple distinguishes [Text.foregroundStyle](https://developer.apple.com/documentation/swiftui/text/foregroundstyle%28_%3A%29), which returns `Text`, from [View.foregroundStyle](https://developer.apple.com/documentation/swiftui/view/foregroundstyle%28_%3A%29), which returns a general view. WidgetKit supports [system-updated date/time text](https://developer.apple.com/documentation/widgetkit/displaying-dynamic-dates), but this documentation does not guarantee every gradient composition remains live. The correction is a compatibility candidate, not a confirmed statement about an unavoidable iOS limitation.

## Review and verification

The change is confined to Home Screen Clock/Date styling. In-app graphics, static Text gradients, photo blending, battery drawing, counter widgets and transparent backgrounds use their existing paths. No extra timer, timeline entries, offscreen drawing group, mask, duplicated text, persistent diagnostic interface or fallback to a different saved colour is introduced. The helper remains inside the existing iOS 18+ scene view, above the availability requirement of the text-specific styling API. Source checks guard type preservation, modifier order, live time/date sources, layout modifiers, colour stop order and directions. Core tests retain formatting and saved-gradient validation coverage.

Windows cannot compile SwiftUI/WidgetKit or validate system-managed ticking. Source checks and managed tests do not prove that the fix works on the Home Screen. Swift/Xcode compilation, performance, accessibility and actual ticking remain device checks. A gradient-masked native text approach is a further candidate only if this first approach still freezes; it is not shipped alongside an unverified automatic fallback.

## Device test

1. Duplicate a Clock design with seconds enabled. Keep one Solid and give the other a clearly visible three-colour Gradient. Save and assign them to separate slots of the same size. Refresh and return to the Home Screen.
2. Watch both for at least a minute, including a minute rollover. Leave the Home Screen, return, lock/unlock, and repeat after backgrounding the app. The numbers must keep advancing; the gradient itself is intentionally still.
3. Check 12-hour with and without AM/PM, 24-hour, and seconds off. Compare Horizontal/Vertical/Diagonal, every font family and available weight, left/centre/right alignment, opacity and free rotation. Check Small, Medium, Large and iPhone XL, including measured canvas sizes, transparent/solid backgrounds and mixed photo/text layers. Verify glyph clipping and gradient alignment as digits change width.
4. Switch Gradient → Solid → Gradient, save and reopen. Colours, direction, font and position must survive. Check VoiceOver reads the clock and does not discover duplicate text. Confirm Date still displays its saved weekday/format and follows a day rollover. Record the exact iOS version and Home Screen appearance mode (normal, Clear or tinted) when comparing results.
