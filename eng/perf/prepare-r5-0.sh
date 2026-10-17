#!/usr/bin/env bash
# Prepares R5-0 (d7ddde50, the SC-005 baseline commit) for eng/perf/ab.sh: a detached git worktree at DIR with this
# tree's A/B harness (tests/NetPrints.Testing/PerformanceGate.cs, copied) and eng/perf/r5-0.patch (the M1 and M2 tests
# of R5-0 calling it) applied, and the two test projects built in Release. DIR is reset if it is already that worktree.
# Usage: eng/perf/prepare-r5-0.sh DIR   (remove it afterwards with: git worktree remove --force DIR)
set -euo pipefail

r50=d7ddde50832d235cc242475cfa9fc48e74868900
root="$(cd "$(dirname "$0")/../.." && pwd)"
[ $# -eq 1 ] || { sed -n '2,5p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }
dir="$1"

if [ ! -e "$dir" ]; then
  git -C "$root" worktree add --detach "$dir" "$r50"
fi
[ "$(git -C "$dir" rev-parse HEAD)" = "$r50" ] || { echo "$dir is not a worktree at R5-0 ($r50)"; exit 2; }
git -C "$dir" reset -q --hard "$r50"
git -C "$dir" apply "$root/eng/perf/r5-0.patch"
cp "$root/tests/NetPrints.Testing/PerformanceGate.cs" "$dir/tests/NetPrints.Testing/PerformanceGate.cs"

for project in NetPrints.Editor.Tests NetPrints.Editor.UITests; do
  dotnet build "$dir/tests/$project" -c Release -v q -tl:off --nologo
done
