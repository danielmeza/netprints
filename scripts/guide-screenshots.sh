#!/usr/bin/env bash
#
# Regenerates the editor guide screenshots (docs/guide/editor, FR-103): builds the desktop E2E project,
# runs GuideScreenshotTests on a private Xvfb display (the test harness starts it, on the first free
# number from the one chosen here, never :1) and lists the images it wrote to website/static/img/guide/editor.
#
#   scripts/guide-screenshots.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

for tool in Xvfb openbox xdotool import; do
    command -v "$tool" >/dev/null || { echo "guide-screenshots.sh: $tool is required (see docs/contributing/testing.md)" >&2; exit 1; }
done

display=100
while [[ -e "/tmp/.X11-unix/X$display" ]] || pgrep -f "Xvfb :$display( |$)" >/dev/null; do
    display=$((display + 1))
done

dotnet build tests/NetPrints.Desktop.E2ETests -c Release -v q -tl:off --nologo
dotnet build src/NetPrints.Generator -c Release -v q -tl:off --nologo

images=website/static/img/guide/editor
mkdir -p "$images"
stamp="$(mktemp)"
trap 'rm -f "$stamp"' EXIT

NETPRINTS_E2E=1 NETPRINTS_E2E_WORKERS=1 NETPRINTS_GUIDE_SHOTS=1 NETPRINTS_E2E_DISPLAY_START="$display" \
    dotnet test --project tests/NetPrints.Desktop.E2ETests -c Release --no-build --no-progress --no-ansi \
    -- --explicit only --filter-class '*GuideScreenshotTests' --fail-skips on

find "$images" -name '*.png' -newer "$stamp" | sort
