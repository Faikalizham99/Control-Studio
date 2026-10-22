# Clipboard

Clipboard has three sections: **Text / Photo / Link**. The owner requested this reduced scope on 22 October 2026 and explicitly chose deletion of saved retired code entries. Add creates the selected type. Items retain saved order, direct Edit and confirmed Delete controls. There is no automatic clipboard reading or background history.

| Section | Actions and storage |
|---|---|
| Text | Copy; exact whitespace and Unicode, up to 10,000 characters. |
| Link | Blue underlined URL: tap to open, hold for a webpage preview and Copy link / Open in browser / Share menu. Existing Copy/Open icons remain. Validated HTTP(S) only, up to 2,048 characters. |
| Photo | Paste a copied photo, import from Photo Library or Files; Copy image, Preview, Share / Save to Photos. Private orientation-correct PNG, longest side at most 4,096 pixels, with a 512-pixel thumbnail. Source at most 10 MB, saved PNG at most 20 MB. |

Names are limited to 60 characters. Existing IDs, metadata and ordering survive edits. Text is never trimmed; only outer whitespace in names/URLs is removed. Generic counter records remain preserved and hidden. User content is not included in application logs.

Long text and link cards initially show at most three lines with a trailing ellipsis. An accent-coloured “Show more” control appears only when native text measurement detects overflow at the card's current width and font size; tapping it displays the full content, and “Show less” collapses it again. Short items and photo previews have no expansion control. This is display-only: Copy, Open and Edit still receive the complete saved content. Recycled/reloaded cards start collapsed, and overflow is rechecked after width/font/handler changes. CollectionView measures individual item heights.

Each card has a one-point horizontal divider between its title and body. It spans the card's inner width, uses a neutral colour adapted to Light/Dark mode and is excluded from accessibility navigation.

The title area has a solid coloured background. By default its optional `headerColorHex` is absent/null, so the header follows the live Settings accent. A saved per-item override uses canonical `#RRGGBB`, independent of the global accent. Titles use the existing 19-unit bold black/white contrast rule. The body keeps its normal Light/Dark surface.

Local validation for card expansion: 42 app tests, managed iOS compilation and whitespace checks pass. Native overflow detection, card resizing, repeated expansion/collapse, large fonts, rotation, Unicode/multiline previews and scrolling/recycling still need iPhone verification.

## Selected sections and item icons

The selected segment uses the same readable accent colour as navigation (`AccentText`) for its icon and bold label, with a subtle 12% accent background and an accent outline. It no longer uses the black/white foreground intended for filled action buttons. Unselected segments use neutral icons and regular labels on a transparent surface. Theme bindings and dynamic resources are explicitly removed before changing a segment's palette so they cannot compete. The native blank hit target sits beneath the independently drawn visuals. Accent and theme changes update the selected palette through dynamic resources. Switching sections clears feedback from the previous section.

Items have pencil/Edit, red trash/Delete and filled-accent clipboard/Copy icons. Links add an external-open arrow; photos add an eye/Preview icon. Buttons have 48-point native hit targets and item-specific accessibility descriptions. Delete asks for confirmation. Selected accent rendering in both themes, touch routing, live accent/theme changes and VoiceOver still require phone checks.

## Link appearance and native preview

Saved URLs are underlined and blue in both appearances, independently of the selected accent; the header still follows its configured colour. The URL has a minimum 44-point tap target. Long URLs retain the three-line expansion control, and Copy/Open/Share always receive the complete saved URL.

The iOS label handler adds a native `UIContextMenuInteraction`. Its preview provider creates an `SFSafariViewController` only when the user holds the URL; displaying or scrolling the list does not request a webpage or fetch metadata. This follows Apple's [context-menu preview example](https://developer.apple.com/documentation/uikit/adding-context-menus-in-your-app?language=objc). Tapping the preview opens the default browser. The menu offers Copy link, Open in browser and the native Share sheet. If preview creation fails, the actions remain available; website loading and error presentation belong to the native Safari view, and not every website is guaranteed to preview successfully.

Contexts capture the current item and reject actions after the card is rebound or disabled. Rebinding, disabling, unloading or disconnecting dismisses the menu and releases its preview; normal dismissal releases it after the animation. VoiceOver custom actions expose Open, Copy and Share without requiring the hold gesture. The same HTTP(S) validation applies to taps and menu actions. No dependency, entitlement, storage schema or version change is needed.

Local validation on 22 October 2026: **71 app tests**, **22 packaging tests**, source configuration checks and managed iOS compilation pass. Link regressions cover Light/Dark contrast, underline and target size, unchanged full URLs, lazy context creation, validation, disabled controls and stale actions after recycling. Native hold/tap arbitration, preview loading/dismissal, scrolling/recycling, iPad Share presentation, live appearance, VoiceOver and real-device browser behaviour remain **NOT VERIFIED** on an iPhone/iPad.

## Add and edit design

Text, Photo and Link editors share the owner's 22 October 2026 reference layout: a custom Back/title/Cancel header, icon-led Name and content cards, inset fields, a privacy note and a rounded accent Save item action. The background is plain black or light grey; card gradients and borders adapt to the appearance. The save fill follows the selected accent, with the existing large-label contrast rule and an accent glow. Save and Cancel retain their original persistence and draft-photo cleanup behaviour.

Text has a 10,000-character editor and count, Paste, Clear, Example and an expanded editor. The expanded editor updates the same draft and does not save it; Done returns to the form. Link has a 2,048-character URL field/count and matching Paste/Clear/Example actions. Paste reads the clipboard only after a tap, rejects oversized content without truncating it, and reports unavailable text. Example confirms before replacing nonempty draft content. Normal save validation still requires a name and valid text, HTTP(S) URL or imported image.

Photo has a contained preview (no cropping of the original), an empty state and explicit **Paste photo**, **Photo Library**, **Files** and **Remove** actions in two rows with two buttons each. Removing/replacing an existing saved photo affects its file only after a successful save; new abandoned draft imports are cleaned up. An in-progress operation disables save, navigation and the form to prevent duplicate actions.

All three editors include Header colour with a live title preview and a **Use app accent** switch. Turning it off reveals the same reusable `ColourPickerView` used in Settings: five presets, custom spectrum, hue slider, labelled HEX field/error/focus border, swatch and native iOS colour picker. The component emits selection events and owns no preferences or item storage. Settings persists global changes; the item editor keeps a draft until Save. Cancel discards that draft. Resetting Use app accent saves null rather than a copy of the current accent, so future Settings changes still apply. Invalid pending HEX blocks item saving. Busy native picking disables the item form/navigation.

Local validation: 42 app tests and managed iOS compilation pass. Native visual matching, keyboard behaviour, expanded editing, clipboard access, import/cancel/replace flows, live accent/theme updates and accessibility remain **NOT VERIFIED** on an iPhone.

## Photos

Photo Library uses MAUI `PickPhotosAsync` / iOS PHPicker with one selection and no broad library access. Files remains available. Originals in Photos/Files are never edited or deleted. Cancelled/replaced drafts and successfully replaced/deleted saved images are cleaned up. A database mutation finishes before its referenced file is removed. Process termination can leave an unreferenced private draft file; there is no automatic storage sweep yet.

Paste photo accesses `UIPasteboard.General.ItemProviders` only after its explicit button tap. It selects the first item offering a concrete image UTI, loads that encoded representation and sends it through the same ImageIO orientation/size normalization and private PNG/thumbnail writer as Library and Files. It does not read the clipboard when opening/resuming the page, monitor clipboard changes, download an image URL, or save an item automatically. Image-link-only, empty, inaccessible and failed pastes show a status message without replacing the existing draft. iOS may ask for paste permission; the UI uses a custom action rather than `UIPasteControl`. Apple documents the [system paste approval behaviour](https://developer.apple.com/documentation/uikit/uipastecontrol) and [item-provider transfer model](https://developer.apple.com/documentation/foundation/nsitemprovider).

`PhotoImportInput` supplies the shared 10 MB encoded-input bound, empty-input checks and bounded stream reader. Native paste data length is checked before creating a managed byte copy. A successful paste uses the existing draft acceptance/cleanup path: failed transfers leave the current photo intact, Cancel deletes newly imported drafts, and Save persists the selected private copy. Multiple copied images currently import only the first image-bearing item. Animation/Live Photo content becomes a still image.

Local validation for Paste photo on 22 October 2026: 61 app tests pass, including exact chunked reads, oversized/empty input, native-length bounds and cancellation. Managed iOS compilation passes. Actual cross-app paste/permission/denial, unsupported clipboard formats, preview orientation/transparency, save/reopen, replacement/cancel cleanup and both-theme button layout remain **NOT VERIFIED** on an iPhone. No new dependency, permission key, schema/version change, commit or tag is needed for the implementation.

Copy writes an actual `UIImage` to `UIPasteboard`. The destination app must accept image pastes. Preview displays the saved copy. The native share sheet offers sharing and, where available, Save Image. `NSPhotoLibraryAddUsageDescription` explains that explicit save operation. No undocumented Photos URL scheme is used. Images are normalized copies; animation/Live Photo behavior and original metadata are not retained. Transparency is retained where supported.

## Storage upgrade

Private SQLite accepts database/document versions 1, 2, 3 and 4, and writes version 4 for optional header colour metadata. Existing items default to following the app accent. On opening a known version 1/2 document, the upgrade deletes only the explicitly retired record type, validates remaining entries and commits the cleaned document atomically. Other unknown types, malformed data and future schemas cause an error without partial cleanup or a data reset. Cancelled upgrades roll back. Secure deletion is enabled when replacing the old document. Retained text, link, photo and counter records preserve their IDs, content and metadata; sort positions are compacted after deletion. Older documents without retired entries remain unchanged on read and upgrade on mutation.

The single legacy discriminator in the repository and its migration fixtures exist only to delete old data safely. There is no editor, viewer, model, formatter, bundled web asset or language runtime for the retired feature. Its downloaded archives and extraction helpers have also been removed from the local workspace.

The shared App Group counter schema, native widgets, App Intents, entitlements and photo permission remain unchanged. The private document is limited to 200 entries and 16 million serialized characters. Earlier app releases cannot open version 4. The owner authorized committing/pushing the current Clipboard work and a new release tag for **v0.2.8**, app/extension version **0.2.8 (10)**. The owner monitors the build personally; no native build or iPhone success is claimed here.

## Validation

Passed 126 automated tests: 42 Core, 21 infrastructure, 42 app and 21 Python packaging tests. Source configuration checks, managed iOS compilation, workflow lint and whitespace checks also passed. Regression checks cover upgrade deletion, retained content/metadata/order, relaunch persistence, cancellation and preservation of unknown/future data. Phone upgrade, photo actions, icon hit targets, appearance and accessibility remain **NOT VERIFIED**.

For the selected-section accent correction on 22 October 2026, the 42 app tests, managed iOS compilation and whitespace checks passed again. These checks do not verify native rendering; Light/Dark selection and changing the accent while the page remains open still need an iPhone check.

For coloured headers and the shared picker on 22 October 2026, **138 tests passed** (50 Core, 24 infrastructure, 43 app, 21 packaging), plus source/configuration checks and managed iOS compilation. New regressions cover canonical colour validation, optional-field JSON, per-item persistence/reordering/renaming/reset, schema-3 defaults and upgrade, corrupted-colour data preservation and ordinary edits retaining the override. Live default-accent updates, custom colours, picker interaction/focus/drag, pending invalid HEX, cancel without saving, card clipping, scrolling/recycling, native picker dismissal and accessibility still require iPhone verification.

After the owner reported the v0.2.8 Save button showing a neutral gradient with black text, primary custom actions were moved to shared `AccentSurface`. Save now binds the solid accent brush directly and calculates its label/checkmark colour from that fill. Related profile/compact-preview styling and header/selector manual-to-resource transitions were corrected in the same audit. See [component rules and managed regression evidence](ui-components.md). Current local checks: 57 app and 22 packaging tests, locked test restore, managed iOS compilation and source/whitespace checks pass; iPhone rendering remains pending.

The owner authorized committing/pushing these changes and the **v0.2.9** release tag on 22 October 2026. App and native extension versions are **0.2.9 (11)**. This release also includes the shared accent-surface correction and Paste photo changes committed after v0.2.8. The owner monitors build progress personally; native build and device verification remain pending.

Commits, pushes and release tags require the owner's explicit permission. The owner monitors future build progress personally.
