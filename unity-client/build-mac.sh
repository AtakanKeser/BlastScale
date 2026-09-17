#!/usr/bin/env bash
# Builds BlastScale as a double-clickable macOS app and opens it.
#
#   ./build-mac.sh            # build unity-client/build/mac/BlastScale.app and launch it
#   ./build-mac.sh --no-open  # build only
#
# Close the Unity editor first: batch mode cannot open a project the editor already holds.
# For online play start the backend in the repository root (docker compose up); the offline demo
# needs nothing.
set -euo pipefail

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$(cd "$(dirname "$0")" && pwd)"
APP="$PROJECT/build/mac/BlastScale.app"

mkdir -p "$PROJECT/build"
echo "==> Building $APP"
"$UNITY" -batchmode -nographics -quit -buildTarget StandaloneOSX -projectPath "$PROJECT" \
  -executeMethod BlastScale.EditorTools.MacBuild.Build -logFile "$PROJECT/build/unity-mac.log"

echo "==> Built $APP"
if [ "${1:-}" != "--no-open" ]; then
  open "$APP"
fi
