#!/usr/bin/env bash
# Builds BlastScale for iPhone and (optionally) installs and launches it on a connected device.
#
#   BLASTSCALE_IOS_TEAM_ID=XXXXXXXXXX ./build-ios.sh                 # export + compile + sign
#   BLASTSCALE_IOS_TEAM_ID=XXXXXXXXXX BLASTSCALE_IOS_DEVICE=<udid> ./build-ios.sh   # + install & launch
#
# Optional: BLASTSCALE_SERVER_URL (default: http://<this Mac's LAN IP>:8080 — the phone must be on
# the same Wi-Fi and the backend must be running: `docker compose up` in the repository root).
# Find the device id with: xcrun devicectl list devices
set -euo pipefail

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$(cd "$(dirname "$0")" && pwd)"
TEAM_ID="${BLASTSCALE_IOS_TEAM_ID:?set BLASTSCALE_IOS_TEAM_ID to your Apple developer team id}"
BUNDLE_ID="${BLASTSCALE_IOS_BUNDLE_ID:-com.atakankeser.blastscale}"
LAN_IP="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo 127.0.0.1)"
SERVER_URL="${BLASTSCALE_SERVER_URL:-http://${LAN_IP}:8080}"
DEVICE="${BLASTSCALE_IOS_DEVICE:-}"
DERIVED="$PROJECT/build/derived"

mkdir -p "$PROJECT/build"
echo "==> 1/3 Unity export (server URL baked in: $SERVER_URL)"
BLASTSCALE_SERVER_URL="$SERVER_URL" BLASTSCALE_IOS_TEAM_ID="$TEAM_ID" BLASTSCALE_IOS_BUNDLE_ID="$BUNDLE_ID" \
  "$UNITY" -batchmode -nographics -quit -buildTarget iOS -projectPath "$PROJECT" \
  -executeMethod BlastScale.EditorTools.IosBuild.Build -logFile "$PROJECT/build/unity-ios.log"

# Note: the app's bundle id comes from the Unity player settings (IosBuild.cs); it must NOT be
# forced with PRODUCT_BUNDLE_IDENTIFIER here, or the embedded UnityFramework would get the same id
# and iOS would refuse the install ("DuplicateIdentifier").
echo "==> 2/3 xcodebuild (automatic signing, team $TEAM_ID)"
xcodebuild -project "$PROJECT/build/ios/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
  -sdk iphoneos -destination "generic/platform=iOS" -derivedDataPath "$DERIVED" \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration \
  DEVELOPMENT_TEAM="$TEAM_ID" CODE_SIGN_STYLE=Automatic \
  build 2>&1 | tee "$PROJECT/build/xcodebuild.log" | grep -E "error:|warning: .*signing|BUILD (SUCCEEDED|FAILED)" || true
grep -q "BUILD SUCCEEDED" "$PROJECT/build/xcodebuild.log"

APP="$(find "$DERIVED/Build/Products/Release-iphoneos" -maxdepth 1 -name '*.app' | head -1)"
echo "==> built $APP"

if [ -n "$DEVICE" ]; then
  echo "==> 3/3 install + launch on $DEVICE"
  xcrun devicectl device install app --device "$DEVICE" "$APP"
  xcrun devicectl device process launch --device "$DEVICE" "$BUNDLE_ID"
else
  echo "==> 3/3 skipped (set BLASTSCALE_IOS_DEVICE=<udid> to install on a phone)"
fi
