# bridge/

The glue between `Backend.cs` and QML. You normally don't touch it. The tools for checking an app live in the omasharp skill (`.agents/skills/omasharp/scripts/`).

| Path | What |
|---|---|
| `cs/Model.cs` | Finds Backend's public properties and methods (camelCased for QML), encodes values as JSON, turns `PropertyChanged` into notifications on the UI thread. |
| `cs/Ui.cs` | `BackendBase` (`Set`, `Notify`), `Ui.Post`, the UI-thread SynchronizationContext, `Bridge.IsNativeAot`. |
| `cs/QmlNames.cs` | How member names look in QML, and the names QML can't take. The `Omasharp.Analyzers` package compiles the same file. |
| `cs/Native.cs` | Window apps: the C ABI (`omasharp_api`) the host calls. |
| `cs/Dev.cs` | Window apps in dev mode: the managed `Main` the host runs through hostfxr; hot-reload hook. |
| `cs/Stdio.cs`, `cs/Shim.cs` | Panels: the JSON-lines protocol over stdin/stdout, and the generator for `plugin/Backend.qml`. |
| `cs/Drawing.cs`, `host/drawview.cpp` | Native drawing: C# `Drawing` + `Bridge.Show(name, drawing)` → QML `DrawView` (scene graph; view transform for zoom/pan). |
| `host/` | The C++ host (qmake + g++): `QQmlPropertyMap` for `backend`, `theme` from Omarchy's colors.toml, hostfxr dev mode, QML hot reload. Identical for every app: built once into `~/.cache/omasharp/host-<Qt version>-<hash>/` and copied as `<AppId>`. |
| `shell/dev-shell.qml` | The standalone Quickshell harness used to develop panels. |
| `Omasharp.targets` | Build glue: AOT settings, warnings as errors and the analyzers (`Omasharp.Analyzers`, same version as `VERSION`), host build, panel publish, dev mode, Install/Uninstall, and `OmasharpLaunch` (the run command the skill's `check.sh` uses). |
| `update.sh` | `dotnet build -t:UpdateBridge`: brings a newer bridge and skill from the installed template, keeping your changes. |
| `VERSION`, `MANIFEST` | The version this app has, and the fingerprints of every bridge and skill file it received. |

Window app, release: `<appid>` (the host) dlopens `lib<appid>.so` (native AOT C#), calls `omasharp_api()`, registers `<appid>.rcc` (the app's QML, fonts, images), and runs `qrc:/Main.qml`.
Window app, dev: `<appid> --dev App.dll --qml-dir .` starts .NET through hostfxr. App.dll's `Main` calls back into `omasharp_host_run`.
Panel: omarchy-shell loads `plugin/Panel.qml`. Its `Backend {}` (generated `Backend.qml`) runs `plugin/bin/<name>` and talks JSON lines to it.
