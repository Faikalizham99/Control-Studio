# My Widgets header refresh — unreleased, 24 October 2026

The owner authorized moving Refresh from the bottom of My Widgets to its fixed header, then authorized committing and pushing the update to main. This update remains unreleased; no new version tag or IPA build is included in this push.

## Behaviour and review

- Refresh is a 48×48-point icon immediately before the existing Add action in the top-right header. Back and Add remain available. Both right-hand actions have an eight-point gap and an explicitly allocated 104-point header column; other widget-page headers retain their existing 48-point column. The title retains its two-line allowance. The header remains outside the scrolling gallery, so refreshing does not require reaching the last design.
- The old bottom Refresh widgets button is removed. The existing `RefreshWidgetDesigns` native button identifier and accessible name are retained. The icon and progress indicator follow the current readable accent resource in both themes, using the shared secondary-action surface rather than a new colour palette.
- A user-requested refresh replaces the icon with a native progress indicator and changes its accessible name to “Refreshing widgets”. Busy state prevents duplicate taps. The existing page operation guard serializes reading/publishing and disables conflicting actions. The icon and controls recover through `finally` on success, failure or cancellation. Departure clears progress immediately, and unloading stops the indicator.
- The existing library read and App Group publication are preserved. Refresh requests Home Screen updates; iOS controls when they appear. Completion, empty-library results and failures are announced only while the page is foreground. Existing visible failure details remain in the gallery status area. There is no new persistence, widget format, family/slot change or IPA trigger.
- Preview loading, size selection, full-render proportions, editing, deletion and Widget slots navigation retain their existing flows. The header change does not move those controls or the navigation bar.

## Verification

**250 app tests pass**, including progress/idle accessibility names, duplicate/disabled tap protection, recovery, a 48-point target and live accent updates for the icon/spinner. The existing lifecycle, preview, colour, layer-order and persistence regressions remain green. **32 packaging/policy tests**, source/project generation checks and whitespace checks pass. Managed iOS C# compilation passes with **zero warnings/errors**.

Source review confirms the header is outside the `ScrollView`, only one Refresh action exists, its column fits both full-size buttons, and all other callers retain their previous header widths. The Windows checks do not run native layout or animations. Physical iPhone checks remain pending: scroll through a long gallery and refresh without returning to either end; inspect 320-point/narrow layouts, landscape and large text; check both themes/varied accents, VoiceOver, spinner feedback, rapid taps, failed publication, departure/return during refresh and Add/Back/size/slot navigation afterward. Xcode/AOT verification remains pending.
