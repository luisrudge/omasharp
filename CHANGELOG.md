# Changelog

One section per `bridge/VERSION`. Apps update with `dotnet build -t:UpdateBridge`; entries marked **ABI** change the C ABI (`bridge/host/omasharp.h` ⟷ `bridge/cs/Native.cs`), so an app that customized either file must merge both before it runs again.

## 0.1.9

Omarchy 4.5 (checked against v4.5.0-rc1, its shell and its Quickshell 0.3.2 and Qt 6.12; window apps, panels and bar widgets run unchanged):

- The bar has more sections: `left`, `centerBegin`, `center`, `centerEnd`, `right`, and `tray` (the tray's drawer). `center` is now the clock's box, and a widget put there moves the clock off center, so Install's message names `omarchy bar move <appid> --section left|centerBegin|centerEnd|right|tray`, and `references/bar-widgets.md` lists the sections and says to put a widget beside the clock with `centerBegin` or `centerEnd`. The starter's `defaultSection` stays `right`, which every Omarchy 4 release takes.
- Omarchy's rule for plugins is that text they show is plain text: `Text`'s default reads markup-shaped text as rich text, which can load a remote image inside the shell. The starter's `Panel.qml` and `BarWidget.qml` set `textFormat: Text.PlainText` on the text that shows the backend's values (so does the hello example's panel), and `references/look.md` says to do the same.

`UpdateBridge` brings in the new message and skill text. The starter's QML is the app's own, so an existing panel or bar widget adds `textFormat: Text.PlainText` itself, to every `Text` that shows backend values.

## 0.1.8

Bar widgets: `dotnet new omarchy-app --kind bar-widget` makes an icon in the Omarchy bar that opens a dropdown, like Omarchy's own Tailscale and Power widgets. The starter is the panel's counter: the icon shows the count, the dropdown has the label and the buttons, and a right click counts.

- The plugin has two entry points. `plugin/BarWidget.qml` is the icon (`BarIconButton`) and the dropdown (`KeyboardPanel`), once per monitor. `plugin/Service.qml` runs the backend once per session, and every monitor's icon gets it with `bar.shell.serviceFor(bar.pluginId)`, so they all show the same state. The backend runs from when the widget is enabled until it's disabled or the shell restarts.
- `Omasharp.targets` treats `bar-widget` like `panel` everywhere (publish into `plugin/bin/`, `Backend.qml`, dev mode, `OmasharpLaunch`, Install and Uninstall), and fails the build for an `OmarchyKind` that isn't `window`, `panel` or `bar-widget`. Install says where the icon went and how to move it (`omarchy bar move <appid> --section left|center|right`).
- Install deletes the installed backend before copying the new one. A running backend made the copy fail with "Text file busy": always for a bar widget, and for a panel with `keepLoaded` or one that was open.
- Dev mode and `check.sh`: `bridge/shell/dev-bar.qml` shows the widget in a stand-in bar at the top of a private screen, with the shell's own `PluginBarApi`, and opens the dropdown; closing it ends the run. `--keys` reach the dropdown, and `--env OMASHARP_BAR_POSITION=left` (or `bottom`, `right`) checks a vertical bar. The visible-text dump moved out of `dev-shell.qml` into `bridge/shell/TextDump.qml`, which both harnesses use; a panel's output is unchanged.
- The skill: a new `references/bar-widgets.md` (the icon, the dropdown, clicks, the backend's lifetime, placement, vertical bars, checking), and `SKILL.md`, `panels.md`, `build.md`, `checking.md`, `backend.md` and `look.md` mention bar widgets. The `create-omasharp-app` skill picks `--kind bar-widget` for a bar, menubar or tray icon app.

`UpdateBridge` brings in the bridge and skill changes; window apps and panels build and run as before. A panel app that customized `Omasharp.targets` must merge its `.upstream` before its dev mode and `check.sh` work again: the new `shell/dev-shell.qml` needs `TextDump.qml` next to it in `obj/panel-dev/`, and only the new targets copy it there (the merge also brings the `_OmasharpShellPlugin` property, which replaces the `'$(OmarchyKind)' == 'panel'` conditions, and Install's delete). One that customized `shell/dev-shell.qml` keeps working, with the text dump inline. The host's source changed only in a comment (`host/textdump.cpp`), so the cached host is built once more.

## 0.1.7

The name is capitalized in prose now, Omasharp, like Omarchy: the README, docs, the skill, comments, the analyzers package's title and description, and the hello example's title. Names that code or tools use don't change: the `Omasharp` namespace, the package IDs, the `.agents/skills/omasharp/` folder and the skill's name, paths, app ids, and the `omasharp:` prefix on log messages.

`UpdateBridge` brings in the new comments and skill text. Nothing changes in how apps build or run.

## 0.1.6

Fixes from agents building system-monitor apps:

- `check.sh --size` keeps the 1.5x scale for every size. An odd width or height has no whole-pixel mode at 1.5x, so Hyprland ran the app at 1x and the screenshot and layout were wrong. The check now rounds odd numbers down (`--size 625x751` runs at 624x750) and says so, and fails if the private screen doesn't end up at 1.5x.
- `check.sh` no longer runs the app against the user's real data. The app gets empty `XDG_DATA_HOME`, `XDG_CONFIG_HOME`, `XDG_STATE_HOME` and `XDG_CACHE_HOME` folders in `obj/omasharp/xdg/` (the unit tests and the build get the first three), emptied at the start of every check and again before the app starts, in the private compositor, for panels and with `--offscreen`. A check's `--keys` used to land in the installed app's database. `--seed DIR` copies `DIR/data`, `config`, `state` and `cache` in first; `--real-data` turns it off; `--env` can't set the four variables. `HOME` stays, so the theme still applies, and the user's fonts and fontconfig folder are linked in. The skill (`SKILL.md`, `references/checking.md`, `sqlite.md`, `panels.md`) says so.
- `Drawing` draws lines and filled areas, so line and area charts no longer need columns of `Rect`s: `Line(x1, y1, x2, y2, width, color)`, `Polyline(points, width, color)` (corners mitred, bevelled when sharper than 60°, so steep data draws no gaps or spikes) and `Area(points, baselineY, color)` (filled from the points down to the baseline, split where it crosses). Points are a `ReadOnlySpan<(double X, double Y)>`, so a collection expression, an array or a `stackalloc` works and nothing is allocated per point. They draw on the GPU and under the software renderer (`--offscreen`). `references/drawing.md` has a CPU history graph, and says how to smooth slanted lines (`layer.samples` on the `DrawView`).
- **Buffer format**: the `Drawing` buffer (`ODL1`) gains op 5 (Polyline; `Line` is one with two points: float width, uint32 color, int32 n, n float x/y pairs) and op 6 (Area: float baseline, uint32 color, int32 n, n pairs). Ops 1–4 are unchanged, and the C ABI too.

`UpdateBridge` brings in the bridge and skill changes. An app that customized `check.sh` or `shot.sh` merges their `.upstream` files. One that customized `cs/Drawing.cs` or `host/drawview.cpp` merges both: a host without ops 5 and 6 stops reading a drawing at its first line or area, so everything drawn after it is missing.

## 0.1.5

`dotnet watch` is no longer supported: no C# hot reload and no QML live reload. Apps are built by agents, which edit and run `check.sh`; reloading was code their loop never used, and it made dev mode differ from release. Dev mode itself stays (a Debug build on JIT, QML from the project folder, no `.rcc`): `check.sh` uses it by default, and `dotnet run` opens the app with it, without reloading.

- **ABI** (`OMASHARP_ABI` 3 → 4): `omasharp_host` loses `method` (a method appeared on Backend), and `HostApi` in `Native.cs` loses `Method`. The host makes `backend`'s methods once at start and always freezes the property map, in dev mode too.
- `Dev.cs` keeps the managed `Main` and drops the `MetadataUpdateHandler`; `Native.cs` drops `Reload()`, and `Model.cs` drops `Detach()` and `NotifyAll()`, which only it used.
- The window host no longer watches QML files (`watchQml` in `host/devhost.cpp`), and the panel harness (`shell/dev-shell.qml`) no longer reloads when `plugin/` changes, so it no longer needs `inotifywait`.
- `Omasharp.targets` drops `StartupHookSupport`, which only hot reload needed, and the `Watch="false"` item for QML.
- The template's next-steps message, the README, the skill (`SKILL.md`, `references/build.md`) and `docs/architecture.md` say `dotnet run` (to look at the app) and `check.sh` (for agents) instead of `dotnet watch`.

`UpdateBridge` brings in the bridge and skill changes. This is an ABI change: the host and the C# side must both come from 0.1.5, or the host refuses to start the app. An app that customized `host/omasharp.h` or `cs/Native.cs` removes the `method`/`Method` field from both (keeping the two struct orders identical) and bumps `OMASHARP_ABI`/`Abi` to 4 in both. One that customized `host/main.cpp` removes `onMethod`, `addMethod` and the static `makeMethod`/`self`, makes the methods once in `omasharp_host_run`, calls `map.freeze()` unconditionally and drops the `watchQml` declaration and call. One that customized `host/devhost.cpp` removes `watchQml` and `qmlFiles`. Customized `Dev.cs`, `Omasharp.targets` or `shell/dev-shell.qml` merge their `.upstream` files.

## 0.1.4

Window apps can be shared with one command, no sudo and no .NET:
`curl -fsSL https://github.com/<owner>/<repo>/releases/latest/download/install.sh | sh` (`sh -s uninstall` removes it, and running it again updates).

- `dotnet publish -t:Package` makes a release in `artifacts/`: `<AppId>-linux-x64.tar.gz`, `SHA256SUMS` and `install.sh` (the new `bridge/install.sh`, with the app's values).
- The template has `.github/workflows/release.yml` (window apps) and a `<Version>`. A `v<Version>` tag runs the unit tests, builds the app on Omarchy's stable mirror (the host needs the Qt it was built with or newer) and attaches the three files to a GitHub release.
- `install.sh` checks the download against `SHA256SUMS`, checks with `ldd` that the system's libraries are new enough (if not, it installs nothing and says to update), swaps the new copy in so a running one keeps working, and runs one install of an app at a time.
- `-t:Install` and `-t:Uninstall` run that same `install.sh`, on the files a release would have, in the same places as before. The launcher's cache refresh moved from `desktop.sh` into it.
- `references/build.md` explains it ("Sharing a window app").

`UpdateBridge` brings in the bridge and skill changes. An existing app copies `.github/workflows/release.yml` from a new app, adds `<Version>` to its csproj and `artifacts/` to `.gitignore`. An app that customized `Omasharp.targets` or `desktop.sh` merges the `.upstream` files. The C ABI is unchanged.

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

The Omasharp logo: the left half of Omarchy's logo as a C, and a sharp on the same grid. It's the icon of both packages and heads the README (`assets/`). Nothing changed for apps.

## 0.1.0

First release (after 0.0.1-rc.1, a test release): the bridge (window apps over the C ABI, panels over JSON lines), the `omarchy-app` template with the `omasharp` agent skill (`check.sh`: unit tests, then the app run in a private compositor, with its visible text and a screenshot), `UpdateBridge`, `Drawing`/`DrawView`, `Theme`, and async methods as Promises. Apps build with warnings as errors, .NET's recommended analyzers and the native AOT ones on every build, and Omasharp's own analyzers (the `Omasharp.Analyzers` package, OMASHARP001–010) checking the backend against what QML can do. The bridge calls `Dispose()` on an `IDisposable` backend when the app exits. Panels use `Commons.Color` for the shell's palette, which Qt 6.12 requires.
