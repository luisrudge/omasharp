---
name: create-omasharp-app
description: Create a new Omarchy app, omarchy-shell panel or bar widget in C# and QML with Omasharp. Use when the user asks to build, make or create an app, tool, utility or bar widget for Omarchy and there's no Omasharp app to work in yet. For an existing Omasharp app, use the Omasharp skill inside it instead.
---

# Create an Omasharp app

1. **The .NET SDK.** `dotnet --list-sdks` must list 10.0 or newer. Omarchy ships the .NET runtime but not the SDK, so `dotnet` being on the PATH proves nothing. If it's missing, ask the user to run `omarchy pkg add dotnet-sdk` (it asks for their password; don't run sudo yourself) and wait until they have.
2. **Build tools:** `qmake6`, `g++` and `make`, which Omarchy ships. If `g++` or `make` is missing, ask the user to run `omarchy pkg add base-devel`.
3. **The template:** `dotnet new install Omasharp.Templates`. Running it again updates it, so run it every time.
4. **Window app, panel or bar widget.** A window app, unless the user wants:
   - an icon in the bar, a menubar or tray app, or a status in the bar that opens a dropdown: `--kind bar-widget`;
   - a panel inside omarchy-shell that opens from a key binding, with no icon in the bar: `--kind panel`.
5. **Create it:** `dotnet new omarchy-app -n <Name>` (with the `--kind` from step 4) in the current folder (ask where if that's the home directory), then `cd <Name>`. `<Name>` is PascalCase; the app id defaults to it in lowercase, and `--app-id` sets another.
6. **Read the new app's `.agents/skills/omasharp/SKILL.md` and follow it from here.** It covers `Backend.cs`, `Main.qml`, tests and checking the app without opening windows on the user's screen. Don't run `dotnet run` or `dotnet watch`: they open a window and never return.
7. **When `check.sh --release` passes, install it** with `dotnet publish -t:Install`: the user asked for a working app, so that counts as being asked. Tell them where to find it: a window app in the app launcher (Super+Space, then its name), a bar widget's icon in the bar (on the right), a panel with `omarchy-shell shell toggle <appid>`.

To update this skill, download it again from https://raw.githubusercontent.com/luisrudge/omasharp/main/skills/create-omasharp-app/SKILL.md to the same place.
