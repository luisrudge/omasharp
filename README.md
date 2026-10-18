# omasharp

C# + QML apps for [Omarchy](https://omarchy.org) that start and feel like Omarchy's own apps (omacalc, omacut).

```sh
dotnet new omarchy-app -n Tally
```

gives you two files to edit: `Backend.cs` (your logic) and `Main.qml` (your UI). `dotnet publish` compiles the C# with native AOT and puts it next to a small Qt host, so the app runs on the system Qt with no .NET installed. Or pick `--kind panel` for an omarchy-shell panel, where the same `Backend.cs` runs in its own process so a crash can't take the shell down.

Each new app also contains an agent skill (`.agents/skills/omasharp/`) for coding agents: how to build, check (screenshots without touching your screen), and install the app.

Status: early (0.x). Linux x64, Omarchy 4 (Hyprland, Qt 6.7+). Not affiliated with Omarchy or Basecamp.

## Why

C++ and qmake fit small Omarchy utilities. C# earns its place when an app has real logic: NuGet, async I/O, memory safety, readable compiler errors (good for agents), and code you can share with other platforms. The usual price is a slow-starting, memory-hungry app. Here it isn't.

Omarchy's calculator, omacalc, in its C++ original and ported to C# on omasharp. Medians of 7 interleaved runs; [details](https://github.com/luisrudge/omasharp/blob/main/examples/omacalc).

| | omacalc (C++) | omasharp (C#) |
|---|---|---|
| Time to window | 618 ms | 593 ms |
| Memory (PSS) | 74.6 MB | 77.8 MB |
| Idle wake-ups | 3/s | 3/s |
| Needs .NET installed to run | no | no |

![omacalc: C++ and omasharp](https://raw.githubusercontent.com/luisrudge/omasharp/main/examples/omacalc/vs-cpp.png)

## Quick start

1. The .NET SDK: `omarchy pkg add dotnet-sdk` (or Omarchy menu › Install › Development › .NET).
2. The template: `dotnet new install Omasharp.Templates` ([from source](https://github.com/luisrudge/omasharp/blob/main/CONTRIBUTING.md#try-your-changes) until it's on nuget.org).
3. An app:

   ```sh
   dotnet new omarchy-app -n Tally            # or --kind panel
   cd Tally
   dotnet watch                               # runs it; QML and C# edits apply live
   dotnet publish -t:Install                  # ~/.local/bin/tally + a launcher entry
   ```

Building needs only what Omarchy ships plus the SDK: qmake6, g++ and Qt 6. Running a published app needs nothing beyond Qt.

## What you get

- **The bridge**: public C# properties and methods appear in QML as `backend.*`, with bindings, async methods as Promises, your own records and lists as JS objects, and C#→QML events. Background threads are safe. [How it works](https://github.com/luisrudge/omasharp/blob/main/docs/architecture.md).
- **Omarchy's look**: `theme.*` in QML and `Theme` in C# follow the current Omarchy theme live; apps use the Omarchy font; the app id is yours for window rules.
- **Fast custom drawing**: `Drawing` in C# + `DrawView` in QML render charts, maps and treemaps natively through Qt's scene graph.
- **A dev loop**: `dotnet watch` reloads QML in ~0.1 s and hot-reloads C# in ~1.5 s, including new properties and methods.
- **Mistakes caught at build time**: warnings fail the build, and omasharp's analyzers check `Backend` against what QML can do: names QML can't take, members it can't see, setters that never tell it, types that can't cross. Each error says the fix.
- **Checks that agents can run**: the skill's `check.sh` runs the unit tests, starts the app in a private, invisible compositor, prints every visible text and saves a screenshot. Typing keys into it: `--keys "12{Tab}3{Enter}"`.
- **Your bridge, your code**: the bridge is copied into each app (like shadcn components). Change it if you need to; `dotnet build -t:UpdateBridge` brings in newer versions and keeps your changes.

![One Backend.cs as a window app and as a panel](https://raw.githubusercontent.com/luisrudge/omasharp/main/examples/hello/screenshot.png)

## This repository

| | |
|---|---|
| `template/` | the `omarchy-app` template, exactly what a new app gets: the starter files, `bridge/` (C# side, C++ host, build targets) and the agent skill (`.agents/skills/omasharp/`: `SKILL.md`, `references/`, `scripts/check.sh`) |
| `analyzers/` | `Omasharp.Analyzers`, the compile-time checks every app gets ([rules](https://github.com/luisrudge/omasharp/blob/main/analyzers/README.md)) |
| `pack/` | the template package (`Omasharp.Templates`) |
| `examples/` | [hello](https://github.com/luisrudge/omasharp/blob/main/examples/hello) (one Backend.cs as a window app and as a panel) and [omacalc](https://github.com/luisrudge/omasharp/blob/main/examples/omacalc) (Omarchy's calculator ported to C#) |
| `scripts/test.sh` | everything CI checks |
| `tools/` | benchmarking helpers for contributors |
| `docs/` | [architecture](https://github.com/luisrudge/omasharp/blob/main/docs/architecture.md) |

## Contributing

Issues and pull requests are welcome, from people and from their agents. See [CONTRIBUTING.md](https://github.com/luisrudge/omasharp/blob/main/CONTRIBUTING.md).

## License

MIT. See [LICENSE](https://github.com/luisrudge/omasharp/blob/main/LICENSE). The omacalc example keeps omacalc's own MIT license and its font's OFL; see [examples/omacalc/README.md](https://github.com/luisrudge/omasharp/blob/main/examples/omacalc/README.md).
