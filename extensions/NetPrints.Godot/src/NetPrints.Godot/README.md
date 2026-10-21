# NetPrints for Godot

A sample NetPrints extension. It lives outside `NetPrints.slnx` and builds only against the packed NetPrints packages (research R26), so it shows what an extension author gets from NuGet.

It contributes:

- the `godot` project profile (Godot.NET.Sdk, graph classes partial so Godot can attach them as scripts);
- the `Godot project` template, with `Properties/launchSettings.json` holding `Executable` profiles for the game and the Godot editor (`%GODOT4%`, `--path .`), noted as running the Debug build;
- the `Check Godot setup` command, a settings section with its generated page, and a status bar item that says where the Godot executable comes from.

No Godot installation is needed to build or test it.

## Build and test

Pack the NetPrints packages once, then build against them:

```
scripts/pack-local.sh
dotnet test --project extensions/NetPrints.Godot/tests/NetPrints.Godot.Tests
```

`Directory.Packages.props` floats `NetPrintsPackageVersion` over `0.1.0-local.*`, so the newest packed version is used; pass `-p:NetPrintsPackageVersion` with the version to pin one.

CI packs the packages in the `packages` job and calls the reusable `netprints-extension.yml` workflow on this folder. A second job unpacks the extension package and loads it in the editor host (`GodotExtensionHostTests`).
