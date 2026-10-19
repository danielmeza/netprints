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
    "NetPrints.Extensibility.$VERSION.nupkg"
    "NetPrints.Extensibility.$VERSION.snupkg"
    "NetPrints.Extensibility.Abstractions.$VERSION.nupkg"
    "NetPrints.Extensibility.Abstractions.$VERSION.snupkg"
    "NetPrints.Extensibility.Testing.$VERSION.nupkg"
    "NetPrints.Extensibility.Testing.$VERSION.snupkg"
    "NetPrints.Extensibility.Testing.Xunit.$VERSION.nupkg"
    "NetPrints.Extensibility.Testing.Xunit.$VERSION.snupkg"
    "NetPrints.Reflection.$VERSION.nupkg"
    "NetPrints.Reflection.$VERSION.snupkg"
    "NetPrints.Sdk.$VERSION.nupkg"
    "NetPrints.Serialization.$VERSION.nupkg"
    "NetPrints.Serialization.$VERSION.snupkg"
    "NetPrints.Templates.$VERSION.nupkg"
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
    # Fenced code blocks may hold angle brackets (an XML install snippet); only prose is checked.
    local prose
    prose="$(awk '/^```/ { fenced = !fenced; next } !fenced' "$EXTRACT/README.md")"
    grep -Eq '<[A-Za-z/]' <<<"$prose" && fail "step 2 ($id): packed README.md still has an HTML tag"
    grep -Poq '\]\((?!https?://|#)' <<<"$prose" && fail "step 2 ($id): packed README.md still has a relative link or image"

    # The README the package ships: its own for NetPrints.Annotations and NetPrints.Catalog, else the root one.
    local heading
    heading="$(head -n 1 "$EXTRACT/README.md")"
    case "$id" in
        NetPrints.Annotations | NetPrints.Catalog | NetPrints.Serialization | NetPrints.Extensibility | NetPrints.Extensibility.Abstractions | NetPrints.Extensibility.Testing | NetPrints.Extensibility.Testing.Xunit | NetPrints.Templates)
            [[ "$heading" == "# $id" ]] || fail "step 2 ($id): packed README.md starts with '$heading', expected '# $id' (src/$id/README.md)"
            ;;
        *)
            grep -qxF '## Project layout' "$EXTRACT/README.md" \
                || fail "step 2 ($id): packed README.md is not the root README"
            ;;
    esac
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
[[ -f "$EXTRACT/tools/net10.0/NetPrints.Catalog.dll" ]] || fail "step 2 (NetPrints.Sdk): missing tools/net10.0/NetPrints.Catalog.dll (the generator host carries the catalog runtime so extensions share it)"
[[ -d "$EXTRACT/lib" ]] && fail "step 2 (NetPrints.Sdk): package has a lib/ folder (should have none)"

extract_nupkg NetPrints.Catalog
check_common_metadata NetPrints.Catalog
[[ -f "$EXTRACT/lib/net10.0/NetPrints.Catalog.dll" ]] || fail "step 2 (NetPrints.Catalog): missing lib/net10.0/NetPrints.Catalog.dll"
[[ -f "$EXTRACT/lib/net10.0/NetPrints.Catalog.xml" ]] || fail "step 2 (NetPrints.Catalog): missing lib/net10.0/NetPrints.Catalog.xml"

for id in NetPrints.Serialization NetPrints.Extensibility.Abstractions NetPrints.Extensibility NetPrints.Extensibility.Testing NetPrints.Extensibility.Testing.Xunit; do
    extract_nupkg "$id"
    check_common_metadata "$id"
    [[ -f "$EXTRACT/lib/net10.0/$id.dll" ]] || fail "step 2 ($id): missing lib/net10.0/$id.dll"
    [[ -f "$EXTRACT/lib/net10.0/$id.xml" ]] || fail "step 2 ($id): missing lib/net10.0/$id.xml (XML docs on every public member)"
done

# research R12: the analyzers of Core reach an author who references only Abstractions, so the dependency excludes nothing.
extract_nupkg NetPrints.Extensibility.Abstractions
CORE_DEPENDENCY="$(grep -o '<dependency id="NetPrints.Core"[^>]*>' "$EXTRACT/NetPrints.Extensibility.Abstractions.nuspec" | head -n 1)"
[[ -n "$CORE_DEPENDENCY" ]] || fail "step 2 (NetPrints.Extensibility.Abstractions): nuspec has no NetPrints.Core dependency"
grep -q 'exclude=' <<<"$CORE_DEPENDENCY" && fail "step 2 (NetPrints.Extensibility.Abstractions): the NetPrints.Core dependency excludes assets ($CORE_DEPENDENCY)"
extract_nupkg NetPrints.Core
[[ -f "$EXTRACT/analyzers/dotnet/cs/NetPrints.Authoring.dll" ]] || fail "step 2 (NetPrints.Core): missing analyzers/dotnet/cs/NetPrints.Authoring.dll"
[[ -f "$EXTRACT/buildTransitive/NetPrints.Core.props" ]] || fail "step 2 (NetPrints.Core): missing buildTransitive/NetPrints.Core.props"
grep -q 'CompilerVisibleProperty Include="NetPrintsExtensionId"' "$EXTRACT/buildTransitive/NetPrints.Core.props" || fail "step 2 (NetPrints.Core): buildTransitive/NetPrints.Core.props does not declare the compiler-visible property NetPrintsExtensionId"

extract_nupkg NetPrints.Annotations
check_common_metadata NetPrints.Annotations
grep -q '<developmentDependency>true</developmentDependency>' "$EXTRACT/NetPrints.Annotations.nuspec" || fail "step 2 (NetPrints.Annotations): nuspec has no <developmentDependency>true</developmentDependency>"
[[ -f "$EXTRACT/analyzers/dotnet/cs/NetPrints.Annotations.dll" ]] || fail "step 2 (NetPrints.Annotations): missing analyzers/dotnet/cs/NetPrints.Annotations.dll"
[[ -f "$EXTRACT/build/NetPrints.Annotations.targets" ]] || fail "step 2 (NetPrints.Annotations): missing build/NetPrints.Annotations.targets"
grep -q 'NetPrintsReferenceDocumentation' "$EXTRACT/build/NetPrints.Annotations.targets" || fail "step 2 (NetPrints.Annotations): build/NetPrints.Annotations.targets does not declare NetPrintsReferenceDocumentation"
[[ -d "$EXTRACT/lib" ]] && fail "step 2 (NetPrints.Annotations): package has a lib/ folder (should have none)"

# --- Step 3: tool install from the feed ---------------------------------------------------------

TOOL_PATH="$WORK/tools"
dotnet tool install NetPrints.Cli --tool-path "$TOOL_PATH" --version "$VERSION" --add-source "$FEED" >&2 \
    || fail "step 3 (tool install): dotnet tool install failed"

TOOL_RC=0
TOOL_OUTPUT="$("$TOOL_PATH/netprints" --version 2>&1)" || TOOL_RC=$?
[[ $TOOL_RC -eq 0 ]] || fail "step 3 (tool --version): exit code $TOOL_RC, expected 0: $TOOL_OUTPUT"
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

# The packed tool renders like the packed SDK generator (same version), against a PackageReference project.
CHECK_RC=0
CHECK_OUTPUT="$("$TOOL_PATH/netprints" regen --check "$APP" 2>&1)" || CHECK_RC=$?
[[ $CHECK_RC -eq 0 ]] || fail "step 4 (netprints regen --check): exit code $CHECK_RC, expected 0: $CHECK_OUTPUT"

RUN_RC=0
RUN_OUTPUT="$(dotnet run --project "$APP" -c Release --no-build 2>&1)" || RUN_RC=$?
[[ $RUN_RC -eq 0 ]] || fail "step 4 (dotnet run): exit code $RUN_RC, expected 0: $RUN_OUTPUT"
echo "$RUN_OUTPUT" | grep -qF "Hello, World!" || fail "step 4 (dotnet run): output does not contain 'Hello, World!': $RUN_OUTPUT"

TOOL_RUN_RC=0
TOOL_RUN_OUTPUT="$("$TOOL_PATH/netprints" run "$APP/HelloWorld.csproj" 2>&1)" || TOOL_RUN_RC=$?
[[ $TOOL_RUN_RC -eq 0 ]] || fail "step 4 (netprints run): exit code $TOOL_RUN_RC, expected 0: $TOOL_RUN_OUTPUT"
echo "$TOOL_RUN_OUTPUT" | grep -qF "Hello, World!" || fail "step 4 (netprints run): output does not contain 'Hello, World!': $TOOL_RUN_OUTPUT"

# --- Step 5: an extension author's view (research R12, FR-020, SC-003) -----------------------------

write_nuget_config() {
    cat > "$1/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-packages" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF
}

CONSUMER="$WORK/consumer"
mkdir -p "$CONSUMER"
write_nuget_config "$CONSUMER"
cat > "$CONSUMER/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NetPrints.Extensibility.Abstractions" Version="$VERSION" />
  </ItemGroup>
</Project>
EOF
cat > "$CONSUMER/Extension.cs" <<'EOF'
using NetPrints.Extensibility;

namespace Consumer;

public sealed class Extension : INetPrintsExtension
{
    public void Register(IExtensionBuilder builder)
    {
    }
}
EOF

CONSUMER_LOG="$WORK/consumer.log"
dotnet build "$CONSUMER" -c Release -v:d -p:ReportAnalyzer=true > "$CONSUMER_LOG" 2>&1 || { tail -n 40 "$CONSUMER_LOG" >&2; fail "step 5 (scratch consumer): dotnet build failed"; }
COMPILER_LINE="$(grep -m 1 -e '/analyzer:[^ ]*NetPrints\.Authoring\.dll' "$CONSUMER_LOG" || true)"
[[ -n "$COMPILER_LINE" ]] || fail "step 5 (analyzers): the compiler command line has no NetPrints.Authoring.dll"
AUTHORING_COUNT="$(grep -o -e '/analyzer:[^ ]*NetPrints\.Authoring\.dll' <<<"$COMPILER_LINE" | wc -l)"
[[ "$AUTHORING_COUNT" -eq 1 ]] || fail "step 5 (analyzers): a consumer of NetPrints.Extensibility.Abstractions alone gets NetPrints.Authoring.dll $AUTHORING_COUNT times, expected exactly 1"

# tests/NetPrints.TestExtension builds from the packed packages only (the project switches its references on this property).
TEST_EXTENSION_OUT="$WORK/test-extension"
dotnet build "$REPO_ROOT/tests/NetPrints.TestExtension/NetPrints.TestExtension.csproj" -c Release \
    -p:NetPrintsPackagedExtensionVersion="$VERSION" \
    -p:RestoreSources="$FEED;https://api.nuget.org/v3/index.json" \
    -p:RestorePackagesPath="$NUGET_PACKAGES" \
    -p:OutputPath="$TEST_EXTENSION_OUT/" >&2 \
    || fail "step 5 (NetPrints.TestExtension): build from the local packages failed"
[[ -f "$TEST_EXTENSION_OUT/NetPrints.TestExtension.dll" ]] || fail "step 5 (NetPrints.TestExtension): NetPrints.TestExtension.dll was not built"
[[ -f "$TEST_EXTENSION_OUT/netprints-extension.json" ]] || fail "step 5 (NetPrints.TestExtension): netprints-extension.json was not copied"

# --- Step 6: the netprints-extension template (FR-079, SC-011, research R25 and R17) ----------------------

extract_nupkg NetPrints.Templates
check_common_metadata NetPrints.Templates
grep -q '<packageType name="Template" />' "$EXTRACT/NetPrints.Templates.nuspec" || fail "step 6 (NetPrints.Templates): nuspec has no Template packageType"
[[ -f "$EXTRACT/content/netprints-extension/.template.config/template.json" ]] || fail "step 6 (NetPrints.Templates): missing the netprints-extension template.json"
[[ -d "$EXTRACT/lib" ]] && fail "step 6 (NetPrints.Templates): package has a lib/ folder (should have none)"

HIVE="$WORK/hive"
dotnet new install "$FEED/NetPrints.Templates.$VERSION.nupkg" --debug:custom-hive "$HIVE" >&2 || fail "step 6 (template): dotnet new install failed"

AUTHOR="$WORK/author"
mkdir -p "$AUTHOR"
dotnet new netprints-extension -n Acme.Widgets -o "$AUTHOR" --ExtensionId acme.widgets --NetPrintsVersion "$VERSION" --debug:custom-hive "$HIVE" >&2 \
    || fail "step 6 (template): dotnet new netprints-extension failed"
write_nuget_config "$AUTHOR"

dotnet build "$AUTHOR/Acme.Widgets.Tests" -c Release >&2 || fail "step 6 (template): the generated solution does not build"
TEMPLATE_TEST_OUTPUT="$(cd "$AUTHOR/Acme.Widgets.Tests" && dotnet test -c Release --no-build --no-progress --no-ansi 2>&1)" \
    || { echo "$TEMPLATE_TEST_OUTPUT" >&2; fail "step 6 (template): the generated kit tests failed"; }
grep -Eq 'Passed|passed' <<<"$TEMPLATE_TEST_OUTPUT" || fail "step 6 (template): no kit test passed: $TEMPLATE_TEST_OUTPUT"
grep -Eq 'skipped: *[1-9]|Skipped *: *[1-9]' <<<"$TEMPLATE_TEST_OUTPUT" && fail "step 6 (template): a kit check was skipped in the generated project: $TEMPLATE_TEST_OUTPUT"

PACK_OUT="$WORK/author-pack"
dotnet pack "$AUTHOR/Acme.Widgets" -c Release -o "$PACK_OUT" >&2 || fail "step 6 (template): dotnet pack of the generated extension failed"
PACKAGE="$(find "$PACK_OUT" -maxdepth 1 -name '*.nupkg' | head -n 1)"
[[ -n "$PACKAGE" ]] || fail "step 6 (template): dotnet pack produced no .nupkg"
EXT_EXTRACT="$WORK/author-extract"
mkdir -p "$EXT_EXTRACT"
unzip -qq "$PACKAGE" -d "$EXT_EXTRACT"
grep -q '<packageType name="NetPrintsExtension" />' "$EXT_EXTRACT"/*.nuspec || fail "step 6 (extension package): nuspec has no NetPrintsExtension packageType"
[[ -f "$EXT_EXTRACT/extension/netprints-extension.json" ]] || fail "step 6 (extension package): missing extension/netprints-extension.json"
[[ -f "$EXT_EXTRACT/extension/Acme.Widgets.dll" ]] || fail "step 6 (extension package): missing extension/Acme.Widgets.dll"
[[ ! -e "$EXT_EXTRACT/extension/NetPrints.Core.dll" ]] || fail "step 6 (extension package): the extension folder holds a host assembly"
[[ ! -d "$EXT_EXTRACT/lib" ]] || fail "step 6 (extension package): the package has a lib/ folder"

echo "verify-packages.sh: all checks passed for $VERSION"
