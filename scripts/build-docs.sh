#!/usr/bin/env bash
#
# Builds the docs site into website/build: Docusaurus pages, the DocFX API reference at /api/ and
# the graph JSON Schema at /schemas/. Used by CI (.github/workflows/docs.yml) and locally, so both
# build the same thing.
#
#   scripts/build-docs.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

mkdir -p website/static/img
cp assets/icons/netprints-icon.png website/static/img/logo.png
cp assets/icons/netprints-icon.png website/static/img/favicon.png

dotnet tool restore
dotnet restore NetPrints.slnx
dotnet docfx docs/api/docfx.json

(cd website && npm ci --silent && npm run build --silent)

mkdir -p website/build/api website/build/schemas
cp -r docs/api/_site/. website/build/api/
cp schemas/*.schema.json website/build/schemas/

test -f website/build/api/index.html
cmp schemas/netpc.v1.schema.json website/build/schemas/netpc.v1.schema.json
cmp schemas/npcat.v1.schema.json website/build/schemas/npcat.v1.schema.json
cmp schemas/netprints.catalog.v1.schema.json website/build/schemas/netprints.catalog.v1.schema.json
