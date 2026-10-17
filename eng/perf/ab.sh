#!/usr/bin/env bash
# SC-005 time gate (M1, M2): the head against R5-0 on the same machine, in the same job, interleaved.
# Usage: eng/perf/ab.sh [--rounds N] [--metrics "M2 M1"] [--results FILE] BASE_TREE HEAD_TREE
# Both trees are built in Release first (eng/perf/prepare-r5-0.sh prepares R5-0). Each round runs every metric once per
# tree, each run in a fresh test process, the base first in odd rounds and the head first in even ones. A run reports the
# fastest of 15 steady-state samples (tests/NetPrints.Testing/PerformanceGate.cs). A tree's statistic is the median of its
# runs; the gate is head / base <= 1.10. Exit 0 within the gate, 1 over it, 2 on a usage or run error.
set -euo pipefail

gate=1.10
rounds=10
metrics="M2 M1"
results=""

usage() { sed -n '2,7p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }

while [ $# -gt 0 ]; do
  case "$1" in
    --rounds) rounds="$2"; shift 2 ;;
    --metrics) metrics="$2"; shift 2 ;;
    --results) results="$2"; shift 2 ;;
    -h|--help) usage ;;
    --*) echo "Unknown option $1"; usage ;;
    *) break ;;
  esac
done
[ $# -eq 2 ] || usage
base="$(cd "$1" && pwd)"
head="$(cd "$2" && pwd)"

declare -A project=([M1]=NetPrints.Editor.UITests [M2]=NetPrints.Editor.Tests)
declare -A method=(
  [M1]=NetPrints.Editor.UITests.Hosting.ShellStartupPerformanceTests.StartingTheShellIsMeasured
  [M2]=NetPrints.Editor.Tests.Graph.GraphScopePerformanceTests.BuildingALargeGraphIsMeasured
)

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
[ -n "$results" ] || results="$work/results.txt"
mkdir -p "$(dirname "$results")"
: > "$results"

for metric in $metrics; do
  [ -n "${project[$metric]:-}" ] || { echo "Unknown metric $metric"; exit 2; }
  for tree in "$base" "$head"; do
    exe="$tree/tests/${project[$metric]}/bin/Release/net10.0/${project[$metric]}"
    [ -x "$exe" ] || { echo "$exe is missing: build ${project[$metric]} in Release first"; exit 2; }
  done
done

cpu="$(grep -m1 '^model name' /proc/cpuinfo 2>/dev/null | sed 's/^[^:]*: *//' || true)"
[ -n "$cpu" ] || cpu="$(uname -m)"
echo "SC-005 A/B: base $base ($(git -C "$base" rev-parse --short HEAD 2>/dev/null || echo '?')), head $head ($(git -C "$head" rev-parse --short HEAD 2>/dev/null || echo '?'))"
echo "CPU: $cpu ($(nproc) cores); rounds: $rounds; metrics: $metrics; gate: head / base <= $gate"

run_one() {
  local name="$1" tree="$2" metric="$3" round="$4"
  local exe="$tree/tests/${project[$metric]}/bin/Release/net10.0/${project[$metric]}"
  local out="$work/$name-$metric-$round.txt" log="$work/$name-$metric-$round.log"
  local start=$SECONDS
  if ! NETPRINTS_PERF_AB_OUTPUT="$out" "$exe" -method "${method[$metric]}" > "$log" 2>&1 || ! grep -q "^$metric " "$out" 2>/dev/null; then
    echo "The $name run of $metric (round $round) failed or recorded nothing:"
    tail -n 40 "$log"
    exit 2
  fi
  local fastest median count warm_runs warm_seconds total
  read -r _ fastest median count warm_runs warm_seconds total < <(grep -m1 "^$metric " "$out")
  echo "$round $name $metric $fastest $median $count $warm_runs $warm_seconds $total" >> "$results"
  LC_ALL=C printf 'round %2d  %-4s %s  fastest %8.2f ms  median %8.2f ms  (%d warm-up runs, process %ds)\n' \
    "$round" "$name" "$metric" "$fastest" "$median" "$warm_runs" "$((SECONDS - start))"
}

for round in $(seq 1 "$rounds"); do
  for metric in $metrics; do
    if [ $((round % 2)) -eq 1 ]; then
      run_one base "$base" "$metric" "$round"
      run_one head "$head" "$metric" "$round"
    else
      run_one head "$head" "$metric" "$round"
      run_one base "$base" "$metric" "$round"
    fi
  done
done

LC_ALL=C awk -v gate="$gate" -v metrics="$metrics" '
  function median(list, n,    sorted, i, j, t) {
    for (i = 1; i <= n; i++) sorted[i] = list[i]
    for (i = 2; i <= n; i++) for (j = i; j > 1 && sorted[j - 1] > sorted[j]; j--) { t = sorted[j]; sorted[j] = sorted[j - 1]; sorted[j - 1] = t }
    return n % 2 ? sorted[(n + 1) / 2] : (sorted[n / 2] + sorted[n / 2 + 1]) / 2
  }
  {
    key = $3 SUBSEP $2
    n[key]++; value[key, n[key]] = $4
    runs[key] = runs[key] sprintf("%s%.2f", n[key] > 1 ? " " : "", $4)
  }
  END {
    failed = 0
    split(metrics, ids, " ")
    for (k = 1; k in ids; k++) {
      m = ids[k]
      for (t = 1; t <= 2; t++) {
        tree = t == 1 ? "base" : "head"; key = m SUBSEP tree
        delete list; for (i = 1; i <= n[key]; i++) list[i] = value[key, i]
        stat[tree] = median(list, n[key])
        printf "%s %s: median %.2f ms of %d runs (fastest sample per run: %s)\n", m, tree, stat[tree], n[key], runs[key]
      }
      ratio = stat["head"] / stat["base"]
      verdict = ratio <= gate ? "PASS" : "FAIL"
      if (ratio > gate) failed = 1
      printf "%s: head / base = %.3f (%+.1f%%), gate %.2f: %s\n", m, ratio, (ratio - 1) * 100, gate, verdict
    }
    exit failed
  }' "$results"
