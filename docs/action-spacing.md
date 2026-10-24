# Action spacing audit — 24 October 2026

The owner authorized matching My Widgets action spacing to Clipboard, auditing related UI across the app, fixing findings and reporting affected components, then authorized committing and pushing main. The spacing update was initially pushed without a new version tag and is now included in the authorized v0.2.29 release, version 0.2.29 (31), alongside the compact widget toolbar and blend illustrations. The toolbar's single grouped strip supersedes the two-row tool layout described below.

## Changes

| Component | Finding and correction |
| --- | --- |
| My Widgets card header | Edit/Delete/Open used a 2-point gap. They now share Clipboard's 8-point gap. Actions move below the title when native text measurement shows that the inline header would squeeze it; long names and larger text receive the full card width. |
| Clipboard saved-item actions | Kept the existing 8-point gap. The five visible actions on Photo/Link items needed 272 points and could overflow a 264-point body on a narrow phone. They now wrap onto another line without shrinking the existing 48-point controls. Hidden actions consume no space and reappear in their original order. |
| Widget photo-layer row | Replaced the 4-point control gap with 8 points. Visibility/More can move below the thumbnail/name on narrow rows. The intrinsic native switch stays vertically centred beside the 48-point menu button; the drag handle remains aligned with the photo row. Existing controls, handlers, image sources and drag/drop scope remain stable. |
| Widget editor tools | The six tools keep their two-row arrangement, using the shared 8-point horizontal and vertical gap instead of 4/10. |
| Widget slots | Change/Clear now use 8 points instead of 7, retaining equal-width columns and the existing compact-card fallback. |
| Shared colour picker | Six 44-point swatch targets could crowd each other in the old six-column grid. They now use 8-point gaps, stay centred, and wrap on narrow widths. Circles align at their top edge even when Custom Colour has a two-line caption. This shared picker is used by Settings, Clipboard header colour and widget background colour. |
| Existing action helpers | Clipboard editor action grids, page action bars, editor/widget headers and blend-mode choices use the same spacing constant. Their existing 8-point appearance is retained. |

The audit also covered Home, Studio, profile actions, theme selection, navigation/size segments, preview captions and colour-field controls. Their different text/icon gaps and compact spacing inside grouped segments are intentional; these are not independent utility-action rows. No unrelated spacing, business operations, persistence formats, navigation destinations, native widget registrations, version values or workflow triggers were changed.

## Implementation and review

`ActionSpacing.Gap` centralizes the 8-point utility-action gap. `ActionButtonRow` measures visible children at their natural sizes, wraps complete targets, retains logical child/VoiceOver order, supports leading/centred/trailing placement and right-to-left layouts, and respects padding and native control alignment. `CardActionHeader` uses native title/action measurements to choose inline or stacked placement. It reuses the measured plan during arrangement at the same width. These are ordinary MAUI layouts using the documented [Measure/ArrangeChildren contract](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/layouts/custom?view=net-maui-10.0) and the pinned [10.0.110 layout constraint rules](https://github.com/dotnet/maui/blob/10.0.110/src/Core/src/Layouts/LayoutManager.cs).

There are no asynchronous layout operations, event-handler replacements, animation loops or page-lifetime subscriptions. Existing native button hit targets, destructive colours, accent bindings, accessibility names, click/toggle callbacks and disabled-state propagation remain in use. Reflow changes positions, not the underlying controls or data. No saved-library migration or additional storage is involved.

## Verification

**231 app tests pass**, including 13 new responsive-layout cases covering narrow/wide wrapping, target sizes, visible/hidden actions, row order, padding, RTL, long-title reflow and return to inline placement, intrinsic switch alignment, swatch top alignment, palette selection and disabled-state propagation. The existing layer-row regression still confirms stable image/switch instances and correct selection/visibility callbacks. **32 packaging/policy checks**, source/project generation checks and whitespace checks pass. Managed iOS C# compilation passes with **zero warnings/errors**.

The sandbox prevented the test runner's local connection and denied temporary packaging-test directories; rerunning the affected commands with approved outside-sandbox execution passed. These were environment restrictions rather than failing app assertions.

Physical iPhone checks remain pending: My Widgets spacing with short/long names, light/dark themes and varied accents; narrow Photo/Link Clipboard action wrapping and all actions; photo-layer visibility/menu/drag controls; slot Change/Clear; all shared colour-picker placements; Dynamic Type, VoiceOver focus after reflow, RTL and rotation. Native pixels, animations, actual hit testing and Xcode/AOT linking have not been verified on Windows.
