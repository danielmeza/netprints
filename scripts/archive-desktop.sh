#!/usr/bin/env bash
#
# Archives a self-contained NetPrints.Desktop publish into a single-top-folder release archive
# (release contract §7): a .tar.gz (tar -czf, preserves the executable bit) for linux-x64 and
# osx-arm64, or a .zip (zip -r -X) for win-x64. The archive's only top-level entry is
# NetPrints-<version>-<rid>/, which also gets LICENSE and a short README.txt.
#
#   scripts/archive-desktop.sh <publish-dir> <version> <rid>
set -euo pipefail

if [[ $# -ne 3 ]]; then
    echo "usage: scripts/archive-desktop.sh <publish-dir> <version> <rid>" >&2
    exit 2
fi

PUBLISH_DIR="$(cd "$1" && pwd)"
VERSION="$2"
RID="$3"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

FOLDER="NetPrints-$VERSION-$RID"
OUT_DIR="$REPO_ROOT/artifacts/desktop"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

STAGE="$WORK/$FOLDER"
mkdir -p "$STAGE"
cp -a "$PUBLISH_DIR"/. "$STAGE"/
cp "$REPO_ROOT/LICENSE" "$STAGE/LICENSE"

cat > "$STAGE/README.txt" <<EOF
NetPrints $VERSION ($RID): a self-contained visual scripting editor for .NET.
Requires the .NET 10 SDK to open and build projects.
Docs: https://danielmeza.github.io/netprints/
EOF

mkdir -p "$OUT_DIR"

case "$RID" in
    win-x64)
        ARCHIVE="$OUT_DIR/$FOLDER.zip"
        rm -f "$ARCHIVE"
        (cd "$WORK" && zip -r -X -q "$ARCHIVE" "$FOLDER")
        ;;
    *)
        ARCHIVE="$OUT_DIR/$FOLDER.tar.gz"
        rm -f "$ARCHIVE"
        tar -czf "$ARCHIVE" -C "$WORK" "$FOLDER"
        ;;
esac

echo "archive-desktop.sh: wrote $ARCHIVE"
