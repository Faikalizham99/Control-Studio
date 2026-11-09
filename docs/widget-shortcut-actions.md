# Shortcut actions for Studio widgets

Version **0.2.54 (56)**. Add/Edit widget now includes a **Tap action** card:

- **Edit widget** is the default for new and existing designs. It keeps the current editor/empty-slot navigation.
- **Run Shortcut** reveals an exact-name field and **Test Shortcut** button. Test launches the Shortcut immediately without saving the design. Save stores the action; Cancel leaves the saved design unchanged. Tap action and name changes participate in the editor's Undo/Redo history.

Create a Shortcut in Apple's Shortcuts app first. In Control Studio, open **Studio → My Widgets → Add/Edit → Tap action → Run Shortcut**, enter its exact name, optionally test, then save. Assign the design to a matching slot as usual. Tapping that Home Screen widget launches the saved Shortcut. The whole widget is one target; Small, Medium, Large and iPhone XL use the same behavior. All slots assigned to the same design share its action. Counter widgets retain their existing behavior.

You can always open the editor through My Widgets. Switching back to Edit widget and saving removes the stored Shortcut name. Switching between choices before saving retains the typed name in the draft. Renaming or deleting a Shortcut in Apple's app requires updating this field; Control Studio does not enumerate or verify the user's Shortcut library. Names are bounded to 200 UTF-16 characters, surrounding whitespace is trimmed on launch/save, and blank/control-character names cannot be used.

## Platform behavior

Apple documents [`shortcuts://run-shortcut?name=…`](https://support.apple.com/en-il/guide/shortcuts/apd624386f42/ios) for launching an existing Shortcut from another app. This implementation passes only an encoded name, without clipboard input, arbitrary URLs or callback parameters. Reserved characters, percent signs, spaces and non-Latin names remain one query value. The original URL string uses Apple's empty command path. The pinned [MAUI iOS URL converter](https://github.com/dotnet/maui/blob/10.0.110/src/Essentials/src/Types/Shared/WebUtils.shared.cs) passes `Uri.OriginalString` to `NSUrl`; ordinary managed URI validation is retained.

The widget keeps its existing public [`widgetURL`](https://developer.apple.com/documentation/widgetkit/linking-to-specific-app-scenes-from-your-widget-or-live-activity) slot address. Control Studio opens, resolves the current saved assignment, then asks iOS to open Shortcuts using the existing MAUI launcher. It does not change widget configuration, signing, scene delegates or extension registration. Swift's existing summary decoder ignores the additional saved-design fields.

Shortcuts may open, request input/permissions, or show its own error if the name does not exist. A successful launch request is not proof that the Shortcut completed. Control Studio reports a failed dispatch without claiming execution success. The selected Shortcut runs when Test or the Home Screen widget is tapped, never on Save, preview updates, ordinary launch or ordinary resume.

## Lifecycle, persistence and review

The private library remains authoritative for tap actions. The current size/slot is resolved on each tap; names are not embedded in widget links. Settings are validated before repository writes and round-trip through private/shared catalogs. Old designs with absent fields default to Edit widget. No layer/scene/photo formats, slot addresses or maximum counts change. Older app binaries that reject unknown JSON fields cannot read the new fields; downgrading after saving in this version is not supported.

Shortcut editing uses raw draft fields outside the validated rendering design. Selecting Run Shortcut before typing a name therefore cannot invalidate the live preview. Save blocks invalid names near the field. Existing drafts, layers, imported photos and history are retained when testing or opening a Shortcut from another slot. Controls use shared accent surfaces, readable disabled states, standard tap sizes, semantic labels and viewport-preservation hooks; the settings card is excluded from blank-space deselection.

The tap router acknowledges the request immediately before the external dispatch. Opening Shortcuts may synchronously deactivate Control Studio; cancellation must not replay an already-issued Shortcut on return. Reads cancelled before dispatch remain deferred/retryable. Launch failures are consumed and can be retried by a new explicit tap. Newer requests cannot be erased by an older handoff. Duplicate delivery of the executing address is ignored while its acknowledged launch is still in flight; the address remains available for a subsequent explicit tap after completion.

## Deep review follow-up — 7 November 2026

- Added regressions that first failed for duplicate delivery after acknowledgement and malformed UTF-16 names, then passed after the fixes. The latter covers a name truncated inside an emoji at the field boundary; complete emoji pairs remain supported.
- Invalid names cannot be persisted or launched as a substituted character. Invalid raw input remains undoable and cannot invalidate the rendered draft.
- Save/Test end name editing before setting operation-busy state and disabling the field. This allows native text composition to finish while the input is still editable. Keyboard Done only ends editing; it does not save or run anything. Final native keyboard/IME behavior remains a device check.
- Inspected the actual pinned iOS launcher/converter, simplified URL creation to a standard `Uri`, and checked the encoded original string that it sends to iOS. No special path/query canonicalization override remains.
- Reviewed saved-library validation/publication, all four slot families, unchanged Counter behavior, Undo/Redo, foreground readiness, newer-request cancellation, failure dialogs, draft/photo retention, theme resources, tap targets and accessibility labels. No additional unresolved issue was found in these reviewed paths; native Shortcuts behavior and final layout still require the iPhone checks below.

## Verification

Automated coverage includes default/old JSON compatibility, all four sizes, encoded names and query-injection characters, validation, save/reopen/publication, unchanged slot assignments, Undo/Redo, preview validity while entering a name, test-without-save, failed launches, current assignment routing, preserved drafts, background/resume, cancelled reads and acknowledged dispatch without replay. All 317 Core, 129 Infrastructure, 578 App and 67 packaging tests pass, along with source guards. Managed iOS compilation passes with zero warnings/errors. The exact-name field disables text prediction and spell checking. These checks do not execute native iOS UI.

Physical iPhone checks still required:

1. Test a harmless Shortcut by exact name, including spaces/non-Latin characters. Confirm Test runs without saving and the draft survives returning.
2. Save and assign each supported widget size; test cold/warm app launches and slots sharing one design. Return to Control Studio and verify no repeat run.
3. Edit through My Widgets, switch to Edit widget, undo/redo and cancel. Check the current saved action is used after reassignment or deletion.
4. Check a missing/renamed Shortcut and unavailable Shortcuts app. Verify iOS prompts, error recovery, light/dark mode, accents, larger text and VoiceOver.
