# Twenty slots per size

Version **0.2.47 (49)** adds Studio 11–20 to the Home Screen gallery. Small, Medium, Large and iPhone Extra Large each have independent addresses 1–20: 80 current assignments in total. A slot can reuse a saved design; the separate limit of 50 saved designs is unchanged. Counter remains its own Small/Medium widget.

## Shared definitions

`contracts/widget-slot-config.json` is the source for slot count, stored family count and the slot catalog byte bound. Run `python scripts/widget_slot_config.py` after editing it. Generated sections are committed source; the app and native project do not need Python at runtime or a new build dependency. `scripts/check-source.py` runs the generator in read-only check mode and fails on stale sections in any language.

The generator produces the C# limits, Swift storage limits, concrete Swift widget types and literal gallery names, bundle groups, root bundle entries, and the Objective-C transparency allowlist. Assignment pages, previews, persistence, rendering and reloads continue using their shared implementations. App guidance reads the slot count from the shared C# rule instead of repeating a number.

Each concrete widget still has a fixed number and an empty initializer. Gallery names remain literal strings: interpolated metadata caused the confirmed 0.2.18 startup assertion. Groups contain at most five widgets, and the root contains Counter plus four groups. Existing slot 1–10 type names, kinds (`ControlStudio.Slot.N`) and family addresses are preserved. No configurable selector is reintroduced.

## Persistence and lifecycle review

Existing library/slot schemas stay unchanged. Reading existing slots does not rewrite or renumber them. The maximum stored assignments increases from 50 to 100 because the five stored enum families include the retired iPad family; the four current iPhone families use 80. Retired addresses remain readable but cannot be newly assigned or published as supported widgets. Both native and managed readers keep the existing 16,384-byte bound. Fully populated catalogs including all retired addresses fit that bound; generated configuration rejects a capacity whose conservative worst-case payload would exceed it. Older versions cannot read new addresses above 10; downgrade is unsupported.

Assignment and Clear still use the existing atomic repository, size guards, confirmation, cancellation and publication logic. The slots page keeps its responsive proportions, action spacing, accessibility descriptions and locked-size assignment page. The existing viewport helper still loads at most six nearby full previews, releases offscreen sources and disposes/reloads on background, departure and size switches; doubling rows does not double the image-source budget. Transparency recognizes exactly the generated 20 Studio kinds, continuing to exclude Counter and unrelated descriptors. Its runtime ABI checks are unchanged.

## Verification

All **951 local automated checks** pass: 273 Core, 121 Infrastructure, 500 App and 57 packaging checks. Managed iOS C# compilation passes with zero warnings/errors. Automated checks cover all 80 current addresses, all 100 stored addresses within the byte bound, invalid slot 21, existing assignments surviving expansion, independent Small/Medium slot 20, publication/reopen/clear, and UI assignment/clear of slot 20 in every family. Generation tests cover one literal registration per number, bundle membership, transparency membership, stale-output rejection without writing, partial groups and invalid configurations. Existing layout, theme, accessibility, cancellation and preview lifecycle tests are also run. Swift tests extend storage decoding and descriptor rewriting to all 20 slots, but have not run locally.

Physical iPhone checks remain necessary: gallery registration of Studio 1–20, Counter, independent small/medium/large/XL assignments at slots 11 and 20, transparency in the new slots, scrolling through 20 cards, large text/VoiceOver, background/return, and existing Home Screen widgets after updating. Native Swift/Xcode tests cannot run on this Windows workstation. The owner subsequently authorized committing, pushing main and pushing the new version tag. IPA generation remains limited to a new version tag; the owner will monitor the build.
