# Precise layer position arrows

Included in **0.2.39 (41)** alongside custom Text layers. The owner authorized committing, pushing main and pushing the new version tag, and will monitor the IPA build. Signing, native widget registration and IPA workflow behavior are unchanged.

Clock and Date share a keyboard-style inverted T control directly below **Centre horizontally** in Position. Photo uses the same component below a new **Centre horizontally** action in **Size, position & crop**. The shared precision controls also offer it for text layers.

```text
    [ Up ]
[ Left ][ Down ][ Right ]
```

Each tap moves only the selected layer by one widget point. Horizontal movement divides by the current canvas width; vertical movement divides by its height. Saved/measured device dimensions take precedence over representative family dimensions. Preview zoom and display pixel density do not change the step. Small, Medium, Large and iPhone Extra Large use the same behavior.

The layer's size, crop, rotation, font, date/time settings, opacity and stacking order remain intact. Layers may cross the widget edge, with the existing outer workspace limit retained. Locked layers cannot move. Hidden layers can be positioned while selected, without changing their visibility. Changes update the live preview and become persistent through **Save widget**.

## Review and verification

- UI/UX: 48-point icon targets, eight-point gaps, Up above Left/Down/Right; no setting panel dismissal or automatic reveal. Existing anchor compensation preserves the tapped control's position during layout.
- Appearance/accessibility: existing shared accent glyphs and readable disabled surfaces in both themes; native buttons describe direction and the one-point step. No long-press timer or background repeat action.
- Functionality: movement uses the existing immutable frame transform and lock/workspace safeguards; operation checks reject hidden-panel, busy, inactive, deselected and gesture-time callbacks.
- Persistence: no schema changes; repository tests save/reopen/publish moved Photo, Clock and Date frames and retain slot assignment.
- Lifecycle/regressions: tests cover picker cancellation recovery, app activation, tab coverage, active gestures, locks, unchanged sibling layers, scroll offset and all four size calculations. Packaging checks retain the no-auto-scroll policy.

Automated verification passed: 204 Core, 85 Infrastructure, 362 headless MAUI App and 41 packaging tests (692 total); source consistency and whitespace checks; managed iOS compilation with zero warnings/errors.

Physical iPhone verification remains required: inspect native spacing and tint in both themes and at larger text sizes; use VoiceOver to operate the keys; check tap responsiveness with photo-heavy designs, unchanged page position and final Home Screen placement after saving. Windows verification cannot establish native UIKit rendering or iOS widget delivery.
