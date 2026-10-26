# Still three-colour text gradients

Included in **0.2.40 (42)**. The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build.

## Shared editing

Clock, Date and Text share **Colour → Solid / Gradient**. Gradient shows three numbered colour stops, swatches, an accessible selected state, a direction control (Horizontal, Vertical, Diagonal) and a small ramp. One instance of the existing reusable `ColourPickerView` edits the selected stop, keeping Settings' palettes, hue slider, hex input and native picker behavior without stacking three large editors.

Choosing Gradient initially uses the retained solid colour as its first stop, purple in the middle and pink at the end. Stops sit at 0%, 50% and 100%. Mode switches retain both the solid colour and all gradient choices. Pending hex input is committed before changing stop, mode, direction, tool, selection or saving; invalid input blocks the transition and keeps the error/edit state. Explicit save-time commits work while the operation has temporarily disabled the page. Native picker preview/cancel/error callbacks retain the active stop and recover editor availability.

The gradient appears only inside the glyphs, supports existing fonts/weights, multiline Text, alignment, opacity, rotation and all four widget families, and is saved with the layer. It is still: no animation timer is introduced. Locks retain their existing meaning of protecting placement while leaving appearance editable. Duplicate creates independent settings with the same three colours/direction. Solid mode remains the default for old and new layers.

## Rendering and persistence

An optional immutable `gradient` appearance record contains `enabled`, three normalized RGB hex colours and a direction. Null records are omitted from old-style JSON. Clock, Date and Text have separate typed records but adapt to the same shared appearance. Validation rejects invalid directions/colours even when the gradient is disabled, so remembered settings cannot hide corrupt values. Catalog and scene schema numbers stay unchanged, as do layer/image/manifest budgets; older apps with strict option decoding cannot edit new gradient-bearing records.

UIKit preview/export draws a white text alpha mask in an isolated transparency layer and applies a three-stop sRGB gradient with SourceIn compositing. Opacity is applied once. Gradient bounds follow the laid-out text block, and the existing outer layer rotation transforms text and fill together. The approach uses the public [Core Graphics gradient API](https://developer.apple.com/documentation/coregraphics/cgcontext/drawlineargradient%28_%3Astart%3Aend%3Aoptions%3A%29). Native gradient/colour/context wrappers are disposed after use.

The faint outside editing ghost uses a pattern colour drawn through the same gradient helper, retaining native font/text geometry. Only that selected ghost enables gradient label painting; the thirty hidden geometry labels do not allocate pattern images. The pattern image is capped at a 1,024-pixel edge, cleared on suspension/disconnect and refreshed after text/size/alignment changes. Named/system font handling remains shared with existing specimens.

Home Screen Clock and Date use dynamic `Text(.currentDate, format:)`. The original implementation applied a type-erased gradient through `View.foregroundStyle` after layout modifiers. The owner subsequently confirmed that seconds freeze with Gradient and resume with Solid on their iPhone; the original implementation has not preserved live updates there. The correction in 0.2.42 applies a concrete `LinearGradient` directly through Apple's [Text.foregroundStyle](https://developer.apple.com/documentation/swiftui/text/foregroundstyle%28_%3A%29), using a helper that explicitly returns `Text`, before layout modifiers. Actual Home Screen ticking with this correction still requires device confirmation. Static Text remains part of the saved raster groups. Directions use explicit left/top coordinates so an RTL interface does not reverse the user's chosen fill direction. No selector, widget registration, signing, workflow or refresh cadence changes are needed. See [the correction and device test](widget-live-gradient-clock.md).

## Review and verification

Tests cover all text kinds, every direction, enabled/disabled retention, solid compatibility, validation, style/font edits, independent copies, normal thirty-layer scene size, shared UI in Light/Dark, native picker cancellation, pending-hex guards, save/reopen/publication/slots, and ghost-only gradient painting with suspension recovery. A shared scene fixture exercises managed and native Clock/Date gradient contracts. Native tests also check direction coordinates, invalid/disabled values and retained live clock formatting.

Completed automated verification: 241 Core, 384 headless MAUI App, 95 Infrastructure and 42 packaging tests passed (762 total), plus source consistency and whitespace checks. Managed iOS compilation passed with zero warnings/errors. The two added Swift tests remain unexecuted on Windows.

Windows can run Core, Infrastructure, headless MAUI App and packaging tests, source checks and managed iOS compilation. The two added Swift tests and actual UIKit/SwiftUI rendering require macOS/iPhone verification. Device checks remain: compare stop colours/order and direction in editor/export/Home Screen; inspect multiline and short text, every font/weight, alignment, opacity and rotated clipping; confirm live seconds/date updates; test native picker backgrounding and VoiceOver; compare the faint outside preview; profile thirty-layer designs. Local tests do not establish pixel-identical rendering or iOS delivery.
