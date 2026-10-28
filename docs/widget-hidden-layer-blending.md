# Hidden live-layer blending

Follow-up to architecture finding A06, 28 October 2026. Prepared for **0.2.44 (46)**. The owner authorized implementation and subsequently committing, pushing main and pushing the new version tag. macOS/device verification remains pending; the owner will monitor the IPA build.

## Confirmed cause

`WidgetScenes.Steps` previously flushed the pending static group before checking whether a Clock, Date or Battery was visible and had nonzero opacity. For `Photo A -> hidden Clock -> Multiply Photo B`, the compositor therefore rendered two separate static groups. Photo B blended inside its own transparent group rather than against A; that completed group was then placed over A with Normal source-over.

All six regression cases (three live-layer types, hidden or zero opacity) failed before correction: the render plan contained two static groups instead of one. This is a confirmed planning defect. Native pixel rendering was not executed on Windows.

## Corrected behavior

- New preview/export plans ignore every hidden or fully transparent layer before deciding boundaries. Photos and custom Text around an invisible live layer stay in the same static group, retaining stack order and photo blend modes.
- A visible live layer with positive opacity still separates static groups. Blending across a visible native live layer is not introduced by this correction.
- Hidden layers remain in the saved design with their original IDs, appearance, placement, locks and visibility. Showing them again restores their live boundary. No layer or user image is removed.
- The editor composition and exported PNG groups already share `WidgetScenes.Steps`; scene manifests use that same plan. No second rendering algorithm, shader/kernel change, timer or cache rewrite was added.

## Versioned compatibility

Scene feature schemas remain **1** (Clock), **2** (Date) and **3** (Battery). The separate `groupingVersion` describes image boundaries, not layer capabilities:

| Value | Meaning |
|---|---|
| Missing, or integer 1 | Existing export: hidden live layers may still separate static groups |
| Integer 2 | New export: only visible, positive-opacity live layers separate groups |
| Any other value | Reject without rewriting existing data |

Explicit null, numeric strings, Booleans and fractional versions are rejected in C# and Swift. Missing metadata defaults to legacy grouping. A shared [grouping fixture](../contracts/widget-scene-grouping-v2.example.json) exercises the new format in both languages.

The C# reader validates an old scene against its old boundaries, rather than comparing it to a newly grouped manifest. The Swift reader loads the original indexed PNG parts without regrouping their pixels. Refresh/republish retains old scene bytes, render keys, parts and assignments exactly. New repository saves require grouping version 2; changing the label on an old export does not bypass part validation.

Saving an edited design installs a new immutable render key, corrected full preview/thumbnail and corresponding version-2 scene/parts. Its ID and same-size slot assignments remain. Recent shared renders from the previous generation remain available under the existing retention policy for cached timelines. Static-only designs still save a PNG without a live scene.

**Existing Home Screen/gallery images do not change merely by installing this fix or tapping Refresh. Open an affected design in Edit and save it to apply the corrected grouping.** Cancel leaves its saved artwork untouched. The live editor shows the corrected draft before saving.

Older strict scene readers do not recognize newly exported metadata, so downgrading exports to earlier app builds remains unsupported. The app's widget library, source photos and existing layer fields require no migration.

## Size-limit review

Compatibility review reproduced a related boundary failure: deserializing an old scene and serializing it with a default new field can increase its measured size beyond 32,768 bytes even though the actual saved file is valid. Publication now validates the length of the original bounded bytes that it copies. Part indices, layer semantics, image signatures/dimensions, decoded pixels and image-byte budgets are still validated normally.

New writes continue to measure their full serialized manifest, including new metadata. The regression test builds a valid legacy export within four bytes of the limit: it republishes unchanged, while an oversized new export is rejected without changing the private catalog. An extreme design at the old limit may need shorter layer names before saving under the new format; the existing editor size-limit error already explains this.

## Review and verification

**851 local automated checks pass:** Core **272**, Infrastructure **105**, App **421**, packaging **53**. Baseline was 819; the follow-up adds 17 Core cases, nine persistence/publication cases and six UI lifecycle cases. Managed iOS C# compilation passes with **zero warnings/errors**. Source/configuration checks and `git diff --check` pass.

Commands run:

```powershell
.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.Core.Tests -c Release --no-restore
.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.Infrastructure.Tests -c Release --no-restore
.\.tools\dotnet27\dotnet.exe test tests/ControlStudio.App.Tests -c Release --no-restore
.\.tools\python\python.exe -m unittest discover -s tests/packaging
.\.tools\python\python.exe scripts/check-source.py
.\.tools\dotnet27\dotnet.exe build src/ControlStudio.App/ControlStudio.App.csproj -c Release --no-restore -t:Compile -p:DesignTimeBuild=true -p:BuildIpa=false -p:ArchiveOnBuild=false -p:EnableCodeSigning=false
git diff --check
```

Coverage includes all four supported sizes and every supported blend mode in grouping tests; hidden and zero-opacity Clock/Date/Battery; custom Text in static groups; visible-live boundaries; legacy feature schemas 1/2/3; strict/future versions; mislabelled parts; retry/Refresh byte preservation; rejected saves; private source preservation; slot continuity and cached-generation retention. UI tests exercise Light/Dark, locked live layers, stable rows/scroll, background/resume, hide/show, save/reopen and an unchanged original draft. Existing tests continue covering accessibility captions, theme resources, gestures and save/cancel guards; no UI controls/layout changed here.

Three additional Swift tests cover both generations, strict version rejection and actual `WidgetDesignStore.scene` PNG-address loading. **They have not run locally:** Swift/Xcode, Core Image pixel output, WidgetKit delivery and physical iPhone behavior are unavailable on this Windows host. Source checks and portable tests do not prove those results. No FPS/memory/CI speedup is claimed.

## Device acceptance

1. Keep an old affected design assigned to a slot. Install the future build, Refresh, and confirm its previous saved image still loads with its assignment.
2. In Edit, place an opaque photo A, a Clock, and photo B with Multiply/Screen/Soft Light. Hide the Clock: compare with moving both photos adjacent while retaining their order. The hidden layer must have no blending effect. Repeat with Date/Battery and with opacity at zero.
3. Save, reopen, Refresh and compare the saved gallery/slot previews and Home Screen artwork. Restore the live layer: its text/battery and original settings should return; visible live layers retain their existing group isolation.
4. Check Small, Medium, Large and iPhone XL, transparent and solid backgrounds, Light/Dark, multiple hidden layers, all-hidden designs, Cancel, background/resume, rotation/opacity and VoiceOver layer controls.
5. Counter widgets and Shortcuts should still work; their bridge, storage, registration and refresh paths were not changed.
