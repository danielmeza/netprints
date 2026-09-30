#!/usr/bin/env bash
#
# Verifies the packages in a local feed against release contract §4 steps 1-4 (RL-T01..RL-T05):
# the exact file list, each .nupkg's metadata and contents, a global tool install, and an SDK build
# from the feed. Works entirely in a mktemp -d directory with an isolated NUGET_PACKAGES cache and
# never touches the repository. Exits non-zero on the first failure with a message naming the check.
#
#   scripts/verify-packages.sh <feed> <version>
set -euo pipefail

if [[ $# -ne 2 ]]; then
    echo "usage: scripts/verify-packages.sh <feed> <version>" >&2
    exit 2
fi

FEED="$(cd "$1" && pwd)"
VERSION="$2"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

fail() {
    echo "verify-packages.sh: FAIL: $1" >&2
    exit 1
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export NUGET_PACKAGES="$WORK/nuget"
mkdir -p "$NUGET_PACKAGES"

# --- Step 1: the exact file list of §3 for <version> -------------------------------------------

EXPECTED_FILES=(
    "NetPrints.Annotations.$VERSION.nupkg"
    "NetPrints.Catalog.$VERSION.nupkg"
    "NetPrints.Catalog.$VERSION.snupkg"
    "NetPrints.Cli.$VERSION.nupkg"
    "NetPrints.Cli.$VERSION.snupkg"
    "NetPrints.Core.$VERSION.nupkg"
    "NetPrints.Core.$VERSION.snupkg"
    "NetPrints.Reflection.$VERSION.nupkg"
    "NetPrints.Reflection.$VERSION.snupkg"
    "NetPrints.Sdk.$VERSION.nupkg"
)

for name in "${EXPECTED_FILES[@]}"; do
    [[ -f "$FEED/$name" ]] || fail "step 1 (file list): missing $name in $FEED"
done

ACTUAL_COUNT=$(find "$FEED" -maxdepth 1 \( -name "*.${VERSION}.nupkg" -o -name "*.${VERSION}.snupkg" \) | wc -l)
[[ "$ACTUAL_COUNT" -eq "${#EXPECTED_FILES[@]}" ]] || fail "step 1 (file list): $FEED has $ACTUAL_COUNT package file(s), expected ${#EXPECTED_FILES[@]}"

# --- Step 2: each .nupkg's nuspec and contents --------------------------------------------------

EXTRACT="$WORK/extract"

extract_nupkg() {
    local id="$1"
    rm -rf "$EXTRACT"
    mkdir -p "$EXTRACT"
    unzip -qq "$FEED/$id.$VERSION.nupkg" -d "$EXTRACT"
}

check_common_metadata() {
    local id="$1"
    local nuspec="$EXTRACT/$id.nuspec"
    [[ -f "$nuspec" ]] || fail "step 2 ($id): $id.nuspec missing"

    grep -q '<license type="expression">MIT</license>' "$nuspec" || fail "step 2 ($id): nuspec has no MIT license expression"
    grep -q '<icon>icon.png</icon>' "$nuspec" || fail "step 2 ($id): nuspec has no <icon>icon.png</icon>"
    grep -q '<readme>README.md</readme>' "$nuspec" || fail "step 2 ($id): nuspec has no <readme>README.md</readme>"
    grep -Eq '<repository type="git" url="https://github.com/danielmeza/netprints" [^>]*commit="[^"]+"' "$nuspec" \
        || fail "step 2 ($id): nuspec has no <repository> with a commit"

    [[ -f "$EXTRACT/icon.png" ]] || fail "step 2 ($id): package has no icon.png"
    [[ -f "$EXTRACT/README.md" ]] || fail "step 2 ($id): package has no README.md"
    grep -Eq '<[A-Za-z/]' "$EXTRACT/README.md" && fail "step 2 ($id): packed README.md still has an HTML tag"
    grep -Poq '\]\((?!https?://|#)' "$EXTRACT/README.md" && fail "step 2 ($id): packed README.md still has a relative link or image"
    return 0
}

extract_nupkg NetPrints.Core
check_common_metadata NetPrints.Core
[[ -f "$EXTRACT/lib/net10.0/NetPrints.Core.dll" ]] || fail "step 2 (NetPrints.Core): missing lib/net10.0/NetPrints.Core.dll"
[[ -f "$EXTRACT/lib/net10.0/NetPrints.Core.xml" ]] || fail "step 2 (NetPrints.Core): missing lib/net10.0/NetPrints.Core.xml"

extract_nupkg NetPrints.Reflection
check_common_metadata NetPrints.Reflection
[[ -f "$EXTRACT/lib/net10.0/NetPrints.Reflection.dll" ]] || fail "step 2 (NetPrints.Reflection): missing lib/net10.0/NetPrints.Reflection.dll"
[[ -f "$EXTRACT/lib/net10.0/NetPrints.Reflection.xml" ]] || fail "step 2 (NetPrints.Reflection): missing lib/net10.0/NetPrints.Reflection.xml"

extract_nupkg NetPrints.Cli
check_common_metadata NetPrints.Cli
grep -q '<packageType name="DotnetTool" />' "$EXTRACT/NetPrints.Cli.nuspec" || fail "step 2 (NetPrints.Cli): nuspec has no DotnetTool packageType"
[[ -d "$EXTRACT/tools/net10.0/any/BuildHost-netcore" ]] || fail "step 2 (NetPrints.Cli): missing tools/net10.0/any/BuildHost-netcore/"

extract_nupkg NetPrints.Sdk
check_common_metadata NetPrints.Sdk
grep -q '<developmentDependency>true</developmentDependency>' "$EXTRACT/NetPrints.Sdk.nuspec" || fail "step 2 (NetPrints.Sdk): nuspec has no <developmentDependency>true</developmentDependency>"
[[ -f "$EXTRACT/build/NetPrints.Sdk.props" ]] || fail "step 2 (NetPrints.Sdk): missing build/NetPrints.Sdk.props"
[[ -f "$EXTRACT/build/NetPrints.Sdk.targets" ]] || fail "step 2 (NetPrints.Sdk): missing build/NetPrints.Sdk.targets"
[[ -f "$EXTRACT/tools/net10.0/NetPrints.Generator.dll" ]] || fail "step 2 (NetPrints.Sdk): missing tools/net10.0/NetPrints.Generator.dll"
[[ -d "$EXTRACT/lib" ]] && fail "step 2 (NetPrints.Sdk): package has a lib/ folder (should have none)"

extract_nupkg NetPrints.Catalog
check_common_metadata NetPrints.Catalog
[[ -f "$EXTRACT/lib/net10.0/NetPrints.Catalog.dll" ]] || fail "step 2 (NetPrints.Catalog): missing lib/net10.0/NetPrints.Catalog.dll"
[[ -f "$EXTRACT/lib/net10.0/NetPrints.Catalog.xml" ]] || fail "step 2 (NetPrints.Catalog): missing lib/net10.0/NetPrints.Catalog.xml"

extract_nupkg NetPrints.Annotations
check_common_metadata NetPrints.Annotations
grep -q '<developmentDependency>true</developmentDependency>' "$EXTRACT/NetPrints.Annotations.nuspec" || fail "step 2 (NetPrints.Annotations): nuspec has no <developmentDependency>true</developmentDependency>"
[[ -f "$EXTRACT/analyzers/dotnet/cs/NetPrints.Annotations.dll" ]] || fail "step 2 (NetPrints.Annotations): missing analyzers/dotnet/cs/NetPrints.Annotations.dll"
[[ -d "$EXTRACT/lib" ]] && fail "step 2 (NetPrints.Annotations): package has a lib/ folder (should have none)"

# --- Step 3: tool install from the feed ---------------------------------------------------------

TOOL_PATH="$WORK/tools"
dotnet tool install NetPrints.Cli --tool-path "$TOOL_PATH" --version "$VERSION" --add-source "$FEED" >&2 \
    || fail "step 3 (tool install): dotnet tool install failed"

TOOL_OUTPUT="$("$TOOL_PATH/netprints" --version 2>&1 || true)"
echo "$TOOL_OUTPUT" | grep -qF "$VERSION" || fail "step 3 (tool --version): output does not contain $VERSION: $TOOL_OUTPUT"

# --- Step 4: SDK build from the feed -------------------------------------------------------------

APP="$WORK/app"
mkdir -p "$APP"
cp "$REPO_ROOT/samples/HelloWorld/HelloWorld.csproj" "$APP/HelloWorld.csproj"
cp "$REPO_ROOT/samples/HelloWorld/HelloWorld.Program.netpc.json" "$APP/HelloWorld.Program.netpc.json"

# Drop the NetPrintsUseLocalSdk condition (this copy is not under samples/, so it never sees
# samples/Directory.Build.props) and point the PackageReference at the packed local version.
sed -i \
    -e 's/ Condition="'"'"'\$(NetPrintsUseLocalSdk)'"'"' != '"'"'true'"'"'"//' \
    -e "s/<PackageReference Include=\"NetPrints.Sdk\" Version=\"[^\"]*\"/<PackageReference Include=\"NetPrints.Sdk\" Version=\"$VERSION\"/" \
    "$APP/HelloWorld.csproj"

cat > "$APP/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-packages" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

dotnet build "$APP" -c Release >&2 || fail "step 4 (SDK build): dotnet build failed"

COMMITTED="$REPO_ROOT/samples/HelloWorld/HelloWorld.Program.netpc.g.cs"
GENERATED="$APP/HelloWorld.Program.netpc.g.cs"
[[ -f "$GENERATED" ]] || fail "step 4 (SDK build): $GENERATED was not generated"
cmp -s "$COMMITTED" "$GENERATED" || fail "step 4 (SDK build): generated .netpc.g.cs differs from the committed one"

RUN_OUTPUT="$(dotnet run --project "$APP" -c Release --no-build 2>&1 || true)"
echo "$RUN_OUTPUT" | grep -qF "Hello, World!" || fail "step 4 (dotnet run): output does not contain 'Hello, World!': $RUN_OUTPUT"

TOOL_RUN_OUTPUT="$("$TOOL_PATH/netprints" -p "$APP/HelloWorld.csproj" -r 2>&1 || true)"
echo "$TOOL_RUN_OUTPUT" | grep -qF "Hello, World!" || fail "step 4 (netprints -r): output does not contain 'Hello, World!': $TOOL_RUN_OUTPUT"

echo "verify-packages.sh: all checks passed for $VERSION"
