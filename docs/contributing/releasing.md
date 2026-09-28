# Releasing

## Local package feed

Before relying on a NetPrints change from another project, pack it locally instead of waiting for a
release — a restore can never silently prefer or fall back to a published package, because every
local pack gets its own version:

```bash
ver=$(scripts/pack-local.sh --print-version)      # packs everything into local-packages/
scripts/verify-packages.sh local-packages "$ver"  # metadata, tool install, a fresh HelloWorld
```

`local-packages/` is git-ignored (kept only through `.gitkeep`, because NuGet fails a restore with
`NU1301` when a configured local source does not exist). Point another project at it with:

```xml
<PackageReference Include="NetPrints.Sdk" Version="0.1.0-local.20260101000000" PrivateAssets="all" />
```

and a `nuget.config` that lists the absolute path to `local-packages/`, or:

```bash
dotnet tool install -g NetPrints.Cli --version 0.1.0-local.20260101000000 --add-source /path/to/local-packages
```

## Release process

A release is a pushed tag matching `v*` (for example `v0.1.0`). `.github/workflows/release.yml`:

1. **`pack`** — restores, computes the version with MinVer, `dotnet pack`s the seven NuGet/tool
   packages, and runs `scripts/verify-packages.sh` against them.
2. **`desktop`** — publishes the self-contained editor for `linux-x64`, `win-x64` and `osx-arm64`,
   smoke-tests each with `scripts/smoke-desktop.sh`, and archives it with `scripts/archive-desktop.sh`
   (`.tar.gz` for Linux/macOS, `.zip` for Windows).
3. **`assets`** — collects every package and archive, writes `SHA256SUMS.txt`, and attaches build
   provenance attestations.
4. **`publish-nuget`** — pushes the packages to nuget.org with Trusted Publishing (OIDC; no stored
   API key). Tag pushes only.
5. **`github-release`** — creates the GitHub Release from `.github/release.yml`'s categories and
   `.github/release-notes.md`. Tag pushes only.

`workflow_dispatch` and a pull request that touches the release inputs (this workflow, the release
scripts, `eng/`, the `Directory.*.props` files or a `src/**/*.csproj`) run the same workflow as a
**dry run**: `pack`, `desktop` and `assets` run and their artifacts (including `SHA256SUMS.txt`) can
be downloaded and verified, but `publish-nuget` and `github-release` are skipped, no secrets are
read, and nothing is published. Never push a `v*` tag, run `dotnet nuget push`, or set
`PUBLISH_DOCS`/`PUBLISH_WIKI` to test this — the dry run already exercises the whole pipeline.

The docs site (this site, plus the [API reference](https://danielmeza.github.io/netprints/api/)) and
the wiki build the same way, from `.github/workflows/docs.yml` and `.github/workflows/wiki.yml`, but
publish independently of package releases — see the owner steps below.

## Owner one-time steps

None of these are needed to open or merge a PR: every workflow tolerates them being undone. The
gated jobs either skip (an unset `vars.*`) or fail with a message naming the missing step (an unset
`NUGET_USER`).

1. **nuget.org Trusted Publishing** — on nuget.org, add a Trusted Publishing policy for owner
   `danielmeza`, repository `netprints`, workflow file `release.yml`, environment `release`. Then set
   the repository (or `release` environment) secret `NUGET_USER` to the nuget.org profile name (not
   the email). Optionally reserve the `NetPrints.` package ID prefix.
2. **`release` environment** — create it under Settings → Environments, optionally with required
   reviewers and a `v*` deployment tag rule.
3. **GitHub Pages** — Settings → Pages → Source = *GitHub Actions*; then set the repository variable
   `PUBLISH_DOCS` = `true`. The next push to `master` (or a manual *Docs* run) deploys the site, and
   from then on the `$schema` URL in generated `.netpc` files resolves.
4. **Wiki** — enable it under Settings → Features → Wikis (restricted to collaborators), create its
   first page in the web UI, then set `PUBLISH_WIKI` = `true`.
5. **Labels** — `gh label create breaking-change --color B60205 --repo danielmeza/netprints` and
   `gh label create ignore-for-release --color EDEDED --repo danielmeza/netprints`.
6. **First release** (after this work is merged, not part of it) — `git tag v0.1.0 && git push origin v0.1.0`.
