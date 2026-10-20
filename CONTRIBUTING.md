# Contributing

Bug reports, fixes and new bridge features are welcome, whether you write them or your agent does. The bridge is meant to cover what most Omarchy apps need, so "my app needed X and had to patch its bridge" is a great issue.

## Setup

- Omarchy (or Arch with Hyprland), the .NET SDK 10+ (`omarchy pkg add dotnet-sdk`), `qt6-base`, `qt6-declarative`, `gcc`, `make`, `clang`.
- Optional: `lld` (faster native links), `jq`, `grim`, `wtype` and `dbus-run-session` for `check.sh` and the tools in `tools/`.

## Layout

| | |
|---|---|
| `template/` | the `omarchy-app` template, exactly what a new app gets |
| `template/bridge/` | the bridge, the one copy in this repo. Read its `README.md` first. |
| `template/.agents/skills/omasharp/` | the agent skill: `SKILL.md`, `references/`, and `scripts/check.sh` (tests + an invisible run with screenshot and visible text) |
| `skills/create-omasharp-app/` | the global skill that starts a new app (agents fetch it from `main`, so keep it working with the released template) |
| `analyzers/` | `Omasharp.Analyzers` (the OMASHARP rules every app gets as a package; [README](analyzers/README.md)) and its tests |
| `pack/` | the template package; `dotnet pack pack -o artifacts` |
| `examples/` | apps that use `../../template/bridge` directly (no copy) |
| `scripts/test.sh` | the full check: what CI runs |
| `tools/` | benchmarking helpers |
| `docs/` | architecture |
| `assets/` | the logo and icon (also the packages' icon) |

## Try your changes

```sh
sh scripts/test.sh                                   # everything, in a private template store
dotnet run --project analyzers/Omasharp.Analyzers.Tests   # the analyzers' tests alone
dotnet pack analyzers/Omasharp.Analyzers -o artifacts && dotnet pack pack -o artifacts   # both packages
dotnet new install artifacts/Omasharp.Templates.*.nupkg   # use it for real (uninstall: dotnet new uninstall Omasharp.Templates)
cd examples/hello && sh ../../template/.agents/skills/omasharp/scripts/check.sh   # an example, checked off-screen (needs Hyprland)
```

An app made from a local package restores `Omasharp.Analyzers` from nuget.org at the same version. For a version that isn't published yet, add `artifacts/` as a package source in the app (a `NuGet.config` next to it), the way `scripts/test.sh` does.

`scripts/test.sh` runs the analyzers' tests, packs both packages into a temporary store, generates a window app, a panel app and a bar widget app, then for each:
- builds it, runs its unit tests and publishes it with native AOT;
- runs it headless and checks what it shows (the panel's and the bar widget's backends are checked through their protocol; with a Hyprland session, the bar widget also runs through `check.sh`, on a top and a left bar).

It also checks that each OMASHARP rule fails a generated app's build, and builds and tests the examples (they use the analyzers from source). It never touches your screen, your installed templates or your files.

Turn on the pre-commit hook once per clone: `git config core.hooksPath .githooks`. It runs `scripts/lint.sh` on the files you stage: C# formatting (`dotnet format whitespace`), C++ formatting (`clang-format`, style in `.clang-format`) and `shellcheck` if you have it. CI runs it on every file. Fix formatting with `dotnet format whitespace --folder` and `clang-format -i FILE`. QML isn't auto-formatted: follow the style of the files around it.

## Rules

- **The bridge has a C ABI.** `template/bridge/host/omasharp.h` and `template/bridge/cs/Native.cs` describe the same structs. Change them together, append new fields at the end, and keep both orders identical.
- **New analyzer rules must be precise.** Warnings fail every app's build, so a rule that can fire on correct code blocks real work: test the quiet cases too (`analyzers/Omasharp.Analyzers.Tests`), and list it in `AnalyzerReleases.Unshipped.md` and `analyzers/README.md`.
- **Bump `template/bridge/VERSION` for every change to the bridge or the skill.** The template package takes its version from it, and apps see it when they update their bridge.
- **Changes reach apps through `dotnet build -t:UpdateBridge`**, which keeps any file an app changed. Prefer small, additive changes, and say in the PR what an app with a customized copy would have to merge.
- **Keep it generic.** No app-specific behavior in the bridge, and nothing tied to one machine (paths, user names, monitor names, Hyprland instance ids).
- **Never show windows on the developer's screen** from tests or tools. Use the skill's `scripts/check.sh` (a private nested compositor) or Qt's `offscreen` platform.
- **Update the docs agents read.** If a change affects how apps are written, update the skill (`SKILL.md` and `references/`). If you can, try it with a fresh agent that has no context.
- **Measure performance claims** with the tools in `tools/`, alternating apps across runs ([tools/README.md](tools/README.md)).
- `sh scripts/test.sh` must pass, with zero IL2xxx/IL3xxx (trimming/AOT) warnings.

## Releases

Maintainers tag `v<template/bridge/VERSION>`. The release workflow tests, packs, attaches the `.nupkg` to a GitHub release and pushes it to nuget.org.
