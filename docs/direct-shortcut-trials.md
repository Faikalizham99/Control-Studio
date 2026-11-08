# Direct Shortcut trials — 8 November 2026

Version **0.2.55 (57)** is a proof of concept for iOS 27+. Two additional Home Screen widget kinds compare system-managed execution paths before changing the working Studio widgets:

| Gallery name | Purpose | Configuration |
| --- | --- | --- |
| Shortcut Test - Picker | Reference path using Apple's `SystemShortcut` selection | Hold the widget → Edit Widget → Action |
| Shortcut Test - Static | Experiment using `RunSystemShortcutIntent()` without a custom configuration intent | Use the system action configuration if iOS exposes it; whether this works with static slots is not yet established |

Both support Small, Medium, Large and iPhone XL. They use neutral system styling and no saved photos or Studio assignments. Small/Medium have compact content; Large/XL include additional instructions. These are separate tests, not a replacement for the current Add/Edit widget Tap action or typed Shortcut name. The existing Counter, 20 Studio slots, saved designs, app routing and URL-based Shortcut behavior are retained.

## Native implementation

Apple documents [`RunSystemShortcutIntent`](https://developer.apple.com/documentation/appintents/runsystemshortcutintent) for `Button(intent:)` inside a widget. It supports system actions, apps and user-created Shortcuts. [`SystemShortcut`](https://developer.apple.com/documentation/appintents/systemshortcut) is an opaque selection owned by iOS; the app does not invent an identifier, enumerate Shortcut contents or convert the existing typed name into one. Apple's [parameterless initializer](https://developer.apple.com/documentation/appintents/runsystemshortcutintent/init()) is used only as the static experiment and an inactive unconfigured button in the picker trial.

The picker configuration has an optional Action parameter so the gallery/initial state can show setup instructions without executing an unspecified action. Its button is inactive until an action is selected. It is not exposed as a separate action in Shortcuts. Providers only produce entries; they never call `perform()`, open URLs, read/write shared storage or schedule app navigation. A tap executes the system intent directly. Shortcuts requiring input or other interaction may still open Shortcuts, as Apple's API documentation states; avoiding that for every possible Shortcut is not promised.

All new types and their bundle registration are gated to iOS 27. Fixed kind strings and literal gallery names/descriptions avoid the earlier interpolated-metadata startup assertion. The trial source is compiled only in the WidgetKit extension, not the bridge or existing counter Intents extension. The root adds one gated sub-bundle, keeping its builder compact. The transparency adapter does not rewrite the trial kinds.

## Packaging and review

The native build now checks that the widget extension's extracted App Intents metadata includes `DirectShortcutPickerConfiguration`. The widget Info.plist advertises the trials with `ControlStudioDirectShortcutTrials = true`; IPA validation requires the metadata when that flag is present. Missing, malformed, oversized or wrong-intent metadata fails validation. Reads are capped at 2 MB, the JSON root must be a collection, and nesting is bounded. Old IPAs without the flag remain inspectable. This is a bounded name-presence/placement guard, not a validator for Apple's private metadata schema or proof of successful device configuration/execution; a later signing service still needs verification.

All 78 packaging checks pass, including target-membership, OS gating, existing-slot isolation, no-URL/no-provider-execution, build-validation and metadata-corruption cases. New subprocess checks execute the real metadata CLI for both counter and widget modes from outside the repository directory. Source/plist/project-generation checks pass. Those are source and packaging checks, **not Swift compilation or native runtime tests**. No Swift/Xcode/iOS SDK runtime is available on this Windows workstation. Existing CI runs Xcode 27 compilation and extracted-metadata validation when authorized code is pushed; IPA production remains exclusive to a newly pushed version tag. The owner authorized the v0.2.55 release commit, main push and version-tag push and will monitor the build. Workflow triggers are unchanged.

Reviewed behavior includes initial/unconfigured state, provider snapshots, `.never` timelines for these data-free buttons, nil selection, selected action labels, tap area, light/dark contrast, compact Medium layout, VoiceOver labels, registration availability and noninterference with saved data. The framework owns action selection, execution, permissions and cancellation. There is no app-side retry loop or duplicate app→Shortcuts forwarding in these trials.

### Follow-up review of the uncommitted changes

The review reproduced gaps before fixing them: a stale file size could bypass the intended read bound, a scalar JSON string could pass the configuration-name test, and the CLI failed to import its new helper under the workspace's isolated Python. Validation now bounds the actual stream read, rejects scalar/invalid/overly nested documents, and explicitly resolves its local module. Argument parsing reports missing/unknown arguments as usage errors instead of treating flags as filenames. Valid counter validation still succeeds; counter-only metadata cannot satisfy widget validation, or vice versa. No additional source-level finding remains in the reviewed native paths, with the Xcode/runtime limits above still applying.

## Device test after a newly authorized build

1. Create a harmless Shortcut with no input, such as setting brightness, in Apple's Shortcuts app. Keep a second Shortcut that uses Ask for Input for comparison.
2. Install the new FlareStore-signed IPA and open Control Studio once. Do not change saved Studio assignments.
3. In Home Screen Add Widget → Control Studio, add **Shortcut Test - Picker**, initially Small. Hold it → Edit Widget → Action and select the no-input Shortcut. The initial "Choose an action" state is not ready to execute.
4. Tap the configured button. Record whether the action ran and whether Control Studio or Shortcuts appeared. Return normally and verify there is no unsolicited repeat. Repeat with Control Studio closed, then in Medium, Large and XL. Try two instances with different selected actions and reconfigure/remove only the trial instances.
5. Test the Shortcut with Ask for Input. An iOS prompt or opening Shortcuts is permitted behavior, not proof that direct execution failed.
6. Add **Shortcut Test - Static**. Check whether iOS offers any action selection for this static widget and whether the chosen action executes directly. If no configuration is offered or it does nothing, report that result; do not treat the static path as established or reset existing Studio widgets to troubleshoot it.
7. Confirm existing Counter and Studio widgets still appear and work. Check long action names, light/dark mode, larger text and VoiceOver in both trial widgets.

Interpretation: Picker working with Static failing supports the native feature but not a drop-in static-slot implementation. Picker failing after signing requires inspecting the signed widget metadata and native/device reports before any rollout. Both working supports integrating the static button into a later reviewed slot design. No automatic migration or removal of current widget kinds should precede that evidence.
