#!/usr/bin/env bash
# SC-005 time gate (M1, M2): the head against R5-0 on the same machine, in the same job, interleaved.
# Usage: eng/perf/ab.sh [--rounds N] [--metrics "M2 M1"] [--results FILE] BASE_TREE HEAD_TREE
# (--results keeps one line per run: round, tree, metric, fastest ms, median ms, bytes, GC pause ms, samples, warm-up
# runs, warm-up s, total s.)
# Both trees are built in Release first (eng/perf/prepare-r5-0.sh prepares R5-0). Each round runs every metric once per
# tree, each run in a fresh test process, the base first in odd rounds and the head first in even ones. A run reports
# its steady state (tests/NetPrints.Testing/PerformanceGate.cs): the fastest sample, GC pauses left out, and the median
# bytes a sample allocates. A tree's time is the median of its runs' fastest samples, its bytes the median of its runs'.
# The gate is head / base <= 1.10 for the time and for the bytes. Exit 0 within the gate, 1 over it, 2 on an error.
set -euo pipefail

gate=1.10
rounds=10
metrics="M2 M1"
results=""

usage() { sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }

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
echo "CPU: $cpu ($(nproc) cores); rounds: $rounds; metrics: $metrics; gate: head / base <= $gate (time and bytes)"

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
  local fastest median bytes gc count warm_runs warm_seconds total
  read -r _ fastest median bytes gc count warm_runs warm_seconds total < <(grep -m1 "^$metric " "$out")
  echo "$round $name $metric $fastest $median $bytes $gc $count $warm_runs $warm_seconds $total" >> "$results"
  LC_ALL=C printf 'round %2d  %-4s %s  fastest %8.2f ms  median %8.2f ms  %10d bytes  GC %6.2f ms  (%d warm-up runs, process %ds)\n' \
    "$round" "$name" "$metric" "$fastest" "$median" "$bytes" "$gc" "$warm_runs" "$((SECONDS - start))"
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

LC_ALL=C awk -v gate="$gate" -v metrics="$metrics" -v cpu="$cpu" '
  function median(list, n,    sorted, i, j, t) {
    for (i = 1; i <= n; i++) sorted[i] = list[i]
    for (i = 2; i <= n; i++) for (j = i; j > 1 && sorted[j - 1] > sorted[j]; j--) { t = sorted[j]; sorted[j] = sorted[j - 1]; sorted[j - 1] = t }
    return n % 2 ? sorted[(n + 1) / 2] : (sorted[n / 2] + sorted[n / 2 + 1]) / 2
  }
  function judge(m, what, unit, base, head,    ratio) {
    ratio = head / base
    if (ratio > gate) failed = 1
    printf "%s %-5s head / base = %.3f (%+.1f%%; base %.2f %s, head %.2f %s), gate %.2f: %s\n", m, what, ratio, (ratio - 1) * 100, base, unit, head, unit, gate, ratio <= gate ? "PASS" : "FAIL"
  }
  {
    key = $3 SUBSEP $2
    n[key]++; fastest[key, n[key]] = $4; bytes[key, n[key]] = $6; gc[key, n[key]] = $7
    runs[key] = runs[key] sprintf("%s%.2f", n[key] > 1 ? " " : "", $4)
  }
  END {
    failed = 0
    split(metrics, ids, " ")
    for (k = 1; k in ids; k++) {
      m = ids[k]
      for (t = 1; t <= 2; t++) {
        tree = t == 1 ? "base" : "head"; key = m SUBSEP tree
        delete a; delete b; delete c
        for (i = 1; i <= n[key]; i++) { a[i] = fastest[key, i]; b[i] = bytes[key, i]; c[i] = gc[key, i] }
        time[tree] = median(a, n[key]); size[tree] = median(b, n[key]); pause[tree] = median(c, n[key])
        printf "%s %s: %.2f ms (median of %d runs; fastest sample per run: %s), %.0f bytes, GC pause %.2f ms left out\n", m, tree, time[tree], n[key], runs[key], size[tree], pause[tree]
      }
      judge(m, "time", "ms", time["base"], time["head"])
      judge(m, "bytes", "MB", size["base"] / 1e6, size["head"] / 1e6)
    }
    printf "SC-005 A/B on %s: %s\n", cpu, failed ? "FAIL" : "PASS"
    exit failed
  }' "$results"
