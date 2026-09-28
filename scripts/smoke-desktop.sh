#!/usr/bin/env bash
#
# Smoke-tests a self-contained NetPrints.Desktop publish (release contract §5, RL-T06): the layout has
# no missing files, the in-repo generator builds, and NetPrints.Desktop --check-project analyzes,
# builds and runs samples/HelloWorld with no DISPLAY, no packages restored, no changes under samples/.
#
#   scripts/smoke-desktop.sh <publish-dir>
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "usage: scripts/smoke-desktop.sh <publish-dir>" >&2
    exit 2
fi

PUBLISH_DIR="$(cd "$1" && pwd)"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

fail() {
    echo "smoke-desktop.sh: FAIL: $1" >&2
    exit 1
}

# --- 1. Layout: a real self-contained, non-single-file publish -----------------------------------

for name in NetPrints.Desktop NetPrints.Core.dll NetPrints.Editor.dll NetPrints.Workspace.dll System.Private.CoreLib.dll; do
    [[ -f "$PUBLISH_DIR/$name" ]] || fail "step 1 (layout): missing $name in $PUBLISH_DIR"
done
[[ -d "$PUBLISH_DIR/BuildHost-netcore" ]] || fail "step 1 (layout): missing BuildHost-netcore/ (roslyn#80127)"

# --- 2. The in-repo generator (Debug: project-system.md §2.1's in-repo SDK default) ---------------

dotnet build "$REPO_ROOT/src/NetPrints.Generator" >&2 || fail "step 2 (generator build): dotnet build failed"

# --- 3. Headless project check + run, no display --------------------------------------------------

OUTPUT="$(env -u DISPLAY -u WAYLAND_DISPLAY "$PUBLISH_DIR/NetPrints.Desktop" --check-project "$REPO_ROOT/samples/HelloWorld/HelloWorld.csproj" --run)" \
    || fail "step 3 (check-project --run): exited non-zero. Output:
$OUTPUT"

echo "$OUTPUT"

echo "$OUTPUT" | grep -Eq '^references: [1-9][0-9]* ' || fail "step 3: no 'references: <n>' line with n > 0"
echo "$OUTPUT" | grep -q 'System.Console: .*packs/Microsoft.NETCore.App.Ref/' || fail "step 3: System.Console reference is not from packs/Microsoft.NETCore.App.Ref/"
echo "$OUTPUT" | grep -q '^analysis: 0 errors' || fail "step 3: analysis reported errors"
echo "$OUTPUT" | grep -q '^build: succeeded' || fail "step 3: build did not succeed"
echo "$OUTPUT" | grep -q 'Hello, World!' || fail "step 3: run output does not contain 'Hello, World!'"
echo "$OUTPUT" | grep -q '^run: exit 0' || fail "step 3: run did not exit 0"

# --- 4. The check regenerated nothing under samples/ -----------------------------------------------

git -C "$REPO_ROOT" diff --exit-code -- samples/ || fail "step 4 (samples/ unchanged): the check modified samples/"

echo "smoke-desktop.sh: all checks passed for $PUBLISH_DIR"
