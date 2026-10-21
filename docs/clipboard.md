# Clipboard

Clipboard has three sections: **Text / Photo / Link**. The owner requested this reduced scope on 22 October 2026 and explicitly chose deletion of saved retired code entries. Add creates the selected type. Items retain saved order, direct Edit and confirmed Delete controls. There is no automatic clipboard reading or background history.

| Section | Actions and storage |
|---|---|
| Text | Copy; exact whitespace and Unicode, up to 10,000 characters. |
| Link | Copy or Open in the default browser; validated HTTP(S) only, up to 2,048 characters. |
| Photo | Import from Photo Library or Files; Copy image, Preview, Share / Save to Photos. Private orientation-correct PNG, longest side at most 4,096 pixels, with a 512-pixel thumbnail. Source at most 10 MB, saved PNG at most 20 MB. |

Names are limited to 60 characters. Existing IDs, metadata and ordering survive edits. Text is never trimmed; only outer whitespace in names/URLs is removed. Generic counter records remain preserved and hidden. User content is not included in application logs.

## Selected sections and item icons

The selected segment uses the same readable accent colour as navigation (`AccentText`) for its icon and bold label, with a subtle 12% accent background and an accent outline. It no longer uses the black/white foreground intended for filled action buttons. Unselected segments use neutral icons and regular labels on a transparent surface. Theme bindings and dynamic resources are explicitly removed before changing a segment's palette so they cannot compete. The native blank hit target sits beneath the independently drawn visuals. Accent and theme changes update the selected palette through dynamic resources. Switching sections clears feedback from the previous section.

Items have pencil/Edit, red trash/Delete and filled-accent clipboard/Copy icons. Links add an external-open arrow; photos add an eye/Preview icon. Buttons have 48-point native hit targets and item-specific accessibility descriptions. Delete asks for confirmation. Selected accent rendering in both themes, touch routing, live accent/theme changes and VoiceOver still require phone checks.

## Photos

Photo Library uses MAUI `PickPhotosAsync` / iOS PHPicker with one selection and no broad library access. Files remains available. Originals in Photos/Files are never edited or deleted. Cancelled/replaced drafts and successfully replaced/deleted saved images are cleaned up. A database mutation finishes before its referenced file is removed. Process termination can leave an unreferenced private draft file; there is no automatic storage sweep yet.

Copy writes an actual `UIImage` to `UIPasteboard`. The destination app must accept image pastes. Preview displays the saved copy. The native share sheet offers sharing and, where available, Save Image. `NSPhotoLibraryAddUsageDescription` explains that explicit save operation. No undocumented Photos URL scheme is used. Images are normalized copies; animation/Live Photo behavior and original metadata are not retained. Transparency is retained where supported.

## Storage upgrade

Private SQLite accepts database/document versions 1, 2 and 3, and writes version 3. On opening a known older document, the upgrade deletes only the explicitly retired record type, validates remaining entries and commits the cleaned document atomically. Other unknown types, malformed data and future schemas cause an error without partial cleanup or a data reset. Cancelled upgrades roll back. Secure deletion is enabled when replacing the old document. Retained text, link, photo and counter records preserve their IDs, content and metadata; sort positions are compacted after deletion. Older documents without retired entries remain unchanged on read.

The single legacy discriminator in the repository and its migration fixtures exist only to delete old data safely. There is no editor, viewer, model, formatter, bundled web asset or language runtime for the retired feature. Its downloaded archives and extraction helpers have also been removed from the local workspace.

The shared App Group counter schema, native widgets, App Intents, entitlements and photo permission remain unchanged. The private document is limited to 200 entries and 16 million serialized characters. Earlier app releases cannot open version 3; the app version/tag itself has not been changed by this local work.

## Validation

Passed 126 automated tests: 42 Core, 21 infrastructure, 42 app and 21 Python packaging tests. Source configuration checks, managed iOS compilation, workflow lint and whitespace checks also passed. Regression checks cover upgrade deletion, retained content/metadata/order, relaunch persistence, cancellation and preservation of unknown/future data. Phone upgrade, photo actions, icon hit targets, appearance and accessibility remain **NOT VERIFIED**.

For the selected-section accent correction on 22 October 2026, the 42 app tests, managed iOS compilation and whitespace checks passed again. These checks do not verify native rendering; Light/Dark selection and changing the accent while the page remains open still need an iPhone check.

Commits, pushes and release tags require the owner's explicit permission. The owner monitors future build progress personally.
