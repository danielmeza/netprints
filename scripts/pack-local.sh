#!/usr/bin/env bash
#
# Packs every NetPrints package into ./local-packages with a distinct local version, so a restore can
# never silently fall back to (or prefer) a published package.
#
#   scripts/pack-local.sh                   pack and print how to use the packages
#   scripts/pack-local.sh --print-version   pack and print only the version (for scripts and CI)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_VERSION="0.1.0-local.$(date -u +%Y%m%d%H%M%S)"
FEED="$REPO_ROOT/local-packages"
mkdir -p "$FEED"

dotnet pack "$REPO_ROOT/NetPrints.slnx" -c Release -p:MinVerVersionOverride="$LOCAL_VERSION" -o "$FEED" >&2

if [[ "${1:-}" == "--print-version" ]]; then echo "$LOCAL_VERSION"; exit 0; fi

cat <<MSG
Packed NetPrints $LOCAL_VERSION into $FEED

  Tool:     dotnet tool install -g NetPrints.Cli --version $LOCAL_VERSION --add-source "$FEED"
  Project:  <PackageReference Include="NetPrints.Sdk" Version="$LOCAL_VERSION" PrivateAssets="all" />
            (with a nuget.config that lists $FEED)
  Verify:   scripts/verify-packages.sh "$FEED" $LOCAL_VERSION

Nothing is committed: local-packages/ is git-ignored.
MSG
