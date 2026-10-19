# Milestone 0 device verification

**App-to-widget sharing confirmed by the owner on 19 October 2026** after the 0.1.1 App Group correction. The owner reported "ITRS WORKING NOW." This confirms the reported widget issue is resolved; individual widget families, exact status text, persistence, accessibility and Shortcuts tests have not been separately confirmed. Earlier screenshot observations refer to the installed 0.1.0 build.

Record:

- iPhone: iPhone 14 Pro Max
- iOS version and build: NOT VERIFIED (target: iOS 27)
- FlareStore version: NOT VERIFIED
- Built artifact: [CI run 39133946833](https://github.com/Faikalizham99/Control-Studio/actions/runs/39133946833), commit `ba282216abbf7cab987170f788034e216152db97`
- Built unsigned IPA SHA-256: `b2b38268f632c00098c25d6b4b38f36bfd23ad904c7e0aa565d4e57de761c3a6`
- Final installed/re-signed IPA hash: NOT VERIFIED
- Corrected 0.1.1 build: [CI run 39136235978](https://github.com/Faikalizham99/Control-Studio/actions/runs/39136235978), commit `cc950e76b4ffcc11ef8a74e470ca34f601896043`
- Corrected unsigned IPA SHA-256: `0ce2c08a950cc2dbd04faa17361bf9e84b6055a854841e230e66f06d63b500f4`
- Corrected configured App Group: `group.23b5a5355f697168.1`; app-to-widget sharing confirmed by owner, final re-signed artifact not inspected
- Signing route (CI / FlareStore re-sign): NOT VERIFIED
- Three profiles cover device, IDs and App Group: NOT VERIFIED
- Installation date and tester: NOT VERIFIED

| Test | Result |
|---|---|
| Overall app-to-widget sharing after the 0.1.1 correction | CONFIRMED by owner, 19 October 2026; individual checks below are not implied |
| IPA installs with host and both extensions intact | NOT VERIFIED |
| App opens without a crash or unnecessary permission prompts | NOT VERIFIED |
| Home displays Control Studio and a counter | OBSERVED in 0.1.0 screenshot: value 3; corrected build NOT VERIFIED |
| Update Widget increments exactly once per tap | NOT VERIFIED |
| Counter persists across force-close/relaunch and reboot | NOT VERIFIED |
| Shared-storage connected message appears | FAILED in 0.1.0: local-only/App Group unavailable; corrected build NOT VERIFIED |
| Settings switches System/Light/Dark and persists after restart | NOT VERIFIED |
| VoiceOver reads counter, button and status; large text remains usable | NOT VERIFIED |
| Control Studio appears in Home Screen widget picker | NOT VERIFIED |
| Small widget displays the shared value and Last Updated | FAILED in 0.1.0: widget renders a storage error; corrected build NOT VERIFIED |
| Medium widget displays the same shared value and Last Updated | FAILED in 0.1.0: widget renders a storage error; corrected build NOT VERIFIED |
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
