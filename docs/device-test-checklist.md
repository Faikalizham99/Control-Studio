# Milestone 0 device verification

**All device results: NOT VERIFIED.** Leave unchecked until the iPhone owner confirms each result. Compilation or IPA structure alone cannot pass these tests.

Record:

- iPhone: iPhone 14 Pro Max
- iOS version and build: NOT VERIFIED (target: iOS 27)
- FlareStore version: NOT VERIFIED
- Workflow URL / commit / artifact SHA-256: NOT VERIFIED
- Signing route (CI / FlareStore re-sign): NOT VERIFIED
- Three profiles cover device, IDs and App Group: NOT VERIFIED
- Installation date and tester: NOT VERIFIED

| Test | Result |
|---|---|
| IPA installs with host and both extensions intact | NOT VERIFIED |
| App opens without a crash or unnecessary permission prompts | NOT VERIFIED |
| Home displays Control Studio and an initial counter | NOT VERIFIED |
| Update Widget increments exactly once per tap | NOT VERIFIED |
| Counter persists across force-close/relaunch and reboot | NOT VERIFIED |
| Shared-storage connected message appears | NOT VERIFIED |
| Settings switches System/Light/Dark and persists after restart | NOT VERIFIED |
| VoiceOver reads counter, button and status; large text remains usable | NOT VERIFIED |
| Control Studio appears in Home Screen widget picker | NOT VERIFIED |
| Small widget displays the shared value and Last Updated | NOT VERIFIED |
| Medium widget displays the same shared value and Last Updated | NOT VERIFIED |
| Updating in app eventually changes both widgets when iOS permits | NOT VERIFIED |
| Shortcuts lists Increment Control Studio Counter under the app | NOT VERIFIED |
| Action runs and returns the incremented integer and meaningful dialog | NOT VERIFIED |
| Reopening/resuming app shows the Shortcuts-updated value | NOT VERIFIED |
| Both widgets eventually reflect the Shortcuts-updated value | NOT VERIFIED |
| Repeated app/Shortcut operations do not lose updates | NOT VERIFIED |
| Airplane Mode does not prevent counter or locally permitted actions | NOT VERIFIED |
| Missing App Group produces an explicit local-only/error state | NOT VERIFIED |
| Device lock/unlock failure is handled without data reset or false success | NOT VERIFIED |

For each failed test, record expected value, actual value, timestamps and visible error. A widget delay alone is not proof of storage failure. Missing discovery must be investigated before accepting Milestone 0C. Attach screenshots/log excerpts only after removing private signing/device data.

**Milestone 0 accepted by owner:** NOT VERIFIED. **Milestone 1 approved:** NO.
