#!/bin/bash
# Invoked only in the signed CI job. Secrets are environment inputs; shell tracing is never enabled.
set -euo pipefail
umask 077
: "${IOS_CERTIFICATE_P12_BASE64:?}" "${IOS_CERTIFICATE_PASSWORD:?}"
: "${IOS_HOST_PROFILE_BASE64:?}" "${IOS_WIDGET_PROFILE_BASE64:?}" "${IOS_INTENTS_PROFILE_BASE64:?}"
: "${RUNNER_TEMP:?}" "${GITHUB_ENV:?}"
signing_dir="$(mktemp -d "$RUNNER_TEMP/controlstudio-signing.XXXXXX")"
keychain="$signing_dir/build.keychain-db"
keychain_password="$(openssl rand -hex 32)"
export SIGNING_TEMP_DIR="$signing_dir"
python3 - <<'PY'
import base64, os
from pathlib import Path
root = Path(os.environ['SIGNING_TEMP_DIR'])
for key, name in [('IOS_CERTIFICATE_P12_BASE64', 'certificate.p12'),
                  ('IOS_HOST_PROFILE_BASE64', 'host.mobileprovision'),
                  ('IOS_WIDGET_PROFILE_BASE64', 'widgets.mobileprovision'),
                  ('IOS_INTENTS_PROFILE_BASE64', 'intents.mobileprovision')]:
    (root / name).write_bytes(base64.b64decode(os.environ[key], validate=True))
PY
security create-keychain -p "$keychain_password" "$keychain"
# Write cleanup coordinates before importing secrets, so cleanup also runs if import fails.
echo "SIGNING_TEMP_DIR=$signing_dir" >> "$GITHUB_ENV"
echo "SIGNING_KEYCHAIN=$keychain" >> "$GITHUB_ENV"
security set-keychain-settings -lut 21600 "$keychain"
security unlock-keychain -p "$keychain_password" "$keychain"
security import "$signing_dir/certificate.p12" -k "$keychain" -P "$IOS_CERTIFICATE_PASSWORD" -T /usr/bin/codesign > /dev/null
security set-key-partition-list -S apple-tool:,apple:,codesign: -k "$keychain_password" "$keychain" > /dev/null
identity="$(security find-identity -v -p codesigning "$keychain" | sed -nE 's/^[[:space:]]*[0-9]+\) ([0-9A-F]{40}) .*/\1/p')"
[[ "$identity" =~ ^[0-9A-F]{40}$ ]] || { echo "Exactly one valid code-signing identity is required" >&2; exit 1; }
echo "SIGNING_IDENTITY=$identity" >> "$GITHUB_ENV"
echo "SIGNING_PROFILES_DIR=$signing_dir" >> "$GITHUB_ENV"
