# Photo object removal

Select an unlocked photo layer → **Edit photo** → **Remove object**. Brush over
the complete object and its shadow, refine with **Select / Deselect**, then tap
**Remove selection**. The accent overlay is a selection; it is not a transparent
hole. Inspect the fill, use Undo/Redo or Show original, then Apply. Save the widget
to publish the edited layer. Clear selection discards an unprocessed selection;
Apply is disabled while a selection remains. Erase and Restore retain their
existing transparency behavior. Reset photo restores the original colours and
transparency in one undo step.

## Processing and delivery

The AOT-GAN model is fetched only by the build, pinned to a source revision,
60,818,996-byte length and SHA-256 in `assets/object-removal/manifest.json`. The
model and its licence/attribution are bundled only in the host app, not the widget
or Intents extension. Source checks are offline; full IPA validation checks the
model hash and licence/notice contents. Source checks also require the host's model
declaration and all three bundle resources, preventing accidental validation bypass
by omitting that declaration. Interrupted or truncated downloads retry up to three
times; oversized or checksum-invalid downloads never replace the model. Temporary
downloads are removed on failure. No photos, masks or selections are sent
to a server. Existing new-version-tag-only IPA workflow triggers are unchanged.

The first removal compiles the bundled model into the app's cache; later removals
reuse that compiled cache. A corrupt cache is discarded after a model-load failure
so the next explicit retry can rebuild it. Preparation failures keep the selection
and offer a storage/retry message; failed cache moves clean up the compiler's
temporary model directory. Inference uses Core ML CPU/GPU, one
operation at a time, and releases the model after each operation. This targets
iPhone 14 Pro Max without relying on Apple Intelligence or Photos Clean Up.
Actual iPhone memory, speed and quality have not yet been measured.

The model receives a fixed 512×512 tensor, normalized RGB plus a binary selection.
Masked RGB is white (+1), matching the upstream training implementation. A square
crop around the selected object keeps the photo's proportions and includes nearby
context; narrow images use edge padding. Padding repeats the selection alongside
the colour pixels so selected edge content cannot leak back as known background.
Generated RGB is sampled back into the
original dimensions and blended only into the brush selection. Original alpha and
all unselected pixels are preserved. An empty selection or selection covering the
entire image is rejected. Large removals and detailed backgrounds may produce
visible artifacts; this is reconstruction, not recovery of the hidden scene.

Sources: [AOT-GAN](https://github.com/researchmm/AOT-GAN-for-Inpainting),
[Core ML conversion](https://github.com/john-rocky/Inpainting-CoreML),
[Core ML](https://developer.apple.com/documentation/coreml).
The model is unmodified; licence and attribution are included in the app.

## Persistence, lifecycle and review

The existing immutable `.png`, `.original.png` and `.mask` assets are retained.
Repaired colours additionally use private `.working.png` and `.working.sha256`
sidecars. They must exist together, and the stored checksum must match before
editing can reopen; an absent repaired image cannot silently restore original
object colours. Old photos and
cut-outs still open; malformed or incomplete sidecars fail without overwriting
the source. Only the finished display PNG is shared with widgets. Both repaired
and original assets remain protected by saved designs and widget Undo/Redo;
deleting the last reference removes the working sidecar too. Saving unchanged
content creates no replacement asset.

A unified edit history covers colour repairs, transparency and selection strokes,
with at most 30 steps and a 32 MiB history budget. Inference, preview rendering and
encoding use immutable image snapshots. Busy state prevents overlapping edits.
Cancel, backgrounding and dismissal request cancellation; a native prediction
already running is allowed to return before its resources are released, and its
cancelled result is discarded. Failed removal leaves the selection available for
retry. Failed saving keeps the editor open. The original photo, layer position,
size, rotation and opacity are unchanged by Apply.

The editor retains accent-aware colours, 44-point control heights, zoom/panning,
Dynamic Type labels and VoiceOver painting/viewport actions. VoiceOver also has a
Select and Deselect object at visible centre actions. Choosing Select/Deselect
also activates Remove object, even if an unfinished selection remains while using
another brush tool. Native appearance and gesture behavior
still require an iPhone check.

## Verification

Automated tests cover crop/mask mapping, input normalization, selected-pixel-only
compositing, feathering, alpha preservation, invalid/non-finite predictions,
cancellation, shared-history ordering/limits, reset, reopen, immutable source
ownership, repaired-sidecar validation and cleanup, and model packaging/download
integrity. These tests do not execute the Core ML prediction or UIKit layout.

Local verification passed: **325 Core, 132 Infrastructure, 578 App and 76 packaging
tests** (1,111 total), source/project checks, the pinned downloaded model's
checksum, and managed iOS C# compilation with no warnings or errors. Test-process
and temporary-folder restrictions required rerunning the affected checks outside
the restricted environment; aborted/blocked attempts are not counted as passes.
Review regressions for edge padding, missing/tampered repaired colours and truncated
downloads were reproduced before their fixes, then passed afterward. Static parsing
of the downloaded model also confirmed its float32 input `[1,4,512,512]`, input name
`x_1`, output name `var_915`, and exact-rank neural-network mapping; it did not execute
the model.

Before release acceptance, test on iPhone 14 Pro Max:

1. Remove a small object on grass/wall, a border object and an object on a detailed
   background. Check the original/edited view and whether the fill is acceptable.
2. Confirm first-use compilation, repeat operations, memory use and responsiveness.
   Test a portrait, landscape, narrow photo and transparent PNG.
3. Select/deselect at 1× and 8× zoom. Undo/Redo selections and repairs, mix in Erase,
   Restore and Remove background, then Reset and Undo.
4. Apply, save, restart and reopen. Duplicate the layer, edit one copy, and confirm
   the other copy and widget history remain correct.
5. Cancel during compilation, inference and saving; background/resume during
   removal. Confirm no delayed repair, repeated operation or disabled controls.
6. Test light/dark modes, bright/dark accents, larger text and VoiceOver.

No device execution, final IPA packaging or speed/quality guarantee is established
by Windows C# compilation. The owner subsequently authorized committing, pushing
main and pushing the new **v0.2.57** tag for **0.2.57 (59)**. Host and native
extension metadata are aligned. Only the new version tag triggers IPA generation;
the owner will monitor that build and test on iPhone.
