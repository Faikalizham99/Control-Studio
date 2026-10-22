#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mode="${1:-unsigned}"
case "$mode" in signed|unsigned) ;; *) echo "Expected signed or unsigned" >&2; exit 1 ;; esac
app="src/ControlStudio.App/bin/Release/net10.0-ios27.0/ios-arm64/ControlStudio.app"
test -d "$app"
mkdir -p artifacts/package/Payload artifacts/ipa
test ! -e artifacts/package/Payload/ControlStudio.app
ditto "$app" artifacts/package/Payload/ControlStudio.app
staged="$PWD/artifacts/package/Payload/ControlStudio.app"
if [[ "$mode" == signed ]]; then
  : "${SIGNING_PROFILES_DIR:?}" "${SIGNING_IDENTITY:?}" "${SIGNING_KEYCHAIN:?}"
  python3 scripts/sign-bundle.py "$staged" --profiles "$SIGNING_PROFILES_DIR" \
    --identity "$SIGNING_IDENTITY" --keychain "$SIGNING_KEYCHAIN"
fi
python3 scripts/validate_ipa.py "$staged" --mode "$mode"
output="$PWD/artifacts/ipa/ControlStudio-m1-$mode.ipa"
(cd artifacts/package && ditto -c -k --keepParent Payload "$output")
python3 scripts/validate_ipa.py "$output" --mode "$mode" --report "artifacts/ipa/validation-$mode.json"
