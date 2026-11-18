# Extra Large slots 21–30

Version **0.2.60 (62)** provides 30 slots for iPhone Extra Large on iOS 27 or later. Small, Medium and Large retain 20 each. The per-widget layer limit remains 30, and the saved-design limit remains 50. Slot assignments can reuse saved designs.

## Shared implementation

`contracts/widget-slot-config.json` holds the standard count and Extra Large count. `scripts/widget_slot_config.py` generates managed/native limits, concrete native widget types, literal gallery names, bundle membership and transparency membership. Source validation checks generated output without writing. Assignment, rendering, preview sizing, saving, clearing and widget links share the existing implementations, with address bounds checked per size.

Studio 1–20 retain their concrete type names, kind identifiers and supported sizes. Studio 21–30 are static, parameterless widget types supporting only the iOS 27 Extra Large portrait family. No configurable selector is introduced. Native registration groups at most five expressions at every level; the root contains Counter and the shared Studio registration group. The Extra Large additions are gated by OS availability, using the conditional support documented by Apple's [WidgetBundleBuilder](https://developer.apple.com/documentation/swiftui/widgetbundlebuilder/buildoptional(_:)). Gallery names remain literal to avoid the earlier interpolated-metadata assertion.

## Persistence and lifecycle

There are 90 active addresses: 20 + 20 + 20 + 30. Storage permits 110 including the 20 retired iPad addresses retained for compatibility. Those retired addresses cannot be assigned or used as current widgets. Library and slot schemas are unchanged. Existing assignments are not renumbered or rewritten just by reading. Catalogs with all 110 addresses fit the existing 16,384-byte budget; the generator rejects capacities exceeding that bound. Versions before this expansion cannot read new Extra Large addresses above 20, so downgrading after assigning them is unsupported.

Full refresh requests include all 30 Studio kinds. Single-slot refresh accepts numbers through 30. Widget links check the size-specific range: Extra Large 30 is valid, Small/Medium/Large 21 and Extra Large 31 are rejected. The transparency allowlist includes all 30 exact Studio kinds, excludes Counter and keeps its existing ABI guards.

The slots page preserves its existing card proportions, action spacing, confirmation, cancellation and accessibility descriptions. Assignment stays locked to the chosen size. The viewport helper continues to limit nearby loaded preview images to six and releases them on departure/background/size changes. More slot rows do not increase the image-source budget or the layer/rendering limits.

## Verification

All **1,158 local automated tests** pass: 342 Core, 135 Infrastructure, 604 App and 77 packaging tests. Source validation passes, and managed iOS C# compilation completes with zero warnings/errors. This compilation does not execute Swift or build an IPA.

Automated coverage checks the full active/stored ranges, existing assignments, bounded serialization, publish/reopen/clear behavior, invalid writes preserving original bytes, links at each size boundary and size-specific UI card counts. UI coverage assigns and clears Extra Large 21 and 30 while preserving existing slot 20 assignments, then switches back to a 20-slot family. Generator coverage checks literal names, one registration/transparency entry per number, OS gating, bounded bundle groups, partial groups and stale output rejection. Native test sources cover storage, links and transparency through slot 30.

Native Swift/Xcode execution and iPhone verification are unavailable on this Windows workstation. Device checks still required: gallery registration of Studio 21–30 as Extra Large only, existing Home Screen widgets after upgrade, assigning/clearing 21 and 30, transparency, widget tap actions, refresh, scrolling all 30 cards, large text/VoiceOver and background/return. Workflows and IPA triggers remain unchanged. The owner subsequently authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build.
