#!/usr/bin/env bash
# Writes the NetPrints mark exports from assets/brand/netprints-mark.svg: the PNG sizes in assets/brand/,
# the NuGet icon and the two application icons. Run by hand after the master changes; CI does not run it.
# Needs ImageMagick 6 or 7 (convert or magick) or rsvg-convert.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
svg="$root/assets/brand/netprints-mark.svg"
sizes=(16 24 32 48 64 128 256)
ico_sizes=(16 32 48 256)

if command -v rsvg-convert >/dev/null 2>&1; then
  render() { rsvg-convert -w "$1" -h "$1" "$svg" -o "$2"; }
elif command -v magick >/dev/null 2>&1; then
  render() { magick -background none -density 1536 "$svg" -resize "${1}x${1}" "PNG32:$2"; }
elif command -v convert >/dev/null 2>&1; then
  render() { convert -background none -density 1536 "$svg" -resize "${1}x${1}" "PNG32:$2"; }
else
  echo "export-mark.sh: install rsvg-convert or ImageMagick" >&2
  exit 1
fi

for size in "${sizes[@]}"; do
  render "$size" "$root/assets/brand/netprints-mark-$size.png"
done

frames=()
for size in "${ico_sizes[@]}"; do
  frames+=("$root/assets/brand/netprints-mark-$size.png")
done
if command -v magick >/dev/null 2>&1; then
  magick "${frames[@]}" "$root/src/NetPrints.Desktop/NetPrintsLogo.ico"
else
  convert "${frames[@]}" "$root/src/NetPrints.Desktop/NetPrintsLogo.ico"
fi

cp "$root/src/NetPrints.Desktop/NetPrintsLogo.ico" "$root/src/NetPrints.Editor/Assets/NetPrintsLogo.ico"
cp "$root/assets/brand/netprints-mark-128.png" "$root/assets/icons/netprints-icon.png"
echo "exported ${#sizes[@]} sizes, the .ico and the NuGet icon"
