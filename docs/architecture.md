# How Omasharp works

An app is `Backend.cs` (C#) and `Main.qml` (QML). The bridge connects them in one of two ways, depending on where the QML runs.

Paths below are as they are in an app; in this repo, they're under `template/`.

```
window app                                    panel (omarchy-shell plugin)

  <appid>            the host: C++, Qt          omarchy-shell (Quickshell)
   ├─ <appid>.rcc    your QML, packed              └─ plugin/Panel.qml + generated Backend.qml
   └─ lib<appid>.so  your C#, native AOT                  │ JSON lines over stdin/stdout
        one process, C ABI between them                   ▼
                                               plugin/bin/<Name>   your C#, native AOT
                                               a separate process: if it crashes, the shell doesn't
```

`Backend.cs` is the same in both. Nothing in it knows which transport it runs on.

## The C# side (`bridge/cs/`)

- `Model.cs` reflects over `Backend`: public instance properties and methods, camelCased for QML. Values cross the boundary as JSON in both transports, written and read with `Utf8JsonWriter`/`JsonDocument` (no reflection-based serializer). Your own classes and records become JS objects; to keep their properties under trimming, the build roots the app's own assembly. `INotifyPropertyChanged` becomes notifications, always delivered on the UI thread.
- `Ui.cs`: `BackendBase` (`Set`, `Notify`, `Raise`), `Ui.Post`, a `SynchronizationContext` so `await` returns to the UI thread, and `Bridge` (`Args`, `Show`, `IsNativeAot`).
- `Drawing.cs` builds a packed buffer of shapes for `DrawView`. `Theme.cs` reads Omarchy's `colors.toml` and `theme.name` for C#.
- Transports, chosen at compile time: `Native.cs` (`OMASHARP_NATIVE`, window apps), `Dev.cs` (window apps in Debug), `Stdio.cs` + `Shim.cs` (`OMASHARP_STDIO`, panels and bar widgets). The unused ones compile away, which is also why unit tests can compile the whole bridge.

## Window apps: the host (`bridge/host/`)

The host is one small Qt program, the same binary for every app. It's built once per machine, Qt version and host source (`~/.cache/omasharp/host-<qt>-<hash>/`) and copied in as `<appid>`. On start it:

1. `dlopen`s `lib<appid>.so` next to itself and calls `omasharp_api()`, which returns a table of function pointers (`host/omasharp.h` ⟷ `cs/Native.cs`).
2. Sets the Wayland app_id (`QGuiApplication::setDesktopFileName`) from the app's `AppId`, so Hyprland window rules match it.
3. Calls `start(host)`. C# constructs `Backend` and returns a description of its properties and methods.
4. Builds `backend`: a `QQmlPropertyMap` holding every property (bindings work), plus one JS function per method that calls through the C ABI. Writes from QML go to C# and come back as whatever C# ended up with.
5. Registers `<appid>.rcc` (the app's QML, fonts and images) and loads `qrc:/Main.qml`. It also exposes `theme` (Omarchy's colors and theme name, live) and the `DrawView` type.

On exit (`aboutToQuit`) the host calls `shutdown`, which runs the backend's `Dispose()` if it's `IDisposable`; the bridge hides `Dispose` from QML.

Threading: C# runs on Qt's main thread. A notification raised on another thread is posted (`QMetaObject::invokeMethod`, queued) and delivered on the main thread.

Async methods: a `Task`/`Task<T>` result makes the host return a JS Promise. C# calls `resolve(id, value)` when the task completes.

`DrawView` (`host/drawview.cpp`) turns a `Drawing` buffer into scene-graph nodes:
- all rectangles, outlines, hatching, lines and areas go into one vertex-colored geometry (one draw call); lines are a quad per segment with mitred corners, bevelled when sharper than 60°;
- text becomes native text nodes, grouped by color, with a layout cache.

`view: [scale, x, y]` maps the drawing's view to the current one with a transform, so zoom and pan don't redraw anything until C# sends a fresh drawing.

Startup choices that matter (measured):
- QML loads from a `.rcc`, which the QML disk cache handles.
- Omarchy's `QT_QPA_PLATFORMTHEME=gtk3` is swapped for the portal theme, about 55 ms faster (`OMASHARP_PLATFORMTHEME` overrides this).
- Updates are pushed synchronously when the app is already on the UI thread, instead of queued behind animation frames.

## Panels and bar widgets

The panel's QML runs inside omarchy-shell, which loads plugins from `~/.config/omarchy/plugins/<id>/`. Loading .NET into the shell would put the whole desktop at risk, so the C# runs as a child process instead:

- `Stdio.cs`, protocol (one JSON object per line):
  - shell → backend: `get`, `set`, `call`;
  - backend → shell: `hello` (every value, plus method names), `notify`, `result`, `error`, `raised`.
  - stdout is reserved for the protocol; `Console.Out` goes to stderr. stdin closing means the shell is gone: the backend's `Dispose()` runs (if it's `IDisposable`), and it exits.
- `Shim.cs` generates `plugin/Backend.qml` at build time:
  - one typed QML property per C# property and one function per method (returning a Promise);
  - the `Process` plumbing.

  Panel QML therefore reads `backend.count` and calls `backend.increment()` exactly like window QML. It adds `ready`, `running`, `lastError` and `restart()`.

A crash in the backend ends that process only: the panel shows `lastError` and can `restart()`. Calls to a backend that isn't running reject immediately.

A bar widget (`--kind bar-widget`) is the same kind of plugin with two entry points (`plugin/manifest.json`):
- `barWidget` (`BarWidget.qml`): the bar loads it once per monitor, as an icon (`BarIconButton`) with a dropdown (`KeyboardPanel`), both from the shell's `qs.Ui`. The bar hands it `bar`, a facade with the bar's colors, position and size.
- `service` (`Service.qml`): the shell creates it once while the plugin is enabled, and its `Backend { }` runs the backend for the whole session. Widgets get it with `bar.shell.serviceFor(bar.pluginId)`, so every monitor's icon shows the same state; until then, each uses a stand-in `Backend` that never runs, so `backend` is never null.

Disabling the plugin ends the service and its backend; Install deletes the running binary before copying the new one (a copy over it fails with "Text file busy").

## Dev mode

A Debug build is dev mode: no native AOT compile and no `.rcc`, so it builds fast. `check.sh` uses it by default, and `dotnet run` opens it. The same host runs with `--dev App.dll --qml-dir <project>`:
- The host starts the normal .NET runtime through hostfxr, with `hostfxr_initialize_for_dotnet_command_line` + `hostfxr_run_app`, so the app's managed `Main` (`Dev.cs`) runs as in `dotnet App.dll`.
- `Main` calls back into the host's exported `omasharp_host_run`, and Qt runs on the main thread as in release.
- QML loads from the project folder instead of a `.rcc`.
- Nothing reloads while the app runs; the backend's members are fixed at start, as in release.
- Panels in dev mode run in their own Quickshell process (`bridge/shell/dev-shell.qml`, with the shell's `qs.Commons`/`qs.Ui`), never inside omarchy-shell, with the backend on JIT.
- Bar widgets run the same way in `bridge/shell/dev-bar.qml`: one `Service.qml`, a stand-in bar (a `PanelWindow` strip on the screen's edge, `OMASHARP_BAR_POSITION`, top by default) whose `bar` is the shell's own `PluginBarApi` with the theme's bar colors, and the widget at the strip's far end with its dropdown open. Closing the dropdown ends the run. Both harnesses write the visible text with `bridge/shell/TextDump.qml`.

## Checks: the skill's `check.sh` (`.agents/skills/omasharp/scripts/`)

`shot.sh` never touches the user's screen:
- It starts a private nested Hyprland with a minimal config (no autostart, nothing exported to the session). Windows tile and fill its screen, as on Omarchy; `--float` floats them at their own size.
- That compositor's window sits on a hidden special workspace of the user's session; no monitor is added.
- The app runs inside it on its own headless output, on a private D-Bus session that starts no services (`shot-dbus.conf`): no notifications, portal dialogs or browser tabs reach the user's desktop. Network and child processes are not sandboxed, and files only as far as the XDG dirs go: `check.sh` gives the app (and the tests and build before it) empty `XDG_DATA_HOME`/`CONFIG`/`STATE`/`CACHE` folders in `obj/omasharp/xdg/`, filled from `--seed DIR` and emptied before every run (`--real-data` turns this off). `HOME` stays, for the theme; the user's fonts and fontconfig are symlinked in.
- The app's arguments, `--env` variables and `--keys` reach it through files, never through a shell. `keys.py` types into it with wtype (`{Tab}`, `{Ctrl+a}`, `{Wait:500}`); the key input stays inside the nested compositor.
- It saves `obj/shot.png` and fails if the app didn't start or died. `check.sh` also fails when the run log has a JS error or `Unable to assign` on a line that points at the app's own QML (`file://<app>/`, `qrc:/`, `@plugin/`), so errors in Qt's or the shell's components don't count. While it runs, the host (and the shell harnesses) writes every visible text to `obj/shot.txt`, so a check can read values, not just pixels (password fields only as their masked text).

`check.sh` = unit tests (`tests/`), a dev build (or `--release`: the native AOT publish, failing on IL warnings; every build fails on any warning), then `shot.sh`. The build target `OmasharpLaunch` writes the command that runs the build to `obj/omasharp/launch`, so the script never has to work out paths. `--offscreen` runs a window app on Qt's offscreen platform instead (visible text only; CI).

## Analyzers (`analyzers/`, the `Omasharp.Analyzers` package)

What QML can do with the backend is decided by the bridge at run time (`Model.cs`), so most mistakes used to show up as a value that never updates or a member that's `undefined`. `Omasharp.Analyzers` checks the same things at compile time, OMASHARP001–010 (see [analyzers/README.md](../analyzers/README.md)):
- The backend is a class deriving from `BackendBase`, or the app's `BackendType` (passed to the compiler as a `CompilerVisibleProperty`).
- Names: `bridge/cs/QmlNames.cs` is compiled into both the bridge and the analyzer, so they can't disagree about what QML can't take.
- Types: the analyzer's list of what can cross mirrors `Json.Convert` (QML → C#) and the few things `Json.Write` can't send (C# → QML).
- A `DiagnosticSuppressor` turns off CA1822 ("can be static") on the backend's public members.
- `Omasharp.targets` turns on warnings as errors, `AnalysisLevel=10.0-recommended` (pinned, so a newer SDK can't add rules that break existing apps), the AOT/trim analyzers on every build, and references the package at the bridge's version. Every app setting has an override (`OmasharpWarningsAsErrors`, `OmasharpAnalyzers`, ...). In this repo the examples use the analyzer project instead of the package (`OmasharpAnalyzersProject`).

Because every warning fails a build, a rule only fires when it's sure: OMASHARP004/005 only count state the backend's own code changes outside its constructor (lambdas count as later), and any `nameof(X)`/`"X"` or notify-everything call in the class counts as notifying X.

## The bridge and the skill as source (`bridge/VERSION`, `bridge/MANIFEST`)

The template copies the whole bridge and the skill (`.agents/skills/omasharp/`) into every app; they're the app's code. `MANIFEST` records the sha256 of every file as shipped, by its path in the app. `dotnet build -t:UpdateBridge` (`bridge/update.sh`) gets both from the installed template:
- files the app never changed are replaced;
- changed files are kept, with the new version saved as `<file>.upstream` plus a diff;
- files added or removed upstream follow.

Within one app, the C# side and the host always come from the same copy, so the C ABI only needs to agree with itself. Changing it means changing `omasharp.h` and `Native.cs` together and bumping `VERSION`.
