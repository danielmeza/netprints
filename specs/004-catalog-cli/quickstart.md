# Quickstart: validating P2 end to end

Runnable checks that prove each user story works. Linux, .NET 10 SDK, git, `mise` (for schema validation).
Commands run from the repository root; `NP` stands for the built CLI:
`dotnet src/NetPrints.Cli/bin/Release/net10.0/NetPrints.Cli.dll`. The sample imports the generator from
`bin/$(Configuration)`, so `run` needs `Configuration=Release` (as in CI). Contracts hold the details; this file only
lists what to run and what to see.

## 0. Build

```bash
dotnet build NetPrints.slnx -c Release -v q -tl:off --nologo
dotnet test --solution NetPrints.slnx -c Release --no-build --no-progress --no-ansi -- --ignore-exit-code 8
```

Expected: 0 warnings; all tests pass (Desktop E2E self-skip without `NETPRINTS_E2E=1`).

## 1. CLI (US1, contracts/cli.md)

```bash
$NP --version; echo "rc=$?"            # NetPrints.Cli <version>, rc=0
$NP --help >/dev/null; echo "rc=$?"    # rc=0
$NP build --help >/dev/null; echo "rc=$?"
$NP -p samples/HelloWorld/HelloWorld.csproj; echo "rc=$?"   # replacement message, rc=2
Configuration=Release $NP run samples/HelloWorld/HelloWorld.csproj; echo "rc=$?"  # Hello, World!, rc=0
$NP regen --check samples/HelloWorld; echo "rc=$?"          # rc=0, nothing written
$NP migrate samples; echo "rc=$?"                           # "No migrations are available…", rc=0
git status --short samples/                                  # empty
```

## 2. Graph checks and git (US4, contracts/git.md)

```bash
$NP format --check samples; echo "rc=$?"                     # rc=0
tmp=$(mktemp -d); cp -r samples/HelloWorld "$tmp/"; cd "$tmp/HelloWorld"
git init -q && git add . && git commit -qm base
sed -i 's/  "name"/    "name"/' HelloWorld.Program.netpc.json   # break canonical form
$NP format --check .; echo "rc=$?"                            # not canonical: …, rc=1
$NP format . && git diff --quiet && echo canonical-again
$NP show HelloWorld.Program.netpc.json | head                # class HelloWorld.Program public …
$NP git-install --merge --command "$NP"; $NP git-install --merge --command "$NP"   # second run: already installed
git diff HEAD --textconv -- HelloWorld.Program.netpc.json    # summary lines, not JSON
cd - >/dev/null
```

Merge scenario (SC-009): `tests/NetPrints.Cli.Tests/Git/GitDriversEndToEndTests` builds two branches adding
different nodes to `Main` and shows the driver merging them with 0 conflicts.

## 3. Catalog tool (US2, contracts/catalog.md)

```bash
dotnet build tests/Fixtures/Catalog/CatalogFixtureLib -c Release -v q
$NP catalog --assembly tests/Fixtures/Catalog/CatalogFixtureLib/bin/Release/net10.0/CatalogFixtureLib.dll \
    --profile public-api --output /tmp/fixture.npcat.json; echo "rc=$?"   # wrote …, rc=0
diff /tmp/fixture.npcat.json tests/NetPrints.Catalog.Tests/Snapshots/public-api.npcat.json && echo same
$NP catalog --assembly …/CatalogFixtureLib.dll --output /tmp/fixture.npcat.json --check; echo "rc=$?"  # rc=0
$NP catalog --assembly …/CatalogFixtureLib.dll --exclude "Fixture.Geometry.*" \
    --output /tmp/fixture.npcat.json --check; echo "rc=$?"                   # stale: …, rc=1
```

Editor and build consumption: `ExtensionCatalogTests` (Core.Tests) and `CatalogSearchTests` (Editor.Tests) with
the `fx.catalog` fixture extension (CT-T15).

## 4. Annotations (US3, contracts/annotations.md)

```bash
tests/NetPrints.Catalog.Tests/bin/Release/net10.0/NetPrints.Catalog.Tests -class '*CrossFlavorSnapshotTests'
unzip -l local-packages/NetPrints.Annotations.*.nupkg | grep -E 'analyzers/dotnet/cs|build/'   # after scripts/pack-local.sh
```

Expected: the tool, generator and built-assembly catalogs are byte-identical for `public-api`, `annotated` and
`fixture-flags`; the package holds the generator and the targets, and no `lib/` folder.

## 5. Extensions coexist (US5, contracts/extensions.md)

```bash
tests/NetPrints.Core.Tests/bin/Release/net10.0/NetPrints.Core.Tests -namespace 'NetPrints.Tests.Extensibility.MultiExtension'
```

Expected: MX-T01…MX-T16 pass, including the 24-permutation test and the 50-extension scale test (< 10 s).

## 6. API tracking (US6)

```bash
grep -c . src/NetPrints.Core/PublicAPI.Shipped.txt          # > 1 (v0.1.1 API)
tests/NetPrints.Core.Tests/bin/Release/net10.0/NetPrints.Core.Tests -class '*PublicApiTrackingTests' -class '*ExperimentalApiTests'
```

## 7. Docs, schemas, packages

```bash
eng/validate-schemas.sh                 # all schemas + instances pass
scripts/build-docs.sh                   # 0 broken links; website/build/schemas/{netpc.v1,npcat.v1,netprints.catalog.v1}.schema.json
scripts/pack-local.sh && scripts/verify-packages.sh local-packages <version>   # includes NetPrints.Catalog and NetPrints.Annotations
```
