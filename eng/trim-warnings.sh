#!/usr/bin/env bash
# M4 trimming ratchet: counts the IL2xxx/IL3xxx warnings of NetPrints.Editor and its references
# and compares the count with eng/trim-warnings.txt. Exit 1 when the count rises.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
count_file="$root/eng/trim-warnings.txt"
log="$(mktemp)"
trap 'rm -f "$log"' EXIT

dotnet build "$root/src/NetPrints.Editor" -c Release --no-incremental \
  -p:IsTrimmable=true -p:EnableTrimAnalyzer=true \
  -p:TreatWarningsAsErrors=false -p:WarningsAsErrors= \
  -v q -tl:off --nologo -clp:NoSummary > "$log" 2>&1 || { tail -n 40 "$log"; echo "Build failed"; exit 2; }

# MSBuild prints each warning more than once and appends the project path, so de-duplicate on the rest.
warnings="$(grep -E '(warning|error) IL[23][0-9]{3}' "$log" | sed -E 's/ \[[^]]*\]$//' | sort -u || true)"
if [ -z "$warnings" ]; then count=0; else count="$(printf '%s\n' "$warnings" | wc -l)"; fi

echo "Trim warnings (IL2xxx/IL3xxx) by code:"
printf '%s\n' "$warnings" | grep -oE 'IL[23][0-9]{3}' | sort | uniq -c || true
echo "Total: $count"

baseline="$(tr -d '[:space:]' < "$count_file")"
echo "Baseline ($(basename "$count_file")): $baseline"

if [ "$count" -gt "$baseline" ]; then
  printf '%s\n' "$warnings" | sed -E "s#$root/##" | head -n 60
  echo "The count rose from $baseline to $count. Fix the new warnings; the count may only go down."
  exit 1
fi
if [ "$count" -lt "$baseline" ]; then
  echo "The count fell from $baseline to $count. Lower the baseline: echo $count > eng/trim-warnings.txt"
fi
