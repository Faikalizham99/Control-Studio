# Widget editor settings scroll stability — 0.2.35 (37)

A [follow-up in 0.2.36](widget-editor-scroll-followup.md) addresses layer-row anchoring and unnecessary panel closure. The release record below describes 0.2.35; the follow-up adds bounded instant position compensation for necessary reflow.

The owner reported that tapping photo or clock settings moved the page up or down. The primary cause was the editor's `RevealAdjustments` call: each newly opened panel requested `ScrollToAsync(..., MakeVisible, false)` against the full page scroll introduced in 0.2.33. Different panel positions/heights produced different destinations. Photo-tool refreshes, clock-toolbar changes, blend groups and every weight update also requested automatic horizontal reveals. Hidden or not-yet-arranged element requests can be deferred by MAUI, making movement appear later than the initiating tap.

Normal editor settings now issue no programmatic scroll requests, including the layer menu's precision controls. Photo tools, clock tools, blend groups and weight choices leave horizontal movement to the user. The current weight is shown in the weight heading, even when its chip is offscreen. Controls, highlighting, accessible descriptions and manual scrolling remain available.

Panel switching also avoids hiding the entire source before mounting the destination. A page-local `WidgetEditorScrollView` protects against a shorter settings layout reducing the content below the current viewport. Before a settings change, it keeps a minimum content height of the current vertical offset plus viewport height. This contributes only trailing space after the existing content, rather than a fixed blank settings panel or gaps between cards. It never calls ScrollTo or resets the offset. The temporary minimum reduces as the user scrolls toward earlier content, returns to the original minimum at the top, and resets on viewport resizing, content replacement, removal or actual editor departure. Picker coverage/backgrounding keeps the page-owned constraint until genuine departure or a layout size change.

Preservation covers photo tool opening/closing, Rotate/Flip/Opacity changes, blend-group rebuilding and Done, clock-panel switching, font/weight updates, precision controls and ordinary controls refreshes. The live canvas stays mounted at the same width-based size and proportions. Saved designs, geometry, font choices, publication and Home Screen rendering are unchanged.

Review covered native scroll-request sources, temporary panel collapse, retained manual horizontal/vertical positions, compact/tall viewport bounds, original minimum sizes, bounce, resize/replacement/removal, picker/background recovery, locks/selection, invalid-colour handling, accessibility text and persistence boundaries. No scroll timer, platform-wide handler override, offset-restoration loop or new persisted state is introduced. Native keyboard and accessibility focus behaviour still belongs to MAUI/iOS.

Automated verification:

- **314 App tests** pass, including eight new viewport/transition regressions. They verify minimum-height policy, preserved reported offsets, manual strip offsets, no scroll events, destination-before-source panel switching, same canvas parent/dimensions, and cleanup.
- **40 packaging checks** pass. The new source-policy check prevents editor/settings/strip code from issuing scroll commands. This matters because the old `IsLoaded`/handler guards skip the offending requests in headless MAUI tests, so a scroll-event assertion alone would miss the regression.
- Source/project-generation, whitespace checks and managed iOS C# compilation pass with zero warnings/errors.

These are managed control/policy checks, not UIKit touch or actual device layout verification. Physical iPhone checks remain required: tap every photo/clock tool, compare tall Font/Blend panels with short panels, select fonts/weights and blend groups, close Blend, use precision controls, manually scroll both axes, resize with keyboard/large text, cancel pickers, background/resume, and leave/reopen the editor. Confirm the temporary bottom space does not obscure the next action and the full-size canvas remains unchanged. The owner authorized committing, pushing main and pushing the new v0.2.35 tag. App and extension versions are 0.2.35 (37); the owner will monitor the IPA build. Workflow triggers remain unchanged.

Framework reference: [MAUI 10.0.110 ScrollView element-scroll and deferred-request handling](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/ScrollView/ScrollView.cs).
