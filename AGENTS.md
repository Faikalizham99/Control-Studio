# Owner rules

- Never commit or push without the owner's explicit permission for that action. This includes branch and tag pushes. Approval to implement, edit, build or test does not authorize committing or pushing.
- IPA generation must run only when a new version tag matching `v*.*.*` is pushed. Regular branch pushes and pull requests may run tests, but must not build or upload an IPA. Do not enable manual IPA workflow dispatch.
- For every change, perform a deep review of UI/UX, functionality, persistence, lifecycle, accessibility and regressions. Fix findings before finishing, and distinguish automated verification from checks that still require a device.
