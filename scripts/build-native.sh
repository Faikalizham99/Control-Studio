#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p artifacts/native
python3 scripts/generate-xcode-project.py
sdk="$(xcrun --sdk iphoneos --show-sdk-path)"
xcrun --sdk iphoneos swiftc -emit-library -static -parse-as-library -O \
  -module-name ControlStudioBridge -target arm64-apple-ios17.0 -sdk "$sdk" \
  native/Shared/CounterStore.swift native/ControlStudioBridge/CounterBridge.swift \
  -o artifacts/native/libControlStudioBridge.a
# The aggregate build runs Xcode's normal App Intents metadata extraction phase.
xcodebuild -project native/ControlStudioNative.xcodeproj -alltargets \
  -configuration Release -sdk iphoneos ARCHS=arm64 ONLY_ACTIVE_ARCH=NO \
  CODE_SIGNING_ALLOWED=NO SYMROOT="$PWD/artifacts/native" build
test -f artifacts/native/Release-iphoneos/ControlStudioWidgets.appex/Info.plist
test -s artifacts/native/Release-iphoneos/ControlStudioIntents.appex/Metadata.appintents/extract.actionsdata
python3 scripts/validate-intent-metadata.py artifacts/native/Release-iphoneos/ControlStudioIntents.appex
