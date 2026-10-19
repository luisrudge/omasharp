# Changelog

One section per `bridge/VERSION`. Apps update with `dotnet build -t:UpdateBridge`; entries marked **ABI** change the C ABI (`bridge/host/omasharp.h` ⟷ `bridge/cs/Native.cs`), so an app that customized either file must merge both before it runs again.

## 0.1.3

Fixes from porting disktiles to the published template:

- `dotnet new omarchy-app --app-id <id>` (it was `--appId`).
- The generated `CLAUDE.md` is `@AGENTS.md` plus the skill symlink step, and no longer asks to be deleted, which also lost `AGENTS.md`.
- CA1716 is off in `.editorconfig`: a record named `Step` or an interface method named `Next` no longer fails the build over Visual Basic keywords.
- `check.sh` tiles the window to fill the screen, as Omarchy does, instead of floating it at its own size and clipping a big one. `--float` keeps the old way for apps that float on Omarchy, and warns when the window is larger than the screen. `--env KEY=VALUE` sets an environment variable for the app, in the private compositor and with `--offscreen`.
- `DrawView` draws its shapes under Qt's software renderer (`--offscreen`, no GPU), not only its text.
- `Theme.Name` in C# and `theme.name` in window QML: the theme's name from `current/theme.name`, following theme switches.
- `-t:Install` uses the app's own `app.desktop` when it has one (`@EXEC@` becomes the installed binary; Install sets `StartupWMClass` and `Icon`) and checks it with `desktop-file-validate`. Install and Uninstall refresh the launcher's caches.
- The skill: `grabToImage` and the window's color, and what QtQuick.Controls costs at startup (about 10 ms).
- `tools/bench.sh`: runs at the same time no longer measure, close or kill each other's apps, and each result names the window's class.

`UpdateBridge` brings in the bridge and skill changes. It doesn't touch `CLAUDE.md` or `.editorconfig`, so an existing app makes those two edits by hand if it wants them. An app that customized `Omasharp.targets`, `Theme.cs`, `host/theme.cpp`, `host/drawview.cpp` or the skill's scripts merges the new versions from the `.upstream` files. The C ABI is unchanged.

## 0.1.2

The skill covers SQLite (`references/sqlite.md`). Apps use the system's `libsqlite3`, which Qt depends on, through `SQLitePCLRaw.core` and `SQLitePCLRaw.provider.sqlite3`, with a small `Db.cs` helper. The database goes in `$XDG_DATA_HOME/<AppId>/`, and rows are mapped by hand, without reflection ORMs. It adds about 50 KB and ships no native file, so it works in window apps and panels, before and after `-t:Install`. `references/backend.md` now explains why packages with their own native `.so`, such as the SQLite that `Microsoft.Data.Sqlite` bundles, fail in installed window apps. `scripts/test.sh` runs the reference's code in an installed AOT window app and in a panel. The bridge itself is unchanged.

## 0.1.1

The omasharp logo: the left half of Omarchy's logo as a C, and a sharp on the same grid. It's the icon of both packages and heads the README (`assets/`). Nothing changed for apps.

## 0.1.0

First release (after 0.0.1-rc.1, a test release): the bridge (window apps over the C ABI, panels over JSON lines), the `omarchy-app` template with the `omasharp` agent skill (`check.sh`: unit tests, then the app run in a private compositor, with its visible text and a screenshot), `UpdateBridge`, `Drawing`/`DrawView`, `Theme`, and async methods as Promises. Apps build with warnings as errors, .NET's recommended analyzers and the native AOT ones on every build, and omasharp's own analyzers (the `Omasharp.Analyzers` package, OMASHARP001–010) checking the backend against what QML can do. The bridge calls `Dispose()` on an `IDisposable` backend when the app exits. Panels use `Commons.Color` for the shell's palette, which Qt 6.12 requires.
